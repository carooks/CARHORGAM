using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.World
{
    public enum TrapSound
    {
        /// <summary>Long wooden groan: boards taking weight they shouldn't.</summary>
        Creak,
        /// <summary>Sharp splintering snap.</summary>
        Crack,
        /// <summary>Heavy body hitting a floor.</summary>
        Thud,
        /// <summary>Grit and bits of concrete trickling over an edge into a drop.</summary>
        Trickle,
    }

    /// <summary>
    /// Placeholder trap sounds built in code (no audio files), in the same spirit as ProceduralAudio.
    /// Replace with recorded clips later.
    /// </summary>
    public static class TrapSounds
    {
        const int Rate = 44100;
        static readonly Dictionary<TrapSound, AudioClip> Cache = new Dictionary<TrapSound, AudioClip>();

        public static void Play(TrapSound sound, Vector3 position, float volume = 1f)
        {
            AudioSource.PlayClipAtPoint(Get(sound), position, volume);
        }

        public static AudioClip Get(TrapSound sound)
        {
            if (Cache.TryGetValue(sound, out var clip) && clip != null)
                return clip;
            var random = new System.Random((int)sound * 7919 + 13);
            float[] data = sound switch
            {
                TrapSound.Creak => Creak(random),
                TrapSound.Crack => Crack(random),
                TrapSound.Thud => Thud(random),
                _ => Trickle(random),
            };
            clip = AudioClip.Create($"Trap_{sound}", data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[sound] = clip;
            return clip;
        }

        static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

        // Stick-slip: a low pitch that wanders, chopped into rapid pulses, swelling then dying.
        static float[] Creak(System.Random r)
        {
            int n = (int)(Rate * 1.3f);
            var d = new float[n];
            double phase = 0, pulsePhase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float pitch = Mathf.Lerp(150f, 85f, t) + Mathf.Sin(t * 19f) * 12f;
                float pulseRate = Mathf.Lerp(38f, 22f, t);
                phase += pitch / Rate;
                pulsePhase += pulseRate / Rate;
                float pulse = Mathf.Pow(1f - (float)(pulsePhase % 1.0), 6f);
                float tone = Mathf.Sin((float)(phase * Mathf.PI * 2)) + 0.5f * Mathf.Sin((float)(phase * Mathf.PI * 6.1));
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.15f));
                d[i] = (tone * 0.7f + Noise(r) * 0.25f) * pulse * env * 0.6f;
            }
            return d;
        }

        static float[] Crack(System.Random r)
        {
            int n = (int)(Rate * 0.6f);
            var d = new float[n];
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float burst = Mathf.Exp(-t * 18f) + 0.6f * Mathf.Exp(-Mathf.Abs(t - 0.09f) * 60f) + 0.4f * Mathf.Exp(-Mathf.Abs(t - 0.2f) * 50f);
                low += (Noise(r) - low) * 0.08f;
                d[i] = Mathf.Clamp((Noise(r) * 0.6f + low * 2f) * burst, -1f, 1f) * 0.9f;
            }
            return d;
        }

        static float[] Thud(System.Random r)
        {
            int n = (int)(Rate * 0.7f);
            var d = new float[n];
            float low = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float pitch = Mathf.Lerp(70f, 40f, Mathf.Clamp01(t * 3f));
                low += (Noise(r) - low) * 0.03f;
                d[i] = (Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-t * 7f) + low * 3f * Mathf.Exp(-t * 12f)) * 0.9f;
            }
            return d;
        }

        static float[] Trickle(System.Random r)
        {
            int n = (int)(Rate * 1.6f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float density = 0.004f * (1f - t);
                if (r.NextDouble() < density)
                {
                    // One grain: a tiny decaying click.
                    float amp = (float)r.NextDouble() * 0.5f + 0.2f;
                    int len = 200 + r.Next(400);
                    for (int k = 0; k < len && i + k < n; k++)
                        d[i + k] += Noise(r) * amp * Mathf.Exp(-k / 60f);
                }
            }
            return d;
        }
    }
}
