using System.Collections.Generic;
using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.AI
{
    /// <summary>
    /// Marks something monsters can see and hunt: the player today, each co-op player later.
    /// Monsters loop over <see cref="All"/> instead of looking for "the" player, so a second
    /// player needs no monster changes.
    /// </summary>
    public class DetectableTarget : MonoBehaviour
    {
        static readonly List<DetectableTarget> Registered = new List<DetectableTarget>();

        /// <summary>Every active target in the scene.</summary>
        public static IReadOnlyList<DetectableTarget> All => Registered;

        [Tooltip("Height of the head above this object's pivot (the feet).")]
        [SerializeField] float headHeight = 1.6f;
        [Tooltip("Height of the chest above the pivot. Monsters check sight to both head and chest.")]
        [SerializeField] float chestHeight = 1.1f;
        [Tooltip("Optional. When the flashlight is on, the target counts as lit. Found automatically on the player.")]
        [SerializeField] Flashlight flashlight;

        PlayerHealth health;

        public Vector3 HeadPosition => transform.position + Vector3.up * headHeight;
        public Vector3 ChestPosition => transform.position + Vector3.up * chestHeight;
        public bool IsAlive => health == null || health.IsAlive;
        public PlayerHealth Health => health;

        /// <summary>Forces the lit state (used by tests). Null means work it out from the flashlight and lamps.</summary>
        public bool? LitOverride { get; set; }

        /// <summary>
        /// True when the target is easy to see: their flashlight is on or they stand in a lamp's pool of light.
        /// Monsters see a lit target from much farther away.
        /// </summary>
        public bool IsLit
        {
            get
            {
                if (LitOverride.HasValue)
                    return LitOverride.Value;
                if (flashlight != null && flashlight.isActiveAndEnabled && flashlight.IsOn)
                    return true;
                return LightExposure.IsLitByLamp(ChestPosition, transform);
            }
        }

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            if (flashlight == null)
                flashlight = GetComponentInChildren<Flashlight>(true);
        }

        void OnEnable() => Registered.Add(this);
        void OnDisable() => Registered.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();
    }
}
