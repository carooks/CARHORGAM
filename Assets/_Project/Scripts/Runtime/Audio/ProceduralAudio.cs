using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Audio
{
    public enum ProceduralSound
    {
        /// <summary>Low rumbling room tone with a slow swell. Used as the global ambience bed.</summary>
        Drone,
        /// <summary>60 Hz machinery hum with harmonics. For tanks, generators, vending machines.</summary>
        MachineHum,
        /// <summary>Thin 120 Hz buzz of a failing light fixture.</summary>
        ElectricalBuzz,
        /// <summary>Short mechanical click, e.g. a flashlight switch.</summary>
        Click,
    }

    /// <summary>
    /// Builds placeholder sound clips in code so the prototype needs no audio files.
    /// Looping clips use whole numbers of cycles plus a crossfade, so they loop without a pop.
    /// Replace with recorded audio later by assigning real clips instead of calling this.
    /// </summary>
    public static class ProceduralAudio
    {
        const int SampleRate = 44100;
        static readonly Dictionary<ProceduralSound, AudioClip> Cache = new Dictionary<ProceduralSound, AudioClip>();

        public static AudioClip GetClip(ProceduralSound sound)
        {
            if (Cache.TryGetValue(sound, out var cached) && cached != null)
                return cached;

            AudioClip clip;
            switch (sound)
            {
                case ProceduralSound.Drone: clip = CreateDrone(); break;
                case ProceduralSound.MachineHum: clip = CreateMachineHum(); break;
                case ProceduralSound.ElectricalBuzz: clip = CreateBuzz(); break;
                default: clip = CreateClick(); break;
            }
            Cache[sound] = clip;
            return clip;
        }

        static AudioClip CreateDrone()
        {
            const int seconds = 8;
            var rng = new System.Random(1234);
            float brown = 0f, lowpass = 0f;
            float[] raw = new float[seconds * SampleRate + SampleRate / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = (brown + 0.02f * white) / 1.02f;
                lowpass += (brown - lowpass) * 0.08f;
                float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                float tone = Sine(55f, t) * 0.18f + Sine(82f, t) * 0.06f + Sine(110f, t) * 0.04f;
                raw[i] = lowpass * 4f + tone * swell;
            }
            return Finish("Drone", raw, seconds * SampleRate, 0.6f, true);
        }

        static AudioClip CreateMachineHum()
        {
            const int seconds = 4;
            var rng = new System.Random(99);
            float noise = 0f;
            float[] raw = new float[seconds * SampleRate + SampleRate / 4];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                noise += ((float)(rng.NextDouble() * 2.0 - 1.0) - noise) * 0.1f;
                float wobble = 1f + 0.08f * Sine(0.5f, t);
                raw[i] = (Sine(60f, t) * 0.5f + Sine(120f, t) * 0.3f + Sine(180f, t) * 0.12f + Sine(240f, t) * 0.05f) * wobble
                         + noise * 0.15f;
            }
            return Finish("MachineHum", raw, seconds * SampleRate, 0.7f, true);
        }

        static AudioClip CreateBuzz()
        {
            const int seconds = 2;
            var rng = new System.Random(7);
            float[] raw = new float[seconds * SampleRate + SampleRate / 4];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phase = t * 120f - Mathf.Floor(t * 120f);
                float saw = phase * 2f - 1f;
                float crackle = rng.NextDouble() < 0.0005 ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
                raw[i] = saw * 0.25f + Sine(240f, t) * 0.1f + crackle;
            }
            return Finish("ElectricalBuzz", raw, seconds * SampleRate, 0.5f, true);
        }

        static AudioClip CreateClick()
        {
            int length = SampleRate / 20;
            var rng = new System.Random(3);
            float[] raw = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Exp(-t * 180f);
                raw[i] = ((float)(rng.NextDouble() * 2.0 - 1.0) * 0.6f + Sine(2200f, t) * 0.4f) * envelope;
            }
            return Finish("Click", raw, length, 0.8f, false);
        }

        static float Sine(float frequency, float time) => Mathf.Sin(2f * Mathf.PI * frequency * time);

        /// <summary>
        /// Crossfades the tail (samples past <paramref name="length"/>) into the head when looping,
        /// normalises to <paramref name="peak"/> and packs the samples into an AudioClip.
        /// </summary>
        static AudioClip Finish(string name, float[] raw, int length, float peak, bool loop)
        {
            float[] samples = new float[length];
            int fade = loop ? raw.Length - length : 0;
            for (int i = 0; i < length; i++)
            {
                if (i < fade)
                {
                    float w = (float)i / fade;
                    samples[i] = raw[i] * w + raw[length + i] * (1f - w);
                }
                else
                {
                    samples[i] = raw[i];
                }
            }

            float max = 0.0001f;
            for (int i = 0; i < length; i++)
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            float gain = peak / max;
            for (int i = 0; i < length; i++)
                samples[i] *= gain;

            var clip = AudioClip.Create("Procedural_" + name, length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
