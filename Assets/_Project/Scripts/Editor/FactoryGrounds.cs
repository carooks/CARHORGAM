using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    // Everything outside the machine hall: receiving room, warehouse, the two brick wings and the courtyard.
    public static partial class FactoryInteriorBuilder
    {
        static readonly Color SodiumColor = new Color(1f, 0.62f, 0.3f);
        const float WarehouseX0 = 120f, WarehouseX1 = 150f, WarehouseZ0 = -6f, WarehouseZ1 = 30f, WarehouseHeight = 9f;
        const float DriveX0 = 36f, DriveX1 = 44f;

        // ------------------------------------------------------------------ receiving room

        static void BuildReceiving(Transform parent)
        {
            Box("Floor", parent, new Vector3(7f, -0.05f, -6f), new Vector3(14.5f, 0.1f, 12.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3(7f, LowRoomHeight + 0.1f, -6f), new Vector3(14.6f, 0.2f, 12.6f), roofBoards);

            Wall("Wall_West", parent, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f), LowRoomHeight, brick);
            Wall("Wall_South", parent, new Vector3(0f, 0f, -12f), new Vector3(14f, 0f, -12f), LowRoomHeight, brick,
                Opening.Door(7f, 4.6f, 4.6f));
            Wall("Wall_East", parent, new Vector3(14f, 0f, -12f), new Vector3(14f, 0f, 0f), LowRoomHeight, brick,
                Opening.Window(6f, 2.2f, 1.6f, 4.2f, boarded: true));

            // Roll-up door stuck 2.2 m open: no ducking for the player, but it reads as forced.
            var door = GrayboxKit.Group("RollUpDoor", parent).transform;
            Box("Slab", door, new Vector3(7f, 3.4f, -12f), new Vector3(4.6f, 2.4f, 0.12f), kit.Metal);
            for (int i = 0; i < 6; i++)
                Box("Rib", door, new Vector3(7f, 2.35f + i * 0.4f, -12.07f), new Vector3(4.6f, 0.05f, 0.03f), iron, false);
            Box("BottomBar", door, new Vector3(7f, 2.22f, -12f), new Vector3(4.6f, 0.06f, 0.16f), kit.Hazard, false);
            foreach (float x in new[] { 4.6f, 9.4f })
                Box("Guide", door, new Vector3(x, 2.3f, -11.8f), new Vector3(0.12f, 4.6f, 0.12f), iron, false);

            // Pallets of boxed matches along the west wall.
            for (int p = 0; p < 4; p++)
                Pallet(parent, new Vector3(1.4f, 0f, -10f + p * 2.4f), 0f, rng.Next(1, 5));

            // A toppled stack spilling cases across the floor.
            for (int i = 0; i < 5; i++)
                Box("SpilledCase", parent, new Vector3(3.2f + Rand(0f, 1.6f), 0.17f, -2.6f + Rand(-0.8f, 0.8f)),
                    new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, true, Quaternion.Euler(0f, Rand(0f, 90f), 0f));

            // Shipping desk, shelving and a hand truck.
            FactoryProps.Place(parent, new Vector3(12.8f, 0f, -4f), -90f, Synty.Desk,
                () => Box("Desk", parent, new Vector3(12.6f, 0.45f, -4f), new Vector3(0.9f, 0.9f, 2.2f), plank));
            Box("Scale", parent, new Vector3(12.6f, 0.95f, -3.4f), new Vector3(0.5f, 0.12f, 0.5f), kit.Metal, false);
            Box("Ledger", parent, new Vector3(12.5f, 0.92f, -4.5f), new Vector3(0.35f, 0.04f, 0.5f), paper, false,
                Quaternion.Euler(0f, 12f, 0f));
            FactoryProps.Place(parent, new Vector3(13.4f, 0f, -1.4f), -90f, Synty.MetalShelf, null);
            FactoryProps.Place(parent, new Vector3(13.6f, 2.2f, -6.5f), -90f, Synty.Clock, null, false);
            FactoryProps.Place(parent, new Vector3(13.65f, 1.6f, -2.8f), -90f, Synty.Corkboard, null, false);
            HandTruck(parent, new Vector3(9.5f, 0f, -7.5f), 30f);
            CrateStack(parent, new Vector3(11.2f, 0f, -9.6f));
            FactoryProps.Place(parent, new Vector3(4.5f, 0f, -5f), 20f, Synty.BoxPile, null);

            HangingLamp(parent, new Vector3(5f, 0f, -6f), LowRoomHeight, LampState.Working, "Receiving_A", 1.4f);
            HangingLamp(parent, new Vector3(10f, 0f, -3f), LowRoomHeight, LampState.Flickering, "Receiving_B", 1.4f);
        }

        static void Pallet(Transform parent, Vector3 c, float yaw, int layers)
        {
            var pallet = GrayboxKit.Group("Pallet", parent).transform;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Pallet", pallet, c + Vector3.up * 0.08f, new Vector3(1.2f, 0.15f, 1.0f), plank, false, rot);
            for (int y = 0; y < layers; y++)
            for (int x = 0; x < 2; x++)
            for (int z = 0; z < 2; z++)
            {
                if (y == layers - 1 && Chance(0.3))
                    continue;
                Box("Case", pallet, c + rot * new Vector3(-0.28f + x * 0.56f, 0.33f + y * 0.36f, -0.23f + z * 0.46f),
                    new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, false, rot * Quaternion.Euler(0f, Rand(-4f, 4f), 0f));
            }
            Blocker("PalletBlock", pallet, c + Vector3.up * (0.2f + layers * 0.18f), new Vector3(1.2f, 0.4f + layers * 0.36f, 1.0f), rot);
        }

        static void HandTruck(Transform parent, Vector3 p, float yaw)
        {
            var truck = GrayboxKit.Group("HandTruck", parent).transform;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Bed", truck, p + rot * new Vector3(0f, 0.35f, 0f), new Vector3(1.0f, 0.08f, 0.7f), kit.Metal, true, rot);
            Box("Handle", truck, p + rot * new Vector3(-0.5f, 0.75f, 0f), new Vector3(0.05f, 0.8f, 0.6f), kit.Metal, false, rot);
            foreach (float x in new[] { -0.35f, 0.35f })
            foreach (float z in new[] { -0.3f, 0.3f })
                Cyl("Wheel", truck, p + rot * new Vector3(x, 0.15f, z), 0.3f, 0.08f, rubber, rot * Vector3.forward);
            Box("Case", truck, p + rot * new Vector3(0.1f, 0.56f, 0f), new Vector3(0.52f, 0.34f, 0.42f), kit.Cardboard, false, rot);
        }

        // ------------------------------------------------------------------ warehouse

        static void BuildWarehouse(Transform parent)
        {
            float cx = (WarehouseX0 + WarehouseX1) * 0.5f, cz = (WarehouseZ0 + WarehouseZ1) * 0.5f;
            float w = WarehouseX1 - WarehouseX0, d = WarehouseZ1 - WarehouseZ0;
            Box("Floor", parent, new Vector3(cx, -0.05f, cz), new Vector3(w + 0.5f, 0.1f, d + 0.5f), kit.ConcreteFloor);

            // South: roll-up door to the courtyard (open), high windows. East and north: high windows only.
            var south = new List<Opening> { Opening.Door(15f, 4f, 4.4f) };
            foreach (float c in new[] { 5f, 25f })
                south.Add(Opening.Window(c, 3f, 5.6f, 7.8f, boarded: Chance(0.3)));
            Wall("Wall_South", parent, new Vector3(WarehouseX0, 0f, WarehouseZ0), new Vector3(WarehouseX1, 0f, WarehouseZ0),
                WarehouseHeight, brickDark, south.ToArray());
            var east = new List<Opening>();
            for (float c = 4f; c < d - 2f; c += 6f)
                east.Add(Opening.Window(c, 3f, 5.6f, 7.8f, boarded: Chance(0.25)));
            Wall("Wall_East", parent, new Vector3(WarehouseX1, 0f, WarehouseZ0), new Vector3(WarehouseX1, 0f, WarehouseZ1),
                WarehouseHeight, brickDark, east.ToArray());
            Wall("Wall_North", parent, new Vector3(WarehouseX0, 0f, WarehouseZ1), new Vector3(WarehouseX1, 0f, WarehouseZ1),
                WarehouseHeight, brickDark, Opening.Window(9f, 3f, 5.6f, 7.8f), Opening.Window(21f, 3f, 5.6f, 7.8f, boarded: true));
            // West: the hall's east wall stands in for Z 0..24; fill the ends and the strip above the hall eave.
            Wall("Wall_West_S", parent, new Vector3(WarehouseX0, 0f, WarehouseZ0), new Vector3(WarehouseX0, 0f, 0f), WarehouseHeight, brickDark);
            Wall("Wall_West_N", parent, new Vector3(WarehouseX0, 0f, HallWidth), new Vector3(WarehouseX0, 0f, WarehouseZ1), WarehouseHeight, brickDark);
            Box("Wall_West_Top", parent, new Vector3(WarehouseX0, EaveHeight + (WarehouseHeight - EaveHeight) * 0.5f, HallWidth * 0.5f),
                new Vector3(WallThickness, WarehouseHeight - EaveHeight, HallWidth), brickDark);

            // Roll-up door slab, rolled most of the way up.
            Box("RollUpDoor", parent, new Vector3(135f, 4.0f, WarehouseZ0 - 0.05f), new Vector3(4f, 0.9f, 0.14f), kit.Metal);
            foreach (float x in new[] { 132.9f, 137.1f })
                Box("Guide", parent, new Vector3(x, 2.2f, WarehouseZ0 + 0.2f), new Vector3(0.12f, 4.4f, 0.12f), iron, false);

            // Flat roof of 6 m panels on steel beams, with three skylights broken out.
            var roof = GrayboxKit.Group("Roof", parent).transform;
            var holes = new HashSet<int> { 1 * 10 + 1, 3 * 10 + 3, 2 * 10 + 5 };
            for (int ix = 0; ix < 5; ix++)
            for (int iz = 0; iz < 6; iz++)
            {
                Vector3 c = new Vector3(WarehouseX0 + ix * 6f + 3f, WarehouseHeight + 0.1f, WarehouseZ0 + iz * 6f + 3f);
                if (holes.Contains(ix * 10 + iz))
                {
                    // Skylight frame with a few shards of glass left in it, and moonlight pouring through.
                    foreach (float s in new[] { -2.9f, 2.9f })
                    {
                        Box("SkylightFrame", roof, c + new Vector3(s, 0f, 0f), new Vector3(0.2f, 0.25f, 6f), iron);
                        Box("SkylightFrame", roof, c + new Vector3(0f, 0f, s), new Vector3(6f, 0.25f, 0.2f), iron);
                    }
                    Box("Shard", roof, c + new Vector3(-2.2f, -0.02f, 1.5f), new Vector3(1.2f, 0.03f, 2.4f), glassDark, false,
                        Quaternion.Euler(0f, 0f, 6f));
                    MoonShaft("MoonShaft", roof, c, MoonRotation * Vector3.forward, 17f, 3.2f);
                    for (int r = 0; r < 4; r++)
                        Box("FallenGlass", parent, new Vector3(c.x + Rand(-2f, 2f), 0.01f, c.z + Rand(-2f, 2f)),
                            new Vector3(Rand(0.2f, 0.6f), 0.02f, Rand(0.2f, 0.5f)), glassDark, false, Quaternion.Euler(0f, Rand(0f, 90f), 0f));
                    continue;
                }
                Box("RoofPanel", roof, c, new Vector3(6f, 0.2f, 6f), roofBoards);
            }
            for (int ix = 1; ix < 5; ix++)
                Box("RoofBeam", roof, new Vector3(WarehouseX0 + ix * 6f, WarehouseHeight - 0.3f, cz), new Vector3(0.3f, 0.6f, d), iron, false);
            for (int iz = 1; iz < 6; iz++)
            for (int ix = 1; ix < 5; ix++)
            {
                float x = WarehouseX0 + ix * 6f, z = WarehouseZ0 + iz * 6f;
                if (Mathf.Abs(z - 12.5f) < 1.5f) // cross aisle stays clear
                    continue;
                if (x > 122f && x < 148f && System.Array.IndexOf(new[] { 125f, 131f, 137f, 143f }, x) < 0)
                    Box("Column", roof, new Vector3(x, WarehouseHeight * 0.5f, z), new Vector3(0.3f, WarehouseHeight, 0.3f), kit.Metal);
            }

            // Pallet racking in four rows, split by a cross aisle at Z 12.5. The far north-east run has collapsed.
            float[] rowX = { 125f, 131f, 137f, 143f };
            for (int r = 0; r < rowX.Length; r++)
            {
                Rack(parent, rowX[r], -2f, 11f, false);
                Rack(parent, rowX[r], 14f, 27f, r == 3);
            }

            // Forklift-ish junk and loose pallets in the aisles.
            Pallet(parent, new Vector3(128f, 0f, 5f), 90f, 2);
            Pallet(parent, new Vector3(140f, 0f, 20f), 80f, 3);
            Pallet(parent, new Vector3(146.5f, 0f, -3f), 10f, 1);
            HandTruck(parent, new Vector3(134f, 0f, 13f), 120f);
            for (int i = 0; i < 8; i++)
                FactoryProps.Place(parent, new Vector3(Rand(122f, 148f), 0f, Rand(11.6f, 13.4f)), Rand(0f, 360f),
                    i % 2 == 0 ? Synty.Papers2 : Synty.CardboardJunk, null, false);

            // Two lamps on the breaker, both failing; the rest are dead.
            string pattern = "DFDDFD";
            for (int i = 0; i < pattern.Length; i++)
                HangingLamp(parent, new Vector3(i % 2 == 0 ? 128f : 140f, 0f, 1f + (i / 2) * 12f), WarehouseHeight,
                    ToState(pattern[i]), $"Warehouse_{i}", 2.5f);
        }

        /// <summary>One run of pallet racking along Z at <paramref name="x"/>: uprights, beams at four levels, loads.</summary>
        static void Rack(Transform parent, float x, float z0, float z1, bool collapsed)
        {
            var rack = GrayboxKit.Group(collapsed ? "Rack_Collapsed" : "Rack", parent).transform;
            const float depth = 1.1f;
            float[] levels = { 0.15f, 2.1f, 4.05f, 6f };
            if (collapsed)
            {
                // Uprights folded over to the west, loads spilled across the aisle.
                for (float z = z0; z <= z1 + 0.01f; z += 2.6f)
                    Box("BentUpright", rack, new Vector3(x - 1.6f, 2.6f, z), new Vector3(0.12f, 6.4f, 0.12f), kit.Hazard, true,
                        Quaternion.Euler(0f, 0f, 35f + Rand(-6f, 6f)));
                Beam("Beam", rack, new Vector3(x - 3.6f, 5.3f, z0), new Vector3(x - 3.3f, 5.1f, z1), 0.1f, 0.15f, kit.Hazard);
                for (int i = 0; i < 14; i++)
                {
                    var p = new Vector3(x + Rand(-4.5f, 0.8f), 0f, Rand(z0, z1));
                    if (!FactoryProps.Place(rack, p, Rand(0f, 360f), i % 3 == 0 ? Synty.CrateMetalOpen : i % 3 == 1 ? Synty.Cardboard : Synty.CrateWood2))
                        Box("SpilledCrate", rack, p + Vector3.up * 0.4f, Vector3.one * 0.8f, kit.Crate, true,
                            Quaternion.Euler(Rand(-20f, 20f), Rand(0f, 90f), Rand(-20f, 20f)));
                }
                return;
            }

            for (float z = z0; z <= z1 + 0.01f; z += 2.6f)
            foreach (float side in new[] { -depth * 0.5f, depth * 0.5f })
                Box("Upright", rack, new Vector3(x + side, 3.3f, z), new Vector3(0.1f, 6.6f, 0.1f), kit.Hazard, false);
            foreach (float y in levels)
            foreach (float side in new[] { -depth * 0.5f, depth * 0.5f })
                Box("Beam", rack, new Vector3(x + side, y + 0.06f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.12f, z1 - z0), kit.Hazard, false);
            Blocker("RackBlock", rack, new Vector3(x, 3.3f, (z0 + z1) * 0.5f), new Vector3(depth + 0.1f, 6.6f, z1 - z0 + 0.1f));

            foreach (float y in levels)
            for (float z = z0 + 1.3f; z < z1; z += 2.6f)
            {
                if (Chance(0.3))
                    continue;
                var p = new Vector3(x, y + 0.12f, z);
                string[] load = Chance(0.5) ? Synty.CrateWood : Chance(0.5) ? Synty.BoxPile : Synty.Cardboard2;
                if (FactoryProps.Place(rack, p, Chance(0.5) ? 0f : 90f, load, null, false) == null)
                {
                    Box("Pallet", rack, p + Vector3.up * 0.07f, new Vector3(1f, 0.14f, 1.2f), plank, false);
                    Box("Load", rack, p + Vector3.up * 0.65f, new Vector3(0.9f, Rand(0.6f, 1.4f) * 0.8f, 1.1f), kit.Cardboard, false);
                }
            }
        }

        // ------------------------------------------------------------------ brick wings

        /// <summary>
        /// A three-storey brick wing between <paramref name="x0"/> and <paramref name="x1"/>, Z -40..-24.
        /// Ground-floor doors are given as offsets along each wall. Floors at or above
        /// <paramref name="walkableFloors"/> get dark glass instead of open windows.
        /// </summary>
        static void WingShell(Transform parent, float x0, float x1, int walkableFloors,
            float[] northDoors, float westDoor, float eastDoor)
        {
            float z0 = WingSouth, z1 = WingNorth;
            float length = x1 - x0, depth = z1 - z0;
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            const int floors = 3;
            const float parapet = 1.1f;

            Box("Floor", parent, new Vector3(cx, -0.05f, cz), new Vector3(length + 0.5f, 0.1f, depth + 0.5f), kit.ConcreteFloor);

            List<Opening> Row(float wallLength, int floor, float[] doors)
            {
                var list = new List<Opening>();
                if (floor == 0 && doors != null)
                    foreach (float door in doors)
                        list.Add(Opening.Door(door, 1.6f, 2.6f));
                for (float c = 2.5f; c < wallLength - 1.5f; c += 4f)
                {
                    bool nearDoor = false;
                    if (floor == 0 && doors != null)
                        foreach (float door in doors)
                            nearDoor |= Mathf.Abs(c - door) < 2.3f;
                    if (nearDoor)
                        continue;
                    list.Add(floor >= walkableFloors
                        ? Opening.Blind(c, 1.5f, 1.0f, 3.1f)
                        : Opening.Window(c, 1.5f, 1.0f, 3.1f, boarded: floor == 0 && Chance(0.25)));
                }
                return list;
            }

            for (int f = 0; f < floors; f++)
            {
                float y = f * StoreyHeight;
                float h = f == floors - 1 ? StoreyHeight + parapet : StoreyHeight;
                Wall($"Wall_North_{f}", parent, new Vector3(x0, y, z1), new Vector3(x1, y, z1), h, brick, Row(length, f, northDoors).ToArray());
                Wall($"Wall_South_{f}", parent, new Vector3(x0, y, z0), new Vector3(x1, y, z0), h, brick, Row(length, f, null).ToArray());
                Wall($"Wall_West_{f}", parent, new Vector3(x0, y, z0), new Vector3(x0, y, z1), h, brick,
                    Row(depth, f, float.IsNaN(westDoor) ? null : new[] { westDoor }).ToArray());
                Wall($"Wall_East_{f}", parent, new Vector3(x1, y, z0), new Vector3(x1, y, z1), h, brick,
                    Row(depth, f, float.IsNaN(eastDoor) ? null : new[] { eastDoor }).ToArray());

                // Stone belt course at each floor line and a coping on the parapet.
                float band = f == 0 ? 0.3f : y;
                StoneBand(parent, x0, x1, z0, z1, band);
                if (f == floors - 1)
                    StoneBand(parent, x0, x1, z0, z1, y + h);

                // Cast-iron mill columns down the middle.
                for (float x = x0 + 6f; x < x1 - 1f; x += 6f)
                    Cyl("MillColumn", parent, new Vector3(x, y + StoreyHeight * 0.5f, cz), 0.28f, StoreyHeight, iron, Vector3.up, true);
            }
            Box("Roof", parent, new Vector3(cx, floors * StoreyHeight + 0.1f, cz), new Vector3(length, 0.2f, depth), roofBoards);
            Box("RoofBeam", parent, new Vector3(cx, floors * StoreyHeight - 0.2f, cz), new Vector3(length, 0.3f, 0.3f), timber, false);
        }

        static void StoneBand(Transform parent, float x0, float x1, float z0, float z1, float y)
        {
            const float t = WallThickness + 0.2f;
            Box("Band", parent, new Vector3((x0 + x1) * 0.5f, y, z0), new Vector3(x1 - x0 + t, 0.2f, t), stone, false);
            Box("Band", parent, new Vector3((x0 + x1) * 0.5f, y, z1), new Vector3(x1 - x0 + t, 0.2f, t), stone, false);
            Box("Band", parent, new Vector3(x0, y, (z0 + z1) * 0.5f), new Vector3(t, 0.2f, z1 - z0 + t), stone, false);
            Box("Band", parent, new Vector3(x1, y, (z0 + z1) * 0.5f), new Vector3(t, 0.2f, z1 - z0 + t), stone, false);
        }

        /// <summary>A floor slab between two storeys, optionally with a rectangular hole (for a stair).</summary>
        static void Slab(Transform parent, string name, float y, Rect area, Rect? hole, Material top)
        {
            var group = GrayboxKit.Group(name, parent).transform;
            void Piece(float xMin, float xMax, float zMin, float zMax)
            {
                if (xMax - xMin < 0.01f || zMax - zMin < 0.01f)
                    return;
                Box("Slab", group, new Vector3((xMin + xMax) * 0.5f, y - 0.1f, (zMin + zMax) * 0.5f),
                    new Vector3(xMax - xMin, 0.2f, zMax - zMin), top);
                Box("Joists", group, new Vector3((xMin + xMax) * 0.5f, y - 0.3f, (zMin + zMax) * 0.5f),
                    new Vector3(xMax - xMin, 0.2f, zMax - zMin), roofBoards, false);
            }
            if (!hole.HasValue)
            {
                Piece(area.xMin, area.xMax, area.yMin, area.yMax);
                return;
            }
            Rect h = hole.Value;
            Piece(area.xMin, area.xMax, area.yMin, h.yMin);       // south strip
            Piece(area.xMin, area.xMax, h.yMax, area.yMax);       // north strip
            Piece(area.xMin, h.xMin, h.yMin, h.yMax);             // west of hole
            Piece(h.xMax, area.xMax, h.yMin, h.yMax);             // east of hole
        }

        /// <summary>A partition wall across a wing at <paramref name="x"/>, one storey high, door in the middle.</summary>
        static void Partition(Transform parent, float x, float y, Material material)
        {
            Wall($"Partition_{x:0}", parent, new Vector3(x, y, WingSouth + WallThickness * 0.5f), new Vector3(x, y, WingNorth - WallThickness * 0.5f),
                StoreyHeight - 0.2f, material, Opening.Door(7.75f, 1.2f, 2.3f));
        }

        static void BuildWestWing(Transform parent)
        {
            const float x0 = -10f, x1 = DriveX0;
            // Doors: north into the courtyard at X 20, east onto the drive at Z -30.
            WingShell(parent, x0, x1, 2, new[] { 30f }, float.NaN, 10f);
            float inner0 = x0 + WallThickness * 0.5f, inner1 = x1 - WallThickness * 0.5f;
            float zIn0 = WingSouth + WallThickness * 0.5f, zIn1 = WingNorth - WallThickness * 0.5f;

            // Floor 2 (walkable, timber) with a stair hole in the south-east corner; floor 3 sealed.
            var area = Rect.MinMaxRect(inner0, zIn0, inner1, zIn1);
            var hole = Rect.MinMaxRect(25.5f, zIn0, 34f, -37.9f);
            Slab(parent, "Floor2", StoreyHeight, area, hole, plank);
            Slab(parent, "Floor3", StoreyHeight * 2f, area, null, plank);
            Stairs("Stairs", parent, new Vector3(26f, 0f, -38.9f), Vector3.right, 8f, StoreyHeight, 1.6f, timber);
            Railing("StairRail_N", parent, new Vector3(25.5f, StoreyHeight, -37.9f), new Vector3(34f, StoreyHeight, -37.9f), iron);
            Railing("StairRail_W", parent, new Vector3(25.5f, StoreyHeight, -37.9f), new Vector3(25.5f, StoreyHeight, zIn0), iron);
            Box("StairStringer", parent, new Vector3(30f, 2.1f, -38.05f), new Vector3(8.2f, 0.15f, 0.08f), timber, false,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(StoreyHeight, 8f) * Mathf.Rad2Deg));

            // Floor 1: the packing room. Long tables of cartons, stacked cases, a time clock by the door.
            var f1 = GrayboxKit.Group("Floor1_Packing", parent).transform;
            for (int t = 0; t < 4; t++)
            {
                float x = -4f + t * 7f;
                foreach (float z in new[] { -35f, -29f })
                {
                    if (x > 22f && z < -32f)
                        continue; // stair
                    if (FactoryProps.Place(f1, new Vector3(x, 0f, z), 90f, Synty.Table, null) == null)
                        Box("PackingTable", f1, new Vector3(x, 0.45f, z), new Vector3(1f, 0.9f, 3.2f), plank);
                    for (int b = 0; b < rng.Next(1, 5); b++)
                        Box("Carton", f1, new Vector3(x + Rand(-0.3f, 0.3f), 1.07f, z + Rand(-1.3f, 1.3f)),
                            new Vector3(0.45f, 0.3f, 0.35f), kit.Cardboard, false, Quaternion.Euler(0f, Rand(-30f, 30f), 0f));
                    FactoryProps.Place(f1, new Vector3(x - 0.9f, 0f, z + Rand(-1f, 1f)), Rand(60f, 120f), Synty.Chair, null);
                }
            }
            for (int p = 0; p < 3; p++)
                Pallet(f1, new Vector3(-8.4f, 0f, -38f + p * 2.4f), 0f, rng.Next(2, 5));
            CrateStack(f1, new Vector3(-8.6f, 0f, -27.5f));
            FactoryProps.Place(f1, new Vector3(34.6f, 1.5f, -27f), -90f, Synty.Clock, null, false);
            FactoryProps.Place(f1, new Vector3(18f, 0f, -24.8f), 180f, Synty.Bin, null);
            FactoryProps.Place(f1, new Vector3(10f, 0f, -38f), 0f, Synty.WorkShelf, null);
            HangingLamp(f1, new Vector3(12f, 0f, -32f), StoreyHeight, LampState.Flickering, "WestF1_A", 1f, false);
            HangingLamp(f1, new Vector3(-2f, 0f, -32f), StoreyHeight, LampState.Dead, "WestF1_B", 1f, false);
            HangingLamp(f1, new Vector3(26f, 0f, -30f), StoreyHeight, LampState.Dead, "WestF1_C", 1f, false);

            // Floor 2: locker room (west), offices (middle), stair hall (east).
            float y2 = StoreyHeight;
            var f2 = GrayboxKit.Group("Floor2", parent).transform;
            Partition(f2, 6f, y2, brickDark);
            Partition(f2, 22f, y2, brickDark);

            for (int i = 0; i < 7; i++)
            {
                float x = -8.5f + i * 0.75f;
                if (FactoryProps.Place(f2, new Vector3(x, y2, zIn1 - 0.4f), 180f, Synty.Locker, null) == null)
                    Box("Locker", f2, new Vector3(x, y2 + 0.95f, zIn1 - 0.3f), new Vector3(0.7f, 1.9f, 0.5f), enamel);
            }
            FactoryProps.Place(f2, new Vector3(-4f, y2, -32f), 90f, Synty.Bench, null);
            FactoryProps.Place(f2, new Vector3(2f, y2, -38.8f), 0f, Synty.Locker, null);
            FactoryProps.Place(f2, new Vector3(2.8f, y2, -38.8f), Rand(-20f, 20f), Synty.Locker, null);
            FactoryProps.Place(f2, new Vector3(0f, y2, -36f), 0f, Synty.Blood, null, false);

            Vector3[] desks = { new Vector3(10f, y2, -36f), new Vector3(16f, y2, -36f), new Vector3(10f, y2, -28f), new Vector3(17f, y2, -29f) };
            foreach (var p in desks)
            {
                if (FactoryProps.Place(f2, p, Rand(-10f, 10f), Chance(0.5) ? Synty.Desk : Synty.Desk2, null) == null)
                    Box("Desk", f2, p + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.8f), timber);
                FactoryProps.Place(f2, p + new Vector3(Rand(-0.3f, 0.3f), 0f, -1f), Rand(-40f, 40f), Synty.OfficeChair, null);
                FactoryProps.Place(f2, p + new Vector3(Rand(-1.5f, 1.5f), 0f, Rand(1f, 2f)), Rand(0f, 360f), Synty.Papers, null, false);
            }
            FactoryProps.Place(f2, new Vector3(21.3f, y2, -38f), -90f, Synty.FilingCabinet, null);
            FactoryProps.Place(f2, new Vector3(21.3f, y2, -37.2f), -90f, Synty.FilingCabinet2, null);
            FactoryProps.Place(f2, new Vector3(14f, y2 + 1.6f, zIn1 - 0.05f), 180f, Synty.Corkboard, null, false);
            FactoryProps.Place(f2, new Vector3(16.2f, y2 + 0.8f, -36f), 0f, Synty.Phone, null, false);
            CrateStack(f2, new Vector3(30f, y2, -27f));

            HangingLamp(f2, new Vector3(-2f, y2, -32f), StoreyHeight, LampState.Dead, "WestF2_Lockers", 1f, false);
            HangingLamp(f2, new Vector3(14f, y2, -32f), StoreyHeight, LampState.Working, "WestF2_Office", 1f, false);
            HangingLamp(f2, new Vector3(29f, y2, -31f), StoreyHeight, LampState.Dead, "WestF2_Stair", 1f, false);
        }

        static void BuildEastWing(Transform parent)
        {
            const float x0 = DriveX1, x1 = 96f;
            // Doors: north into the courtyard at X 60, west onto the drive at Z -32.
            WingShell(parent, x0, x1, 1, new[] { 16f }, 8f, float.NaN);
            float inner0 = x0 + WallThickness * 0.5f, inner1 = x1 - WallThickness * 0.5f;
            float zIn0 = WingSouth + WallThickness * 0.5f, zIn1 = WingNorth - WallThickness * 0.5f;
            var area = Rect.MinMaxRect(inner0, zIn0, inner1, zIn1);
            Slab(parent, "Floor2", StoreyHeight, area, null, kit.Concrete);
            Slab(parent, "Floor3", StoreyHeight * 2f, area, null, kit.Concrete);
            Partition(parent, 70f, 0f, brickDark);

            // Canteen (X 44..70): tables and chairs, some knocked over, a dead vending machine.
            var canteen = GrayboxKit.Group("Canteen", parent).transform;
            for (int t = 0; t < 6; t++)
            {
                var p = new Vector3(49f + (t % 3) * 6.5f, 0f, t < 3 ? -36f : -28.5f);
                if (FactoryProps.Place(canteen, p, Rand(-8f, 8f), Synty.Table, null) == null)
                    Box("Table", canteen, p + Vector3.up * 0.38f, new Vector3(2.4f, 0.76f, 1f), timber);
                for (int c = 0; c < 4; c++)
                {
                    float side = c < 2 ? -0.9f : 0.9f;
                    bool toppled = Chance(0.2);
                    FactoryProps.Place(canteen, p + new Vector3(-0.6f + (c % 2) * 1.2f, 0f, side),
                        toppled ? Quaternion.Euler(90f, Rand(0f, 360f), 0f) : Quaternion.Euler(0f, side < 0 ? 0f : 180f, 0f), Synty.Chair, null);
                }
            }
            FactoryProps.Place(canteen, new Vector3(68.8f, 0f, -39f), -90f, Synty.Vending, null);
            FactoryProps.Place(canteen, new Vector3(45f, 0f, -39.2f), 0f, Synty.Bin, null);
            FactoryProps.Place(canteen, new Vector3(64f, 2f, zIn1 - 0.05f), 180f, Synty.Clock, null, false);
            Box("Counter", canteen, new Vector3(66f, 0.5f, -26f), new Vector3(5f, 1f, 0.8f), timber);

            // Locker room (X 70..96): rows of lockers, benches, a shower wall.
            var lockers = GrayboxKit.Group("LockerRoom", parent).transform;
            foreach (float z in new[] { -35f, -29f })
            {
                for (int i = 0; i < 12; i++)
                {
                    float x = 74f + i * 0.75f;
                    if (FactoryProps.Place(lockers, new Vector3(x, 0f, z), z < -32f ? 0f : 180f, Synty.Locker, null) == null)
                        Box("Locker", lockers, new Vector3(x, 0.95f, z), new Vector3(0.7f, 1.9f, 0.5f), enamel);
                }
                FactoryProps.Place(lockers, new Vector3(78f, 0f, z + (z < -32f ? 1.2f : -1.2f)), 90f, Synty.Bench, null);
            }
            Box("ShowerWall", lockers, new Vector3(92f, 1.1f, -32f), new Vector3(0.15f, 2.2f, 10f), enamel);
            FactoryProps.Place(lockers, new Vector3(86f, 0f, -32f), 0f, Synty.Blood, null, false);

            HangingLamp(parent, new Vector3(56f, 0f, -32f), StoreyHeight, LampState.Flickering, "EastF1_Canteen", 1f, false);
            HangingLamp(parent, new Vector3(64f, 0f, -32f), StoreyHeight, LampState.Dead, "EastF1_CanteenB", 1f, false);
            HangingLamp(parent, new Vector3(82f, 0f, -32f), StoreyHeight, LampState.Dead, "EastF1_Lockers", 1f, false);
        }

        // ------------------------------------------------------------------ courtyard

        static void BuildCourtyard(Transform parent)
        {
            Box("Ground", parent, new Vector3(70f, -0.07f, 0f), new Vector3(220f, 0.1f, 140f), kit.Ground);
            Box("Asphalt", parent, new Vector3(70f, -0.015f, -12f), new Vector3(160f, 0.02f, 24f), kit.Asphalt);
            Box("Drive", parent, new Vector3(40f, -0.015f, -36f), new Vector3(8f, 0.02f, 24f), kit.Asphalt);

            // Cracks and tar seams, weeds in them.
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector3(Rand(-9f, 149f), 0.002f, Rand(-23.5f, -0.5f));
                if (p.x > 0f && p.x < 14f && p.z > -12f)
                    continue;
                Box("Crack", parent, p, new Vector3(Rand(1f, 5f), 0.01f, Rand(0.04f, 0.12f)), asphaltCrack, false,
                    Quaternion.Euler(0f, Rand(0f, 180f), 0f));
                if (Chance(0.5))
                    Weeds(parent, p + new Vector3(Rand(-1f, 1f), 0f, Rand(-0.3f, 0.3f)));
            }
            // Weeds thickest along wall bases.
            for (int i = 0; i < 40; i++)
                Weeds(parent, new Vector3(Rand(15f, 119f), 0f, Rand(-1.2f, -0.4f)));
            for (int i = 0; i < 30; i++)
                Weeds(parent, new Vector3(Rand(-9f, 95f), 0f, Rand(-23.6f, -22.8f)));
            for (int i = 0; i < 12; i++)
                FactoryProps.Place(parent, new Vector3(Rand(-8f, 94f), 0f, -23.7f), 0f, Chance(0.5) ? Synty.Ivy : Synty.Ivy2, null, false);
            for (int i = 0; i < 12; i++)
                FactoryProps.Place(parent, new Vector3(Rand(15f, 118f), 0f, -0.3f), 180f, Chance(0.5) ? Synty.Ivy : Synty.Ivy2, null, false);

            // Dead trees inside the fence and beyond it, silhouettes in the fog.
            Vector3[] trees =
            {
                new Vector3(-7f, 0f, -8f), new Vector3(-6.5f, 0f, -19f), new Vector3(147f, 0f, -20f),
                new Vector3(-16f, 0f, -4f), new Vector3(-18f, 0f, -26f), new Vector3(104f, 0f, -32f),
                new Vector3(120f, 0f, -36f), new Vector3(158f, 0f, -12f), new Vector3(160f, 0f, 10f), new Vector3(28f, 0f, -45f),
                new Vector3(52f, 0f, -45f),
            };
            for (int i = 0; i < trees.Length; i++)
                FactoryProps.Place(parent, trees[i], Rand(0f, 360f), i % 3 == 0 ? Synty.TreeTwisted : i % 3 == 1 ? Synty.TreeDead : Synty.TreeDead2,
                    () => DeadTree(parent, trees[i]));
            for (int i = 0; i < 10; i++)
                FactoryProps.Place(parent, new Vector3(Rand(-9f, -3f), 0f, Rand(-23f, -2f)), Rand(0f, 360f), Chance(0.5) ? Synty.Bush : Synty.Shrub, null, false);
            for (int i = 0; i < 6; i++)
                FactoryProps.Place(parent, new Vector3(Rand(97f, 148f), 0f, Rand(-23f, -18f)), Rand(0f, 360f), Chance(0.5) ? Synty.Bush2 : Synty.Rock, null, false);

            // Vehicles left where they stopped.
            FactoryProps.Place(parent, new Vector3(70f, 0f, -12f), 90f, Synty.Truck, () => Trailer(parent, new Vector3(70f, 0f, -12f)));
            FactoryProps.Place(parent, new Vector3(135f, 0f, -15f), 0f, Synty.TruckDelivery, () => Trailer(parent, new Vector3(135f, 0f, -15f)));
            FactoryProps.Place(parent, new Vector3(12f, 0f, -19.5f), 80f, Synty.Van, null);

            // Dumpsters, bins, drums, pallets and bags.
            FactoryProps.Place(parent, new Vector3(-6f, 0f, -3f), 8f, Synty.Dumpster,
                () => Box("Dumpster", parent, new Vector3(-6f, 0.75f, -3f), new Vector3(3.2f, 1.5f, 1.8f), kit.Hazard, true, Quaternion.Euler(0f, 8f, 0f)));
            FactoryProps.Place(parent, new Vector3(90f, 0f, -2.4f), 182f, Synty.Dumpster,
                () => Box("Dumpster", parent, new Vector3(90f, 0.75f, -2.4f), new Vector3(3.2f, 1.5f, 1.8f), kit.Hazard, true));
            for (int i = 0; i < 10; i++)
            {
                var p = new Vector3(Rand(-8f, 2f), 0f, Rand(-6f, -1f));
                if (i >= 5)
                    p = new Vector3(Rand(86f, 94f), 0f, Rand(-4.5f, -1f));
                FactoryProps.Place(parent, p, Rand(0f, 360f), Chance(0.5) ? Synty.TrashBag : Synty.TrashBag2, null, false);
            }
            for (int i = 0; i < 3; i++)
                FactoryProps.Place(parent, new Vector3(12.5f + i * 0.7f, 0f, -13.2f + (i % 2) * 0.4f), Rand(0f, 360f), Synty.BarrelMetal,
                    () => Cyl("Drum", parent, new Vector3(12.5f, 0.45f, -13.2f), 0.6f, 0.9f, kit.Rust, Vector3.up, true));
            for (int i = 0; i < 4; i++)
                FactoryProps.Place(parent, new Vector3(108f + i * 0.75f, 0f, -1.2f + (i % 2) * 0.5f), Rand(0f, 360f),
                    i == 3 ? Synty.BarrelBroken : Synty.BarrelMetal2, null);
            Pallet(parent, new Vector3(24f, 0f, -2f), 10f, 0);
            Pallet(parent, new Vector3(25.3f, 0f, -2.2f), -5f, 2);
            Pallet(parent, new Vector3(118f, 0f, -9f), 30f, 1);
            CrateStack(parent, new Vector3(46f, 0f, -1.6f));
            CrateStack(parent, new Vector3(116f, 0f, -3f));
            FactoryProps.Place(parent, new Vector3(55f, 0f, -6f), 40f, Synty.RubblePlank, null);
            FactoryProps.Place(parent, new Vector3(28f, 0f, -16f), 0f, Synty.Plank, null, false);

            BuildSmokestack(parent, new Vector3(102f, 0f, -14f));
            BuildWaterTower(parent, new Vector3(60f, 0f, 50f));

            // Lighting: sodium pole lamps (two working, one flickering, one dead) and lamps over the doors.
            PoleLamp(parent, new Vector3(20f, 0f, -18f), LampState.Working, "A");
            PoleLamp(parent, new Vector3(55f, 0f, -16f), LampState.Dead, "B");
            PoleLamp(parent, new Vector3(85f, 0f, -18f), LampState.Working, "C");
            PoleLamp(parent, new Vector3(126f, 0f, -20f), LampState.Flickering, "D");
            DoorLamp(parent, new Vector3(7f, 4.6f, -12.5f), 180f, LampState.Working, "Receiving");
            DoorLamp(parent, new Vector3(BigDoorX, 6.2f, -0.5f), 180f, LampState.Working, "BigDoor");
            DoorLamp(parent, new Vector3(135f, 6f, WarehouseZ0 - 0.5f), 180f, LampState.Dead, "Warehouse");
            DoorLamp(parent, new Vector3(20f, 3.2f, WingNorth + 0.5f), 0f, LampState.Dead, "WestWing");
            DoorLamp(parent, new Vector3(60f, 3.2f, WingNorth + 0.5f), 0f, LampState.Flickering, "EastWing");

            // Fences close the courtyard off at the west and east ends; the gate off Route 94 is chained.
            Fence(parent, new Vector3(0f, 0f, -0.3f), new Vector3(-10f, 0f, -0.3f), new Vector3(-10f, 0f, WingNorth));
            Fence(parent, new Vector3(96f, 0f, WingNorth), new Vector3(WarehouseX1, 0f, WingNorth), new Vector3(WarehouseX1, 0f, WarehouseZ0));
            BuildGate(parent);
        }

        static void Weeds(Transform parent, Vector3 p)
        {
            if (FactoryProps.Place(parent, p, Rand(0f, 360f), Chance(0.4) ? Synty.GrassTall : Chance(0.5) ? Synty.GrassTall2 : Synty.Grass, null, false) != null)
                return;
            var g = GrayboxKit.Group("Weeds", parent).transform;
            int blades = rng.Next(3, 7);
            for (int i = 0; i < blades; i++)
            {
                float h = Rand(0.2f, 0.6f);
                Box("Blade", g, p + new Vector3(Rand(-0.25f, 0.25f), h * 0.5f, Rand(-0.25f, 0.25f)), new Vector3(0.04f, h, 0.12f), weed, false,
                    Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 180f), Rand(-25f, 25f)));
            }
        }

        static GameObject DeadTree(Transform parent, Vector3 p)
        {
            var g = GrayboxKit.Group("DeadTree", parent).transform;
            float h = Rand(5f, 8f);
            Cyl("Trunk", g, p + Vector3.up * h * 0.5f, 0.35f, h, kit.Bark, Vector3.up, true);
            for (int i = 0; i < 5; i++)
            {
                Vector3 start = p + Vector3.up * Rand(h * 0.45f, h * 0.9f);
                Vector3 dir = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * new Vector3(1f, Rand(0.4f, 1.2f), 0f);
                Beam("Branch", g, start, start + dir.normalized * Rand(1.2f, 2.6f), 0.1f, 0.1f, kit.Bark);
            }
            return g.gameObject;
        }

        static GameObject Trailer(Transform parent, Vector3 c)
        {
            var trailer = GrayboxKit.Group("Trailer", parent).transform;
            Box("Body", trailer, c + new Vector3(0f, 2.6f, 0f), new Vector3(12f, 2.8f, 2.6f), kit.VanPaint);
            Box("Chassis", trailer, c + new Vector3(0f, 1.0f, 0f), new Vector3(11.5f, 0.3f, 2.2f), iron, false);
            foreach (float x in new[] { 3.5f, 4.8f })
            foreach (float z in new[] { -1.1f, 1.1f })
                Cyl("Wheel", trailer, c + new Vector3(x, 0.5f, z), 1f, 0.35f, rubber, Vector3.forward);
            Box("LandingLeg", trailer, c + new Vector3(-4.5f, 0.6f, 0f), new Vector3(0.2f, 1.2f, 1.8f), iron);
            return trailer.gameObject;
        }

        static void BuildSmokestack(Transform parent, Vector3 p)
        {
            var g = GrayboxKit.Group("Smokestack", parent).transform;
            const float height = 36f;
            Box("Base", g, p + Vector3.up * 2f, new Vector3(5f, 4f, 5f), brickDark);
            Box("BaseCap", g, p + Vector3.up * 4.1f, new Vector3(5.4f, 0.25f, 5.4f), stone, false);
            Box("CleanoutDoor", g, p + new Vector3(0f, 0.8f, 2.52f), new Vector3(1f, 1.4f, 0.05f), iron, false);
            Cyl("Stack", g, p + Vector3.up * (4f + height * 0.5f), 3.2f, height, brick, Vector3.up, true);
            for (int i = 1; i <= 4; i++)
                Cyl("Band", g, p + Vector3.up * (4f + i * height / 5f), 3.35f, 0.3f, iron, Vector3.up);
            Cyl("Crown", g, p + Vector3.up * (4f + height + 0.4f), 3.6f, 0.8f, soot, Vector3.up);
            // A red aircraft-warning lamp somebody never switched off.
            Box("WarningLamp", g, p + new Vector3(1.75f, 4f + height - 0.5f, 0f), new Vector3(0.25f, 0.25f, 0.25f), pilotRed, false);
            // A ladder up the side.
            Beam("LadderRail", g, p + new Vector3(-0.3f, 4.2f, -1.7f), p + new Vector3(-0.3f, 4f + height - 1f, -1.7f), 0.05f, 0.05f, iron);
            Beam("LadderRail", g, p + new Vector3(0.3f, 4.2f, -1.7f), p + new Vector3(0.3f, 4f + height - 1f, -1.7f), 0.05f, 0.05f, iron);
        }

        static void BuildWaterTower(Transform parent, Vector3 p)
        {
            var g = GrayboxKit.Group("WaterTower", parent).transform;
            const float legHeight = 20f;
            foreach (float x in new[] { -3f, 3f })
            foreach (float z in new[] { -3f, 3f })
                Beam("Leg", g, p + new Vector3(x * 1.3f, 0f, z * 1.3f), p + new Vector3(x, legHeight, z), 0.35f, 0.35f, iron);
            for (int i = 1; i < 4; i++)
            {
                float y = i * legHeight / 4f;
                Beam("Brace", g, p + new Vector3(-3.5f, y, -3.5f), p + new Vector3(3.5f, y, 3.5f), 0.12f, 0.12f, iron);
                Beam("Brace", g, p + new Vector3(3.5f, y, -3.5f), p + new Vector3(-3.5f, y, 3.5f), 0.12f, 0.12f, iron);
            }
            Cyl("Tank", g, p + Vector3.up * (legHeight + 3f), 8f, 6f, kit.Rust, Vector3.up);
            MeshObject("TankRoof", g, GrayboxKit.ConeMesh(), kit.Rust, p + Vector3.up * (legHeight + 6f), Quaternion.identity,
                new Vector3(8.4f, 2.5f, 8.4f));
        }

        /// <summary>Sodium street lamp on a pole: Synty if available, else a primitive pole.</summary>
        static void PoleLamp(Transform parent, Vector3 p, LampState state, string id)
        {
            var g = GrayboxKit.Group($"PoleLamp_{id}", parent).transform;
            const float headHeight = 7f;
            if (FactoryProps.Place(g, p, 0f, Synty.Streetlamp, null) == null)
            {
                Cyl("Pole", g, p + Vector3.up * (headHeight * 0.5f), 0.18f, headHeight, kit.Metal, Vector3.up, true);
                Box("Arm", g, p + new Vector3(0.6f, headHeight, 0f), new Vector3(1.4f, 0.1f, 0.1f), kit.Metal, false);
                Box("Head", g, p + new Vector3(1.2f, headHeight - 0.1f, 0f), new Vector3(0.6f, 0.15f, 0.35f),
                    state == LampState.Dead ? bulbDead : kit.LampSodium, false);
            }
            if (state == LampState.Dead)
                return;
            Vector3 lightPos = p + new Vector3(1f, headHeight - 0.3f, 0f);
            var light = GrayboxKit.SpotLight("Light", g, lightPos, Quaternion.Euler(90f, 0f, 0f), SodiumColor,
                state == LampState.Flickering ? 16f : 20f, 14f, 125f);
            if (state == LampState.Working)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;
            }
            var haze = HazeCone("Haze", light.transform, lightPos, headHeight * 0.9f, 4f, hazeWarm);
            haze.transform.SetParent(light.transform, true);
            if (state == LampState.Flickering)
                AddFlicker(light);
        }

        /// <summary>A wall-mounted sodium lamp over a doorway, aimed down and out from the wall.</summary>
        static void DoorLamp(Transform parent, Vector3 p, float yaw, LampState state, string id)
        {
            var g = GrayboxKit.Group($"DoorLamp_{id}", parent).transform;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Arm", g, p + rot * new Vector3(0f, 0.15f, -0.3f), new Vector3(0.08f, 0.08f, 0.6f), kit.Metal, false, rot);
            Box("Head", g, p, new Vector3(0.5f, 0.15f, 0.35f), state == LampState.Dead ? bulbDead : kit.LampSodium, false, rot);
            if (state == LampState.Dead)
                return;
            var light = GrayboxKit.SpotLight("Light", g, p + Vector3.down * 0.1f, rot * Quaternion.Euler(70f, 0f, 0f), SodiumColor,
                state == LampState.Flickering ? 7f : 9f, 14f, 100f);
            if (state == LampState.Working)
                light.shadows = LightShadows.Soft;
            var haze = HazeCone("Haze", light.transform, p + Vector3.down * 0.1f, p.y * 0.85f, 2.4f, hazeWarm);
            haze.transform.SetParent(light.transform, true);
            if (state == LampState.Flickering)
                AddFlicker(light);
        }

        static void AddFlicker(Light light)
        {
            light.gameObject.AddComponent<LightFlicker>();
            var buzz = light.gameObject.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(buzz, "sound", (int)ProceduralSound.ElectricalBuzz);
            SerializedFields.SetFloat(buzz, "volume", 0.2f);
            SerializedFields.SetBool(buzz, "spatial", true);
            SerializedFields.SetFloat(buzz, "maxDistance", 10f);
        }

        /// <summary>Chain-link fence along a polyline, with an invisible collider on each run.</summary>
        static void Fence(Transform parent, params Vector3[] corners)
        {
            var fence = GrayboxKit.Group("Fence", parent).transform;
            for (int s = 0; s < corners.Length - 1; s++)
            {
                Vector3 a = corners[s], b = corners[s + 1];
                int posts = Mathf.CeilToInt(Vector3.Distance(a, b) / 3f);
                for (int i = 0; i <= posts; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)posts);
                    float lean = i % 7 == 3 ? 12f : 0f;
                    Box("Post", fence, p + Vector3.up * 1.2f, new Vector3(0.08f, 2.4f, 0.08f), kit.Metal, false,
                        Quaternion.Euler(lean, 0f, lean * 0.5f));
                }
                Beam("Rail", fence, a + Vector3.up * 2.3f, b + Vector3.up * 2.3f, 0.05f, 0.05f, kit.Metal);
                Beam("Mesh", fence, a + Vector3.up * 1.15f, b + Vector3.up * 1.15f, 0.01f, 2.2f, iron).GetComponent<Renderer>()
                    .shadowCastingMode = ShadowCastingMode.Off;
                // Barbed wire on top.
                Beam("Barb", fence, a + Vector3.up * 2.55f, b + Vector3.up * 2.55f, 0.02f, 0.02f, iron);
                Blocker("FenceBlock", fence, (a + b) * 0.5f + Vector3.up * 2f, Vector3.one,
                    Quaternion.LookRotation(b - a)).GetComponent<BoxCollider>().size = new Vector3(0.3f, 4f, Vector3.Distance(a, b));
            }
        }

        /// <summary>The chained gate off Route 94 behind the player's start, between the two wings.</summary>
        static void BuildGate(Transform parent)
        {
            var gate = GrayboxKit.Group("Gate", parent).transform;
            float z = WingSouth - 0.4f;
            Fence(gate, new Vector3(DriveX0, 0f, z), new Vector3(DriveX1, 0f, z));
            foreach (float x in new[] { DriveX0 + 0.1f, DriveX1 - 0.1f })
                Box("GatePost", gate, new Vector3(x, 1.4f, z), new Vector3(0.2f, 2.8f, 0.2f), kit.Metal);
            Beam("Chain", gate, new Vector3(39.6f, 1.3f, z - 0.05f), new Vector3(40.4f, 1.0f, z - 0.05f), 0.04f, 0.04f, iron);
            Beam("Chain", gate, new Vector3(39.6f, 1.0f, z - 0.05f), new Vector3(40.4f, 1.3f, z - 0.05f), 0.04f, 0.04f, iron);
            Box("Padlock", gate, new Vector3(40f, 1.1f, z - 0.1f), new Vector3(0.1f, 0.14f, 0.06f), kit.Hazard, false);
            Box("Sign", gate, new Vector3(41.6f, 1.5f, z - 0.06f), new Vector3(1.2f, 0.6f, 0.03f), paper, false);
            // Route 94 beyond, empty.
            Box("Road", parent, new Vector3(40f, -0.016f, -52f), new Vector3(220f, 0.02f, 8f), kit.Asphalt);
            for (float x = -60f; x < 160f; x += 6f)
                Box("RoadLine", parent, new Vector3(x, 0.001f, -52f), new Vector3(3f, 0.01f, 0.15f), kit.RoadLine, false);
            FactoryProps.Place(parent, new Vector3(34f, 0f, -47.5f), 180f, Synty.Streetlamp, null);
        }
    }
}
