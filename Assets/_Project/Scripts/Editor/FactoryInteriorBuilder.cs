using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates the old match factory at night as its own scene (Scenes/MatchFactory_Interior.unity).
    /// Run Horror Game > Build Match Factory Interior after changing anything here.
    ///
    /// Built from primitives, dressed with Synty POLYGON props when the packs are imported under
    /// Assets/Synty (never committed; see <see cref="FactoryProps"/>). Without them it still builds.
    ///
    /// Layout (metres, +X east, +Z north, origin = south-west corner of the machine hall), loosely
    /// after the aerial photo: two three-storey brick wings along Route 94 with a drive between them,
    /// a courtyard, the long machine hall, a warehouse at its east end and burnt rooms behind it.
    ///   Drive / spawn     X 36..44,   Z -40..-24   gated off Route 94; player starts here
    ///   West wing         X -10..36,  Z -40..-24   floors 1-2 walkable (packing room, offices)
    ///   East wing         X 44..96,   Z -40..-24   floor 1 walkable (canteen, lockers)
    ///   Courtyard         X -10..150, Z -24..0     trucks, smokestack, lamps, weeds
    ///   Receiving room    X 0..14,    Z -12..0     main breaker (starts OFF)
    ///   Machine hall      X 0..120,   Z 0..24      21 match machines, line shafts, collapsed bays
    ///   Foreman's office  X 112..120, Z 14..24     3.5 m up a stair; desk lamp on
    ///   Warehouse         X 120..150, Z -6..30     pallet racking, skylights
    ///   Dip room          X 0..16,    Z 24..36     burnt out
    ///   Stairwell         X 10..16,   Z 36..42     dead end
    /// </summary>
    public static partial class FactoryInteriorBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/MatchFactory_Interior.unity";
        const string PostFxPath = ProjectPaths.Settings + "/PostFX_Factory.asset";
        const string LitterMeshPath = ProjectPaths.Meshes + "/MatchLitter.asset";

        const float HallLength = 120f;
        const float HallWidth = 24f;
        const float EaveHeight = 8f;
        const float RidgeHeight = 13f;
        const float WallThickness = 0.5f;
        const float BayWidth = 6f;
        const int Bays = 20;
        const float PlatformHeight = 3.5f;
        const float LowRoomHeight = 5f;
        const float StoreyHeight = 4.2f;
        const float WingSouth = -40f, WingNorth = -24f;

        static readonly Color FogColor = new Color(0.02f, 0.022f, 0.03f);
        static readonly Color LampColor = new Color(1f, 0.8f, 0.55f);

        /// <summary>Where the player starts: in the drive between the two wings, facing the courtyard.</summary>
        public static readonly Vector3 PlayerSpawn = new Vector3(40f, 0.1f, -37f);

        /// <summary>Everything walkable, including the office platform and the wing's upper floor.</summary>
        public static readonly Bounds NavBounds = new Bounds(new Vector3(70f, 6f, 1f), new Vector3(166f, 14f, 86f));

        [MenuItem("Horror Game/Build Match Factory Interior", priority = 20)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (System.IO.File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild match factory?",
                    "This regenerates " + ScenePath + " from code. Manual edits to that scene will be lost.",
                    "Rebuild", "Cancel"))
                return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Horror Game/Open Match Factory Interior", priority = 21)]
        static void OpenFromMenu()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Not built yet", "Run Horror Game > Build Match Factory Interior first.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Entry point for batch mode: -executeMethod HorrorGame.EditorTools.FactoryInteriorBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            Debug.Log("[Horror Game] Batch build of the match factory interior finished.");
        }

        public static void Build()
        {
            AssetFolders.Ensure(ProjectPaths.Scenes);
            RenderPipelineSetup.EnsureUrp();
            kit = new GrayboxKit();
            rng = new System.Random(19340807);
            BreakerLights.Clear();
            BreakerFixtures.Clear();
            shadowedLamps = 0;
            FactoryProps.Reset();
            CreateMaterials();
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GrayboxKit.Group("MatchFactory", null).transform;

            // Hall first: its lamps get the shadow-casting budget.
            BuildHallShell(GrayboxKit.Group("Hall_Structure", root).transform);
            BuildRoof(GrayboxKit.Group("Hall_Roof", root).transform);
            BuildHallLamps(GrayboxKit.Group("Hall_Lamps", root).transform);
            BuildMachines(GrayboxKit.Group("Hall_Machines", root).transform);
            BuildLineShafts(GrayboxKit.Group("Hall_LineShafts", root).transform);
            BuildHallProps(GrayboxKit.Group("Hall_Props", root).transform);
            BuildCollapse(GrayboxKit.Group("Hall_Collapse", root).transform);
            BuildOffice(GrayboxKit.Group("ForemansOffice", root).transform);
            BuildDipRoom(GrayboxKit.Group("DipRoom", root).transform);
            BuildStairwell(GrayboxKit.Group("Stairwell", root).transform);
            BuildReceiving(GrayboxKit.Group("Receiving", root).transform);
            BuildWarehouse(GrayboxKit.Group("Warehouse", root).transform);
            BuildWestWing(GrayboxKit.Group("WestWing", root).transform);
            BuildEastWing(GrayboxKit.Group("EastWing", root).transform);
            BuildCourtyard(GrayboxKit.Group("Courtyard", root).transform);
            BuildMatchLitter(root);
            BuildMainBreaker(GrayboxKit.Group("MainBreaker", root).transform);
            BuildWaypoints();
            MonsterBuilder.BuildInFactory(kit);

            ConfigureAtmosphere();
            CreateMoonlight();
            CreatePostProcessing();
            CreateAmbience();
            SpawnPlayer(playerPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Horror Game] Built {ScenePath}. Synty props placed: {FactoryProps.Placed}, primitive fallbacks: {FactoryProps.FellBack}, shadowed lamps: {shadowedLamps}.");
        }

        // ------------------------------------------------------------------ AI waypoints

        /// <summary>
        /// Empty markers along the walkable routes, used as the Foreman's patrol. He starts at the
        /// ground-level one farthest from the player, so keep some deep inside.
        /// </summary>
        static void BuildWaypoints()
        {
            var root = GrayboxKit.Group("AI_Waypoints", null).transform;
            Vector3[] points =
            {
                new Vector3(40f, 0f, -20f), new Vector3(10f, 0f, -18f), new Vector3(70f, 0f, -16f),   // courtyard
                new Vector3(110f, 0f, -14f), new Vector3(135f, 0f, -12f),
                new Vector3(7f, 0f, -6f),                                                         // receiving
                new Vector3(6f, 0f, 12f), new Vector3(24f, 0f, 12f), new Vector3(45f, 0f, 12f),   // hall centre aisle
                new Vector3(66f, 0f, 12f), new Vector3(88f, 0f, 12f), new Vector3(108f, 0f, 12f),
                new Vector3(15f, 0f, 1.8f), new Vector3(45f, 0f, 1.8f), new Vector3(90f, 0f, 1.8f), // south aisle
                new Vector3(20f, 0f, 22.2f), new Vector3(60f, 0f, 22.2f), new Vector3(96f, 0f, 22.2f), // north aisle
                new Vector3(112.8f, PlatformHeight, 18f), new Vector3(116.5f, PlatformHeight, 21f), // landing, office
                new Vector3(7f, 0f, 26f), new Vector3(8f, 0f, 34.5f), new Vector3(13f, 0f, 38f),    // dip room, stairwell
                new Vector3(128f, 0f, 12.5f), new Vector3(146f, 0f, 2f), new Vector3(146f, 0f, 27f), // warehouse
                new Vector3(122f, 0f, 27f),
                new Vector3(0f, 0f, -32f), new Vector3(24f, 0f, -28f),                          // west wing floor 1
                new Vector3(0f, StoreyHeight, -30f), new Vector3(14f, StoreyHeight, -32f),       // west wing floor 2
                new Vector3(55f, 0f, -32f), new Vector3(85f, 0f, -30f),                         // east wing
            };
            for (int i = 0; i < points.Length; i++)
            {
                var wp = new GameObject($"WP_{i:00}");
                wp.transform.SetParent(root, false);
                wp.transform.position = points[i];
            }
        }

        // ------------------------------------------------------------------ atmosphere

        static void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.06f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static readonly Quaternion MoonRotation = Quaternion.Euler(32f, 20f, 0f);

        static void CreateMoonlight()
        {
            var moon = new GameObject("Moonlight");
            // Low from the south-south-west, so it falls through the south windows in long patches.
            moon.transform.rotation = MoonRotation;
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.65f, 0.95f);
            light.intensity = 0.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            RenderSettings.sun = light;
        }

        /// <summary>Dark grade, grain, vignette, slight aberration, and a softened pixel filter (75% resolution).</summary>
        static void CreatePostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile == null)
            {
                AssetFolders.Ensure(ProjectPaths.Settings);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostFxPath);
            }
            // Missing overrides are added; values are (re)applied on every build so the look is defined here.
            var tonemapping = Ensure<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var color = Ensure<ColorAdjustments>(profile);
            color.postExposure.Override(0.8f);
            color.contrast.Override(12f);
            color.saturation.Override(-30f);
            var vignette = Ensure<Vignette>(profile);
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            var grain = Ensure<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.35f);
            var bloom = Ensure<Bloom>(profile);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.75f);
            Ensure<ChromaticAberration>(profile).intensity.Override(0.12f);
            foreach (var component in profile.components)
                EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostProcessVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var pixelation = new GameObject("PixelationFilter").AddComponent<PixelationFilter>();
            SerializedFields.SetFloat(pixelation, "renderScale", 0.75f);
        }

        static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;
            var component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
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
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity); // facing north, into the courtyard
            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.backgroundColor = FogColor;
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == ScenePath)
                    return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
