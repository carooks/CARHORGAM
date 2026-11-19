using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Finds Synty POLYGON prefabs by name when they are imported under Assets/Synty, and places them.
    /// The packs are bought assets and are never committed (Assets/Synty/ is git-ignored), so every
    /// placement has a primitive fallback: on a machine without the packs the factory still builds,
    /// just plainer.
    /// Names are path endings without ".prefab", e.g. "SM_Gen_Prop_Crate_01" or
    /// "PolygonDungeon/Prefabs/Props/SM_Prop_Crate_Metal_01" when the bare name is ambiguous.
    /// </summary>
    public static class FactoryProps
    {
        const string SyntyRoot = "Assets/Synty";
        const StaticEditorFlags StaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        static List<string> paths;
        static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        public static int Placed { get; private set; }
        public static int FellBack { get; private set; }

        /// <summary>True when any Synty pack is imported.</summary>
        public static bool Available
        {
            get
            {
                Index();
                return paths.Count > 0;
            }
        }

        public static void Reset()
        {
            paths = null;
            Cache.Clear();
            Placed = 0;
            FellBack = 0;
        }

        static void Index()
        {
            if (paths != null)
                return;
            paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(SyntyRoot))
                return;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SyntyRoot }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>The first of <paramref name="names"/> that exists, or null.</summary>
        public static GameObject Find(params string[] names)
        {
            Index();
            foreach (string name in names)
            {
                if (Cache.TryGetValue(name, out var cached))
                {
                    if (cached != null)
                        return cached;
                    continue;
                }
                GameObject found = null;
                string suffix = "/" + name + ".prefab";
                foreach (string path in paths)
                {
                    if (!path.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                        continue;
                    found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    break;
                }
                Cache[name] = found;
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Places the first available prefab of <paramref name="names"/> at <paramref name="position"/>
        /// (Synty pivots sit at the base) with a yaw in degrees. If none is available, calls
        /// <paramref name="fallback"/> instead (which may be null to place nothing).
        /// With <paramref name="collider"/>, a prefab without colliders gets a box collider fitted to its meshes.
        /// </summary>
        public static GameObject Place(Transform parent, Vector3 position, float yaw, string[] names,
            System.Func<GameObject> fallback = null, bool collider = true, float scale = 1f)
        {
            var prefab = Find(names);
            if (prefab == null)
            {
                FellBack++;
                return fallback?.Invoke();
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale *= scale;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticFlags);
            if (collider && go.GetComponentInChildren<Collider>() == null)
                FitBoxCollider(go);
            Placed++;
            return go;
        }

        /// <summary>Same as <see cref="Place"/> but with a full rotation.</summary>
        public static GameObject Place(Transform parent, Vector3 position, Quaternion rotation, string[] names,
            System.Func<GameObject> fallback = null, bool collider = true, float scale = 1f)
        {
            var go = Place(parent, position, 0f, names, fallback, collider, scale);
            if (go != null && Find(names) != null)
                go.transform.rotation = rotation;
            return go;
        }

        static void FitBoxCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            var inverse = go.transform.worldToLocalMatrix;
            box.center = inverse.MultiplyPoint3x4(bounds.center);
            Vector3 size = inverse.MultiplyVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }
    }
}
