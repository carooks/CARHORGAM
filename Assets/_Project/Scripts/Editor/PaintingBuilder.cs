using System.Collections.Generic;
using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using HorrorGame.Painting;
using HorrorGame.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates the haunted painting test level (Scenes/HauntedPainting_Test.unity) from primitives:
    ///
    ///   Parlor   around the origin. A small shut-up front room in an old house: armchair, lamp,
    ///            boarded window, and the portrait on the north wall. Pixel filter on, as everywhere.
    ///   Realm    centred on <see cref="RealmOrigin"/>, far from everything else. The inside of the
    ///            painting: a dusk field of tall grass, a dead farmhouse, fences, dead trees, three
    ///            easels to burn and the entity. Seen through the oil-paint filter.
    ///
    /// The portrait and its realm are built by <see cref="BuildPortrait"/> and <see cref="BuildRealm"/>,
    /// so they can be dropped into any other scene (a house in town) later.
    /// Menu: Horror Game > Build Haunted Painting Test.
    /// </summary>
    public static class PaintingBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/HauntedPainting_Test.unity";
        const string RendererPath = ProjectPaths.Settings + "/URP_Horror_Renderer.asset";
        const string ParlorPostFxPath = ProjectPaths.Settings + "/PostFX_Parlor.asset";
        const string RealmPostFxPath = ProjectPaths.Settings + "/PostFX_Painting.asset";
        const string FilterMaterialPath = ProjectPaths.Materials + "/M_PaintedRealmFilter.mat";
        const string PortraitMaterialPath = ProjectPaths.Materials + "/M_HauntedPortrait.mat";
        public const string FeatureName = "PaintedRealm";

        public static readonly Vector3 RealmOrigin = new Vector3(1000f, 0f, 0f);
        const float RealmHalfSize = 60f;

        static readonly Color ParlorFog = new Color(0.015f, 0.014f, 0.016f);
        static readonly Color RealmFog = new Color(0.34f, 0.25f, 0.15f);

        static GrayboxKit kit;
        static System.Random rng;

        [MenuItem("Horror Game/Build Haunted Painting Test", priority = 30)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (System.IO.File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild haunted painting test?",
                    "This regenerates " + ScenePath + " from code. Manual edits to that scene will be lost.", "Rebuild", "Cancel"))
                return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Batch entry point: -executeMethod HorrorGame.EditorTools.PaintingBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            Debug.Log("[Horror Game] Batch build of the haunted painting test finished.");
        }

        public static void Build()
        {
            AssetFolders.Ensure(ProjectPaths.Scenes);
            RenderPipelineSetup.EnsureUrp();
            kit = new GrayboxKit();
            rng = new System.Random(66613);
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The real world.
            var outside = new List<GameObject>();
            var parlor = GrayboxKit.Group("Parlor", null).transform;
            BuildParlor(parlor, outside);
            outside.Add(CreateVolume("PostProcessVolume", ParlorPostFxPath, false, 0f));
            outside.Add(new GameObject("PixelationFilter", typeof(PixelationFilter)));
            outside.Add(CreateAmbience("ParlorAmbience", 0.3f, 1f));

            // The painting.
            var portrait = BuildPortrait(parlor, new Vector3(0f, 1.75f, 2.9f), Quaternion.identity);
            var realm = BuildRealm(RealmOrigin, outside);
            portrait.Realm = realm;

            ConfigureAtmosphere();
            SpawnPlayer(playerPrefab, new Vector3(0f, 0.05f, -2.2f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Built " + ScenePath + ". Press Play, walk to the portrait and press E.");
        }

        // ------------------------------------------------------------------ parlor (the real world)

        static void BuildParlor(Transform parent, List<GameObject> outside)
        {
            var wallpaper = kit.Get("M_Parlor_Wallpaper", new Color(0.24f, 0.2f, 0.15f), 0.05f);
            var wainscot = kit.Get("M_Parlor_Wainscot", new Color(0.17f, 0.11f, 0.07f), 0.25f);
            var boards = kit.Get("M_Parlor_Floorboards", new Color(0.2f, 0.14f, 0.09f), 0.2f);
            var plaster = kit.Get("M_Parlor_Plaster", new Color(0.3f, 0.29f, 0.26f), 0.05f);
            var fabric = kit.Get("M_Parlor_Upholstery", new Color(0.22f, 0.07f, 0.06f), 0.05f);
            var rug = kit.Get("M_Parlor_Rug", new Color(0.18f, 0.12f, 0.1f), 0.02f);
            var shade = kit.Get("M_Parlor_LampShade", new Color(0.7f, 0.6f, 0.45f), 0.1f, 0f, new Color(1f, 0.7f, 0.4f) * 0.6f);
            var plank = kit.Get("M_Parlor_BoardUp", new Color(0.3f, 0.24f, 0.16f), 0.05f);

            const float w = 7f, d = 6f, h = 3f, t = 0.2f;
            kit.Box("Floor", parent, new Vector3(0f, -0.1f, 0f), new Vector3(w + 2 * t, 0.2f, d + 2 * t), boards);
            kit.Box("Ceiling", parent, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 2 * t, 0.2f, d + 2 * t), plaster);
            kit.Box("Wall_North", parent, new Vector3(0f, h / 2, d / 2 + t / 2), new Vector3(w + 2 * t, h, t), wallpaper);
            kit.Box("Wall_South", parent, new Vector3(0f, h / 2, -d / 2 - t / 2), new Vector3(w + 2 * t, h, t), wallpaper);
            kit.Box("Wall_East", parent, new Vector3(w / 2 + t / 2, h / 2, 0f), new Vector3(t, h, d), wallpaper);
            // West wall with a boarded window.
            kit.Box("Wall_West_Low", parent, new Vector3(-w / 2 - t / 2, 0.5f, 0f), new Vector3(t, 1f, d), wallpaper);
            kit.Box("Wall_West_High", parent, new Vector3(-w / 2 - t / 2, 2.6f, 0f), new Vector3(t, 0.8f, d), wallpaper);
            kit.Box("Wall_West_N", parent, new Vector3(-w / 2 - t / 2, 1.6f, 1.9f), new Vector3(t, 1.2f, 2.2f), wallpaper);
            kit.Box("Wall_West_S", parent, new Vector3(-w / 2 - t / 2, 1.6f, -1.9f), new Vector3(t, 1.2f, 2.2f), wallpaper);
            for (int i = 0; i < 4; i++)
                kit.Box("BoardUp", parent, new Vector3(-w / 2 - 0.02f, 1.15f + i * 0.3f, 0f), new Vector3(0.04f, 0.2f, 1.9f), plank, false,
                    Quaternion.Euler(Rand(-6f, 6f), 0f, 0f));
            // Wainscot band around the room.
            foreach (var (pos, size) in new[]
                     {
                         (new Vector3(0f, 0.45f, d / 2 - 0.02f), new Vector3(w, 0.9f, 0.04f)),
                         (new Vector3(0f, 0.45f, -d / 2 + 0.02f), new Vector3(w, 0.9f, 0.04f)),
                         (new Vector3(w / 2 - 0.02f, 0.45f, 0f), new Vector3(0.04f, 0.9f, d)),
                     })
                kit.Box("Wainscot", parent, pos, size, wainscot, false);
            // A shut door in the south wall.
            kit.Box("Door", parent, new Vector3(2.2f, 1.05f, -d / 2 + 0.03f), new Vector3(0.95f, 2.1f, 0.06f), wainscot, false);
            kit.Box("DoorKnob", parent, new Vector3(1.85f, 1.0f, -d / 2 + 0.08f), new Vector3(0.06f, 0.06f, 0.06f), kit.Metal, false);

            // Armchair turned to face the portrait, side table and lamp.
            kit.Box("Rug", parent, new Vector3(0f, 0.01f, 0.3f), new Vector3(3.2f, 0.02f, 2.4f), rug, false);
            var chair = GrayboxKit.Group("Armchair", parent).transform;
            chair.SetPositionAndRotation(new Vector3(-1.2f, 0f, -0.6f), Quaternion.Euler(0f, 20f, 0f));
            kit.Box("Seat", chair, chair.TransformPoint(new Vector3(0f, 0.25f, 0f)), new Vector3(0.9f, 0.5f, 0.85f), fabric, true, chair.rotation);
            kit.Box("Back", chair, chair.TransformPoint(new Vector3(0f, 0.75f, -0.37f)), new Vector3(0.9f, 0.9f, 0.18f), fabric, false, chair.rotation);
            kit.Box("ArmL", chair, chair.TransformPoint(new Vector3(-0.42f, 0.6f, 0f)), new Vector3(0.14f, 0.3f, 0.85f), fabric, false, chair.rotation);
            kit.Box("ArmR", chair, chair.TransformPoint(new Vector3(0.42f, 0.6f, 0f)), new Vector3(0.14f, 0.3f, 0.85f), fabric, false, chair.rotation);
            kit.Box("SideTable", parent, new Vector3(-2.3f, 0.35f, -0.4f), new Vector3(0.5f, 0.7f, 0.5f), wainscot);
            kit.Cylinder("LampBase", parent, new Vector3(-2.3f, 0.85f, -0.4f), new Vector3(0.05f, 0.3f, 0.05f), kit.Metal, false);
            kit.Cylinder("LampShade", parent, new Vector3(-2.3f, 1.08f, -0.4f), new Vector3(0.32f, 0.22f, 0.32f), shade, false);
            var lamp = GrayboxKit.PointLight("LampLight", parent, new Vector3(-2.3f, 1.1f, -0.4f), new Color(1f, 0.68f, 0.4f), 1.4f, 6f);
            lamp.shadows = LightShadows.Soft;
            var flicker = lamp.gameObject.AddComponent<World.LightFlicker>();
            SerializedFields.SetFloat(flicker, "dropoutsPerSecond", 0.08f);
            // Picture lamp over the portrait, the only other light.
            var pictureLight = GrayboxKit.SpotLight("PictureLight", parent, new Vector3(0f, 2.75f, 2.4f),
                Quaternion.Euler(60f, 0f, 0f), new Color(1f, 0.8f, 0.6f), 2.2f, 4f, 70f);
            pictureLight.shadows = LightShadows.Soft;
            kit.Box("PictureLamp", parent, new Vector3(0f, 2.7f, 2.82f), new Vector3(0.6f, 0.06f, 0.12f), kit.Metal, false);

            // Moonlight through the boards.
            var moon = new GameObject("Moonlight");
            moon.transform.rotation = Quaternion.Euler(25f, 80f, 0f);
            var moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.5f, 0.6f, 0.9f);
            moonLight.intensity = 0.25f;
            moonLight.shadows = LightShadows.Soft;
            outside.Add(moon);
        }

        /// <summary>
        /// The framed canvas. <paramref name="center"/> is the middle of the canvas; the canvas faces
        /// along -Z of <paramref name="rotation"/> (identity = hung on a north wall, facing south).
        /// </summary>
        public static HauntedPainting BuildPortrait(Transform parent, Vector3 center, Quaternion rotation)
        {
            kit ??= new GrayboxKit();
            var gilt = kit.Get("M_Painting_Gilt", new Color(0.42f, 0.3f, 0.12f), 0.55f, 0.8f);
            var root = GrayboxKit.Group("HauntedPortrait", parent).transform;
            root.SetPositionAndRotation(center, rotation);

            const float cw = 1.1f, ch = 1.5f, fw = 0.12f;
            var canvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvas.name = "Canvas";
            canvas.transform.SetParent(root, false);
            canvas.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            canvas.transform.localScale = new Vector3(cw, ch, 1f);
            canvas.GetComponent<MeshRenderer>().sharedMaterial = PortraitMaterial();
            Object.DestroyImmediate(canvas.GetComponent<Collider>());
            canvas.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.05f);

            Part("Frame_Top", new Vector3(0f, ch / 2 + fw / 2, -0.03f), new Vector3(cw + 2 * fw, fw, 0.08f));
            Part("Frame_Bottom", new Vector3(0f, -ch / 2 - fw / 2, -0.03f), new Vector3(cw + 2 * fw, fw, 0.08f));
            Part("Frame_Left", new Vector3(-cw / 2 - fw / 2, 0f, -0.03f), new Vector3(fw, ch, 0.08f));
            Part("Frame_Right", new Vector3(cw / 2 + fw / 2, 0f, -0.03f), new Vector3(fw, ch, 0.08f));
            Part("Plaque", new Vector3(0f, -ch / 2 - fw - 0.08f, -0.01f), new Vector3(0.3f, 0.07f, 0.02f));

            var painting = canvas.AddComponent<HauntedPainting>();
            SerializedFields.Set(painting, "canvas", canvas.GetComponent<MeshRenderer>());
            return painting;

            void Part(string name, Vector3 local, Vector3 size)
            {
                var go = kit.Box(name, root, root.TransformPoint(local), size, gilt, false, root.rotation);
                go.transform.SetParent(root, true);
            }
        }

        static Material PortraitMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PortraitMaterialPath);
            if (material != null)
                return material;
            var shader = Shader.Find("HorrorGame/HauntedPortrait");
            if (shader == null)
            {
                Debug.LogError("[Horror Game] HorrorGame/HauntedPortrait shader not found.");
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            material = new Material(shader) { name = "M_HauntedPortrait" };
            AssetDatabase.CreateAsset(material, PortraitMaterialPath);
            return material;
        }

        // ------------------------------------------------------------------ the realm inside the painting

        public static PaintedRealm BuildRealm(Vector3 origin, List<GameObject> outsideOnly)
        {
            kit ??= new GrayboxKit();
            rng ??= new System.Random(66613);
            var root = GrayboxKit.Group("PaintedRealm", null).transform;
            root.position = origin;
            var realm = root.gameObject.AddComponent<PaintedRealm>();
            var attention = root.gameObject.AddComponent<PaintAttention>();

            var soil = kit.Get("M_Painting_Soil", new Color(0.3f, 0.21f, 0.1f), 0.05f);
            var path = kit.Get("M_Painting_Path", new Color(0.42f, 0.33f, 0.2f), 0.05f);
            var wood = kit.Get("M_Painting_DeadWood", new Color(0.14f, 0.1f, 0.08f), 0.05f);
            var house = kit.Get("M_Painting_Farmhouse", new Color(0.36f, 0.33f, 0.28f), 0.05f);
            var roof = kit.Get("M_Painting_Roof", new Color(0.12f, 0.08f, 0.07f), 0.05f);
            var window = kit.Get("M_Painting_Window", new Color(0.9f, 0.6f, 0.25f), 0.3f, 0f, new Color(1f, 0.55f, 0.2f) * 1.5f);
            var grass = new[]
            {
                kit.Get("M_Painting_Grass1", new Color(0.55f, 0.43f, 0.18f), 0.05f),
                kit.Get("M_Painting_Grass2", new Color(0.42f, 0.36f, 0.14f), 0.05f),
                kit.Get("M_Painting_Grass3", new Color(0.33f, 0.3f, 0.16f), 0.05f),
            };

            Vector3 P(float x, float z, float y = 0f) => origin + new Vector3(x, y, z);

            // Ground, a worn path from where you arrive up to the farmhouse.
            var scenery = GrayboxKit.Group("Scenery", root).transform;
            kit.Box("Ground", scenery, P(0f, 0f, -0.1f), new Vector3(RealmHalfSize * 2f, 0.2f, RealmHalfSize * 2f), soil);
            for (int i = 0; i < 14; i++)
            {
                float z = -36f + i * 6f;
                float x = Mathf.Sin(i * 0.7f) * 3f;
                kit.Box("Path", scenery, P(x, z, 0.005f), new Vector3(2.2f, 0.01f, 6.4f), path, false,
                    Quaternion.Euler(0f, Mathf.Cos(i * 0.7f) * 18f, 0f));
            }

            // Tall grass: thousands of strokes in the real thing; a few hundred clumps here. It hides the entity.
            var field = GrayboxKit.Group("Grass", scenery).transform;
            for (int i = 0; i < 520; i++)
            {
                float x = Rand(-RealmHalfSize + 2f, RealmHalfSize - 2f);
                float z = Rand(-RealmHalfSize + 2f, RealmHalfSize - 2f);
                if (Mathf.Abs(x - Mathf.Sin((z + 36f) / 6f * 0.7f) * 3f) < 2f && z < 48f)
                    continue; // keep the path clear
                float height = Rand(0.9f, 2.0f);
                for (int b = 0; b < 3; b++)
                    kit.Box("Blade", field, P(x + Rand(-0.3f, 0.3f), z + Rand(-0.3f, 0.3f), height * 0.5f),
                        new Vector3(0.12f, height * Rand(0.8f, 1.1f), 0.12f), grass[rng.Next(grass.Length)], false,
                        Quaternion.Euler(Rand(-12f, 12f), Rand(0f, 180f), Rand(-12f, 12f)));
            }

            // The farmhouse at the top of the path. One window lit. Nobody has lived here for a long time.
            var farm = GrayboxKit.Group("Farmhouse", scenery).transform;
            kit.Box("House", farm, P(6f, 50f, 3f), new Vector3(9f, 6f, 7f), house);
            kit.Box("RoofL", farm, P(3.6f, 50f, 7.3f), new Vector3(5.6f, 0.3f, 7.6f), roof, false, Quaternion.Euler(0f, 0f, 38f));
            kit.Box("RoofR", farm, P(8.4f, 50f, 7.3f), new Vector3(5.6f, 0.3f, 7.6f), roof, false, Quaternion.Euler(0f, 0f, -38f));
            kit.Box("Chimney", farm, P(9f, 51f, 8.5f), new Vector3(0.8f, 3f, 0.8f), roof);
            kit.Box("WindowLit", farm, P(4f, 46.45f, 4.2f), new Vector3(1f, 1.3f, 0.05f), window, false);
            kit.Box("WindowDark", farm, P(8.5f, 46.45f, 4.2f), new Vector3(1f, 1.3f, 0.05f), wood, false);
            kit.Box("Door", farm, P(6f, 46.45f, 1.1f), new Vector3(1.1f, 2.2f, 0.05f), wood, false);
            var houseLight = GrayboxKit.PointLight("WindowGlow", farm, P(4f, 45.5f, 4.2f), new Color(1f, 0.55f, 0.2f), 3f, 9f);
            houseLight.transform.SetParent(farm, true);

            // Fences along the path, half fallen.
            var fences = GrayboxKit.Group("Fences", scenery).transform;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 16; i++)
            {
                float z = -30f + i * 4.5f;
                float x = side * 4.5f + Mathf.Sin(z * 0.12f) * 2f;
                if (rng.NextDouble() < 0.25)
                    continue;
                float lean = Rand(-14f, 14f);
                kit.Box("Post", fences, P(x, z, 0.6f), new Vector3(0.15f, 1.2f, 0.15f), wood, true, Quaternion.Euler(lean, 0f, lean * 0.5f));
                if (i < 15 && rng.NextDouble() < 0.7)
                    kit.Box("Rail", fences, P(x, z + 2.25f, 0.9f), new Vector3(0.08f, 0.1f, 4.5f), wood, false, Quaternion.Euler(Rand(-6f, 6f), 0f, 0f));
            }

            // Dead trees, black against the dusk.
            var trees = GrayboxKit.Group("DeadTrees", scenery).transform;
            foreach (var spot in new[] { new Vector2(-18f, 12f), new Vector2(20f, 30f), new Vector2(-30f, -20f), new Vector2(28f, -25f), new Vector2(-12f, 40f), new Vector2(35f, 10f) })
                DeadTree(trees, P(spot.x, spot.y), wood);

            // Dusk: a low, sick sun behind the farmhouse. Only on while inside.
            var lighting = GrayboxKit.Group("RealmLighting", root);
            var sun = new GameObject("DuskSun");
            sun.transform.SetParent(lighting.transform, false);
            sun.transform.rotation = Quaternion.Euler(9f, 200f, 0f);
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.6f, 0.32f);
            sunLight.intensity = 1.3f;
            sunLight.shadows = LightShadows.Soft;
            var wind = CreateAmbience("RealmWind", 0.35f, 0.55f);
            wind.transform.SetParent(lighting.transform, false);

            // Where you arrive: standing in front of the back of the frame you came through.
            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root, false);
            arrival.SetPositionAndRotation(P(0f, -34f, 0.05f), Quaternion.identity);
            var exit = BuildExitFrame(root, P(0f, -36f), kit.Get("M_Painting_Gilt", new Color(0.42f, 0.3f, 0.12f), 0.55f, 0.8f));
            SerializedFields.Set(exit, "realm", realm);

            // The easels: three paintings within the painting. Burn them.
            var anchors = new List<Object>
            {
                BuildEasel(root, P(-24f, -8f), Quaternion.Euler(0f, 140f, 0f), realm),
                BuildEasel(root, P(23f, 4f), Quaternion.Euler(0f, 230f, 0f), realm),
                BuildEasel(root, P(-7f, 28f), Quaternion.Euler(0f, 170f, 0f), realm),
            };

            // The entity starts by the farmhouse door.
            var entity = BuildEntity(root, P(6f, 44f));

            // NavMesh for the entity, baked from colliders at runtime.
            var navMesh = new GameObject("RealmNavMesh");
            navMesh.transform.SetParent(root, false);
            navMesh.transform.position = P(0f, 0f, 3f);
            navMesh.AddComponent<RuntimeNavMesh>().Size = new Vector3(RealmHalfSize * 2f, 10f, RealmHalfSize * 2f);

            // Edges: invisible walls, hidden by fog.
            var bounds = GrayboxKit.Group("Bounds", root).transform;
            float e = RealmHalfSize - 1f;
            GrayboxKit.InvisibleWall("EdgeN", bounds, P(0f, e, 5f), new Vector3(RealmHalfSize * 2f, 10f, 1f));
            GrayboxKit.InvisibleWall("EdgeS", bounds, P(0f, -e, 5f), new Vector3(RealmHalfSize * 2f, 10f, 1f));
            GrayboxKit.InvisibleWall("EdgeE", bounds, P(e, 0f, 5f), new Vector3(1f, 10f, RealmHalfSize * 2f));
            GrayboxKit.InvisibleWall("EdgeW", bounds, P(-e, 0f, 5f), new Vector3(1f, 10f, RealmHalfSize * 2f));

            // The oil-paint filter and the painting's colour grade.
            var filterObject = new GameObject("PaintFilter");
            filterObject.transform.SetParent(root, false);
            var filter = filterObject.AddComponent<PaintFilter>();
            filter.Feature = EnsurePaintFeature();
            filter.enabled = false;
            var volume = CreateVolume("PaintingVolume", RealmPostFxPath, true, 10f);
            volume.transform.SetParent(root, false);

            // Only switched on while the player is inside.
            lighting.SetActive(false);
            volume.SetActive(false);

            SerializedFields.Set(realm, "arrivalPoint", arrival);
            SerializedFields.Set(realm, "entity", entity);
            SerializedFields.SetArray(realm, "anchors", anchors);
            SerializedFields.Set(realm, "attention", attention);
            SerializedFields.Set(realm, "paintFilter", filter);
            SerializedFields.Set(realm, "realmVolume", volume);
            SerializedFields.Set(realm, "realmLighting", lighting);
            SerializedFields.SetArray(realm, "outsideOnly", outsideOnly.ConvertAll(go => (Object)go));
            return realm;
        }

        static void DeadTree(Transform parent, Vector3 basePos, Material wood)
        {
            var tree = GrayboxKit.Group("DeadTree", parent).transform;
            float height = Rand(5f, 8f);
            kit.Cylinder("Trunk", tree, basePos + Vector3.up * height * 0.5f, new Vector3(0.45f, height, 0.45f), wood);
            for (int i = 0; i < 5; i++)
            {
                float y = height * Rand(0.45f, 0.95f);
                float len = Rand(1.5f, 3.2f);
                float yaw = Rand(0f, 360f);
                var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(Rand(-60f, -25f), 0f, 0f);
                kit.Box("Branch", tree, basePos + Vector3.up * y + rot * Vector3.forward * len * 0.5f,
                    new Vector3(0.12f, 0.12f, len), wood, false, rot);
            }
        }

        static RealmExit BuildExitFrame(Transform parent, Vector3 position, Material gilt)
        {
            var root = GrayboxKit.Group("ExitFrame", parent).transform;
            root.position = position;
            var back = kit.Get("M_Painting_CanvasBack", new Color(0.5f, 0.44f, 0.33f), 0.05f);
            const float w = 2.2f, h = 3f, f = 0.25f;
            // Seen from inside: the raw back of the canvas, stretcher bars and all, standing in the field.
            var panel = kit.Box("CanvasBack", root, position + new Vector3(0f, h / 2 + 0.2f, 0f), new Vector3(w, h, 0.05f), back);
            kit.Box("Stretcher_H", root, position + new Vector3(0f, h / 2 + 0.2f, 0.06f), new Vector3(w, 0.08f, 0.06f), gilt, false);
            kit.Box("Stretcher_V", root, position + new Vector3(0f, h / 2 + 0.2f, 0.06f), new Vector3(0.08f, h, 0.06f), gilt, false);
            kit.Box("Frame_Top", root, position + new Vector3(0f, h + 0.2f + f / 2, 0f), new Vector3(w + 2 * f, f, 0.15f), gilt);
            kit.Box("Frame_Bottom", root, position + new Vector3(0f, 0.2f - f / 2, 0f), new Vector3(w + 2 * f, f, 0.15f), gilt);
            kit.Box("Frame_Left", root, position + new Vector3(-w / 2 - f / 2, h / 2 + 0.2f, 0f), new Vector3(f, h, 0.15f), gilt);
            kit.Box("Frame_Right", root, position + new Vector3(w / 2 + f / 2, h / 2 + 0.2f, 0f), new Vector3(f, h, 0.15f), gilt);
            return panel.AddComponent<RealmExit>();
        }

        static BanishAnchor BuildEasel(Transform parent, Vector3 position, Quaternion rotation, PaintedRealm realm)
        {
            var wood = kit.Get("M_Painting_EaselWood", new Color(0.4f, 0.28f, 0.16f), 0.1f);
            var canvasMat = kit.Get("M_Painting_SmallCanvas", new Color(0.55f, 0.48f, 0.38f), 0.05f);
            var root = GrayboxKit.Group("Easel", parent).transform;
            root.SetPositionAndRotation(position, rotation);
            Vector3 L(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
            var legs = new List<Renderer>();
            legs.Add(kit.Box("LegL", root, L(-0.35f, 0.85f, 0f), new Vector3(0.06f, 1.75f, 0.06f), wood, false, rotation * Quaternion.Euler(-8f, 0f, -10f)).GetComponent<Renderer>());
            legs.Add(kit.Box("LegR", root, L(0.35f, 0.85f, 0f), new Vector3(0.06f, 1.75f, 0.06f), wood, false, rotation * Quaternion.Euler(-8f, 0f, 10f)).GetComponent<Renderer>());
            legs.Add(kit.Box("LegBack", root, L(0f, 0.8f, 0.45f), new Vector3(0.06f, 1.7f, 0.06f), wood, false, rotation * Quaternion.Euler(20f, 0f, 0f)).GetComponent<Renderer>());
            legs.Add(kit.Box("Ledge", root, L(0f, 0.85f, -0.08f), new Vector3(0.9f, 0.05f, 0.1f), wood, false, rotation).GetComponent<Renderer>());
            // The small painting on it: a dark shape on pale ground, someone it took.
            var canvas = kit.Box("SmallCanvas", root, L(0f, 1.3f, -0.05f), new Vector3(0.8f, 0.95f, 0.04f), canvasMat, true, rotation * Quaternion.Euler(-8f, 0f, 0f));
            var figure = kit.Box("Figure", root, L(0f, 1.25f, -0.08f), new Vector3(0.22f, 0.6f, 0.01f), kit.Get("M_Painting_Figure", new Color(0.06f, 0.05f, 0.05f), 0.05f), false, rotation * Quaternion.Euler(-8f, 0f, 0f));
            legs.Add(canvas.GetComponent<Renderer>());
            legs.Add(figure.GetComponent<Renderer>());
            foreach (var r in legs)
                r.transform.SetParent(root, true);

            var fire = GrayboxKit.PointLight("Fire", root, L(0f, 1.4f, -0.4f), new Color(1f, 0.45f, 0.12f), 6f, 9f);
            fire.transform.SetParent(root, true);
            var anchor = canvas.AddComponent<BanishAnchor>();
            SerializedFields.SetArray(anchor, "burnRenderers", legs.ConvertAll(r => (Object)r));
            SerializedFields.Set(anchor, "fire", fire);
            return anchor;
        }

        /// <summary>
        /// Placeholder body: very tall and thin, a long dark coat, arms hanging past the knees, and
        /// a head with the face wiped away into a pale smear. Replace with a model later.
        /// </summary>
        static PaintedEntity BuildEntity(Transform parent, Vector3 position)
        {
            var coat = kit.Get("M_Painting_EntityCoat", new Color(0.035f, 0.03f, 0.03f), 0.1f);
            var skin = kit.Get("M_Painting_EntitySmear", new Color(0.66f, 0.55f, 0.45f), 0.6f);
            var root = new GameObject("PaintedEntity");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            var t = root.transform;
            Vector3 L(float x, float y, float z) => t.TransformPoint(new Vector3(x, y, z));

            Body(kit.Cylinder("Coat", t, L(0f, 1.05f, 0f), new Vector3(0.55f, 2.1f, 0.4f), coat, false));
            Body(kit.Cylinder("Neck", t, L(0f, 2.25f, 0f), new Vector3(0.14f, 0.35f, 0.14f), skin, false));
            Body(kit.Box("Shoulders", t, L(0f, 2.05f, 0f), new Vector3(0.75f, 0.18f, 0.32f), coat, false, t.rotation));
            Body(kit.Box("ArmL", t, L(-0.42f, 1.25f, 0.02f), new Vector3(0.1f, 1.6f, 0.1f), coat, false, t.rotation * Quaternion.Euler(0f, 0f, -3f)));
            Body(kit.Box("ArmR", t, L(0.42f, 1.25f, 0.02f), new Vector3(0.1f, 1.6f, 0.1f), coat, false, t.rotation * Quaternion.Euler(0f, 0f, 3f)));
            Body(kit.Box("HandL", t, L(-0.45f, 0.38f, 0.02f), new Vector3(0.08f, 0.3f, 0.12f), skin, false, t.rotation));
            Body(kit.Box("HandR", t, L(0.45f, 0.38f, 0.02f), new Vector3(0.08f, 0.3f, 0.12f), skin, false, t.rotation));
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = coat;
            head.transform.SetParent(t, false);
            head.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            head.transform.localScale = new Vector3(0.32f, 0.42f, 0.34f);
            // The smear: the face dragged sideways off the head.
            var smear = kit.Box("FaceSmear", t, L(0.08f, 2.55f, 0.15f), new Vector3(0.42f, 0.22f, 0.06f), skin, false, t.rotation * Quaternion.Euler(0f, 0f, -14f));
            Body(smear);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.6f;
            return root.AddComponent<PaintedEntity>();

            void Body(GameObject part)
            {
                part.transform.SetParent(t, true);
                GameObjectUtility.SetStaticEditorFlags(part, 0); // it moves
            }
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>
        /// Adds the "PaintedRealm" Full Screen Pass feature to the project's URP renderer, switched
        /// off. <see cref="PaintFilter"/> switches it on while the player is inside a painting.
        /// </summary>
        public static ScriptableRendererFeature EnsurePaintFeature()
        {
            RenderPipelineSetup.EnsureUrp();
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                Debug.LogError("[Horror Game] " + RendererPath + " not found; the paint filter cannot be added.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(FilterMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("HorrorGame/PaintedRealm");
                if (shader == null)
                {
                    Debug.LogError("[Horror Game] HorrorGame/PaintedRealm shader not found.");
                    return null;
                }
                material = new Material(shader) { name = "M_PaintedRealmFilter" };
                AssetDatabase.CreateAsset(material, FilterMaterialPath);
            }

            foreach (var existing in data.rendererFeatures)
                if (existing != null && existing.name == FeatureName)
                {
                    existing.SetActive(false);
                    return existing;
                }

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = FeatureName;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = true;
            feature.passMaterial = material;
            feature.SetActive(false);
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);

            // The renderer also keeps a map of its features' file IDs; keep it in step.
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                var serialized = new SerializedObject(data);
                var map = serialized.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Added the PaintedRealm full-screen feature (off) to " + RendererPath);
            return feature;
        }

        static GameObject CreateVolume(string name, string profilePath, bool painting, float priority)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                AssetFolders.Ensure(ProjectPaths.Settings);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
                var color = profile.Add<ColorAdjustments>(true);
                var vignette = profile.Add<Vignette>(true);
                var grain = profile.Add<FilmGrain>(true);
                var bloom = profile.Add<Bloom>(true);
                if (painting)
                {
                    // Rich, oily, slightly sour colour. Little grain: the paint is the texture now.
                    color.postExposure.Override(0.4f);
                    color.contrast.Override(18f);
                    color.saturation.Override(10f);
                    color.colorFilter.Override(new Color(1f, 0.93f, 0.8f));
                    vignette.intensity.Override(0.38f);
                    vignette.smoothness.Override(0.5f);
                    grain.type.Override(FilmGrainLookup.Thin1);
                    grain.intensity.Override(0.12f);
                    bloom.threshold.Override(0.9f);
                    bloom.intensity.Override(0.6f);
                }
                else
                {
                    // The approved look, same as the factory.
                    color.postExposure.Override(1.0f);
                    color.contrast.Override(10f);
                    color.saturation.Override(-40f);
                    vignette.intensity.Override(0.42f);
                    vignette.smoothness.Override(0.45f);
                    grain.type.Override(FilmGrainLookup.Medium1);
                    grain.intensity.Override(0.5f);
                    bloom.threshold.Override(1f);
                    bloom.intensity.Override(0.5f);
                    profile.Add<ChromaticAberration>(true).intensity.Override(0.15f);
                }
                foreach (var component in profile.components)
                {
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            var go = new GameObject(name);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            return go;
        }

        static GameObject CreateAmbience(string name, float volume, float pitch)
        {
            var go = new GameObject(name);
            go.AddComponent<AudioSource>().pitch = pitch;
            var ambient = go.AddComponent<AmbientSound>();
            SerializedFields.SetFloat(ambient, "volume", volume);
            SerializedFields.SetBool(ambient, "randomizePitch", false);
            return go;
        }

        static void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.04f, 0.045f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.04f;
            RenderSettings.fogColor = ParlorFog;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static void SpawnPlayer(GameObject prefab, Vector3 position)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = ParlorFog;
            }
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

        static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
