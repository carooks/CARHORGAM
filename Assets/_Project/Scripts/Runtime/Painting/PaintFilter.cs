using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Painting
{
    /// <summary>
    /// Switches the oil-paint full-screen effect on while this component is enabled. The effect is a
    /// Full Screen Pass renderer feature on the project's URP renderer (added by the editor builder,
    /// inactive by default), so the rest of the game is unaffected.
    /// Renderer features are assets, and asset changes made in Play mode stick in the editor, so the
    /// feature is always switched back off when this component is disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaintFilter : MonoBehaviour
    {
        static readonly int DreadId = Shader.PropertyToID("_PaintDread");
        static readonly int StrengthId = Shader.PropertyToID("_PaintStrength");

        [Tooltip("The 'PaintedRealm' Full Screen Pass feature on URP_Horror_Renderer.")]
        [SerializeField] ScriptableRendererFeature feature;
        [Tooltip("How much of the full-screen smear to use when calm. The brushstrokes carry the look now; this only softens them. It rises to full as the entity closes in.")]
        [Range(0f, 1f)] [SerializeField] float calmStrength = 0.25f;

        /// <summary>0 = calm, 1 = the entity is on top of you: heavier smears, running paint, red bleed.</summary>
        public float Dread { get; set; }

        /// <summary>Fades the whole effect in and out (used during the transition into the painting).</summary>
        public float Strength { get; set; } = 1f;

        public ScriptableRendererFeature Feature
        {
            get => feature;
            set => feature = value;
        }

        void OnEnable()
        {
            if (feature == null)
                Debug.LogWarning("[Horror Game] PaintFilter has no renderer feature. Rebuild the match factory interior.", this);
            else
                feature.SetActive(true);
            Push();
        }

        void Update() => Push();

        void OnDisable()
        {
            if (feature != null)
                feature.SetActive(false);
        }

        void Push()
        {
            float dread = Mathf.Clamp01(Dread);
            Shader.SetGlobalFloat(DreadId, dread);
            Shader.SetGlobalFloat(StrengthId, Mathf.Clamp01(Strength) * Mathf.Lerp(calmStrength, 1f, dread));
        }
    }
}
