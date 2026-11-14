using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    // The machine hall and the rooms opening off it: office, dip room, stairwell, plus floor litter and the breaker.
    public static partial class FactoryInteriorBuilder
    {
        static readonly int[] CollapsedBays = { 9, 10 };   // X 54..66, south roof slope gone
        const float ColumnZSouth = 8.4f, ColumnZNorth = 15.6f;
        const float RowAZ = 5f, RowBZ = 19f;
        const float BigDoorX = 33f, SideDoorX = 75f;

        static bool IsCollapsed(int bay) => System.Array.IndexOf(CollapsedBays, bay) >= 0;

        // ------------------------------------------------------------------ shell

        static void BuildHallShell(Transform parent)
        {
            Box("Floor", parent, new Vector3(HallLength * 0.5f, -0.05f, HallWidth * 0.5f),
                new Vector3(HallLength + 0.5f, 0.1f, HallWidth + 0.5f), kit.ConcreteFloor);

            // South wall: receiving-room door, a big sliding door and a side door to the courtyard, windows elsewhere.
            var south = new List<Opening>
            {
                Opening.Door(10.5f, 3f, 3.6f), Opening.Door(BigDoorX, 4.5f, 4.6f), Opening.Door(SideDoorX, 1.4f, 2.4f),
            };
            for (int bay = 2; bay < Bays; bay++)
            {
                float c = bay * BayWidth + 3f;
                if (Mathf.Abs(c - BigDoorX) < 4f || Mathf.Abs(c - SideDoorX) < 3f)
                    continue;
                south.Add(Opening.Window(c, 2.6f, 1.8f, 6.6f, boarded: Chance(0.3)));
            }
            Wall("Wall_South", parent, Vector3.zero, new Vector3(HallLength, 0f, 0f), EaveHeight, brick, south.ToArray());

            // The big door's steel leaf, slid half open.
            Box("SlidingDoor", parent, new Vector3(BigDoorX + 3.2f, 2.35f, -0.4f), new Vector3(4.6f, 4.7f, 0.12f), kit.Metal);
            Box("SlidingDoorRail", parent, new Vector3(BigDoorX + 2f, 4.8f, -0.42f), new Vector3(10f, 0.12f, 0.12f), iron, false);
            Box("SideDoorLeaf", parent, new Vector3(SideDoorX - 0.4f, 1.2f, -0.55f), new Vector3(0.06f, 2.4f, 1.3f), plank, true,
                Quaternion.Euler(0f, 70f, 0f));

            // North wall: the dip-room doorway, then windows (none where the office stair runs).
            var north = new List<Opening> { Opening.Door(7f, 2.4f, 3.2f) };
            for (int bay = 3; bay < 17; bay++)
                north.Add(Opening.Window(bay * BayWidth + 3f, 2.6f, 1.8f, 6.6f, boarded: Chance(0.35)));
            Wall("Wall_North", parent, new Vector3(0f, 0f, HallWidth), new Vector3(HallLength, 0f, HallWidth), EaveHeight, brick, north.ToArray());

            // West end: big double doors to the unbuilt part of the factory, chained shut.
            Wall("Wall_West", parent, Vector3.zero, new Vector3(0f, 0f, HallWidth), EaveHeight, brick, Opening.Door(12f, 4f, 4.5f));
            var doors = GrayboxKit.Group("ChainedDoors", parent).transform;
            Box("LeafA", doors, new Vector3(0f, 2.25f, 11f), new Vector3(0.15f, 4.5f, 1.98f), plank);
            Box("LeafB", doors, new Vector3(0.03f, 2.25f, 13f), new Vector3(0.15f, 4.5f, 1.98f), plank, true, Quaternion.Euler(0f, -3f, 0f));
            Beam("Chain", doors, new Vector3(0.2f, 1.5f, 10.6f), new Vector3(0.2f, 1.1f, 13.4f), 0.05f, 0.05f, iron);
            Beam("Chain", doors, new Vector3(0.2f, 1.1f, 10.6f), new Vector3(0.2f, 1.5f, 13.4f), 0.05f, 0.05f, iron);
            Box("Padlock", doors, new Vector3(0.25f, 1.3f, 12f), new Vector3(0.08f, 0.14f, 0.1f), kit.Hazard, false);

            // East end: wide door into the warehouse, and a high window from the office.
            Wall("Wall_East", parent, new Vector3(HallLength, 0f, 0f), new Vector3(HallLength, 0f, HallWidth), EaveHeight, brick,
                Opening.Door(5f, 4f, 4.5f), Opening.Window(19f, 2f, 4.7f, 6.3f));

            var prism = PrismMesh();
            foreach (float x in new[] { 0f, HallLength })
                MeshObject("Gable", parent, prism, brick, new Vector3(x, EaveHeight, HallWidth * 0.5f),
                    Quaternion.Euler(0f, 90f, 0f), new Vector3(HallWidth + WallThickness, RidgeHeight - EaveHeight, WallThickness));

            // Steel columns either side of the centre aisle, one per truss, with a cobweb or a power box here and there.
            for (int bay = 1; bay < Bays; bay++)
            {
                float x = bay * BayWidth;
                foreach (float z in new[] { ColumnZSouth, ColumnZNorth })
                {
                    Box($"Column_{bay}", parent, new Vector3(x, EaveHeight * 0.5f, z), new Vector3(0.35f, EaveHeight, 0.35f), kit.Metal);
                    Box($"ColumnBase_{bay}", parent, new Vector3(x, 0.1f, z), new Vector3(0.7f, 0.2f, 0.7f), kit.Concrete, false);
                    Box($"ColumnStripe_{bay}", parent, new Vector3(x, 0.55f, z), new Vector3(0.37f, 0.3f, 0.37f), kit.Hazard, false);
                    if (Chance(0.35))
                        FactoryProps.Place(parent, new Vector3(x, EaveHeight - 0.6f, z), Rand(0f, 360f), Chance(0.5) ? Synty.Cobweb : Synty.Cobweb2, null, false);
                }
                if (bay % 4 == 2)
                    FactoryProps.Place(parent, new Vector3(x, 1.2f, ColumnZSouth - 0.2f), 180f, Synty.PowerBox, null, false);
            }

            // Painted aisle lines, worn.
            foreach (float z in new[] { ColumnZSouth + 0.5f, ColumnZNorth - 0.5f })
                for (float x = 2f; x < HallLength - 2f; x += 4f)
                    if (Chance(0.75))
                        Box("AisleLine", parent, new Vector3(x + 1.5f, 0.004f, z), new Vector3(Rand(2f, 3.2f), 0.01f, 0.12f), kit.Hazard, false);

            // Pipe runs high along both long walls, with valves; a vent duct along the north wall.
            foreach (float z in new[] { 0.6f, HallWidth - 0.6f })
            {
                Cyl("WallPipe", parent, new Vector3(HallLength * 0.5f, 6.9f, z), 0.3f, HallLength - 1f, rubber, Vector3.right);
                Cyl("WallPipeSmall", parent, new Vector3(HallLength * 0.5f, 6.5f, z), 0.14f, HallLength - 1f, kit.Rust, Vector3.right);
                for (int bay = 1; bay < Bays; bay += 3)
                {
                    float x = bay * BayWidth;
                    Box("PipeBracket", parent, new Vector3(x, 6.7f, z), new Vector3(0.1f, 0.7f, 0.5f), iron, false);
                    FactoryProps.Place(parent, new Vector3(x + 0.6f, 6.9f, z), z < 1f ? 0f : 180f, Synty.Valve, null, false);
                    // A drop pipe down to a gauge box at chest height.
                    Cyl("DropPipe", parent, new Vector3(x + 1.2f, 4f, z), 0.12f, 5.6f, kit.Rust, Vector3.up);
                }
            }
        }

        // ------------------------------------------------------------------ roof and trusses

        static void BuildRoof(Transform parent)
        {
            float rise = RidgeHeight - EaveHeight;
            float halfSpan = HallWidth * 0.5f;
            float slopeLength = Mathf.Sqrt(halfSpan * halfSpan + rise * rise) + 0.6f;
            float angle = Mathf.Atan2(rise, halfSpan) * Mathf.Rad2Deg;

            for (int bay = 0; bay < Bays; bay++)
            {
                float x = bay * BayWidth + BayWidth * 0.5f;
                for (int side = 0; side < 2; side++)
                {
                    if (side == 0 && IsCollapsed(bay))
                        continue;
                    var rot = Quaternion.Euler(side == 0 ? -angle : angle, 0f, 0f);
                    Vector3 c = new Vector3(x, EaveHeight + rise * 0.5f, side == 0 ? halfSpan * 0.5f : halfSpan * 1.5f);
                    c += rot * Vector3.up * 0.25f;
                    Box($"RoofPanel_{bay}_{side}", parent, c, new Vector3(BayWidth + 0.02f, 0.2f, slopeLength), roofBoards, false, rot);
                }
            }
            Box("RidgeCap", parent, new Vector3(HallLength * 0.5f, RidgeHeight + 0.3f, halfSpan), new Vector3(HallLength + 0.6f, 0.3f, 0.7f), roofBoards, false);

            // Boards still hanging off the ridge over the collapse.
            foreach (int bay in CollapsedBays)
            {
                float cx = bay * BayWidth;
                Beam("HangingBoard", parent, new Vector3(cx + 1.5f, RidgeHeight - 0.1f, halfSpan - 0.4f),
                    new Vector3(cx + 1.2f, RidgeHeight - 4.2f, halfSpan - 1.8f), 0.5f, 0.06f, roofBoards);
                Beam("HangingBoard", parent, new Vector3(cx + 4.3f, RidgeHeight - 0.3f, halfSpan - 0.8f),
                    new Vector3(cx + 4.8f, RidgeHeight - 3f, halfSpan - 3.6f), 0.4f, 0.06f, roofBoards);
            }

            for (int bay = 1; bay < Bays; bay++)
            {
                float x = bay * BayWidth;
                var t = GrayboxKit.Group($"Truss_{bay}", parent).transform;
                Vector3 eaveS = new Vector3(x, EaveHeight, 0f), eaveN = new Vector3(x, EaveHeight, HallWidth);
                Vector3 ridge = new Vector3(x, RidgeHeight, halfSpan), tieMid = new Vector3(x, EaveHeight, halfSpan);
                Vector3 quarterS = new Vector3(x, EaveHeight + rise * 0.5f, halfSpan * 0.5f);
                Vector3 quarterN = new Vector3(x, EaveHeight + rise * 0.5f, halfSpan * 1.5f);
                Beam("Tie", t, eaveS, eaveN, 0.25f, 0.3f, timber);
                Beam("ChordS", t, eaveS, ridge, 0.25f, 0.3f, timber);
                Beam("ChordN", t, eaveN, ridge, 0.25f, 0.3f, timber);
                Beam("KingPost", t, tieMid, ridge, 0.22f, 0.22f, timber);
                Beam("QueenS", t, new Vector3(x, EaveHeight, halfSpan * 0.5f), quarterS, 0.18f, 0.18f, timber);
                Beam("QueenN", t, new Vector3(x, EaveHeight, halfSpan * 1.5f), quarterN, 0.18f, 0.18f, timber);
                Beam("StrutS", t, tieMid, quarterS, 0.16f, 0.16f, timber);
                Beam("StrutN", t, tieMid, quarterN, 0.16f, 0.16f, timber);
                if (Chance(0.3))
                    FactoryProps.Place(t, new Vector3(x, EaveHeight - 0.1f, Rand(3f, 21f)), 0f, Synty.Chain, null, false);
            }

            for (int i = 1; i < 4; i++)
            {
                float f = i / 4f;
                float y = EaveHeight + rise * f + 0.05f;
                Box("PurlinS", parent, new Vector3(HallLength * 0.5f, y, halfSpan * f), new Vector3(HallLength, 0.2f, 0.2f), timber, false);
                Box("PurlinN", parent, new Vector3(HallLength * 0.5f, y, HallWidth - halfSpan * f), new Vector3(HallLength, 0.2f, 0.2f), timber, false);
            }
        }

        // ------------------------------------------------------------------ lamps

        static void BuildHallLamps(Transform parent)
        {
            // Centre aisle, one per truss. W = works, F = flickers, D = dead.
            const string centre = "WDWFDWDDFWDWWDFDWDW";
            for (int i = 0; i < centre.Length; i++)
                HangingLamp(parent, new Vector3((i + 1) * BayWidth, 0f, HallWidth * 0.5f), EaveHeight, ToState(centre[i]), $"Aisle_{i}", 2.2f);
            // Side aisles: mostly dead, a few still burning.
            const string sides = "DWDDFDDWDDDF";
            for (int i = 0; i < sides.Length; i++)
            {
                float x = (i % 6) * 18f + 12f;
                float z = i < 6 ? 2f : 22f;
                HangingLamp(parent, new Vector3(x, 0f, z), EaveHeight, ToState(sides[i]), $"Side_{i}", 2.2f);
            }
        }

        // ------------------------------------------------------------------ match machines

        static void BuildMachines(Transform parent)
        {
            for (int i = 0; i < 11; i++)
            {
                float start = 4f + i * 10f;
                float x = start + 4f;
                if (start != 54f) // the row-A machine under the collapse is wrecked instead
                    MatchMachine(parent, new Vector3(x, 0f, RowAZ), +1, $"A{i}", hum: false);
                MatchMachine(parent, new Vector3(x, 0f, RowBZ), -1, $"B{i}", hum: i == 5);
            }
        }

        /// <summary>
        /// One continuous match machine, 8 m long, after the reference photos: an iron base, tall end
        /// frames, the chain runs, a forest of pipe uprights, a drum, gear wheels and a hopper.
        /// <paramref name="aisleSide"/> is +1 if the centre aisle is north of the machine.
        /// </summary>
        static void MatchMachine(Transform parent, Vector3 c, int aisleSide, string id, bool hum)
        {
            const float L = 8f, D = 2.8f, H = 3.4f;
            var m = GrayboxKit.Group($"MatchMachine_{id}", parent).transform;
            float front = aisleSide * (D * 0.5f);

            Box("Base", m, c + Vector3.up * 0.25f, new Vector3(L, 0.5f, D), iron, false);
            foreach (float s in new[] { -1f, 1f })
                Box("EndFrame", m, c + new Vector3(s * (L * 0.5f - 0.2f), H * 0.5f, 0f), new Vector3(0.35f, H, D), iron, false);
            Box("ChainUpper", m, c + Vector3.up * 3.1f, new Vector3(L - 0.4f, 0.35f, D - 0.4f), rubber, false);
            Box("ChainLower", m, c + Vector3.up * 1.9f, new Vector3(L - 0.4f, 0.25f, D - 0.6f), rubber, false);
            for (int k = 0; k < 8; k++)
            {
                float x = -L * 0.5f + 0.6f + k * 1.0f;
                foreach (float z in new[] { -(D * 0.5f - 0.15f), D * 0.5f - 0.15f })
                    Cyl("Upright", m, c + new Vector3(x, 1.75f, z), 0.08f, 3.3f, kit.Metal, Vector3.up);
            }
            foreach (float z in new[] { -(D * 0.5f - 0.15f), D * 0.5f - 0.15f })
                Cyl("TopPipe", m, c + new Vector3(0f, 3.45f, z), 0.1f, L - 0.4f, kit.Metal, Vector3.right);
            Cyl("Drum", m, c + new Vector3(-L * 0.5f + 1.3f, 1.35f, front), 1.8f, 0.8f, iron, Vector3.forward);
            for (int g = 0; g < 3; g++)
                Cyl("Gear", m, c + new Vector3(-0.5f + g * 1.4f, 0.95f, front + aisleSide * 0.08f), Rand(0.45f, 0.75f), 0.1f, iron, Vector3.forward);
            Box("Hopper", m, c + new Vector3(L * 0.5f - 1.1f, 3.75f, 0f), new Vector3(1.2f, 0.8f, 1.6f), kit.Metal, false,
                Quaternion.Euler(0f, 0f, 25f));
            Box("Shelf", m, c + new Vector3(1.5f, 1.0f, front + aisleSide * 0.35f), new Vector3(2f, 0.06f, 0.6f), plank, false);

            // Stool, toolbox and the odd crate on the aisle side.
            Vector3 stool = c + new Vector3(Rand(-2f, 2.5f), 0f, front + aisleSide * 0.9f);
            if (Chance(0.4))
            {
                Cyl("StoolSeat", m, stool + new Vector3(0.3f, 0.18f, 0f), 0.36f, 0.05f, plank, Vector3.right);
                Cyl("StoolLeg", m, stool + new Vector3(0f, 0.18f, 0f), 0.06f, 0.6f, kit.Metal, Vector3.right);
            }
            else
            {
                Cyl("StoolSeat", m, stool + Vector3.up * 0.62f, 0.36f, 0.05f, plank, Vector3.up);
                Cyl("StoolLeg", m, stool + Vector3.up * 0.3f, 0.06f, 0.6f, kit.Metal, Vector3.up);
            }
            FactoryProps.Place(m, c + new Vector3(1.2f, 1.04f, front + aisleSide * 0.35f), Rand(-20f, 20f), Synty.Toolbox, null, false);
            if (Chance(0.5))
                FactoryProps.Place(m, c + new Vector3(Rand(-3f, 3f), 0f, front + aisleSide * 0.75f), Rand(0f, 360f),
                    Chance(0.5) ? Synty.Cardboard : Synty.CrateWood2);

            Blocker("Collider", m, c + Vector3.up * (H * 0.5f), new Vector3(L, H, D));

            if (hum)
            {
                // One machine still hums with no power to the building. Nobody can say why.
                var source = GrayboxKit.Group("Hum", m);
                source.transform.position = c + Vector3.up * 1.5f;
                var sound = source.AddComponent<AmbientSound>();
                SerializedFields.SetEnum(sound, "sound", (int)ProceduralSound.MachineHum);
                SerializedFields.SetFloat(sound, "volume", 0.3f);
                SerializedFields.SetBool(sound, "spatial", true);
                SerializedFields.SetFloat(sound, "maxDistance", 18f);
            }
        }

        // ------------------------------------------------------------------ line shafts and belts

        static void BuildLineShafts(Transform parent)
        {
            const float shaftY = 5.8f;
            foreach (float z in new[] { RowAZ, RowBZ })
            {
                int aisleSide = z < HallWidth * 0.5f ? 1 : -1;
                Cyl("Shaft", parent, new Vector3(HallLength * 0.5f, shaftY, z), 0.14f, HallLength - 4f, kit.Metal, Vector3.right);
                for (int bay = 1; bay < Bays; bay++)
                    Beam("Hanger", parent, new Vector3(bay * BayWidth, shaftY, z), new Vector3(bay * BayWidth, EaveHeight, z), 0.08f, 0.08f, kit.Metal);

                for (float x = 5f; x < 112f; x += 2.5f)
                {
                    Cyl("Pulley", parent, new Vector3(x, shaftY, z), Rand(0.7f, 1.0f), 0.16f, iron, Vector3.right);
                    bool wrecked = aisleSide > 0 && x > 54f && x < 66f;
                    if (wrecked || Chance(0.2))
                    {
                        Beam("BeltSnapped", parent, new Vector3(x, shaftY - 0.4f, z),
                            new Vector3(x + Rand(-0.2f, 0.2f), shaftY - Rand(1.2f, 2.4f), z + 0.1f), 0.12f, 0.02f, rubber);
                        continue;
                    }
                    Vector3 top = new Vector3(x, shaftY + 0.35f, z);
                    Vector3 bottom = new Vector3(x, 3.6f, z + aisleSide * 0.6f);
                    Beam("Belt", parent, top + Vector3.right * 0.06f, bottom + Vector3.right * 0.06f, 0.12f, 0.02f, rubber);
                    Beam("Belt", parent, top - Vector3.right * 0.06f + Vector3.down * 0.7f, bottom - Vector3.right * 0.06f, 0.12f, 0.02f, rubber);
                }
            }
        }

        // ------------------------------------------------------------------ hall props

        static void BuildHallProps(Transform parent)
        {
            // Stacks of splint/paper rolls in the side aisles, as in the old photo.
            Vector3[] stacks =
            {
                new Vector3(10f, 0f, 22.6f), new Vector3(11.3f, 0f, 22.7f), new Vector3(26f, 0f, 22.6f), new Vector3(46f, 0f, 22.6f),
                new Vector3(70f, 0f, 22.6f), new Vector3(71.3f, 0f, 22.5f), new Vector3(92f, 0f, 22.6f),
                new Vector3(24f, 0f, 1.6f), new Vector3(25.3f, 0f, 1.7f), new Vector3(48f, 0f, 1.6f), new Vector3(84f, 0f, 1.6f),
            };
            for (int s = 0; s < stacks.Length; s++)
            {
                var stack = GrayboxKit.Group($"RollStack_{s}", parent).transform;
                int count = rng.Next(5, 11);
                for (int i = 0; i < count; i++)
                    Cyl("Roll", stack, stacks[s] + new Vector3(Rand(-0.05f, 0.05f), 0.18f + i * 0.36f, Rand(-0.05f, 0.05f)), 1.1f, 0.35f, paper, Vector3.up);
                Blocker("Collider", stack, stacks[s] + Vector3.up * (count * 0.18f), new Vector3(1.1f, count * 0.36f, 1.1f));
            }
            for (int i = 0; i < 6; i++)
            {
                var axis = Quaternion.Euler(0f, Rand(0f, 180f), 0f) * Vector3.right;
                Cyl("FallenRoll", parent, new Vector3(Rand(40f, 44f), 0.55f, Rand(21.4f, 23.2f)), 1.1f, 0.35f, paper, axis, true);
            }

            // Shipping carts of boxed matches.
            Vector3[] carts = { new Vector3(16f, 0f, 12f), new Vector3(43f, 0f, 10.6f), new Vector3(88f, 0f, 13f), new Vector3(110f, 0f, 3f) };
            foreach (var c in carts)
            {
                var cart = GrayboxKit.Group("BoxCart", parent).transform;
                var rot = Quaternion.Euler(0f, Rand(-20f, 20f), 0f);
                Box("Bed", cart, c + Vector3.up * 0.35f, new Vector3(1.6f, 0.06f, 0.9f), kit.Metal, false, rot);
                Box("Handle", cart, c + rot * new Vector3(-0.8f, 0.8f, 0f), new Vector3(0.04f, 0.9f, 0.8f), kit.Metal, false, rot);
                foreach (float x in new[] { -0.7f, 0.7f })
                foreach (float z in new[] { -0.4f, 0.4f })
                    Cyl("Wheel", cart, c + rot * new Vector3(x, 0.12f, z), 0.24f, 0.06f, rubber, rot * Vector3.forward);
                int boxes = rng.Next(3, 9);
                for (int b = 0; b < boxes; b++)
                    Box("Carton", cart, c + rot * new Vector3(-0.5f + (b % 3) * 0.5f, 0.55f + (b / 3) * 0.34f, Rand(-0.2f, 0.2f)),
                        new Vector3(0.45f, 0.3f, 0.35f), kit.Cardboard, false, rot * Quaternion.Euler(0f, Rand(-8f, 8f), 0f));
                Blocker("Collider", cart, c + Vector3.up * 0.6f, new Vector3(1.6f, 1.2f, 0.9f), rot);
            }

            // Spilled cartons and junk around the machines.
            for (int i = 0; i < 24; i++)
            {
                float x = Rand(4f, 112f);
                float z = Chance(0.5) ? Rand(6.8f, 7.6f) : Rand(16.4f, 17.2f);
                if (!FactoryProps.Place(parent, new Vector3(x, 0f, z), Rand(0f, 360f),
                        i % 3 == 0 ? Synty.CardboardJunk : i % 3 == 1 ? Synty.Cardboard2 : Synty.Papers, null, i % 3 != 2))
                    Box("LooseCarton", parent, new Vector3(x, 0.15f, z), new Vector3(0.45f, 0.3f, 0.35f), kit.Cardboard, true,
                        Quaternion.Euler(0f, Rand(0f, 180f), Chance(0.3) ? 90f : 0f));
            }

            // Work stations against the walls between the windows: bench, tool board, cabinet, barrels.
            for (int bay = 1; bay < 17; bay += 2)
            {
                float x = bay * BayWidth;
                bool southSide = bay % 4 == 1;
                float z = southSide ? 0.75f : HallWidth - 0.75f;
                float yaw = southSide ? 0f : 180f;
                if (southSide && (Mathf.Abs(x - BigDoorX) < 4f || Mathf.Abs(x - SideDoorX) < 3f || x < 15f))
                    continue;
                if (!southSide && x < 17f)
                    continue;
                FactoryProps.Place(parent, new Vector3(x, 0f, z), yaw, Synty.Workbench,
                    () => Box("Workbench", parent, new Vector3(x, 0.45f, z), new Vector3(2f, 0.9f, 0.8f), plank));
                FactoryProps.Place(parent, new Vector3(x, 1.5f, southSide ? 0.3f : HallWidth - 0.3f), yaw, Synty.ToolBoard, null, false);
                FactoryProps.Place(parent, new Vector3(x + 1.7f, 0f, z), yaw, Synty.ToolCabinet, null);
                for (int b = 0; b < rng.Next(1, 4); b++)
                    FactoryProps.Place(parent, new Vector3(x - 1.8f - b * 0.7f, 0f, z + Rand(-0.2f, 0.2f)), Rand(0f, 360f),
                        b == 2 ? Synty.BarrelBroken : Synty.BarrelMetal,
                        () => Cyl("Drum", parent, new Vector3(x - 1.8f, 0.45f, z), 0.6f, 0.9f, kit.Rust, Vector3.up, true));
            }

            // Crate stacks in the side aisles and by the columns.
            for (int i = 0; i < 10; i++)
            {
                float x = Rand(6f, 108f);
                float z = Chance(0.5) ? Rand(1.4f, 2.4f) : Rand(21.6f, 22.8f);
                CrateStack(parent, new Vector3(x, 0f, z));
            }

            // Ladders leaning on the walls.
            for (int i = 0; i < 4; i++)
            {
                float x = 20f + i * 25f;
                FactoryProps.Place(parent, new Vector3(x, 0f, HallWidth - 0.6f), Quaternion.Euler(-12f, 180f, 0f), Synty.Ladder, null, false);
            }
        }

        /// <summary>Two or three crates stacked, Synty if available, else primitive crates.</summary>
        static void CrateStack(Transform parent, Vector3 p)
        {
            var group = GrayboxKit.Group("CrateStack", parent).transform;
            float yaw = Rand(-15f, 15f);
            if (FactoryProps.Find(Synty.CrateWood) != null)
            {
                FactoryProps.Place(group, p, yaw, Synty.CrateWood);
                FactoryProps.Place(group, p + new Vector3(1.05f, 0f, Rand(-0.1f, 0.1f)), yaw + Rand(-6f, 6f), Synty.CrateMetal);
                if (Chance(0.6))
                    FactoryProps.Place(group, p + new Vector3(0.1f, 0.95f, 0f), yaw + Rand(-12f, 12f), Synty.CrateWood2);
                return;
            }
            float size = Rand(0.9f, 1.2f);
            Box("Crate", group, p + Vector3.up * size * 0.5f, Vector3.one * size, kit.Crate, true, Quaternion.Euler(0f, yaw, 0f));
            Box("Crate", group, p + new Vector3(0.1f, size + 0.4f, 0f), Vector3.one * 0.8f, kit.Crate, true, Quaternion.Euler(0f, yaw + 12f, 0f));
        }

        // ------------------------------------------------------------------ collapsed bays

        static void BuildCollapse(Transform parent)
        {
            float x0 = CollapsedBays[0] * BayWidth;
            float span = CollapsedBays.Length * BayWidth;
            for (int i = 0; i < 26; i++)
            {
                var rot = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 180f), Rand(-20f, 20f));
                Box("FallenBoard", parent, new Vector3(x0 + Rand(0.5f, span - 0.5f), Rand(0.1f, 0.8f), Rand(1f, 7.5f)),
                    new Vector3(Rand(0.4f, 0.8f), 0.08f, Rand(2f, 4.5f)), roofBoards, false, rot);
            }
            for (int i = 0; i < 10; i++)
                if (!FactoryProps.Place(parent, new Vector3(x0 + Rand(0.8f, span - 0.8f), 0f, Rand(1.5f, 7f)), Rand(0f, 360f),
                        i % 3 == 0 ? Synty.RubblePlank : i % 2 == 0 ? Synty.Rubble : Synty.Rubble2, null, false))
                    Box("Rubble", parent, new Vector3(x0 + Rand(0.8f, span - 0.8f), Rand(0.1f, 0.5f), Rand(1.5f, 6.5f)),
                        Vector3.one * Rand(0.3f, 0.8f), debris, false, Quaternion.Euler(Rand(0f, 90f), Rand(0f, 90f), Rand(0f, 90f)));
            Blocker("RubbleMound", parent, new Vector3(x0 + span * 0.5f, 0.4f, 4.5f), new Vector3(span - 2f, 0.8f, 5f));

            Beam("FallenChord", parent, new Vector3(x0 + 0.6f, 0f, 0.8f), new Vector3(x0 + 4.5f, 4.2f, 7.6f), 0.25f, 0.3f, timber, true);
            Beam("FallenChord", parent, new Vector3(x0 + 11f, 0f, 1.5f), new Vector3(x0 + 8f, 3.4f, 7.4f), 0.25f, 0.3f, timber, true);
            Box("WreckBase", parent, new Vector3(x0 + 3f, 0.25f, 5.2f), new Vector3(7f, 0.5f, 2.6f), iron, true, Quaternion.Euler(0f, 4f, 3f));
            Box("WreckFrame", parent, new Vector3(x0 + 6.2f, 1.1f, 5.6f), new Vector3(0.35f, 2.8f, 2.6f), iron, true, Quaternion.Euler(0f, 10f, 38f));
            Cyl("WreckDrum", parent, new Vector3(x0 - 0.4f, 0.9f, 4.2f), 1.8f, 0.8f, iron, new Vector3(0.3f, 0f, 1f), true);

            // Moonlight pouring through the hole: a real shadowed light plus a visible shaft.
            var light = GrayboxKit.SpotLight("MoonShaft", parent, new Vector3(x0 + span * 0.5f, RidgeHeight + 3f, 2f),
                Quaternion.Euler(75f, 0f, 0f), new Color(0.6f, 0.7f, 1f), 9f, 22f, 60f);
            light.shadows = LightShadows.Soft;
            for (int i = 0; i < 2; i++)
                MoonShaft("MoonHaze", parent, new Vector3(x0 + 3f + i * 6f, RidgeHeight - 0.5f, 3.5f), new Vector3(0.05f, -1f, 0.25f), 12f, 3.2f);
        }

        // ------------------------------------------------------------------ Foreman's office

        static void BuildOffice(Transform parent)
        {
            const float x0 = 112f, officeX = 113.5f, zSouth = 14.2f, inner = HallWidth - WallThickness * 0.5f;
            float y = PlatformHeight;

            Box("Platform", parent, new Vector3((x0 + HallLength) * 0.5f, y - 0.15f, (zSouth + HallWidth) * 0.5f),
                new Vector3(HallLength - x0, 0.3f, HallWidth - zSouth), kit.Concrete);
            foreach (var p in new[] { new Vector3(x0 + 0.2f, 0f, zSouth + 0.2f), new Vector3(x0 + 0.2f, 0f, 21.4f), new Vector3(117f, 0f, zSouth + 0.2f) })
                Box("PlatformPost", parent, p + Vector3.up * (y * 0.5f - 0.15f), new Vector3(0.25f, y - 0.3f, 0.25f), kit.Metal);

            Stairs("Stair", parent, new Vector3(x0 - 7f, 0f, 22.7f), Vector3.right, 7f, y, 1.8f, kit.Concrete);
            Railing("StairRail", parent, new Vector3(x0 - 7f, 0f, 21.75f), new Vector3(x0, y, 21.75f), kit.Hazard);
            Railing("LandingRailWest", parent, new Vector3(x0 + 0.05f, y, zSouth), new Vector3(x0 + 0.05f, y, 21.75f), kit.Hazard);
            Railing("LandingRailSouth", parent, new Vector3(x0, y, zSouth + 0.05f), new Vector3(officeX, y, zSouth + 0.05f), kit.Hazard);

            const float officeHeight = 2.8f;
            Wall("OfficeWall_West", parent, new Vector3(officeX, y, zSouth), new Vector3(officeX, y, inner), officeHeight, brickDark,
                Opening.Window(3.8f, 4f, 1.0f, 2.2f), Opening.Door(8.5f, 1.1f, 2.2f));
            Wall("OfficeWall_South", parent, new Vector3(officeX, y, zSouth), new Vector3(HallLength - WallThickness * 0.5f, y, zSouth),
                officeHeight, brickDark, Opening.Window(3f, 2.4f, 1.0f, 2.2f));
            Box("OfficeCeiling", parent, new Vector3((officeX + HallLength) * 0.5f, y + officeHeight + 0.1f, (zSouth + HallWidth) * 0.5f),
                new Vector3(HallLength - officeX + 0.3f, 0.2f, HallWidth - zSouth + 0.3f), roofBoards);
            Box("Door", parent, new Vector3(officeX + 0.45f, y + 1.1f, 23.35f), new Vector3(0.06f, 2.2f, 1.0f), plank, true, Quaternion.Euler(0f, 65f, 0f));

            float fy = y;
            FactoryProps.Place(parent, new Vector3(117.5f, fy, 18f), 90f, Synty.Desk, () =>
            {
                var desk = Box("Desk", parent, new Vector3(117.5f, fy + 0.38f, 18f), new Vector3(1.0f, 0.76f, 1.8f), plank);
                Box("DeskTop", parent, new Vector3(117.5f, fy + 0.77f, 18f), new Vector3(1.1f, 0.04f, 1.9f), timber, false);
                return desk;
            });
            FactoryProps.Place(parent, new Vector3(116.4f, fy, 18.2f), 80f, Synty.OfficeChair,
                () => Cyl("ChairSeat", parent, new Vector3(116.4f, fy + 0.5f, 18.2f), 0.5f, 0.06f, rubber, Vector3.up));
            for (int i = 0; i < 3; i++)
                FactoryProps.Place(parent, new Vector3(119.3f, fy, 22.8f - i * 0.7f), 270f, i == 1 ? Synty.FilingCabinet2 : Synty.FilingCabinet,
                    () => Box("FilingCabinet", parent, new Vector3(119.4f, fy + 0.66f, 22.8f - i * 0.65f), new Vector3(0.6f, 1.32f, 0.55f), kit.Metal));
            FactoryProps.Place(parent, new Vector3(119.7f, fy + 1.5f, 16f), 270f, Synty.Corkboard, null, false);
            FactoryProps.Place(parent, new Vector3(119.7f, fy + 2.2f, 18f), 270f, Synty.Clock,
                () => Cyl("WallClock", parent, new Vector3(119.7f, fy + 2.1f, 18f), 0.35f, 0.05f, paper, Vector3.right), false);
            FactoryProps.Place(parent, new Vector3(117.3f, fy + 0.78f, 17.4f), 30f, Synty.Phone, null, false);
            for (int i = 0; i < 9; i++)
                if (!FactoryProps.Place(parent, new Vector3(Rand(114.5f, 119f), fy, Rand(14.8f, 23f)), Rand(0f, 360f), Synty.Papers, null, false))
                    Box("Paper", parent, new Vector3(Rand(114.5f, 119f), fy + 0.006f, Rand(14.8f, 23f)), new Vector3(0.22f, 0.01f, 0.3f), paper, false,
                        Quaternion.Euler(0f, Rand(0f, 180f), 0f));
            FactoryProps.Place(parent, new Vector3(114.2f, fy, 15f), 0f, Synty.BoxPile);

            // The desk lamp is on. Nothing in this building should have power.
            MeshObject("DeskLampShade", parent, GrayboxKit.ConeMesh(), enamel, new Vector3(117.7f, fy + 1.05f, 18.7f),
                Quaternion.identity, new Vector3(0.28f, 0.18f, 0.28f));
            Cyl("DeskLampStem", parent, new Vector3(117.7f, fy + 0.93f, 18.7f), 0.03f, 0.3f, kit.Metal, Vector3.up);
            var deskLamp = GrayboxKit.PointLight("DeskLamp", parent, new Vector3(117.7f, fy + 0.95f, 18.7f), new Color(1f, 0.72f, 0.42f), 3f, 7f);
            deskLamp.shadows = LightShadows.Soft;
            // Spill from the office windows onto the landing so it reads from across the hall.
            var spill = GrayboxKit.SpotLight("WindowSpill", parent, new Vector3(115.5f, fy + 1.7f, 18f), Quaternion.Euler(20f, 270f, 0f),
                new Color(1f, 0.7f, 0.4f), 4f, 14f, 70f);
            HazeCone("SpillHaze", spill.transform, spill.transform.position, 6f, 2.2f, hazeWarm).transform.rotation =
                Quaternion.FromToRotation(Vector3.down, spill.transform.forward);
        }

        // ------------------------------------------------------------------ dip room (burnt)

        static void BuildDipRoom(Transform parent)
        {
            const float z0 = HallWidth, z1 = HallWidth + 12f, x1 = 16f;
            Box("Floor", parent, new Vector3(x1 * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 + 0.5f, 0.1f, z1 - z0 + 0.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3(x1 * 0.5f, LowRoomHeight + 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 + 0.6f, 0.2f, z1 - z0 + 0.6f), soot);
            Wall("Wall_West", parent, new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), LowRoomHeight, soot);
            Wall("Wall_North", parent, new Vector3(0f, 0f, z1), new Vector3(x1, 0f, z1), LowRoomHeight, soot,
                Opening.Door(13f, 1.4f, 2.4f), Opening.Window(5f, 1.6f, 2.6f, 4.2f, boarded: true));
            Wall("Wall_East", parent, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), LowRoomHeight, soot, Opening.Window(6f, 1.8f, 1.8f, 4.0f));

            Box("ScorchWall", parent, new Vector3(7f, 3.6f, HallWidth - 0.27f), new Vector3(4.6f, 2.4f, 0.03f), soot, false);
            Box("ScorchWall", parent, new Vector3(7f, 2.0f, HallWidth - 0.27f), new Vector3(3.6f, 1.0f, 0.03f), soot, false);
            for (int i = 0; i < 18; i++)
            {
                bool inHall = i < 5;
                Vector3 p = inHall ? new Vector3(Rand(5f, 9f), 0.006f, Rand(21.5f, 23.6f)) : new Vector3(Rand(0.5f, 15.5f), 0.006f, Rand(z0 + 0.5f, z1 - 0.5f));
                Box("ScorchFloor", parent, p, new Vector3(Rand(1f, 3f), 0.01f, Rand(1f, 3f)), soot, false, Quaternion.Euler(0f, Rand(0f, 90f), 0f));
            }

            for (int i = 0; i < 2; i++)
            {
                float z = z0 + 4f + i * 4f;
                var tank = GrayboxKit.Group($"DipTank_{i}", parent).transform;
                Box("Trough", tank, new Vector3(7f, 0.45f, z), new Vector3(8f, 0.9f, 1.4f), iron);
                Box("Liquid", tank, new Vector3(7f, 0.86f, z), new Vector3(7.7f, 0.04f, 1.1f), dipLiquid, false);
                Beam("RackRail", tank, new Vector3(2.5f, 2.6f, z), new Vector3(11.5f, 2.6f, z), 0.12f, 0.12f, iron);
                foreach (float x in new[] { 3f, 11f })
                    Box("RackPost", tank, new Vector3(x, 1.3f, z + 0.8f), new Vector3(0.12f, 2.6f, 0.12f), iron);
            }

            for (int i = 0; i < 9; i++)
            {
                bool tipped = Chance(0.35);
                Vector3 p = new Vector3(Rand(13.4f, 15.2f), 0f, Rand(z0 + 1f, z0 + 5f));
                if (FactoryProps.Place(parent, p, Rand(0f, 360f), tipped ? Synty.BarrelBroken : Synty.BarrelMetal2) == null)
                    Cyl("Drum", parent, p + Vector3.up * (tipped ? 0.3f : 0.45f), 0.6f, 0.9f, i % 3 == 0 ? soot : kit.Rust,
                        tipped ? Quaternion.Euler(0f, Rand(0f, 180f), 0f) * Vector3.right : Vector3.up, true);
            }

            Beam("FallenBeam", parent, new Vector3(2f, 0f, z1 - 2f), new Vector3(6f, LowRoomHeight - 0.2f, z1 - 0.4f), 0.3f, 0.3f, soot, true);
            Beam("FallenBeam", parent, new Vector3(9f, 0.2f, z0 + 1f), new Vector3(12.5f, 1.4f, z0 + 6f), 0.3f, 0.3f, soot); // no collider: crosses the route
            for (int i = 0; i < 8; i++)
                Box("CharredDebris", parent, new Vector3(Rand(1.5f, 6f), Rand(0.1f, 0.4f), Rand(z1 - 3.5f, z1 - 0.6f)), Vector3.one * Rand(0.3f, 0.7f), soot, false,
                    Quaternion.Euler(Rand(0f, 60f), Rand(0f, 90f), Rand(0f, 60f)));
            for (int i = 0; i < 3; i++)
                FactoryProps.Place(parent, new Vector3(Rand(1f, 15f), LowRoomHeight - 0.3f, Rand(z0 + 1f, z1 - 1f)), Rand(0f, 360f), Synty.Cobweb, null, false);

            HangingLamp(parent, new Vector3(8f, 0f, z0 + 2f), LowRoomHeight, LampState.Flickering, "DipRoom", 1.2f);
        }

        // ------------------------------------------------------------------ stairwell (dead end)

        static void BuildStairwell(Transform parent)
        {
            const float x0 = 10f, x1 = 16f, z0 = HallWidth + 12f, z1 = HallWidth + 18f;
            Box("Floor", parent, new Vector3((x0 + x1) * 0.5f, -0.05f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.5f, 0.1f, z1 - z0 + 0.5f), kit.ConcreteFloor);
            Box("Ceiling", parent, new Vector3((x0 + x1) * 0.5f, LowRoomHeight + 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.6f, 0.2f, z1 - z0 + 0.6f), roofBoards);
            Wall("Wall_West", parent, new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), LowRoomHeight, brick);
            Wall("Wall_North", parent, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), LowRoomHeight, brick, Opening.Window(3f, 1.4f, 3.0f, 4.4f));
            Wall("Wall_East", parent, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), LowRoomHeight, brick, Opening.Door(3.5f, 1.2f, 2.3f));

            Box("Door", parent, new Vector3(x1, 1.15f, z0 + 3.5f), new Vector3(0.12f, 2.3f, 1.18f), plank);
            Beam("Chain", parent, new Vector3(x1 - 0.12f, 1.3f, z0 + 2.9f), new Vector3(x1 - 0.12f, 0.9f, z0 + 4.1f), 0.04f, 0.04f, iron);
            Box("Padlock", parent, new Vector3(x1 - 0.15f, 1.1f, z0 + 3.5f), new Vector3(0.08f, 0.14f, 0.1f), kit.Hazard, false);

            Stairs("Stair", parent, new Vector3(x0 + 1f, 0f, z0 + 1f), Vector3.forward, 4f, 2f, 1.5f, kit.Concrete);
            Railing("StairRail", parent, new Vector3(x0 + 1.8f, 0f, z0 + 1f), new Vector3(x0 + 1.8f, 2f, z0 + 5f), kit.Metal);
            for (int i = 0; i < 10; i++)
                Box("Rubble", parent, new Vector3(x0 + Rand(0.5f, 2f), Rand(1.8f, 4.6f), z0 + Rand(4.8f, 5.7f)), Vector3.one * Rand(0.4f, 0.9f),
                    debris, false, Quaternion.Euler(Rand(0f, 90f), Rand(0f, 90f), Rand(0f, 90f)));
            Beam("FallenJoist", parent, new Vector3(x0 + 0.3f, LowRoomHeight, z0 + 5.6f), new Vector3(x0 + 3f, 1.2f, z0 + 4.4f), 0.25f, 0.3f, timber, true);
            Blocker("CollapseBlock", parent, new Vector3(x0 + 1.25f, 3.5f, z0 + 5.4f), new Vector3(2.5f, 3f, 1.2f));
        }

        // ------------------------------------------------------------------ match litter

        /// <summary>Thousands of spilled match sticks merged into one mesh, thickest along the machines' aisle sides.</summary>
        static void BuildMatchLitter(Transform parent)
        {
            AssetFolders.Ensure(ProjectPaths.Meshes);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int count = 6000;
            for (int i = 0; i < count; i++)
            {
                Vector3 c;
                double roll = rng.NextDouble();
                if (roll < 0.35) c = new Vector3(Rand(4f, 112f), 0f, Rand(6.4f, 8.2f));
                else if (roll < 0.7) c = new Vector3(Rand(4f, 112f), 0f, Rand(15.8f, 17.6f));
                else if (roll < 0.9) c = new Vector3(Rand(1f, 119f), 0f, Rand(0.5f, 23.5f));
                else c = new Vector3(Rand(1f, 13f), 0f, Rand(-11f, -1f));
                c.y = 0.004f;
                float yaw = Rand(0f, Mathf.PI);
                var along = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.05f;
                var across = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 0.008f;
                int s = vertices.Count;
                vertices.Add(c - along - across);
                vertices.Add(c - along + across);
                vertices.Add(c + along + across);
                vertices.Add(c + along - across);
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LitterMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "MatchLitter" };
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew)
                AssetDatabase.CreateAsset(mesh, LitterMeshPath);
            else
                EditorUtility.SetDirty(mesh);

            var go = MeshObject("MatchLitter", parent, mesh, matchWood, Vector3.zero, Quaternion.identity, Vector3.one);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ main breaker

        /// <summary>
        /// Main breaker on the receiving room's south wall, just right of the roll-up door as you come
        /// in from the courtyard. It starts OFF; a red pilot light and a humming generator mark it.
        /// </summary>
        static void BuildMainBreaker(Transform parent)
        {
            Vector3 face = new Vector3(11f, 1.4f, -12f + WallThickness * 0.5f + 0.07f);
            parent.position = face;
            Box("Box", parent, face, new Vector3(0.7f, 0.9f, 0.14f), kit.Metal);
            Box("Label", parent, face + new Vector3(0f, 0.33f, 0.071f), new Vector3(0.45f, 0.08f, 0.01f), kit.Hazard, false);
            Box("PilotLamp", parent, face + new Vector3(0.25f, 0.33f, 0.08f), new Vector3(0.06f, 0.06f, 0.03f), pilotRed, false);
            GrayboxKit.PointLight("PilotGlow", parent, face + new Vector3(0.25f, 0.33f, 0.25f), new Color(1f, 0.1f, 0.05f), 0.8f, 2.5f);
            Beam("Conduit", parent, face + new Vector3(0f, 0.45f, -0.02f), face + new Vector3(0f, LowRoomHeight - 1.4f, -0.02f), 0.06f, 0.06f, kit.Metal);
            FactoryProps.Place(parent, new Vector3(12.6f, 0f, -10.8f), 180f, Synty.Generator);
            FactoryProps.Place(parent, new Vector3(11.8f, 0f, -11.3f), 90f, Synty.Cable, null, false);

            var pivot = GrayboxKit.Group("LeverPivot", parent);
            pivot.transform.position = face + new Vector3(0f, -0.05f, 0.09f);
            var handle = Box("Handle", pivot.transform, pivot.transform.position + new Vector3(0f, 0.13f, 0f),
                new Vector3(0.08f, 0.26f, 0.08f), kit.Hazard, false);
            GameObjectUtility.SetStaticEditorFlags(handle, 0);

            var breaker = parent.gameObject.AddComponent<LightSwitch>();
            SerializedFields.SetBool(breaker, "startOn", false);
            SetString(breaker, "turnOnPrompt", "Throw the main breaker");
            SetString(breaker, "turnOffPrompt", "Cut the main breaker");
            SerializedFields.SetArray(breaker, "lights", BreakerLights);
            SerializedFields.SetArray(breaker, "fixtures", BreakerFixtures);
            SerializedFields.Set(breaker, "lever", pivot.transform);
        }
    }
}
