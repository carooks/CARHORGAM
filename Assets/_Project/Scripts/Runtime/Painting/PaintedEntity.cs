using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Painting.Strokes;
using HorrorGame.Player;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The thing that stands in the painting when the choir stops singing. It obeys the painting's
    /// rule: it can only move where the world has not been painted yet, which means wherever you
    /// have not looked for a moment (see <see cref="PaintAttention"/>). Look at it and the paint
    /// around it finishes and it is pinned in place. Look away and it comes.
    ///
    /// It is only out while it is <see cref="Hunting"/>: the <see cref="Choir"/> calls
    /// <see cref="Emerge"/> when the song stops and <see cref="Submerge"/> when it starts again.
    /// It does not chase. It walks slowly to the last sound it heard (footsteps, a burning easel),
    /// stops there to listen, then wanders on. Standing still and quiet lets it pass you; it only
    /// kills someone it reaches while they are still making noise. Faster with every voice freed.
    ///
    /// It is blind: the painter painted its eyes over in white, which is why it goes by sound. When
    /// the choir falls silent the only voice left is its own, one sour note held against the chord,
    /// so you can hear where it is. It never animates: it snaps into a new pose whenever nobody is
    /// looking (<see cref="EntityPoser"/>), leaves wet black drips where it walks, and its strokes
    /// never dry.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class PaintedEntity : MonoBehaviour
    {
        [Tooltip("Below this much paint in its direction, it may move.")]
        [Range(0f, 1f)] [SerializeField] float moveBelowPaint = 0.5f;
        [Tooltip("A slow walk. It never runs.")]
        [SerializeField] float baseSpeed = 1.5f;
        [Tooltip("Added to the speed for every voice freed.")]
        [SerializeField] float speedPerBurn = 0.3f;
        [Tooltip("Reach. Bigger than the distance it stops at to listen, so it can grab a noisy player it walks up to.")]
        [SerializeField] float killDistance = 1.7f;
        [Tooltip("It only kills someone who made a sound this recently (seconds). Freeze and it passes you.")]
        [SerializeField] float quietGrace = 1.5f;
        [Tooltip("Seconds it stands listening where it heard the last sound before wandering on.")]
        [SerializeField] float listenTime = 2.5f;
        [Tooltip("How far behind the player it emerges.")]
        [SerializeField] float emergeDistance = 13f;
        [Tooltip("Distance at which dread (smear, red bleed) starts building.")]
        [SerializeField] float dreadDistance = 16f;
        [SerializeField] float banishTime = 2.2f;
        [Tooltip("Seconds between pose snaps while it walks unseen.")]
        [SerializeField] Vector2 poseInterval = new Vector2(0.6f, 1.2f);
        [Tooltip("Snaps pose while unseen. Optional.")]
        [SerializeField] EntityPoser poser;
        [Tooltip("Where its drips are painted. Optional.")]
        [SerializeField] BrushStrokeWorld strokes;
        [Tooltip("Its one sour note, a tritone against the choir's chord.")]
        [SerializeField] float humPitch = 0.707f;
        [Range(0f, 1f)] [SerializeField] float humVolume = 0.55f;

        NavMeshAgent agent;
        AudioSource breath;
        AudioSource voice;
        AudioSource hum;
        float poseTimer;
        bool listening;
        Vector3 lastDrip;
        int dripSide = 1;
        Renderer[] bodyRenderers;
        Transform target;
        PlayerHealth targetHealth;
        Vector3 homeScale;
        int burns;
        bool active;
        bool banishing;
        Vector3 goal;
        bool hasGoal;
        float listenTimer;
        float lastTargetNoiseTime = -100f;
        float banishProgress;

        public PaintedRealm Realm { get; set; }

        /// <summary>True while it is out in the field, hunting.</summary>
        public bool Hunting { get; private set; }
        /// <summary>True while it is allowed to move this frame (the world in its direction is unfinished).</summary>
        public bool CanMove { get; private set; }
        public bool IsBanished { get; private set; }
        public float Speed => baseSpeed + burns * speedPerBurn;
        /// <summary>Where it is walking: the last sound it heard, or a wander point.</summary>
        public Vector3 Goal => goal;
        public EntityPoser Poser => poser;

        public void Configure(EntityPoser bodyPoser, BrushStrokeWorld strokeWorld)
        {
            poser = bodyPoser;
            strokes = strokeWorld;
        }

        void OnEnable() => Noise.Emitted += OnNoise;
        void OnDisable() => Noise.Emitted -= OnNoise;

        void OnNoise(NoiseEvent noise)
        {
            if (!Hunting)
                return;
            if (Vector3.Distance(Flat(noise.Position), Flat(transform.position)) > noise.Radius)
                return;
            HearAt(noise.Position);
            if (target != null && noise.Source != null && noise.Source.transform == target)
                lastTargetNoiseTime = Time.time;
        }

        /// <summary>It heard something at <paramref name="position"/> and walks there.</summary>
        public void HearAt(Vector3 position)
        {
            goal = position;
            hasGoal = true;
            listenTimer = 0f;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
            homeScale = transform.localScale;
            bodyRenderers = GetComponentsInChildren<Renderer>(true);

            breath = gameObject.AddComponent<AudioSource>();
            breath.clip = ProceduralAudio.GetClip(ProceduralSound.MonsterBreath);
            breath.loop = true;
            breath.spatialBlend = 1f;
            breath.minDistance = 1f;
            breath.maxDistance = 14f;
            breath.rolloffMode = AudioRolloffMode.Linear;
            breath.pitch = 0.8f;
            breath.playOnAwake = false;

            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0.6f;
            voice.maxDistance = 40f;
            voice.playOnAwake = false;

            // The discord: one voice held a tritone off the chord, thick and torn.
            // On its own child: audio filters affect every source on their object.
            var humObject = new GameObject("Hum");
            humObject.transform.SetParent(transform, false);
            humObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            hum = humObject.AddComponent<AudioSource>();
            hum.clip = ProceduralAudio.GetClip(ProceduralSound.ChoirVoice);
            hum.loop = true;
            hum.spatialBlend = 1f;
            hum.minDistance = 2f;
            hum.maxDistance = 32f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.pitch = humPitch;
            hum.volume = humVolume;
            hum.playOnAwake = false;
            var torn = humObject.AddComponent<AudioDistortionFilter>();
            torn.distortionLevel = 0.55f;
            var buried = humObject.AddComponent<AudioLowPassFilter>();
            buried.cutoffFrequency = 2200f;

            if (poser == null)
                poser = GetComponent<EntityPoser>();
            SetVisible(false);
        }

        /// <summary>The player has entered: it knows who to hunt, but stays hidden until it emerges.</summary>
        public void Activate(GameObject player)
        {
            target = player.transform;
            targetHealth = player.GetComponent<PlayerHealth>();
            if (IsBanished)
                return;
            active = true;
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.speed = Speed;
            agent.acceleration = 40f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.2f;
            agent.updateRotation = false;
            agent.isStopped = true;
        }

        public void Deactivate()
        {
            active = false;
            Submerge();
        }

        /// <summary>Another voice joined the chord: it is angrier, and walks faster from now on.</summary>
        public void Agitate()
        {
            burns++;
            if (agent.enabled)
                agent.speed = Speed;
        }

        /// <summary>Stands up at a given spot (the finale, in front of the altar).</summary>
        public void EmergeAt(Vector3 position)
        {
            if (!active || IsBanished)
                return;
            if (agent.enabled)
                agent.Warp(position);
            else
                transform.position = position;
            Hunting = true;
            SetVisible(true);
            FaceTarget();
            SnapPose(EntityPoser.Pose.Stretch);
            breath.Play();
            StartHum();
        }

        /// <summary>
        /// Stands up out of the wet paint. With <paramref name="behindTarget"/> it appears in the
        /// unfinished paint behind the player; otherwise where it is.
        /// </summary>
        public void Emerge(bool behindTarget, bool shriek)
        {
            if (!active || IsBanished)
                return;
            if (behindTarget)
                MoveBehindTarget();
            Hunting = true;
            // It starts toward where the player was when the song stopped, in case they were moving.
            if (target != null)
                HearAt(target.position);
            SetVisible(true);
            FaceTarget();
            SnapPose(EntityPoser.Pose.Stretch);
            poseTimer = poseInterval.y;
            breath.Play();
            StartHum();
            if (shriek)
                voice.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Screech), 1f);
        }

        /// <summary>Sinks back into the paint and stops hunting.</summary>
        public void Submerge()
        {
            Hunting = false;
            CanMove = false;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            breath.Stop();
            hum.Stop();
            if (Realm != null && Realm.Filter != null)
                Realm.Filter.Dread = 0f;
            if (!banishing)
                SetVisible(false);
        }

        void MoveBehindTarget()
        {
            if (target == null)
                return;
            Vector3 back = -target.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f)
                back = Vector3.back;
            back.Normalize();
            Vector3 wanted = target.position + back * emergeDistance + Vector3.Cross(Vector3.up, back) * Random.Range(-3f, 3f);
            if (NavMesh.SamplePosition(wanted, out var hit, 4f, NavMesh.AllAreas))
                wanted = hit.position;
            if (agent.enabled)
                agent.Warp(wanted);
            else
                transform.position = wanted;
        }

        public void Banish()
        {
            if (IsBanished)
                return;
            IsBanished = true;
            banishing = true;
            active = false;
            Hunting = false;
            SetVisible(true);
            if (agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
            breath.Stop();
            hum.Stop();
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

            if (!active || !Hunting || target == null)
                return;

            float distance = Vector3.Distance(Flat(transform.position), Flat(target.position));
            if (Realm != null && Realm.Filter != null)
                Realm.Filter.Dread = Mathf.InverseLerp(dreadDistance, killDistance, distance);

            var attention = Realm != null ? Realm.Attention : null;
            CanMove = attention == null || attention.At(transform.position + Vector3.up) < moveBelowPaint;

            if (CanMove)
            {
                Walk();
                UpdatePose(distance);
                bool heardYou = Time.time - lastTargetNoiseTime <= quietGrace;
                if (distance <= killDistance && heardYou && targetHealth != null && targetHealth.IsAlive)
                {
                    active = false;
                    Hunting = false;
                    agent.isStopped = true;
                    breath.Stop();
                    hum.Stop();
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

        /// <summary>Walks to the last sound, listens there, then wanders to a nearby spot.</summary>
        void Walk()
        {
            if (!hasGoal)
                PickWanderPoint();
            float toGoal = Vector3.Distance(Flat(transform.position), Flat(goal));
            if (toGoal <= 1.0f)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                listenTimer += Time.deltaTime;
                if (!listening)
                {
                    listening = true;
                    SnapPose(EntityPoser.Pose.Listen);
                }
                if (listenTimer >= listenTime)
                    PickWanderPoint();
                return;
            }
            listening = false;
            agent.isStopped = false;
            LeaveDrips();
            agent.SetDestination(goal);
            Vector3 v = agent.desiredVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v.normalized), 180f * Time.deltaTime);
        }

        /// <summary>While nobody is looking it keeps snapping into new poses, never seen in between.</summary>
        void UpdatePose(float distanceToTarget)
        {
            if (poser == null || listening)
                return;
            poseTimer -= Time.deltaTime;
            if (poseTimer > 0f)
                return;
            poseTimer = Random.Range(poseInterval.x, poseInterval.y);
            SnapPose(EntityPoser.NextWalkingPose(poser.Current, distanceToTarget, () => Random.value));
        }

        void SnapPose(EntityPoser.Pose pose)
        {
            if (poser == null)
                return;
            bool close = target != null && Vector3.Distance(transform.position, target.position) < 14f;
            poser.Snap(pose, close ? target : null);
            // A wet slap of paint when it shifts close by: you hear it move, you never see it.
            if (close && Hunting && voice != null)
                voice.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.HeavyStep), 0.35f);
        }

        void StartHum()
        {
            if (hum != null && !hum.isPlaying)
            {
                hum.pitch = humPitch;
                hum.Play();
            }
        }

        /// <summary>Wet black drips where it walks, alternating feet.</summary>
        void LeaveDrips()
        {
            if (strokes == null)
                return;
            Vector3 here = transform.position;
            if ((here - lastDrip).sqrMagnitude < 0.45f * 0.45f)
                return;
            lastDrip = here;
            dripSide = -dripSide;
            Vector3 side = transform.right * (0.16f * dripSide);
            strokes.AddDrip(here + side + Vector3.up * 0.01f, transform.forward);
            strokes.AddDrip(here + side * 0.6f + transform.forward * 0.12f + Vector3.up * 0.01f, transform.forward, 0.6f);
        }

        void PickWanderPoint()
        {
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(6f, 12f);
            Vector3 wanted = transform.position + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(wanted, out var hit, 4f, NavMesh.AllAreas))
                wanted = hit.position;
            goal = wanted;
            hasGoal = true;
            listenTimer = 0f;
        }

        void SetVisible(bool visible)
        {
            if (bodyRenderers == null)
                return;
            foreach (var r in bodyRenderers)
                if (r != null)
                    r.enabled = visible;
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
