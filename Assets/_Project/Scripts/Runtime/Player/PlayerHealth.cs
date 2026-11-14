using System;
using UnityEngine;

namespace HorrorGame.Player
{
    /// <summary>
    /// The player's health. Monsters call <see cref="TakeHit"/>; at zero the player dies and
    /// <see cref="Died"/> fires (the caught screen listens and restarts the level).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        /// <summary>What dealt the killing blow, if anything.</summary>
        public GameObject Killer { get; private set; }

        /// <summary>Raised with (damage, attacker) on every hit.</summary>
        public event Action<float, GameObject> Damaged;
        /// <summary>Raised once, with the attacker, when health reaches zero.</summary>
        public event Action<GameObject> Died;

        void Awake() => Current = maxHealth;

        public void TakeHit(float damage, GameObject attacker)
        {
            if (!IsAlive || damage <= 0f)
                return;
            Current = Mathf.Max(0f, Current - damage);
            Damaged?.Invoke(damage, attacker);
            if (IsAlive)
                return;
            Killer = attacker;
            Died?.Invoke(attacker);
        }
    }
}
