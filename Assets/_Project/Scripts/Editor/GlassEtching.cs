using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Draws the pattern etched into the glass panes: a double border, a containment ring of made-up
    /// glyphs, spokes, a central sigil, and two lines of glyph "script". Returns a coverage mask
    /// (0 = clear glass, 1 = fully etched), anti-aliased. Same seed, same pattern.
    /// </summary>
    public static class GlassEtching
    {
        public static float[] Draw(int size, int seed)
        {
            var canvas = new EtchCanvas(size);
            var rng = new System.Random(seed);
            float s = size;
            float w = s / 400f; // line width unit: about 1.3 px at 512

            // Frame: double border inset from the pane edge.
            canvas.Rect(s * 0.04f, s * 0.04f, s * 0.96f, s * 0.96f, w * 1.6f);
            canvas.Rect(s * 0.06f, s * 0.06f, s * 0.94f, s * 0.94f, w * 0.8f);

            // Containment ring with glyphs between two circles.
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float outer = s * 0.33f, inner = s * 0.26f;
            canvas.Circle(c, outer, w * 1.4f);
            canvas.Circle(c, inner, w);
            canvas.Circle(c, s * 0.12f, w);
            const int glyphs = 20;
            for (int i = 0; i < glyphs; i++)
            {
                float angle = (i + 0.5f) / glyphs * Mathf.PI * 2f;
                Vector2 at = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ((outer + inner) * 0.5f);
                Glyph(canvas, rng, at, (outer - inner) * 0.32f, angle + Mathf.PI * 0.5f, w * 0.9f);
                // Spokes from the small circle out to the inner ring, every other glyph.
                if (i % 2 == 0)
                {
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    canvas.Line(c + dir * s * 0.12f, c + dir * inner, w * 0.7f);
                }
            }

            // Central sigil: a triangle pointing down with a bar through it.
            float r = s * 0.09f;
            Vector2 a = c + new Vector2(0f, -r), b = c + new Vector2(r * 0.87f, r * 0.5f), d = c + new Vector2(-r * 0.87f, r * 0.5f);
            canvas.Line(a, b, w * 1.2f);
            canvas.Line(b, d, w * 1.2f);
            canvas.Line(d, a, w * 1.2f);
            canvas.Line(c + new Vector2(-r * 1.1f, 0f), c + new Vector2(r * 1.1f, 0f), w);

            // Two lines of glyph script, top and bottom.
            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? s * 0.12f : s * 0.88f;
                for (float x = s * 0.12f; x < s * 0.88f; x += s * 0.045f)
                    Glyph(canvas, rng, new Vector2(x, y), s * 0.016f, 0f, w * 0.7f);
            }

            // A few faint scratches, so the glass isn't too clean.
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * s, (float)rng.NextDouble() * s);
                float angle = (float)rng.NextDouble() * Mathf.PI;
                float length = s * (0.02f + 0.06f * (float)rng.NextDouble());
                canvas.Line(p, p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, w * 0.35f, 0.4f);
            }
            return canvas.Mask;
        }

        /// <summary>A made-up rune: 2-4 strokes between points on a small 3x3 grid, rotated to face out of the ring.</summary>
        static void Glyph(EtchCanvas canvas, System.Random rng, Vector2 center, float halfSize, float rotation, float width)
        {
            float cos = Mathf.Cos(rotation), sin = Mathf.Sin(rotation);
            Vector2 Point(int index)
            {
                var local = new Vector2((index % 3 - 1) * halfSize, (index / 3 - 1) * halfSize);
                return center + new Vector2(local.x * cos - local.y * sin, local.x * sin + local.y * cos);
            }

            int strokes = 2 + rng.Next(3);
            int from = rng.Next(9);
            for (int i = 0; i < strokes; i++)
            {
                int to = rng.Next(9);
                if (to == from)
                    to = (to + 4) % 9;
                canvas.Line(Point(from), Point(to), width);
                from = rng.Next(3) == 0 ? rng.Next(9) : to;
            }
            if (rng.Next(3) == 0)
                canvas.Circle(Point(4), halfSize * 0.35f, width * 0.8f);
        }

        sealed class EtchCanvas
        {
            public readonly float[] Mask;
            readonly int size;

            public EtchCanvas(int size)
            {
                this.size = size;
                Mask = new float[size * size];
            }

            public void Line(Vector2 a, Vector2 b, float width, float strength = 1f)
            {
                float reach = width + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach));
                int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach));
                Vector2 ab = b - a;
                float lengthSq = Mathf.Max(ab.sqrMagnitude, 0.0001f);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                    float distance = Vector2.Distance(p, a + ab * t);
                    Plot(x, y, Coverage(distance, width) * strength);
                }
            }

            public void Circle(Vector2 center, float radius, float width)
            {
                float reach = radius + width + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - reach)), x1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.x + reach));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - reach)), y1 = Mathf.Min(size - 1, Mathf.CeilToInt(center.y + reach));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float distance = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) - radius);
                    Plot(x, y, Coverage(distance, width));
                }
            }

            public void Rect(float x0, float y0, float x1, float y1, float width)
            {
                Line(new Vector2(x0, y0), new Vector2(x1, y0), width);
                Line(new Vector2(x1, y0), new Vector2(x1, y1), width);
                Line(new Vector2(x1, y1), new Vector2(x0, y1), width);
                Line(new Vector2(x0, y1), new Vector2(x0, y0), width);
            }

            /// <summary>1 inside the stroke, fading to 0 over one pixel at its edge.</summary>
            static float Coverage(float distance, float width) => Mathf.Clamp01(width * 0.5f + 0.5f - distance);

            void Plot(int x, int y, float value)
            {
                int i = y * size + x;
                if (value > Mask[i])
                    Mask[i] = value;
            }
        }
    }
}
