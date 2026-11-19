using HorrorGame.AI;
using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Player
{
    /// <summary>
    /// Plays a footstep every stride and reports each one as a gameplay <see cref="Noise"/>, so monsters
    /// hear walking nearby and sprinting from much farther. Landing from a jump is loud too.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Stride length (metres per step)")]
        [SerializeField] float walkStride = 1.7f;
        [SerializeField] float sprintStride = 2.3f;

        [Header("How far monsters hear each step (metres)")]
        [SerializeField] float walkNoise = 5f;
        [SerializeField] float sprintNoise = 13f;
        [SerializeField] float landingNoise = 9f;

        [Header("Audio")]
        [Range(0f, 1f)] [SerializeField] float walkVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] float sprintVolume = 0.6f;

        PlayerMotor motor;
        AudioSource source;
        AudioClip clip;
        float distanceSinceStep;
        bool wasGrounded = true;
        float airborneSpeed;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            clip = ProceduralAudio.GetClip(ProceduralSound.Footstep);
        }

        void Update()
        {
            bool grounded = motor.IsGrounded;
            Vector3 velocity = motor.Velocity;
            if (!grounded)
                airborneSpeed = Mathf.Min(airborneSpeed, velocity.y);

            if (grounded && !wasGrounded && airborneSpeed < -4f)
            {
                Step(landingNoise, sprintVolume);
                distanceSinceStep = 0f;
            }
            if (grounded)
                airborneSpeed = 0f;
            wasGrounded = grounded;
            if (!grounded)
                return;

            float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            if (speed < 0.5f)
            {
                distanceSinceStep = Mathf.Min(distanceSinceStep, walkStride * 0.6f); // first step comes quickly
                return;
            }
            distanceSinceStep += speed * Time.deltaTime;
            bool sprinting = motor.IsSprinting;
            float stride = sprinting ? sprintStride : walkStride;
            if (distanceSinceStep < stride)
                return;
            distanceSinceStep -= stride;
            Step(sprinting ? sprintNoise : walkNoise, sprinting ? sprintVolume : walkVolume);
        }

        void Step(float noiseRadius, float volume)
        {
            source.pitch = Random.Range(0.85f, 1.1f);
            source.PlayOneShot(clip, volume);
            Noise.Emit(transform.position, noiseRadius, gameObject);
        }
    }
}
