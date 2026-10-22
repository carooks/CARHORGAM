using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// First-open setup. When the project is opened and the Phase 1 scene doesn't exist yet,
    /// configures URP, generates the scene and opens it, so a fresh clone is ready to press Play.
    /// Once the scene exists it only adds new components to the player prefab (use the Horror Game
    /// menu to rebuild the scene).
    /// </summary>
    [InitializeOnLoad]
    static class ProjectBootstrap
    {
        const string SessionKey = "HorrorGame.BootstrapChecked";

        static ProjectBootstrap()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false))
                return;
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (GraphicsSettings.defaultRenderPipeline == null)
                RenderPipelineSetup.EnsureUrp();

            if (System.IO.File.Exists(ProjectPaths.Phase1Scene))
            {
                PlayerPrefabBuilder.EnsurePrefab();
                return;
            }

            Debug.Log("[Horror Game] First open: generating the Phase 1 graybox scene...");
            Phase1SceneBuilder.Build();
            EditorSceneManager.OpenScene(ProjectPaths.Phase1Scene);
        }
    }
}
