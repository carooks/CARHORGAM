using System;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The choir buried in the painted field. While they sing, you are safe, and the song shows the
    /// way: every head turns toward where you need to go next (the nearest unburnt easel, then the
    /// frame), and the chord is in tune when you face that way and curdles when you face away.
    ///
    /// Every so often, and every time an easel burns, they all stop at once and turn their heads to
    /// one spot in the wet paint behind you. Something is standing there. Follow their eyes and look
    /// at it to pin it in place until the song starts again.
    /// </summary>
    public class Choir : MonoBehaviour
    {
        public enum ChoirState { Idle, Singing, Silent }

        [SerializeField] ChoirSinger[] singers = Array.Empty<ChoirSinger>();
        [SerializeField] PaintedRealm realm;
        [SerializeField] PaintedEntity entity;
        [Tooltip("Seconds of singing between silences (random in this range).")]
        [SerializeField] Vector2 singTime = new Vector2(30f, 50f);
        [Tooltip("Seconds of singing before the first silence.")]
        [SerializeField] float firstSilenceAfter = 25f;
        [Tooltip("How long a silence lasts.")]
        [SerializeField] float silenceTime = 11f;
        [SerializeField] float lookSpeed = 60f;
        [Tooltip("Heads snap to the thing this fast when the song stops.")]
        [SerializeField] float snapSpeed = 900f;

        float timer;
        Transform listener;

        public ChoirState State { get; private set; } = ChoirState.Idle;
        public float TimeLeftInState => timer;
        public ChoirSinger[] Singers => singers;

        public event Action<ChoirState> StateChanged;

        public void Configure(PaintedRealm owner, PaintedEntity thing, ChoirSinger[] voices)
        {
            realm = owner;
            entity = thing;
            singers = voices;
        }

        /// <summary>The player has arrived: start singing.</summary>
        public void Begin(Transform player)
        {
            listener = player;
            timer = firstSilenceAfter;
            SetState(ChoirState.Singing);
        }

        /// <summary>The player has left: everyone goes quiet and the thing goes under.</summary>
        public void End()
        {
            if (entity != null)
                entity.Submerge();
            SetState(ChoirState.Idle);
        }

        /// <summary>Stops the song now (an easel just burned). Ignored if already silent.</summary>
        public void FallSilent(bool shriek)
        {
            if (State != ChoirState.Singing)
                return;
            timer = silenceTime;
            SetState(ChoirState.Silent);
            if (entity != null)
                entity.Emerge(true, shriek);
        }

        void Update()
        {
            if (State == ChoirState.Idle)
                return;

            timer -= Time.deltaTime;
            if (State == ChoirState.Singing)
            {
                Vector3? goal = realm != null ? realm.CurrentGoal : (Vector3?)null;
                float detune = 0f;
                if (goal.HasValue && listener != null)
                {
                    // In tune facing the goal, sour facing away.
                    Vector3 toGoal = goal.Value - listener.position;
                    toGoal.y = 0f;
                    Vector3 facing = listener.forward;
                    facing.y = 0f;
                    if (toGoal.sqrMagnitude > 0.01f && facing.sqrMagnitude > 0.01f)
                        detune = Mathf.Clamp01((Vector3.Angle(facing, toGoal) - 25f) / 120f);
                }
                foreach (var singer in singers)
                {
                    if (singer == null)
                        continue;
                    singer.SetDetune(detune);
                    if (goal.HasValue)
                        singer.LookToward(goal.Value, lookSpeed);
                }
                if (timer <= 0f)
                    FallSilent(false);
            }
            else if (State == ChoirState.Silent)
            {
                if (entity != null && entity.Hunting)
                    foreach (var singer in singers)
                        if (singer != null)
                            singer.LookToward(entity.transform.position, snapSpeed);
                if (timer <= 0f)
                {
                    if (entity != null)
                        entity.Submerge();
                    timer = UnityEngine.Random.Range(singTime.x, singTime.y);
                    SetState(ChoirState.Singing);
                }
            }
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
