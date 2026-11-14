using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorGame.Player
{
    /// <summary>
    /// What happens when the player dies: controls freeze, the camera snaps to whatever killed them,
    /// the screen cuts to black with a message, and the level restarts.
    /// Placeholder for the Phase 4 restart flow (title screen, retry menu).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class CaughtScreen : MonoBehaviour
    {
        [SerializeField] string message = "YOU WERE CAUGHT";
        [Tooltip("Seconds the player stares at the monster before the screen goes black.")]
        [SerializeField] float stareTime = 0.6f;
        [SerializeField] float restartDelay = 3.5f;

        PlayerHealth health;

        void Awake() => health = GetComponent<PlayerHealth>();
        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(GameObject killer) => StartCoroutine(CaughtSequence(killer));

        IEnumerator CaughtSequence(GameObject killer)
        {
            // Stop all player control. PlayerLook unlocks the cursor when disabled.
            foreach (var behaviour in new MonoBehaviour[]
                     {
                         GetComponent<PlayerInputReader>(), GetComponent<PlayerMotor>(), GetComponent<PlayerLook>(),
                         GetComponent<PlayerFootsteps>(),
                     })
                if (behaviour != null)
                    behaviour.enabled = false;

            var cam = GetComponentInChildren<Camera>();
            if (killer != null && cam != null)
            {
                // Whip the view onto the killer's face.
                Vector3 face = killer.transform.position + Vector3.up * 2f;
                Quaternion start = cam.transform.rotation;
                Quaternion end = Quaternion.LookRotation(face - cam.transform.position);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
                {
                    cam.transform.rotation = Quaternion.Slerp(start, end, t);
                    yield return null;
                }
                cam.transform.rotation = end;
            }
            yield return new WaitForSeconds(stareTime);

            BuildOverlay();
            yield return new WaitForSeconds(restartDelay);
            RestartLevel();
        }

        static void RestartLevel()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // In the editor this also works for scenes that aren't in the build list.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        void BuildOverlay()
        {
            var canvasObject = new GameObject("CaughtScreen", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            black.transform.SetParent(canvasObject.transform, false);
            black.color = Color.black;
            black.rectTransform.anchorMin = Vector2.zero;
            black.rectTransform.anchorMax = Vector2.one;
            black.rectTransform.offsetMin = black.rectTransform.offsetMax = Vector2.zero;

            var text = new GameObject("Message", typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(canvasObject.transform, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = message;
            text.fontSize = 56;
            text.color = new Color(0.75f, 0.1f, 0.08f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(1200f, 120f);
        }
    }
}
