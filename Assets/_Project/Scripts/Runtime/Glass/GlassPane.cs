using System;
using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.Glass
{
    /// <summary>
    /// One sliding pane of thick glass. Blocks players and monsters, but not sight: it lives on the
    /// built-in "Ignore Raycast" layer, which monster vision and noise occlusion skip, while the
    /// player's interaction ray (all layers) still hits it so you can bang on it.
    ///
    /// The pane slides between its closed spot (where it was placed) and an open spot
    /// (<see cref="openOffset"/> away, over its partner pane). It never crushes anyone: if a player
    /// or monster is in the way it waits until they move.
    ///
    /// Needs a BoxCollider on this object. A NavMeshObstacle (carving) keeps monsters from pathing
    /// through it, since the runtime NavMesh bake skips the Ignore Raycast layer.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class GlassPane : MonoBehaviour, IInteractable, IStrikeBarrier
    {
        /// <summary>Unity's built-in "Ignore Raycast" layer. Every collider of a pane goes on it.</summary>
        public const int Layer = 2;
        public const int LayerMask = 1 << Layer;

        [Tooltip("World-space offset from the closed spot to the open spot.")]
        [SerializeField] Vector3 openOffset = new Vector3(2.95f, 0f, 0.1f);
        [SerializeField] bool startClosed = true;
        [Tooltip("Ghosts drift straight through this pane. Its etching glows green so players can learn which.")]
        [SerializeField] bool ghostsPassThrough;
        [SerializeField] float slideSpeed = 1.1f;
        [Tooltip("Optional visual child that shudders when the pane is hit.")]
        [SerializeField] Transform shakeVisual;

        [Header("Banging")]
        [SerializeField] string bangPrompt = "Bang on the glass";
        [Tooltip("How far monsters hear a player banging on the glass, in metres.")]
        [SerializeField] float bangNoiseRadius = 24f;
        [SerializeField] float bangCooldown = 0.6f;
        [Range(0f, 1f)] [SerializeField] float bangVolume = 1f;

        static readonly Collider[] Overlaps = new Collider[8];

        BoxCollider box;
        Vector3 closedPosition;
        Vector3 shakeRest;
        AudioSource thudSource;
        AudioSource slideSource;
        float nextBangAt;
        float shakeUntil;
        float shakeStrength;

        /// <summary>Where the pane is heading: true = closed (blocking its slot).</summary>
        public bool IsClosed { get; private set; }
        /// <summary>True while sliding toward its target spot.</summary>
        public bool IsMoving { get; private set; }
        /// <summary>True while a player or monster stands in its path and it is waiting.</summary>
        public bool IsBlocked { get; private set; }
        public bool GhostsPassThrough => ghostsPassThrough;
        public Vector3 ClosedPosition => closedPosition;
        public Vector3 OpenPosition => closedPosition + openOffset;

        /// <summary>Raised when a player bangs on the pane or a monster's grab hits it.</summary>
        public event Action<GlassPane> Banged;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            closedPosition = transform.position;
            if (shakeVisual != null)
                shakeRest = shakeVisual.localPosition;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = Layer;

            thudSource = CreateSource(ProceduralSound.GlassThud, false, 18f);
            slideSource = CreateSource(ProceduralSound.MachineHum, true, 12f);
            slideSource.pitch = 0.45f;
            slideSource.volume = 0.35f;

            IsClosed = startClosed;
            transform.position = startClosed ? closedPosition : OpenPosition;
        }

        /// <summary>Set up a pane made in code (tests, or builders that add the component at runtime).</summary>
        public void Configure(Vector3 offsetToOpen, bool ghostsCanPass, Transform visual = null)
        {
            openOffset = offsetToOpen;
            ghostsPassThrough = ghostsCanPass;
            if (visual != null)
            {
                shakeVisual = visual;
                shakeRest = visual.localPosition;
            }
        }

        /// <summary>Starts sliding closed or open. <paramref name="instant"/> snaps it there (scene start, tests).</summary>
        public void SetClosed(bool closed, bool instant = false)
        {
            IsClosed = closed;
            if (instant)
            {
                transform.position = closed ? closedPosition : OpenPosition;
                IsMoving = false;
                IsBlocked = false;
                Physics.SyncTransforms();
                return;
            }
            IsMoving = (transform.position - Target).sqrMagnitude > 0.0001f;
        }

        Vector3 Target => IsClosed ? closedPosition : OpenPosition;

        void Update()
        {
            UpdateShake();
            if (!IsMoving)
            {
                if (slideSource.isPlaying)
                    slideSource.Stop();
                return;
            }

            Vector3 next = Vector3.MoveTowards(transform.position, Target, slideSpeed * Time.deltaTime);
            IsBlocked = SomeoneInTheWay(next);
            if (IsBlocked)
            {
                if (slideSource.isPlaying)
                    slideSource.Pause();
                return;
            }

            transform.position = next;
            if (!slideSource.isPlaying)
                slideSource.Play();
            if ((transform.position - Target).sqrMagnitude <= 0.0001f)
            {
                transform.position = Target;
                IsMoving = false;
                thudSource.pitch = 0.7f;
                thudSource.PlayOneShot(thudSource.clip, 0.35f); // settles into the end of its track
            }
        }

        /// <summary>True if a player or monster overlaps where the pane would be at <paramref name="position"/>.</summary>
        bool SomeoneInTheWay(Vector3 position)
        {
            Vector3 center = position + transform.rotation * box.center;
            Vector3 half = Vector3.Scale(box.size, transform.lossyScale) * 0.5f + new Vector3(0.02f, 0f, 0f);
            int count = Physics.OverlapBoxNonAlloc(center, half, Overlaps, transform.rotation, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = Overlaps[i];
                if (other.transform.IsChildOf(transform))
                    continue;
                if (other is CharacterController || other.GetComponentInParent<NavMeshAgent>() != null ||
                    other.GetComponentInParent<DetectableTarget>() != null)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ banging

        public string GetPrompt(PlayerInteractor interactor) => bangPrompt;

        public bool CanInteract(PlayerInteractor interactor) => Time.time >= nextBangAt;

        public void Interact(PlayerInteractor interactor)
        {
            nextBangAt = Time.time + bangCooldown;
            Vector3 point = interactor != null
                ? box.ClosestPoint(interactor.transform.position + Vector3.up * 1.4f)
                : transform.position + Vector3.up * 1.5f;
            Thud(point, bangVolume, 0.012f);
            Noise.Emit(point, bangNoiseRadius, interactor != null ? interactor.gameObject : gameObject);
        }

        public void OnStrikeBlocked(MonsterBrain monster, Vector3 point)
        {
            // A monster slamming into the glass is louder and shakes it harder than a fist.
            Thud(point, 1f, 0.03f);
        }

        void Thud(Vector3 point, float volume, float shake)
        {
            thudSource.transform.position = point;
            thudSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            thudSource.PlayOneShot(thudSource.clip, volume);
            shakeStrength = shake;
            shakeUntil = Time.time + 0.35f;
            Banged?.Invoke(this);
        }

        void UpdateShake()
        {
            if (shakeVisual == null)
                return;
            if (Time.time >= shakeUntil)
            {
                shakeVisual.localPosition = shakeRest;
                return;
            }
            float fade = (shakeUntil - Time.time) / 0.35f;
            float wobble = Mathf.Sin(Time.time * 90f) * shakeStrength * fade;
            shakeVisual.localPosition = shakeRest + new Vector3(0f, 0f, wobble);
        }

        AudioSource CreateSource(ProceduralSound sound, bool loop, float maxDistance)
        {
            // Child object so the thud can be moved to where the pane was hit.
            var go = new GameObject(sound + "_Audio");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = box.center;
            go.layer = Layer;
            var source = go.AddComponent<AudioSource>();
            source.clip = ProceduralAudio.GetClip(sound);
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        void OnDrawGizmosSelected()
        {
            if (box == null)
                box = GetComponent<BoxCollider>();
            Vector3 closed = Application.isPlaying ? closedPosition : transform.position;
            Gizmos.color = ghostsPassThrough ? new Color(0.3f, 1f, 0.4f, 0.8f) : new Color(0.6f, 0.8f, 1f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(closed, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.TRS(closed + openOffset, transform.rotation, Vector3.one);
            Gizmos.color *= 0.5f;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
