using HorrorGame.Audio;
using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// One of the things inside the painting that holds the entity there. Press E to set it alight;
    /// it burns for a few seconds and then counts toward the banishing. Burning every anchor in the
    /// realm banishes the entity. Placeholder objective: what the anchors are (easels, effigies,
    /// sketches of the entity's victims) is a set-piece decision per entity.
    /// </summary>
    public class BanishAnchor : MonoBehaviour, IInteractable
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] string prompt = "Strike a match";
        [SerializeField] string burningPrompt = "Burning...";
        [Tooltip("Renderers that blacken as it burns.")]
        [SerializeField] Renderer[] burnRenderers = System.Array.Empty<Renderer>();
        [Tooltip("Fire light, off until lit.")]
        [SerializeField] Light fire;
        [SerializeField] float burnTime = 2.5f;
        [SerializeField] float fireIntensity = 6f;

        enum AnchorState { Intact, Burning, Burnt }

        AnchorState state = AnchorState.Intact;
        float burnProgress;
        AudioSource audioSource;
        MaterialPropertyBlock block;
        Color[] originalColors;

        public PaintedRealm Realm { get; set; }
        public bool IsBurnt => state == AnchorState.Burnt;
        public bool IsIntact => state == AnchorState.Intact;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            originalColors = new Color[burnRenderers.Length];
            for (int i = 0; i < burnRenderers.Length; i++)
                if (burnRenderers[i] != null && burnRenderers[i].sharedMaterial != null && burnRenderers[i].sharedMaterial.HasProperty(BaseColorId))
                    originalColors[i] = burnRenderers[i].sharedMaterial.GetColor(BaseColorId);
            if (fire != null)
                fire.enabled = false;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = 20f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
        }

        public string GetPrompt(PlayerInteractor interactor) => state == AnchorState.Intact ? prompt : state == AnchorState.Burning ? burningPrompt : null;

        public bool CanInteract(PlayerInteractor interactor) => state == AnchorState.Intact;

        public void Interact(PlayerInteractor interactor) => Ignite();

        /// <summary>Sets it alight. Public so tests can burn anchors without a player.</summary>
        public void Ignite()
        {
            if (state != AnchorState.Intact)
                return;
            state = AnchorState.Burning;
            if (fire != null)
                fire.enabled = true;
            audioSource.PlayOneShot(ProceduralAudio.GetClip(ProceduralSound.Click), 0.9f);
            audioSource.clip = ProceduralAudio.GetClip(ProceduralSound.ElectricalBuzz);
            audioSource.loop = true;
            audioSource.pitch = 0.35f; // a low crackling roar from the buzz clip
            audioSource.volume = 0.5f;
            audioSource.Play();
        }

        void Update()
        {
            if (state == AnchorState.Intact)
                return;
            if (state == AnchorState.Burning)
            {
                burnProgress += Time.deltaTime / Mathf.Max(0.01f, burnTime);
                ApplyChar(Mathf.Clamp01(burnProgress));
                if (burnProgress >= 1f)
                {
                    state = AnchorState.Burnt;
                    if (Realm != null)
                        Realm.OnAnchorBurned(this);
                }
            }
            if (fire != null)
            {
                // Flames gutter: bright while burning, dying embers after.
                float flicker = 0.75f + Mathf.PerlinNoise(Time.time * 9f, transform.position.x) * 0.5f;
                float level = state == AnchorState.Burning ? 1f : Mathf.Max(0.12f, 1f - (burnProgress - 1f) * 0.5f);
                fire.intensity = fireIntensity * level * flicker;
                burnProgress += state == AnchorState.Burnt ? Time.deltaTime * 0.3f : 0f;
                if (state == AnchorState.Burnt && audioSource.isPlaying && burnProgress > 2.5f)
                    audioSource.Stop();
            }
        }

        void ApplyChar(float amount)
        {
            for (int i = 0; i < burnRenderers.Length; i++)
            {
                var r = burnRenderers[i];
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(originalColors[i], new Color(0.03f, 0.02f, 0.015f), amount));
                r.SetPropertyBlock(block);
            }
        }
    }
}
