using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Rendering
{
    /// <summary>
    /// The chunky-pixel look: while this component is enabled it lowers the URP render scale and
    /// switches upscaling to point sampling, so the 3D image is drawn at a fraction of the screen
    /// resolution and blown up with hard pixel edges. UI is unaffected.
    /// The values live on the shared URP asset, and edits to assets made in Play mode stick in the
    /// editor, so the previous values are put back when the component is disabled or the scene
    /// unloads. Put one in any scene that should be pixelated.
    /// </summary>
    [DisallowMultipleComponent]
    public class PixelationFilter : MonoBehaviour
    {
        [Tooltip("Fraction of the screen resolution the 3D view is rendered at. 0.5 = half resolution.")]
        [Range(0.2f, 1f)] [SerializeField] float renderScale = 0.5f;

        UniversalRenderPipelineAsset asset;
        float previousScale;
        UpscalingFilterSelection previousFilter;
        bool applied;

        void OnEnable()
        {
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                Debug.LogWarning("[Horror Game] PixelationFilter needs URP as the active render pipeline.");
                return;
            }
            previousScale = asset.renderScale;
            previousFilter = asset.upscalingFilter;
            asset.renderScale = renderScale;
            asset.upscalingFilter = UpscalingFilterSelection.Point;
            applied = true;
        }

        void OnDisable()
        {
            if (!applied || asset == null)
                return;
            asset.renderScale = previousScale;
            asset.upscalingFilter = previousFilter;
            applied = false;
        }

        void OnValidate()
        {
            if (applied && asset != null)
                asset.renderScale = renderScale;
        }
    }
}
