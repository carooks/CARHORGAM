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
        /// <summary>Player footstep on concrete: a short scuff with a soft low thump.</summary>
        Footstep,
        /// <summary>A heavy, slow footfall for a large monster.</summary>
        HeavyStep,
        /// <summary>Slow, wet, rasping breathing loop for a monster (heard close up).</summary>
        MonsterBreath,
        /// <summary>Harsh rising shriek, played when a monster starts chasing.</summary>
        Screech,
        /// <summary>One voice of a choir holding a long "ooh" (220 Hz, slow vibrato). Pitch it for other notes.</summary>
        ChoirVoice,
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
                case ProceduralSound.Footstep: clip = CreateFootstep(); break;
                case ProceduralSound.HeavyStep: clip = CreateHeavyStep(); break;
                case ProceduralSound.MonsterBreath: clip = CreateBreath(); break;
                case ProceduralSound.Screech: clip = CreateScreech(); break;
                case ProceduralSound.ChoirVoice: clip = CreateChoirVoice(); break;
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

        static AudioClip CreateChoirVoice()
        {
            const int seconds = 4;
            const float f0 = 220f;
            var rng = new System.Random(5150);
            float breath = 0f, phase = 0f;
            float[] raw = new float[seconds * SampleRate + SampleRate / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                // Vibrato that drifts in and out, integrated so the pitch bends smoothly.
                float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 5.3f * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t));
                phase += f0 * vibrato / SampleRate;
                float sample = 0f;
                // Harmonics shaped by "oo" vowel formants (about 300 Hz and 870 Hz).
                for (int k = 1; k <= 12; k++)
                {
                    float f = f0 * k;
                    float formant = Mathf.Exp(-Mathf.Pow((f - 300f) / 160f, 2f)) + 0.35f * Mathf.Exp(-Mathf.Pow((f - 870f) / 220f, 2f));
                    sample += Mathf.Sin(2f * Mathf.PI * phase * k) * (formant + 0.02f) / k;
                }
                breath += ((float)(rng.NextDouble() * 2.0 - 1.0) - breath) * 0.05f;
                float swell = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                raw[i] = (sample * 0.5f + breath * 0.08f) * swell;
            }
            return Finish("ChoirVoice", raw, seconds * SampleRate, 0.5f, true);
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

        static AudioClip CreateFootstep()
        {
            int length = SampleRate * 18 / 100;
            var rng = new System.Random(11);
            float lowpass = 0f;
            float[] raw = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpass += (white - lowpass) * 0.25f;
                float scuff = lowpass * Mathf.Exp(-t * 35f);
                float thump = Sine(85f * (1f - t * 1.5f), t) * Mathf.Exp(-t * 30f);
                raw[i] = scuff * 0.7f + thump * 0.6f;
            }
            return Finish("Footstep", raw, length, 0.8f, false);
        }

        static AudioClip CreateHeavyStep()
        {
            int length = SampleRate * 45 / 100;
            var rng = new System.Random(21);
            float lowpass = 0f;
            float[] raw = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lowpass += (white - lowpass) * 0.06f;
                float thud = Sine(48f * (1f - t * 0.6f), t) * Mathf.Exp(-t * 9f);
                float drag = lowpass * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 20f);
                raw[i] = thud + drag * 2.5f;
            }
            return Finish("HeavyStep", raw, length, 0.9f, false);
        }

        static AudioClip CreateBreath()
        {
            // Two 3-second breaths (inhale, pause, exhale) so the 6-second loop has whole cycles.
            const int seconds = 6;
            const float period = 3f;
            var rng = new System.Random(31);
            float band = 0f, low = 0f;
            float[] raw = new float[seconds * SampleRate + SampleRate / 4];
            for (int i = 0; i < raw.Length; i++)
            {
                float t = (float)i / SampleRate;
                float phase = (t % period) / period;
                float inhale = phase < 0.35f ? Mathf.Sin(phase / 0.35f * Mathf.PI) * 0.6f : 0f;
                float exhale = phase > 0.45f ? Mathf.Sin((phase - 0.45f) / 0.55f * Mathf.PI) : 0f;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.12f;
                band += (low - band) * 0.3f;
                float breath = (low - band) * 6f;
                float rasp = Sine(70f, t) * (0.5f + 0.5f * Sine(23f, t)) * 0.25f;
                raw[i] = (breath + rasp) * (inhale + exhale);
            }
            return Finish("MonsterBreath", raw, seconds * SampleRate, 0.6f, true);
        }

        static AudioClip CreateScreech()
        {
            int length = SampleRate * 13 / 10;
            var rng = new System.Random(41);
            float[] raw = new float[length];
            float phaseA = 0f, phaseB = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float pitch = Mathf.Lerp(380f, 760f, Mathf.Sqrt(t / 1.3f));
                phaseA += pitch / SampleRate;
                phaseB += pitch * 1.47f / SampleRate;
                float sawA = (phaseA - Mathf.Floor(phaseA)) * 2f - 1f;
                float sawB = (phaseB - Mathf.Floor(phaseB)) * 2f - 1f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float envelope = Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1.3f - t) * 3f);
                float tremolo = 0.75f + 0.25f * Sine(13f, t);
                raw[i] = (sawA * 0.5f + sawB * 0.3f + noise * 0.35f) * envelope * tremolo;
            }
            return Finish("Screech", raw, length, 0.7f, false);
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
