using System;
using System.Collections.Generic;
using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Items
{
    /// <summary>
    /// The scrap the player is carrying. For now it is an unlimited list with running totals;
    /// carrying capacity, switching between items and dropping them come in the next milestone.
    /// Picked-up items stay in the scene as hidden children of this object, so they can be
    /// dropped back into the world later.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] float pickupVolume = 0.6f;

        readonly List<ScrapItem> items = new List<ScrapItem>();
        Transform holdPoint;
        AudioSource pickupSource;

        public IReadOnlyList<ScrapItem> Items => items;
        public int TotalValue { get; private set; }
        public float TotalWeight { get; private set; }

        /// <summary>Raised whenever an item is added or removed.</summary>
        public event Action Changed;

        void Awake()
        {
            holdPoint = new GameObject("CarriedItems").transform;
            holdPoint.SetParent(transform, false);

            pickupSource = gameObject.AddComponent<AudioSource>();
            pickupSource.playOnAwake = false;
            pickupSource.spatialBlend = 0f;
            pickupSource.pitch = 0.7f;
            pickupSource.clip = ProceduralAudio.GetClip(ProceduralSound.Click);
        }

        /// <summary>Whether the item can be picked up. Capacity limits will be checked here.</summary>
        public bool CanAdd(ScrapItem item) => item != null && !items.Contains(item);

        public void Add(ScrapItem item)
        {
            if (!CanAdd(item))
                return;
            items.Add(item);
            TotalValue += item.Value;
            TotalWeight += item.Weight;
            item.OnPickedUp(this, holdPoint);
            pickupSource.PlayOneShot(pickupSource.clip, pickupVolume);
            Changed?.Invoke();
        }
    }
}
