using HorrorGame.Interaction;
using HorrorGame.Items;
using HorrorGame.Player;
using HorrorGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Creates Assets/_Project/Prefabs/Player.prefab if it doesn't exist yet, and adds components
    /// introduced by later milestones to an existing prefab (without touching anything else on it).
    ///
    /// Player                (CharacterController, PlayerInputReader, PlayerMotor, PlayerLook,
    ///                        PlayerInteractor, InteractionPromptUI, PlayerInventory, InventoryHUD)
    ///   CameraRoot          (pitch pivot at eye height)
    ///     PlayerCamera      (Camera, AudioListener)
    ///       Flashlight      (Spot Light, Flashlight)
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const float EyeHeight = 1.62f;

        public static GameObject EnsurePrefab(bool overwrite = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PlayerPrefab);
            if (existing != null && !overwrite)
            {
                UpgradeExisting();
                return AssetDatabase.LoadAssetAtPath<GameObject>(ProjectPaths.PlayerPrefab);
            }

            AssetFolders.Ensure(ProjectPaths.Prefabs);
            var root = new GameObject("Player") { tag = "Player" };

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.05f;

            root.AddComponent<PlayerInputReader>();
            root.AddComponent<PlayerMotor>();
            var look = root.AddComponent<PlayerLook>();

            var cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(root.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
            SerializedFields.Set(look, "cameraRoot", cameraRoot.transform);

            var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(cameraRoot.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 150f;
            camera.fieldOfView = 72f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Phase1SceneBuilder.FogColor;
            cameraObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;

            var flashlightObject = new GameObject("Flashlight");
            flashlightObject.transform.SetParent(cameraObject.transform, false);
            flashlightObject.transform.localPosition = new Vector3(0.2f, -0.15f, 0.1f);
            var beam = flashlightObject.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.95f, 0.85f);
            beam.intensity = 12f;
            beam.range = 25f;
            beam.spotAngle = 58f;
            beam.innerSpotAngle = 22f;
            beam.shadows = LightShadows.Soft;
            beam.shadowStrength = 0.9f;
            flashlightObject.AddComponent<Flashlight>();

            AddMissingComponents(root);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectPaths.PlayerPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Adds any missing later-milestone components to the saved prefab.</summary>
        static void UpgradeExisting()
        {
            var root = PrefabUtility.LoadPrefabContents(ProjectPaths.PlayerPrefab);
            try
            {
                if (AddMissingComponents(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, ProjectPaths.PlayerPrefab);
                    Debug.Log("[Horror Game] Added new components to " + ProjectPaths.PlayerPrefab + ".");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Phase 2 components. Returns true if anything was added.</summary>
        static bool AddMissingComponents(GameObject root)
        {
            bool changed = false;
            if (root.GetComponent<PlayerInteractor>() == null)
            {
                var interactor = root.AddComponent<PlayerInteractor>();
                var playerCamera = root.GetComponentInChildren<Camera>(true);
                if (playerCamera != null)
                    SerializedFields.Set(interactor, "rayOrigin", playerCamera.transform);
                changed = true;
            }
            if (root.GetComponent<InteractionPromptUI>() == null)
            {
                root.AddComponent<InteractionPromptUI>();
                changed = true;
            }
            if (root.GetComponent<PlayerInventory>() == null)
            {
                root.AddComponent<PlayerInventory>();
                changed = true;
            }
            if (root.GetComponent<InventoryHUD>() == null)
            {
                root.AddComponent<InventoryHUD>();
                changed = true;
            }
            return changed;
        }
    }
}
