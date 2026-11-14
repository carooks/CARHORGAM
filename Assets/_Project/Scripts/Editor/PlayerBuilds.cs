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
        public static void BuildForemanFactoryTest()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { FactoryInteriorBuilder.ScenePath },
                locationPathName = "Builds/ForemanFactory/ForemanFactory.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Horror Game] Foreman factory build: {summary.result}, {summary.totalErrors} errors, " +
                      $"{summary.totalSize / (1024 * 1024)} MB, at {options.locationPathName}");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
