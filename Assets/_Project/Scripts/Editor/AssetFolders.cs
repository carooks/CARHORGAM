using UnityEditor;

namespace HorrorGame.EditorTools
{
    public static class AssetFolders
    {
        /// <summary>Creates every missing folder in an "Assets/..." path.</summary>
        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
