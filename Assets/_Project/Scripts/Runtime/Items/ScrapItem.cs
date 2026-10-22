using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Items
{
    /// <summary>
    /// A piece of scrap lying in the world that the player can pick up for its value.
    /// Name, value range and weight are set per object in the Inspector; the actual value is rolled
    /// once when the level starts, so the same item can be worth a different amount each run.
    /// </summary>
    public class ScrapItem : MonoBehaviour, IInteractable
    {
        [SerializeField] string displayName = "Scrap";
        [Tooltip("Value in dollars is rolled between these two numbers (inclusive) when the level starts.")]
        [SerializeField] Vector2Int valueRange = new Vector2Int(10, 30);
        [Tooltip("Weight in pounds. Used by carrying capacity in a later milestone.")]
        [SerializeField] float weight = 5f;

        public string DisplayName => displayName;
        public int Value { get; private set; }
        public float Weight => weight;
        /// <summary>The inventory carrying this item, or null while it lies in the world.</summary>
        public PlayerInventory Holder { get; private set; }

        void Awake()
        {
            // Single-player for now: each client rolls its own value. With networking the host
            // will roll it and send it to everyone.
            Value = Random.Range(Mathf.Min(valueRange.x, valueRange.y), Mathf.Max(valueRange.x, valueRange.y) + 1);
        }

        public string GetPrompt(PlayerInteractor interactor) => $"Pick up {displayName} (${Value})";

        public bool CanInteract(PlayerInteractor interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            return Holder == null && inventory != null && inventory.CanAdd(this);
        }

        public void Interact(PlayerInteractor interactor)
        {
            interactor.GetComponent<PlayerInventory>().Add(this);
        }

        /// <summary>Called by <see cref="PlayerInventory"/>. Hides the item and attaches it to the carrier.</summary>
        internal void OnPickedUp(PlayerInventory inventory, Transform holdPoint)
        {
            Holder = inventory;
            transform.SetParent(holdPoint, false);
            transform.localPosition = Vector3.zero;
            gameObject.SetActive(false);
        }
    }
}
