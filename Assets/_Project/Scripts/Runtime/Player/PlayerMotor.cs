using UnityEngine;

namespace HorrorGame.Player
{
    /// <summary>
    /// Moves the player with a CharacterController: walking, sprinting, jumping and gravity.
    /// Reads intent from <see cref="PlayerInputReader"/> and never touches devices directly.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Speed (metres per second)")]
        [SerializeField] float walkSpeed = 3.5f;
        [SerializeField] float sprintSpeed = 6f;
        [Tooltip("How quickly the player reaches target speed on the ground, in m/s².")]
        [SerializeField] float groundAcceleration = 40f;
        [Tooltip("Fraction of ground acceleration available while airborne.")]
        [Range(0f, 1f)] [SerializeField] float airControl = 0.3f;

        [Header("Jump and gravity")]
        [SerializeField] float jumpHeight = 1.0f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Small downward speed while grounded so the controller stays snapped to slopes and steps.")]
        [SerializeField] float groundedStickSpeed = -2f;

        CharacterController controller;
        PlayerInputReader input;
        Vector3 horizontalVelocity;
        float verticalVelocity;

        public bool IsGrounded => controller.isGrounded;
        public bool IsSprinting { get; private set; }
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool grounded = controller.isGrounded;
            Vector2 move = input != null ? input.Move : Vector2.zero;

            IsSprinting = input != null && input.SprintHeld && move.y > 0.1f && grounded;
            float targetSpeed = IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = (transform.right * move.x + transform.forward * move.y) * targetSpeed;
            float acceleration = grounded ? groundAcceleration : groundAcceleration * airControl;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * dt);

            if (grounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = groundedStickSpeed;
                if (input != null && input.JumpPressed)
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            verticalVelocity += gravity * dt;

            CollisionFlags flags = controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        /// <summary>Moves the player instantly, e.g. for spawning or respawning.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
