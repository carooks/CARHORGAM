using HorrorGame.Items;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Builds collectible scrap items from primitives. Each entry in <see cref="Catalogue"/> is one kind
    /// of item with its value range and weight; edit it to rebalance or add items, then rebuild the scene.
    /// Values can also be tweaked on any placed item's ScrapItem component in the Inspector.
    /// </summary>
    public static class ScrapBuilder
    {
        /// <summary>Builds the item's shape around <c>root</c>, with the item's base at <c>p</c>.</summary>
        delegate void ShapeBuilder(Transform root, GrayboxKit kit, Vector3 p);

        readonly struct ScrapKind
        {
            public readonly string Name;
            public readonly int MinValue, MaxValue;
            public readonly float Weight;
            public readonly ShapeBuilder Build;

            public ScrapKind(string name, int minValue, int maxValue, float weight, ShapeBuilder build)
            {
                Name = name;
                MinValue = minValue;
                MaxValue = maxValue;
                Weight = weight;
                Build = build;
            }
        }

        static readonly ScrapKind[] Catalogue =
        {
            new ScrapKind("Old Radio", 20, 45, 6f, (root, kit, p) =>
            {
                kit.Box("Body", root, p + new Vector3(0f, 0.11f, 0f), new Vector3(0.36f, 0.22f, 0.16f), kit.Metal);
                kit.Box("Antenna", root, p + new Vector3(0.12f, 0.34f, 0f), new Vector3(0.02f, 0.25f, 0.02f), kit.Metal, false);
            }),
            new ScrapKind("Toolbox", 30, 60, 12f, (root, kit, p) =>
            {
                kit.Box("Box", root, p + new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.2f, 0.22f), kit.ScrapRed);
                kit.Box("Handle", root, p + new Vector3(0f, 0.23f, 0f), new Vector3(0.3f, 0.04f, 0.04f), kit.Metal, false);
            }),
            new ScrapKind("Brass Bell", 45, 90, 8f, (root, kit, p) =>
            {
                kit.Cylinder("Bell", root, p + new Vector3(0f, 0.15f, 0f), new Vector3(0.28f, 0.3f, 0.28f), kit.ScrapBrass);
                kit.Cylinder("Knob", root, p + new Vector3(0f, 0.34f, 0f), new Vector3(0.06f, 0.08f, 0.06f), kit.ScrapBrass, false);
            }),
            new ScrapKind("Gas Can", 15, 35, 15f, (root, kit, p) =>
            {
                kit.Box("Can", root, p + new Vector3(0f, 0.18f, 0f), new Vector3(0.3f, 0.36f, 0.16f), kit.ScrapRed);
                kit.Box("Spout", root, p + new Vector3(0.1f, 0.4f, 0f), new Vector3(0.05f, 0.1f, 0.05f), kit.Metal, false);
            }),
            new ScrapKind("Teddy Bear", 5, 25, 2f, (root, kit, p) =>
            {
                kit.Box("Body", root, p + new Vector3(0f, 0.12f, 0f), new Vector3(0.2f, 0.24f, 0.14f), kit.ScrapPlush);
                kit.Box("Head", root, p + new Vector3(0f, 0.31f, 0f), new Vector3(0.16f, 0.14f, 0.13f), kit.ScrapPlush);
            }),
            new ScrapKind("Candlestick", 35, 70, 4f, (root, kit, p) =>
            {
                kit.Cylinder("Base", root, p + new Vector3(0f, 0.015f, 0f), new Vector3(0.16f, 0.03f, 0.16f), kit.ScrapBrass);
                kit.Cylinder("Stem", root, p + new Vector3(0f, 0.2f, 0f), new Vector3(0.06f, 0.35f, 0.06f), kit.ScrapBrass);
            }),
        };

        const float FloorTop = 0.02f;

        /// <summary>Places a random kind of scrap somewhere inside the 3 m cell centred on <paramref name="cellCenter"/>.</summary>
        public static GameObject PlaceRandom(Transform parent, GrayboxKit kit, Vector3 cellCenter, System.Random rng)
        {
            var kind = Catalogue[rng.Next(Catalogue.Length)];
            var offset = new Vector3((float)rng.NextDouble() * 1.6f - 0.8f, FloorTop, (float)rng.NextDouble() * 1.6f - 0.8f);
            float yaw = (float)rng.NextDouble() * 360f;
            return Place(parent, kit, kind, cellCenter + offset, yaw);
        }

        static GameObject Place(Transform parent, GrayboxKit kit, ScrapKind kind, Vector3 position, float yaw)
        {
            var root = GrayboxKit.Group("Scrap_" + kind.Name.Replace(' ', '_'), parent);
            root.transform.position = position;
            kind.Build(root.transform, kit, position);
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Scrap gets picked up and moved, so none of it may be static (static batching would freeze it).
            foreach (Transform part in root.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);

            var item = root.AddComponent<ScrapItem>();
            SerializedFields.SetString(item, "displayName", kind.Name);
            SerializedFields.SetVector2Int(item, "valueRange", new Vector2Int(kind.MinValue, kind.MaxValue));
            SerializedFields.SetFloat(item, "weight", kind.Weight);
            return root;
        }
    }
}
