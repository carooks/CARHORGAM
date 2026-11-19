using System;
using System.Collections.Generic;
using HorrorGame.Audio;
using UnityEngine;

namespace HorrorGame.Glass
{
    /// <summary>
    /// Runs the glass house: a grid of rooms whose walls are sliding glass panes, reshuffled on a timer
    /// or when someone pulls a lever, so players get split up and can still see each other.
    ///
    /// Panes come in pairs (array index 2k and 2k+1) that share one doorway-sized wall section. Each
    /// section is in one of three states: <see cref="Closed"/> (both panes in place), or one pane slid
    /// over its partner, leaving a gap where it stood. Only one pane of a pair is ever open, so a shift
    /// can never leave two panes stacked over the same gap.
    ///
    /// Co-op: the whole layout is one small int array (<see cref="Layout"/>). Later, the host picks it
    /// with <see cref="Reshuffle"/> and sends it; clients call <see cref="ApplyLayout"/> with it.
    /// </summary>
    public class GlassHouse : MonoBehaviour
    {
        public const int Closed = 0;
        public const int FirstOpen = 1;
        public const int SecondOpen = 2;

        [Tooltip("Panes in pairs: 0 and 1 share a wall section, 2 and 3, and so on.")]
        [SerializeField] GlassPane[] panes = Array.Empty<GlassPane>();
        [Range(0f, 1f)] [SerializeField] float closedChance = 0.45f;
        [Tooltip("Seconds between automatic shifts. 0 turns the timer off (lever only).")]
        [SerializeField] float shiftInterval = 45f;
        [Tooltip("Warning (red lights, buzzer) before a timed shift.")]
        [SerializeField] float warningTime = 4f;
        [Tooltip("Extra delay before the first timed shift, so several houses (floors) don't all shift together.")]
        [SerializeField] float firstShiftOffset;
        [Tooltip("Same seed, same sequence of layouts. 0 picks a new seed every run.")]
        [SerializeField] int seed;

        [Header("Warning")]
        [SerializeField] Light[] warningLights = Array.Empty<Light>();
        [SerializeField] float warningLightIntensity = 3f;
        [Range(0f, 1f)] [SerializeField] float buzzerVolume = 0.5f;
        [Tooltip("Heard everywhere. Turn off when there are several houses (one per floor): the buzzer then sounds from this object.")]
        [SerializeField] bool globalBuzzer = true;

        [Header("Debug")]
        [Tooltip("Shows a small countdown at the top of the screen.")]
        [SerializeField] bool showCountdown = true;
        [Tooltip("Name shown in the countdown, e.g. \"Basement\". Empty shows just \"glass\".")]
        [SerializeField] string displayName = "";
        [Tooltip("Countdown line, from the top. Give each house its own row.")]
        [SerializeField] int countdownRow;

        System.Random rng;
        int[] layout = Array.Empty<int>();
        float nextShiftAt;
        float pendingShiftAt = -1f;
        float warningStartedAt;
        AudioSource buzzer;
        GUIStyle countdownStyle;

        public IReadOnlyList<GlassPane> Panes => panes;
        public int SectionCount => panes.Length / 2;
        /// <summary>Current state of each wall section (<see cref="Closed"/>, <see cref="FirstOpen"/>, <see cref="SecondOpen"/>).</summary>
        public IReadOnlyList<int> Layout => layout;
        /// <summary>True between the warning starting and the panes being told to move.</summary>
        public bool ShiftPending => pendingShiftAt >= 0f;
        public float SecondsUntilShift => ShiftPending ? pendingShiftAt - Time.time : nextShiftAt - Time.time;
        /// <summary>True while any pane is still sliding.</summary>
        public bool IsShifting
        {
            get
            {
                foreach (var pane in panes)
                    if (pane != null && pane.IsMoving)
                        return true;
                return false;
            }
        }

        /// <summary>Raised whenever a new layout is applied.</summary>
        public event Action<IReadOnlyList<int>> LayoutChanged;

        void Awake()
        {
            rng = new System.Random(seed != 0 ? seed : Environment.TickCount);
            layout = new int[SectionCount];

            buzzer = gameObject.AddComponent<AudioSource>();
            buzzer.clip = ProceduralAudio.GetClip(ProceduralSound.ElectricalBuzz);
            buzzer.loop = true;
            buzzer.playOnAwake = false;
            buzzer.spatialBlend = globalBuzzer ? 0f : 1f;
            buzzer.minDistance = 4f;
            buzzer.maxDistance = 30f;
            buzzer.rolloffMode = AudioRolloffMode.Linear;
            buzzer.pitch = 0.5f;
            buzzer.volume = buzzerVolume;
            SetWarningLights(0f);
        }

