using HorrorGame.Audio;
using HorrorGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates the Phase 1 graybox scene: town block-out, facility, lighting, fog, post-processing,
    /// ambience and the player. The scene is generated, so to change the layout edit
    /// <see cref="FacilityBuilder"/> or <see cref="TownBlockoutBuilder"/> and run
    /// Horror Game > Rebuild Phase 1 Scene. Materials and the player prefab are only created when
    /// missing, so hand-made changes to them survive a rebuild.
    /// </summary>
    public static class Phase1SceneBuilder
    {
        public static readonly Color FogColor = new Color(0.018f, 0.022f, 0.032f);

        [MenuItem("Horror Game/Rebuild Phase 1 Scene", priority = 0)]
        static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (System.IO.File.Exists(ProjectPaths.Phase1Scene) && !EditorUtility.DisplayDialog("Rebuild Phase 1 scene?",
                    "This regenerates " + ProjectPaths.Phase1Scene + " from code. Manual edits to that scene will be lost.",
                    "Rebuild", "Cancel"))
                return;
            Build();
        }

        [MenuItem("Horror Game/Open Phase 1 Scene", priority = 1)]
        static void OpenFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ProjectPaths.Phase1Scene);
        }

        public static void Build()
        {
            AssetFolders.Ensure(ProjectPaths.Scenes);
            RenderPipelineSetup.EnsureUrp();
            var kit = new GrayboxKit();
            var postFx = RenderPipelineSetup.EnsurePostFxProfile();
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var environment = GrayboxKit.Group("Environment", null).transform;
            TownBlockoutBuilder.Build(environment, kit);
            FacilityBuilder.Build(environment, kit, TownBlockoutBuilder.FacilityOrigin);

            ConfigureAtmosphere();
            CreateMoonlight();
            CreatePostProcessVolume(postFx);
            CreateAmbience();
            SpawnPlayer(playerPrefab);

            EditorSceneManager.SaveScene(scene, ProjectPaths.Phase1Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ProjectPaths.Phase1Scene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Built " + ProjectPaths.Phase1Scene + ". Press Play to walk around.");
        }

        static void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.04f, 0.055f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static void CreateMoonlight()
        {
            var moon = new GameObject("Moonlight");
            moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.65f, 0.9f);
            light.intensity = 0.12f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            RenderSettings.sun = light;
        }

        static void CreatePostProcessVolume(VolumeProfile profile)
        {
            var go = new GameObject("PostProcessVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void CreateAmbience()
        {
            var go = new GameObject("Ambience_Drone");
            var ambience = go.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(ambience, "sound", (int)ProceduralSound.Drone);
            SerializedFields.SetFloat(ambience, "volume", 0.3f);
            SerializedFields.SetBool(ambience, "spatial", false);
        }

        static void SpawnPlayer(GameObject prefab)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            Vector3 spawn = TownBlockoutBuilder.PlayerSpawn;
            Vector3 toEntrance = FacilityBuilder.EntrancePosition(TownBlockoutBuilder.FacilityOrigin) - spawn;
            toEntrance.y = 0f;
            player.transform.SetPositionAndRotation(spawn, Quaternion.LookRotation(toEntrance.normalized, Vector3.up));
            if (player.GetComponent<PlayerMotor>() == null)
                Debug.LogWarning("[Horror Game] Player prefab is missing PlayerMotor.");
        }
    }
}
