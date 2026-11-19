using System.Collections.Generic;
using System.IO;
using HorrorGame.Glass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Generates the glass mansion (Scenes/GlassMansion.unity) from the text floor plans below: a
    /// basement, a ground floor and an upper floor, 30 x 24 m, on a grid of 6 m cells.
    /// Run Horror Game > Build Glass Mansion after changing a plan.
    ///
    /// How a plan reads. One string per row of cells, NORTH row first. Each cell is two characters:
    ///   1st  room letter. Neighbouring cells with different letters get a sliding glass wall between
    ///        them; the same letter means one open room. '.' for a void cell.
    ///   2nd  what is in the cell:
    ///        _  plain floor
    ///        g  glass floor panel (you see the room below through it)
    ///        v  no floor: open to the floor below (atrium, or the top of a staircase)
    ///        S  staircase going UP to the next floor, climbing south to north. The cell above must
    ///           be 'v' and the cell north of that must have a floor (you arrive there).
    ///        L  the shift lever (pulling it shifts the glass on every floor)
    ///        P  player start
    ///        F  where the stand-in monster starts
    /// The outside of the grid is solid wall. Edges between a floor and a void get a railing.
    ///
    /// The Foreman is only a stand-in to test chases; the mansion will get its own ghosts.
    /// </summary>
    public static class GlassMansionBuilder
    {
        public const string ScenePath = ProjectPaths.Scenes + "/GlassMansion.unity";

        // ------------------------------------------------------------------ floor plans (north row first)

        static readonly string[] Basement =
        {
            "W_ W_ X_ X_ Y_",
            "W_ WL X_ Y_ Y_",
            "ZS Z_ X_ Y_ YF",
            "Z_ Z_ X_ Y_ Y_",
        };

        static readonly string[] Ground =
        {
            "L_ L_ H_ N_ N_",
            "L_ L_ H_ N_ N_",
            ".v K_ H_ O_ OS",
            "KP K_ H_ O_ O_",
        };

        static readonly string[] Upper =
        {
            "U_ U_ Tg V_ V_",
            "U_ U_ .v V_ V_",
            "R_ Rg .v Q_ .v",
            "R_ R_ Tg Q_ Q_",
        };

        static readonly FloorPlan[] Floors =
        {
            new FloorPlan("Basement", -4f, Basement),
            new FloorPlan("Ground floor", 0f, Ground),
            new FloorPlan("Upper floor", 4f, Upper),
        };

        // ------------------------------------------------------------------ dimensions

        const float Cell = GlassHouseBuilder.Cell;
        const float StoreyHeight = 4f;
        const float SlabThickness = 0.3f;
        const float WallThickness = 0.4f;
        const float RailHeight = 1.05f;
        const float StairWidth = 2.4f;
        const int StairSteps = 16;
        /// <summary>Seconds between one floor's shift and the next one's, so they don't all move at once.</summary>
        const float FloorShiftStagger = 15f;

        static GrayboxKit kit;
        static Material floorMaterial, wallMaterial, basementWall, glassMaterial, stairMaterial;

        [MenuItem("Horror Game/Build Glass Mansion", priority = 32)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Horror Game/Open Glass Mansion", priority = 33)]
        static void OpenFromMenu()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("Not built yet", "Run Horror Game > Build Glass Mansion first.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Batch entry point: -executeMethod HorrorGame.EditorTools.GlassMansionBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            Debug.Log("[Horror Game] Batch build of the glass mansion finished.");
        }

        public static void Build()
        {
            if (!ValidatePlans())
            {
                Debug.LogError("[Horror Game] Glass mansion not built: fix the floor plans first (see errors above).");
                return;
            }

            AssetFolders.Ensure(ProjectPaths.Scenes);
            RenderPipelineSetup.EnsureUrp();
            kit = new GrayboxKit();
            GlassHouseBuilder.Kit = kit;
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();
            floorMaterial = kit.Get("M_MansionFloor", new Color(0.2f, 0.12f, 0.07f), 0.35f);
            wallMaterial = kit.Get("M_MansionWall", new Color(0.2f, 0.17f, 0.16f), 0.1f);
            stairMaterial = kit.Get("M_MansionStair", new Color(0.15f, 0.09f, 0.05f), 0.3f);
            basementWall = kit.Concrete;
            glassMaterial = GlassHouseBuilder.GlassMaterial("M_Glass", new Color(0.16f, 0.2f, 0.24f));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GrayboxKit.Group("GlassMansion", null).transform;

            var houses = new List<GlassHouse>();
            Vector3? playerSpawn = null, monsterSpawn = null;
            GlassHouse leverHouse = null;
            Vector3 leverPosition = default;
            var patrol = new List<Object>();
            var patrolRoot = GrayboxKit.Group("Foreman_Patrol", null).transform;
            int sections = 0;

            for (int f = 0; f < Floors.Length; f++)
            {
                var plan = Floors[f];
                var floorRoot = GrayboxKit.Group(plan.Name, root).transform;
                BuildSlabs(f, GrayboxKit.Group("Floor", floorRoot).transform);
                BuildWallsAndRailings(f, GrayboxKit.Group("Walls", floorRoot).transform);
                BuildStairs(f, GrayboxKit.Group("Stairs", floorRoot).transform);
                BuildLamps(f, GrayboxKit.Group("Lamps", floorRoot).transform);
                var house = BuildGlass(f, floorRoot, houses.Count);
                if (house != null)
                {
                    houses.Add(house);
                    sections += house.SectionCount;
                }

                var roomsSeen = new HashSet<char>();
                for (int r = 0; r < plan.Rows; r++)
                for (int c = 0; c < plan.Columns; c++)
                {
                    var cell = plan.Get(c, r);
                    if (!cell.HasFloor)
                        continue;
                    Vector3 center = CellCenter(f, c, r);
                    if (cell.Feature == 'P')
                        playerSpawn = center + Vector3.up * 0.1f;
                    if (cell.Feature == 'F')
                        monsterSpawn = center;
                    if (cell.Feature == 'L')
                    {
                        leverHouse = house;
                        leverPosition = center;
                    }
                    if (cell.Feature != 'S' && roomsSeen.Add(cell.Room))
                    {
                        var point = new GameObject($"Point_{plan.Name}_{cell.Room}").transform;
                        point.SetParent(patrolRoot, false);
                        point.position = center;
                        patrol.Add(point);
                    }
                }
            }

            BuildRoof(root);

            if (leverHouse != null)
            {
                var others = new List<Object>();
                foreach (var h in houses)
                    if (h != leverHouse)
                        others.Add(h);
                // Freestanding in the middle of its cell, facing south.
                var lever = GlassHouseBuilder.BuildLever(root, leverHouse, leverPosition, Quaternion.Euler(0f, 180f, 0f), true);
                SerializedFields.SetArray(lever, "alsoShifts", others);
            }

            // Placeholder ghost drifting around the ground floor until the mansion's own ghosts exist.
            int ground = 1;
            GlassHouseBuilder.BuildGhost(CellCenter(ground, 2, 2) + Vector3.up * 1.3f,
                new Bounds(new Vector3(Floors[ground].Columns * Cell * 0.5f, Floors[ground].Y + 1.3f, Floors[ground].Rows * Cell * 0.5f),
                    new Vector3(Floors[ground].Columns * Cell - 1f, 0.1f, Floors[ground].Rows * Cell - 1f)));

            var navBounds = NavBounds();
            if (monsterSpawn.HasValue)
                MonsterBuilder.BuildInArena(kit, monsterSpawn.Value, patrol, navBounds);

            new GameObject("GlassAudioMuffler").AddComponent<GlassAudioMuffler>();
            GlassHouseBuilder.ConfigureAtmosphere();
            GlassHouseBuilder.CreateAmbience();
            GlassHouseBuilder.SpawnPlayer(playerPrefab, playerSpawn ?? CellCenter(ground, 0, 0), Quaternion.Euler(0f, 45f, 0f));

            int unreachable = CheckEveryRoomIsReachable(playerSpawn ?? CellCenter(ground, 0, 0), patrol, navBounds);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            GlassHouseBuilder.AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Horror Game] Built {ScenePath}: {Floors.Length} floors, {sections} sliding glass sections, " +
                      $"{patrol.Count} rooms, {unreachable} unreachable with all glass open.");
        }

        // ------------------------------------------------------------------ floors, walls, railings

        static void BuildSlabs(int f, Transform parent)
        {
            var plan = Floors[f];
            for (int r = 0; r < plan.Rows; r++)
            for (int c = 0; c < plan.Columns; c++)
            {
                var cell = plan.Get(c, r);
                if (!cell.HasFloor)
                    continue;
                Vector3 center = CellCenter(f, c, r);
                if (cell.Feature == 'g')
                {
                    BuildGlassFloor(parent, center, $"GlassFloor_{c}{r}");
                    continue;
                }
                kit.Box($"Slab_{c}{r}", parent, center + Vector3.down * (SlabThickness * 0.5f),
                    new Vector3(Cell, SlabThickness, Cell), floorMaterial);
            }
        }

        /// <summary>A walkable sheet of etched glass in a steel frame. Solid (Default layer), so the NavMesh walks on it.</summary>
        static void BuildGlassFloor(Transform parent, Vector3 center, string name)
        {
            var root = GrayboxKit.Group(name, parent).transform;
            var glass = kit.Box("Glass", root, center + Vector3.down * 0.04f, new Vector3(Cell, 0.08f, Cell), glassMaterial);
            glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(glass, 0); // no lightmapping or occlusion: it's see-through
            float half = Cell * 0.5f;
            var beam = new Vector3(Cell, SlabThickness, 0.15f);
            kit.Box("Beam_N", root, center + new Vector3(0f, -SlabThickness * 0.5f, half - 0.075f), beam, kit.Metal, false);
            kit.Box("Beam_S", root, center + new Vector3(0f, -SlabThickness * 0.5f, -half + 0.075f), beam, kit.Metal, false);
            beam = new Vector3(0.15f, SlabThickness, Cell);
            kit.Box("Beam_E", root, center + new Vector3(half - 0.075f, -SlabThickness * 0.5f, 0f), beam, kit.Metal, false);
            kit.Box("Beam_W", root, center + new Vector3(-half + 0.075f, -SlabThickness * 0.5f, 0f), beam, kit.Metal, false);
            kit.Box("Beam_Mid", root, center + new Vector3(0f, -0.12f, 0f), new Vector3(0.1f, 0.16f, Cell), kit.Metal, false);
        }

        static void BuildRoof(Transform parent)
        {
            var top = Floors[Floors.Length - 1];
            float y = top.Y + StoreyHeight;
            float w = top.Columns * Cell, d = top.Rows * Cell;
            kit.Box("Roof", parent, new Vector3(w * 0.5f, y - SlabThickness * 0.5f, d * 0.5f),
                new Vector3(w + WallThickness * 2f, SlabThickness, d + WallThickness * 2f), kit.Ceiling);
        }

        /// <summary>
        /// Every cell edge: outer wall at the grid boundary, a solid wall under the top of each staircase,
        /// a railing between floor and void (open where a staircase arrives). Glass is built separately.
        /// </summary>
        static void BuildWallsAndRailings(int f, Transform parent)
        {
            var plan = Floors[f];
            float y = plan.Y;
            var outer = f == 0 ? basementWall : wallMaterial;
            float wallHeight = StoreyHeight - SlabThickness;

            for (int r = 0; r < plan.Rows; r++)
            for (int c = 0; c < plan.Columns; c++)
            {
                // Outer walls on boundary edges (full storey height, slab to slab).
                if (c == 0)
                    kit.Box($"Wall_W_{r}", parent, new Vector3(-WallThickness * 0.5f, y + wallHeight * 0.5f - SlabThickness * 0.5f, (r + 0.5f) * Cell),
                        new Vector3(WallThickness, StoreyHeight, Cell), outer);
                if (c == plan.Columns - 1)
                    kit.Box($"Wall_E_{r}", parent, new Vector3(plan.Columns * Cell + WallThickness * 0.5f, y + wallHeight * 0.5f - SlabThickness * 0.5f, (r + 0.5f) * Cell),
                        new Vector3(WallThickness, StoreyHeight, Cell), outer);
                if (r == 0)
                    kit.Box($"Wall_S_{c}", parent, new Vector3((c + 0.5f) * Cell, y + wallHeight * 0.5f - SlabThickness * 0.5f, -WallThickness * 0.5f),
                        new Vector3(Cell + WallThickness * 2f, StoreyHeight, WallThickness), outer);
                if (r == plan.Rows - 1)
                    kit.Box($"Wall_N_{c}", parent, new Vector3((c + 0.5f) * Cell, y + wallHeight * 0.5f - SlabThickness * 0.5f, plan.Rows * Cell + WallThickness * 0.5f),
                        new Vector3(Cell + WallThickness * 2f, StoreyHeight, WallThickness), outer);

                var cell = plan.Get(c, r);
                // Edges to the east and north neighbours (each interior edge once).
                foreach (bool north in new[] { false, true })
                {
                    int c2 = north ? c : c + 1, r2 = north ? r + 1 : r;
                    if (c2 >= plan.Columns || r2 >= plan.Rows)
                        continue;
                    var other = plan.Get(c2, r2);
                    EdgeLine(c, r, north, y, out Vector3 a, out Vector3 b);

                    // Solid wall under the top of a staircase.
                    if (north && cell.Feature == 'S')
                    {
                        WallBetween(parent, $"StairWall_{c}{r}", a, b, y, wallHeight, outer);
                        continue;
                    }
                    if (cell.HasFloor == other.HasFloor)
                        continue; // floor-floor is glass or open; void-void is nothing

                    // Floor next to void: railing, except across the top of a staircase arriving here.
                    bool arrival = north && !cell.HasFloor && IsStairBelow(f, c, r);
                    if (arrival)
                    {
                        float cx = (c + 0.5f) * Cell;
                        float gap = StairWidth * 0.5f + 0.05f;
                        Railing(parent, $"Railing_{c}{r}_{c2}{r2}_W", a, new Vector3(cx - gap, y, a.z), y);
                        Railing(parent, $"Railing_{c}{r}_{c2}{r2}_E", new Vector3(cx + gap, y, a.z), b, y);
                    }
                    else
                    {
                        Railing(parent, $"Railing_{c}{r}_{c2}{r2}", a, b, y);
                    }
                }
            }
        }

        /// <summary>The two ends of the edge on the east (<paramref name="north"/> false) or north side of a cell.</summary>
        static void EdgeLine(int c, int r, bool north, float y, out Vector3 a, out Vector3 b)
        {
            if (north)
            {
                a = new Vector3(c * Cell, y, (r + 1) * Cell);
                b = new Vector3((c + 1) * Cell, y, (r + 1) * Cell);
            }
            else
            {
                a = new Vector3((c + 1) * Cell, y, r * Cell);
                b = new Vector3((c + 1) * Cell, y, (r + 1) * Cell);
            }
        }

        static void WallBetween(Transform parent, string name, Vector3 a, Vector3 b, float y, float height, Material material)
        {
            Vector3 mid = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(b.x - a.x) > 0.01f;
            float length = Vector3.Distance(a, b);
            kit.Box(name, parent, new Vector3(mid.x, y + height * 0.5f, mid.z),
                alongX ? new Vector3(length, height, 0.25f) : new Vector3(0.25f, height, length), material);
        }

        static void Railing(Transform parent, string name, Vector3 a, Vector3 b, float y)
        {
            Vector3 mid = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(b.x - a.x) > 0.01f;
            float length = Vector3.Distance(a, b);
            if (length < 0.1f)
                return;
            var rail = GrayboxKit.Group(name, parent).transform;
            // One solid panel (so nothing falls through) under a wooden handrail.
            kit.Box("Panel", rail, new Vector3(mid.x, y + RailHeight * 0.5f, mid.z),
                alongX ? new Vector3(length, RailHeight, 0.06f) : new Vector3(0.06f, RailHeight, length), kit.Metal);
            kit.Box("Handrail", rail, new Vector3(mid.x, y + RailHeight, mid.z),
                alongX ? new Vector3(length, 0.07f, 0.14f) : new Vector3(0.14f, 0.07f, length), stairMaterial, false);
        }

        static bool IsStairBelow(int f, int c, int r) => f > 0 && Floors[f - 1].Get(c, r).Feature == 'S';

        // ------------------------------------------------------------------ stairs

        /// <summary>Straight flight up one storey, climbing south to north, centred in the cell.</summary>
        static void BuildStairs(int f, Transform parent)
        {
            var plan = Floors[f];
            float rise = StoreyHeight;
            float run = Cell;
            float stepRise = rise / StairSteps, stepRun = run / StairSteps;
            float pitch = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(rise * rise + run * run);
            var tilt = Quaternion.Euler(-pitch, 0f, 0f);

            for (int r = 0; r < plan.Rows; r++)
            for (int c = 0; c < plan.Columns; c++)
            {
                if (plan.Get(c, r).Feature != 'S')
                    continue;
                var stair = GrayboxKit.Group($"Stair_{c}{r}", parent).transform;
                float cx = (c + 0.5f) * Cell, z0 = r * Cell, y = plan.Y;

                // Visible steps: solid blocks, no colliders (the ramp below is what you walk on).
                for (int i = 0; i < StairSteps; i++)
                {
                    float h = (i + 1) * stepRise;
                    kit.Box($"Step_{i:00}", stair, new Vector3(cx, y + h * 0.5f, z0 + (i + 0.5f) * stepRun),
                        new Vector3(StairWidth, h, stepRun), stairMaterial, false);
                }

                // Smooth invisible ramp: easy on the CharacterController and on the NavMesh.
                Vector3 mid = new Vector3(cx, y + rise * 0.5f, z0 + run * 0.5f);
                var ramp = new GameObject("Ramp");
                ramp.transform.SetParent(stair, false);
                ramp.transform.SetPositionAndRotation(mid + tilt * new Vector3(0f, -0.1f, 0f), tilt);
                ramp.AddComponent<BoxCollider>().size = new Vector3(StairWidth, 0.2f, length);

                for (int side = -1; side <= 1; side += 2)
                {
                    var rail = kit.Box(side < 0 ? "Rail_W" : "Rail_E", stair, mid + tilt * new Vector3(side * (StairWidth * 0.5f + 0.04f), 0.5f, 0f),
                        new Vector3(0.08f, 1f, length), kit.Metal, true, tilt);
                    GameObjectUtility.SetStaticEditorFlags(rail, 0);
                }
            }
        }

        // ------------------------------------------------------------------ lamps

        static void BuildLamps(int f, Transform parent)
        {
            var plan = Floors[f];
            bool top = f == Floors.Length - 1;
            for (int r = 0; r < plan.Rows; r++)
            for (int c = 0; c < plan.Columns; c++)
            {
                var cell = plan.Get(c, r);
                if (!cell.HasFloor || cell.Feature == 'S')
                    continue;
                // A lamp needs a solid ceiling: not under glass or a void.
                if (!top)
                {
                    var above = Floors[f + 1].Get(c, r);
                    if (!above.HasFloor || above.Feature == 'g')
                        continue;
                }
                // Some rooms stay dark, picked the same way every build.
                if ((f * 31 + c * 7 + r * 13) % 4 == 0)
                    continue;
                Vector3 ceiling = CellCenter(f, c, r) + Vector3.up * (StoreyHeight - SlabThickness - 0.05f);
                kit.Box($"LampFixture_{c}{r}", parent, ceiling, new Vector3(0.5f, 0.08f, 0.5f), kit.LampWarm, false);
                var color = f == 0 ? new Color(0.85f, 0.9f, 0.65f) : new Color(1f, 0.8f, 0.58f);
                GrayboxKit.PointLight($"Lamp_{c}{r}", parent, ceiling + Vector3.down * 0.3f, color, f == 0 ? 1f : 1.3f, 7f);
            }
        }

        // ------------------------------------------------------------------ glass

        /// <summary>Sliding glass between every pair of neighbouring floor cells in different rooms; one GlassHouse per floor.</summary>
        static GlassHouse BuildGlass(int f, Transform floorRoot, int houseIndex)
        {
            var plan = Floors[f];
            var parent = GrayboxKit.Group("Glass", floorRoot).transform;
            var panes = new List<Object>();
            var posts = new HashSet<Vector2Int>();

            for (int r = 0; r < plan.Rows; r++)
            for (int c = 0; c < plan.Columns; c++)
            {
                var cell = plan.Get(c, r);
                foreach (bool north in new[] { false, true })
                {
                    int c2 = north ? c : c + 1, r2 = north ? r + 1 : r;
                    if (c2 >= plan.Columns || r2 >= plan.Rows)
                        continue;
                    var other = plan.Get(c2, r2);
                    if (!cell.HasFloor || !other.HasFloor || cell.Room == other.Room)
                        continue;
                    if (north && cell.Feature == 'S')
                        continue; // solid wall under the stair top instead
                    AddSection(parent, panes, f, c, r, north);
                    if (north)
                    {
                        posts.Add(new Vector2Int(c, r + 1));
                        posts.Add(new Vector2Int(c + 1, r + 1));
                    }
                    else
                    {
                        posts.Add(new Vector2Int(c + 1, r));
                        posts.Add(new Vector2Int(c + 1, r + 1));
                    }
                }
            }
            if (panes.Count == 0)
                return null;

            float y = plan.Y;
            var postRoot = GrayboxKit.Group("Posts", parent).transform;
            var warning = new List<Object>();
            int i = 0;
            foreach (var p in posts)
            {
                float height = StoreyHeight - SlabThickness;
                kit.Box($"Post_{p.x}_{p.y}", postRoot, new Vector3(p.x * Cell, y + height * 0.5f, p.y * Cell),
                    new Vector3(0.24f, height, 0.24f), kit.Rust);
                if (i++ % 3 == 0)
                {
                    var light = GrayboxKit.PointLight($"WarningLight_{p.x}_{p.y}", postRoot,
                        new Vector3(p.x * Cell, y + height - 0.3f, p.y * Cell), new Color(1f, 0.08f, 0.04f), 0f, 9f);
                    light.enabled = false;
                    warning.Add(light);
                }
            }

            var houseObject = new GameObject("GlassHouse");
            houseObject.transform.SetParent(floorRoot, false);
            houseObject.transform.position = new Vector3(plan.Columns * Cell * 0.5f, y + 2f, plan.Rows * Cell * 0.5f);
            // Re-parent the panes under the house object so each floor's glass is easy to find.
            foreach (GlassPane pane in panes)
                pane.transform.SetParent(houseObject.transform, true);
            var house = houseObject.AddComponent<GlassHouse>();
            SerializedFields.SetArray(house, "panes", panes);
            SerializedFields.SetArray(house, "warningLights", warning);
            SerializedFields.SetFloat(house, "firstShiftOffset", houseIndex * FloorShiftStagger);
            SerializedFields.SetBool(house, "globalBuzzer", false);
            SetString(house, "displayName", plan.Name);
            SetInt(house, "countdownRow", houseIndex);
            return house;
        }

        /// <summary>One 6 m wall section: two 3 m panes that slide over each other (see GlassHouse).</summary>
        static void AddSection(Transform parent, List<Object> panes, int f, int c, int r, bool north)
        {
            float y = Floors[f].Y + GlassHouseBuilder.PaneBase;
            Vector3 along = north ? Vector3.right : Vector3.forward;
            Vector3 normal = north ? Vector3.forward : Vector3.right;
            var rotation = Quaternion.LookRotation(normal);
            Vector3 start = north ? new Vector3(c * Cell, y, (r + 1) * Cell) : new Vector3((c + 1) * Cell, y, r * Cell);

            for (int k = 0; k < 2; k++)
            {
                Vector3 position = start + along * (Cell * 0.25f + k * Cell * 0.5f);
                float direction = k == 0 ? 1f : -1f;
                Vector3 openOffset = along * (Cell * 0.5f * direction) + normal * (GlassHouseBuilder.TrackOffset * direction);
                string name = $"Pane_{c}{r}_{(north ? "N" : "E")}_{(k == 0 ? "A" : "B")}";
                panes.Add(GlassHouseBuilder.BuildPane(name, parent, position, rotation, openOffset, false, glassMaterial));
            }

            // Floor and ceiling tracks, visual only.
            Vector3 mid = start + along * (Cell * 0.5f);
            Vector3 trackSize = north ? new Vector3(Cell, 0.012f, 0.32f) : new Vector3(0.32f, 0.012f, Cell);
            kit.Box($"Track_{c}{r}_{(north ? "N" : "E")}", parent, new Vector3(mid.x, Floors[f].Y + 0.006f, mid.z), trackSize, kit.Metal, false);
            trackSize.y = 0.08f;
            kit.Box($"TrackTop_{c}{r}_{(north ? "N" : "E")}", parent,
                new Vector3(mid.x, Floors[f].Y + StoreyHeight - SlabThickness - 0.04f, mid.z), trackSize, kit.Metal, false);
        }

        // ------------------------------------------------------------------ checks

        /// <summary>Logs every mistake in the plans: ragged rows, unknown features, staircases with nowhere to arrive.</summary>
        static bool ValidatePlans()
        {
            bool ok = true;
            for (int f = 0; f < Floors.Length; f++)
            {
                var plan = Floors[f];
                if (plan.Columns != Floors[0].Columns || plan.Rows != Floors[0].Rows)
                {
                    Debug.LogError($"[Horror Game] {plan.Name}: every floor must be {Floors[0].Columns} x {Floors[0].Rows} cells.");
                    ok = false;
                }
                foreach (string error in plan.Errors)
                {
                    Debug.LogError($"[Horror Game] {plan.Name}: {error}");
                    ok = false;
                }
                if (!ok)
                    continue;
                for (int r = 0; r < plan.Rows; r++)
                for (int c = 0; c < plan.Columns; c++)
                {
                    if (plan.Get(c, r).Feature != 'S')
                        continue;
                    if (f == Floors.Length - 1)
                    {
                        Debug.LogError($"[Horror Game] {plan.Name} cell {c},{r}: stairs on the top floor lead nowhere.");
                        ok = false;
                        continue;
                    }
                    var next = Floors[f + 1];
                    if (next.Get(c, r).HasFloor)
                    {
                        Debug.LogError($"[Horror Game] {next.Name} cell {c},{r} must be 'v' (open) above the stairs from {plan.Name}.");
                        ok = false;
                    }
                    if (r + 1 >= next.Rows || !next.Get(c, r + 1).HasFloor)
                    {
                        Debug.LogError($"[Horror Game] {next.Name} cell {c},{r + 1} must have a floor: the stairs from {plan.Name} arrive there.");
                        ok = false;
                    }
                }
            }
            return ok;
        }

        /// <summary>
        /// Bakes a NavMesh from the scene (glass is skipped by the bake, so this is "all glass open")
        /// and checks a path exists from the player's start to every room. Returns how many can't be reached.
        /// </summary>
        static int CheckEveryRoomIsReachable(Vector3 from, List<Object> rooms, Bounds bounds)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            // Same filter as the runtime bake: no players, monsters or carving obstacles (the glass).
            sources.RemoveAll(HorrorGame.AI.RuntimeNavMesh.IsMovingThing);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.4f;
            settings.agentHeight = 2.2f;
            settings.agentClimb = 0.45f;
            settings.agentSlope = 40f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("[Horror Game] Glass mansion: NavMesh bake failed, can't check the rooms are reachable.");
                return rooms.Count;
            }
            Debug.Log($"[Horror Game] Reachability bake: {sources.Count} collider sources in {bounds}.");
            var instance = NavMesh.AddNavMeshData(data);
            int unreachable = 0;
            try
            {
                if (!NavMesh.SamplePosition(from, out var start, 2f, NavMesh.AllAreas))
                {
                    Debug.LogError("[Horror Game] Glass mansion: the player's start is not on the NavMesh.");
                    return rooms.Count;
                }
                var path = new NavMeshPath();
                foreach (Transform room in rooms)
                {
                    bool reached = NavMesh.SamplePosition(room.position, out var end, 2f, NavMesh.AllAreas) &&
                                   NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                                   path.status == NavMeshPathStatus.PathComplete;
                    if (!reached)
                    {
                        unreachable++;
                        string lastCorner = path.corners.Length > 0 ? path.corners[path.corners.Length - 1].ToString() : "none";
                        Debug.LogError($"[Horror Game] Glass mansion: can't walk from the start to {room.name} " +
                                       $"(status {path.status}, {path.corners.Length} corners, closest point reached {lastCorner}).");
                        if (room.name.Contains("_H"))
                            LogWhyBlocked(start.position, room.position, path);
                    }
                }
            }
            finally
            {
                instance.Remove();
                Object.DestroyImmediate(data);
            }
            return unreachable;
        }

        /// <summary>Diagnostics for the first unreachable room: where the path gives up and what solid things are there.</summary>
        static void LogWhyBlocked(Vector3 from, Vector3 to, NavMeshPath path)
        {
            var log = new System.Text.StringBuilder();
            log.Append($"[Horror Game] Path check from {from} to {to}: status {path.status}, {path.corners.Length} corners");
            if (path.corners.Length > 0)
            {
                Vector3 end = path.corners[path.corners.Length - 1];
                log.Append($", gives up at {end}. Colliders within 1.5 m of there:");
                foreach (var c in Physics.OverlapSphere(end + Vector3.up, 1.5f, Physics.AllLayers, QueryTriggerInteraction.Collide))
                    log.Append($"\n  {c.name} (parent {(c.transform.parent != null ? c.transform.parent.name : "-")}, layer {c.gameObject.layer}, {c.GetType().Name}, bounds {c.bounds})");
            }
            // Everything solid in the K/H doorway on the ground floor (x = 12, z 0..6).
            log.Append("\n  Colliders in the ground-floor doorway at x = 12, z 0..6:");
            foreach (var c in Physics.OverlapBox(new Vector3(12f, 1.1f, 3f), new Vector3(0.5f, 1f, 3f), Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Collide))
                log.Append($"\n    {c.name} (parent {(c.transform.parent != null ? c.transform.parent.name : "-")}, layer {c.gameObject.layer}, {c.GetType().Name}, bounds {c.bounds})");
            if (NavMesh.SamplePosition(new Vector3(9f, 0f, 3f), out var west, 1f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(new Vector3(15f, 0f, 3f), out var east, 1f, NavMesh.AllAreas))
            {
                var across = new NavMeshPath();
                NavMesh.CalculatePath(west.position, east.position, NavMesh.AllAreas, across);
                log.Append($"\n  Path straight across that doorway {west.position} -> {east.position}: {across.status}, {across.corners.Length} corners.");
                bool hit = NavMesh.Raycast(west.position, east.position, out var edge, NavMesh.AllAreas);
                log.Append($" NavMesh.Raycast blocked: {hit} at {edge.position}.");
            }
            else
            {
                log.Append("\n  No NavMesh within 1 m of (9, 0, 3) or (15, 0, 3).");
            }
            var tri = NavMesh.CalculateTriangulation();
            log.Append($"\n  NavMesh has {tri.vertices.Length} vertices, {tri.indices.Length / 3} triangles.");
            Debug.Log(log.ToString());
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 CellCenter(int f, int c, int r) => new Vector3((c + 0.5f) * Cell, Floors[f].Y, (r + 0.5f) * Cell);

        static Bounds NavBounds()
        {
            var plan = Floors[0];
            float bottom = Floors[0].Y - 1f;
            float top = Floors[Floors.Length - 1].Y + StoreyHeight - SlabThickness - 0.2f; // below the roof
            return new Bounds(new Vector3(plan.Columns * Cell * 0.5f, (bottom + top) * 0.5f, plan.Rows * Cell * 0.5f),
                new Vector3(plan.Columns * Cell + 1f, top - bottom, plan.Rows * Cell + 1f));
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        readonly struct PlanCell
        {
            public readonly char Room;
            public readonly char Feature;

            public PlanCell(char room, char feature)
            {
                Room = room;
                Feature = feature;
            }

            public bool HasFloor => Feature != 'v';
        }

        sealed class FloorPlan
        {
            public readonly string Name;
            public readonly float Y;
            public readonly int Columns;
            public readonly int Rows;
            public readonly List<string> Errors = new List<string>();
            readonly PlanCell[,] cells;

            const string Features = "_gvSLPF";

            /// <param name="northFirst">Rows as written in the plan, north row first.</param>
            public FloorPlan(string name, float y, string[] northFirst)
            {
                Name = name;
                Y = y;
                Rows = northFirst.Length;
                Columns = Rows > 0 ? northFirst[0].Split(' ').Length : 0;
                cells = new PlanCell[Columns, Rows];
                for (int i = 0; i < Rows; i++)
                {
                    int r = Rows - 1 - i; // row 0 is the south row
                    var tokens = northFirst[i].Split(' ');
                    if (tokens.Length != Columns)
                    {
                        Errors.Add($"row {i + 1} from the north has {tokens.Length} cells, expected {Columns}.");
                        continue;
                    }
                    for (int c = 0; c < Columns; c++)
                    {
                        string t = tokens[c];
                        if (t.Length != 2 || Features.IndexOf(t[1]) < 0)
                        {
                            Errors.Add($"cell '{t}' (row {i + 1} from the north, column {c + 1}) is not a room letter plus one of {Features}.");
                            continue;
                        }
                        cells[c, r] = new PlanCell(t[0], t[1]);
                    }
                }
            }

            public PlanCell Get(int c, int r) => cells[c, r];
        }
    }
}
