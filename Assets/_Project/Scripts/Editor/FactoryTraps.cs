using System.Collections.Generic;
using HorrorGame.World;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    // Fall traps: the basement pit under the collapsed roof, the rotten plank bridge across it, and rotten floorboards.
    // Every one of them warns first (a sound, a sag, a sign), so a death reads as the player's own fault.
    public static partial class FactoryInteriorBuilder
    {
        /// <summary>Where the roof crashed through the hall floor into the basement (x, z).</summary>
        static readonly Rect PitRect = Rect.MinMaxRect(54f, 0.25f, 63.5f, 7.6f);
        const float PitDepth = 7f;

        static bool InPit(float x, float z, float margin = 0.4f) =>
            x > PitRect.xMin - margin && x < PitRect.xMax + margin && z > PitRect.yMin - margin && z < PitRect.yMax + margin;

        static void BuildPit(Transform parent)
        {
            var pit = GrayboxKit.Group("BasementPit", parent).transform;
            float x0 = PitRect.xMin, x1 = PitRect.xMax, z0 = PitRect.yMin, z1 = PitRect.yMax;
            float cx = PitRect.center.x, cz = PitRect.center.y;
            float bottom = -PitDepth;

            // Basement walls and floor: soot-black brick, a slick of black water, the wreck at the bottom.
            Box("Wall_N", pit, new Vector3(cx, bottom * 0.5f, z1 + 0.25f), new Vector3(x1 - x0 + 1f, PitDepth, 0.5f), brickDark);
            Box("Wall_S", pit, new Vector3(cx, bottom * 0.5f, z0 - 0.25f), new Vector3(x1 - x0 + 1f, PitDepth, 0.5f), brickDark);
            Box("Wall_W", pit, new Vector3(x0 - 0.25f, bottom * 0.5f, cz), new Vector3(0.5f, PitDepth, z1 - z0), brickDark);
            Box("Wall_E", pit, new Vector3(x1 + 0.25f, bottom * 0.5f, cz), new Vector3(0.5f, PitDepth, z1 - z0), brickDark);
            Box("Bottom", pit, new Vector3(cx, bottom - 0.1f, cz), new Vector3(x1 - x0 + 1f, 0.2f, z1 - z0 + 1f), debris);
            Box("Water", pit, new Vector3(cx + 1f, bottom + 0.02f, cz - 0.5f), new Vector3(6f, 0.02f, 4f), dipLiquid, false);
            // The floor slab's broken edge, so it reads as a hole and not a trench.
            Box("SlabEdge_N", pit, new Vector3(cx, -0.25f, z1 + 0.02f), new Vector3(x1 - x0, 0.5f, 0.06f), kit.Concrete, false);
            Box("SlabEdge_W", pit, new Vector3(x0 - 0.02f, -0.25f, cz), new Vector3(0.06f, 0.5f, z1 - z0), kit.Concrete, false);
            Box("SlabEdge_E", pit, new Vector3(x1 + 0.02f, -0.25f, cz), new Vector3(0.06f, 0.5f, z1 - z0), kit.Concrete, false);

            // Wrecked machine and roof timbers that came down with it. No colliders on the leaning timbers,
            // so nobody tries to walk down them.
            Box("WreckBase", pit, new Vector3(cx - 1f, bottom + 0.6f, cz + 0.4f), new Vector3(7f, 0.5f, 2.6f), iron, true, Quaternion.Euler(0f, 6f, 11f));
            Box("WreckFrame", pit, new Vector3(cx + 2.4f, bottom + 1.6f, cz + 0.8f), new Vector3(0.35f, 2.8f, 2.6f), iron, true, Quaternion.Euler(0f, 10f, 38f));
            Cyl("WreckDrum", pit, new Vector3(x0 + 1.2f, bottom + 0.9f, cz - 1.5f), 1.8f, 0.8f, iron, new Vector3(0.3f, 0f, 1f), true);
            Beam("FallenChord", pit, new Vector3(x0 + 1f, bottom, z0 + 1f), new Vector3(x0 + 3.5f, 0.6f, z1 + 0.3f), 0.25f, 0.3f, timber);
            Beam("FallenChord", pit, new Vector3(x1 - 0.5f, bottom, z0 + 1.5f), new Vector3(x1 - 3f, -0.3f, z1 + 0.2f), 0.25f, 0.3f, timber);
            for (int i = 0; i < 22; i++)
                Box("FallenBoard", pit, new Vector3(Rand(x0 + 0.4f, x1 - 0.4f), bottom + Rand(0.05f, 0.7f), Rand(z0 + 0.4f, z1 - 0.4f)),
                    new Vector3(Rand(0.4f, 0.8f), 0.08f, Rand(2f, 4f)), roofBoards, false,
                    Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 180f), Rand(-20f, 20f)));
            for (int i = 0; i < 8; i++)
                FactoryProps.Place(pit, new Vector3(Rand(x0 + 0.8f, x1 - 0.8f), bottom, Rand(z0 + 0.8f, z1 - 0.8f)), Rand(0f, 360f),
                    i % 3 == 0 ? Synty.RubblePlank : i % 2 == 0 ? Synty.Rubble : Synty.Rubble2, null, false);

            // Jagged floorboard ends and slab chunks sticking out over the edge, and a few hanging in.
            for (float x = x0 + 0.3f; x < x1; x += Rand(0.5f, 1.1f))
                Box("EdgeShard", pit, new Vector3(x, -0.06f, z1 - Rand(0.05f, 0.35f)), new Vector3(Rand(0.25f, 0.6f), 0.08f, Rand(0.3f, 0.9f)),
                    Chance(0.5) ? kit.Concrete : plank, false, Quaternion.Euler(Rand(-40f, -10f), Rand(-20f, 20f), Rand(-10f, 10f)));
            for (float z = z0 + 0.4f; z < z1; z += Rand(0.6f, 1.2f))
            {
                Box("EdgeShard", pit, new Vector3(x0 + Rand(0.05f, 0.3f), -0.06f, z), new Vector3(Rand(0.3f, 0.8f), 0.08f, Rand(0.25f, 0.5f)),
                    kit.Concrete, false, Quaternion.Euler(Rand(-10f, 10f), Rand(-20f, 20f), Rand(-35f, -10f)));
                Box("EdgeShard", pit, new Vector3(x1 - Rand(0.05f, 0.3f), -0.06f, z), new Vector3(Rand(0.3f, 0.8f), 0.08f, Rand(0.25f, 0.5f)),
                    kit.Concrete, false, Quaternion.Euler(Rand(-10f, 10f), Rand(-20f, 20f), Rand(10f, 35f)));
            }

            // Warnings: torn hazard tape on posts along the aisle side, a sign, and grit trickling in when you get close.
            var tape = GrayboxKit.Group("HazardTape", pit).transform;
            float tz = z1 + 0.5f;
            Vector3[] posts = { new Vector3(x0 + 0.2f, 0f, tz), new Vector3(x0 + 3.4f, 0f, tz), new Vector3(x0 + 6.6f, 0f, tz) };
            foreach (var p in posts)
                Box("TapePost", tape, p + Vector3.up * 0.5f, new Vector3(0.06f, 1f, 0.06f), kit.Metal, false, Quaternion.Euler(Rand(-8f, 8f), 0f, Rand(-8f, 8f)));
            Beam("Tape", tape, posts[0] + Vector3.up * 0.95f, posts[1] + Vector3.up * 0.8f, 0.01f, 0.06f, kit.Hazard);
            Beam("TapeTorn", tape, posts[1] + Vector3.up * 0.9f, posts[1] + new Vector3(0.6f, 0.1f, 0.15f), 0.01f, 0.06f, kit.Hazard);
            Beam("TapeTorn", tape, posts[2] + Vector3.up * 0.95f, posts[2] + new Vector3(-0.4f, 0.05f, -0.1f), 0.01f, 0.06f, kit.Hazard);
            Sign(pit, new Vector3(x0 + 3.4f, 1.05f, tz + 0.04f), 0f);

            Cue(pit, new Vector3(cx, 1f, z1 + 0.6f), new Vector3(x1 - x0 + 1f, 2.5f, 1.4f));
            Cue(pit, new Vector3(x0 - 0.6f, 1f, cz + 1.5f), new Vector3(1.4f, 2.5f, z1 - z0 - 3f));
            Cue(pit, new Vector3(x1 + 0.6f, 1f, cz + 1.5f), new Vector3(1.4f, 2.5f, z1 - z0 - 3f));

            PlankBridge(pit, new Vector3(x0 - 0.6f, 0f, 2.1f), new Vector3(x1 + 0.6f, 0f, 2.1f), 1.2f);
            Sawhorse(pit, new Vector3(x0 - 1.4f, 0f, 3.1f), 70f, true);
            Sawhorse(pit, new Vector3(x1 + 1.5f, 0f, 1.2f), 10f, false);
        }

        /// <summary>A trickle of grit falling into the dark when the player gets near the edge.</summary>
        static void Cue(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("EdgeCue");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var cue = go.AddComponent<TrapCue>();
            SetVector(cue, "size", size);
            SetVector(cue, "soundOffset", new Vector3(0f, -3f, -0.8f));
        }

        static void Sign(Transform parent, Vector3 p, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Sign", parent, p, new Vector3(0.9f, 0.45f, 0.03f), paper, false, rot * Quaternion.Euler(0f, 0f, 4f));
            Box("SignStripe", parent, p + rot * new Vector3(0f, 0.15f, -0.017f), new Vector3(0.85f, 0.1f, 0.01f), kit.Hazard, false, rot * Quaternion.Euler(0f, 0f, 4f));
        }

        static void Sawhorse(Transform parent, Vector3 p, float yaw, bool knockedOver)
        {
            var g = GrayboxKit.Group("Sawhorse", parent).transform;
            var rot = Quaternion.Euler(knockedOver ? 85f : 0f, yaw, 0f);
            Vector3 lift = knockedOver ? Vector3.up * 0.2f : Vector3.up * 0.9f;
            Box("Bar", g, p + lift, new Vector3(1.6f, 0.15f, 0.06f), kit.Hazard, false, rot);
            foreach (float x in new[] { -0.65f, 0.65f })
                Box("Leg", g, p + lift + rot * new Vector3(x, -0.45f, 0f), new Vector3(0.06f, 0.9f, 0.5f), plank, false, rot);
            Sign(g, p + lift + rot * new Vector3(0f, 0.35f, 0.05f), yaw);
            if (!knockedOver)
                Blocker("SawhorseBlock", g, p + Vector3.up * 0.5f, new Vector3(1.6f, 1f, 0.5f), Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>
        /// A workmen's plank bridge laid across the pit along X: tempting, creaky, and rotten through.
        /// It sags and breaks under the player a moment after they step on it.
        /// </summary>
        static void PlankBridge(Transform parent, Vector3 a, Vector3 b, float width)
        {
            var bridge = new GameObject("PlankBridge").transform;
            bridge.SetParent(parent, false);
            var surface = bridge.gameObject.AddComponent<BoxCollider>();
            float length = b.x - a.x;
            surface.center = new Vector3((a.x + b.x) * 0.5f, -0.05f, a.z);
            surface.size = new Vector3(length, 0.1f, width);

            var pieces = new List<Object>();
            var visuals = GrayboxKit.Group("Planks", bridge).transform;
            foreach (float side in new[] { -0.45f, 0.45f })
            {
                var stringer = Box("Stringer", visuals, new Vector3((a.x + b.x) * 0.5f, -0.18f, a.z + side * width), new Vector3(length, 0.16f, 0.12f), timber, false);
                Unstatic(stringer);
                pieces.Add(stringer.transform);
            }
            float mid = (a.x + b.x) * 0.5f;
            for (float x = a.x + 0.15f; x < b.x - 0.1f; x += 0.3f)
            {
                if (Mathf.Abs(x - mid) < 2.5f && Chance(0.18))
                    continue; // a missing board or two, showing the drop
                float sag = 0.06f * (1f - Mathf.Abs(x - mid) / (length * 0.5f));
                var board = Box("Board", visuals, new Vector3(x, -0.04f - sag, a.z + Rand(-0.05f, 0.05f)), new Vector3(0.26f, 0.06f, width + Rand(-0.1f, 0.15f)),
                    Chance(0.3) ? roofBoards : plank, false, Quaternion.Euler(Rand(-2f, 2f), Rand(-4f, 4f), Rand(-3f, 3f)));
                Unstatic(board);
                pieces.Add(board.transform);
            }

            var trap = bridge.gameObject.AddComponent<CollapsingFloor>();
            SerializedFields.Set(trap, "surface", surface);
            SerializedFields.SetArray(trap, "pieces", pieces);
            SerializedFields.SetFloat(trap, "breakDelay", 1.6f);
            SerializedFields.SetFloat(trap, "warnDistance", 3.5f);
        }

        /// <summary>Rotten boards patched into an upper floor. They drop you one storey: it hurts, it doesn't kill.</summary>
        static void RottenFloor(Transform parent, Rect area, float y)
        {
            var patch = new GameObject("RottenFloor").transform;
            patch.SetParent(parent, false);
            var surface = patch.gameObject.AddComponent<BoxCollider>();
            surface.center = new Vector3(area.center.x, y - 0.1f, area.center.y);
            surface.size = new Vector3(area.width, 0.2f, area.height);

            var pieces = new List<Object>();
            int i = 0;
            for (float x = area.xMin + 0.13f; x < area.xMax; x += 0.25f, i++)
            {
                if (i == 5)
                    continue; // one board already gone: you can see the floor below through it
                var board = Box("RottenBoard", patch, new Vector3(x, y - 0.05f - Rand(0f, 0.04f), area.center.y), new Vector3(0.23f, 0.08f, area.height - Rand(0f, 0.3f)),
                    Chance(0.5) ? roofBoards : soot, false, Quaternion.Euler(Rand(-2f, 2f), 0f, Rand(-3f, 3f)));
                Unstatic(board);
                pieces.Add(board.transform);
            }

            var trap = patch.gameObject.AddComponent<CollapsingFloor>();
            SerializedFields.Set(trap, "surface", surface);
            SerializedFields.SetArray(trap, "pieces", pieces);
            SerializedFields.SetFloat(trap, "breakDelay", 0.55f); // short: it has to catch someone walking across 3 m
        }

        static void SetVector(Object target, string field, Vector3 value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Unstatic(GameObject go) => GameObjectUtility.SetStaticEditorFlags(go, 0);
    }
}
