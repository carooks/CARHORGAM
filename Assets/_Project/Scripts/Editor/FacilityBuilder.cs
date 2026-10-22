using System.Collections.Generic;
using HorrorGame.Audio;
using HorrorGame.World;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Builds the industrial facility interior from a text grid. Each character is one 3 m cell,
    /// so the layout can be redesigned by editing <see cref="Layout"/> and rebuilding the scene.
    /// Row 0 is the north wall; the bottom row faces south toward the parking lot.
    /// </summary>
    public static class FacilityBuilder
    {
        public const float CellSize = 3f;
        public const float WallHeight = 4f;
        const float PlatformHeight = 1.5f;

        // Legend
        //   #  wall                .  floor
        //   O  doorway in the outer wall (the entrance)
        //   L  ceiling lamp        l  flickering, buzzing ceiling lamp
        //   K  storage shelf       C  crate stack
        //   T  humming tank        p  pillar
        //   r  ramp rising north   h  raised platform (mezzanine)
        //   $  scrap item on the floor (type picked by ScrapBuilder)
        public static readonly string[] Layout =
        {
            "#################",
            "#K$K.K.#T..T..T.#",
            "#..l...#.$.L....#",
            "#K.K.K.#T..T..T.#",
            "#...L....p.$.p..#",
            "###.####........#",
            "#..$....L..l#####",
            "#.p..p..#.....$.#",
            "#...h...#..C..K.#",
            "#...r.L.#.l.$.K.#",
            "#.C...$.#...C...#",
            "##.######.......#",
            "#...L.......L...#",
            "#C.............C#",
            "########O########",
        };

        static int Rows => Layout.Length;
        static int Columns => Layout[0].Length;
        public static float Width => Columns * CellSize;
        public static float Depth => Rows * CellSize;

        /// <summary>World position of the centre of the entrance doorway at floor level.</summary>
        public static Vector3 EntrancePosition(Vector3 origin)
        {
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                if (Layout[r][c] == 'O')
                    return CellCenter(origin, r, c);
            return origin;
        }

        /// <summary>Builds the facility with its south-west corner at <paramref name="origin"/>.</summary>
        public static void Build(Transform parent, GrayboxKit kit, Vector3 origin)
        {
            var root = GrayboxKit.Group("Facility", parent).transform;
            var structure = GrayboxKit.Group("Structure", root).transform;
            var props = GrayboxKit.Group("Props", root).transform;
            var lights = GrayboxKit.Group("Lights", root).transform;
            var rng = new System.Random(20261021);
            // Separate generator so adding scrap doesn't reshuffle the shelves and crates.
            var scrapRng = new System.Random(4242);
            var lampLights = new List<Object>();
            var lampFixtures = new List<Object>();

            Vector3 center = origin + new Vector3(Width * 0.5f, 0f, Depth * 0.5f);
            kit.Box("Floor", structure, center + new Vector3(0f, -0.03f, 0f), new Vector3(Width, 0.1f, Depth), kit.ConcreteFloor);
            kit.Box("Ceiling", structure, center + new Vector3(0f, WallHeight + 0.1f, 0f), new Vector3(Width, 0.2f, Depth), kit.Ceiling);
            BuildWalls(structure, kit, origin);

            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                char cell = Layout[r][c];
                Vector3 p = CellCenter(origin, r, c);
                string id = $"{r:00}_{c:00}";
                switch (cell)
                {
                    case 'O':
                        kit.Box($"DoorHeader_{id}", structure, p + new Vector3(0f, 3.3f, 0f), new Vector3(CellSize, 1.4f, CellSize), kit.Wall);
                        kit.Box($"DoorStripe_{id}", structure, p + new Vector3(0f, 2.55f, -CellSize * 0.5f - 0.01f), new Vector3(CellSize, 0.15f, 0.02f), kit.Hazard, false);
                        break;
                    case 'L':
                    case 'l':
                        var (lampLight, fixture) = CeilingLamp(lights, kit, p, id, cell == 'l');
                        lampLights.Add(lampLight.gameObject);
                        lampFixtures.Add(fixture);
                        break;
                    case 'K':
                        Shelf(props, kit, p, id, rng);
                        break;
                    case 'C':
                        Crates(props, kit, p, id, rng);
                        break;
                    case 'T':
                        Tank(props, kit, p, id);
                        break;
                    case 'p':
                        kit.Box($"Pillar_{id}", structure, p + new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(0.7f, WallHeight, 0.7f), kit.Concrete);
                        kit.Box($"PillarStripe_{id}", structure, p + new Vector3(0f, 0.4f, 0f), new Vector3(0.74f, 0.25f, 0.74f), kit.Hazard, false);
                        break;
                    case 'r':
                        Ramp(structure, kit, p, id);
                        break;
                    case 'h':
                        Platform(structure, kit, origin, r, c, id);
                        break;
                    case '$':
                        ScrapBuilder.PlaceRandom(props, kit, p, scrapRng);
                        break;
                }
            }

            BreakerPanel(props, kit, EntrancePosition(origin), lampLights, lampFixtures);
        }

        static Vector3 CellCenter(Vector3 origin, int row, int column)
        {
            return origin + new Vector3((column + 0.5f) * CellSize, 0f, (Rows - 1 - row + 0.5f) * CellSize);
        }

        static bool IsWall(int row, int column)
        {
            return row < 0 || row >= Rows || column < 0 || column >= Columns || Layout[row][column] == '#';
        }

        /// <summary>Merges each horizontal run of '#' cells into a single box to keep the object count low.</summary>
        static void BuildWalls(Transform parent, GrayboxKit kit, Vector3 origin)
        {
            for (int r = 0; r < Rows; r++)
            {
                int c = 0;
                while (c < Columns)
                {
                    if (Layout[r][c] != '#')
                    {
                        c++;
                        continue;
                    }
                    int start = c;
                    while (c < Columns && Layout[r][c] == '#')
                        c++;
                    int length = c - start;
                    Vector3 a = CellCenter(origin, r, start);
                    Vector3 b = CellCenter(origin, r, c - 1);
                    kit.Box($"Wall_{r:00}_{start:00}", parent, (a + b) * 0.5f + Vector3.up * WallHeight * 0.5f,
                        new Vector3(length * CellSize, WallHeight, CellSize), kit.Wall);
                }
            }
        }

        static (Light light, Renderer fixture) CeilingLamp(Transform parent, GrayboxKit kit, Vector3 p, string id, bool faulty)
        {
            var fixture = kit.Box($"LampFixture_{id}", parent, p + new Vector3(0f, WallHeight - 0.05f, 0f),
                new Vector3(1.4f, 0.08f, 0.3f), kit.LampWarm, false);
            var light = GrayboxKit.PointLight($"LampLight_{id}", parent, p + new Vector3(0f, WallHeight - 0.4f, 0f),
                new Color(1f, 0.85f, 0.62f), faulty ? 5f : 6f, 9f);
            var fixtureRenderer = fixture.GetComponent<Renderer>();
            if (!faulty)
                return (light, fixtureRenderer);

            var flicker = light.gameObject.AddComponent<LightFlicker>();
            // LightFlicker dims the fixture through a per-renderer property block, so other lamps
            // sharing the material are unaffected.
            SerializedFields.Set(flicker, "emissiveRenderer", fixtureRenderer);

            var buzz = light.gameObject.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(buzz, "sound", (int)ProceduralSound.ElectricalBuzz);
            SerializedFields.SetFloat(buzz, "volume", 0.25f);
            SerializedFields.SetBool(buzz, "spatial", true);
            SerializedFields.SetFloat(buzz, "maxDistance", 8f);
            return (light, fixtureRenderer);
        }

        /// <summary>
        /// Breaker box on the outside wall, left of the entrance as you approach from the parking lot.
        /// Flipping it switches every facility ceiling lamp on or off.
        /// </summary>
        static void BreakerPanel(Transform parent, GrayboxKit kit, Vector3 entrance, List<Object> lampLights,
            List<Object> lampFixtures)
        {
            // The entrance cell's outer face is half a cell south of its centre.
            Vector3 wallFace = entrance + new Vector3(-2.5f, 1.4f, -CellSize * 0.5f);
            var panel = GrayboxKit.Group("BreakerPanel", parent);
            panel.transform.position = wallFace + new Vector3(0f, 0f, -0.06f);
            kit.Box("Box", panel.transform, panel.transform.position, new Vector3(0.6f, 0.8f, 0.12f), kit.Metal);
            kit.Box("Label", panel.transform, panel.transform.position + new Vector3(0f, 0.3f, -0.061f),
                new Vector3(0.4f, 0.08f, 0.01f), kit.Hazard, false);

            // The lever moves, so it must not be marked static (static batching would freeze it).
            var pivot = GrayboxKit.Group("LeverPivot", panel.transform);
            pivot.transform.position = panel.transform.position + new Vector3(0f, -0.05f, -0.08f);
            var handle = kit.Box("Handle", pivot.transform, pivot.transform.position + new Vector3(0f, 0.12f, 0f),
                new Vector3(0.07f, 0.24f, 0.07f), kit.Hazard, false);
            GameObjectUtility.SetStaticEditorFlags(handle, 0);

            var breaker = panel.AddComponent<LightSwitch>();
            SerializedFields.SetArray(breaker, "lights", lampLights);
            SerializedFields.SetArray(breaker, "fixtures", lampFixtures);
            SerializedFields.Set(breaker, "lever", pivot.transform);
        }

        static void Shelf(Transform parent, GrayboxKit kit, Vector3 p, string id, System.Random rng)
        {
            var shelf = GrayboxKit.Group($"Shelf_{id}", parent).transform;
            const float width = 2.4f, depth = 0.8f, height = 2.4f;
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                kit.Box("Post", shelf, p + new Vector3(x * (width * 0.5f - 0.04f), height * 0.5f, z * (depth * 0.5f - 0.04f)),
                    new Vector3(0.08f, height, 0.08f), kit.Metal);
            for (int level = 0; level < 3; level++)
            {
                float y = 0.15f + level * 0.95f;
                kit.Box("Board", shelf, p + new Vector3(0f, y, 0f), new Vector3(width, 0.05f, depth), kit.Rust);
                int boxes = rng.Next(1, 4);
                for (int i = 0; i < boxes; i++)
                {
                    float size = 0.3f + (float)rng.NextDouble() * 0.3f;
                    float offset = -width * 0.5f + 0.35f + i * 0.75f + (float)rng.NextDouble() * 0.1f;
                    kit.Box("Box", shelf, p + new Vector3(offset, y + 0.025f + size * 0.5f, 0f),
                        new Vector3(size, size, size * 0.9f), kit.Cardboard);
                }
            }
        }

        static void Crates(Transform parent, GrayboxKit kit, Vector3 p, string id, System.Random rng)
        {
            var group = GrayboxKit.Group($"Crates_{id}", parent).transform;
            int count = rng.Next(2, 4);
            for (int i = 0; i < count; i++)
            {
                float size = 0.9f + (float)rng.NextDouble() * 0.3f;
                var offset = new Vector3((float)rng.NextDouble() * 1.4f - 0.7f, 0f, (float)rng.NextDouble() * 1.4f - 0.7f);
                var rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 30f - 15f, 0f);
                kit.Box("Crate", group, p + offset + Vector3.up * size * 0.5f, Vector3.one * size, kit.Crate, true, rotation);
                if (i == 0)
                    kit.Box("Crate", group, p + offset + Vector3.up * (size + 0.4f), Vector3.one * 0.8f, kit.Crate, true, rotation);
            }
        }

        static void Tank(Transform parent, GrayboxKit kit, Vector3 p, string id)
        {
            var group = GrayboxKit.Group($"Tank_{id}", parent).transform;
            kit.Cylinder("Body", group, p + Vector3.up * 1.6f, new Vector3(2.2f, 3.2f, 2.2f), kit.Metal);
            kit.Cylinder("Band", group, p + Vector3.up * 0.5f, new Vector3(2.26f, 0.2f, 2.26f), kit.Hazard, false);
            kit.Cylinder("Pipe", group, p + new Vector3(0f, 3.6f, 0f), new Vector3(0.25f, 0.8f, 0.25f), kit.Rust, false);

            var hum = GrayboxKit.Group("Hum", group);
            hum.transform.position = p + Vector3.up * 1.5f;
            var sound = hum.AddComponent<AmbientSound>();
            SerializedFields.SetEnum(sound, "sound", (int)ProceduralSound.MachineHum);
            SerializedFields.SetFloat(sound, "volume", 0.35f);
            SerializedFields.SetBool(sound, "spatial", true);
            SerializedFields.SetFloat(sound, "maxDistance", 10f);
        }

        static void Ramp(Transform parent, GrayboxKit kit, Vector3 p, string id)
        {
            const float thickness = 0.2f;
            float length = Mathf.Sqrt(CellSize * CellSize + PlatformHeight * PlatformHeight);
            float angle = Mathf.Atan2(PlatformHeight, CellSize) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(-angle, 0f, 0f);
            Vector3 surfaceCenter = p + Vector3.up * (PlatformHeight * 0.5f);
            Vector3 normal = rotation * Vector3.up;
            kit.Box($"Ramp_{id}", parent, surfaceCenter - normal * (thickness * 0.5f),
                new Vector3(CellSize - 0.4f, thickness, length), kit.Metal, true, rotation);
        }

        static void Platform(Transform parent, GrayboxKit kit, Vector3 origin, int r, int c, string id)
        {
            Vector3 p = CellCenter(origin, r, c);
            kit.Box($"Platform_{id}", parent, p + Vector3.up * (PlatformHeight * 0.5f),
                new Vector3(CellSize, PlatformHeight, CellSize), kit.Concrete);

            // Railings on every open edge that isn't a ramp or another platform cell.
            (int dr, int dc, Vector3 offset, Vector3 size)[] edges =
            {
                (-1, 0, new Vector3(0f, 0f, CellSize * 0.5f), new Vector3(CellSize, 1f, 0.08f)),
                (1, 0, new Vector3(0f, 0f, -CellSize * 0.5f), new Vector3(CellSize, 1f, 0.08f)),
                (0, -1, new Vector3(-CellSize * 0.5f, 0f, 0f), new Vector3(0.08f, 1f, CellSize)),
                (0, 1, new Vector3(CellSize * 0.5f, 0f, 0f), new Vector3(0.08f, 1f, CellSize)),
            };
            foreach (var edge in edges)
            {
                int nr = r + edge.dr, nc = c + edge.dc;
                if (IsWall(nr, nc))
                    continue;
                char neighbour = Layout[nr][nc];
                if (neighbour == 'r' || neighbour == 'h')
                    continue;
                kit.Box($"Railing_{id}", parent, p + edge.offset + Vector3.up * (PlatformHeight + 0.5f), edge.size, kit.Hazard);
            }
        }
    }
}
