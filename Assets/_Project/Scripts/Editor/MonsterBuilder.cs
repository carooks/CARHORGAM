using System.Collections.Generic;
using HorrorGame.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Puts the Foreman (the match factory's monster) into the scene: a graybox body, the AI brain,
    /// its patrol route through the facility, and the runtime NavMesh region it walks on.
    ///
    /// Patrol points are picked automatically from open floor cells in <see cref="FacilityBuilder.Layout"/>,
    /// spread as far apart as possible, so editing the layout and rebuilding keeps the route sensible.
    /// Use Horror Game > Add or Replace Foreman in Open Scene to add him without rebuilding the whole scene.
    /// </summary>
    public static class MonsterBuilder
    {
        public const string MonsterName = "Foreman";
        public const string PatrolName = "Foreman_Patrol";
        public const string NavMeshName = "NavMeshArea_Facility";
        const int PatrolPointCount = 8;
        /// <summary>How far the walkable area reaches outside the facility walls, so he can follow you out the door a little.</summary>
        const float OutdoorApron = 8f;

        [MenuItem("Horror Game/Add or Replace Foreman in Open Scene", priority = 20)]
        static void AddToOpenSceneMenu()
        {
            var scene = SceneManager.GetActiveScene();
            AddToScene(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = GameObject.Find(MonsterName);
            Debug.Log("[Horror Game] Added the Foreman to " + scene.path + " and saved it. Press Play; F3 shows what he's thinking.");
        }

        /// <summary>For Remote Control / command-line testing: adds the Foreman to the Phase 1 scene and saves it.</summary>
        public static void AddToPhase1SceneBatch()
        {
            var scene = EditorSceneManager.OpenScene(ProjectPaths.Phase1Scene);
            AddToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Foreman added to " + ProjectPaths.Phase1Scene);
        }

        static void AddToScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == MonsterName || root.name == PatrolName || root.name == NavMeshName)
                    Object.DestroyImmediate(root);
            PlayerPrefabBuilder.EnsurePrefab(); // adds health, footsteps etc. to the player
            Build(new GrayboxKit(), TownBlockoutBuilder.FacilityOrigin);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // ------------------------------------------------------------------ match factory interior

        /// <summary>Walkable box for the factory scene: yard, hall, dip room, stairwell and the raised office (see Docs/MatchFactoryInterior.md).</summary>
        static readonly Bounds FactoryBounds = new Bounds(new Vector3(29.5f, 4f, 4f), new Vector3(70f, 12f, 72f));
        /// <summary>Where the player starts in the factory scene; the Foreman spawns at the ground waypoint farthest from it.</summary>
        static readonly Vector3 FactoryPlayerSpawn = new Vector3(7f, 0f, -22f);

        [MenuItem("Horror Game/Add or Replace Foreman in Match Factory", priority = 22)]
        static void AddToFactoryMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            AddToFactorySceneBatch();
            EditorSceneManager.OpenScene(FactoryInteriorBuilder.ScenePath);
            Selection.activeGameObject = GameObject.Find(MonsterName);
        }

        /// <summary>Batch entry point: opens the match factory scene, adds the Foreman on its AI_Waypoints route, and saves it.</summary>
        public static void AddToFactorySceneBatch()
        {
            var scene = EditorSceneManager.OpenScene(FactoryInteriorBuilder.ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == MonsterName || root.name == NavMeshName)
                    Object.DestroyImmediate(root);
            PlayerPrefabBuilder.EnsurePrefab();
            BuildInFactory(new GrayboxKit());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Horror Game] Foreman added to " + FactoryInteriorBuilder.ScenePath);
        }

        /// <summary>Adds the NavMesh region and the Foreman to the open factory scene, patrolling its AI_Waypoints markers.</summary>
        public static GameObject BuildInFactory(GrayboxKit kit)
        {
            var waypointRoot = GameObject.Find("AI_Waypoints");
            var points = new List<Object>();
            if (waypointRoot != null)
                foreach (Transform child in waypointRoot.transform)
                    points.Add(child);
            if (points.Count == 0)
                Debug.LogWarning("[Horror Game] No AI_Waypoints in the factory scene; the Foreman will stand still.");

            BuildNavMesh(FactoryBounds.center, FactoryBounds.size);

            // Spawn deep inside, at the ground-level waypoint farthest from the player's start.
            Vector3 spawn = FactoryBounds.center;
            float farthest = -1f;
            foreach (Transform point in points)
            {
                if (point.position.y > 0.5f)
                    continue;
                float d = Vector3.Distance(point.position, FactoryPlayerSpawn);
                if (d > farthest)
                {
                    farthest = d;
                    spawn = point.position;
                }
            }

            var monster = BuildBody(kit, spawn, out var eyes);
            SerializedFields.SetArray(monster.GetComponent<MonsterBrain>(), "patrolPoints", points);
            SerializedFields.SetArray(monster.GetComponent<MonsterPresentation>(), "eyes", eyes);
            return monster;
        }

        static void BuildNavMesh(Vector3 center, Vector3 size)
        {
            var navMeshObject = new GameObject(NavMeshName);
            navMeshObject.transform.position = center;
            navMeshObject.AddComponent<RuntimeNavMesh>().Size = size;
        }

        // ------------------------------------------------------------------ Phase 1 facility

        /// <summary>Builds the NavMesh region, patrol route and Foreman for a facility whose south-west corner is at <paramref name="facilityOrigin"/>.</summary>
        public static GameObject Build(GrayboxKit kit, Vector3 facilityOrigin)
        {
            BuildNavMesh(facilityOrigin + new Vector3(FacilityBuilder.Width * 0.5f, 4f, FacilityBuilder.Depth * 0.5f),
                new Vector3(FacilityBuilder.Width + OutdoorApron * 2f, 12f, FacilityBuilder.Depth + OutdoorApron * 2f));

            var route = GrayboxKit.Group(PatrolName, null).transform;
            var points = new List<Object>();
            var cells = PickPatrolCells(PatrolPointCount);
            for (int i = 0; i < cells.Count; i++)
            {
                var point = new GameObject($"Point_{i:00}").transform;
                point.SetParent(route, false);
                point.position = FacilityBuilder.CellCenter(facilityOrigin, cells[i].row, cells[i].column);
                points.Add(point);
            }

            Vector3 spawn = points.Count > 0
                ? ((Transform)points[0]).position
                : FacilityBuilder.EntrancePosition(facilityOrigin) + Vector3.forward * 6f;
            var monster = BuildBody(kit, spawn, out var eyes);

            var brain = monster.GetComponent<MonsterBrain>();
            SerializedFields.SetArray(brain, "patrolPoints", points);
            SerializedFields.SetArray(monster.GetComponent<MonsterPresentation>(), "eyes", eyes);
            return monster;
        }

        static GameObject BuildBody(GrayboxKit kit, Vector3 position, out List<Object> eyes)
        {
            var skin = kit.Get("M_MonsterSkin", new Color(0.07f, 0.065f, 0.06f), 0.15f);
            var face = kit.Get("M_MonsterFace", new Color(0.5f, 0.46f, 0.38f), 0.35f);
            var eyeMaterial = kit.Get("M_MonsterEyes", new Color(1f, 0.6f, 0.2f), 0.8f, 0f, new Color(1f, 0.55f, 0.15f));

            var root = new GameObject(MonsterName);
            root.transform.position = position;

            // One collider for the whole body: the player bumps into him and can't interact through him.
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 2.4f;
            capsule.center = new Vector3(0f, 1.2f, 0f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false; // the brain turns it on once the NavMesh has been baked
            root.AddComponent<MonsterBrain>();
            root.AddComponent<MonsterPresentation>();

            // Graybox body: too tall, hunched, arms hanging past the knees, still wearing his hard hat.
            var body = GrayboxKit.Group("Body", root.transform).transform;
            body.localPosition = Vector3.zero;
            Part(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 1.15f, 0f), new Vector3(0.8f, 1.05f, 0.55f), skin,
                Quaternion.Euler(8f, 0f, 0f));
            Part(PrimitiveType.Cube, "Head", body, new Vector3(0f, 2.12f, 0.22f), new Vector3(0.42f, 0.5f, 0.42f), face,
                Quaternion.Euler(15f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "HatBrim", body, new Vector3(0f, 2.42f, 0.24f), new Vector3(0.62f, 0.03f, 0.62f),
                kit.Hazard, Quaternion.Euler(15f, 0f, 0f));
            Part(PrimitiveType.Sphere, "HatDome", body, new Vector3(0f, 2.45f, 0.22f), new Vector3(0.48f, 0.3f, 0.5f),
                kit.Hazard, Quaternion.Euler(15f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cube, side < 0 ? "ArmLeft" : "ArmRight", body, new Vector3(side * 0.5f, 1.15f, 0.12f),
                    new Vector3(0.15f, 1.45f, 0.15f), skin, Quaternion.Euler(-8f, 0f, side * -6f));
                Part(PrimitiveType.Capsule, side < 0 ? "LegLeft" : "LegRight", body, new Vector3(side * 0.2f, 0.4f, 0f),
                    new Vector3(0.22f, 0.42f, 0.22f), skin, Quaternion.identity);
            }

            eyes = new List<Object>();
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Part(PrimitiveType.Sphere, side < 0 ? "EyeLeft" : "EyeRight", body,
                    new Vector3(side * 0.1f, 2.18f, 0.44f), Vector3.one * 0.07f, eyeMaterial, Quaternion.identity);
                eyes.Add(eye.GetComponent<Renderer>());
            }
            return root;
        }

        /// <summary>A visual-only primitive: no collider, and not static, because the monster moves.</summary>
        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale,
            Material material, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>
        /// Picks open floor cells spread across the facility: starts from the cell farthest from the
        /// entrance (where he spawns), then repeatedly adds the cell farthest from all cells chosen so far.
        /// </summary>
        static List<(int row, int column)> PickPatrolCells(int count)
        {
            var layout = FacilityBuilder.Layout;
            var open = new List<(int row, int column)>();
            for (int r = 0; r < layout.Length; r++)
            for (int c = 0; c < layout[r].Length; c++)
                if (IsOpenFloor(layout, r, c) && IsClearAround(layout, r, c))
                    open.Add((r, c));

            var chosen = new List<(int row, int column)>();
            if (open.Count == 0)
            {
                Debug.LogWarning("[Horror Game] No open floor cells found for the Foreman's patrol route.");
                return chosen;
            }

            var entrance = FindCell(layout, 'O');
            var first = open[0];
            float best = -1f;
            foreach (var cell in open)
            {
                float d = CellDistance(cell, entrance);
                if (d > best)
                {
                    best = d;
                    first = cell;
                }
            }
            chosen.Add(first);

            while (chosen.Count < count && chosen.Count < open.Count)
            {
                (int row, int column) next = open[0];
                float farthest = -1f;
                foreach (var cell in open)
                {
                    float nearest = float.MaxValue;
                    foreach (var picked in chosen)
                        nearest = Mathf.Min(nearest, CellDistance(cell, picked));
                    if (nearest > farthest)
                    {
                        farthest = nearest;
                        next = cell;
                    }
                }
                chosen.Add(next);
            }
            return chosen;
        }

        static bool IsOpenFloor(string[] layout, int r, int c)
        {
            if (r < 0 || r >= layout.Length || c < 0 || c >= layout[r].Length)
                return false;
            char cell = layout[r][c];
            return cell == '.' || cell == 'L' || cell == 'l';
        }

        /// <summary>Open floor whose four neighbours are not props or walls, so the point isn't wedged between shelves.</summary>
        static bool IsClearAround(string[] layout, int r, int c)
        {
            int openNeighbours = 0;
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                if (IsOpenFloor(layout, r + dr, c + dc))
                    openNeighbours++;
            return openNeighbours >= 3;
        }

        static (int row, int column) FindCell(string[] layout, char wanted)
        {
            for (int r = 0; r < layout.Length; r++)
            for (int c = 0; c < layout[r].Length; c++)
                if (layout[r][c] == wanted)
                    return (r, c);
            return (layout.Length - 1, layout[0].Length / 2);
        }

        static float CellDistance((int row, int column) a, (int row, int column) b)
        {
            float dr = a.row - b.row, dc = a.column - b.column;
            return Mathf.Sqrt(dr * dr + dc * dc);
        }
    }
}
