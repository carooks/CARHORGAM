using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Glass
{
    /// <summary>
    /// A heavy wall lever that makes the glass house shift right away (after a short warning).
    /// Loud: monsters nearby hear it. Can't be pulled again until the panes have stopped.
    /// </summary>
    public class GlassHouseLever : MonoBehaviour, IInteractable
    {
        [SerializeField] GlassHouse house;
        [Tooltip("The handle; it swings down when pulled and springs back.")]
        [SerializeField] Transform handle;
        [SerializeField] float pulledAngle = 70f;
        [SerializeField] float warningSeconds = 1.5f;
        [SerializeField] float noiseRadius = 20f;
        [SerializeField] string prompt = "Pull the lever (shift the glass)";
        [SerializeField] string busyPrompt = "The glass is moving";

        AudioSource clank;
        float pulledAt = -10f;
        Quaternion handleRest;

        void Awake()
        {
            if (house == null)
                house = FindAnyObjectByType<GlassHouse>();
            if (handle != null)
                handleRest = handle.localRotation;
            clank = gameObject.AddComponent<AudioSource>();
            clank.clip = ProceduralAudio.GetClip(ProceduralSound.Click);
            clank.playOnAwake = false;
            clank.spatialBlend = 1f;
            clank.maxDistance = 20f;
            clank.rolloffMode = AudioRolloffMode.Linear;
        }

        public string GetPrompt(PlayerInteractor interactor) => CanInteract(interactor) ? prompt : busyPrompt;

        public bool CanInteract(PlayerInteractor interactor) =>
            house != null && !house.ShiftPending && !house.IsShifting;

        public void Interact(PlayerInteractor interactor)
        {
            pulledAt = Time.time;
            clank.pitch = 0.35f;
            clank.PlayOneShot(clank.clip, 1f);
            Noise.Emit(transform.position, noiseRadius, interactor != null ? interactor.gameObject : gameObject);
            house.RequestShift(warningSeconds);
        }

        void Update()
        {
            if (handle == null)
                return;
            // Snaps down in 0.15 s, eases back up over the next second.
            float t = Time.time - pulledAt;
            float down = t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.15f));
            handle.localRotation = handleRest * Quaternion.Euler(down * pulledAngle, 0f, 0f);
        }
    }
}
