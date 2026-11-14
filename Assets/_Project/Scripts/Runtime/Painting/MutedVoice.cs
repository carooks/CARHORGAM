using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// A member of the choir whose mouth has been painted over in white, so the chord is missing
    /// their note. Press E to scrape the paint off and they join the song. Every freed voice makes
    /// the harmony fuller, and the thing that hates harmony angrier. Free them all and the real
    /// church will open.
    /// </summary>
    [RequireComponent(typeof(ChoirSinger))]
    public class MutedVoice : MonoBehaviour, IInteractable
    {
        [Tooltip("The white paint over the mouth. Hidden once scraped off.")]
        [SerializeField] GameObject paint;
        [SerializeField] string prompt = "Scrape the paint off their mouth";
        [Tooltip("Scraping is not quiet. How far it carries, in metres.")]
        [SerializeField] float noiseRadius = 8f;

        ChoirSinger singer;
        AudioSource scrape;

        public PaintedRealm Realm { get; set; }
        public bool IsFreed { get; private set; }
        public ChoirSinger Singer => singer;

        void Awake()
        {
            singer = GetComponent<ChoirSinger>();
            singer.Muted = true;
            scrape = gameObject.AddComponent<AudioSource>();
            scrape.playOnAwake = false;
            scrape.spatialBlend = 1f;
            scrape.maxDistance = 15f;
            scrape.rolloffMode = AudioRolloffMode.Linear;
        }

        public string GetPrompt(PlayerInteractor interactor) => IsFreed ? null : prompt;

        public bool CanInteract(PlayerInteractor interactor) => !IsFreed;

        public void Interact(PlayerInteractor interactor) => Free();

        /// <summary>Scrapes the paint off. Public so tests can free voices without a player.</summary>
        public void Free()
        {
            if (IsFreed)
                return;
            IsFreed = true;
            if (paint != null)
                paint.SetActive(false);
            singer.Muted = false;
            scrape.pitch = 0.6f;
            scrape.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Footstep), 1f);
            Noise.Emit(transform.position, noiseRadius, gameObject);
            if (Realm != null)
                Realm.OnVoiceFreed(this);
        }
    }
}
