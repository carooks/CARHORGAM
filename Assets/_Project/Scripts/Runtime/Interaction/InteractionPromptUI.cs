using HorrorGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Interaction
{
    /// <summary>
    /// Minimal HUD for interaction: a small crosshair dot and a prompt line under it, e.g. "[E] Cut the power".
    /// Builds its own screen-space canvas at runtime so it needs no scene or prefab setup and no fonts
    /// imported into the project (it uses Unity's built-in font).
    /// </summary>
    [RequireComponent(typeof(PlayerInteractor))]
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] int fontSize = 24;
        [SerializeField] Color promptColor = new Color(0.92f, 0.92f, 0.88f);
        [SerializeField] Color unavailableColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        [SerializeField] Color crosshairIdle = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color crosshairFocused = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] float crosshairIdleSize = 4f;
        [SerializeField] float crosshairFocusedSize = 7f;

        PlayerInteractor interactor;
        GameObject canvasObject;
        Image crosshair;
        Text prompt;

        void Awake()
        {
            interactor = GetComponent<PlayerInteractor>();
            BuildCanvas();
        }

        void OnEnable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(true);
        }

        void OnDisable()
        {
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        void LateUpdate()
        {
            bool focused = interactor.Focus != null;
            crosshair.color = focused ? crosshairFocused : crosshairIdle;
            float size = focused ? crosshairFocusedSize : crosshairIdleSize;
            crosshair.rectTransform.sizeDelta = new Vector2(size, size);

            string text = interactor.Prompt;
            if (string.IsNullOrEmpty(text))
            {
                prompt.enabled = false;
                return;
            }
            prompt.enabled = true;
            bool available = interactor.CanInteractWithFocus;
            prompt.text = available ? $"[{PlayerInputReader.InteractKeyLabel}] {text}" : text;
            prompt.color = available ? promptColor : unavailableColor;
        }

        void BuildCanvas()
        {
            canvasObject = new GameObject("InteractionHUD", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var crosshairObject = new GameObject("Crosshair", typeof(RectTransform));
            crosshairObject.transform.SetParent(canvasObject.transform, false);
            crosshair = crosshairObject.AddComponent<Image>();
            crosshair.raycastTarget = false;
            Centre(crosshair.rectTransform, Vector2.zero, new Vector2(crosshairIdleSize, crosshairIdleSize));

            var promptObject = new GameObject("Prompt", typeof(RectTransform));
            promptObject.transform.SetParent(canvasObject.transform, false);
            prompt = promptObject.AddComponent<Text>();
            prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            prompt.fontSize = fontSize;
            prompt.alignment = TextAnchor.MiddleCenter;
            prompt.horizontalOverflow = HorizontalWrapMode.Overflow;
            prompt.raycastTarget = false;
            prompt.enabled = false;
            Centre(prompt.rectTransform, new Vector2(0f, -48f), new Vector2(800f, 40f));
            var shadow = promptObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        static void Centre(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
