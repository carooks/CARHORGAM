using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// One member of the choir: a head buried to the neck in the field, singing one note of the chord.
    /// The <see cref="Choir"/> tells it where to look and whether to sing. Its voice is a positional
    /// sound, so you can hear where each of them is.
    /// </summary>
    public class ChoirSinger : MonoBehaviour
    {
        [Tooltip("The part that turns to look (the head).")]
        [SerializeField] Transform head;
        [Tooltip("The open mouth; it stretches open and shut with the song.")]
        [SerializeField] Transform mouth;
        [Tooltip("Pitch of this voice relative to 220 Hz, e.g. 1, 1.19, 1.5, 2 for a minor chord.")]
        [SerializeField] float pitch = 1f;
        [Range(0f, 1f)] [SerializeField] float volume = 0.55f;

        AudioSource source;
        Vector3 mouthScale;
        float phase;
        float detune;
        bool singing;

        public bool Singing => singing;
        public Transform Head => head != null ? head : transform;

        public float Pitch
        {
            get => pitch;
            set => pitch = value;
        }

        void Awake()
        {
            if (head == null)
                head = transform;
            if (mouth != null)
                mouthScale = mouth.localScale;
            phase = Random.value * 10f;
            source = gameObject.AddComponent<AudioSource>();
            source.clip = ProceduralAudio.GetClip(ProceduralSound.ChoirVoice);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.pitch = pitch;
        }

        /// <summary>Starts or stops singing. Stopping is instant: the whole choir cuts out at once.</summary>
        public void SetSinging(bool sing)
        {
            if (sing == singing)
                return;
            singing = sing;
            if (sing)
            {
                source.timeSamples = Random.Range(0, source.clip.samples);
                source.Play();
            }
            else
            {
                source.Stop();
            }
        }

        /// <summary>0 = in tune; 1 = badly sour. Each voice drifts its own way, so the chord curdles.</summary>
        public void SetDetune(float amount) => detune = amount;

        /// <summary>Turns the head toward <paramref name="point"/> at <paramref name="degreesPerSecond"/>.</summary>
        public void LookToward(Vector3 point, float degreesPerSecond)
        {
            Vector3 d = point - Head.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f)
                return;
            var wanted = Quaternion.LookRotation(d.normalized, Vector3.up);
            Head.rotation = Quaternion.RotateTowards(Head.rotation, wanted, degreesPerSecond * Time.deltaTime);
        }

        void Update()
        {
            phase += Time.deltaTime;
            if (source != null)
            {
                float sour = Mathf.Sin(phase * 0.7f + pitch * 13f) * 0.045f * detune;
                source.pitch = pitch * (1f + sour);
            }
            if (mouth != null)
            {
                float open = singing ? 0.75f + 0.25f * Mathf.Sin(phase * 1.3f) : 0.08f;
                mouth.localScale = new Vector3(mouthScale.x, mouthScale.y * open, mouthScale.z);
            }
        }
    }
}
