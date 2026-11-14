using System.Collections.Generic;
using HorrorGame.Painting.Strokes;
using NUnit.Framework;
using UnityEngine;

namespace HorrorGame.Tests
{
    /// <summary>
    /// The brushstroke generator: strokes cover a surface at the density asked for, the same every
    /// time, never on the underside, grass keeps out of clearings, and the GPU packing round-trips.
    /// </summary>
    public class StrokeTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
                if (go != null)
                    Object.DestroyImmediate(go);
            spawned.Clear();
        }

        BrushStrokeSource Source(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go.AddComponent<BrushStrokeSource>();
        }

        [Test]
        public void Box_IsCoveredAtTheRequestedDensity_TheSameEveryTime_AndNotUnderneath()
        {
            var source = Source("Box");
            source.density = 10f;
            // A 2 x 1 x 2 box: top 4 m2, four sides 8 m2, bottom 4 m2 (skipped: it faces the ground).
            source.SetShapes(new List<StrokeShape> { new StrokeShape { type = StrokeShapeType.Box, matrix = Matrix4x4.Scale(new Vector3(2f, 1f, 2f)) } }, null);

            var first = StrokeGenerator.Generate(new[] { StrokeJob.From(source, 0, 42) });
            var second = StrokeGenerator.Generate(new[] { StrokeJob.From(source, 0, 42) });

            Assert.That(first.Length, Is.InRange(108, 132), "About 12 m2 x 10 strokes per m2.");
            Assert.AreEqual(first.Length, second.Length, "Same seed, same strokes.");
            for (int i = 0; i < first.Length; i++)
                Assert.AreEqual(first[i].position, second[i].position);
            foreach (var stroke in first)
            {
                Assert.That(stroke.Normal.y, Is.GreaterThan(-0.5f), "No strokes on the underside.");
                Assert.That(stroke.position.y, Is.InRange(-0.51f, 0.51f));
                Assert.AreEqual(StrokeKind.Surface, stroke.Kind);
            }
        }

        [Test]
        public void Grass_StaysOutOfClearings()
        {
            var source = Source("Grass");
            source.kind = StrokeKind.Grass;
            source.density = 20f;
            source.grassHalfSize = new Vector2(5f, 5f);
            source.grassClearings = new[] { new Vector3(0f, 0f, 2f) };
            var strokes = StrokeGenerator.Generate(new[] { StrokeJob.From(source, 0, 7) });

            Assert.That(strokes.Length, Is.GreaterThan(1500), "Most of the 100 m2 is grass.");
            foreach (var stroke in strokes)
            {
                // Clearing edges straggle by up to 15%.
                Assert.That(new Vector2(stroke.position.x, stroke.position.z).magnitude, Is.GreaterThan(1.69f));
                Assert.AreEqual(StrokeKind.Grass, stroke.Kind);
            }
        }

        [Test]
        public void MovingSources_KeepTheirStrokesInTheirOwnSpace()
        {
            var source = Source("Head");
            source.transform.position = new Vector3(100f, 5f, 0f);
            source.moving = true;
            source.density = 50f;
            source.SetShapes(new List<StrokeShape> { new StrokeShape { type = StrokeShapeType.Sphere, matrix = Matrix4x4.identity } }, null);
            var strokes = StrokeGenerator.Generate(new[] { StrokeJob.From(source, 3, 1) });

            Assert.That(strokes.Length, Is.GreaterThan(100));
            foreach (var stroke in strokes)
            {
                Assert.AreEqual(3, stroke.Bone);
                Assert.AreEqual(0.5f, stroke.position.magnitude, 0.01f, "On the sphere, in the source's own space.");
            }
        }

        [Test]
        public void Packing_RoundTrips()
        {
            var dir = new Vector3(0.3f, -0.8f, 0.52f).normalized;
            Assert.That(Vector3.Angle(dir, GpuStroke.UnpackDirection(GpuStroke.PackDirection(dir))), Is.LessThan(0.5f));
            Assert.AreEqual(0.375f, GpuStroke.HalfToFloat(GpuStroke.FloatToHalf(0.375f)), 1e-4f);
            Assert.AreEqual(12.5f, GpuStroke.HalfToFloat(GpuStroke.FloatToHalf(12.5f)), 0.01f);
            var stroke = new GpuStroke { size = GpuStroke.PackSize(0.2f, 0.05f), misc = GpuStroke.PackMisc(7, StrokeKind.Restless, 0.01f) };
            Assert.AreEqual(0.2f, stroke.HalfSize.x, 1e-3f);
            Assert.AreEqual(0.05f, stroke.HalfSize.y, 1e-3f);
            Assert.AreEqual(7, stroke.Bone);
            Assert.AreEqual(StrokeKind.Restless, stroke.Kind);
        }
    }
}
