using System;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// A switch or breaker that turns a group of lamps on and off. Switching off deactivates the
    /// light objects (which also silences any buzz or flicker on them) and blacks out the glowing
    /// fixtures. The first interactable in the game; also a template for others.
    /// </summary>
    public class LightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] string turnOffPrompt = "Cut the power";
        [SerializeField] string turnOnPrompt = "Restore the power";
        [SerializeField] bool startOn = true;
        [Tooltip("Light objects to switch. Whole objects are toggled so their flicker and buzz stop too.")]
        [SerializeField] GameObject[] lights = Array.Empty<GameObject>();
        [Tooltip("Emissive lamp fixtures that go dark when the power is off.")]
        [SerializeField] Renderer[] fixtures = Array.Empty<Renderer>();
        [Tooltip("Optional handle that tilts up (on) or down (off).")]
        [SerializeField] Transform lever;
        [SerializeField] float leverAngle = 35f;
        [Range(0f, 1f)] [SerializeField] float clickVolume = 0.8f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        AudioSource clickSource;
        MaterialPropertyBlock block;

        public bool IsOn { get; private set; }

        /// <summary>Raised with the new state whenever the switch is flipped.</summary>
        public event Action<bool> Switched;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            clickSource = gameObject.AddComponent<AudioSource>();
            clickSource.playOnAwake = false;
            clickSource.spatialBlend = 1f;
            clickSource.minDistance = 1f;
            clickSource.maxDistance = 15f;
            clickSource.rolloffMode = AudioRolloffMode.Linear;
            clickSource.clip = ProceduralAudio.GetClip(ProceduralSound.Click);
            Apply(startOn);
        }

        public string GetPrompt(PlayerInteractor interactor) => IsOn ? turnOffPrompt : turnOnPrompt;

        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor interactor)
        {
            Apply(!IsOn);
            clickSource.PlayOneShot(clickSource.clip, clickVolume);
            Switched?.Invoke(IsOn);
        }

        void Apply(bool on)
        {
            IsOn = on;
            foreach (var lightObject in lights)
                if (lightObject != null)
                    lightObject.SetActive(on);

            // Off: override the emission with black. On: clear the override so the material
            // (or a LightFlicker driving the same renderer) is in charge again.
            block.Clear();
            if (!on)
                block.SetColor(EmissionColorId, Color.black);
            foreach (var fixture in fixtures)
                if (fixture != null)
                    fixture.SetPropertyBlock(block);

            if (lever != null)
                lever.localRotation = Quaternion.Euler(on ? -leverAngle : leverAngle, 0f, 0f);
        }
    }
}
