using System.Collections.Generic;
using System.IO;
using HorrorGame.Audio;
using HorrorGame.Glass;
using HorrorGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates the glass house test room (Scenes/GlassHouse_Test.unity): an 18 x 18 m room split
    /// into a 3 x 3 grid of 6 m cells by sliding glass panes. Run Horror Game > Build Glass House Test Room.
    ///
    /// Layout (metres, +X east, +Z north, origin = south-west inside corner):
    ///   Glass lines   x = 6, x = 12 (panes run north-south), z = 6, z = 12 (panes run east-west)
    ///   Each line     3 wall sections (one per cell it borders), each two 3 m panes that slide
    ///                 over each other like patio doors: 12 sections, 24 panes in all
    ///   Player        south-west cell, facing north-east; the shift lever is on the wall behind you
    ///   Foreman       north-east cell, patrolling all nine cell centres
    ///   Ghost         centre cell
    ///   Green-etched panes let the ghost through. Two cells are unlit.
    /// </summary>
    public static class GlassHouseBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/GlassHouse_Test.unity";
        const string TexturesFolder = ProjectPaths.Root + "/Textures";
        const string EtchTexturePath = TexturesFolder + "/T_GlassEtch.png";
        const string EtchGlowTexturePath = TexturesFolder + "/T_GlassEtchGlow.png";

        const float RoomSize = 18f;
        internal const float Cell = 6f;
        const float Height = 3.6f;
        const float WallThickness = 0.4f;
        internal const float PaneLength = 2.96f;
        const float PaneHeight = 3.48f;
        const float PaneThickness = 0.05f;
        internal const float PaneBase = 0.03f;
        internal const float TrackOffset = 0.1f;

        /// <summary>Panes the ghost can float through (index = section * 2 + pane in pair).</summary>
        static readonly HashSet<int> HauntedPanes = new HashSet<int> { 2, 7, 15, 22 };
        /// <summary>Cells (column, row) left without a lamp.</summary>
        static readonly HashSet<(int, int)> DarkCells = new HashSet<(int, int)> { (1, 1), (2, 1) };
        static readonly HashSet<(int, int)> FlickerCells = new HashSet<(int, int)> { (2, 0), (0, 2) };

        static readonly Color FogColor = new Color(0.015f, 0.018f, 0.024f);
        static readonly Vector3 PlayerSpawn = new Vector3(3f, 0.1f, 3f);
        static readonly Vector3 ForemanSpawn = new Vector3(15f, 0f, 15f);
        static readonly Vector3 GhostSpawn = new Vector3(9f, 1.3f, 9f);

        static GrayboxKit kit;

        /// <summary>The kit the shared helpers below use. Other glass builders (the mansion) set it first.</summary>
        internal static GrayboxKit Kit { get => kit; set => kit = value; }

        [MenuItem("Horror Game/Build Glass House Test Room", priority = 30)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Horror Game/Open Glass House Test Room", priority = 31)]
        static void OpenFromMenu()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Not built yet", "Run Horror Game > Build Glass House Test Room first.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Batch entry point: -executeMethod HorrorGame.EditorTools.GlassHouseBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            Debug.Log("[Horror Game] Batch build of the glass house finished.");
        }

        public static void Build()
        {
            AssetFolders.Ensure(ProjectPaths.Scenes);
            RenderPipelineSetup.EnsureUrp();
            kit = new GrayboxKit();
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();
            var glass = GlassMaterial("M_Glass", new Color(0.16f, 0.2f, 0.24f));
            var hauntedGlass = GlassMaterial("M_GlassHaunted", new Color(0.08f, 0.32f, 0.14f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GrayboxKit.Group("GlassHouse_Room", null).transform;
            BuildShell(GrayboxKit.Group("Shell", root).transform);
            BuildPosts(GrayboxKit.Group("Posts", root).transform);
            var warningLights = BuildLamps(GrayboxKit.Group("Lamps", root).transform);

            var houseObject = new GameObject("GlassHouse");
            var panes = BuildPanes(houseObject.transform, glass, hauntedGlass);
            var house = houseObject.AddComponent<GlassHouse>();
            SerializedFields.SetArray(house, "panes", panes);
            SerializedFields.SetArray(house, "warningLights", warningLights);

            BuildLever(root, house);
            BuildGhost();
            BuildForeman();

            new GameObject("GlassAudioMuffler").AddComponent<GlassAudioMuffler>();
            ConfigureAtmosphere();
            CreateAmbience();
            SpawnPlayer(playerPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Horror Game] Built {ScenePath}: {panes.Count} glass panes in {panes.Count / 2} sliding sections.");
        }

        // ------------------------------------------------------------------ room

        static void BuildShell(Transform parent)
        {
            float half = RoomSize * 0.5f;
            float outer = RoomSize + WallThickness * 2f;
            kit.Box("Floor", parent, new Vector3(half, -0.1f, half), new Vector3(outer, 0.2f, outer), kit.ConcreteFloor);
            kit.Box("Ceiling", parent, new Vector3(half, Height + 0.1f, half), new Vector3(outer, 0.2f, outer), kit.Ceiling);
            float y = Height * 0.5f;
            kit.Box("Wall_South", parent, new Vector3(half, y, -WallThickness * 0.5f), new Vector3(outer, Height, WallThickness), kit.Wall);
            kit.Box("Wall_North", parent, new Vector3(half, y, RoomSize + WallThickness * 0.5f), new Vector3(outer, Height, WallThickness), kit.Wall);
            kit.Box("Wall_West", parent, new Vector3(-WallThickness * 0.5f, y, half), new Vector3(WallThickness, Height, RoomSize), kit.Wall);
            kit.Box("Wall_East", parent, new Vector3(RoomSize + WallThickness * 0.5f, y, half), new Vector3(WallThickness, Height, RoomSize), kit.Wall);

            // Floor and ceiling tracks the panes run in. Visual only, so nothing trips on them.
            for (int i = 1; i <= 2; i++)
            {
                float at = i * Cell;
                kit.Box($"Track_Floor_X{at}", parent, new Vector3(at, 0.006f, half), new Vector3(0.32f, 0.012f, RoomSize), kit.Metal, false);
                kit.Box($"Track_Floor_Z{at}", parent, new Vector3(half, 0.007f, at), new Vector3(RoomSize, 0.012f, 0.32f), kit.Metal, false);
                kit.Box($"Track_Ceiling_X{at}", parent, new Vector3(at, Height - 0.04f, half), new Vector3(0.34f, 0.08f, RoomSize), kit.Metal, false);
                kit.Box($"Track_Ceiling_Z{at}", parent, new Vector3(half, Height - 0.05f, at), new Vector3(RoomSize, 0.08f, 0.34f), kit.Metal, false);
            }
        }

        /// <summary>Steel posts where glass lines cross and where they meet the outer walls.</summary>
        static void BuildPosts(Transform parent)
        {
            var size = new Vector3(0.24f, Height, 0.24f);
            for (int i = 1; i <= 2; i++)
            for (int j = 0; j <= 3; j++)
            {
                float a = i * Cell, b = j * Cell;
                kit.Box($"Post_{a}_{b}", parent, new Vector3(a, Height * 0.5f, b), size, kit.Rust);
                if (j == 0 || j == 3)
                    kit.Box($"Post_{b}_{a}", parent, new Vector3(b, Height * 0.5f, a), size, kit.Rust);
            }
        }

        /// <summary>One lamp per lit cell, plus red warning lights (off until a shift) on the four inner posts.</summary>
        static List<Object> BuildLamps(Transform parent)
        {
            for (int c = 0; c < 3; c++)
            for (int r = 0; r < 3; r++)
            {
                var center = new Vector3(c * Cell + Cell * 0.5f, Height - 0.05f, r * Cell + Cell * 0.5f);
                bool dark = DarkCells.Contains((c, r));
                var fixture = kit.Box($"LampFixture_{c}{r}", parent, center, new Vector3(0.6f, 0.08f, 0.6f),
                    dark ? kit.Metal : kit.LampWarm, false);
                if (dark)
                    continue;
                var light = GrayboxKit.PointLight($"Lamp_{c}{r}", parent, center - Vector3.up * 0.3f,
                    new Color(1f, 0.82f, 0.6f), 1.4f, 7f);
                if (c == 0 && r == 0 || c == 1 && r == 2)
                    light.shadows = LightShadows.Soft; // the posts throw a few long shadows
                if (FlickerCells.Contains((c, r)))
                {
                    var flicker = light.gameObject.AddComponent<LightFlicker>();
                    SerializedFields.Set(flicker, "emissiveRenderer", fixture.GetComponent<Renderer>());
                }
            }

            var warning = new List<Object>();
            for (int i = 1; i <= 2; i++)
            for (int j = 1; j <= 2; j++)
            {
                var light = GrayboxKit.PointLight($"WarningLight_{i}{j}", parent, new Vector3(i * Cell, Height - 0.3f, j * Cell),
                    new Color(1f, 0.08f, 0.04f), 0f, 9f);
                light.enabled = false;
                warning.Add(light);
            }
            return warning;
        }

        // ------------------------------------------------------------------ glass

        /// <summary>
        /// 24 panes, in pairs. Lines 0-1 run north-south at x = 6 and 12; lines 2-3 run east-west at
        /// z = 6 and 12. Each line has three sections (one per cell), each with pane A (opens forward,
        /// onto B's slot) and pane B (opens backward, onto A's slot), on opposite sides of the track.
        /// </summary>
        static List<Object> BuildPanes(Transform parent, Material glass, Material hauntedGlass)
        {
            var panes = new List<Object>();
            for (int line = 0; line < 4; line++)
            {
                bool northSouth = line < 2;
                float at = (line % 2 + 1) * Cell;
                Vector3 along = northSouth ? Vector3.forward : Vector3.right;
                Vector3 normal = northSouth ? Vector3.right : Vector3.forward;
                var rotation = Quaternion.LookRotation(normal); // local X runs along the line, local Z across it

                for (int section = 0; section < 3; section++)
                for (int k = 0; k < 2; k++)
                {
                    int index = panes.Count;
                    bool haunted = HauntedPanes.Contains(index);
                    float s = section * Cell + Cell * 0.25f + k * Cell * 0.5f;
                    Vector3 position = northSouth ? new Vector3(at, PaneBase, s) : new Vector3(s, PaneBase, at);
                    float direction = k == 0 ? 1f : -1f;
                    Vector3 openOffset = along * (Cell * 0.5f * direction) + normal * (TrackOffset * direction);
                    string name = $"Pane_L{line}_S{section}_{(k == 0 ? "A" : "B")}{(haunted ? "_Haunted" : "")}";
                    panes.Add(BuildPane(name, parent, position, rotation, openOffset, haunted, haunted ? hauntedGlass : glass));
                }
            }
            return panes;
        }

        internal static GlassPane BuildPane(string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 openOffset,
            bool haunted, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, PaneHeight * 0.5f, 0f);
            collider.size = new Vector3(PaneLength, PaneHeight, PaneThickness);

            // The runtime NavMesh skips the Ignore Raycast layer, so carve the pane out for monsters.
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = new Vector3(PaneLength, PaneHeight, 0.3f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            var visual = GrayboxKit.Group("Visual", root.transform).transform;
            Part("Glass", visual, new Vector3(0f, PaneHeight * 0.5f, 0f), new Vector3(PaneLength, PaneHeight, PaneThickness * 0.6f),
                material, ShadowCastingMode.Off);
            // Thin steel frame so the pane's edges catch the light even when the glass doesn't.
            float x = PaneLength * 0.5f - 0.025f;
            var frame = new Vector3(0.05f, PaneHeight, PaneThickness + 0.02f);
            Part("Frame_Left", visual, new Vector3(-x, PaneHeight * 0.5f, 0f), frame, kit.Metal, ShadowCastingMode.On);
            Part("Frame_Right", visual, new Vector3(x, PaneHeight * 0.5f, 0f), frame, kit.Metal, ShadowCastingMode.On);
            var rail = new Vector3(PaneLength, 0.06f, PaneThickness + 0.02f);
            Part("Frame_Bottom", visual, new Vector3(0f, 0.03f, 0f), rail, kit.Metal, ShadowCastingMode.On);
            Part("Frame_Top", visual, new Vector3(0f, PaneHeight - 0.03f, 0f), rail, kit.Metal, ShadowCastingMode.On);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = GlassPane.Layer;

            var pane = root.AddComponent<GlassPane>();
            SerializedFields.Set(pane, "shakeVisual", visual);
            SerializedFields.SetBool(pane, "ghostsPassThrough", haunted);
            SetVector3(pane, "openOffset", openOffset);
            return pane;
        }

        internal static void Part(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, ShadowCastingMode shadows)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows;
        }

        internal static void SetVector3(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Horror Game] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }
            property.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ lever, ghost, Foreman

        static void BuildLever(Transform parent, GlassHouse house)
        {
            // On the south wall of the starting cell, behind the player's spawn, facing north into the room.
            BuildLever(parent, house, new Vector3(1.6f, 0f, 0f), Quaternion.identity, false);
        }

        /// <summary>A shift lever at <paramref name="position"/> (its foot), facing along <paramref name="rotation"/>'s forward.
        /// <paramref name="freestanding"/> adds a pedestal behind it so it can stand in the middle of a room.</summary>
        internal static GlassHouseLever BuildLever(Transform parent, GlassHouse house, Vector3 position, Quaternion rotation, bool freestanding)
        {
            var root = new GameObject("ShiftLever");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            if (freestanding)
            {
                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedestal.name = "Pedestal";
                pedestal.transform.SetParent(root.transform, false);
                pedestal.transform.localPosition = new Vector3(0f, 0.9f, -0.2f);
                pedestal.transform.localScale = new Vector3(0.7f, 1.8f, 0.4f);
                pedestal.GetComponent<Renderer>().sharedMaterial = kit.Metal;
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.35f, 0.18f);
            collider.size = new Vector3(0.6f, 1.1f, 0.36f);

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Plate";
            Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(0f, 1.3f, 0.05f);
            plate.transform.localScale = new Vector3(0.5f, 0.9f, 0.1f);
            plate.GetComponent<Renderer>().sharedMaterial = kit.Rust;

            var pivot = GrayboxKit.Group("HandlePivot", root.transform).transform;
            pivot.localPosition = new Vector3(0f, 1.3f, 0.12f);
            pivot.localRotation = Quaternion.Euler(-35f, 0f, 0f); // rests tilted up; pulling swings it down
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            Object.DestroyImmediate(handle.GetComponent<Collider>());
            handle.transform.SetParent(pivot, false);
            handle.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            handle.transform.localScale = new Vector3(0.07f, 0.5f, 0.07f);
            handle.GetComponent<Renderer>().sharedMaterial = kit.Hazard;

            var lever = root.AddComponent<GlassHouseLever>();
            SerializedFields.Set(lever, "house", house);
            SerializedFields.Set(lever, "handle", pivot);
            return lever;
        }

        static void BuildGhost() => BuildGhost(GhostSpawn, null);

        /// <summary>A ghost at <paramref name="position"/>. <paramref name="roamArea"/> null keeps the component's default (the test room).</summary>
        internal static GlassGhost BuildGhost(Vector3 position, Bounds? roamArea)
        {
            var body = TransparentMaterial("M_Ghost", new Color(0.75f, 0.85f, 1f, 0.22f), new Color(0.22f, 0.28f, 0.36f), null, null);
            var eyes = kit.Get("M_GhostEyes", new Color(0.01f, 0.01f, 0.01f), 0f);

            var root = new GameObject("Ghost");
            root.transform.position = position;
            var renderers = new List<Object>
            {
                GhostPart(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, -0.35f, 0f), new Vector3(0.5f, 0.6f, 0.4f), body),
                GhostPart(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.36f, 0.42f, 0.36f), body),
                GhostPart(PrimitiveType.Sphere, "EyeLeft", root.transform, new Vector3(-0.07f, 0.5f, 0.16f), Vector3.one * 0.07f, eyes),
                GhostPart(PrimitiveType.Sphere, "EyeRight", root.transform, new Vector3(0.07f, 0.5f, 0.16f), Vector3.one * 0.07f, eyes),
            };
            var ghost = root.AddComponent<GlassGhost>();
            SerializedFields.SetArray(ghost, "renderers", renderers);
            SerializedFields.SetFloat(ghost, "hoverHeight", position.y);
            if (roamArea.HasValue)
            {
                var so = new SerializedObject(ghost);
                so.FindProperty("roamArea").boundsValue = roamArea.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return ghost;
        }

        static Renderer GhostPart(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        static void BuildForeman()
        {
            var route = GrayboxKit.Group("Foreman_Patrol", null).transform;
            var points = new List<Object>();
            // Snake through the nine cells, starting in his own corner.
            int[] order = { 8, 7, 6, 3, 4, 5, 2, 1, 0 };
            foreach (int cell in order)
            {
                int c = cell % 3, r = cell / 3;
                var point = new GameObject($"Point_{c}{r}").transform;
                point.SetParent(route, false);
                point.position = new Vector3(c * Cell + Cell * 0.5f, 0f, r * Cell + Cell * 0.5f);
                points.Add(point);
            }
            // NavMesh box from just under the floor to below the ceiling, so the roof isn't walkable.
            var navBounds = new Bounds(new Vector3(RoomSize * 0.5f, 1.5f, RoomSize * 0.5f), new Vector3(RoomSize + 1f, 3.4f, RoomSize + 1f));
            MonsterBuilder.BuildInArena(kit, ForemanSpawn, points, navBounds);
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Nearly clear, very smooth glass with faint etched lines that glow just enough to read in the dark.</summary>
        internal static Material GlassMaterial(string name, Color etchGlow)
        {
            EnsureEtchTextures();
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(EtchTexturePath);
            var glowMap = AssetDatabase.LoadAssetAtPath<Texture2D>(EtchGlowTexturePath);
            return TransparentMaterial(name, new Color(0.82f, 0.9f, 0.95f, 1f), etchGlow, baseMap, glowMap);
        }

        /// <summary>
        /// URP Lit, transparent (alpha blend) with "preserve specular": the surface fades out but
        /// highlights from the flashlight stay bright, which is what makes glass read as glass.
        /// Settings are re-applied on every build so the look is defined here.
        /// </summary>
        internal static Material TransparentMaterial(string name, Color baseColor, Color emission, Texture2D baseMap, Texture2D emissionMap)
        {
            string path = $"{ProjectPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", baseColor);
            material.SetTexture("_BaseMap", baseMap);
            material.SetFloat("_Smoothness", 0.96f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetColor("_EmissionColor", emission);
            material.SetTexture("_EmissionMap", emissionMap);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f); // alpha
            material.SetFloat("_BlendModePreserveSpecular", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_CastShadows", 0f);
            material.SetFloat("_ReceiveShadows", 1f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("DepthNormals", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureEtchTextures()
        {
            if (File.Exists(EtchTexturePath) && File.Exists(EtchGlowTexturePath))
                return;
            AssetFolders.Ensure(TexturesFolder);
            const int size = 512;
            float[] mask = GlassEtching.Draw(size, 13);
            var etch = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var glow = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var etchPixels = new Color32[size * size];
            var glowPixels = new Color32[size * size];
            for (int i = 0; i < mask.Length; i++)
            {
                // Clean glass is almost invisible; etched lines are frosted.
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(0.035f, 0.55f, mask[i]) * 255f);
                etchPixels[i] = new Color32(255, 255, 255, alpha);
                byte g = (byte)Mathf.RoundToInt(mask[i] * 255f);
                glowPixels[i] = new Color32(g, g, g, 255);
            }
            etch.SetPixels32(etchPixels);
            glow.SetPixels32(glowPixels);
            File.WriteAllBytes(EtchTexturePath, etch.EncodeToPNG());
            File.WriteAllBytes(EtchGlowTexturePath, glow.EncodeToPNG());
            Object.DestroyImmediate(etch);
            Object.DestroyImmediate(glow);
            AssetDatabase.ImportAsset(EtchTexturePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(EtchGlowTexturePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImporter(EtchTexturePath, true);
            ConfigureImporter(EtchGlowTexturePath, false);
        }

        static void ConfigureImporter(string path, bool alpha)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alpha;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ scene setup

        internal static void ConfigureAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.045f, 0.055f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.fogColor = FogColor;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            var volume = new GameObject("PostProcessVolume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = RenderPipelineSetup.EnsurePostFxProfile();
        }

        internal static void CreateAmbience()
        {
            var go = new GameObject("Ambience_Drone");
            var ambience = go.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(ambience, "sound", (int)ProceduralSound.Drone);
            SerializedFields.SetFloat(ambience, "volume", 0.25f);
            SerializedFields.SetBool(ambience, "spatial", false);
        }

        static void SpawnPlayer(GameObject prefab) => SpawnPlayer(prefab, PlayerSpawn, Quaternion.Euler(0f, 45f, 0f));

        internal static void SpawnPlayer(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            player.transform.SetPositionAndRotation(position, rotation);
            var camera = player.GetComponentInChildren<Camera>();
            if (camera != null)
                camera.backgroundColor = FogColor;
        }

        static void AddToBuildSettings() => AddToBuildSettings(ScenePath);

        internal static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath)
                    return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
