using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Support for the Leartes Studios "Abandoned Village" pack, which ships for HDRP.
    ///
    /// 1. While the package imports, its textures are capped at 2048 px (the source maps go up to
    ///    8K and make the pack about 1.9 GB).
    /// 2. Horror Game > Art > Convert Abandoned Village Materials to URP rebuilds every material
    ///    under the pack folder on URP/Lit, reading the texture slots the HDRP materials saved
    ///    (base colour, normal, mask map) so it works even though HDRP isn't installed.
    ///
    /// HDRP mask maps pack metallic (R), occlusion (G) and smoothness (A), which matches what
    /// URP/Lit reads from its metallic map (R metallic, A smoothness), so the same texture is used
    /// for both the metallic and occlusion slots.
    /// </summary>
    public class AbandonedVillageImport : AssetPostprocessor
    {
        public const string PackRoot = "Assets/LeartesStudios";
        const int MaxTextureSize = 2048;

        static readonly string[] CutoutHints =
            { "leaf", "leaves", "grass", "wheat", "bush", "foliage", "plant", "fern", "ivy", "branch", "hay" };

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PackRoot))
                return;
            var importer = (TextureImporter)assetImporter;
            if (importer.maxTextureSize > MaxTextureSize)
                importer.maxTextureSize = MaxTextureSize;
        }

        [MenuItem("Horror Game/Art/Convert Abandoned Village Materials to URP", priority = 40)]
        public static void ConvertAll()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[Horror Game] URP Lit shader not found.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(PackRoot))
            {
                Debug.LogError("[Horror Game] " + PackRoot + " not found. Import the Abandoned Village package first.");
                return;
            }

            int converted = 0, skipped = 0;
            var noBaseMap = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PackRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                    continue;
                if (material.shader == lit && material.GetTexture("_BaseMap") != null)
                {
                    skipped++;
                    continue;
                }
                if (!Convert(material, lit))
                    noBaseMap.Add(path);
                converted++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Horror Game] Abandoned Village: converted {converted} materials to URP/Lit, {skipped} already done.");
            if (noBaseMap.Count > 0)
                Debug.LogWarning("[Horror Game] No base colour texture found for " + noBaseMap.Count +
                                 " materials (left as flat colour):\n" + string.Join("\n", noBaseMap));
        }

        /// <summary>Returns false if no base colour texture was found.</summary>
        static bool Convert(Material material, Shader lit)
        {
            // Read what the HDRP material saved before switching shader (switching drops unknown slots).
            var textures = new Dictionary<string, Texture>();
            var floats = new Dictionary<string, float>();
            var colors = new Dictionary<string, Color>();
            var so = new SerializedObject(material);
            var saved = so.FindProperty("m_SavedProperties");
            ReadTextures(saved.FindPropertyRelative("m_TexEnvs"), textures);
            ReadFloats(saved.FindPropertyRelative("m_Floats"), floats);
            ReadColors(saved.FindPropertyRelative("m_Colors"), colors);

            Texture baseMap = Pick(textures, "_BaseColorMap", "_BaseMap", "_MainTex", "_Albedo", "_BaseColor", "_Diffuse", "_Color");
            Texture normal = Pick(textures, "_NormalMap", "_BumpMap", "_Normal");
            Texture mask = Pick(textures, "_MaskMap", "_Mask", "_ORM", "_MetallicGlossMap");
            Color tint = colors.TryGetValue("_BaseColor", out var c) ? c : Color.white;
            if (tint.maxColorComponent < 0.05f)
                tint = Color.white; // some graphs save black when the colour wasn't exposed

            bool cutout = (floats.TryGetValue("_AlphaCutoffEnable", out var a) && a > 0.5f) || LooksLikeFoliage(material, baseMap);
            bool doubleSided = (floats.TryGetValue("_DoubleSidedEnable", out var d) && d > 0.5f) || cutout;
            float cutoff = floats.TryGetValue("_AlphaCutoff", out var co) ? co : 0.4f;

            material.shader = lit;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_MainTex", baseMap);

            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", floats.TryGetValue("_NormalScale", out var ns) ? ns : 1f);
            SetKeyword(material, "_NORMALMAP", normal != null);

            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_OcclusionMap", mask);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", mask != null);
            SetKeyword(material, "_OCCLUSIONMAP", mask != null);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", mask != null ? 1f : (floats.TryGetValue("_Smoothness", out var s) ? s : 0.3f));
            material.SetFloat("_Metallic", mask != null ? 1f : (floats.TryGetValue("_Metallic", out var m) ? m : 0f));

            material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            material.SetFloat("_Cutoff", cutoff);
            SetKeyword(material, "_ALPHATEST_ON", cutout);
            material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.renderQueue = cutout ? (int)RenderQueue.AlphaTest : -1;

            EditorUtility.SetDirty(material);
            return baseMap != null;
        }

        static bool LooksLikeFoliage(Material material, Texture baseMap)
        {
            string name = (material.name + " " + (baseMap != null ? baseMap.name : "")).ToLowerInvariant();
            foreach (string hint in CutoutHints)
                if (name.Contains(hint))
                    return true;
            return false;
        }

        static Texture Pick(Dictionary<string, Texture> textures, params string[] names)
        {
            foreach (string n in names)
                if (textures.TryGetValue(n, out var t) && t != null)
                    return t;
            // Shader-graph materials use their own slot names: fall back to a loose match.
            foreach (var pair in textures)
            {
                if (pair.Value == null)
                    continue;
                string key = pair.Key.ToLowerInvariant();
                foreach (string n in names)
                    if (key.Contains(n.TrimStart('_').ToLowerInvariant()))
                        return pair.Value;
            }
            return null;
        }

        static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        static void ReadTextures(SerializedProperty array, Dictionary<string, Texture> into)
        {
            if (array == null) return;
            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                var tex = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                into[entry.FindPropertyRelative("first").stringValue] = tex;
            }
        }

        static void ReadFloats(SerializedProperty array, Dictionary<string, float> into)
        {
            if (array == null) return;
            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                into[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").floatValue;
            }
        }

        static void ReadColors(SerializedProperty array, Dictionary<string, Color> into)
        {
            if (array == null) return;
            for (int i = 0; i < array.arraySize; i++)
            {
                var entry = array.GetArrayElementAtIndex(i);
                into[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").colorValue;
            }
        }
    }
}
