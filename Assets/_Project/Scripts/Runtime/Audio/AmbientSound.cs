using UnityEngine;

namespace HorrorGame.Audio
{
    /// <summary>
    /// Plays a looping procedural sound. Set <c>spatial</c> for a sound that comes from this
    /// object's position (a humming tank); leave it off for a sound heard everywhere (room tone).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbientSound : MonoBehaviour
    {
        [SerializeField] ProceduralSound sound = ProceduralSound.Drone;
        [Range(0f, 1f)] [SerializeField] float volume = 0.4f;
        [SerializeField] bool spatial;
        [Tooltip("Distance in metres at which a spatial sound fades out completely.")]
        [SerializeField] float maxDistance = 12f;
        [SerializeField] bool randomizePitch = true;

        void Awake()
        {
            var source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = ProceduralAudio.GetClip(sound);
            source.volume = volume;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            if (randomizePitch)
                source.pitch = Random.Range(0.94f, 1.06f);
        }

        void Start()
        {
            var source = GetComponent<AudioSource>();
            // Start at a random point so several identical sources don't play in lockstep.
            source.timeSamples = Random.Range(0, source.clip.samples);
            source.Play();
        }
    }
}
