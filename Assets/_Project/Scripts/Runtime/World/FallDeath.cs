using System.Collections;
using HorrorGame.Player;
using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// Kills the player after a long fall, shows a caption and a death counter, and respawns them at
    /// the start a moment later. Falls between <see cref="hardLandingDrop"/> and <see cref="lethalDrop"/>
    /// only hurt: a thud and a jolt. Put one in any scene with pits.
    /// Traps listen to <see cref="Respawned"/> to put themselves back.
    /// </summary>
    public class FallDeath : MonoBehaviour
    {
        public static event System.Action Respawned;

        [SerializeField] float lethalDrop = 5.5f;
        [SerializeField] float hardLandingDrop = 3f;
        [SerializeField] float killBelowY = -30f;
        [SerializeField] float blackoutSeconds = 2.2f;
        [SerializeField] string[] captions =
        {
            "The floor was more of a suggestion.",
            "You fell {0:0.0} m. Nobody heard.",
            "Gravity: 1. You: 0.",
            "Should've watched your step.",
            "That's going in the report.",
            "Not covered by the contract.",
        };

        PlayerMotor motor;
        Transform cameraTransform;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float peakY;
        bool wasGrounded = true;
        bool dying;
        int deaths;
        float fade;
        float shake;
        float roll;
        string caption = "";
        Texture2D black;
        GUIStyle captionStyle, counterStyle;

        void Start()
        {
            motor = FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
            {
                enabled = false;
                return;
            }
            spawnPosition = motor.transform.position;
            spawnRotation = motor.transform.rotation;
            peakY = spawnPosition.y;
            var cam = motor.GetComponentInChildren<Camera>();
            cameraTransform = cam != null ? cam.transform : null;
        }

        /// <summary>Respawn here from now on.</summary>
        public void SetCheckpoint(Vector3 position, Quaternion rotation)
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }

        void Update()
        {
            if (dying)
                return;
            float y = motor.transform.position.y;
            bool grounded = motor.IsGrounded;
            if (!grounded)
            {
                peakY = Mathf.Max(peakY, y);
            }
            else
            {
                if (!wasGrounded)
                {
                    float drop = peakY - y;
                    if (drop >= lethalDrop)
                    {
                        StartCoroutine(Die(drop));
                        return;
                    }
                    if (drop >= hardLandingDrop)
                    {
                        TrapSounds.Play(TrapSound.Thud, motor.transform.position, 0.8f);
                        shake = 0.35f;
                    }
                }
                peakY = y;
            }
            wasGrounded = grounded;
            if (y < killBelowY)
                StartCoroutine(Die(peakY - y));
        }

        void LateUpdate()
        {
            if (cameraTransform == null)
                return;
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime);
            float s = shake * 6f;
            cameraTransform.localRotation = Quaternion.Euler(Random.Range(-s, s), Random.Range(-s, s), roll + Random.Range(-s, s));
        }

        IEnumerator Die(float drop)
        {
            dying = true;
            deaths++;
            caption = captions.Length > 0 ? string.Format(captions[Random.Range(0, captions.Length)], drop) : "";
            TrapSounds.Play(TrapSound.Thud, motor.transform.position, 1f);
            TrapSounds.Play(TrapSound.Crack, motor.transform.position, 0.6f);
            SetControl(false);

            // Keel over, then black.
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                roll = Mathf.Lerp(0f, 70f, t);
                fade = t;
                yield return null;
            }
            fade = 1f;
            yield return new WaitForSeconds(blackoutSeconds);

            roll = 0f;
            motor.Teleport(spawnPosition, spawnRotation);
            peakY = spawnPosition.y;
            wasGrounded = true;
            Respawned?.Invoke();
            SetControl(true);
            PlayerLook.SetCursorLocked(true);
            dying = false;
            for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.6f)
            {
                fade = t;
                yield return null;
            }
            fade = 0f;
        }

        void SetControl(bool on)
        {
            foreach (var behaviour in new MonoBehaviour[]
                     {
                         motor, motor.GetComponent<PlayerLook>(), motor.GetComponent<PlayerInputReader>(),
                     })
                if (behaviour != null)
                    behaviour.enabled = on;
        }

        void OnGUI()
        {
            if (fade <= 0f && deaths == 0)
                return;
            if (black == null)
            {
                black = new Texture2D(1, 1);
                black.SetPixel(0, 0, Color.black);
                black.Apply();
                captionStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
                counterStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperRight, fontSize = 16 };
            }
            if (fade > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
                captionStyle.normal.textColor = new Color(0.85f, 0.8f, 0.75f, fade);
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, 40), caption, captionStyle);
                GUI.color = Color.white;
            }
            if (deaths > 0)
            {
                counterStyle.normal.textColor = new Color(0.8f, 0.75f, 0.7f, 0.6f);
                GUI.Label(new Rect(0, 12, Screen.width - 16, 24), $"Falls: {deaths}", counterStyle);
            }
        }
    }
}
