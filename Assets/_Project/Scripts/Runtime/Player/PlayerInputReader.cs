using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HorrorGame.Player
{
    /// <summary>
    /// Reads keyboard and mouse once per frame and exposes the result as plain values.
    /// Every other player component reads from here instead of from devices, so input can
    /// later come from a network message or a replay without touching movement code.
    /// Works with either the Input System package or the legacy Input Manager, whichever
    /// is enabled in Project Settings > Player > Active Input Handling.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        // Legacy "Mouse X/Y" axes are pre-scaled by 0.1; multiply back so both paths report pixels.
        const float LegacyMouseToPixels = 10f;

        public Vector2 Move { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FlashlightPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool PrimaryClickPressed { get; private set; }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            ReadInputSystem();
#else
            ReadLegacyInput();
#endif
        }

        void OnDisable()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            SprintHeld = JumpPressed = FlashlightPressed = PausePressed = PrimaryClickPressed = false;
        }

#if ENABLE_INPUT_SYSTEM
        void ReadInputSystem()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            Vector2 move = Vector2.zero;
            bool sprint = false, jump = false, flashlight = false, pause = false;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                sprint = keyboard.leftShiftKey.isPressed;
                jump = keyboard.spaceKey.wasPressedThisFrame;
                flashlight = keyboard.fKey.wasPressedThisFrame;
                pause = keyboard.escapeKey.wasPressedThisFrame;
            }

            Move = Vector2.ClampMagnitude(move, 1f);
            SprintHeld = sprint;
            JumpPressed = jump;
            FlashlightPressed = flashlight;
            PausePressed = pause;
            LookDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            PrimaryClickPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
#else
        void ReadLegacyInput()
        {
            Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Move = Vector2.ClampMagnitude(move, 1f);
            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            JumpPressed = Input.GetKeyDown(KeyCode.Space);
            FlashlightPressed = Input.GetKeyDown(KeyCode.F);
            PausePressed = Input.GetKeyDown(KeyCode.Escape);
            LookDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * LegacyMouseToPixels;
            PrimaryClickPressed = Input.GetMouseButtonDown(0);
        }
#endif
    }
}
