using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Player
{
    /// <summary>
    /// Toggleable spot light held by the player. It trails the camera slightly so it feels
    /// hand-held. Battery drain is planned for Phase 2 (resource management).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] bool startOn = true;
        [Tooltip("How tightly the beam follows the camera. Higher is stiffer; 0 disables the trailing.")]
        [SerializeField] float followSharpness = 16f;
        [Range(0f, 1f)] [SerializeField] float clickVolume = 0.5f;

        Light beam;
        AudioSource clickSource;
        PlayerInputReader input;
        Quaternion smoothedRotation;

        public bool IsOn => beam.enabled;

        void Awake()
        {
            beam = GetComponent<Light>();
            input = GetComponentInParent<PlayerInputReader>();
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 0f;
            clickSource.clip = ProceduralAudio.GetClip(ProceduralSound.Click);
            beam.enabled = startOn;
        }

        void OnEnable()
        {
            smoothedRotation = transform.parent != null ? transform.parent.rotation : transform.rotation;
        }

        void LateUpdate()
        {
            if (input != null && input.FlashlightPressed)
                SetOn(!IsOn);

            if (followSharpness > 0f && transform.parent != null)
            {
                float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
                smoothedRotation = Quaternion.Slerp(smoothedRotation, transform.parent.rotation, t);
                transform.rotation = smoothedRotation;
            }
        }

        public void SetOn(bool on)
        {
            if (beam.enabled == on)
                return;
            beam.enabled = on;
            clickSource.PlayOneShot(clickSource.clip, clickVolume);
        }
    }
}
