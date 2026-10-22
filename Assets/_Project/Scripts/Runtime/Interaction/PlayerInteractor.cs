using System;
using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.Interaction
{
    /// <summary>
    /// Casts a ray from the camera each frame to find the <see cref="IInteractable"/> the player is
    /// looking at, and uses it when the interact key is pressed. Exposes the current prompt for the
    /// HUD. Other systems (inventory, monster noise) can listen to <see cref="Interacted"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Tooltip("Where the ray starts and which way it points. Defaults to the player camera.")]
        [SerializeField] Transform rayOrigin;
        [Tooltip("How far the player can reach, in metres.")]
        [SerializeField] float reach = 2.5f;
        [Tooltip("Layers the ray can hit. Walls must be included so they block interaction.")]
        [SerializeField] LayerMask layers = ~0;

        PlayerInputReader input;

        /// <summary>The interactable under the crosshair, or null.</summary>
        public IInteractable Focus { get; private set; }
        /// <summary>Prompt text for the focused interactable, or null when there is nothing to show.</summary>
        public string Prompt { get; private set; }
        /// <summary>Whether the focused interactable can be used right now.</summary>
        public bool CanInteractWithFocus { get; private set; }

        /// <summary>Raised after the player successfully uses an interactable.</summary>
        public event Action<IInteractable> Interacted;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (rayOrigin == null)
            {
                var playerCamera = GetComponentInChildren<Camera>();
                rayOrigin = playerCamera != null ? playerCamera.transform : transform;
            }
        }

        void Update()
        {
            // While the cursor is free (paused / alt-tabbed) the player isn't aiming at anything.
            Focus = PlayerLook.CursorLocked ? FindFocus() : null;

            if (Focus != null && input.InteractPressed && Focus.CanInteract(this))
            {
                Focus.Interact(this);
                Interacted?.Invoke(Focus);
            }

            // Read the prompt after interacting so the HUD shows the new state straight away
            // (e.g. "Turn on" becomes "Turn off"), and drop targets the interaction destroyed or
            // hid (e.g. an item that was just picked up).
            if (Focus is Behaviour focusBehaviour && (focusBehaviour == null || !focusBehaviour.isActiveAndEnabled))
                Focus = null;
            Prompt = Focus?.GetPrompt(this);
            CanInteractWithFocus = Focus != null && Focus.CanInteract(this);
        }

        void OnDisable()
        {
            Focus = null;
            Prompt = null;
            CanInteractWithFocus = false;
        }

        IInteractable FindFocus()
        {
            var ray = new Ray(rayOrigin.position, rayOrigin.forward);
            // The ray starts inside the player's own capsule, which raycasts ignore, so the player
            // never targets themselves. Triggers are ignored so trigger volumes don't block the view.
            if (!Physics.Raycast(ray, out RaycastHit hit, reach, layers, QueryTriggerInteraction.Ignore))
                return null;
            // Search up the hierarchy so a whole prop can share one interactable on its root.
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        void OnDrawGizmosSelected()
        {
            Transform origin = rayOrigin != null ? rayOrigin : transform;
            Gizmos.color = Focus != null ? Color.green : Color.yellow;
            Gizmos.DrawRay(origin.position, origin.forward * reach);
        }
    }
}
