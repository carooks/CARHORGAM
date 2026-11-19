using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// Plays a warning sound when the player walks into a box zone, e.g. grit trickling over the edge
    /// of a drop, so a death never comes without a hint. Repeats after a cooldown.
    /// </summary>
    public class TrapCue : MonoBehaviour
    {
        [SerializeField] Vector3 size = new Vector3(4f, 3f, 2f);
        [SerializeField] TrapSound sound = TrapSound.Trickle;
        [SerializeField] float volume = 0.7f;
        [SerializeField] float cooldown = 6f;
        [Tooltip("Where the sound comes from, relative to this object (e.g. down in the pit).")]
        [SerializeField] Vector3 soundOffset = new Vector3(0f, -2f, 0f);

        Transform player;
        float nextTime;
        bool inside;

        void Start()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            player = motor != null ? motor.transform : null;
        }

        void Update()
        {
            if (player == null)
                return;
            Vector3 local = transform.InverseTransformPoint(player.position);
            bool now = Mathf.Abs(local.x) < size.x * 0.5f && Mathf.Abs(local.y) < size.y * 0.5f && Mathf.Abs(local.z) < size.z * 0.5f;
            if (now && !inside && Time.time >= nextTime)
            {
                TrapSounds.Play(sound, transform.TransformPoint(soundOffset), volume);
                nextTime = Time.time + cooldown;
            }
            inside = now;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
