using HorrorGame.AI;
using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Glass
{
    /// <summary>
    /// A drifting ghost for the glass house. It floats in straight lines, through open gaps and through
    /// panes marked <see cref="GlassPane.GhostsPassThrough"/> (green etching), but never through plain
    /// glass or walls. It drifts toward a player when it has a way through; touching one makes it wail
    /// (loud enough to bring the Foreman) and vanish for a while. It does no damage by itself.
    ///
    /// Has no collider on purpose: players and monsters walk through it.
    /// </summary>
    public class GlassGhost : MonoBehaviour
    {
        [Tooltip("World-space box it roams inside.")]
        [SerializeField] Bounds roamArea = new Bounds(new Vector3(9f, 1.3f, 9f), new Vector3(17f, 0.1f, 17f));
        [SerializeField] float speed = 1.1f;
        [SerializeField] float hoverHeight = 1.3f;
        [SerializeField] float bodyRadius = 0.25f;
        [SerializeField] float touchRadius = 0.9f;
        [Tooltip("Chance, each time it picks a new goal, that it goes for a player it can reach.")]
        [Range(0f, 1f)] [SerializeField] float huntChance = 0.6f;
        [SerializeField] float vanishSeconds = 8f;
        [Tooltip("How far monsters hear the wail, in metres.")]
        [SerializeField] float wailNoiseRadius = 22f;
        [SerializeField] Renderer[] renderers = System.Array.Empty<Renderer>();

        static readonly RaycastHit[] Hits = new RaycastHit[16];

        AudioSource voice;
        Vector3 goal;
        DetectableTarget hunting;
        float nextThinkAt;
        float hiddenUntil = -1f;
        float bobPhase;

        public bool IsHidden => hiddenUntil >= 0f;
        public Vector3 Goal => goal;

        void Awake()
        {
            if (renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();
            foreach (var c in GetComponentsInChildren<Collider>())
                Destroy(c);
            voice = gameObject.AddComponent<AudioSource>();
            voice.clip = ProceduralAudio.GetClip(ProceduralSound.MonsterBreath);
            voice.loop = true;
            voice.spatialBlend = 1f;
            voice.pitch = 1.7f; // a thin, airy whisper rather than the Foreman's rasp
            voice.volume = 0.35f;
            voice.minDistance = 1f;
            voice.maxDistance = 9f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.Play();
            bobPhase = Random.value * 10f;
            goal = transform.position;
        }

        void Update()
        {
            if (IsHidden)
            {
                if (Time.time >= hiddenUntil)
                    Reappear();
                return;
            }

            if (Time.time >= nextThinkAt)
            {
                nextThinkAt = Time.time + 0.3f;
                Think();
            }

            Vector3 position = transform.position;
            Vector3 flatGoal = new Vector3(goal.x, position.y, goal.z);
            Vector3 next = Vector3.MoveTowards(position, flatGoal, speed * Time.deltaTime);
            next.y = hoverHeight + Mathf.Sin((Time.time + bobPhase) * 1.3f) * 0.12f;
            Vector3 heading = next - position;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), 4f * Time.deltaTime);
            transform.position = next;

            foreach (var target in DetectableTarget.All)
            {
                if (!target.IsAlive)
                    continue;
                Vector3 d = target.transform.position - transform.position;
                d.y = 0f;
                if (d.magnitude <= touchRadius)
                {
                    Touch(target);
                    break;
                }
            }
        }

        void Think()
        {
            // Keep going for the player while there's still a way through.
            if (hunting != null && hunting.IsAlive && IsPathClear(transform.position, Hover(hunting.transform.position), bodyRadius))
            {
                goal = Hover(hunting.transform.position);
                return;
            }
            hunting = null;

            bool arrived = FlatDistance(transform.position, goal) < 0.3f;
            bool blocked = !IsPathClear(transform.position, goal, bodyRadius);
            if (!arrived && !blocked)
                return;

            if (Random.value < huntChance)
            {
                foreach (var target in DetectableTarget.All)
                {
                    if (target.IsAlive && IsPathClear(transform.position, Hover(target.transform.position), bodyRadius))
                    {
                        hunting = target;
                        goal = Hover(target.transform.position);
                        return;
                    }
                }
            }

            for (int i = 0; i < 12; i++)
            {
                Vector3 candidate = RandomPointInArea();
                if (IsPathClear(transform.position, candidate, bodyRadius))
                {
                    goal = candidate;
                    return;
                }
            }
            goal = transform.position; // boxed in: wait for the glass to move
        }

        void Touch(DetectableTarget target)
        {
            voice.Stop();
            var wail = ProceduralAudio.GetClip(ProceduralSound.Screech);
            AudioSource.PlayClipAtPoint(wail, transform.position, 0.8f);
            Noise.Emit(transform.position, wailNoiseRadius, gameObject);
            hiddenUntil = Time.time + vanishSeconds;
            hunting = null;
            foreach (var r in renderers)
                if (r != null)
                    r.enabled = false;
        }

        void Reappear()
        {
            hiddenUntil = -1f;
            // Somewhere at least 6 m from every player, so it never pops in on top of someone.
            Vector3 spot = RandomPointInArea();
            for (int i = 0; i < 20; i++)
            {
                bool farEnough = true;
                foreach (var target in DetectableTarget.All)
                    if (FlatDistance(target.transform.position, spot) < 6f)
                        farEnough = false;
                if (farEnough)
                    break;
                spot = RandomPointInArea();
            }
            transform.position = spot;
            goal = spot;
            foreach (var r in renderers)
                if (r != null)
                    r.enabled = true;
            voice.Play();
        }

        Vector3 RandomPointInArea()
        {
            Vector3 min = roamArea.min, max = roamArea.max;
            return new Vector3(Random.Range(min.x, max.x), hoverHeight, Random.Range(min.z, max.z));
        }

        Vector3 Hover(Vector3 feet) => new Vector3(feet.x, hoverHeight, feet.z);

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>
        /// True if a ghost of <paramref name="radius"/> can float straight from <paramref name="from"/> to
        /// <paramref name="to"/>: nothing solid in the way except ghost-permeable glass, players and monsters.
        /// </summary>
        public static bool IsPathClear(Vector3 from, Vector3 to, float radius)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f)
                return true;
            int count = Physics.SphereCastNonAlloc(from, radius, direction / length, Hits, length, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = Hits[i].collider;
                if (collider.GetComponentInParent<DetectableTarget>() != null ||
                    collider.GetComponentInParent<MonsterBrain>() != null ||
                    collider is CharacterController)
                    continue;
                var pane = collider.GetComponentInParent<GlassPane>();
                if (pane != null && pane.GhostsPassThrough)
                    continue;
                // Anything it is already inside (a pane that slid over it) doesn't hold it there.
                if (Hits[i].distance <= 0f)
                    continue;
                return false;
            }
            return true;
        }
    }
}
