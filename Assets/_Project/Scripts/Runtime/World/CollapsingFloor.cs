using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// Rotten boards that hold just long enough to be fair: they groan when the player comes near,
    /// sag and shudder when stepped on, then break and drop whoever is on them. They break even if
    /// the player steps back off in time. Restores itself when the player respawns.
    /// </summary>
    public class CollapsingFloor : MonoBehaviour
    {
        [Tooltip("The invisible collider the player walks on. It is switched off when the boards break.")]
        [SerializeField] Collider surface;
        [Tooltip("The visible boards. They sag, then fall.")]
        [SerializeField] Transform[] pieces = new Transform[0];
        [SerializeField] float warnDistance = 3f;
        [SerializeField] float breakDelay = 1.3f;
        [SerializeField] float sagDepth = 0.15f;
        [SerializeField] float shudderAngle = 3f;

        enum State { Idle, Sagging, Broken }

        State state;
        bool warned;
        float timer;
        Transform player;
        Vector3[] basePositions;
        Quaternion[] baseRotations;

        void Start()
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            player = motor != null ? motor.transform : null;
            basePositions = new Vector3[pieces.Length];
            baseRotations = new Quaternion[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                basePositions[i] = pieces[i].localPosition;
                baseRotations[i] = pieces[i].localRotation;
            }
        }

        void OnEnable() => FallDeath.Respawned += Restore;
        void OnDisable() => FallDeath.Respawned -= Restore;

        void Update()
        {
            if (player == null || surface == null)
                return;
            switch (state)
            {
                case State.Idle:
                    var b = surface.bounds;
                    Vector3 feet = player.position;
                    float distance = Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(
                        Mathf.Clamp(feet.x, b.min.x, b.max.x), Mathf.Clamp(feet.z, b.min.z, b.max.z)));
                    bool onIt = distance < 0.05f && feet.y > b.min.y - 0.3f && feet.y < b.max.y + 1.5f;
                    if (!warned && distance < warnDistance && Mathf.Abs(feet.y - b.max.y) < 2.5f)
                    {
                        warned = true;
                        TrapSounds.Play(TrapSound.Creak, b.center, 0.45f);
                    }
                    if (onIt)
                    {
                        state = State.Sagging;
                        timer = 0f;
                        TrapSounds.Play(TrapSound.Creak, feet, 1f);
                    }
                    break;

                case State.Sagging:
                    timer += Time.deltaTime;
                    float k = Mathf.Clamp01(timer / breakDelay);
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        if (pieces[i] == null)
                            continue;
                        float wobble = Mathf.Sin(timer * 23f + i * 1.7f);
                        pieces[i].localPosition = basePositions[i] + Vector3.down * (sagDepth * k * (0.6f + 0.4f * Mathf.Sin(i * 2.3f)));
                        pieces[i].localRotation = baseRotations[i] * Quaternion.Euler(wobble * shudderAngle * k, 0f, wobble * shudderAngle * 0.5f * k);
                    }
                    if (timer >= breakDelay)
                        Break();
                    break;
            }
        }

        void Break()
        {
            state = State.Broken;
            surface.enabled = false;
            TrapSounds.Play(TrapSound.Crack, surface.bounds.center, 1f);
            foreach (var piece in pieces)
            {
                if (piece == null)
                    continue;
                // Colliders only while falling, so the boards land in a heap instead of dropping through the floor.
                if (piece.GetComponent<Collider>() == null)
                    piece.gameObject.AddComponent<BoxCollider>();
                var body = piece.GetComponent<Rigidbody>();
                if (body == null)
                    body = piece.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = false;
                body.mass = 8f;
                body.linearVelocity = Vector3.down * Random.Range(0.5f, 2f);
                body.angularVelocity = Random.insideUnitSphere * 3f;
            }
        }

        void Restore()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                    continue;
                var body = pieces[i].GetComponent<Rigidbody>();
                if (body != null)
                    Destroy(body);
                var box = pieces[i].GetComponent<BoxCollider>();
                if (box != null)
                    Destroy(box);
                pieces[i].localPosition = basePositions[i];
                pieces[i].localRotation = baseRotations[i];
            }
            surface.enabled = true;
            state = State.Idle;
            warned = false;
        }
    }
}
