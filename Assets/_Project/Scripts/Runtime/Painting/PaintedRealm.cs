using System;
using System.Collections;
using HorrorGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The world inside a haunted painting. It is built in the same scene as the painting, far away
    /// from everything else, so "entering" is a teleport plus a change of atmosphere: fog, ambient
    /// light, the pixel filter off and the oil-paint filter on. Leaving puts everything back.
    ///
    /// You are trapped: the frame you came through will not open. Burn every <see cref="BanishAnchor"/>
    /// and the frame opens, the entity goes berserk, and you have to run back to the frame to escape.
    /// Escaping breaks the curse: the entity is gone and the painting is left burnt.
    /// Wrong Doors can reuse the same enter and leave plumbing later.
    /// </summary>
    public class PaintedRealm : MonoBehaviour
    {
        public enum RealmState { Outside, Inside, Banished }

        [Header("Where the player goes")]
        [SerializeField] Transform arrivalPoint;

        [Header("Inside")]
        [SerializeField] PaintedEntity entity;
        [Tooltip("Optional. Without a choir the entity hunts all the time.")]
        [SerializeField] Choir choir;
        [Tooltip("The frame you escape through; the choir points here once every anchor has burned.")]
        [SerializeField] Transform exitPoint;
        [Tooltip("Door of the one real church in the fog. Until you are inside, the choir points here. Optional.")]
        [SerializeField] Transform realChurchDoor;
        [Tooltip("Places in the field a false church door can drop you.")]
        [SerializeField] Transform[] lostPoints = Array.Empty<Transform>();
        [SerializeField] string falseDoorLine = "More field. The singing has stopped.";
        [SerializeField] string churchLine = "They are all in here. The song is deafening.";
        [SerializeField] BanishAnchor[] anchors = Array.Empty<BanishAnchor>();
        [SerializeField] PaintAttention attention;
        [SerializeField] PaintFilter paintFilter;
        [Tooltip("Global volume with the painting's colour grade. Enabled only while inside.")]
        [SerializeField] GameObject realmVolume;
        [SerializeField] Color fogColor = new Color(0.32f, 0.24f, 0.15f);
        [SerializeField] float fogDensity = 0.035f;
        [SerializeField] Color ambientLight = new Color(0.32f, 0.26f, 0.2f);
        [Tooltip("Lights that belong to the inside of the painting, switched on only while inside.")]
        [SerializeField] GameObject realmLighting;

        [Header("Outside (switched off while inside)")]
        [Tooltip("The pixel filter, post-processing volume, moonlight and other things of the real world.")]
        [SerializeField] GameObject[] outsideOnly = Array.Empty<GameObject>();

        [Header("Feel")]
        [SerializeField] float fadeTime = 0.8f;
        [SerializeField] string arrivalLine = "The paint is still wet. Somebody is singing.";
        [SerializeField] string trappedLine = "The frame won't move. Something here is holding it shut.";
        [SerializeField] string openLine = "The frame is open. RUN.";
        [SerializeField] string banishedLine = "The canvas is ash. Whatever lived in it is gone.";

        HauntedPainting painting;
        GameObject player;
        Camera playerCamera;
        Vector3 returnPosition;
        Quaternion returnRotation;
        bool busy;

        // Saved real-world atmosphere.
        bool savedFog;
        Color savedFogColor, savedAmbient, savedCameraBackground;
        float savedFogDensity;
        FogMode savedFogMode;

        // Simple overlay: black fade, objective counter and a subtitle line.
        Canvas overlay;
        Image fade;
        Text counter;
        Text subtitle;
        Coroutine subtitleRoutine;

        public RealmState State { get; private set; } = RealmState.Outside;
        public int Burned { get; private set; }
        /// <summary>True once every anchor has burned: the frame you came through can be climbed out of.</summary>
        public bool ExitOpen { get; private set; }
        public int AnchorCount => anchors.Length;
        public PaintAttention Attention => attention;
        public PaintFilter Filter => paintFilter;
        public GameObject Player => player;
        public Choir Choir => choir;
        public bool Busy => busy;
        /// <summary>True once the player has found the real church.</summary>
        public bool InChurch { get; private set; }

        /// <summary>
        /// Where the player should go next: the nearest anchor still standing, or the exit frame once
        /// every anchor has burned. Null when there is nowhere to go.
        /// </summary>
        public Vector3? CurrentGoal
        {
            get
            {
                if (realChurchDoor != null && !InChurch)
                    return realChurchDoor.position;
                if (ExitOpen)
                    return exitPoint != null ? exitPoint.position : (Vector3?)null;
                Vector3 from = player != null ? player.transform.position : transform.position;
                Vector3? best = null;
                float bestDistance = float.MaxValue;
                foreach (var anchor in anchors)
                {
                    if (anchor == null || !anchor.IsIntact)
                        continue;
                    float d = Vector3.Distance(from, anchor.transform.position);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = anchor.transform.position;
                    }
                }
                return best;
            }
        }

        /// <summary>Raised when the player escapes with the frame open and the entity is gone.</summary>
        public event Action Banished;

        void Awake()
        {
            if (realmVolume != null) realmVolume.SetActive(false);
            if (realmLighting != null) realmLighting.SetActive(false);
            if (paintFilter != null) paintFilter.enabled = false;
            if (attention != null) attention.enabled = false;
            foreach (var anchor in anchors)
                if (anchor != null)
                    anchor.Realm = this;
            if (entity != null)
                entity.Realm = this;
        }

        public void Configure(Transform arrival, PaintedEntity realmEntity, BanishAnchor[] realmAnchors,
            PaintAttention realmAttention, PaintFilter filter)
        {
            arrivalPoint = arrival;
            entity = realmEntity;
            anchors = realmAnchors;
            attention = realmAttention;
            paintFilter = filter;
            Awake();
        }

        // ------------------------------------------------------------------ entering and leaving

        public bool CanEnter => State == RealmState.Outside && !busy;

        public void Enter(GameObject who, HauntedPainting from)
        {
            if (!CanEnter || who == null)
                return;
            StartCoroutine(EnterRoutine(who, from));
        }

        IEnumerator EnterRoutine(GameObject who, HauntedPainting from)
        {
            busy = true;
            player = who;
            painting = from;
            playerCamera = who.GetComponentInChildren<Camera>();
            returnPosition = who.transform.position;
            returnRotation = who.transform.rotation;

            yield return Fade(0f, 1f);
            SetInside(true);
            Teleport(arrivalPoint.position, arrivalPoint.rotation);
            if (painting != null)
                painting.SetEmpty(true);
            State = RealmState.Inside;
            if (entity != null)
                entity.Activate(player);
            if (choir != null)
                choir.Begin(player.transform);
            else if (entity != null)
                entity.Emerge(false, false);
            UpdateCounter();
            yield return Fade(1f, 0f);
            ShowSubtitle(arrivalLine, 4f);
            busy = false;
            yield return new WaitForSeconds(5f);
            if (State == RealmState.Inside && !ExitOpen)
                ShowSubtitle(realChurchDoor != null ? "Follow the singing." : "Burn the easels. Follow the singing.", 4f);
        }

        /// <summary>Climb out through the frame. Only works once every anchor has burned.</summary>
        public void Leave()
        {
            if (State != RealmState.Inside || busy)
                return;
            if (!ExitOpen)
            {
                ShowSubtitle(trappedLine, 3f);
                return;
            }
            StartCoroutine(LeaveRoutine(true));
        }

        IEnumerator LeaveRoutine(bool banished)
        {
            busy = true;
            if (banished && entity != null)
                entity.Banish();
            yield return Fade(0f, 1f);
            if (choir != null)
                choir.End();
            if (entity != null)
                entity.Deactivate();
            if (banished)
                Banished?.Invoke();
            SetInside(false);
            Teleport(returnPosition, returnRotation);
            if (painting != null)
            {
                painting.SetEmpty(false);
                if (banished)
                    painting.Burn();
            }
            State = banished ? RealmState.Banished : RealmState.Outside;
            yield return Fade(1f, 0f);
            if (banished)
                ShowSubtitle(banishedLine, 5f);
            busy = false;
        }

        void SetInside(bool inside)
        {
            foreach (var go in outsideOnly)
                if (go != null)
                    go.SetActive(!inside);
            if (realmVolume != null) realmVolume.SetActive(inside);
            if (realmLighting != null) realmLighting.SetActive(inside);

            if (inside)
            {
                savedFog = RenderSettings.fog;
                savedFogColor = RenderSettings.fogColor;
                savedFogDensity = RenderSettings.fogDensity;
                savedFogMode = RenderSettings.fogMode;
                savedAmbient = RenderSettings.ambientLight;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.ambientLight = ambientLight;
                if (playerCamera != null)
                {
                    savedCameraBackground = playerCamera.backgroundColor;
                    playerCamera.backgroundColor = fogColor;
                }
            }
            else
            {
                RenderSettings.fog = savedFog;
                RenderSettings.fogColor = savedFogColor;
                RenderSettings.fogDensity = savedFogDensity;
                RenderSettings.fogMode = savedFogMode;
                RenderSettings.ambientLight = savedAmbient;
                if (playerCamera != null)
                    playerCamera.backgroundColor = savedCameraBackground;
            }

            if (attention != null)
            {
                attention.Viewer = playerCamera;
                attention.enabled = inside;
            }
            if (paintFilter != null)
            {
                paintFilter.Dread = 0f;
                paintFilter.enabled = inside;
            }
            if (counter != null)
                counter.enabled = inside;
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
                motor.Teleport(position, rotation);
            else
                player.transform.SetPositionAndRotation(position, rotation);
        }

        // ------------------------------------------------------------------ the churches

        /// <summary>A false church: the door opens onto more field somewhere else, and the singing stops.</summary>
        public void FalseDoor(Vector3 doorPosition)
        {
            if (State != RealmState.Inside || busy)
                return;
            StartCoroutine(FalseDoorRoutine(doorPosition));
        }

        IEnumerator FalseDoorRoutine(Vector3 doorPosition)
        {
            busy = true;
            yield return Fade(0f, 1f);
            // Land at the lost point farthest from this door.
            Transform best = null;
            float bestDistance = -1f;
            foreach (var point in lostPoints)
            {
                if (point == null)
                    continue;
                float d = Vector3.Distance(point.position, doorPosition);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = point;
                }
            }
            if (best != null)
                Teleport(best.position, best.rotation);
            if (attention != null)
                attention.ResetAll();
            yield return Fade(1f, 0f);
            busy = false;
            ShowSubtitle(falseDoorLine, 3.5f);
            if (choir != null)
                choir.FallSilent(true);
        }

        /// <summary>The real church: step inside for the last stretch.</summary>
        public void EnterChurch(Transform inside)
        {
            if (State != RealmState.Inside || busy || inside == null)
                return;
            StartCoroutine(EnterChurchRoutine(inside));
        }

        IEnumerator EnterChurchRoutine(Transform inside)
        {
            busy = true;
            yield return Fade(0f, 1f);
            if (choir != null)
                choir.ResumeSinging();
            InChurch = true;
            Teleport(inside.position, inside.rotation);
            if (attention != null)
                attention.ResetAll();
            UpdateCounter();
            yield return Fade(1f, 0f);
            busy = false;
            ShowSubtitle(churchLine, 4f);
        }

        // ------------------------------------------------------------------ banishing

        /// <summary>Called by an anchor when it has been burned.</summary>
        public void OnAnchorBurned(BanishAnchor anchor)
        {
            if (State != RealmState.Inside)
                return;
            Burned++;
            UpdateCounter();
            if (entity != null)
                entity.AddBurn();
            if (choir != null)
                choir.FallSilent(true);
            else if (entity != null)
                entity.Emerge(true, true);
            if (Burned >= anchors.Length)
            {
                ExitOpen = true;
                UpdateCounter();
                ShowSubtitle(openLine, 4f);
            }
        }

        // ------------------------------------------------------------------ overlay

        void EnsureOverlay()
        {
            if (overlay != null)
                return;
            var canvasObject = new GameObject("PaintedRealmOverlay", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            overlay = canvasObject.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 90;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            fade = new GameObject("Fade", typeof(RectTransform)).AddComponent<Image>();
            fade.transform.SetParent(canvasObject.transform, false);
            fade.color = new Color(0.05f, 0.02f, 0.01f, 0f);
            fade.raycastTarget = false;
            Stretch(fade.rectTransform);

            counter = MakeText("Counter", canvasObject.transform, font, 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
            counter.enabled = false;
            subtitle = MakeText("Subtitle", canvasObject.transform, font, 34, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f));
        }

        static Text MakeText(string name, Transform parent, Font font, int size, TextAnchor anchor, Vector2 pivot, Vector2 offset)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.85f, 0.78f, 0.62f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1400f, 60f);
            return text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        IEnumerator Fade(float from, float to)
        {
            EnsureOverlay();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime))
            {
                SetFade(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float alpha)
        {
            var c = fade.color;
            c.a = alpha;
            fade.color = c;
            if (paintFilter != null && paintFilter.enabled)
                paintFilter.Strength = 1f - alpha * 0.5f;
        }

        void UpdateCounter()
        {
            EnsureOverlay();
            if (realChurchDoor != null && !InChurch)
                counter.text = "Find the church they are singing about";
            else
                counter.text = ExitOpen ? "Get back to the frame" : $"Burned {Burned} / {anchors.Length}";
        }

        public void ShowSubtitle(string line, float seconds)
        {
            EnsureOverlay();
            if (subtitleRoutine != null)
                StopCoroutine(subtitleRoutine);
            subtitleRoutine = StartCoroutine(SubtitleRoutine(line, seconds));
        }

        IEnumerator SubtitleRoutine(string line, float seconds)
        {
            subtitle.text = line;
            yield return new WaitForSeconds(seconds);
            subtitle.text = string.Empty;
            subtitleRoutine = null;
        }

        void OnDisable()
        {
            // Never leave the renderer feature switched on in the editor.
            if (paintFilter != null)
                paintFilter.enabled = false;
        }
    }
}
