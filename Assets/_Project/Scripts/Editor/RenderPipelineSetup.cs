using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Creates the project's URP asset and post-processing profile if they are missing and makes
    /// URP the active render pipeline. Safe to run repeatedly: existing assets are left untouched.
    /// </summary>
    public static class RenderPipelineSetup
    {
        const string PipelinePath = ProjectPaths.Settings + "/URP_Horror.asset";
        const string RendererPath = ProjectPaths.Settings + "/URP_Horror_Renderer.asset";
        const string PostFxPath = ProjectPaths.Settings + "/PostFX_Horror.asset";
        const string DefaultPostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("Horror Game/Setup/Configure Render Pipeline (URP)")]
        public static void ConfigureFromMenu()
        {
            EnsureUrp();
            Debug.Log("[Horror Game] URP configured: " + PipelinePath);
        }

        public static UniversalRenderPipelineAsset EnsureUrp()
        {
            AssetFolders.Ensure(ProjectPaths.Settings);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
                // Forward+ lifts the per-object light limit, which matters with many small lamps.
                renderer.renderingMode = UnityEngine.Rendering.Universal.RenderingMode.ForwardPlus;
                AssetDatabase.CreateAsset(renderer, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.shadowDistance = 45f;
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);

                // The flashlight is a spot light, so shadows from additional lights must be on.
                // That setter is internal, so go through the serialized field.
                var serialized = new SerializedObject(pipeline);
                var additionalShadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
                if (additionalShadows != null)
                    additionalShadows.boolValue = true;
                else
                    Debug.LogWarning("[Horror Game] Could not enable additional light shadows on the URP asset; the flashlight will not cast shadows.");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int currentLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(currentLevel, false);

            // URP is designed around linear colour and linear light intensities; the lighting values
            // in the scene builder assume both.
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                PlayerSettings.colorSpace = ColorSpace.Linear;
            GraphicsSettings.lightsUseLinearIntensity = true;

            return pipeline;
        }

        public static VolumeProfile EnsurePostFxProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile != null)
                return profile;

            AssetFolders.Ensure(ProjectPaths.Settings);
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxPath);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-25f);
            color.contrast.Override(10f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.45f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.35f);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);

            // Volume components are stored as sub-assets of the profile.
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
