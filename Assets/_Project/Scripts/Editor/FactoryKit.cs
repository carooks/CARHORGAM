using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    // Materials and geometry helpers shared by the factory builder's parts.
    public static partial class FactoryInteriorBuilder
    {
        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        const string PrismMeshPath = ProjectPaths.Meshes + "/UnitPrism.asset";
        const string ConeMeshPath = ProjectPaths.Meshes + "/LightCone.asset";
        const string HazeTexturePath = ProjectPaths.Materials + "/T_Factory_HazeGradient.asset";

        static GrayboxKit kit;
        static System.Random rng;

        // Factory-only materials (created once under Assets/_Project/Materials, never overwritten).
        static Material brick, brickDark, timber, roofBoards, soot, iron, enamel, bulb, bulbDead, matchWood,
            paper, dipLiquid, debris, plank, pilotRed, rubber, glassDark, stone, hazeWarm, hazeMoon, weed, asphaltCrack;

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
            glassDark = Mat("M_Factory_GlassDark", new Color(0.04f, 0.05f, 0.06f), 0.95f);
            stone = Mat("M_Factory_Stone", new Color(0.45f, 0.42f, 0.38f), 0.1f);
            weed = Mat("M_Factory_Weed", new Color(0.12f, 0.16f, 0.08f), 0.05f);
            asphaltCrack = Mat("M_Factory_Crack", new Color(0.03f, 0.03f, 0.03f), 0.05f);
            hazeWarm = HazeMat("M_Factory_HazeWarm", new Color(1f, 0.75f, 0.45f, 0.10f));
            hazeMoon = HazeMat("M_Factory_HazeMoon", new Color(0.55f, 0.65f, 1f, 0.08f));
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

        /// <summary>
        /// Additive, unlit, double-sided material for fake light shafts ("haze"). The gradient texture
        /// fades it from bright at the lamp to nothing at the floor.
        /// </summary>
        static Material HazeMat(string name, Color color)
        {
            string path = $"{ProjectPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = name };
            material.SetTexture("_BaseMap", HazeGradient());
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f); // additive
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Texture2D HazeGradient()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HazeTexturePath);
            if (texture != null)
                return texture;
            texture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "T_Factory_HazeGradient" };
            for (int y = 0; y < 64; y++)
            {
                float v = y / 63f;                 // 0 at the floor end, 1 at the lamp
                float a = Mathf.Pow(v, 1.6f);
                for (int x = 0; x < 4; x++)
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            texture.Apply();
            AssetDatabase.CreateAsset(texture, HazeTexturePath);
            return texture;
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

        /// <summary>Removes the visible mesh from a primitive, leaving only its collider.</summary>
        static GameObject ColliderOnly(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            return go;
        }

        static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        static bool Chance(double p) => rng.NextDouble() < p;

        struct Opening
        {
            public float Center, Width, Bottom, Top;
            public bool Boarded, Blocked, Glazed;

            /// <summary>A walk-through gap from the floor.</summary>
            public static Opening Door(float center, float width, float top) =>
                new Opening { Center = center, Width = width, Bottom = 0f, Top = top };

            /// <summary>A window: the player can't climb through, and it may be boarded up.</summary>
            public static Opening Window(float center, float width, float bottom, float top, bool boarded = false) =>
                new Opening { Center = center, Width = width, Bottom = bottom, Top = top, Boarded = boarded, Blocked = true };

            /// <summary>A window with dark glass in it, for rooms the player never enters.</summary>
            public static Opening Blind(float center, float width, float bottom, float top) =>
                new Opening { Center = center, Width = width, Bottom = bottom, Top = top, Blocked = true, Glazed = true };
        }

        /// <summary>
        /// A straight brick wall from <paramref name="a"/> to <paramref name="b"/> (on the wall's centre
        /// line, at the wall's base height) with holes cut for doors and windows. Opening centres are
        /// measured in metres from <paramref name="a"/>. Windows get a stepped brick arch and a stone sill.
        /// </summary>
        static void Wall(string name, Transform parent, Vector3 a, Vector3 b, float height, Material material,
            params Opening[] openings)
        {
            Vector3 dir = (b - a).normalized;
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
                Vector3 mid = a + dir * (o.Center + shift) + Vector3.up * ((o.Bottom + o.Top) * 0.5f);

                if (o.Blocked)
                {
                    Blocker($"WindowBlock_{i}", group, mid, Vector3.one, rotation).GetComponent<BoxCollider>().size =
                        new Vector3(WallThickness, o.Top - o.Bottom, o.Width);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 corner = a + dir * (o.Center + shift + side * (o.Width * 0.5f - 0.2f)) + Vector3.up * (o.Top - 0.2f);
                        Box($"Arch_{i}", group, corner, new Vector3(WallThickness * 0.98f, 0.4f, 0.4f), material, false, rotation);
                    }
                    Box($"SillStone_{i}", group, a + dir * (o.Center + shift) + Vector3.up * (o.Bottom - 0.04f),
                        new Vector3(WallThickness + 0.12f, 0.08f, o.Width + 0.2f), stone, false, rotation);
                    // Mullions: a few iron glazing bars, so the windows read as windows from inside.
                    Box($"Mullion_{i}", group, mid, new Vector3(0.05f, o.Top - o.Bottom, 0.05f), iron, false, rotation);
                    Box($"Transom_{i}", group, mid + Vector3.up * ((o.Top - o.Bottom) * 0.2f), new Vector3(0.05f, 0.05f, o.Width), iron, false, rotation);
                }
                if (o.Glazed)
                    Box($"Glass_{i}", group, mid, new Vector3(0.04f, o.Top - o.Bottom, o.Width), glassDark, false, rotation);
                if (o.Boarded)
                {
                    int planks = rng.Next(2, 4);
                    for (int p = 0; p < planks; p++)
                    {
                        float y = Mathf.Lerp(o.Bottom + 0.6f, o.Top - 0.6f, (p + 0.5f) / planks);
                        var tilt = rotation * Quaternion.Euler(Rand(-25f, 25f), 0f, 0f);
                        Box($"Board_{i}_{p}", group, a + dir * (o.Center + shift) + Vector3.up * y,
                            new Vector3(0.05f, 0.25f, o.Width + 0.3f), plank, false, tilt);
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
            ColliderOnly(Beam("Collider", group, a + Vector3.up * 0.6f, b + Vector3.up * 0.6f, 0.1f, 1.2f, material, true));
        }

        /// <summary>Visible steps plus one smooth invisible ramp collider, so the CharacterController glides up.</summary>
        static void Stairs(string name, Transform parent, Vector3 bottom, Vector3 runDirection, float run, float rise,
            float width, Material material)
        {
            var group = GrayboxKit.Group(name, parent).transform;
            int steps = Mathf.Max(1, Mathf.RoundToInt(rise / 0.22f));
            float stepRise = rise / steps, stepRun = run / steps;
            var rotation = Quaternion.LookRotation(runDirection, Vector3.up);
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * stepRise;
                Vector3 c = bottom + runDirection * ((i + 0.5f) * stepRun) + Vector3.up * (h * 0.5f);
                Box($"Step_{i}", group, c, new Vector3(width, h, stepRun), material, false, rotation);
            }
            Vector3 top = bottom + runDirection * run + Vector3.up * rise;
            ColliderOnly(Beam("RampCollider", group, bottom, top, width, 0.05f, material, true));
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
            Tri(l + f, t + f, r + f);
            Tri(r + k, t + k, l + k);
            Quad(l + k, t + k, t + f, l + f);
            Quad(r + f, t + f, t + k, r + k);
            Quad(l + f, r + f, r + k, l + k);
            mesh = new Mesh { name = "UnitPrism" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, PrismMeshPath);
            return mesh;
        }

        /// <summary>Open cone, apex at the origin pointing up, base ring of radius 1 at y = -1. UV v = 1 at the apex.</summary>
        static Mesh ConeMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
            if (mesh != null)
                return mesh;
            AssetFolders.Ensure(ProjectPaths.Meshes);
            const int sides = 14;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a) * 0.05f, 0f, Mathf.Sin(a) * 0.05f));
                uvs.Add(new Vector2(i / (float)sides, 1f));
                vertices.Add(new Vector3(Mathf.Cos(a), -1f, Mathf.Sin(a)));
                uvs.Add(new Vector2(i / (float)sides, 0f));
            }
            for (int i = 0; i < sides; i++)
            {
                int s = i * 2;
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 1);
                triangles.Add(s + 1); triangles.Add(s + 2); triangles.Add(s + 3);
            }
            mesh = new Mesh { name = "LightCone" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, ConeMeshPath);
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

        /// <summary>A soft beam of haze hanging under a light: an additive cone that fades toward the floor.</summary>
        static GameObject HazeCone(string name, Transform parent, Vector3 apex, float length, float radius, Material material)
        {
            var go = MeshObject(name, parent, ConeMesh(), material, apex, Quaternion.identity, new Vector3(radius, length, radius));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        /// <summary>A slanted shaft of moonlight, e.g. from a window or a hole in the roof, pointing along <paramref name="direction"/>.</summary>
        static GameObject MoonShaft(string name, Transform parent, Vector3 origin, Vector3 direction, float length, float radius)
        {
            var go = HazeCone(name, parent, origin, length, radius, hazeMoon);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.down, direction.normalized);
            return go;
        }

        // ------------------------------------------------------------------ lamps

        enum LampState { Working, Flickering, Dead }

        static readonly List<Object> BreakerLights = new List<Object>();
        static readonly List<Object> BreakerFixtures = new List<Object>();
        static int shadowedLamps;
        const int MaxShadowedLamps = 14;

        /// <summary>
        /// Enamel-shade lamp on a cord. Working and flickering lamps are wired to the main breaker and
        /// throw a downward spot with a haze cone; the first <see cref="MaxShadowedLamps"/> working ones
        /// also cast shadows, which is what makes pools of light read.
        /// </summary>
        static void HangingLamp(Transform parent, Vector3 floorPoint, float ceiling, LampState state, string id,
            float drop = 1.8f, bool onBreaker = true)
        {
            var g = GrayboxKit.Group($"Lamp_{id}", parent).transform;
            float shadeY = ceiling - drop;
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

            Vector3 lightPos = floorPoint + Vector3.up * (shadeY - 0.15f);
            var light = GrayboxKit.SpotLight("Light", g, lightPos, Quaternion.Euler(90f, 0f, 0f), LampColor,
                state == LampState.Flickering ? 9f : 11f, shadeY + 4f, 110f);
            light.innerSpotAngle = 60f;
            if (state == LampState.Working && shadowedLamps < MaxShadowedLamps)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.9f;
                shadowedLamps++;
            }
            // The haze is a child of the light, so the breaker switches it with the lamp.
            var haze = HazeCone("Haze", light.transform, lightPos, shadeY * 0.85f, shadeY * 0.6f, hazeWarm);
            haze.transform.SetParent(light.transform, true);

            if (onBreaker)
            {
                BreakerLights.Add(light.gameObject);
                BreakerFixtures.Add(bulbRenderer);
            }
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

        static LampState ToState(char c) => c == 'W' ? LampState.Working : c == 'F' ? LampState.Flickering : LampState.Dead;

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
    }
}
