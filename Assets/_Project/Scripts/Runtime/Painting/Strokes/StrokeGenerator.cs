using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HorrorGame.Painting.Strokes
{
    /// <summary>One brushstroke as the GPU sees it: 32 bytes. Must match <c>Stroke</c> in BrushStrokeCommon.hlsl.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuStroke
    {
        public const int Stride = 32;

        /// <summary>Centre (Grass: the root of the blade), in the space of its bone.</summary>
        public Vector3 position;
        /// <summary>Surface normal, 3 x 10-bit signed.</summary>
        public uint normal;
        /// <summary>Direction the brush was dragged, 3 x 10-bit signed.</summary>
        public uint tangent;
        /// <summary>RGBA8. A &gt;= 128: RGB is a tint (x2) on the colour of the lit world under the stroke. Otherwise RGB is the paint colour.</summary>
        public uint color;
        /// <summary>Half length and half width as two halfs.</summary>
        public uint size;
        /// <summary>Bits 0-15 bone, 16-19 <see cref="StrokeKind"/>, 20-27 lift above the surface in millimetres.</summary>
        public uint misc;

        public int Bone => (int)(misc & 0xFFFF);
        public StrokeKind Kind => (StrokeKind)((misc >> 16) & 0xF);
        public Vector3 Normal => UnpackDirection(normal);
        public Vector2 HalfSize => new Vector2(HalfToFloat(size & 0xFFFF), HalfToFloat(size >> 16));

        public static uint PackDirection(Vector3 v)
        {
            uint Q(float f) => (uint)Mathf.Clamp(Mathf.RoundToInt((f * 0.5f + 0.5f) * 1023f), 0, 1023);
            return Q(v.x) | (Q(v.y) << 10) | (Q(v.z) << 20);
        }

        public static Vector3 UnpackDirection(uint v)
        {
            float D(uint q) => q / 1023f * 2f - 1f;
            return new Vector3(D(v & 1023), D((v >> 10) & 1023), D((v >> 20) & 1023)).normalized;
        }

        public static uint PackColor(Color c, bool tintOnWorld)
        {
            uint B(float f) => (uint)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);
            if (tintOnWorld)
                return B(c.r * 0.5f) | (B(c.g * 0.5f) << 8) | (B(c.b * 0.5f) << 16) | (255u << 24);
            return B(c.r) | (B(c.g) << 8) | (B(c.b) << 16);
        }

        public static uint PackSize(float halfLength, float halfWidth) => FloatToHalf(halfLength) | (FloatToHalf(halfWidth) << 16);

        public static uint PackMisc(int bone, StrokeKind kind, float liftMetres)
        {
            uint lift = (uint)Mathf.Clamp(Mathf.RoundToInt(liftMetres * 1000f), 0, 255);
            return ((uint)bone & 0xFFFF) | (((uint)kind & 0xF) << 16) | (lift << 20);
        }

        /// <summary>float to IEEE half (no Unity API, so it is safe on a worker thread).</summary>
        public static uint FloatToHalf(float f)
        {
            uint x = (uint)BitConverter.SingleToInt32Bits(f);
            uint sign = (x >> 16) & 0x8000;
            int exp = (int)((x >> 23) & 0xFF) - 127 + 15;
            uint mantissa = x & 0x7FFFFF;
            if (exp <= 0)
                return sign;
            if (exp >= 31)
                return sign | 0x7C00;
            return sign | ((uint)exp << 10) | (mantissa >> 13);
        }

        public static float HalfToFloat(uint h)
        {
            uint sign = (h & 0x8000) << 16;
            int exp = (int)((h >> 10) & 0x1F);
            uint mantissa = h & 0x3FF;
            if (exp == 0)
                return BitConverter.Int32BitsToSingle((int)sign);
            uint bits = sign | ((uint)(exp - 15 + 127) << 23) | (mantissa << 13);
            return BitConverter.Int32BitsToSingle((int)bits);
        }
    }

    /// <summary>
    /// A snapshot of one <see cref="BrushStrokeSource"/> in plain data, taken on the main thread, so
    /// the strokes can be generated on a worker thread.
    /// </summary>
    public sealed class StrokeJob
    {
        public StrokeKind kind;
        public int bone;
        public int seed;
        public float density;
        public Vector2 length;
        public Vector2 width;
        public float lift;
        public bool skipDownFacing;
        public bool tintOnWorld;
        public Color color;
        public Color color2;
        public float colorJitter;

        /// <summary>Where samples are taken: the source's world matrix (fixed sources) or its scale only (moving ones).</summary>
        public Matrix4x4 place = Matrix4x4.identity;
        /// <summary>From sample space back to the space the strokes are stored in (inverse scale for moving sources).</summary>
        public Matrix4x4 store = Matrix4x4.identity;
        /// <summary>True when "down" in sample space is really down in the world (fixed sources).</summary>
        public bool worldAligned = true;

        public StrokeShape[] shapes = Array.Empty<StrokeShape>();
        /// <summary>Per shape: place * shape matrix, and its inverse transpose for normals. Filled on the main thread.</summary>
        public Matrix4x4[] shapeMatrices = Array.Empty<Matrix4x4>();
        public Matrix4x4[] normalMatrices = Array.Empty<Matrix4x4>();
        public Vector3[] triangles = Array.Empty<Vector3>();

        public Vector2 grassHalfSize;
        public Vector3[] grassClearings = Array.Empty<Vector3>();

        public float skyRadius = 150f;
        public Color skyZenith;
        public Color skyGlow;
        public Vector3 skyGlowDirection = Vector3.forward;

        /// <summary>Copies a source's settings. Call on the main thread.</summary>
        public static StrokeJob From(BrushStrokeSource source, int bone, int seed)
        {
            var job = new StrokeJob
            {
                kind = source.kind,
                bone = bone,
                seed = seed,
                density = source.density,
                length = source.length,
                width = source.width,
                lift = source.lift,
                skipDownFacing = source.skipDownFacing,
                tintOnWorld = source.sampleWorld >= 0.5f,
                color = source.color,
                color2 = source.color2,
                colorJitter = source.colorJitter,
                grassHalfSize = source.grassHalfSize,
                grassClearings = source.grassClearings ?? Array.Empty<Vector3>(),
                skyRadius = source.skyRadius,
                skyZenith = source.skyZenith,
                skyGlow = source.skyGlow,
                skyGlowDirection = source.skyGlowDirection.sqrMagnitude > 0f ? source.skyGlowDirection.normalized : Vector3.forward,
                triangles = source.Triangles ?? Array.Empty<Vector3>(),
            };
            if (source.moving)
            {
                Vector3 s = source.transform.lossyScale;
                s = new Vector3(Mathf.Max(Mathf.Abs(s.x), 1e-4f), Mathf.Max(Mathf.Abs(s.y), 1e-4f), Mathf.Max(Mathf.Abs(s.z), 1e-4f));
                job.place = Matrix4x4.Scale(s);
                job.store = Matrix4x4.Scale(new Vector3(1f / s.x, 1f / s.y, 1f / s.z));
                job.worldAligned = false;
            }
            else if (source.kind != StrokeKind.Sky)
            {
                job.place = source.transform.localToWorldMatrix;
            }

            var shapes = source.Shapes;
            job.shapes = new StrokeShape[shapes.Count];
            job.shapeMatrices = new Matrix4x4[shapes.Count];
            job.normalMatrices = new Matrix4x4[shapes.Count];
            for (int i = 0; i < shapes.Count; i++)
            {
                job.shapes[i] = shapes[i];
                var m = job.place * shapes[i].matrix;
                job.shapeMatrices[i] = m;
                job.normalMatrices[i] = m.inverse.transpose;
            }
            return job;
        }
    }

    /// <summary>
    /// Turns <see cref="StrokeJob"/>s into brushstrokes. Pure C# with no Unity engine calls, so it
    /// can run on a worker thread while the player is still in the parlor.
    /// </summary>
    public static class StrokeGenerator
    {
        /// <summary>Hard ceiling on strokes per source, so a typo in a density can't eat the GPU.</summary>
        public const int MaxPerSource = 1_500_000;

        public static GpuStroke[] Generate(IReadOnlyList<StrokeJob> jobs)
        {
            var all = new List<GpuStroke>(1 << 20);
            foreach (var job in jobs)
                Generate(job, all);
            return all.ToArray();
        }

        public static void Generate(StrokeJob job, List<GpuStroke> output)
        {
            var rng = new System.Random(job.seed);
            int start = output.Count;
            switch (job.kind)
            {
                case StrokeKind.Grass:
                    Grass(job, rng, output);
                    break;
                case StrokeKind.Sky:
                    Sky(job, rng, output);
                    break;
                default:
                    for (int i = 0; i < job.shapes.Length && output.Count - start < MaxPerSource; i++)
                        Shape(job, i, rng, output);
                    break;
            }
        }

        // ------------------------------------------------------------------ surfaces

        static void Shape(StrokeJob job, int index, System.Random rng, List<GpuStroke> output)
        {
            var shape = job.shapes[index];
            var m = job.shapeMatrices[index];
            var nm = job.normalMatrices[index];
            switch (shape.type)
            {
                case StrokeShapeType.Box:
                    for (int axis = 0; axis < 3; axis++)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 nLocal = Vector3.zero;
                        nLocal[axis] = sign;
                        Vector3 a = Vector3.zero, b = Vector3.zero;
                        a[(axis + 1) % 3] = 1f;
                        b[(axis + 2) % 3] = 1f;
                        Vector3 ea = m.MultiplyVector(a), eb = m.MultiplyVector(b);
                        float area = Vector3.Cross(ea, eb).magnitude;
                        Vector3 n = nm.MultiplyVector(nLocal).normalized;
                        if (SkipFacing(job, n))
                            continue;
                        int count = Count(job, shape, area, rng);
                        for (int k = 0; k < count; k++)
                        {
                            Vector3 p = nLocal * 0.5f + a * ((float)rng.NextDouble() - 0.5f) + b * ((float)rng.NextDouble() - 0.5f);
                            Emit(job, shape, m.MultiplyPoint3x4(p), n, rng, output);
                        }
                    }
                    break;

                case StrokeShapeType.Sphere:
                {
                    float sa = m.GetColumn(0).magnitude * 0.5f, sb = m.GetColumn(1).magnitude * 0.5f, sc = m.GetColumn(2).magnitude * 0.5f;
                    const float p = 1.6075f;
                    float area = 4f * Mathf.PI * Mathf.Pow((Mathf.Pow(sa * sb, p) + Mathf.Pow(sa * sc, p) + Mathf.Pow(sb * sc, p)) / 3f, 1f / p);
                    int count = Count(job, shape, area, rng);
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 d = RandomDirection(rng);
                        Vector3 n = nm.MultiplyVector(d).normalized;
                        if (SkipFacing(job, n))
                            continue;
                        Emit(job, shape, m.MultiplyPoint3x4(d * 0.5f), n, rng, output);
                    }
                    break;
                }

                case StrokeShapeType.Cylinder:
                {
                    // Unity's cylinder: radius 0.5, y from -1 to 1.
                    float ra = m.GetColumn(0).magnitude * 0.5f, rc = m.GetColumn(2).magnitude * 0.5f;
                    float height = m.GetColumn(1).magnitude * 2f;
                    float perimeter = Mathf.PI * (3f * (ra + rc) - Mathf.Sqrt((3f * ra + rc) * (ra + 3f * rc)));
                    float side = perimeter * height, cap = Mathf.PI * ra * rc;
                    int count = Count(job, shape, side + 2f * cap, rng);
                    float sideShare = side / Mathf.Max(1e-6f, side + 2f * cap);
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 local, nLocal;
                        if (rng.NextDouble() < sideShare)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            nLocal = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            local = new Vector3(nLocal.x * 0.5f, (float)rng.NextDouble() * 2f - 1f, nLocal.z * 0.5f);
                        }
                        else
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float r = 0.5f * Mathf.Sqrt((float)rng.NextDouble());
                            float y = rng.NextDouble() < 0.5 ? -1f : 1f;
                            nLocal = new Vector3(0f, y, 0f);
                            local = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        }
                        Vector3 n = nm.MultiplyVector(nLocal).normalized;
                        if (SkipFacing(job, n))
                            continue;
                        Emit(job, shape, m.MultiplyPoint3x4(local), n, rng, output);
                    }
                    break;
                }

                case StrokeShapeType.Triangles:
                {
                    var tris = job.triangles;
                    int end = Mathf.Min(tris.Length, (shape.triangleStart + shape.triangleCount) * 3);
                    for (int t = shape.triangleStart * 3; t + 2 < end; t += 3)
                    {
                        Vector3 a = m.MultiplyPoint3x4(tris[t]), b = m.MultiplyPoint3x4(tris[t + 1]), c = m.MultiplyPoint3x4(tris[t + 2]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        float area = cross.magnitude * 0.5f;
                        if (area < 1e-7f)
                            continue;
                        Vector3 n = cross / (area * 2f);
                        if (SkipFacing(job, n))
                            continue;
                        int count = Count(job, shape, area, rng);
                        for (int k = 0; k < count; k++)
                        {
                            float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                            if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                            Emit(job, shape, a + (b - a) * u + (c - a) * v, n, rng, output);
                        }
                    }
                    break;
                }
            }
        }

        static bool SkipFacing(StrokeJob job, Vector3 n) => job.skipDownFacing && job.worldAligned && n.y < -0.6f;

        static int Count(StrokeJob job, StrokeShape shape, float area, System.Random rng)
        {
            float wanted = area * job.density * (shape.detail ? 3f : 1f);
            int count = (int)wanted;
            if (rng.NextDouble() < wanted - count)
                count++;
            if (shape.detail)
                count = Math.Max(count, 6);
            return Math.Min(count, MaxPerSource);
        }

        static void Emit(StrokeJob job, StrokeShape shape, Vector3 position, Vector3 normal, System.Random rng, List<GpuStroke> output)
        {
            float r = (float)rng.NextDouble();
            Vector3 tangent = Flow(position, normal, r);
            float len = Mathf.Lerp(job.length.x, job.length.y, (float)rng.NextDouble());
            float wid = Mathf.Lerp(job.width.x, job.width.y, (float)rng.NextDouble());
            float lift = job.lift;
            if (shape.detail)
            {
                len *= 0.5f;
                wid *= 0.6f;
                lift += 0.012f;
            }
            Vector3 storedNormal = job.store.MultiplyVector(normal);
            Vector3 storedTangent = job.store.MultiplyVector(tangent);
            output.Add(new GpuStroke
            {
                position = job.store.MultiplyPoint3x4(position),
                normal = GpuStroke.PackDirection(storedNormal.normalized),
                tangent = GpuStroke.PackDirection(storedTangent.normalized),
                color = PaintColor(job, rng),
                size = GpuStroke.PackSize(len * 0.5f, wid * 0.5f),
                misc = GpuStroke.PackMisc(job.bone, job.kind, lift),
            });
        }

        /// <summary>
        /// Which way the brush was dragged. Flat ground swirls along a slow noise field; walls are
        /// painted in mostly upright strokes that lean one way and the other.
        /// </summary>
        static Vector3 Flow(Vector3 p, Vector3 n, float r)
        {
            Vector3 t;
            if (Mathf.Abs(n.y) > 0.7f)
            {
                float a = (Noise(p.x * 0.09f, p.z * 0.09f) * 2.2f + Noise(p.x * 0.4f + 7.1f, p.z * 0.4f) * 0.5f) * Mathf.PI * 2f
                          + (r - 0.5f) * 0.5f;
                t = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            else
            {
                Vector3 up = Vector3.up - n * n.y;
                if (up.sqrMagnitude < 1e-6f)
                    up = Vector3.forward;
                up.Normalize();
                float a = (Noise((p.x + p.z) * 0.3f, p.y * 0.3f) - 0.5f) * 1.3f + (r - 0.5f) * 0.4f;
                t = Rotate(up, n, a);
            }
            t -= n * Vector3.Dot(t, n);
            if (t.sqrMagnitude < 1e-6f)
                t = Perpendicular(n);
            return t.normalized;
        }

        static uint PaintColor(StrokeJob job, System.Random rng)
        {
            float j = job.colorJitter;
            float v = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * j;
            float h = ((float)rng.NextDouble() - 0.5f) * j * 0.5f;
            if (job.tintOnWorld)
                return GpuStroke.PackColor(new Color(v * (1f + h), v, v * (1f - h)), true);
            var c = Color.Lerp(job.color, job.color2, (float)rng.NextDouble());
            return GpuStroke.PackColor(new Color(c.r * v * (1f + h), c.g * v, c.b * v * (1f - h)), false);
        }

        // ------------------------------------------------------------------ grass

        static void Grass(StrokeJob job, System.Random rng, List<GpuStroke> output)
        {
            float hx = Mathf.Max(0f, job.grassHalfSize.x), hz = Mathf.Max(0f, job.grassHalfSize.y);
            int candidates = Math.Min(MaxPerSource, (int)(4f * hx * hz * job.density));
            Vector3 up = job.place.MultiplyVector(Vector3.up).normalized;
            var clearings = job.grassClearings;
            for (int i = 0; i < candidates; i++)
            {
                float x = ((float)rng.NextDouble() * 2f - 1f) * hx;
                float z = ((float)rng.NextDouble() * 2f - 1f) * hz;
                bool clear = false;
                for (int c = 0; c < clearings.Length && !clear; c++)
                {
                    float dx = x - clearings[c].x, dz = z - clearings[c].y;
                    // Clearings are soft-edged: a few blades straggle in.
                    float edge = clearings[c].z * (0.85f + 0.3f * (float)rng.NextDouble());
                    clear = dx * dx + dz * dz < edge * edge;
                }
                if (clear)
                    continue;
                Vector3 position = job.place.MultiplyPoint3x4(new Vector3(x, 0f, z));
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 lean = job.place.MultiplyVector(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))).normalized;
                float height = Mathf.Lerp(job.length.x, job.length.y, Mathf.Pow((float)rng.NextDouble(), 0.7f));
                float wid = Mathf.Lerp(job.width.x, job.width.y, (float)rng.NextDouble());
                output.Add(new GpuStroke
                {
                    position = job.store.MultiplyPoint3x4(position),
                    normal = GpuStroke.PackDirection(up),
                    tangent = GpuStroke.PackDirection(lean),
                    color = PaintColor(job, rng),
                    size = GpuStroke.PackSize(height * 0.5f, wid * 0.5f),
                    misc = GpuStroke.PackMisc(job.bone, StrokeKind.Grass, 0f),
                });
            }
        }

        // ------------------------------------------------------------------ sky

        static void Sky(StrokeJob job, System.Random rng, List<GpuStroke> output)
        {
            int count = Math.Min(MaxPerSource, (int)job.density);
            Vector3 glowDir = job.skyGlowDirection;
            for (int i = 0; i < count; i++)
            {
                // Uniform over the dome, from a little under the horizon up.
                float y = Mathf.Lerp(-0.12f, 1f, (float)rng.NextDouble());
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var dir = new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                Vector3 normal = -dir;
                // Strokes run along the horizon and curl, like a sky laid in with a wide brush.
                Vector3 east = Vector3.Cross(Vector3.up, dir);
                if (east.sqrMagnitude < 1e-4f)
                    east = Vector3.right;
                east.Normalize();
                float swirl = (Noise(a * 2.5f, y * 4f) - 0.5f) * 1.6f + ((float)rng.NextDouble() - 0.5f) * 0.3f;
                Vector3 tangent = Rotate(east, dir, swirl);

                float elevation = Mathf.Clamp01(y);
                var baseColor = Color.Lerp(job.color, job.skyZenith, Mathf.Pow(elevation, 0.55f));
                float glow = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, glowDir)), 5f);
                var c = Color.Lerp(baseColor, job.skyGlow, glow * 0.85f);
                float v = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * job.colorJitter;
                c = new Color(c.r * v, c.g * v, c.b * v);

                float len = Mathf.Lerp(job.length.x, job.length.y, (float)rng.NextDouble());
                float wid = Mathf.Lerp(job.width.x, job.width.y, (float)rng.NextDouble());
                output.Add(new GpuStroke
                {
                    position = dir * job.skyRadius,
                    normal = GpuStroke.PackDirection(normal),
                    tangent = GpuStroke.PackDirection(tangent),
                    color = GpuStroke.PackColor(c, false),
                    size = GpuStroke.PackSize(len * 0.5f, wid * 0.5f),
                    misc = GpuStroke.PackMisc(job.bone, StrokeKind.Sky, 0f),
                });
            }
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 RandomDirection(System.Random rng)
        {
            float z = (float)rng.NextDouble() * 2f - 1f;
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
        }

        /// <summary>Rotates <paramref name="v"/> about the unit <paramref name="axis"/> by <paramref name="radians"/>.</summary>
        static Vector3 Rotate(Vector3 v, Vector3 axis, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return v * c + Vector3.Cross(axis, v) * s + axis * (Vector3.Dot(axis, v) * (1f - c));
        }

        static Vector3 Perpendicular(Vector3 n)
        {
            Vector3 other = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
            return Vector3.Cross(n, other).normalized;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        public static float Noise(float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
    }
}
