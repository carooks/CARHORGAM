#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The choir buried in the painted field. Their song is the map: every head turns toward where
    /// you need to go next, and the chord swells and comes into tune when you face that way and
    /// sours when you face away. Some voices are missing (mouths painted over) until you free them.
    ///
    /// The thing in the painting hates harmony. <see cref="Anger"/> builds all the time, faster the
    /// fuller the chord and faster still while it is in tune (you heading the right way). When it
    /// boils over, the choir falls silent in fear, every head turns to where it stands in the wet
    /// paint, and it walks to the last sound it heard. After a while it sinks away and they sing again.
    ///
    /// Co-op ready: alignment is averaged over every listener, so players heading different ways
    /// make the chord sour: no guidance, but nothing to anger it either.
    ///
    /// Voice filter: a reverb zone gives the field its huge cavernous echo; each singer gets a
    /// low-pass (sung from under the earth) and distortion that rises with anger. <see cref="FilterStrength"/>
    /// scales all of it (F6 cycles off / half / full in play, to judge the raw synth voices).
    /// </summary>
    public class Choir : MonoBehaviour
    {
        public enum ChoirState { Idle, Singing, Silent }

        [SerializeField] ChoirSinger[] singers = Array.Empty<ChoirSinger>();
        [SerializeField] PaintedRealm realm;
        [SerializeField] PaintedEntity entity;

        [Header("Anger (it hates harmony)")]
        [Tooltip("Anger it takes to come out.")]
        [SerializeField] float angerThreshold = 100f;
        [Tooltip("Anger per second with no freed voices and the chord out of tune.")]
        [SerializeField] float baseAngerRate = 1.6f;
        [Tooltip("Extra anger per second with the chord complete.")]
        [SerializeField] float harmonyAngerRate = 4f;
        [Tooltip("Multiplier while the chord is in tune (someone heading the right way).")]
        [SerializeField] float inTuneMultiplier = 1.8f;
        [Tooltip("Anger added at once when a voice is freed.")]
        [SerializeField] float freedVoiceAnger = 35f;
        [Tooltip("How long it stays out before sinking away.")]
        [SerializeField] float silenceTime = 12f;
        [SerializeField] float lookSpeed = 60f;
        [Tooltip("Heads snap to the thing this fast when the song stops.")]
        [SerializeField] float snapSpeed = 900f;

        [Header("Voice filter")]
        [Tooltip("0 = raw synth voices, 1 = full treatment (muffled, echoing, warped).")]
        [Range(0f, 1f)] [SerializeField] float filterStrength = 1f;
        [Tooltip("Low-pass cutoff at full filter strength, in Hz. Lower sounds more buried.")]
        [SerializeField] float buriedCutoff = 1400f;
        [Tooltip("Distortion at full anger and full filter strength (0..1).")]
        [SerializeField] float maxDistortion = 0.55f;
        [Tooltip("Cavernous echo over the whole field. Strength is scaled with the filter.")]
        [SerializeField] AudioReverbZone reverb;

        readonly List<Transform> listeners = new List<Transform>();
        float timer;
        float anger;
        bool crescendo;

        public ChoirState State { get; private set; } = ChoirState.Idle;
        /// <summary>How close it is to coming out, 0..1.</summary>
        public float Anger => Mathf.Clamp01(anger / angerThreshold);
        /// <summary>How well the listeners face where the song leads, 0 (away or split up) to 1.</summary>
        public float Alignment { get; private set; }
        public ChoirSinger[] Singers => singers;

        public float FilterStrength
        {
            get => filterStrength;
            set => filterStrength = Mathf.Clamp01(value);
        }

        public event Action<ChoirState> StateChanged;

        public void Configure(PaintedRealm owner, PaintedEntity thing, ChoirSinger[] voices)
        {
            realm = owner;
            entity = thing;
            singers = voices;
        }

        /// <summary>A player has arrived: start singing. Call once per player for co-op.</summary>
        public void Begin(Transform player)
        {
            if (!listeners.Contains(player))
                listeners.Add(player);
            if (State != ChoirState.Idle)
                return;
            anger = 0f;
            crescendo = false;
            SetState(ChoirState.Singing);
        }

        /// <summary>Everyone has left: they go quiet and the thing goes under.</summary>
        public void End()
        {
            listeners.Clear();
            if (entity != null)
                entity.Submerge();
            SetState(ChoirState.Idle);
        }

        /// <summary>A voice joined the chord: the harmony is fuller and the thing angrier.</summary>
        public void OnVoiceFreed() => anger += freedVoiceAnger;

        /// <summary>The song stops in fear and the thing stands up in the wet paint behind the player.</summary>
        public void FallSilent(bool shriek)
        {
            if (State != ChoirState.Singing || crescendo)
                return;
            timer = silenceTime;
            anger = 0f;
            SetState(ChoirState.Silent);
            if (entity != null)
                entity.Emerge(true, shriek);
        }

        /// <summary>Ends a silence early: the thing goes under and the song starts again.</summary>
        public void ResumeSinging()
        {
            if (State != ChoirState.Silent)
                return;
            if (entity != null)
                entity.Submerge();
            anger = 0f;
            SetState(ChoirState.Singing);
        }

        /// <summary>The finale: every voice at full strength, in tune, and nothing can silence them.</summary>
        public void Crescendo()
        {
            crescendo = true;
            anger = 0f;
            SetState(ChoirState.Singing);
            foreach (var singer in singers)
                if (singer != null)
                {
                    singer.SetDetune(0f);
                    singer.SetVolumeScale(1.8f);
                }
        }

        void Update()
        {
            HandleFilterKey();
            ApplyFilters();
            if (State == ChoirState.Idle)
                return;

            if (State == ChoirState.Singing)
            {
                Vector3? goal = realm != null ? realm.CurrentGoal : (Vector3?)null;
                Alignment = goal.HasValue ? ComputeAlignment(listeners, goal.Value) : 0f;
                float detune = crescendo ? 0f : 1f - Alignment;
                float swell = crescendo ? 1.8f : Mathf.Lerp(0.55f, 1.15f, Alignment);
                foreach (var singer in singers)
                {
                    if (singer == null)
                        continue;
                    singer.SetDetune(detune);
                    singer.SetVolumeScale(swell);
                    if (goal.HasValue)
                        singer.LookToward(goal.Value, lookSpeed);
                }
                if (crescendo)
                    return;

                float harmony = realm != null ? realm.Harmony : 0f;
                float rate = (baseAngerRate + harmonyAngerRate * harmony) * Mathf.Lerp(1f, inTuneMultiplier, Alignment);
                anger += rate * Time.deltaTime;
                if (anger >= angerThreshold)
                    FallSilent(true);
            }
            else if (State == ChoirState.Silent)
            {
                timer -= Time.deltaTime;
                if (entity != null && entity.Hunting)
                    foreach (var singer in singers)
                        if (singer != null)
                            singer.LookToward(entity.transform.position, snapSpeed);
                if (timer <= 0f)
                    ResumeSinging();
            }
        }

        /// <summary>
        /// How well a group faces where the song leads: each listener scores 1 facing the goal and
        /// 0 facing 145 degrees or more away, and the group's score is the average. Split up and it sours.
        /// </summary>
        public static float ComputeAlignment(IReadOnlyList<Transform> who, Vector3 goal)
        {
            if (who == null || who.Count == 0)
                return 0f;
            float sum = 0f;
            int n = 0;
            foreach (var t in who)
            {
                if (t == null)
                    continue;
                Vector3 toGoal = goal - t.position;
                toGoal.y = 0f;
                Vector3 facing = t.forward;
                facing.y = 0f;
                n++;
                if (toGoal.sqrMagnitude < 0.01f || facing.sqrMagnitude < 0.01f)
                {
                    sum += 1f;
                    continue;
                }
                sum += 1f - Mathf.Clamp01((Vector3.Angle(facing, toGoal) - 25f) / 120f);
            }
            return n == 0 ? 0f : sum / n;
        }

        void ApplyFilters()
        {
            float cutoff = Mathf.Lerp(22000f, buriedCutoff, filterStrength);
            float distortion = maxDistortion * filterStrength * Mathf.Max(Anger, realm != null ? realm.Harmony * 0.3f : 0f);
            foreach (var singer in singers)
                if (singer != null)
                    singer.SetFilter(cutoff, distortion, filterStrength);
            if (reverb != null)
            {
                reverb.enabled = filterStrength > 0.01f;
                reverb.room = Mathf.RoundToInt(Mathf.Lerp(-10000f, 0f, Mathf.Sqrt(filterStrength)));
            }
        }

        void HandleFilterKey()
        {
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.F6);
#endif
            if (!pressed || State == ChoirState.Idle)
                return;
            filterStrength = filterStrength > 0.75f ? 0f : filterStrength < 0.25f ? 0.5f : 1f;
            if (realm != null)
                realm.ShowSubtitle($"Voice filter: {Mathf.RoundToInt(filterStrength * 100f)}%", 1.5f);
        }

        void SetState(ChoirState next)
        {
            State = next;
            foreach (var singer in singers)
                if (singer != null)
                    singer.SetSinging(next == ChoirState.Singing);
            StateChanged?.Invoke(next);
        }
    }
}
