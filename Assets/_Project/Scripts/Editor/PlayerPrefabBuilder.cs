using HorrorGame.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.EditorTools
{
    /// <summary>
    /// Creates Assets/_Project/Prefabs/Player.prefab if it doesn't exist yet.
    ///
    /// Player                (CharacterController, PlayerInputReader, PlayerMotor, PlayerLook)
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
                return existing;

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

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectPaths.PlayerPrefab);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
