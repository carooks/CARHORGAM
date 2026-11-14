using System.Collections.Generic;
using HorrorGame.AI;
using HorrorGame.Audio;
using HorrorGame.Interaction;
using HorrorGame.Painting;
using HorrorGame.Painting.Strokes;
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
    ///            painting: a dusk field of tall grass with a choir buried in it up to their necks,
    ///            a dead farmhouse, fences, dead trees, three easels to burn, the thing that stands
    ///            up when the singing stops, and the frame you escape through. Everything in it is
    ///            covered in real brushstrokes (<see cref="BrushStrokeWorld"/>): every object gets a
    ///            <see cref="BrushStrokeSource"/>, the grass and the sky are nothing but strokes.
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
        const string StrokeShaderPath = ProjectPaths.Root + "/Shaders/BrushStroke.shader";
        const string StrokeCullPath = ProjectPaths.Root + "/Shaders/BrushStrokeCull.compute";
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

            Vector3 P(float x, float z, float y = 0f) => origin + new Vector3(x, y, z);

            // Ground, a worn path from where you arrive up to the farmhouse.
            var scenery = GrayboxKit.Group("Scenery", root).transform;
            var ground = kit.Box("Ground", scenery, P(0f, 0f, -0.1f), new Vector3(RealmHalfSize * 2f, 0.2f, RealmHalfSize * 2f), soil);
            Strokes(ground, 9f, new Vector2(0.45f, 0.8f), new Vector2(0.12f, 0.2f), 0.18f);
            var pathGroup = GrayboxKit.Group("Path", scenery);
            Strokes(pathGroup, 22f, new Vector2(0.3f, 0.55f), new Vector2(0.08f, 0.14f), 0.2f).lift = 0.014f;
            for (int i = 0; i < 14; i++)
            {
                float z = -36f + i * 6f;
                float x = Mathf.Sin(i * 0.7f) * 3f;
                kit.Box("Path", pathGroup.transform, P(x, z, 0.005f), new Vector3(2.2f, 0.01f, 6.4f), path, false,
                    Quaternion.Euler(0f, Mathf.Cos(i * 0.7f) * 18f, 0f));
            }

            // Where the choir is buried: a line of them along the path, the rest scattered in the grass.
            var spots = new List<Vector2>();
            for (int i = 0; i < 7; i++)
            {
                float z = -24f + i * 9f;
                float x = Mathf.Sin((z + 36f) / 6f * 0.7f) * 3f;
                spots.Add(new Vector2(x + (i % 2 == 0 ? -2.6f : 2.6f), z));
            }
            while (spots.Count < 28)
            {
                var p = new Vector2(Rand(-45f, 45f), Rand(-30f, 42f));
                bool clear = Vector2.Distance(p, new Vector2(0f, -34f)) > 6f;
                foreach (var other in spots)
                    clear &= Vector2.Distance(p, other) > 5f;
                if (clear)
                    spots.Add(p);
            }

            // Four identical black churches in the fog. The path leads to a false one.
            var churches = GrayboxKit.Group("Churches", scenery).transform;
            var churchDoors = new List<ChurchDoor>
            {
                BuildChurch(churches, P(3f, 52f), 180f, realm),   // top of the path: false
                BuildChurch(churches, P(-40f, 16f), 110f, realm), // false
                BuildChurch(churches, P(30f, 42f), 220f, realm),  // false
                BuildChurch(churches, P(40f, -8f), 270f, realm),  // the real one, off in the grass
            };
            var realDoor = churchDoors[3];
            SerializedFields.SetBool(realDoor, "isReal", true);

            // Tall dusk grass, nothing but brushstrokes: upright blades that sway, hiding the entity
            // up to the chest. Cleared along the path, around each head (so you can see the faces),
            // around the churches and where you arrive.
            var clearings = new List<Vector3>();
            for (float z = -38f; z <= 50f; z += 1.2f)
                clearings.Add(new Vector3(Mathf.Sin((z + 36f) / 6f * 0.7f) * 3f, z, 1.6f));
            foreach (var spot in spots)
                clearings.Add(new Vector3(spot.x, spot.y, 1.7f));
            foreach (var door in churchDoors)
            {
                var church = door.transform.parent;
                Vector3 Local(float x, float z) => church.TransformPoint(new Vector3(x, 0f, z)) - origin;
                var nave = Local(0f, -7f);
                var tower = Local(0f, 1.2f);
                var front = Local(0f, 4.5f);
                clearings.Add(new Vector3(nave.x, nave.z, 8.5f));
                clearings.Add(new Vector3(tower.x, tower.z, 3.2f));
                clearings.Add(new Vector3(front.x, front.z, 2.6f));
            }
            clearings.Add(new Vector3(0f, -35f, 3f));
            var grassObject = GrayboxKit.Group("GrassStrokes", scenery);
            grassObject.transform.position = origin;
            var grass = grassObject.AddComponent<BrushStrokeSource>();
            grass.kind = StrokeKind.Grass;
            grass.density = 24f;
            grass.length = new Vector2(0.5f, 1.55f);
            grass.width = new Vector2(0.035f, 0.07f);
            grass.sampleWorld = 0f;
            grass.color = new Color(0.58f, 0.45f, 0.19f);
            grass.color2 = new Color(0.3f, 0.27f, 0.13f);
            grass.colorJitter = 0.25f;
            grass.grassHalfSize = new Vector2(RealmHalfSize - 2f, RealmHalfSize - 2f);
            grass.grassClearings = clearings.ToArray();

            // Dead brambles: dark knots of thorny strokes in the grass.
            var brambleMat = kit.Get("M_Painting_Bramble", new Color(0.06f, 0.045f, 0.035f), 0.05f);
            var brambles = GrayboxKit.Group("Brambles", scenery);
            var bramble = brambles.AddComponent<BrushStrokeSource>();
            bramble.density = 40f;
            bramble.length = new Vector2(0.15f, 0.35f);
            bramble.width = new Vector2(0.04f, 0.09f);
            bramble.sampleWorld = 0f;
            bramble.color = new Color(0.09f, 0.06f, 0.04f);
            bramble.color2 = new Color(0.17f, 0.08f, 0.05f);
            bramble.colorJitter = 0.3f;
            for (int placed = 0, tries = 0; placed < 36 && tries < 400; tries++)
            {
                var at = new Vector2(Rand(-52f, 52f), Rand(-52f, 52f));
                bool free = true;
                foreach (var c in clearings)
                    free &= Vector2.Distance(at, new Vector2(c.x, c.y)) > c.z + 1.5f;
                if (!free)
                    continue;
                placed++;
                int lumps = rng.Next(3, 6);
                for (int k = 0; k < lumps; k++)
                {
                    var lump = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    lump.name = "Bramble";
                    Object.DestroyImmediate(lump.GetComponent<Collider>());
                    lump.transform.SetParent(brambles.transform, false);
                    float w = Rand(0.7f, 1.8f);
                    lump.transform.position = P(at.x + Rand(-1f, 1f), at.y + Rand(-1f, 1f), Rand(0.1f, 0.5f));
                    lump.transform.localScale = new Vector3(w, Rand(0.6f, 1.2f), w * Rand(0.7f, 1.2f));
                    lump.GetComponent<Renderer>().sharedMaterial = brambleMat;
                }
            }

            // Fences along the path, half fallen.
            var fences = GrayboxKit.Group("Fences", scenery).transform;
            Strokes(fences.gameObject, 60f, new Vector2(0.12f, 0.22f), new Vector2(0.04f, 0.07f), 0.25f);
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
            Strokes(trees.gameObject, 50f, new Vector2(0.15f, 0.3f), new Vector2(0.04f, 0.08f), 0.25f);
            foreach (var spot in new[] { new Vector2(-18f, 12f), new Vector2(20f, 30f), new Vector2(-30f, -20f), new Vector2(28f, -25f), new Vector2(-12f, 40f), new Vector2(35f, 10f) })
                DeadTree(trees, P(spot.x, spot.y), wood);

            // Dusk: a low, sick sun. Only on while inside.
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

            // A painted dusk sky: big swirling strokes on a dome that follows you, melting into the
            // fog at the horizon, sour orange where the sun is going down.
            var skyObject = GrayboxKit.Group("PaintedSky", root);
            var sky = skyObject.AddComponent<BrushStrokeSource>();
            sky.kind = StrokeKind.Sky;
            sky.density = 9000f;
            sky.length = new Vector2(9f, 18f);
            sky.width = new Vector2(3f, 5.5f);
            sky.sampleWorld = 0f;
            sky.color = new Color(0.36f, 0.26f, 0.16f);
            sky.colorJitter = 0.12f;
            sky.skyZenith = new Color(0.09f, 0.06f, 0.08f);
            sky.skyGlow = new Color(0.9f, 0.52f, 0.22f);
            sky.skyGlowDirection = -(sun.transform.rotation * Vector3.forward);

            // Where you arrive: standing in front of the back of the frame you came through.
            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(root, false);
            arrival.SetPositionAndRotation(P(0f, -34f, 0.05f), Quaternion.identity);
            BuildCanvasBack(root, P(0f, -36f));

            // Where false church doors drop you: lost somewhere else in the fog.
            var lost = GrayboxKit.Group("LostPoints", root).transform;
            var lostPoints = new List<Object>();
            foreach (var spot in new[] { new Vector3(-44f, 0f, -30f), new Vector3(-20f, 0f, 30f), new Vector3(20f, 0f, -40f), new Vector3(0f, 0f, 5f) })
            {
                var point = new GameObject("Lost").transform;
                point.SetParent(lost, false);
                point.SetPositionAndRotation(P(spot.x, spot.z, 0.05f), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
                lostPoints.Add(point);
            }

            // The real church, bigger inside than out: the last stretch.
            BuildChurchInterior(root, P(0f, 200f), soil, out var churchArrival, out var altar, out var finaleSpot,
                out var churchSingers);
            SerializedFields.Set(realDoor, "inside", churchArrival);

            // The thing waits under the paint until the singing stops.
            var entity = BuildEntity(root, P(6f, 40f));

            // The choir: heads buried to the neck, scattered through the grass and lining the path.
            var choirRoot = GrayboxKit.Group("Choir", root).transform;
            var choir = choirRoot.gameObject.AddComponent<Choir>();
            var singers = new List<ChoirSinger>();
            float[] chord = { 1f, 1.189f, 1.498f, 2f, 0.5f, 1.189f }; // a minor chord with a low drone
            for (int i = 0; i < spots.Count; i++)
                singers.Add(BuildSinger(choirRoot, P(spots[i].x, spots[i].y), chord[i % chord.Length], soil));
            singers.AddRange(churchSingers);

            // The missing voices: four singers far apart around the field with their mouths painted
            // over in white. The song leads from one to the next, all the way round the field.
            var voices = new List<Object>();
            var white = kit.Get("M_Painting_WhitePaint", new Color(0.92f, 0.9f, 0.85f), 0.7f);
            foreach (var want in new[] { new Vector2(-30f, -12f), new Vector2(-24f, 34f), new Vector2(22f, 26f), new Vector2(28f, -30f) })
            {
                // The field singer closest to each corner of the route gets painted over.
                int best = 0;
                for (int i = 1; i < spots.Count; i++)
                    if (Vector2.Distance(spots[i], want) < Vector2.Distance(spots[best], want))
                        best = i;
                var singer = singers[best];
                var head = singer.transform.Find("Head");
                // The paint sits on an unscaled pivot that carries its strokes; scraping it off hides both.
                var paint = new GameObject("WhitePaint");
                paint.transform.SetParent(head, false);
                paint.transform.localPosition = new Vector3(0f, -0.06f, 0.15f);
                paint.transform.localRotation = Quaternion.Euler(0f, 0f, Rand(-12f, 12f));
                var smear = GameObject.CreatePrimitive(PrimitiveType.Cube);
                smear.name = "Smear";
                Object.DestroyImmediate(smear.GetComponent<Collider>());
                smear.GetComponent<Renderer>().sharedMaterial = white;
                smear.transform.SetParent(paint.transform, false);
                smear.transform.localScale = new Vector3(0.24f, 0.13f, 0.04f);
                var paintStrokes = paint.AddComponent<BrushStrokeSource>();
                paintStrokes.moving = true;
                paintStrokes.visibility = smear.GetComponent<Renderer>();
                paintStrokes.density = 2500f;
                paintStrokes.length = new Vector2(0.05f, 0.1f);
                paintStrokes.width = new Vector2(0.025f, 0.04f);
                paintStrokes.lift = 0.016f;
                paintStrokes.sampleWorld = 0f;
                paintStrokes.color = new Color(0.92f, 0.9f, 0.85f);
                paintStrokes.color2 = new Color(0.99f, 0.97f, 0.92f);
                paintStrokes.colorJitter = 0.06f;
                // A collider on the head so the interaction ray can find it.
                var hit = head.gameObject.AddComponent<SphereCollider>();
                hit.radius = 0.5f;
                var voice = singer.gameObject.AddComponent<MutedVoice>();
                SerializedFields.Set(voice, "paint", paint);
                voices.Add(voice);
            }

            // The cavernous echo over the whole field and the church.
            var reverbObject = new GameObject("ChoirReverb");
            reverbObject.transform.SetParent(choirRoot, false);
            reverbObject.transform.position = origin + new Vector3(0f, 0f, 80f);
            var reverb = reverbObject.AddComponent<AudioReverbZone>();
            reverb.reverbPreset = AudioReverbPreset.Concerthall;
            reverb.minDistance = 200f;
            reverb.maxDistance = 260f;
            SerializedFields.Set(choir, "reverb", reverb);

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
            SerializedFields.Set(realm, "choir", choir);
            SerializedFields.Set(realm, "altar", altar);
            SerializedFields.Set(realm, "finaleSpot", finaleSpot);
            SerializedFields.Set(realm, "realChurchDoor", realDoor.transform);
            SerializedFields.SetArray(realm, "lostPoints", lostPoints);
            SerializedFields.Set(choir, "realm", realm);
            SerializedFields.Set(choir, "entity", entity);
            SerializedFields.SetArray(choir, "singers", singers.ConvertAll(x => (Object)x));
            SerializedFields.SetArray(realm, "voices", voices);
            SerializedFields.Set(realm, "attention", attention);
            SerializedFields.Set(realm, "paintFilter", filter);
            SerializedFields.Set(realm, "realmVolume", volume);
            SerializedFields.Set(realm, "realmLighting", lighting);
            SerializedFields.SetArray(realm, "outsideOnly", outsideOnly.ConvertAll(go => (Object)go));

            // The brushstrokes over all of it.
            var strokeWorld = root.gameObject.AddComponent<BrushStrokeWorld>();
            SerializedFields.Set(strokeWorld, "strokeShader", AssetDatabase.LoadAssetAtPath<Shader>(StrokeShaderPath));
            SerializedFields.Set(strokeWorld, "cullShader", AssetDatabase.LoadAssetAtPath<ComputeShader>(StrokeCullPath));
            SerializedFields.Set(strokeWorld, "attention", attention);
            entity.Configure(entity.GetComponent<EntityPoser>(), strokeWorld);
            BakeStrokeShapes(root);
            return realm;
        }

        /// <summary>
        /// One black church, all four identical: a nave, a pitched roof, a square tower with a spire,
        /// red slits for windows, and a double door facing <paramref name="yaw"/>.
        /// </summary>
        static ChurchDoor BuildChurch(Transform parent, Vector3 position, float yaw, PaintedRealm realm)
        {
            var black = kit.Get("M_Painting_ChurchBlack", new Color(0.02f, 0.018f, 0.02f), 0.15f);
            var glow = kit.Get("M_Painting_ChurchGlow", new Color(0.6f, 0.05f, 0.03f), 0.3f, 0f, new Color(1f, 0.08f, 0.03f) * 2f);
            var doorMat = kit.Get("M_Painting_ChurchDoor", new Color(0.12f, 0.02f, 0.02f), 0.3f);
            var root = GrayboxKit.Group("BlackChurch", parent).transform;
            Strokes(root.gameObject, 14f, new Vector2(0.5f, 0.9f), new Vector2(0.14f, 0.24f), 0.35f);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            root.SetPositionAndRotation(position, rot);
            Vector3 L(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z));
            // Local +Z is the front (the door side).
            kit.Box("Nave", root, L(0f, 3.5f, -7f), new Vector3(8f, 7f, 14f), black, true, rot);
            kit.Box("RoofL", root, L(-2.3f, 8.4f, -7f), new Vector3(5.6f, 0.3f, 14.4f), black, false, rot * Quaternion.Euler(0f, 0f, 45f));
            kit.Box("RoofR", root, L(2.3f, 8.4f, -7f), new Vector3(5.6f, 0.3f, 14.4f), black, false, rot * Quaternion.Euler(0f, 0f, -45f));
            kit.Box("Tower", root, L(0f, 6.5f, 1.2f), new Vector3(3.4f, 13f, 3.4f), black, true, rot);
            var spire = new GameObject("Spire");
            spire.transform.SetParent(root, false);
            spire.transform.position = L(0f, 13f, 1.2f);
            spire.transform.rotation = rot;
            spire.transform.localScale = new Vector3(3.6f, 9f, 3.6f);
            spire.AddComponent<MeshFilter>().sharedMesh = GrayboxKit.ConeMesh();
            spire.AddComponent<MeshRenderer>().sharedMaterial = black;
            for (int i = 0; i < 4; i++)
                for (int side = -1; side <= 1; side += 2)
                    kit.Box("WindowSlit", root, L(side * 4.02f, 3.8f, -2.5f - i * 3f), new Vector3(0.05f, 2.6f, 0.35f), glow, false, rot);
            kit.Box("TowerSlit", root, L(0f, 9f, 2.92f), new Vector3(0.35f, 1.8f, 0.05f), glow, false, rot);
            kit.Box("DoorL", root, L(-0.55f, 1.4f, 2.93f), new Vector3(1.05f, 2.8f, 0.08f), doorMat, false, rot);
            var door = kit.Box("DoorR", root, L(0.55f, 1.4f, 2.93f), new Vector3(1.05f, 2.8f, 0.08f), doorMat, true, rot);
            // One collider over both leaves so the prompt works anywhere on the door.
            Object.DestroyImmediate(door.GetComponent<Collider>());
            var hit = new GameObject("Door");
            hit.transform.SetParent(root, false);
            hit.transform.SetPositionAndRotation(L(0f, 1.4f, 2.97f), rot);
            hit.AddComponent<BoxCollider>().size = new Vector3(2.1f, 2.8f, 0.1f);
            var glowLight = GrayboxKit.PointLight("DoorGlow", root, L(0f, 3.4f, 3.6f), new Color(1f, 0.1f, 0.05f), 1.5f, 6f);
            glowLight.transform.SetParent(root, true);
            var churchDoor = hit.AddComponent<ChurchDoor>();
            SerializedFields.Set(churchDoor, "realm", realm);
            return churchDoor;
        }

        /// <summary>
        /// Inside the real church: a long black nave, the choir buried in the pews, the easels in the
        /// aisle, and behind the altar the back of the frame you came in through: the way out.
        /// Bigger inside than it is outside. Built far from the field; the real door teleports here.
        /// </summary>
        static void BuildChurchInterior(Transform parent, Vector3 origin, Material soil,
            out Transform arrival, out Transform altar, out Transform finaleSpot, out List<ChoirSinger> singers)
        {
            var black = kit.Get("M_Painting_ChurchBlack", new Color(0.02f, 0.018f, 0.02f), 0.15f);
            var stone = kit.Get("M_Painting_ChurchStone", new Color(0.16f, 0.13f, 0.12f), 0.2f);
            var pewWood = kit.Get("M_Painting_Pew", new Color(0.12f, 0.06f, 0.04f), 0.35f);
            var glow = kit.Get("M_Painting_ChurchGlow", new Color(0.6f, 0.05f, 0.03f), 0.3f, 0f, new Color(1f, 0.08f, 0.03f) * 2f);
            var cloth = kit.Get("M_Painting_AltarCloth", new Color(0.5f, 0.45f, 0.38f), 0.05f);
            var root = GrayboxKit.Group("ChurchInterior", parent).transform;
            root.position = origin;
            Strokes(root.gameObject, 12f, new Vector2(0.5f, 0.9f), new Vector2(0.14f, 0.24f), 0.3f).skipDownFacing = false;
            Vector3 P(float x, float z, float y = 0f) => origin + new Vector3(x, y, z);

            const float halfWidth = 6.5f, length = 46f, height = 11f;
            kit.Box("Floor", root, P(0f, length / 2f, -0.1f), new Vector3(halfWidth * 2f + 1f, 0.2f, length + 1f), stone);
            kit.Box("Ceiling", root, P(0f, length / 2f, height), new Vector3(halfWidth * 2f + 1f, 0.3f, length + 1f), black);
            kit.Box("WallW", root, P(-halfWidth - 0.25f, length / 2f, height / 2f), new Vector3(0.5f, height, length + 1f), black);
            kit.Box("WallE", root, P(halfWidth + 0.25f, length / 2f, height / 2f), new Vector3(0.5f, height, length + 1f), black);
            kit.Box("WallS", root, P(0f, -0.25f, height / 2f), new Vector3(halfWidth * 2f + 1f, height, 0.5f), black);
            kit.Box("WallN", root, P(0f, length + 0.25f, height / 2f), new Vector3(halfWidth * 2f + 1f, height, 0.5f), black);
            // Tall red windows down both sides, columns between them.
            for (int i = 0; i < 6; i++)
            {
                float z = 5f + i * 7f;
                for (int side = -1; side <= 1; side += 2)
                {
                    kit.Box("Window", root, P(side * (halfWidth - 0.02f), z, 5.5f), new Vector3(0.05f, 6f, 1.2f), glow, false);
                    kit.Box("Column", root, P(side * (halfWidth - 0.6f), z + 3.5f, height / 2f), new Vector3(0.8f, height, 0.8f), black);
                }
                var red = GrayboxKit.PointLight("WindowLight", root, P(0f, z, 6f), new Color(1f, 0.12f, 0.06f), 2.2f, 11f);
                red.transform.SetParent(root, true);
            }

            // The pews: rows down both sides of the aisle, filled with earth, a head buried in every one.
            singers = new List<ChoirSinger>();
            float[] chord = { 1f, 1.498f, 1.189f, 2f, 0.75f };
            int n = 0;
            for (int row = 0; row < 12; row++)
            {
                float z = 6f + row * 2.6f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 3.4f;
                    kit.Box("Pew", root, P(x, z, 0.45f), new Vector3(3.6f, 0.9f, 1.1f), pewWood);
                    kit.Box("PewBack", root, P(x, z - 0.6f, 0.9f), new Vector3(3.6f, 1.8f, 0.12f), pewWood);
                    kit.Box("PewEarth", root, P(x, z, 0.91f), new Vector3(3.4f, 0.04f, 0.9f), soil, false);
                    var singer = BuildSinger(root, P(x + Rand(-1f, 1f), z, 0.6f), chord[n++ % chord.Length], soil);
                    Object.DestroyImmediate(singer.transform.Find("Mound").gameObject);
                    SerializedFields.SetFloat(singer, "volume", 0.9f);
                    singers.Add(singer);
                }
            }

            // The altar and its candles. Reach it with the chord complete.
            kit.Box("AltarStep", root, P(0f, length - 5f, 0.2f), new Vector3(8f, 0.4f, 4f), stone);
            kit.Box("Altar", root, P(0f, length - 5f, 0.9f), new Vector3(3f, 1.0f, 1.2f), black);
            kit.Box("AltarCloth", root, P(0f, length - 5f, 1.42f), new Vector3(3.2f, 0.04f, 1.4f), cloth, false);
            for (int i = -1; i <= 1; i++)
            {
                var candle = GrayboxKit.PointLight("Candle", root, P(i * 1.1f, length - 5f, 1.8f), new Color(1f, 0.55f, 0.2f), 1.6f, 7f);
                candle.transform.SetParent(root, true);
                kit.Box("CandleStick", root, P(i * 1.1f, length - 5f, 1.6f), new Vector3(0.08f, 0.35f, 0.08f), cloth, false);
            }
            altar = new GameObject("AltarPoint").transform;
            altar.SetParent(root, false);
            altar.position = P(0f, length - 5f);
            // For the finale the thing stands up here, looming behind the altar.
            finaleSpot = new GameObject("FinaleSpot").transform;
            finaleSpot.SetParent(root, false);
            finaleSpot.position = P(0f, length - 2.5f);

            arrival = new GameObject("ChurchArrival").transform;
            arrival.SetParent(root, false);
            arrival.SetPositionAndRotation(P(0f, 1.5f, 0.05f), Quaternion.identity);

            var navMesh = new GameObject("ChurchNavMesh");
            navMesh.transform.SetParent(root, false);
            navMesh.transform.position = P(0f, length / 2f, 4f);
            navMesh.AddComponent<RuntimeNavMesh>().Size = new Vector3(halfWidth * 2f + 2f, 12f, length + 2f);
        }

        /// <summary>Where you arrive in the field: the raw back of the canvas you came through. Not a way out.</summary>
        static void BuildCanvasBack(Transform parent, Vector3 position)
        {
            var root = GrayboxKit.Group("CanvasBack", parent).transform;
            Strokes(root.gameObject, 60f, new Vector2(0.1f, 0.2f), new Vector2(0.04f, 0.07f), 0.12f);
            var back = kit.Get("M_Painting_CanvasBack", new Color(0.5f, 0.44f, 0.33f), 0.05f);
            var gilt = kit.Get("M_Painting_Gilt", new Color(0.42f, 0.3f, 0.12f), 0.55f, 0.8f);
            kit.Box("Canvas", root, position + new Vector3(0f, 1.7f, 0f), new Vector3(2.2f, 3f, 0.05f), back);
            kit.Box("Stretcher_H", root, position + new Vector3(0f, 1.7f, 0.06f), new Vector3(2.2f, 0.08f, 0.06f), gilt, false);
            kit.Box("Stretcher_V", root, position + new Vector3(0f, 1.7f, 0.06f), new Vector3(0.08f, 3f, 0.06f), gilt, false);
        }

        /// <summary>A head buried to the neck: a mound of earth, the head, two dark eyes and a mouth that sings.</summary>
        static ChoirSinger BuildSinger(Transform parent, Vector3 groundPos, float pitch, Material soil)
        {
            var skin = kit.Get("M_Painting_ChoirSkin", new Color(0.72f, 0.6f, 0.5f), 0.35f);
            var dark = kit.Get("M_Painting_ChoirDark", new Color(0.03f, 0.015f, 0.015f), 0.6f);
            var hair = kit.Get("M_Painting_ChoirHair", new Color(0.08f, 0.06f, 0.05f), 0.1f);
            var root = GrayboxKit.Group("Singer", parent).transform;
            root.SetPositionAndRotation(groundPos, Quaternion.Euler(0f, Rand(0f, 360f), 0f));

            var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mound.name = "Mound";
            mound.transform.SetParent(root, false);
            mound.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
            mound.GetComponent<Renderer>().sharedMaterial = soil;
            GameObjectUtility.SetStaticEditorFlags(mound, StaticEditorFlags.BatchingStatic);
            Strokes(mound, 30f, new Vector2(0.15f, 0.3f), new Vector2(0.05f, 0.09f), 0.2f);

            var head = GrayboxKit.Group("Head", root).transform;
            head.localPosition = new Vector3(0f, 0.32f, 0f);
            Part(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.36f, 0.32f), skin);
            Part(PrimitiveType.Sphere, "Hair", head, new Vector3(0f, 0.08f, -0.03f), new Vector3(0.31f, 0.26f, 0.31f), hair);
            Part(PrimitiveType.Cube, "EyeL", head, new Vector3(-0.065f, 0.04f, 0.15f), new Vector3(0.05f, 0.025f, 0.02f), dark);
            Part(PrimitiveType.Cube, "EyeR", head, new Vector3(0.065f, 0.04f, 0.15f), new Vector3(0.05f, 0.025f, 0.02f), dark);
            var mouth = Part(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, -0.08f, 0.145f), new Vector3(0.07f, 0.1f, 0.03f), dark);
            // Painted in small strokes that turn with the head; the mouth has its own, so it can open.
            var headStrokes = Strokes(head.gameObject, 1100f, new Vector2(0.05f, 0.09f), new Vector2(0.02f, 0.035f), 0.2f);
            headStrokes.moving = true;
            headStrokes.lift = 0.004f;
            headStrokes.visibility = head.Find("Skull").GetComponent<Renderer>();
            var mouthStrokes = Strokes(mouth, 1100f, new Vector2(0.05f, 0.09f), new Vector2(0.02f, 0.035f), 0.1f);
            mouthStrokes.moving = true;
            mouthStrokes.lift = 0.006f;
            mouthStrokes.visibility = mouth.GetComponent<Renderer>();

            var singer = root.gameObject.AddComponent<ChoirSinger>();
            SerializedFields.Set(singer, "head", head);
            SerializedFields.Set(singer, "mouth", mouth.transform);
            SerializedFields.SetFloat(singer, "pitch", pitch);
            return singer;

            static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, Material material)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                return go;
            }
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

        /// <summary>
        /// The thing in the painting, roughly 3.8 m when it stands up straight. Long legs, a narrow
        /// hunched body, a neck too long, arms that hang to the ground and end in brush-bristle
        /// fingers. A long pale face with a white band of paint where the eyes were (the painter
        /// blinded it, so it hunts by sound) over a mouth stretched open into a black gash. Built from
        /// jointed pivots so <see cref="EntityPoser"/> can snap it between poses, and covered in
        /// restless strokes that never dry. Replace the primitives with a model later.
        /// </summary>
        static PaintedEntity BuildEntity(Transform parent, Vector3 position)
        {
            var coat = kit.Get("M_Painting_EntityCoat", new Color(0.035f, 0.03f, 0.03f), 0.1f);
            var face = kit.Get("M_Painting_EntityFace", new Color(0.55f, 0.5f, 0.42f), 0.5f);
            var hole = kit.Get("M_Painting_EntityMouth", new Color(0f, 0f, 0f), 0f);
            var white = kit.Get("M_Painting_WhitePaint", new Color(0.92f, 0.9f, 0.85f), 0.7f);
            var black = new Color(0.03f, 0.025f, 0.025f);
            var oxblood = new Color(0.2f, 0.03f, 0.02f);

            var root = new GameObject("PaintedEntity");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            var t = root.transform;

            var hips = Pivot("Hips", t, new Vector3(0f, 1.5f, 0f));
            var legL = Limb("LegL", hips, new Vector3(-0.14f, 0f, 0f), new Vector3(0.11f, 1.56f, 0.11f));
            var legR = Limb("LegR", hips, new Vector3(0.14f, 0f, 0f), new Vector3(0.11f, 1.56f, 0.11f));
            var spine = Pivot("Spine", hips, new Vector3(0f, 0.05f, 0f));
            BodyStrokes(spine, Part(PrimitiveType.Cube, "Torso", spine, new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 1.1f, 0.24f), coat), black, oxblood);
            var neck = Pivot("Neck", spine, new Vector3(0f, 1.08f, 0f));
            BodyStrokes(neck, Part(PrimitiveType.Cylinder, "NeckMesh", neck, new Vector3(0f, 0.35f, 0f), new Vector3(0.09f, 0.35f, 0.09f), coat), black, oxblood);
            var head = Pivot("Head", neck, new Vector3(0f, 0.72f, 0f));
            var skull = Part(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.5f, 0.32f), face);
            var headStrokes = BodyStrokes(head, skull, new Color(0.5f, 0.46f, 0.38f), new Color(0.62f, 0.55f, 0.44f));
            headStrokes.density = 600f;
            headStrokes.length = new Vector2(0.06f, 0.14f);
            headStrokes.width = new Vector2(0.025f, 0.05f);
            // The mouth: stretched open, a black gash down most of the face.
            var mouth = Part(PrimitiveType.Sphere, "Mouth", head, new Vector3(0f, 0.08f, 0.13f), new Vector3(0.12f, 0.34f, 0.08f), hole);
            var mouthStrokes = BodyStrokes(mouth.transform, mouth, new Color(0.004f, 0.003f, 0.003f), new Color(0.03f, 0.005f, 0.005f));
            mouthStrokes.density = 900f;
            mouthStrokes.lift = 0.02f;
            mouthStrokes.length = new Vector2(0.05f, 0.1f);
            mouthStrokes.width = new Vector2(0.02f, 0.04f);
            // Its eyes, painted over in white like the choir's mouths.
            var eyes = Part(PrimitiveType.Cube, "EyePaint", head, new Vector3(0f, 0.3f, 0.15f), new Vector3(0.3f, 0.09f, 0.05f), white, new Vector3(0f, 0f, -8f));
            var eyeStrokes = BodyStrokes(eyes.transform, eyes, new Color(0.92f, 0.9f, 0.85f), new Color(0.99f, 0.97f, 0.92f));
            eyeStrokes.kind = StrokeKind.Surface;
            eyeStrokes.density = 2500f;
            eyeStrokes.lift = 0.02f;
            eyeStrokes.length = new Vector2(0.06f, 0.12f);
            eyeStrokes.width = new Vector2(0.025f, 0.04f);
            eyeStrokes.colorJitter = 0.06f;

            Arm("L", -1f, out var shoulderL, out var elbowL, out var handL);
            Arm("R", 1f, out var shoulderR, out var elbowR, out var handR);

            var poser = root.AddComponent<EntityPoser>();
            poser.Configure(hips, spine, neck, head, shoulderL, elbowL, handL, shoulderR, elbowR, handR, legL, legR);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.6f;
            agent.enabled = false; // switched on when the player enters, after the NavMesh is baked
            return root.AddComponent<PaintedEntity>();

            void Arm(string side, float sign, out Transform shoulder, out Transform elbow, out Transform hand)
            {
                shoulder = Limb("Shoulder" + side, spine, new Vector3(sign * 0.27f, 1.0f, 0f), new Vector3(0.08f, 0.95f, 0.08f));
                elbow = Limb("Elbow" + side, shoulder, new Vector3(0f, -0.95f, 0f), new Vector3(0.07f, 0.95f, 0.07f));
                hand = Pivot("Hand" + side, elbow, new Vector3(0f, -0.95f, 0f));
                GameObject first = null;
                for (int i = 0; i < 4; i++)
                {
                    var finger = Part(PrimitiveType.Cube, "Finger", hand, new Vector3(-0.045f + i * 0.03f, -0.2f, 0f),
                        new Vector3(0.02f, 0.42f, 0.02f), coat, new Vector3(0f, 0f, (i - 1.5f) * 6f * sign));
                    first ??= finger;
                }
                BodyStrokes(hand, first, black, oxblood).length = new Vector2(0.1f, 0.22f);
            }

            // A joint with one long box hanging (or standing) from it, and its strokes.
            Transform Limb(string name, Transform parentJoint, Vector3 local, Vector3 size)
            {
                var joint = Pivot(name, parentJoint, local);
                var mesh = Part(PrimitiveType.Cube, name + "Mesh", joint, new Vector3(0f, -size.y * 0.5f, 0f), size, coat);
                BodyStrokes(joint, mesh, black, oxblood);
                return joint;
            }
        }

        static Transform Pivot(string name, Transform parent, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, Material material,
            Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Never-dry strokes that follow one of the entity's joints and vanish when it is hidden.</summary>
        static BrushStrokeSource BodyStrokes(Transform joint, GameObject mesh, Color color, Color color2)
        {
            var source = joint.gameObject.AddComponent<BrushStrokeSource>();
            source.kind = StrokeKind.Restless;
            source.moving = true;
            source.visibility = mesh != null ? mesh.GetComponent<Renderer>() : null;
            source.density = 260f;
            source.length = new Vector2(0.08f, 0.18f);
            source.width = new Vector2(0.035f, 0.065f);
            source.lift = 0.01f;
            source.sampleWorld = 0f;
            source.color = color;
            source.color2 = color2;
            source.colorJitter = 0.3f;
            return source;
        }

        // ------------------------------------------------------------------ brushstrokes

        /// <summary>Covers <paramref name="target"/> and its children in strokes coloured by the lit world under them.</summary>
        static BrushStrokeSource Strokes(GameObject target, float density, Vector2 length, Vector2 width, float jitter)
        {
            var source = target.AddComponent<BrushStrokeSource>();
            source.density = density;
            source.length = length;
            source.width = width;
            source.colorJitter = jitter;
            source.sampleWorld = 1f;
            return source;
        }

        /// <summary>
        /// Records the shapes each <see cref="BrushStrokeSource"/> covers: every mesh under it that no
        /// nearer source claims. Unity's cube, sphere and cylinder are stored as exact shapes; any other
        /// mesh is copied as triangles. Run after everything is placed.
        /// </summary>
        public static void BakeStrokeShapes(Transform root)
        {
            int count = 0;
            foreach (var source in root.GetComponentsInChildren<BrushStrokeSource>(true))
            {
                var shapes = new List<StrokeShape>();
                var tris = new List<Vector3>();
                if (source.kind != StrokeKind.Grass && source.kind != StrokeKind.Sky)
                {
                    var toSource = source.transform.worldToLocalMatrix;
                    foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null || Owner(filter.transform) != source)
                            continue;
                        var matrix = toSource * filter.transform.localToWorldMatrix;
                        var shape = new StrokeShape { matrix = matrix, detail = IsDetail(filter) };
                        switch (filter.sharedMesh.name)
                        {
                            case "Cube": shape.type = StrokeShapeType.Box; break;
                            case "Sphere": shape.type = StrokeShapeType.Sphere; break;
                            case "Cylinder": shape.type = StrokeShapeType.Cylinder; break;
                            default:
                                shape.type = StrokeShapeType.Triangles;
                                shape.matrix = Matrix4x4.identity;
                                shape.triangleStart = tris.Count / 3;
                                var mesh = filter.sharedMesh;
                                var vertices = mesh.vertices;
                                var indices = mesh.triangles;
                                foreach (int index in indices)
                                    tris.Add(matrix.MultiplyPoint3x4(vertices[index]));
                                shape.triangleCount = indices.Length / 3;
                                break;
                        }
                        shapes.Add(shape);
                    }
                }
                source.SetShapes(shapes, tris.ToArray());
                EditorUtility.SetDirty(source);
                count++;
            }
            Debug.Log($"[Horror Game] Baked brushstroke shapes for {count} sources.");

            static BrushStrokeSource Owner(Transform t)
            {
                for (; t != null; t = t.parent)
                {
                    var source = t.GetComponent<BrushStrokeSource>();
                    if (source != null)
                        return source;
                }
                return null;
            }

            // Eyes, a mouth: too small for ordinary strokes.
            static bool IsDetail(MeshFilter filter)
            {
                var size = Vector3.Scale(filter.sharedMesh.bounds.size, filter.transform.lossyScale);
                return Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) < 0.12f;
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
