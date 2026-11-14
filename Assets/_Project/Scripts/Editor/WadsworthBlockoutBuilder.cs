using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Builds a separate scene with the real-scale (1 unit = 1 m) Wadsworth blockout: streets and
    /// extruded building footprints from map data, around the match factory.
    ///
    /// The geometry comes from Assets/_Project/Data/Wadsworth/wadsworth_blockout.json, which
    /// Tools/CityBlockout/build_blockout.py writes (already triangulated, one entry per building).
    /// To change the area or the data, rerun that script, then Horror Game > Build Wadsworth Blockout Scene.
    ///
    /// Coordinates: origin = Route 94 in front of the match factory, +X east, +Z north.
    /// This does not touch the Phase 1 scene.
    /// </summary>
    public static class WadsworthBlockoutBuilder
    {
        // Shapes of the JSON written by build_blockout.py (field names must match its keys).
        [Serializable]
        class BlockoutData
        {
            public string attribution;
            public float xMin, xMax, zMin, zMax;
            public BlockoutObject[] objects;
            public BlockoutLamp[] lamps;
        }

        [Serializable]
        class BlockoutLamp
        {
            public float x, z;   // pole base
            public float ax, az; // arm direction, towards the road
            public bool on;      // dead lamps leave dark stretches
        }

        [Serializable]
        class BlockoutObject
        {
            public string name;
            public BlockoutPart[] parts;
        }

        [Serializable]
        class BlockoutPart
        {
            public string material;
            public float[] v; // x,y,z per vertex, three vertices per triangle, already wound for Unity
        }

        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        // On Route 94, just east of the centre line, looking west at the factory drive.
        static readonly Vector3 PlayerSpawn = new Vector3(3f, 0.1f, 0f);

        [MenuItem("Horror Game/Build Wadsworth Blockout Scene", priority = 20)]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (File.Exists(ProjectPaths.WadsworthScene) && !EditorUtility.DisplayDialog("Rebuild Wadsworth blockout?",
                    "This regenerates " + ProjectPaths.WadsworthScene + " from the map data. Manual edits to that scene will be lost.",
                    "Rebuild", "Cancel"))
                return;
            Build();
        }

        [MenuItem("Horror Game/Open Wadsworth Blockout Scene", priority = 21)]
        static void OpenFromMenu()
        {
            if (!File.Exists(ProjectPaths.WadsworthScene))
            {
                Debug.LogWarning("[Horror Game] No Wadsworth blockout scene yet. Use Horror Game > Build Wadsworth Blockout Scene.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ProjectPaths.WadsworthScene);
        }

        public static void Build()
        {
            if (!File.Exists(ProjectPaths.WadsworthData))
            {
                Debug.LogError("[Horror Game] Missing " + ProjectPaths.WadsworthData +
                               ". Run Tools/CityBlockout/build_blockout.py to generate it.");
                return;
            }
            var data = JsonUtility.FromJson<BlockoutData>(File.ReadAllText(ProjectPaths.WadsworthData));
            if (data?.objects == null || data.objects.Length == 0)
            {
                Debug.LogError("[Horror Game] " + ProjectPaths.WadsworthData + " has no objects.");
                return;
            }

            AssetFolders.Ensure(ProjectPaths.Scenes);
            AssetFolders.Ensure(ProjectPaths.Meshes);
            RenderPipelineSetup.EnsureUrp();
            var kit = new GrayboxKit();
            var postFx = RenderPipelineSetup.EnsurePostFxProfile();
            var playerPrefab = PlayerPrefabBuilder.EnsurePrefab();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = GrayboxKit.Group("Wadsworth", null).transform;
            var groups = new Dictionary<string, Transform>
            {
                { "Ground", GrayboxKit.Group("Ground", root).transform },
                { "Roads", GrayboxKit.Group("Roads", root).transform },
                { "Lots", GrayboxKit.Group("Lots", root).transform },
                { "Buildings", GrayboxKit.Group("Buildings", root).transform },
            };

            // All meshes go into one asset file so the scene can reference them after it is saved.
            if (File.Exists(ProjectPaths.WadsworthMeshes))
                AssetDatabase.DeleteAsset(ProjectPaths.WadsworthMeshes);
            bool meshAssetCreated = false;

            int buildingCount = 0;
            foreach (var obj in data.objects)
            {
                var materials = new Material[obj.parts.Length];
                for (int i = 0; i < obj.parts.Length; i++)
                    materials[i] = MaterialFor(kit, obj.parts[i].material);

                var mesh = BuildMesh(obj);
                if (!meshAssetCreated)
                {
                    AssetDatabase.CreateAsset(mesh, ProjectPaths.WadsworthMeshes);
                    meshAssetCreated = true;
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, ProjectPaths.WadsworthMeshes);
                }

                string group = GroupFor(obj.name);
                var go = new GameObject(obj.name);
                go.transform.SetParent(groups[group], false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = materials;
                GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);

                // Only the ground and buildings block the player; roads and lots sit a few cm above
                // the ground and are walked over on the ground collider.
                if (group == "Ground" || group == "Buildings")
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (group == "Buildings")
                    buildingCount++;
            }
            AssetDatabase.SaveAssets();

            int lampCount = CreateStreetLamps(root, kit, data);
            CreateMapEdges(root, data);
            Phase1SceneBuilder.ConfigureAtmosphere();
            // Lighter fog than the Phase 1 scene so the street layout can be judged at a distance.
            RenderSettings.fogDensity = 0.02f;
            Phase1SceneBuilder.CreateMoonlight();
            Phase1SceneBuilder.CreatePostProcessVolume(postFx);
            Phase1SceneBuilder.CreateAmbience();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, SceneManager.GetActiveScene());
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.LookRotation(Vector3.left, Vector3.up));

            EditorSceneManager.SaveScene(scene, ProjectPaths.WadsworthScene);
            Debug.Log($"[Horror Game] Built {ProjectPaths.WadsworthScene}: {buildingCount} buildings, " +
                      $"{data.objects.Length - buildingCount} ground/road objects, {lampCount} street lamps. {data.attribution}");
        }

        static Mesh BuildMesh(BlockoutObject obj)
        {
            int vertexCount = 0;
            foreach (var part in obj.parts)
                vertexCount += part.v.Length / 3;

            var vertices = new List<Vector3>(vertexCount);
            foreach (var part in obj.parts)
                for (int i = 0; i + 2 < part.v.Length; i += 3)
                    vertices.Add(new Vector3(part.v[i], part.v[i + 1], part.v[i + 2]));

            var mesh = new Mesh
            {
                name = obj.name,
                indexFormat = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = obj.parts.Length;
            int start = 0;
            for (int p = 0; p < obj.parts.Length; p++)
            {
                int count = obj.parts[p].v.Length / 3;
                var indices = new int[count];
                for (int i = 0; i < count; i++)
                    indices[i] = start + i;
                mesh.SetTriangles(indices, p);
                start += count;
            }
            // Every triangle has its own vertices, so recalculated normals are flat (low-poly look).
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static string GroupFor(string objectName)
        {
            if (objectName.StartsWith("Bldg_", StringComparison.Ordinal))
                return "Buildings";
            if (objectName.StartsWith("Road", StringComparison.Ordinal) || objectName == "Intersections")
                return "Roads";
            if (objectName == "Ground")
                return "Ground";
            return "Lots";
        }

        static Material MaterialFor(GrayboxKit kit, string name)
        {
            switch (name)
            {
                case "Ground": return kit.Ground;
                case "RoadMain":
                case "RoadStreet": return kit.Asphalt;
                case "RoadService":
                case "Parking": return kit.ConcreteFloor;
                case "Water": return kit.Water;
                case "Building": return kit.HouseWall;
                case "BuildingLandmark": return kit.Wall;
                case "Roof": return kit.Roof;
                default: return kit.Concrete;
            }
        }

        static int CreateStreetLamps(Transform root, GrayboxKit kit, BlockoutData data)
        {
            if (data.lamps == null || data.lamps.Length == 0)
            {
                Debug.LogWarning("[Horror Game] No street lamps in the blockout data. Rerun Tools/CityBlockout/build_blockout.py.");
                return 0;
            }
            var group = GrayboxKit.Group("StreetLamps", root).transform;
            for (int i = 0; i < data.lamps.Length; i++)
            {
                var lamp = data.lamps[i];
                kit.StreetLamp($"Lamp_{i:000}" + (lamp.on ? "" : "_Dead"), group,
                    new Vector3(lamp.x, 0f, lamp.z), new Vector3(lamp.ax, 0f, lamp.az), lamp.on);
            }
            return data.lamps.Length;
        }

        static void CreateMapEdges(Transform root, BlockoutData data)
        {
            var edges = GrayboxKit.Group("MapEdges", root).transform;
            float width = data.xMax - data.xMin;
            float depth = data.zMax - data.zMin;
            float cx = (data.xMin + data.xMax) * 0.5f;
            float cz = (data.zMin + data.zMax) * 0.5f;
            const float height = 10f, thickness = 1f;
            GrayboxKit.InvisibleWall("Edge_North", edges, new Vector3(cx, height * 0.5f, data.zMax), new Vector3(width, height, thickness));
            GrayboxKit.InvisibleWall("Edge_South", edges, new Vector3(cx, height * 0.5f, data.zMin), new Vector3(width, height, thickness));
            GrayboxKit.InvisibleWall("Edge_East", edges, new Vector3(data.xMax, height * 0.5f, cz), new Vector3(thickness, height, depth));
            GrayboxKit.InvisibleWall("Edge_West", edges, new Vector3(data.xMin, height * 0.5f, cz), new Vector3(thickness, height, depth));
        }
    }
}
