namespace HorrorGame.Interaction
{
    /// <summary>
    /// Anything the player can look at and use with the interact key: switches, doors, pickups.
    /// Implement it on a MonoBehaviour that sits on (or above) a collider. The
    /// <see cref="PlayerInteractor"/> finds it by raycast and calls these methods.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Text shown under the crosshair, e.g. "Pick up wrench". The key hint is added by the UI.
        /// Return null or empty to show nothing at all.
        /// </summary>
        string GetPrompt(PlayerInteractor interactor);

        /// <summary>
        /// Whether pressing the key would do something right now. When false the prompt is still
        /// shown, dimmed and without the key hint, so it can explain why (e.g. "Hands full").
        /// </summary>
        bool CanInteract(PlayerInteractor interactor);

        /// <summary>Performs the interaction. Only called when <see cref="CanInteract"/> is true.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
