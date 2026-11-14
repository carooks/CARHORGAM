using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.AI
{
    /// <summary>
    /// Plays a rigged model's animations to match the brain's state, using the legacy Animation
    /// component that glTFast puts on imported .glb models. Clip names are typed in the Inspector, so a
    /// re-exported model with different or extra clips only needs the names changed.
    /// Playback speed follows how fast the monster actually moves, to limit foot sliding.
    /// An empty or missing clip falls back sensibly (see each field), and an attack without its own clip
    /// becomes a quick procedural lunge.
    /// </summary>
    [RequireComponent(typeof(MonsterBrain))]
    public class MonsterAnimation : MonoBehaviour
    {
        [Tooltip("The imported model (child object). Its Animation component is found automatically.")]
        [SerializeField] Transform model;

        [Header("Clip names (as listed on the model's Animation component)")]
        [Tooltip("Standing still. Empty: the patrol clip, frozen.")]
        [SerializeField] string idleClip = "";
        [SerializeField] string patrolClip = "Elderly_Shaky_Walk";
        [SerializeField] string investigateClip = "Walking";
        [SerializeField] string chaseClip = "Running";
        [Tooltip("Empty: keeps the chase clip and lunges forward procedurally.")]
        [SerializeField] string attackClip = "";

        [Header("Ground speed (m/s) at which each clip plays at normal speed")]
        [SerializeField] float patrolClipSpeed = 1.0f;
        [SerializeField] float investigateClipSpeed = 1.9f;
        [SerializeField] float chaseClipSpeed = 5.0f;
        [SerializeField] float crossFade = 0.25f;

        [Header("Procedural lunge (used when there is no attack clip)")]
        [SerializeField] float lungeAngle = 28f;

        MonsterBrain brain;
        NavMeshAgent agent;
        Animation anim;
        string current;
        Quaternion modelRestRotation;

        void Awake()
        {
            brain = GetComponent<MonsterBrain>();
            agent = GetComponent<NavMeshAgent>();
            if (model == null)
                return;
            modelRestRotation = model.localRotation;
            anim = model.GetComponentInChildren<Animation>(true);
            if (anim == null)
            {
                Debug.LogWarning($"[Horror Game] {name}: the model has no Animation component, so it won't animate. " +
                                 "Import the .glb with glTFast's Animation set to Legacy.", this);
                return;
            }
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState state in anim)
                state.wrapMode = WrapMode.Loop;
        }

        void Update()
        {
            if (anim == null)
                return;

            float speed = agent != null && agent.enabled ? new Vector3(agent.velocity.x, 0f, agent.velocity.z).magnitude : 0f;
            bool moving = speed > 0.15f;
            string clip;
            float clipSpeed;
            switch (brain.State)
            {
                case MonsterState.Chase:
                    clip = chaseClip;
                    clipSpeed = chaseClipSpeed;
                    break;
                case MonsterState.Attack:
                    clip = HasClip(attackClip) ? attackClip : chaseClip;
                    clipSpeed = 0f;
                    break;
                case MonsterState.Investigate:
                    clip = investigateClip;
                    clipSpeed = investigateClipSpeed;
                    break;
                default:
                    clip = patrolClip;
                    clipSpeed = patrolClipSpeed;
                    break;
            }

            float playbackSpeed;
            if (brain.State == MonsterState.Attack)
            {
                playbackSpeed = HasClip(attackClip) ? 1f : 0.15f;
            }
            else if (!moving)
            {
                if (HasClip(idleClip))
                {
                    clip = idleClip;
                    playbackSpeed = 1f;
                }
                else
                {
                    playbackSpeed = 0.1f; // nearly frozen mid-stride: an uneasy sway
                }
            }
            else
            {
                playbackSpeed = Mathf.Clamp(speed / Mathf.Max(0.1f, clipSpeed), 0.3f, 1.8f);
            }

            if (HasClip(clip) && clip != current)
            {
                anim.CrossFade(clip, crossFade);
                current = clip;
            }
            if (current != null && HasClip(current))
                anim[current].speed = playbackSpeed;
        }

        void LateUpdate()
        {
            if (model == null)
                return;
            // Lunge: lean forward through the wind-up, snap upright after the strike.
            float lean = 0f;
            if (brain.State == MonsterState.Attack && !HasClip(attackClip))
                lean = Mathf.Clamp01(brain.TimeInState / 0.5f) * (brain.TimeInState < 0.8f ? 1f : 0f);
            model.localRotation = modelRestRotation * Quaternion.Euler(lean * lungeAngle, 0f, 0f);
        }

        bool HasClip(string clip) => anim != null && !string.IsNullOrEmpty(clip) && anim[clip] != null;
    }
}
