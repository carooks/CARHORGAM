using HorrorGame.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.AI
{
    /// <summary>
    /// How a monster sounds and looks as its state changes: heavy positional footsteps that speed up
    /// when it runs, a breathing loop you hear before you see it, a shriek when it starts chasing,
    /// and eyes that glow brighter and redder the more alert it is. Reads the brain, never drives it.
    /// </summary>
    [RequireComponent(typeof(MonsterBrain))]
    public class MonsterPresentation : MonoBehaviour
    {
        [Header("Footsteps")]
        [SerializeField] float strideLength = 1.6f;
        [Range(0f, 1f)] [SerializeField] float stepVolume = 0.9f;
        [Tooltip("Footsteps fade out completely at this distance.")]
        [SerializeField] float stepHearingDistance = 22f;

        [Header("Voice")]
        [Range(0f, 1f)] [SerializeField] float breathVolume = 0.55f;
        [SerializeField] float breathHearingDistance = 9f;
        [Range(0f, 1f)] [SerializeField] float screechVolume = 1f;
        [SerializeField] float screechHearingDistance = 40f;

        [Header("Eyes")]
        [Tooltip("Renderers whose emission shows alertness. Assigned by the scene builder.")]
        [SerializeField] Renderer[] eyes = System.Array.Empty<Renderer>();
        [SerializeField] Color calmEyes = new Color(0.9f, 0.55f, 0.15f) * 0.6f;
        [SerializeField] Color alertEyes = new Color(1f, 0.85f, 0.2f) * 2f;
        [SerializeField] Color huntingEyes = new Color(1f, 0.08f, 0.04f) * 6f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MonsterBrain brain;
        NavMeshAgent agent;
        AudioSource stepSource;
        AudioSource voiceSource;
        AudioSource breathSource;
        AudioClip stepClip;
        MaterialPropertyBlock block;
        float distanceSinceStep;

        void Awake()
        {
            brain = GetComponent<MonsterBrain>();
            agent = GetComponent<NavMeshAgent>();
            block = new MaterialPropertyBlock();
            stepClip = ProceduralAudio.GetClip(ProceduralSound.HeavyStep);
            stepSource = CreateSource("Steps", 0.2f, stepHearingDistance);
            voiceSource = CreateSource("Voice", 1.9f, screechHearingDistance);
            breathSource = CreateSource("Breath", 1.9f, breathHearingDistance);
            breathSource.clip = ProceduralAudio.GetClip(ProceduralSound.MonsterBreath);
            breathSource.loop = true;
            breathSource.volume = breathVolume;
        }

        void OnEnable() => brain.StateChanged += OnStateChanged;
        void OnDisable() => brain.StateChanged -= OnStateChanged;

        void Start()
        {
            breathSource.timeSamples = Random.Range(0, breathSource.clip.samples);
            breathSource.Play();
        }

        void Update()
        {
            float speed = agent.enabled ? new Vector3(agent.velocity.x, 0f, agent.velocity.z).magnitude : 0f;
            if (speed > 0.3f)
            {
                distanceSinceStep += speed * Time.deltaTime;
                float stride = brain.State == MonsterState.Chase ? strideLength * 1.4f : strideLength;
                if (distanceSinceStep >= stride)
                {
                    distanceSinceStep -= stride;
                    stepSource.pitch = Random.Range(0.85f, 1.05f);
                    float volume = brain.State == MonsterState.Chase ? stepVolume : stepVolume * 0.75f;
                    stepSource.PlayOneShot(stepClip, volume);
                }
            }

            // Breathing gets faster and harsher when it hunts.
            float targetPitch = brain.State == MonsterState.Chase || brain.State == MonsterState.Attack ? 1.5f : 1f;
            breathSource.pitch = Mathf.MoveTowards(breathSource.pitch, targetPitch, Time.deltaTime);

            UpdateEyes();
        }

        void OnStateChanged(MonsterState previous, MonsterState next)
        {
            if (next == MonsterState.Chase && previous != MonsterState.Attack)
                voiceSource.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Screech), screechVolume);
        }

        void UpdateEyes()
        {
            if (eyes.Length == 0)
                return;
            Color color = brain.State switch
            {
                MonsterState.Chase or MonsterState.Attack => huntingEyes,
                MonsterState.Investigate => alertEyes,
                _ => Color.Lerp(calmEyes, alertEyes, brain.Awareness),
            };
            block.SetColor(EmissionColorId, color);
            foreach (var eye in eyes)
                if (eye != null)
                    eye.SetPropertyBlock(block);
        }

        AudioSource CreateSource(string sourceName, float height, float maxDistance)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
