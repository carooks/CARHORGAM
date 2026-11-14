using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.Rendering;
using HorrorGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates a walkable slice of the old match factory interior as its own scene
    /// (Scenes/MatchFactory_Interior.unity), built only from primitives. Run
    /// Horror Game > Build Match Factory Interior after changing anything here.
    ///
    /// Layout (metres, +X east, +Z north, origin = south-west corner of the machine hall):
    ///   Loading yard     X -3..62,  Z -30..0   exterior, fenced and fogged; player starts here
    ///   Receiving room   X 0..14,   Z -12..0   half-open roll-up door, main breaker, pallets
    ///   Machine hall     X 0..60,   Z 0..20    7 m eaves, timber trusses, two rows of match
    ///                                          machines, line shafts and belts, a collapsed bay
    ///   Dip room         X 0..16,   Z 20..32   burnt out: charred walls, dip tanks, drums
    ///   Stairwell        X 10..16,  Z 32..38   stairs up end in rubble; chained door
    ///   Foreman's office X 52..60,  Z 12..20   up a stair on a 3.5 m platform, desk lamp on
    ///
    /// The hall lamps start OFF. The main breaker inside the roll-up door turns them on.
    /// </summary>
    public static class FactoryInteriorBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/MatchFactory_Interior.unity";
        const string PostFxPath = ProjectPaths.Settings + "/PostFX_Factory.asset";
        const string PrismMeshPath = ProjectPaths.Meshes + "/UnitPrism.asset";
        const string LitterMeshPath = ProjectPaths.Meshes + "/MatchLitter.asset";

        const float HallLength = 60f;
        const float HallWidth = 20f;
        const float EaveHeight = 7f;
        const float RidgeHeight = 11f;
        const float WallThickness = 0.5f;
        const float BayWidth = 6f;
        const float PlatformHeight = 3.5f;
        const float LowRoomHeight = 5f;
        const int CollapsedBay = 6; // bay X 36..42, south roof slope missing

        static readonly Color FogColor = new Color(0.02f, 0.022f, 0.028f);
        static readonly Color LampColor = new Color(1f, 0.82f, 0.58f);
        static readonly Vector3 PlayerSpawn = new Vector3(7f, 0.1f, -22f);

        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        static GrayboxKit kit;
        static System.Random rng;
        static readonly List<Object> BreakerLights = new List<Object>();
        static readonly List<Object> BreakerFixtures = new List<Object>();

        // Factory-only materials (created once under Assets/_Project/Materials, never overwritten).
        static Material brick, brickDark, timber, roofBoards, soot, iron, enamel, bulb, bulbDead, matchWood,
            paper, dipLiquid, debris, plank, pilotRed, rubber;

        [MenuItem("Horror Game/Build Match Factory Interior", priority = 20)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (System.IO.File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rebuild match factory interior?",
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
            CreateMaterials();
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GrayboxKit.Group("MatchFactory", null).transform;

            BuildYard(GrayboxKit.Group("Yard", root).transform);
            BuildReceiving(GrayboxKit.Group("Receiving", root).transform);
            BuildHallShell(GrayboxKit.Group("Hall_Structure", root).transform);
            BuildRoof(GrayboxKit.Group("Hall_Roof", root).transform);
            BuildMachines(GrayboxKit.Group("Hall_Machines", root).transform);
            BuildLineShafts(GrayboxKit.Group("Hall_LineShafts", root).transform);
            BuildHallProps(GrayboxKit.Group("Hall_Props", root).transform);
            BuildCollapse(GrayboxKit.Group("Hall_Collapse", root).transform);
            BuildHallLamps(GrayboxKit.Group("Hall_Lamps", root).transform);
            BuildOffice(GrayboxKit.Group("ForemansOffice", root).transform);
            BuildDipRoom(GrayboxKit.Group("DipRoom", root).transform);
            BuildStairwell(GrayboxKit.Group("Stairwell", root).transform);
            BuildMatchLitter(root);
            BuildMainBreaker(GrayboxKit.Group("MainBreaker", root).transform);
            BuildWaypoints();

            ConfigureAtmosphere();
            CreateMoonlight();
            CreatePostProcessing();
            CreateAmbience();
            SpawnPlayer(playerPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Built " + ScenePath + ". Press Play, walk in under the roll-up door and find the red breaker light.");
        }

        // ------------------------------------------------------------------ materials

        static void CreateMaterials()
        {
            brick = Mat("M_Factory_Brick", new Color(0.36f, 0.16f, 0.11f), 0.08f);
            brickDark = Mat("M_Factory_BrickDark", new Color(0.24f, 0.11f, 0.08f), 0.08f);
            timber = Mat("M_Factory_Timber", new Color(0.3f, 0.22f, 0.14f), 0.1f);
            roofBoards = Mat("M_Factory_RoofBoards", new Color(0.2f, 0.16f, 0.12f), 0.05f);
            soot = Mat("M_Factory_Soot", new Color(0.05f, 0.045f, 0.04f), 0.02f);
            iron = Mat("M_Factory_Iron", new Color(0.14f, 0.15f, 0.15f), 0.35f, 0.6f);
            enamel = Mat("M_Factory_Enamel", new Color(0.3f, 0.36f, 0.3f), 0.5f, 0.1f);
            bulb = Mat("M_Factory_Bulb", new Color(1f, 0.9f, 0.7f), 0.5f, 0f, new Color(1f, 0.78f, 0.5f) * 3f);
            bulbDead = Mat("M_Factory_BulbDead", new Color(0.25f, 0.24f, 0.22f), 0.6f);
            matchWood = Mat("M_Factory_MatchWood", new Color(0.75f, 0.63f, 0.42f), 0.05f);
            paper = Mat("M_Factory_Paper", new Color(0.62f, 0.58f, 0.5f), 0.05f);
            dipLiquid = Mat("M_Factory_DipLiquid", new Color(0.12f, 0.03f, 0.03f), 0.92f);
            debris = Mat("M_Factory_Debris", new Color(0.24f, 0.21f, 0.19f), 0.05f);
            plank = Mat("M_Factory_Plank", new Color(0.34f, 0.26f, 0.17f), 0.05f);
            pilotRed = Mat("M_Factory_PilotRed", new Color(0.8f, 0.05f, 0.05f), 0.5f, 0f, new Color(1f, 0.05f, 0.03f) * 4f);
            rubber = Mat("M_Factory_Rubber", new Color(0.08f, 0.07f, 0.06f), 0.2f);
        }

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            string path = $"{ProjectPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------ primitive helpers

        static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = true, Quaternion? rotation = null)
        {
            return kit.Box(name, parent, center, size, material, collider, rotation);
        }

        /// <summary>Box stretched between two points (a beam, belt, cord or rail).</summary>
        static GameObject Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, float height,
            Material material, bool collider = false)
        {
            Vector3 d = b - a;
            Vector3 up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return kit.Box(name, parent, (a + b) * 0.5f, new Vector3(width, height, d.magnitude), material, collider,
                Quaternion.LookRotation(d, up));
        }

        /// <summary>Cylinder of a given diameter and length lying along <paramref name="axis"/>.</summary>
        static GameObject Cyl(string name, Transform parent, Vector3 center, float diameter, float length,
            Material material, Vector3 axis, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.FromToRotation(Vector3.up, axis.normalized));
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return go;
        }

        static GameObject Blocker(string name, Transform parent, Vector3 center, Vector3 size, Quaternion? rotation = null)
        {
            var go = GrayboxKit.InvisibleWall(name, parent, center, size);
            if (rotation.HasValue)
                go.transform.rotation = rotation.Value;
            return go;
        }

        static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        struct Opening
        {
            public float Center, Width, Bottom, Top;
            public bool Boarded, Blocked;

            /// <summary>A walk-through gap from the floor.</summary>
            public static Opening Door(float center, float width, float top) =>
                new Opening { Center = center, Width = width, Bottom = 0f, Top = top };

            /// <summary>A window: the player can't climb through, and it may be boarded up.</summary>
            public static Opening Window(float center, float width, float bottom, float top, bool boarded = false) =>
                new Opening { Center = center, Width = width, Bottom = bottom, Top = top, Boarded = boarded, Blocked = true };
        }

        /// <summary>
        /// A straight brick wall from <paramref name="a"/> to <paramref name="b"/> (floor level, on the
        /// wall's centre line) with holes cut for doors and windows. Opening centres are measured in
        /// metres from <paramref name="a"/>. Windows get a stepped brick arch, like the real factory.
        /// </summary>
        static void Wall(string name, Transform parent, Vector3 a, Vector3 b, float height, Material material,
            params Opening[] openings)
        {
            Vector3 dir = (b - a).normalized;
            // Extend by half the thickness at both ends so corners close up.
            a -= dir * (WallThickness * 0.5f);
            float length = Vector3.Distance(a, b) + WallThickness * 0.5f;
            const float shift = WallThickness * 0.5f;
            var rotation = Quaternion.LookRotation(dir, Vector3.up);
            var group = GrayboxKit.Group(name, parent).transform;

            void Piece(string n, float from, float to, float bottom, float top)
            {
                if (to - from < 0.01f || top - bottom < 0.01f)
                    return;
                Vector3 c = a + dir * ((from + to) * 0.5f) + Vector3.up * ((bottom + top) * 0.5f);
                Box(n, group, c, new Vector3(WallThickness, top - bottom, to - from), material, true, rotation);
            }

            var sorted = new List<Opening>(openings);
            sorted.Sort((x, y) => x.Center.CompareTo(y.Center));
            float cursor = 0f;
            int i = 0;
            foreach (var o in sorted)
            {
                float start = o.Center + shift - o.Width * 0.5f;
                float end = o.Center + shift + o.Width * 0.5f;
                Piece($"Pier_{i}", cursor, start, 0f, height);
                Piece($"Sill_{i}", start, end, 0f, o.Bottom);
                Piece($"Header_{i}", start, end, o.Top, height);
                cursor = end;

                if (o.Blocked)
                {
                    Vector3 mid = a + dir * (o.Center + shift) + Vector3.up * ((o.Bottom + o.Top) * 0.5f);
                    Blocker($"WindowBlock_{i}", group, mid, Vector3.one, rotation).GetComponent<BoxCollider>().size =
                        new Vector3(WallThickness, o.Top - o.Bottom, o.Width);
                    // Stepped arch: two brick corners at the top of the opening.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 corner = a + dir * (o.Center + shift + side * (o.Width * 0.5f - 0.2f)) +
                                         Vector3.up * (o.Top - 0.2f);
                        Box($"Arch_{i}", group, corner, new Vector3(WallThickness * 0.98f, 0.4f, 0.4f), material, false, rotation);
                    }
                    Box($"SillStone_{i}", group, a + dir * (o.Center + shift) + Vector3.up * (o.Bottom - 0.04f),
                        new Vector3(WallThickness + 0.12f, 0.08f, o.Width + 0.2f), kit.Concrete, false, rotation);
                }
                if (o.Boarded)
                {
                    int planks = rng.Next(2, 4);
                    for (int p = 0; p < planks; p++)
                    {
                        float y = Mathf.Lerp(o.Bottom + 0.6f, o.Top - 0.6f, (p + 0.5f) / planks);
                        Vector3 c = a + dir * (o.Center + shift) + Vector3.up * y;
                        var tilt = rotation * Quaternion.Euler(Rand(-25f, 25f), 0f, 0f);
                        Box($"Board_{i}_{p}", group, c, new Vector3(0.05f, 0.25f, o.Width + 0.3f), plank, false, tilt);
                    }
                }
                i++;
            }
            Piece($"Pier_{i}", cursor, length, 0f, height);
        }

        /// <summary>Simple railing: posts, top and mid rail, plus an invisible full-height collider.</summary>
        static void Railing(string name, Transform parent, Vector3 a, Vector3 b, Material material)
        {
            var group = GrayboxKit.Group(name, parent).transform;
            Beam("TopRail", group, a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, 0.06f, 0.06f, material);
            Beam("MidRail", group, a + Vector3.up * 0.55f, b + Vector3.up * 0.55f, 0.04f, 0.04f, material);
            int posts = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.5f));
            for (int i = 0; i <= posts; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                Beam("Post", group, p, p + Vector3.up * 1.08f, 0.06f, 0.06f, material);
            }
            var block = Beam("Collider", group, a + Vector3.up * 0.6f, b + Vector3.up * 0.6f, 0.1f, 1.2f, material, true);
            Object.DestroyImmediate(block.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(block.GetComponent<MeshFilter>());
        }

        /// <summary>Visible steps plus one smooth invisible ramp collider, so the CharacterController glides up.</summary>
        static void Stairs(string name, Transform parent, Vector3 bottom, Vector3 runDirection, float run, float rise,
            float width, Material material)
        {
            var group = GrayboxKit.Group(name, parent).transform;
            int steps = Mathf.RoundToInt(rise / 0.25f);
            float stepRise = rise / steps, stepRun = run / steps;
            var rotation = Quaternion.LookRotation(runDirection, Vector3.up);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * stepRise;
                Vector3 c = bottom + runDirection * ((i + 0.5f) * stepRun) + Vector3.up * (h * 0.5f);
                Box($"Step_{i}", group, c, new Vector3(width, h, stepRun), material, false, rotation);
            }
            Vector3 top = bottom + runDirection * run + Vector3.up * rise;
            var ramp = Beam("RampCollider", group, bottom, top, width, 0.05f, material, true);
            Object.DestroyImmediate(ramp.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(ramp.GetComponent<MeshFilter>());
        }

        /// <summary>Triangular prism mesh (base 1 wide at y=0, apex at y=1, 1 deep), for gable ends.</summary>
        static Mesh PrismMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PrismMeshPath);
            if (mesh != null)
                return mesh;
            AssetFolders.Ensure(ProjectPaths.Meshes);
            var l = new Vector3(-0.5f, 0f, 0f);
            var r = new Vector3(0.5f, 0f, 0f);
            var t = new Vector3(0f, 1f, 0f);
            var f = new Vector3(0f, 0f, -0.5f);
            var k = new Vector3(0f, 0f, 0.5f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int s = vertices.Count;
                vertices.Add(p0); vertices.Add(p1); vertices.Add(p2);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
            }
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) { Tri(p0, p1, p2); Tri(p0, p2, p3); }
            Tri(l + f, t + f, r + f);                 // front
            Tri(r + k, t + k, l + k);                 // back
            Quad(l + k, t + k, t + f, l + f);         // left slope
            Quad(r + f, t + f, t + k, r + k);         // right slope
            Quad(l + f, r + f, r + k, l + k);         // bottom
            mesh = new Mesh { name = "UnitPrism" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, PrismMeshPath);
            return mesh;
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material, Vector3 position,
            Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return go;
        }

        // ------------------------------------------------------------------ yard (exterior)

        static void BuildYard(Transform parent)
        {
            Box("Ground", parent, new Vector3(30f, -0.07f, 5f), new Vector3(110f, 0.1f, 90f), kit.Ground);
            Box("Apron", parent, new Vector3(30f, -0.015f, -9f), new Vector3(66f, 0.02f, 18f), kit.Asphalt);

            // Fence around the yard; fog hides what is beyond it.
            var fence = GrayboxKit.Group("Fence", parent).transform;
            Vector3[] corners =
            {
                new Vector3(-0.3f, 0f, -0.3f), new Vector3(-3f, 0f, -0.3f), new Vector3(-3f, 0f, -30f),
                new Vector3(62f, 0f, -30f), new Vector3(62f, 0f, -0.3f), new Vector3(60.3f, 0f, -0.3f),
            };
            for (int s = 0; s < corners.Length - 1; s++)
            {
                Vector3 a = corners[s], b = corners[s + 1];
                int posts = Mathf.CeilToInt(Vector3.Distance(a, b) / 3f);
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                    float lean = i % 7 == 3 ? 12f : 0f;
                    Box("Post", fence, p + Vector3.up * 1.1f, new Vector3(0.08f, 2.2f, 0.08f), kit.Metal, false,
                        Quaternion.Euler(lean, 0f, lean * 0.5f));
                }
                Beam("Rail", fence, a + Vector3.up * 2.1f, b + Vector3.up * 2.1f, 0.05f, 0.05f, kit.Metal);
                Beam("Mesh", fence, a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, 0.01f, 2.0f, iron).GetComponent<Renderer>()
                    .shadowCastingMode = ShadowCastingMode.Off;
                Blocker("FenceBlock", fence, (a + b) * 0.5f + Vector3.up * 2f, Vector3.one,
                    Quaternion.LookRotation(b - a)).GetComponent<BoxCollider>().size = new Vector3(0.3f, 4f, Vector3.Distance(a, b));
            }

            // Box trailer left at the hall wall.
            var trailer = GrayboxKit.Group("Trailer", parent).transform;
            Box("Body", trailer, new Vector3(28f, 2.6f, -4.2f), new Vector3(12f, 2.8f, 2.6f), kit.VanPaint);
            Box("Chassis", trailer, new Vector3(28f, 1.0f, -4.2f), new Vector3(11.5f, 0.3f, 2.2f), iron, false);
            foreach (float x in new[] { 31.5f, 32.8f })
            foreach (float z in new[] { -5.3f, -3.1f })
                Cyl("Wheel", trailer, new Vector3(x, 0.5f, z), 1f, 0.35f, rubber, Vector3.forward);
            Box("LandingLeg", trailer, new Vector3(23.5f, 0.6f, -4.2f), new Vector3(0.2f, 1.2f, 1.8f), iron);

            // Dumpster and drums near the door.
            Box("Dumpster", parent, new Vector3(-0.8f, 0.75f, -17f), new Vector3(1.8f, 1.5f, 3.2f), kit.Hazard, true,
                Quaternion.Euler(0f, 8f, 0f));
            for (int i = 0; i < 3; i++)
                Cyl("Drum", parent, new Vector3(12.5f + i * 0.7f, 0.45f, -13.2f + (i % 2) * 0.4f), 0.6f, 0.9f, kit.Rust, Vector3.up, true);

            // Sodium lamp over the roll-up door: always on, so the way in is visible from the start.
            Box("DoorLampArm", parent, new Vector3(7f, 5.6f, -12.55f), new Vector3(0.1f, 0.1f, 0.6f), kit.Metal, false);
            Box("DoorLampHead", parent, new Vector3(7f, 5.5f, -12.85f), new Vector3(0.5f, 0.15f, 0.35f), kit.LampSodium, false);
            var lamp = GrayboxKit.SpotLight("DoorLamp", parent, new Vector3(7f, 5.4f, -12.9f), Quaternion.Euler(70f, 180f, 0f),
                new Color(1f, 0.62f, 0.3f), 9f, 14f, 95f);
            lamp.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------ receiving room

        static void BuildReceiving(Transform parent)
        {
            Box("Floor", parent, new Vector3(7f, -0.05f, -6f), new Vector3(14.5f, 0.1f, 12.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3(7f, LowRoomHeight + 0.1f, -6f), new Vector3(14.6f, 0.2f, 12.6f), roofBoards);

            Wall("Wall_West", parent, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f), LowRoomHeight, brick);
            Wall("Wall_South", parent, new Vector3(0f, 0f, -12f), new Vector3(14f, 0f, -12f), LowRoomHeight, brick,
                Opening.Door(7f, 4.6f, 4.6f));
            Wall("Wall_East", parent, new Vector3(14f, 0f, -12f), new Vector3(14f, 0f, 0f), LowRoomHeight, brick,
                Opening.Window(6f, 2.2f, 1.6f, 4.2f, boarded: true));

            // Roll-up door stuck 2.2 m open: duck-free for the player, but it reads as forced.
            var door = GrayboxKit.Group("RollUpDoor", parent).transform;
            Box("Slab", door, new Vector3(7f, 3.4f, -12f), new Vector3(4.6f, 2.4f, 0.12f), kit.Metal);
            for (int i = 0; i < 6; i++)
                Box("Rib", door, new Vector3(7f, 2.35f + i * 0.4f, -12.07f), new Vector3(4.6f, 0.05f, 0.03f), iron, false);
            Box("BottomBar", door, new Vector3(7f, 2.22f, -12f), new Vector3(4.6f, 0.06f, 0.16f), kit.Hazard, false);
            foreach (float x in new[] { 4.6f, 9.4f })
                Box("Guide", door, new Vector3(x, 2.3f, -11.8f), new Vector3(0.12f, 4.6f, 0.12f), iron, false);

            // Pallets of boxed matches along the west wall.
            for (int p = 0; p < 4; p++)
            {
                var pallet = GrayboxKit.Group($"Pallet_{p}", parent).transform;
                Vector3 c = new Vector3(1.4f, 0f, -10f + p * 2.4f);
                Box("Pallet", pallet, c + Vector3.up * 0.08f, new Vector3(1.2f, 0.15f, 1.0f), plank);
                int layers = rng.Next(1, 5);
                for (int y = 0; y < layers; y++)
                for (int x = 0; x < 2; x++)
                for (int z = 0; z < 2; z++)
                {
                    if (y == layers - 1 && rng.NextDouble() < 0.3)
                        continue;
                    Box("Case", pallet, c + new Vector3(-0.28f + x * 0.56f, 0.33f + y * 0.36f, -0.23f + z * 0.46f),
                        new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, false, Quaternion.Euler(0f, Rand(-4f, 4f), 0f));
                }
                Blocker("PalletBlock", pallet, c + Vector3.up * (0.2f + layers * 0.18f), new Vector3(1.2f, 0.4f + layers * 0.36f, 1.0f));
            }

            // A toppled stack spilling cases across the floor.
            for (int i = 0; i < 5; i++)
                Box("SpilledCase", parent, new Vector3(3.2f + Rand(0f, 1.6f), 0.17f, -2.6f + Rand(-0.8f, 0.8f)),
                    new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, true, Quaternion.Euler(0f, Rand(0f, 90f), 0f));

            // Shipping desk and a hand truck.
            Box("Desk", parent, new Vector3(12.6f, 0.45f, -4f), new Vector3(0.9f, 0.9f, 2.2f), plank);
            Box("Scale", parent, new Vector3(12.6f, 0.95f, -3.4f), new Vector3(0.5f, 0.12f, 0.5f), kit.Metal, false);
            Box("Ledger", parent, new Vector3(12.5f, 0.92f, -4.5f), new Vector3(0.35f, 0.04f, 0.5f), paper, false,
                Quaternion.Euler(0f, 12f, 0f));
            HandTruck(parent, new Vector3(9.5f, 0f, -7.5f), 30f);

            Box("Crate", parent, new Vector3(11.8f, 0.6f, -9.8f), new Vector3(1.2f, 1.2f, 1.2f), kit.Crate, true, Quaternion.Euler(0f, 10f, 0f));
            Box("Crate", parent, new Vector3(12.0f, 1.6f, -9.7f), new Vector3(0.8f, 0.8f, 0.8f), kit.Crate, true, Quaternion.Euler(0f, 25f, 0f));

            // Two ceiling lamps on the main breaker.
            HangingLamp(parent, new Vector3(5f, 0f, -6f), LowRoomHeight, LampState.Working, "Receiving_A");
            HangingLamp(parent, new Vector3(10f, 0f, -3f), LowRoomHeight, LampState.Flickering, "Receiving_B");
        }

        static void HandTruck(Transform parent, Vector3 p, float yaw)
        {
            var truck = GrayboxKit.Group("HandTruck", parent).transform;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Bed", truck, p + rot * new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.08f, 0.7f), kit.Metal, true, rot);
            Box("Handle", truck, p + rot * new Vector3(-0.5f, 0.75f, 0f), new Vector3(0.05f, 0.8f, 0.6f), kit.Metal, false, rot);
            foreach (float x in new[] { -0.35f, 0.35f })
            foreach (float z in new[] { -0.3f, 0.3f })
                Cyl("Wheel", truck, p + rot * new Vector3(x, 0.15f, z), 0.3f, 0.08f, rubber, rot * Vector3.forward);
            Box("Case", truck, p + rot * new Vector3(0.1f, 0.56f, 0f), new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, false, rot);
        }

        // ------------------------------------------------------------------ machine hall shell

        static void BuildHallShell(Transform parent)
        {
            Box("Floor", parent, new Vector3(HallLength * 0.5f, -0.05f, HallWidth * 0.5f),
                new Vector3(HallLength + 0.5f, 0.1f, HallWidth + 0.5f), kit.ConcreteFloor);

            // South wall: solid behind the receiving room (with the doorway), then a window per bay.
            var south = new List<Opening> { Opening.Door(10.5f, 3f, 3.6f) };
            for (int bay = 2; bay < 10; bay++)
                south.Add(Opening.Window(bay * BayWidth + 3f, 2.4f, 1.8f, 5.6f, boarded: rng.NextDouble() < 0.35));
            Wall("Wall_South", parent, new Vector3(0f, 0f, 0f), new Vector3(HallLength, 0f, 0f), EaveHeight, brick, south.ToArray());

            // North wall: the dip-room doorway, then windows.
            var north = new List<Opening> { Opening.Door(7f, 2.4f, 3.2f) };
            for (int bay = 3; bay < 10; bay++)
            {
                if (bay == 9)
                    continue; // the office sits against this bay
                north.Add(Opening.Window(bay * BayWidth + 3f, 2.4f, 1.8f, 5.6f, boarded: rng.NextDouble() < 0.35));
            }
            Wall("Wall_North", parent, new Vector3(0f, 0f, HallWidth), new Vector3(HallLength, 0f, HallWidth), EaveHeight, brick, north.ToArray());

            // West end: big double doors to the unbuilt part of the factory, chained shut.
            Wall("Wall_West", parent, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, HallWidth), EaveHeight, brick,
                Opening.Door(10f, 4f, 4.5f));
            var doors = GrayboxKit.Group("ChainedDoors", parent).transform;
            Box("LeafA", doors, new Vector3(0f, 2.25f, 9f), new Vector3(0.15f, 4.5f, 1.98f), plank);
            Box("LeafB", doors, new Vector3(0.03f, 2.25f, 11f), new Vector3(0.15f, 4.5f, 1.98f), plank, true,
                Quaternion.Euler(0f, -3f, 0f));
            Beam("Chain", doors, new Vector3(0.2f, 1.5f, 8.6f), new Vector3(0.2f, 1.1f, 11.4f), 0.05f, 0.05f, iron);
            Beam("Chain", doors, new Vector3(0.2f, 1.1f, 8.6f), new Vector3(0.2f, 1.5f, 11.4f), 0.05f, 0.05f, iron);
            Box("Padlock", doors, new Vector3(0.25f, 1.3f, 10f), new Vector3(0.08f, 0.14f, 0.1f), kit.Hazard, false);

            // East end: one tall window, and a high one lighting the office.
            Wall("Wall_East", parent, new Vector3(HallLength, 0f, 0f), new Vector3(HallLength, 0f, HallWidth), EaveHeight, brick,
                Opening.Window(5f, 2.4f, 1.8f, 5.6f), Opening.Window(16f, 2f, 4.7f, 6.3f));

            // Gable triangles above the eaves at both ends.
            var prism = PrismMesh();
            foreach (float x in new[] { 0f, HallLength })
                MeshObject("Gable", parent, prism, brick, new Vector3(x, EaveHeight, HallWidth * 0.5f),
                    Quaternion.Euler(0f, 90f, 0f), new Vector3(HallWidth + WallThickness, RidgeHeight - EaveHeight, WallThickness));

            // Steel columns either side of the centre aisle, one per truss.
            for (int bay = 1; bay < 10; bay++)
            {
                float x = bay * BayWidth;
                foreach (float z in new[] { 7.2f, 12.8f })
                {
                    Box($"Column_{bay}", parent, new Vector3(x, EaveHeight * 0.5f, z), new Vector3(0.3f, EaveHeight, 0.3f), kit.Metal);
                    Box($"ColumnBase_{bay}", parent, new Vector3(x, 0.1f, z), new Vector3(0.6f, 0.2f, 0.6f), kit.Concrete, false);
                    Box($"ColumnStripe_{bay}", parent, new Vector3(x, 0.55f, z), new Vector3(0.32f, 0.3f, 0.32f), kit.Hazard, false);
                }
            }

            // Painted aisle lines, worn.
            foreach (float z in new[] { 7.7f, 12.3f })
                for (float x = 2f; x < 58f; x += 4f)
                    if (rng.NextDouble() > 0.25)
                        Box("AisleLine", parent, new Vector3(x + 1.5f, 0.004f, z), new Vector3(Rand(2f, 3.2f), 0.01f, 0.12f), kit.Hazard, false);
        }

        // ------------------------------------------------------------------ roof and trusses

        static void BuildRoof(Transform parent)
        {
            float rise = RidgeHeight - EaveHeight;
            float halfSpan = HallWidth * 0.5f;
            float slopeLength = Mathf.Sqrt(halfSpan * halfSpan + rise * rise) + 0.6f;
            float angle = Mathf.Atan2(rise, halfSpan) * Mathf.Rad2Deg;

            for (int bay = 0; bay < 10; bay++)
            {
                float x = bay * BayWidth + BayWidth * 0.5f;
                for (int side = 0; side < 2; side++)
                {
                    if (side == 0 && bay == CollapsedBay)
                        continue;
                    var rot = Quaternion.Euler(side == 0 ? -angle : angle, 0f, 0f);
                    Vector3 c = new Vector3(x, EaveHeight + rise * 0.5f, side == 0 ? halfSpan * 0.5f : halfSpan * 1.5f);
                    c += rot * Vector3.up * 0.25f;
                    Box($"RoofPanel_{bay}_{side}", parent, c, new Vector3(BayWidth + 0.02f, 0.2f, slopeLength), roofBoards, false, rot);
                }
            }
            Box("RidgeCap", parent, new Vector3(HallLength * 0.5f, RidgeHeight + 0.3f, halfSpan), new Vector3(HallLength + 0.6f, 0.3f, 0.7f), roofBoards, false);

            // Hanging remains of the collapsed panel: two boards still pinned at the ridge.
            float cx = CollapsedBay * BayWidth;
            Beam("HangingBoard", parent, new Vector3(cx + 1.5f, RidgeHeight - 0.1f, halfSpan - 0.4f),
                new Vector3(cx + 1.2f, RidgeHeight - 3.6f, halfSpan - 1.6f), 0.5f, 0.06f, roofBoards);
            Beam("HangingBoard", parent, new Vector3(cx + 4.3f, RidgeHeight - 0.3f, halfSpan - 0.8f),
                new Vector3(cx + 4.8f, RidgeHeight - 2.6f, halfSpan - 3.2f), 0.4f, 0.06f, roofBoards);

            // Timber trusses at every column line.
            for (int bay = 1; bay < 10; bay++)
            {
                float x = bay * BayWidth;
                var t = GrayboxKit.Group($"Truss_{bay}", parent).transform;
                Vector3 eaveS = new Vector3(x, EaveHeight, 0f), eaveN = new Vector3(x, EaveHeight, HallWidth);
                Vector3 ridge = new Vector3(x, RidgeHeight, halfSpan), tieMid = new Vector3(x, EaveHeight, halfSpan);
                Vector3 quarterS = new Vector3(x, EaveHeight + rise * 0.5f, halfSpan * 0.5f);
                Vector3 quarterN = new Vector3(x, EaveHeight + rise * 0.5f, halfSpan * 1.5f);
                Beam("Tie", t, eaveS, eaveN, 0.25f, 0.3f, timber);
                Beam("ChordS", t, eaveS, ridge, 0.25f, 0.3f, timber);
                Beam("ChordN", t, eaveN, ridge, 0.25f, 0.3f, timber);
                Beam("KingPost", t, tieMid, ridge, 0.22f, 0.22f, timber);
                Beam("QueenS", t, new Vector3(x, EaveHeight, halfSpan * 0.5f), quarterS, 0.18f, 0.18f, timber);
                Beam("QueenN", t, new Vector3(x, EaveHeight, halfSpan * 1.5f), quarterN, 0.18f, 0.18f, timber);
                Beam("StrutS", t, tieMid, quarterS, 0.16f, 0.16f, timber);
                Beam("StrutN", t, tieMid, quarterN, 0.16f, 0.16f, timber);
            }

            // Purlins running the length of the hall.
            for (int i = 1; i < 4; i++)
            {
                float f = i / 4f;
                float y = EaveHeight + rise * f + 0.05f;
                Box("PurlinS", parent, new Vector3(HallLength * 0.5f, y, halfSpan * f), new Vector3(HallLength, 0.2f, 0.2f), timber, false);
                Box("PurlinN", parent, new Vector3(HallLength * 0.5f, y, HallWidth - halfSpan * f), new Vector3(HallLength, 0.2f, 0.2f), timber, false);
            }
        }

        // ------------------------------------------------------------------ match machines

        static void BuildMachines(Transform parent)
        {
            float[] starts = { 4f, 14f, 24f, 34f, 44f };
            for (int i = 0; i < starts.Length; i++)
            {
                float x = starts[i] + 4f;
                if (starts[i] != 34f) // row A bay under the collapse is wrecked instead
                    MatchMachine(parent, new Vector3(x, 0f, 5f), +1, $"A{i}", hum: false);
                MatchMachine(parent, new Vector3(x, 0f, 15f), -1, $"B{i}", hum: i == 2);
            }
        }

        /// <summary>
        /// One continuous match machine, 8 m long, after the reference photos: an iron base, tall end
        /// frames, the chain runs, a forest of pipe uprights, a drum, gear wheels and a hopper.
        /// <paramref name="aisleSide"/> is +1 if the centre aisle is north of the machine.
        /// </summary>
        static void MatchMachine(Transform parent, Vector3 c, int aisleSide, string id, bool hum)
        {
            const float L = 8f, D = 2.8f, H = 3.4f;
            var m = GrayboxKit.Group($"MatchMachine_{id}", parent).transform;
            float front = aisleSide * (D * 0.5f);

            Box("Base", m, c + Vector3.up * 0.25f, new Vector3(L, 0.5f, D), iron, false);
            foreach (float s in new[] { -1f, 1f })
                Box("EndFrame", m, c + new Vector3(s * (L * 0.5f - 0.2f), H * 0.5f, 0f), new Vector3(0.35f, H, D), iron, false);
            Box("ChainUpper", m, c + Vector3.up * 3.1f, new Vector3(L - 0.4f, 0.35f, D - 0.4f), rubber, false);
            Box("ChainLower", m, c + Vector3.up * 1.9f, new Vector3(L - 0.4f, 0.25f, D - 0.6f), rubber, false);

            for (int k = 0; k < 8; k++)
            {
                float x = -L * 0.5f + 0.6f + k * 1.0f;
                foreach (float z in new[] { -(D * 0.5f - 0.15f), D * 0.5f - 0.15f })
                    Cyl("Upright", m, c + new Vector3(x, 1.75f, z), 0.08f, 3.3f, kit.Metal, Vector3.up);
            }
            foreach (float z in new[] { -(D * 0.5f - 0.15f), D * 0.5f - 0.15f })
                Cyl("TopPipe", m, c + new Vector3(0f, 3.45f, z), 0.1f, L - 0.4f, kit.Metal, Vector3.right);

            Cyl("Drum", m, c + new Vector3(-L * 0.5f + 1.3f, 1.35f, front), 1.8f, 0.8f, iron, Vector3.forward);
            for (int g = 0; g < 3; g++)
                Cyl("Gear", m, c + new Vector3(-0.5f + g * 1.4f, 0.95f, front + aisleSide * 0.08f), Rand(0.45f, 0.75f), 0.1f, iron, Vector3.forward);
            Box("Hopper", m, c + new Vector3(L * 0.5f - 1.1f, 3.75f, 0f), new Vector3(1.2f, 0.8f, 1.6f), kit.Metal, false,
                Quaternion.Euler(0f, 0f, 25f));

            // Operator's shelf and stool on the aisle side.
            Box("Shelf", m, c + new Vector3(1.5f, 1.0f, front + aisleSide * 0.35f), new Vector3(2f, 0.06f, 0.6f), plank, false);
            Vector3 stool = c + new Vector3(Rand(-2f, 2.5f), 0f, front + aisleSide * 0.9f);
            bool knocked = rng.NextDouble() < 0.4;
            if (knocked)
            {
                Cyl("StoolSeat", m, stool + new Vector3(0.3f, 0.18f, 0f), 0.36f, 0.05f, plank, Vector3.right);
                Cyl("StoolLeg", m, stool + new Vector3(0f, 0.18f, 0f), 0.06f, 0.6f, kit.Metal, Vector3.right);
            }
            else
            {
                Cyl("StoolSeat", m, stool + Vector3.up * 0.62f, 0.36f, 0.05f, plank, Vector3.up);
                Cyl("StoolLeg", m, stool + Vector3.up * 0.3f, 0.06f, 0.6f, kit.Metal, Vector3.up);
            }

            Blocker("Collider", m, c + Vector3.up * (H * 0.5f), new Vector3(L, H, D));

            if (hum)
            {
                // One machine still hums with no power to the building. Nobody can say why.
                var source = GrayboxKit.Group("Hum", m);
                source.transform.position = c + Vector3.up * 1.5f;
                var sound = source.AddComponent<AmbientSound>();
                SerializedFields.SetEnum(sound, "sound", (int)ProceduralSound.MachineHum);
                SerializedFields.SetFloat(sound, "volume", 0.3f);
                SerializedFields.SetBool(sound, "spatial", true);
                SerializedFields.SetFloat(sound, "maxDistance", 16f);
            }
        }

        // ------------------------------------------------------------------ line shafts and belts

        static void BuildLineShafts(Transform parent)
        {
            const float shaftY = 5.6f;
            foreach (float z in new[] { 5f, 15f })
            {
                int aisleSide = z < HallWidth * 0.5f ? 1 : -1;
                Cyl("Shaft", parent, new Vector3(HallLength * 0.5f, shaftY, z), 0.14f, HallLength - 4f, kit.Metal, Vector3.right);
                for (int bay = 1; bay < 10; bay++)
                    Beam("Hanger", parent, new Vector3(bay * BayWidth, shaftY, z), new Vector3(bay * BayWidth, EaveHeight, z), 0.08f, 0.08f, kit.Metal);

                for (float x = 5f; x < 52f; x += 2.5f)
                {
                    Cyl("Pulley", parent, new Vector3(x, shaftY, z), Rand(0.7f, 1.0f), 0.16f, iron, Vector3.right);
                    bool wreckedBay = aisleSide > 0 && x > 34f && x < 42f;
                    double roll = rng.NextDouble();
                    if (wreckedBay || roll < 0.2)
                    {
                        // Snapped belt hanging straight down.
                        Beam("BeltSnapped", parent, new Vector3(x, shaftY - 0.4f, z), new Vector3(x + Rand(-0.2f, 0.2f), shaftY - Rand(1.2f, 2.4f), z + 0.1f),
                            0.12f, 0.02f, rubber);
                        continue;
                    }
                    Vector3 top = new Vector3(x, shaftY + 0.35f, z);
                    Vector3 bottom = new Vector3(x, 3.6f, z + aisleSide * 0.6f);
                    Beam("Belt", parent, top + Vector3.right * 0.06f, bottom + Vector3.right * 0.06f, 0.12f, 0.02f, rubber);
                    Beam("Belt", parent, top - Vector3.right * 0.06f + Vector3.down * 0.7f, bottom - Vector3.right * 0.06f, 0.12f, 0.02f, rubber);
                }
            }
        }

        // ------------------------------------------------------------------ hall props

        static void BuildHallProps(Transform parent)
        {
            // Stacks of splint/paper rolls in the side aisles, as in the old photo. One stack has fallen.
            Vector3[] stacks =
            {
                new Vector3(6f, 0f, 18.6f), new Vector3(7.3f, 0f, 18.7f), new Vector3(20f, 0f, 18.6f),
                new Vector3(33f, 0f, 18.6f), new Vector3(26f, 0f, 1.5f), new Vector3(27.3f, 0f, 1.6f), new Vector3(48f, 0f, 1.5f),
            };
            for (int s = 0; s < stacks.Length; s++)
            {
                var stack = GrayboxKit.Group($"RollStack_{s}", parent).transform;
                int count = rng.Next(5, 11);
                for (int i = 0; i < count; i++)
                    Cyl("Roll", stack, stacks[s] + new Vector3(Rand(-0.05f, 0.05f), 0.18f + i * 0.36f, Rand(-0.05f, 0.05f)), 1.1f, 0.35f, paper, Vector3.up);
                Blocker("Collider", stack, stacks[s] + Vector3.up * (count * 0.18f), new Vector3(1.1f, count * 0.36f, 1.1f));
            }
            for (int i = 0; i < 6; i++)
            {
                var axis = Quaternion.Euler(0f, Rand(0f, 180f), 0f) * Vector3.right;
                Cyl("FallenRoll", parent, new Vector3(Rand(14f, 18f), 0.55f, Rand(17.2f, 19.2f)), 1.1f, 0.35f, paper, axis, true);
            }

            // Carts of boxed matches like the shipping carts in the photo.
            Vector3[] carts = { new Vector3(16f, 0f, 9f), new Vector3(43f, 0f, 10.6f), new Vector3(55f, 0f, 3f) };
            foreach (var c in carts)
            {
                var cart = GrayboxKit.Group("BoxCart", parent).transform;
                var rot = Quaternion.Euler(0f, Rand(-20f, 20f), 0f);
                Box("Bed", cart, c + Vector3.up * 0.35f, new Vector3(1.6f, 0.06f, 0.9f), kit.Metal, false, rot);
                Box("Handle", cart, c + rot * new Vector3(-0.8f, 0.8f, 0f), new Vector3(0.04f, 0.9f, 0.8f), kit.Metal, false, rot);
                foreach (float x in new[] { -0.7f, 0.7f })
                foreach (float z in new[] { -0.4f, 0.4f })
                    Cyl("Wheel", cart, c + rot * new Vector3(x, 0.12f, z), 0.24f, 0.06f, rubber, rot * Vector3.forward);
                int boxes = rng.Next(3, 9);
                for (int b = 0; b < boxes; b++)
                    Box("Carton", cart, c + rot * new Vector3(-0.5f + (b % 3) * 0.5f, 0.55f + (b / 3) * 0.34f, Rand(-0.2f, 0.2f)),
                        new Vector3(0.45f, 0.3f, 0.35f), kit.Cardboard, false, rot * Quaternion.Euler(0f, Rand(-8f, 8f), 0f));
                Blocker("Collider", cart, c + Vector3.up * 0.6f, new Vector3(1.6f, 1.2f, 0.9f), rot);
            }

            // Spilled cartons around the machines.
            for (int i = 0; i < 14; i++)
            {
                float x = Rand(4f, 52f);
                float z = rng.NextDouble() < 0.5 ? Rand(6.6f, 7.0f) : Rand(13.0f, 13.4f);
                Box("LooseCarton", parent, new Vector3(x, 0.15f, z), new Vector3(0.45f, 0.3f, 0.35f), kit.Cardboard, true,
                    Quaternion.Euler(0f, Rand(0f, 180f), rng.NextDouble() < 0.3 ? 90f : 0f));
            }
        }

        // ------------------------------------------------------------------ collapsed bay

        static void BuildCollapse(Transform parent)
        {
            float x0 = CollapsedBay * BayWidth;
            // Fallen roof boards and timber.
            for (int i = 0; i < 16; i++)
            {
                var rot = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 180f), Rand(-20f, 20f));
                Box("FallenBoard", parent, new Vector3(x0 + Rand(0.5f, 5.5f), Rand(0.1f, 0.8f), Rand(1f, 7f)),
                    new Vector3(Rand(0.4f, 0.8f), 0.08f, Rand(2f, 4.5f)), roofBoards, false, rot);
            }
            for (int i = 0; i < 12; i++)
                Box("Rubble", parent, new Vector3(x0 + Rand(0.8f, 5.2f), Rand(0.1f, 0.5f), Rand(1.5f, 6.5f)),
                    Vector3.one * Rand(0.3f, 0.8f), debris, false, Quaternion.Euler(Rand(0f, 90f), Rand(0f, 90f), Rand(0f, 90f)));
            Blocker("RubbleMound", parent, new Vector3(x0 + 3f, 0.4f, 4f), new Vector3(5f, 0.8f, 5f));

            // A fallen truss chord leaning on the wrecked machine.
            Beam("FallenChord", parent, new Vector3(x0 + 0.6f, 0f, 0.8f), new Vector3(x0 + 4.5f, 4.2f, 7.2f), 0.25f, 0.3f, timber, true);

            // The wrecked machine: base and one end frame, tipped.
            Box("WreckBase", parent, new Vector3(x0 + 2f, 0.25f, 5.2f), new Vector3(7f, 0.5f, 2.6f), iron, true, Quaternion.Euler(0f, 4f, 3f));
            Box("WreckFrame", parent, new Vector3(x0 + 5.2f, 1.1f, 5.6f), new Vector3(0.35f, 2.8f, 2.6f), iron, true, Quaternion.Euler(0f, 10f, 38f));
            Cyl("WreckDrum", parent, new Vector3(x0 - 0.6f, 0.9f, 4.2f), 1.8f, 0.8f, iron, new Vector3(0.3f, 0f, 1f), true);

            // Moonlight pouring through the hole.
            var shaft = GrayboxKit.SpotLight("MoonShaft", parent, new Vector3(x0 + 3f, RidgeHeight + 2f, 3.5f),
                Quaternion.Euler(80f, 0f, 0f), new Color(0.6f, 0.7f, 1f), 7f, 18f, 55f);
            shaft.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------ lamps

        enum LampState { Working, Flickering, Dead }

        static void BuildHallLamps(Transform parent)
        {
            // Centre aisle, one per truss. W = works, F = flickers, D = dead.
            const string centre = "WDFWDDFDW";
            for (int i = 0; i < centre.Length; i++)
                HangingLamp(parent, new Vector3((i + 1) * BayWidth, 0f, HallWidth * 0.5f), EaveHeight, ToState(centre[i]), $"Aisle_{i}");
            // Side aisles: almost all dead.
            const string sides = "DDFDDDDD";
            for (int i = 0; i < sides.Length; i++)
            {
                float x = (i % 4 + 1) * 12f;
                float z = i < 4 ? 2f : 18f;
                HangingLamp(parent, new Vector3(x, 0f, z), EaveHeight, ToState(sides[i]), $"Side_{i}");
            }
        }

        static LampState ToState(char c) => c == 'W' ? LampState.Working : c == 'F' ? LampState.Flickering : LampState.Dead;

        /// <summary>Enamel-shade lamp on a cord. Working and flickering lamps are wired to the main breaker.</summary>
        static void HangingLamp(Transform parent, Vector3 floorPoint, float ceiling, LampState state, string id)
        {
            var g = GrayboxKit.Group($"Lamp_{id}", parent).transform;
            float shadeY = ceiling - 1.8f;
            Beam("Cord", g, floorPoint + Vector3.up * ceiling, floorPoint + Vector3.up * (shadeY + 0.3f), 0.03f, 0.03f, rubber);
            var shade = MeshObject("Shade", g, GrayboxKit.ConeMesh(), enamel, floorPoint + Vector3.up * shadeY,
                Quaternion.identity, new Vector3(0.75f, 0.32f, 0.75f));
            shade.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var bulbObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulbObject.name = "Bulb";
            bulbObject.transform.SetParent(g, false);
            bulbObject.transform.position = floorPoint + Vector3.up * (shadeY - 0.02f);
            bulbObject.transform.localScale = Vector3.one * 0.2f;
            Object.DestroyImmediate(bulbObject.GetComponent<Collider>());
            var bulbRenderer = bulbObject.GetComponent<MeshRenderer>();
            bulbRenderer.sharedMaterial = state == LampState.Dead ? bulbDead : bulb;
            bulbRenderer.shadowCastingMode = ShadowCastingMode.Off;
            if (state == LampState.Dead)
                return;

            var light = GrayboxKit.PointLight("Light", g, floorPoint + Vector3.up * (shadeY - 0.3f), LampColor,
                state == LampState.Flickering ? 5f : 6f, 12f);
            BreakerLights.Add(light.gameObject);
            BreakerFixtures.Add(bulbRenderer);
            if (state != LampState.Flickering)
                return;

            var flicker = light.gameObject.AddComponent<LightFlicker>();
            SerializedFields.Set(flicker, "emissiveRenderer", bulbRenderer);
            var buzz = light.gameObject.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(buzz, "sound", (int)ProceduralSound.ElectricalBuzz);
            SerializedFields.SetFloat(buzz, "volume", 0.25f);
            SerializedFields.SetBool(buzz, "spatial", true);
            SerializedFields.SetFloat(buzz, "maxDistance", 9f);
        }

        // ------------------------------------------------------------------ Foreman's office

        static void BuildOffice(Transform parent)
        {
            const float x0 = 52f, officeX = 53.5f, zSouth = 12.2f, inner = HallWidth - WallThickness * 0.5f;
            float y = PlatformHeight;

            // Platform and its posts.
            Box("Platform", parent, new Vector3((x0 + HallLength) * 0.5f, y - 0.15f, (zSouth + HallWidth) * 0.5f),
                new Vector3(HallLength - x0, 0.3f, HallWidth - zSouth), kit.Concrete);
            foreach (var p in new[] { new Vector3(x0 + 0.2f, 0f, zSouth + 0.2f), new Vector3(x0 + 0.2f, 0f, 17.4f), new Vector3(57f, 0f, zSouth + 0.2f) })
                Box("PlatformPost", parent, p + Vector3.up * (y * 0.5f - 0.15f), new Vector3(0.25f, y - 0.3f, 0.25f), kit.Metal);

            // Stair up along the north wall, rising east.
            Stairs("Stair", parent, new Vector3(x0 - 7f, 0f, 18.7f), Vector3.right, 7f, y, 1.8f, kit.Concrete);
            Railing("StairRail", parent, new Vector3(x0 - 7f, 0f, 17.75f), new Vector3(x0, y, 17.75f), kit.Hazard);
            Railing("LandingRailWest", parent, new Vector3(x0 + 0.05f, y, zSouth), new Vector3(x0 + 0.05f, y, 17.75f), kit.Hazard);
            Railing("LandingRailSouth", parent, new Vector3(x0, y, zSouth + 0.05f), new Vector3(officeX, y, zSouth + 0.05f), kit.Hazard);

            // Office walls (the hall's north and east walls close the other two sides).
            const float officeHeight = 2.8f;
            Wall("OfficeWall_West", parent, new Vector3(officeX, y, zSouth), new Vector3(officeX, y, inner), officeHeight, brickDark,
                Opening.Window(2.8f, 3.4f, 1.0f, 2.2f), Opening.Door(6.3f, 1.1f, 2.2f));
            Wall("OfficeWall_South", parent, new Vector3(officeX, y, zSouth), new Vector3(HallLength - WallThickness * 0.5f, y, zSouth),
                officeHeight, brickDark, Opening.Window(3f, 2.4f, 1.0f, 2.2f));
            Box("OfficeCeiling", parent, new Vector3((officeX + HallLength) * 0.5f, y + officeHeight + 0.1f, (zSouth + HallWidth) * 0.5f),
                new Vector3(HallLength - officeX + 0.3f, 0.2f, HallWidth - zSouth + 0.3f), roofBoards);
            // Window frames: a thin mullion in each office window.
            Box("Mullion", parent, new Vector3(officeX, y + 1.6f, 15f), new Vector3(0.08f, 1.2f, 0.06f), timber, false);
            Box("Mullion", parent, new Vector3(56.5f, y + 1.6f, zSouth), new Vector3(0.06f, 1.2f, 0.08f), timber, false);
            // The door, left ajar into the office.
            Box("Door", parent, new Vector3(officeX + 0.45f, y + 1.1f, 19.35f), new Vector3(0.06f, 2.2f, 1.0f), plank, true,
                Quaternion.Euler(0f, 65f, 0f));

            // Furniture.
            float fy = y;
            Box("Desk", parent, new Vector3(57.5f, fy + 0.38f, 15f), new Vector3(1.0f, 0.76f, 1.8f), plank);
            Box("DeskTop", parent, new Vector3(57.5f, fy + 0.77f, 15f), new Vector3(1.1f, 0.04f, 1.9f), timber, false);
            Cyl("ChairSeat", parent, new Vector3(56.5f, fy + 0.5f, 15.3f), 0.5f, 0.06f, rubber, Vector3.up);
            Cyl("ChairPost", parent, new Vector3(56.5f, fy + 0.25f, 15.3f), 0.06f, 0.5f, kit.Metal, Vector3.up);
            Box("ChairBack", parent, new Vector3(56.25f, fy + 0.8f, 15.3f), new Vector3(0.06f, 0.5f, 0.45f), rubber, false);
            for (int i = 0; i < 2; i++)
                Box("FilingCabinet", parent, new Vector3(59.4f, fy + 0.66f, 18.6f - i * 0.65f), new Vector3(0.6f, 1.32f, 0.55f), kit.Metal);
            Box("OpenDrawer", parent, new Vector3(58.85f, fy + 0.9f, 17.95f), new Vector3(0.6f, 0.25f, 0.5f), kit.Metal, false);
            for (int i = 0; i < 9; i++)
                Box("Paper", parent, new Vector3(Rand(54.5f, 59f), fy + 0.006f, Rand(12.8f, 19f)), new Vector3(0.22f, 0.01f, 0.3f), paper, false,
                    Quaternion.Euler(0f, Rand(0f, 180f), 0f));
            Box("Clipboard", parent, new Vector3(57.4f, fy + 0.8f, 14.6f), new Vector3(0.24f, 0.02f, 0.33f), timber, false,
                Quaternion.Euler(0f, 20f, 0f));
            Cyl("WallClock", parent, new Vector3(59.7f, fy + 2.1f, 15f), 0.35f, 0.05f, paper, Vector3.right);
            Box("Calendar", parent, new Vector3(59.72f, fy + 1.6f, 13.6f), new Vector3(0.02f, 0.5f, 0.35f), paper, false);

            // The desk lamp is on. Nothing in this building should have power.
            MeshObject("DeskLampShade", parent, GrayboxKit.ConeMesh(), enamel, new Vector3(57.7f, fy + 1.05f, 15.7f),
                Quaternion.identity, new Vector3(0.28f, 0.18f, 0.28f));
            Cyl("DeskLampStem", parent, new Vector3(57.7f, fy + 0.93f, 15.7f), 0.03f, 0.3f, kit.Metal, Vector3.up);
            var deskLamp = GrayboxKit.PointLight("DeskLamp", parent, new Vector3(57.7f, fy + 0.95f, 15.7f), new Color(1f, 0.75f, 0.45f), 2.2f, 6f);
            deskLamp.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------ dip room (burnt)

        static void BuildDipRoom(Transform parent)
        {
            const float z0 = HallWidth, z1 = 32f, x1 = 16f;
            Box("Floor", parent, new Vector3(x1 * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 + 0.5f, 0.1f, z1 - z0 + 0.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3(x1 * 0.5f, LowRoomHeight + 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 + 0.6f, 0.2f, z1 - z0 + 0.6f), soot);
            Wall("Wall_West", parent, new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), LowRoomHeight, soot);
            Wall("Wall_North", parent, new Vector3(0f, 0f, z1), new Vector3(x1, 0f, z1), LowRoomHeight, soot,
                Opening.Door(13f, 1.4f, 2.4f), Opening.Window(5f, 1.6f, 2.6f, 4.2f, boarded: true));
            Wall("Wall_East", parent, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), LowRoomHeight, soot,
                Opening.Window(6f, 1.8f, 1.8f, 4.0f));

            // Scorch spreading out of the doorway onto the hall side of the wall and floor.
            Box("ScorchWall", parent, new Vector3(7f, 3.6f, HallWidth - 0.27f), new Vector3(4.6f, 2.4f, 0.03f), soot, false);
            Box("ScorchWall", parent, new Vector3(7f, 2.0f, HallWidth - 0.27f), new Vector3(3.6f, 1.0f, 0.03f), soot, false);
            for (int i = 0; i < 18; i++)
            {
                bool inHall = i < 5;
                Vector3 p = inHall ? new Vector3(Rand(5f, 9f), 0.006f, Rand(17.5f, 19.6f)) : new Vector3(Rand(0.5f, 15.5f), 0.006f, Rand(20.5f, 31.5f));
                Box("ScorchFloor", parent, p, new Vector3(Rand(1f, 3f), 0.01f, Rand(1f, 3f)), soot, false, Quaternion.Euler(0f, Rand(0f, 90f), 0f));
            }

            // Dip tanks: long troughs where the heads were dipped. The liquid is still there.
            for (int i = 0; i < 2; i++)
            {
                float z = 24f + i * 4f;
                var tank = GrayboxKit.Group($"DipTank_{i}", parent).transform;
                Box("Trough", tank, new Vector3(7f, 0.45f, z), new Vector3(8f, 0.9f, 1.4f), iron);
                Box("Liquid", tank, new Vector3(7f, 0.86f, z), new Vector3(7.7f, 0.04f, 1.1f), dipLiquid, false);
                Beam("RackRail", tank, new Vector3(2.5f, 2.6f, z), new Vector3(11.5f, 2.6f, z), 0.12f, 0.12f, iron);
                foreach (float x in new[] { 3f, 11f })
                    Box("RackPost", tank, new Vector3(x, 1.3f, z + 0.8f), new Vector3(0.12f, 2.6f, 0.12f), iron);
            }

            // Drums by the east wall, some tipped.
            for (int i = 0; i < 9; i++)
            {
                bool tipped = rng.NextDouble() < 0.35;
                Vector3 p = new Vector3(Rand(13.4f, 15.2f), tipped ? 0.3f : 0.45f, Rand(21f, 25f));
                Cyl("Drum", parent, p, 0.6f, 0.9f, i % 3 == 0 ? soot : kit.Rust,
                    tipped ? Quaternion.Euler(0f, Rand(0f, 180f), 0f) * Vector3.right : Vector3.up, true);
            }

            // Fallen ceiling beams.
            Beam("FallenBeam", parent, new Vector3(2f, 0f, 30f), new Vector3(6f, LowRoomHeight - 0.2f, 31.6f), 0.3f, 0.3f, soot, true);
            Beam("FallenBeam", parent, new Vector3(9f, 0.2f, 21f), new Vector3(12.5f, 1.4f, 26f), 0.3f, 0.3f, soot); // no collider: it crosses the walking route
            for (int i = 0; i < 8; i++)
                Box("CharredDebris", parent, new Vector3(Rand(1.5f, 6f), Rand(0.1f, 0.4f), Rand(28.5f, 31.4f)), Vector3.one * Rand(0.3f, 0.7f), soot, false,
                    Quaternion.Euler(Rand(0f, 60f), Rand(0f, 90f), Rand(0f, 60f)));

            HangingLamp(parent, new Vector3(8f, 0f, 22f), LowRoomHeight, LampState.Flickering, "DipRoom");
        }

        // ------------------------------------------------------------------ stairwell (dead end)

        static void BuildStairwell(Transform parent)
        {
            const float x0 = 10f, x1 = 16f, z0 = 32f, z1 = 38f;
            Box("Floor", parent, new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.5f, 0.1f, z1 - z0 + 0.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3((x0 + x1) * 0.5f, LowRoomHeight + 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.6f, 0.2f, z1 - z0 + 0.6f), roofBoards);
            Wall("Wall_West", parent, new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), LowRoomHeight, brick);
            Wall("Wall_North", parent, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), LowRoomHeight, brick,
                Opening.Window(3f, 1.4f, 3.0f, 4.4f));
            Wall("Wall_East", parent, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), LowRoomHeight, brick,
                Opening.Door(3.5f, 1.2f, 2.3f));

            // Chained door east, toward the unbuilt wing.
            Box("Door", parent, new Vector3(x1, 1.15f, z0 + 3.5f), new Vector3(0.12f, 2.3f, 1.18f), plank);
            Beam("Chain", parent, new Vector3(x1 - 0.12f, 1.3f, z0 + 2.9f), new Vector3(x1 - 0.12f, 0.9f, z0 + 4.1f), 0.04f, 0.04f, iron);
            Box("Padlock", parent, new Vector3(x1 - 0.15f, 1.1f, z0 + 3.5f), new Vector3(0.08f, 0.14f, 0.1f), kit.Hazard, false);

            // Stairs up the west wall that end in a collapse.
            Stairs("Stair", parent, new Vector3(x0 + 1f, 0f, z0 + 1f), Vector3.forward, 4f, 2f, 1.5f, kit.Concrete);
            Railing("StairRail", parent, new Vector3(x0 + 1.8f, 0f, z0 + 1f), new Vector3(x0 + 1.8f, 2f, z0 + 5f), kit.Metal);
            for (int i = 0; i < 10; i++)
                Box("Rubble", parent, new Vector3(x0 + Rand(0.5f, 2f), Rand(1.8f, 4.6f), z0 + Rand(4.8f, 5.7f)), Vector3.one * Rand(0.4f, 0.9f),
                    debris, false, Quaternion.Euler(Rand(0f, 90f), Rand(0f, 90f), Rand(0f, 90f)));
            Beam("FallenJoist", parent, new Vector3(x0 + 0.3f, LowRoomHeight, z0 + 5.6f), new Vector3(x0 + 3f, 1.2f, z0 + 4.4f), 0.25f, 0.3f, timber, true);
            Blocker("CollapseBlock", parent, new Vector3(x0 + 1.25f, 3.5f, z0 + 5.4f), new Vector3(2.5f, 3f, 1.2f));
        }

        // ------------------------------------------------------------------ match litter

        /// <summary>
        /// Thousands of spilled match sticks merged into one mesh (one draw call), thickest along the
        /// machines' aisle sides, like the floor in the old photo.
        /// </summary>
        static void BuildMatchLitter(Transform parent)
        {
            AssetFolders.Ensure(ProjectPaths.Meshes);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int count = 3000;
            for (int i = 0; i < count; i++)
            {
                Vector3 c;
                double roll = rng.NextDouble();
                if (roll < 0.35) c = new Vector3(Rand(4f, 52f), 0f, Rand(6.4f, 8f));
                else if (roll < 0.7) c = new Vector3(Rand(4f, 52f), 0f, Rand(12f, 13.6f));
                else if (roll < 0.85) c = new Vector3(Rand(1f, 59f), 0f, Rand(0.5f, 19.5f));
                else c = new Vector3(Rand(1f, 13f), 0f, Rand(-11f, -1f));
                c.y = 0.004f;
                float yaw = Rand(0f, Mathf.PI);
                var along = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.05f;
                var across = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 0.008f;
                int s = vertices.Count;
                vertices.Add(c - along - across);
                vertices.Add(c - along + across);
                vertices.Add(c + along + across);
                vertices.Add(c + along - across);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LitterMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "MatchLitter" };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew)
                AssetDatabase.CreateAsset(mesh, LitterMeshPath);
            else
                EditorUtility.SetDirty(mesh);

            var go = MeshObject("MatchLitter", parent, mesh, matchWood, Vector3.zero, Quaternion.identity, Vector3.one);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ main breaker

        /// <summary>
        /// Main breaker on the receiving room's south wall, just right of the roll-up door as you come
        /// in. It starts OFF: the hall is dark until the player throws it. A red pilot light marks it.
        /// </summary>
        static void BuildMainBreaker(Transform parent)
        {
            Vector3 face = new Vector3(11f, 1.4f, -12f + WallThickness * 0.5f + 0.07f);
            parent.position = face;
            Box("Box", parent, face, new Vector3(0.7f, 0.9f, 0.14f), kit.Metal);
            Box("Label", parent, face + new Vector3(0f, 0.33f, 0.071f), new Vector3(0.45f, 0.08f, 0.01f), kit.Hazard, false);
            Box("PilotLamp", parent, face + new Vector3(0.25f, 0.33f, 0.08f), new Vector3(0.06f, 0.06f, 0.03f), pilotRed, false);
            var pilot = GrayboxKit.PointLight("PilotGlow", parent, face + new Vector3(0.25f, 0.33f, 0.25f), new Color(1f, 0.1f, 0.05f), 0.8f, 2.5f);
            pilot.transform.SetParent(parent, true);
            Beam("Conduit", parent, face + new Vector3(0f, 0.45f, -0.02f), face + new Vector3(0f, LowRoomHeight - 1.4f, -0.02f), 0.06f, 0.06f, kit.Metal);

            // The lever moves, so it must not be static.
            var pivot = GrayboxKit.Group("LeverPivot", parent);
            pivot.transform.position = face + new Vector3(0f, -0.05f, 0.09f);
            var handle = Box("Handle", pivot.transform, pivot.transform.position + new Vector3(0f, 0.13f, 0f),
                new Vector3(0.08f, 0.26f, 0.08f), kit.Hazard, false);
            GameObjectUtility.SetStaticEditorFlags(handle, 0);

            var breaker = parent.gameObject.AddComponent<LightSwitch>();
            SerializedFields.SetBool(breaker, "startOn", false);
            SetString(breaker, "turnOnPrompt", "Throw the main breaker");
            SetString(breaker, "turnOffPrompt", "Cut the main breaker");
            SerializedFields.SetArray(breaker, "lights", BreakerLights);
            SerializedFields.SetArray(breaker, "fixtures", BreakerFixtures);
            SerializedFields.Set(breaker, "lever", pivot.transform);
        }

        static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Horror Game] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ AI waypoints

        /// <summary>
        /// Empty markers along the walkable routes, for the monster's patrol when it moves in here.
        /// Nothing reads them yet.
        /// </summary>
        static void BuildWaypoints()
        {
            var root = GrayboxKit.Group("AI_Waypoints", null).transform;
            Vector3[] points =
            {
                new Vector3(7f, 0f, -6f),                                     // receiving
                new Vector3(10.5f, 0f, 1.5f),                                 // hall doorway
                new Vector3(4f, 0f, 10f), new Vector3(18f, 0f, 10f), new Vector3(30f, 0f, 10f),
                new Vector3(42f, 0f, 10f), new Vector3(56f, 0f, 9f),          // centre aisle
                new Vector3(12f, 0f, 2f), new Vector3(30f, 0f, 2f), new Vector3(50f, 0f, 2f),   // south aisle
                new Vector3(12f, 0f, 18f), new Vector3(30f, 0f, 18f), new Vector3(42f, 0f, 18f), // north aisle
                new Vector3(52.8f, PlatformHeight, 15f), new Vector3(56f, PlatformHeight, 17f), // landing, office
                new Vector3(7f, 0f, 22f), new Vector3(8f, 0f, 30.5f),         // dip room
                new Vector3(13f, 0f, 34f),                                    // stairwell
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
            RenderSettings.ambientLight = new Color(0.05f, 0.055f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        }

        static void CreateMoonlight()
        {
            var moon = new GameObject("Moonlight");
            // Low from the south-south-west, so it falls through the south windows in long patches.
            moon.transform.rotation = Quaternion.Euler(32f, 20f, 0f);
            var light = moon.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.65f, 0.95f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            RenderSettings.sun = light;
        }

        /// <summary>The approved look: dark grade, heavy grain, vignette, slight aberration, and the pixel filter.</summary>
        static void CreatePostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile == null)
            {
                AssetFolders.Ensure(ProjectPaths.Settings);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostFxPath);

                profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(1.0f);
                color.contrast.Override(10f);
                color.saturation.Override(-40f);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.42f);
                vignette.smoothness.Override(0.45f);
                var grain = profile.Add<FilmGrain>(true);
                grain.type.Override(FilmGrainLookup.Medium1);
                grain.intensity.Override(0.5f);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1f);
                bloom.intensity.Override(0.5f);
                profile.Add<ChromaticAberration>(true).intensity.Override(0.15f);

                foreach (var component in profile.components)
                {
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            var go = new GameObject("PostProcessVolume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            new GameObject("PixelationFilter").AddComponent<PixelationFilter>();
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
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity); // facing north, at the roll-up door
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
