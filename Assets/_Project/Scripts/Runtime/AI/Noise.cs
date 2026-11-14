using System;
using UnityEngine;

namespace HorrorGame.AI
{
    /// <summary>A sound something made that monsters can hear.</summary>
    public readonly struct NoiseEvent
    {
        /// <summary>Where the sound came from.</summary>
        public readonly Vector3 Position;
        /// <summary>How far the sound carries in open air, in metres. Walls halve it.</summary>
        public readonly float Radius;
        /// <summary>What made the sound (a player, a breaker box...). May be null.</summary>
        public readonly GameObject Source;

        public NoiseEvent(Vector3 position, float radius, GameObject source)
        {
            Position = position;
            Radius = radius;
            Source = source;
        }
    }

    /// <summary>
    /// The game's "hearing" channel. Anything that makes a sound a monster should react to calls
    /// <see cref="Emit"/>; every listening monster decides for itself whether it was close enough to hear.
    /// This is separate from the audio the player hears, so loudness for gameplay can be tuned
    /// without touching the mix.
    /// </summary>
    public static class Noise
    {
        /// <summary>Raised for every gameplay noise.</summary>
        public static event Action<NoiseEvent> Emitted;

        public static void Emit(Vector3 position, float radius, GameObject source)
        {
            if (radius <= 0f)
                return;
            Emitted?.Invoke(new NoiseEvent(position, radius, source));
        }

        // Clears listeners left over from a previous play session when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Emitted = null;
    }
}
