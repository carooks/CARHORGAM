using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Rough block-out of the Route 94 area of Wadsworth, OH (see Docs/Reference/wadsworth_map.png),
    /// compressed to about 170 m square. The facility stands in the industrial lot west of 94 with the
    /// pond to its north-west; houses line the side streets east of 94. Distances are not to scale.
    /// x points east, z points north, 1 unit = 1 metre.
    /// </summary>
    public static class TownBlockoutBuilder
    {
        public const float HalfExtent = 85f;
        public const float Route94X = 40f;

        // Facility footprint and its parking lot, consumed by the scene builder too.
        public static readonly Vector3 FacilityOrigin = new Vector3(-30f, 0f, 0f);
        public static readonly Vector3 ExtractionPoint = new Vector3(5f, 0f, -6f);
        public static readonly Vector3 PlayerSpawn = new Vector3(8f, 0.1f, -9f);

        const float RoadWidth = 9f;

        struct Road
        {
            public string Name;
            public bool NorthSouth;
            public float Position; // x for north-south roads, z for east-west roads
            public float From, To;  // extent along the road
            public float Width;
            public float Height;
        }

        static readonly Road[] Roads =
        {
            new Road { Name = "Route94", NorthSouth = true, Position = Route94X, From = -HalfExtent, To = HalfExtent, Width = 10f, Height = 0.04f },
            new Road { Name = "WaterSt", Position = -16f, From = -HalfExtent, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "ChestnutSt", Position = 12f, From = Route94X, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "PineSt", Position = 31f, From = Route94X, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "ParkSt", Position = 50f, From = Route94X, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "GarfieldSt", Position = 58f, From = -40f, To = Route94X, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "WalnutSt", Position = -45f, From = -HalfExtent, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
            new Road { Name = "BergeySt", Position = -70f, From = -HalfExtent, To = HalfExtent, Width = RoadWidth, Height = 0.03f },
        };

        static readonly Rect Pond = Rect.MinMaxRect(-75f, 15f, -45f, 52f);

        public static void Build(Transform parent, GrayboxKit kit)
        {
            var root = GrayboxKit.Group("Town", parent).transform;
            var rng = new System.Random(94);
            var blocked = new List<Rect>();

            kit.Box("Ground", root, new Vector3(0f, -0.5f, 0f), new Vector3(HalfExtent * 2f + 10f, 1f, HalfExtent * 2f + 10f), kit.Ground);
            BuildRoads(root, kit, blocked);
            BuildParkingLot(root, kit, blocked);
            BuildPond(root, kit, blocked);
            BuildHouses(root, kit, rng, blocked);
            BuildStreetLights(root, kit);
            BuildTrees(root, kit, rng, blocked);
            BuildBounds(root);
        }

        static void BuildRoads(Transform root, GrayboxKit kit, List<Rect> blocked)
        {
            var group = GrayboxKit.Group("Roads", root).transform;
            foreach (var road in Roads)
            {
                float length = road.To - road.From;
                float mid = (road.From + road.To) * 0.5f;
                Vector3 center = road.NorthSouth
                    ? new Vector3(road.Position, road.Height - 0.05f, mid)
                    : new Vector3(mid, road.Height - 0.05f, road.Position);
                Vector3 size = road.NorthSouth ? new Vector3(road.Width, 0.1f, length) : new Vector3(length, 0.1f, road.Width);
                kit.Box(road.Name, group, center, size, kit.Asphalt);
                blocked.Add(ToRect(center, size, 2f));

                // Dashed centre line, skipped where it would cross Route 94.
                for (float s = road.From + 2f; s < road.To - 2f; s += 6f)
                {
                    if (!road.NorthSouth && Mathf.Abs(s + 1.5f - Route94X) < 7f)
                        continue;
                    Vector3 dash = road.NorthSouth
                        ? new Vector3(road.Position, road.Height + 0.005f, s + 1.5f)
                        : new Vector3(s + 1.5f, road.Height + 0.005f, road.Position);
                    Vector3 dashSize = road.NorthSouth ? new Vector3(0.15f, 0.02f, 3f) : new Vector3(3f, 0.02f, 0.15f);
                    kit.Box("Line", group, dash, dashSize, kit.RoadLine, false);
                }
            }
        }

        static void BuildParkingLot(Transform root, GrayboxKit kit, List<Rect> blocked)
        {
            var group = GrayboxKit.Group("ParkingLot", root).transform;
            float west = FacilityOrigin.x, east = FacilityOrigin.x + FacilityBuilder.Width;
            var lotCenter = new Vector3((west + east) * 0.5f, -0.015f, -6f);
            var lotSize = new Vector3(east - west, 0.1f, 12f);
            kit.Box("Lot", group, lotCenter, lotSize, kit.Asphalt);
            kit.Box("Driveway", group, new Vector3((east + Route94X) * 0.5f, -0.016f, -6f), new Vector3(Route94X - east, 0.1f, 7f), kit.Asphalt);
            blocked.Add(Rect.MinMaxRect(west - 3f, -14f, east + 3f, FacilityBuilder.Depth + 3f));
            blocked.Add(Rect.MinMaxRect(east, -11f, Route94X, -1f));

            for (int i = 0; i < 6; i++)
                kit.Box("StallLine", group, new Vector3(west + 6f + i * 4f, 0.04f, -8f), new Vector3(0.12f, 0.02f, 5f), kit.RoadLine, false);

            // The player's van doubles as the extraction point (wired up in Phase 2).
            var van = GrayboxKit.Group("Van", group).transform;
            Vector3 v = ExtractionPoint + new Vector3(4.5f, 0f, 0f);
            kit.Box("Body", van, v + new Vector3(0.4f, 1.35f, 0f), new Vector3(4.2f, 1.9f, 2.1f), kit.VanPaint);
            kit.Box("Cab", van, v + new Vector3(-2.2f, 1.0f, 0f), new Vector3(1.2f, 1.3f, 2.0f), kit.VanPaint);
            kit.Box("Windshield", van, v + new Vector3(-2.81f, 1.35f, 0f), new Vector3(0.02f, 0.5f, 1.7f), kit.Water, false);
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                kit.Box("Wheel", van, v + new Vector3(x * 1.6f, 0.35f, z * 1.0f), new Vector3(0.7f, 0.7f, 0.3f), kit.Asphalt);

            var extraction = GrayboxKit.Group("ExtractionPoint", root);
            extraction.transform.position = ExtractionPoint;
            kit.Box("Pad", extraction.transform, ExtractionPoint + new Vector3(0f, 0.045f, 0f), new Vector3(2.6f, 0.02f, 2.6f), kit.ExtractionGlow, false);
            GrayboxKit.PointLight("PadLight", extraction.transform, ExtractionPoint + Vector3.up * 1.5f, new Color(0.3f, 1f, 0.5f), 2f, 5f);
        }

        static void BuildPond(Transform root, GrayboxKit kit, List<Rect> blocked)
        {
            var group = GrayboxKit.Group("Pond", root).transform;
            Vector3 center = new Vector3(Pond.center.x, 0.02f, Pond.center.y);
            kit.Box("Water", group, center, new Vector3(Pond.width, 0.02f, Pond.height), kit.Water, false);
            // Knee-high bank so the player can't wade across the graybox water.
            float h = 0.6f;
            kit.Box("BankN", group, new Vector3(center.x, h * 0.5f, Pond.yMax), new Vector3(Pond.width, h, 0.6f), kit.Concrete);
            kit.Box("BankS", group, new Vector3(center.x, h * 0.5f, Pond.yMin), new Vector3(Pond.width, h, 0.6f), kit.Concrete);
            kit.Box("BankW", group, new Vector3(Pond.xMin, h * 0.5f, center.z), new Vector3(0.6f, h, Pond.height), kit.Concrete);
            kit.Box("BankE", group, new Vector3(Pond.xMax, h * 0.5f, center.z), new Vector3(0.6f, h, Pond.height), kit.Concrete);
            GrayboxKit.InvisibleWall("PondBlocker", group, center + Vector3.up * 1.5f, new Vector3(Pond.width - 0.6f, 3f, Pond.height - 0.6f));
            blocked.Add(Expand(Pond, 1.5f));
        }

        static void BuildHouses(Transform root, GrayboxKit kit, System.Random rng, List<Rect> blocked)
        {
            var group = GrayboxKit.Group("Houses", root).transform;
            int index = 0;

            // East of Route 94: rows of houses between the side streets, fronts facing west.
            (float south, float north)[] eastBlocks = { (-65.5f, -49.5f), (-40.5f, -20.5f), (-11.5f, 7.5f), (16.5f, 26.5f), (35.5f, 45.5f), (54.5f, 80f) };
            float[] eastColumns = { 53f, 66f, 78f };
            foreach (var block in eastBlocks)
            foreach (float x in eastColumns)
            {
                for (float z = block.south + 4.75f; z + 4.5f <= block.north; z += 12f)
                    House(group, kit, rng, blocked, new Vector3(x, 0f, z), facingWest: true, index++);
            }

            // West of 94, south of Water St: the State St / Walnut St neighbourhood.
            float[] westRows = { -30f, -58f };
            foreach (float z in westRows)
            for (float x = -75f; x <= 26f; x += 14f)
                House(group, kit, rng, blocked, new Vector3(x, 0f, z), facingWest: false, index++);
        }

        static void House(Transform parent, GrayboxKit kit, System.Random rng, List<Rect> blocked, Vector3 p, bool facingWest, int index)
        {
            float width = 7f + (float)rng.NextDouble() * 2f;   // across the front
            float depth = 6f + (float)rng.NextDouble() * 2f;
            float height = 3f + (float)rng.NextDouble() * 1.8f;
            Vector3 size = facingWest ? new Vector3(depth, height, width) : new Vector3(width, height, depth);

            var house = GrayboxKit.Group($"House_{index:00}", parent).transform;
            kit.Box("Walls", house, p + Vector3.up * (height * 0.5f), size, kit.HouseWall);

            // Gable roof: a cube turned 45 degrees about the ridge so its top half pokes out as a prism.
            float span = facingWest ? size.x : size.z;
            float s = span * 1.05f / Mathf.Sqrt(2f);
            Vector3 roofCenter = p + Vector3.up * height;
            if (facingWest)
                kit.Box("Roof", house, roofCenter, new Vector3(s, s, size.z + 0.4f), kit.Roof, false, Quaternion.Euler(0f, 0f, 45f));
            else
                kit.Box("Roof", house, roofCenter, new Vector3(size.x + 0.4f, s, s), kit.Roof, false, Quaternion.Euler(45f, 0f, 0f));

            // About a third of the houses have someone still awake.
            if (rng.NextDouble() < 0.35)
            {
                Vector3 front = facingWest ? Vector3.left : Vector3.forward;
                float frontOffset = (facingWest ? size.x : size.z) * 0.5f + 0.01f;
                Vector3 windowSize = facingWest ? new Vector3(0.02f, 1f, 1.2f) : new Vector3(1.2f, 1f, 0.02f);
                kit.Box("WindowLit", house, p + front * frontOffset + Vector3.up * 1.6f, windowSize, kit.WindowLit, false);
                if (rng.NextDouble() < 0.4)
                    GrayboxKit.PointLight("PorchLight", house, p + front * (frontOffset + 0.8f) + Vector3.up * 2.4f, new Color(1f, 0.75f, 0.45f), 2f, 6f);
            }

            blocked.Add(ToRect(p, size, 2f));
        }

        static void BuildStreetLights(Transform root, GrayboxKit kit)
        {
            var group = GrayboxKit.Group("StreetLights", root).transform;
            int i = 0;
            for (float z = -75f; z <= 75f; z += 25f, i++)
            {
                // Every third light along 94 is dead.
                StreetLight(group, kit, new Vector3(Route94X - 6.5f, 0f, z), 1f, i % 3 != 2, $"94_{i}");
            }
            StreetLight(group, kit, new Vector3(-22f, 0f, -11f), 1f, true, "Lot_W");
            StreetLight(group, kit, new Vector3(14f, 0f, -11f), 1f, false, "Lot_E");
        }

        static void StreetLight(Transform parent, GrayboxKit kit, Vector3 basePosition, float armDirection, bool working, string id)
        {
            const float poleHeight = 7f;
            var light = GrayboxKit.Group($"StreetLight_{id}", parent).transform;
            kit.Box("Pole", light, basePosition + Vector3.up * (poleHeight * 0.5f), new Vector3(0.2f, poleHeight, 0.2f), kit.Metal);
            Vector3 head = basePosition + new Vector3(armDirection * 1.4f, poleHeight - 0.1f, 0f);
            kit.Box("Arm", light, basePosition + new Vector3(armDirection * 0.7f, poleHeight - 0.1f, 0f), new Vector3(1.4f, 0.1f, 0.1f), kit.Metal, false);
            kit.Box("Head", light, head + Vector3.down * 0.1f, new Vector3(0.6f, 0.12f, 0.3f), working ? kit.LampSodium : kit.Metal, false);
            if (working)
                GrayboxKit.SpotLight("Light", light, head + Vector3.down * 0.2f, Quaternion.Euler(90f, 0f, 0f),
                    new Color(1f, 0.7f, 0.4f), 30f, 14f, 110f);
        }

        static void BuildTrees(Transform root, GrayboxKit kit, System.Random rng, List<Rect> blocked)
        {
            var group = GrayboxKit.Group("Trees", root).transform;
            Mesh cone = GrayboxKit.ConeMesh();
            int placed = 0;
            for (int attempt = 0; attempt < 1500 && placed < 110; attempt++)
            {
                var point = new Vector2((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f) * (HalfExtent - 3f);
                if (IsBlocked(point, blocked))
                    continue;

                float height = 6f + (float)rng.NextDouble() * 5f;
                float radius = height * 0.35f;
                var p = new Vector3(point.x, 0f, point.y);
                var tree = GrayboxKit.Group($"Tree_{placed:000}", group).transform;
                kit.Cylinder("Trunk", tree, p + Vector3.up * 1f, new Vector3(0.35f, 2f, 0.35f), kit.Bark);

                var crown = new GameObject("Crown");
                crown.transform.SetParent(tree, false);
                crown.transform.position = p + Vector3.up * 1.2f;
                crown.transform.localScale = new Vector3(radius * 2f, height - 1.2f, radius * 2f);
                crown.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                crown.AddComponent<MeshFilter>().sharedMesh = cone;
                crown.AddComponent<MeshRenderer>().sharedMaterial = kit.Foliage;
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(crown, UnityEditor.StaticEditorFlags.BatchingStatic);

                blocked.Add(new Rect(point.x - 2f, point.y - 2f, 4f, 4f));
                placed++;
            }
        }

        static void BuildBounds(Transform root)
        {
            var group = GrayboxKit.Group("MapBounds", root).transform;
            float e = HalfExtent, h = 10f, span = HalfExtent * 2f;
            GrayboxKit.InvisibleWall("North", group, new Vector3(0f, h * 0.5f, e), new Vector3(span, h, 1f));
            GrayboxKit.InvisibleWall("South", group, new Vector3(0f, h * 0.5f, -e), new Vector3(span, h, 1f));
            GrayboxKit.InvisibleWall("East", group, new Vector3(e, h * 0.5f, 0f), new Vector3(1f, h, span));
            GrayboxKit.InvisibleWall("West", group, new Vector3(-e, h * 0.5f, 0f), new Vector3(1f, h, span));
        }

        static bool IsBlocked(Vector2 point, List<Rect> blocked)
        {
            foreach (var rect in blocked)
                if (rect.Contains(point))
                    return true;
            return false;
        }

        static Rect ToRect(Vector3 center, Vector3 size, float margin)
        {
            return Rect.MinMaxRect(center.x - size.x * 0.5f - margin, center.z - size.z * 0.5f - margin,
                center.x + size.x * 0.5f + margin, center.z + size.z * 0.5f + margin);
        }

        static Rect Expand(Rect rect, float margin)
        {
            return Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);
        }
    }
}
