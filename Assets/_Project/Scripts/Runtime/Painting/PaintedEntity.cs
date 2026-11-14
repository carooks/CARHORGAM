using HorrorGame.Audio;
using HorrorGame.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The thing that lives in the painting. It obeys the painting's one rule: it can only move
    /// where the world has not been painted yet, which means wherever you have not looked for a
    /// couple of seconds (see <see cref="PaintAttention"/>). Look at it and the paint around it
    /// finishes and it is pinned in place. Turn away and the world goes wet behind you, and it comes.
    ///
    /// It walks a runtime NavMesh toward the player, kills on contact, and every time an anchor is
    /// burned it shrieks, moves faster and reappears in the wet paint behind the player.
    /// The body is a placeholder built by the editor; swap in a model as a child.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PaintedEntity : MonoBehaviour
    {
        [Tooltip("Below this much paint in its direction, it may move.")]
        [Range(0f, 1f)] [SerializeField] float moveBelowPaint = 0.5f;
        [SerializeField] float baseSpeed = 3.6f;
        [Tooltip("Added to the speed for every anchor burned.")]
        [SerializeField] float speedPerBurn = 1.1f;
        [SerializeField] float killDistance = 1.4f;
        [Tooltip("How far behind the player it reappears after an anchor burns.")]
        [SerializeField] float reappearDistance = 7f;
        [Tooltip("Distance at which dread (smear, red bleed) starts building.")]
        [SerializeField] float dreadDistance = 16f;
        [SerializeField] float banishTime = 2.2f;

        NavMeshAgent agent;
        AudioSource breath;
        AudioSource voice;
        Transform target;
        PlayerHealth targetHealth;
        Vector3 home;
        Quaternion homeRotation;
        Vector3 homeScale;
        int burns;
        bool active;
        bool banishing;
        float banishProgress;

        public PaintedRealm Realm { get; set; }

        /// <summary>True while it is allowed to move this frame (the world in its direction is unfinished).</summary>
        public bool CanMove { get; private set; }
        public bool IsBanished { get; private set; }
        public float Speed => baseSpeed + burns * speedPerBurn;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            home = transform.position;
            homeRotation = transform.rotation;
            homeScale = transform.localScale;

            breath = gameObject.AddComponent<AudioSource>();
            breath.clip = ProceduralAudio.GetClip(ProceduralSound.MonsterBreath);
            breath.loop = true;
            breath.spatialBlend = 1f;
            breath.minDistance = 1f;
            breath.maxDistance = 12f;
            breath.rolloffMode = AudioRolloffMode.Linear;
            breath.pitch = 0.8f;
            breath.playOnAwake = false;

            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0.6f;
            voice.maxDistance = 40f;
            voice.playOnAwake = false;
        }

        public void Activate(GameObject player)
        {
            target = player.transform;
            targetHealth = player.GetComponent<PlayerHealth>();
            if (!IsBanished)
            {
                active = true;
                agent.enabled = true;
                agent.Warp(transform.position);
                agent.speed = Speed;
                agent.acceleration = 40f;
                agent.angularSpeed = 720f;
                agent.stoppingDistance = 0.2f;
                agent.updateRotation = false;
                breath.Play();
            }
        }

        public void Deactivate()
        {
            active = false;
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
            breath.Stop();
        }

        /// <summary>An anchor burned: faster, louder, and suddenly behind the player.</summary>
        public void Enrage()
        {
            if (!active || IsBanished)
                return;
            burns++;
            agent.speed = Speed;
            voice.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Screech), 1f);
            ReappearBehindTarget();
        }

        public void ReappearBehindTarget()
        {
            if (target == null)
                return;
            Vector3 back = -target.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f)
                back = Vector3.back;
            Vector3 wanted = target.position + back.normalized * reappearDistance
                             + Vector3.Cross(Vector3.up, back.normalized) * Random.Range(-2f, 2f);
            if (NavMesh.SamplePosition(wanted, out var hit, 4f, NavMesh.AllAreas))
                wanted = hit.position;
            if (agent.enabled)
                agent.Warp(wanted);
            else
                transform.position = wanted;
            FaceTarget();
        }

        public void Banish()
        {
            if (IsBanished)
                return;
            IsBanished = true;
            banishing = true;
            active = false;
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
            breath.Stop();
            voice.pitch = 0.6f;
            voice.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Screech), 1f);
        }

        void Update()
        {
            if (banishing)
            {
                // Melts into the ground like paint washed off with turpentine.
                banishProgress += Time.deltaTime / banishTime;
                transform.localScale = new Vector3(homeScale.x * (1f + banishProgress * 0.8f),
                    homeScale.y * Mathf.Max(0.02f, 1f - banishProgress), homeScale.z * (1f + banishProgress * 0.8f));
                if (banishProgress >= 1f)
                {
                    banishing = false;
                    gameObject.SetActive(false);
                }
                return;
            }

            if (!active || target == null || Realm == null)
                return;

            float distance = Vector3.Distance(Flat(transform.position), Flat(target.position));
            if (Realm.Filter != null)
                Realm.Filter.Dread = Mathf.InverseLerp(dreadDistance, killDistance, distance);

            var attention = Realm.Attention;
            CanMove = attention == null || attention.At(transform.position + Vector3.up) < moveBelowPaint;

            if (CanMove)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
                FaceTarget();
                if (distance <= killDistance && targetHealth != null && targetHealth.IsAlive)
                {
                    active = false;
                    agent.isStopped = true;
                    breath.Stop();
                    voice.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Screech), 1f);
                    targetHealth.TakeHit(targetHealth.MaxHealth * 10f, gameObject);
                }
            }
            else
            {
                // Pinned by the finished paint: stops dead mid-stride.
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        void FaceTarget()
        {
            if (target == null)
                return;
            Vector3 d = Flat(target.position) - Flat(transform.position);
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
