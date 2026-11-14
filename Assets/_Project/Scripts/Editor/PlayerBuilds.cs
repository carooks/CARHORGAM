using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Builds playable Windows .exe test builds from the command line or the menu.
    /// Output goes to Builds/ (git-ignored) at the project root.
    /// </summary>
    public static class PlayerBuilds
    {
        [MenuItem("Horror Game/Build Foreman Factory Test (.exe)", priority = 40)]
        static void BuildFactoryMenu() => BuildForemanFactoryTest();

        /// <summary>
        /// Batch entry point: -executeMethod HorrorGame.EditorTools.PlayerBuilds.BuildForemanFactoryTest
        /// Builds only the match factory scene (with the Foreman) to Builds/ForemanFactory/ForemanFactory.exe.
        /// </summary>
        public static void BuildForemanFactoryTest() =>
            Build("Foreman factory", FactoryInteriorBuilder.ScenePath, "Builds/ForemanFactory/ForemanFactory.exe");

        [MenuItem("Horror Game/Build Haunted Painting Test (.exe)", priority = 41)]
        static void BuildPaintingMenu() => BuildHauntedPaintingTest();

        /// <summary>
        /// Batch entry point: -executeMethod HorrorGame.EditorTools.PlayerBuilds.BuildHauntedPaintingTest
        /// Builds only the haunted painting test scene to Builds/HauntedPainting/HauntedPainting.exe.
        /// </summary>
        public static void BuildHauntedPaintingTest() =>
            Build("Haunted painting", PaintingBuilder.ScenePath, "Builds/HauntedPainting/HauntedPainting.exe");

        static void Build(string label, string scenePath, string exePath)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Horror Game] {label} build: {summary.result}, {summary.totalErrors} errors, " +
                      $"{summary.totalSize / (1024 * 1024)} MB, at {options.locationPathName}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
