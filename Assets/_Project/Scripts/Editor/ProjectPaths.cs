namespace HorrorGame.EditorTools
{
    /// <summary>Where the generated graybox assets live. Everything we own sits under Assets/_Project.</summary>
    public static class ProjectPaths
    {
        public const string Root = "Assets/_Project";
        public const string Scenes = Root + "/Scenes";
        public const string Materials = Root + "/Materials";
        public const string Prefabs = Root + "/Prefabs";
        public const string Settings = Root + "/Settings";
        public const string Meshes = Root + "/Meshes";

        public const string Phase1Scene = Scenes + "/Phase1_Graybox.unity";
        public const string PlayerPrefab = Prefabs + "/Player.prefab";

        // Real-scale Wadsworth blockout (data written by Tools/CityBlockout/build_blockout.py)
        public const string WadsworthScene = Scenes + "/Wadsworth_Blockout.unity";
        public const string WadsworthData = Root + "/Data/Wadsworth/wadsworth_blockout.json";
        public const string WadsworthMeshes = Meshes + "/Wadsworth_Blockout.asset";
    }
}
