using UnityEngine;

namespace HorrorGame.Player
{
    /// <summary>
    /// Mouse look: yaw turns the whole player body, pitch tilts the camera root.
    /// Also owns cursor locking: Escape frees the cursor, clicking the game view locks it again.
    /// </summary>
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] Transform cameraRoot;
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] float sensitivity = 0.1f;
        [SerializeField] float pitchLimit = 85f;
        [SerializeField] bool invertY;

        PlayerInputReader input;
        float pitch;

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (cameraRoot == null)
                cameraRoot = transform.Find("CameraRoot");
        }

        void Start()
        {
            SetCursorLocked(true);
        }

        void Update()
        {
            if (input == null || cameraRoot == null)
                return;

            if (input.PausePressed)
                SetCursorLocked(false);
            else if (!CursorLocked && input.PrimaryClickPressed)
                SetCursorLocked(true);

            if (!CursorLocked)
                return;

            Vector2 delta = input.LookDelta * sensitivity;
            transform.Rotate(0f, delta.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y), -pitchLimit, pitchLimit);
            cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void OnDisable()
        {
            SetCursorLocked(false);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