        void Start()
        {
            ApplyLayout(PickLayout(), instant: true);
            nextShiftAt = Time.time + Mathf.Max(shiftInterval, 0f) + Mathf.Max(firstShiftOffset, 0f);
        }

        void Update()
        {
            if (ShiftPending)
            {
                float t = Time.time - warningStartedAt;
                SetWarningLights((0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 4f)) * warningLightIntensity);
                if (Time.time >= pendingShiftAt)
                {
                    pendingShiftAt = -1f;
                    SetWarningLights(0f);
                    buzzer.Stop();
                    Reshuffle();
                }
                return;
            }

            if (shiftInterval > 0f && Time.time >= nextShiftAt - warningTime)
                RequestShift(Mathf.Max(0f, nextShiftAt - Time.time));
        }

        /// <summary>Sets the panes for a house built in code (tests). Call before the object is activated.</summary>
        public void SetPanes(GlassPane[] pairedPanes)
        {
            panes = pairedPanes ?? Array.Empty<GlassPane>();
            layout = new int[SectionCount];
        }

        /// <summary>Starts the warning, then shifts after <paramref name="warningSeconds"/>. Ignored if a shift is already coming.</summary>
        public void RequestShift(float warningSeconds)
        {
            if (ShiftPending)
                return;
            warningStartedAt = Time.time;
            pendingShiftAt = Time.time + warningSeconds;
            if (warningSeconds > 0f)
                buzzer.Play();
        }

        /// <summary>Picks a new random layout, different enough from the current one, and starts sliding to it.</summary>
        public void Reshuffle()
        {
            ApplyLayout(PickLayout(), instant: false);
            nextShiftAt = Time.time + Mathf.Max(shiftInterval, 0f);
        }

        /// <summary>Slides every pane to match <paramref name="sections"/> (one entry per pair of panes).</summary>
        public void ApplyLayout(IReadOnlyList<int> sections, bool instant)
        {
            if (sections.Count != SectionCount)
                throw new ArgumentException($"Layout has {sections.Count} sections, the house has {SectionCount}.");
            for (int i = 0; i < SectionCount; i++)
            {
                int state = Mathf.Clamp(sections[i], Closed, SecondOpen);
                layout[i] = state;
                var first = panes[i * 2];
                var second = panes[i * 2 + 1];
                if (first != null)
                    first.SetClosed(state != FirstOpen, instant);
                if (second != null)
                    second.SetClosed(state != SecondOpen, instant);
            }
            LayoutChanged?.Invoke(layout);
        }

        /// <summary>
        /// A random layout where at least a third of the sections change, so every shift is noticeable.
        /// Each section is closed with <see cref="closedChance"/>, otherwise one of its panes opens.
        /// </summary>
        public int[] PickLayout()
        {
            var next = new int[SectionCount];
            int wantChanged = Mathf.Max(1, SectionCount / 3);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int changed = 0;
                for (int i = 0; i < next.Length; i++)
                {
                    next[i] = rng.NextDouble() < closedChance ? Closed : (rng.NextDouble() < 0.5 ? FirstOpen : SecondOpen);
                    if (i < layout.Length && next[i] != layout[i])
                        changed++;
                }
                if (changed >= wantChanged || layout.Length == 0)
                    break;
            }
            return next;
        }

        void SetWarningLights(float intensity)
        {
            foreach (var light in warningLights)
            {
                if (light == null)
                    continue;
                light.intensity = intensity;
                light.enabled = intensity > 0.01f;
            }
        }

        void OnGUI()
        {
            if (!showCountdown || shiftInterval <= 0f && !ShiftPending)
                return;
            if (countdownStyle == null)
            {
                countdownStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = Mathf.Max(14, Screen.height / 45),
                };
            }
            string what = string.IsNullOrEmpty(displayName) ? "glass" : displayName + " glass";
            string text = ShiftPending ? what.ToUpperInvariant() + " IS MOVING" :
                IsShifting ? what + "..." : $"{what} shifts in {Mathf.CeilToInt(Mathf.Max(0f, SecondsUntilShift))}";
            countdownStyle.normal.textColor = ShiftPending ? new Color(1f, 0.3f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
            float line = countdownStyle.fontSize * 1.5f;
            GUI.Label(new Rect(0f, 8f + countdownRow * line, Screen.width, line), text, countdownStyle);
        }
    }
}
