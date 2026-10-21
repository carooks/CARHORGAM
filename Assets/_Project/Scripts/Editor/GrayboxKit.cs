using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Shared placeholder materials and primitive helpers for building graybox geometry in code.
    /// Materials are created once under Assets/_Project/Materials and never overwritten, so
    /// tweaks made by hand (or by the art pass) survive a scene rebuild.
    /// </summary>
    public class GrayboxKit
    {
        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        readonly Shader litShader;
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public Material Ground { get; }
        public Material Asphalt { get; }
        public Material RoadLine { get; }
        public Material Concrete { get; }
        public Material ConcreteFloor { get; }
        public Material Wall { get; }
        public Material Ceiling { get; }
        public Material Metal { get; }
        public Material Rust { get; }
        public Material Crate { get; }
        public Material Cardboard { get; }
        public Material Hazard { get; }
        public Material Water { get; }
        public Material HouseWall { get; }
        public Material Roof { get; }
        public Material Bark { get; }
        public Material Foliage { get; }
        public Material LampWarm { get; }
        public Material LampSodium { get; }
        public Material WindowLit { get; }
        public Material ExtractionGlow { get; }
        public Material VanPaint { get; }

        public GrayboxKit()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogError("[Horror Game] URP Lit shader not found. Is the Universal RP package installed?");
                litShader = Shader.Find("Standard");
            }
            AssetFolders.Ensure(ProjectPaths.Materials);

            Ground = Get("M_Ground", new Color(0.13f, 0.16f, 0.11f), 0.05f);
            Asphalt = Get("M_Asphalt", new Color(0.09f, 0.09f, 0.1f), 0.25f);
            RoadLine = Get("M_RoadLine", new Color(0.75f, 0.6f, 0.15f), 0.2f);
            Concrete = Get("M_Concrete", new Color(0.42f, 0.42f, 0.4f), 0.1f);
            ConcreteFloor = Get("M_ConcreteFloor", new Color(0.3f, 0.3f, 0.29f), 0.3f);
            Wall = Get("M_FacilityWall", new Color(0.38f, 0.4f, 0.37f), 0.15f);
            Ceiling = Get("M_Ceiling", new Color(0.2f, 0.2f, 0.2f), 0.05f);
            Metal = Get("M_Metal", new Color(0.32f, 0.35f, 0.38f), 0.45f, 0.7f);
            Rust = Get("M_Rust", new Color(0.4f, 0.2f, 0.1f), 0.2f, 0.3f);
            Crate = Get("M_Crate", new Color(0.36f, 0.27f, 0.16f), 0.1f);
            Cardboard = Get("M_Cardboard", new Color(0.55f, 0.43f, 0.28f), 0.05f);
            Hazard = Get("M_Hazard", new Color(0.8f, 0.6f, 0.08f), 0.3f);
            Water = Get("M_PondWater", new Color(0.03f, 0.06f, 0.08f), 0.92f);
            HouseWall = Get("M_HouseSiding", new Color(0.45f, 0.43f, 0.4f), 0.1f);
            Roof = Get("M_Roof", new Color(0.18f, 0.14f, 0.13f), 0.15f);
            Bark = Get("M_Bark", new Color(0.2f, 0.14f, 0.1f), 0.05f);
            Foliage = Get("M_Foliage", new Color(0.07f, 0.13f, 0.08f), 0.05f);
            LampWarm = Get("M_LampWarm", new Color(1f, 0.9f, 0.7f), 0.5f, 0f, new Color(1f, 0.82f, 0.55f) * 3f);
            LampSodium = Get("M_LampSodium", new Color(1f, 0.75f, 0.45f), 0.5f, 0f, new Color(1f, 0.6f, 0.25f) * 4f);
            WindowLit = Get("M_WindowLit", new Color(0.9f, 0.75f, 0.45f), 0.6f, 0f, new Color(1f, 0.7f, 0.35f) * 1.2f);
            ExtractionGlow = Get("M_ExtractionGlow", new Color(0.2f, 0.9f, 0.4f), 0.2f, 0f, new Color(0.2f, 1f, 0.4f) * 1.5f);
            VanPaint = Get("M_VanPaint", new Color(0.55f, 0.55f, 0.52f), 0.5f, 0.2f);
        }

        Material Get(string name, Color color, float smoothness, float metallic = 0f, Color? emission = null)
        {
            if (materials.TryGetValue(name, out var cached))
                return cached;

            string path = $"{ProjectPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(litShader) { name = name };
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
            }
            materials[name] = material;
            return material;
        }

        public static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = true, Quaternion? rotation = null)
        {
            return Primitive(PrimitiveType.Cube, name, parent, center, size, material, collider, rotation);
        }

        /// <summary>Cylinder whose <paramref name="size"/> is (diameter, height, diameter).</summary>
        public GameObject Cylinder(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = true)
        {
            return Primitive(PrimitiveType.Cylinder, name, parent, center,
                new Vector3(size.x, size.y * 0.5f, size.z), material, collider, null);
        }

        GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 center, Vector3 scale,
            Material material, bool collider, Quaternion? rotation)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!collider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            return go;
        }

        /// <summary>Box with a collider but no visible mesh, for blocking the player at map edges.</summary>
        public static GameObject InvisibleWall(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        public static Light PointLight(string name, Transform parent, Vector3 position, Color color, float intensity,
            float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        public static Light SpotLight(string name, Transform parent, Vector3 position, Quaternion rotation, Color color,
            float intensity, float range, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.5f;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Low-poly cone mesh (used for pine trees), saved once as an asset.</summary>
        public static Mesh ConeMesh()
        {
            string path = ProjectPaths.Meshes + "/LowPolyCone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;

            AssetFolders.Ensure(ProjectPaths.Meshes);
            const int sides = 7;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides;
                float a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);

                // Separate vertices per face give the flat-shaded low-poly look.
                int s = vertices.Count;
                vertices.Add(p0); vertices.Add(apex); vertices.Add(p1);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);

                int b = vertices.Count;
                vertices.Add(Vector3.zero); vertices.Add(p0); vertices.Add(p1);
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
            }
            mesh = new Mesh { name = "LowPolyCone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
