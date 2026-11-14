using System;
using System.Collections;
using HorrorGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The pocket world inside a cursed painting. It is built in the same scene as the painting, far
    /// away from everything else, so "entering" is a teleport plus a change of atmosphere: fog,
    /// ambient light, the pixel filter off and the oil-paint filter on. Leaving puts everything back.
    ///
    /// The escape: the choir's chord is missing voices. Find the singers whose mouths are painted
    /// over (<see cref="MutedVoice"/>) and scrape them free; the song leads you from one to the next.
    /// Once the chord is complete, the one real church among the black churches in the fog opens.
    /// Walk to its altar: the full chord destroys the thing that hates harmony, and the painting
    /// lets you go. Its canvas is left burnt.
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
        [Tooltip("The singers whose mouths are painted over. Free them all to complete the chord.")]
        [SerializeField] MutedVoice[] voices = Array.Empty<MutedVoice>();
        [Tooltip("Door of the one real church. Opens once the chord is complete. Optional.")]
        [SerializeField] Transform realChurchDoor;
        [Tooltip("The altar inside the real church: reach it with the chord complete to destroy the thing.")]
        [SerializeField] Transform altar;
        [SerializeField] float altarReach = 6f;
        [Tooltip("Where the thing stands for the finale, between you and the altar.")]
        [SerializeField] Transform finaleSpot;
        [Tooltip("Places in the field a false church door can drop you.")]
        [SerializeField] Transform[] lostPoints = Array.Empty<Transform>();
        [SerializeField] PaintAttention attention;
        [SerializeField] PaintFilter paintFilter;
        [Tooltip("Global volume with the painting's colour grade. Enabled only while inside.")]
        [SerializeField] GameObject realmVolume;
        [SerializeField] Color fogColor = new Color(0.32f, 0.24f, 0.15f);
        [Tooltip("Thick enough to hide the churches until you are within about 50 m, thin enough to glimpse them.")]
        [SerializeField] float fogDensity = 0.026f;
        [SerializeField] Color ambientLight = new Color(0.32f, 0.26f, 0.2f);
        [Tooltip("Lights that belong to the inside of the painting, switched on only while inside.")]
        [SerializeField] GameObject realmLighting;

        [Header("Outside (switched off while inside)")]
        [Tooltip("The pixel filter, post-processing volume, moonlight and other things of the real world.")]
        [SerializeField] GameObject[] outsideOnly = Array.Empty<GameObject>();

        [Header("Feel")]
        [SerializeField] float fadeTime = 0.8f;
        [SerializeField] string arrivalLine = "The paint is still wet. Somebody is singing, but the chord is wrong.";
        [SerializeField] string hintLine = "Some of them can't sing. Follow the song.";
        [SerializeField] string completeLine = "The chord is whole. They are singing about one of the churches.";
        [SerializeField] string falseDoorLine = "More field. The singing has stopped.";
        [SerializeField] string churchLine = "They are all in here. The song is deafening.";
        [SerializeField] string banishedLine = "The canvas is ash. Whatever lived in it is gone.";

        HauntedPainting painting;
        GameObject player;
        Camera playerCamera;
        Vector3 returnPosition;
        Quaternion returnRotation;
        bool busy;
        bool finaleStarted;

        // Saved real-world atmosphere.
        bool savedFog;
        Color savedFogColor, savedAmbient, savedCameraBackground;
        float savedFogDensity;
        FogMode savedFogMode;

        // Simple overlay: black fade, objective line and a subtitle line.
        Canvas overlay;
        Image fade;
        Text counter;
        Text subtitle;
        Coroutine subtitleRoutine;

        public RealmState State { get; private set; } = RealmState.Outside;
        public int Freed { get; private set; }
        public int VoiceCount => voices.Length;
        /// <summary>0 with every painted voice still muted, 1 with the chord complete.</summary>
        public float Harmony => voices.Length == 0 ? 1f : (float)Freed / voices.Length;
        public bool ChordComplete => Freed >= voices.Length;
        /// <summary>True once the player has found the real church.</summary>
        public bool InChurch { get; private set; }
        public bool Busy => busy;
        public PaintAttention Attention => attention;
        public PaintFilter Filter => paintFilter;
        public GameObject Player => player;
        public Choir Choir => choir;

        /// <summary>Raised when the full chord destroys the entity, just before the player is returned.</summary>
        public event Action Banished;

        /// <summary>
        /// Where the song leads next: the nearest voice still painted over, then the real church,
        /// then its altar. Null when there is nowhere to go.
        /// </summary>
        public Vector3? CurrentGoal
        {
            get
            {
                if (!ChordComplete)
                {
                    Vector3 from = player != null ? player.transform.position : transform.position;
                    Vector3? best = null;
                    float bestDistance = float.MaxValue;
                    foreach (var voice in voices)
                    {
                        if (voice == null || voice.IsFreed)
                            continue;
                        float d = Vector3.Distance(from, voice.transform.position);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = voice.transform.position;
                        }
                    }
                    return best;
                }
                if (!InChurch && realChurchDoor != null)
                    return realChurchDoor.position;
                return altar != null ? altar.position : (Vector3?)null;
            }
        }

        void Awake()
        {
            if (realmVolume != null) realmVolume.SetActive(false);
            if (realmLighting != null) realmLighting.SetActive(false);
            if (paintFilter != null) paintFilter.enabled = false;
            if (attention != null) attention.enabled = false;
            foreach (var voice in voices)
                if (voice != null)
                    voice.Realm = this;
            if (entity != null)
                entity.Realm = this;
        }

        /// <summary>Wires the realm up in code (tests).</summary>
        public void Configure(Transform arrival, PaintedEntity realmEntity, MutedVoice[] realmVoices,
            PaintAttention realmAttention, PaintFilter filter, Choir realmChoir = null,
            Transform churchDoor = null, Transform realmAltar = null)
        {
            arrivalPoint = arrival;
            entity = realmEntity;
            voices = realmVoices;
            attention = realmAttention;
            paintFilter = filter;
            choir = realmChoir;
            realChurchDoor = churchDoor;
            altar = realmAltar;
            Awake();
        }

        // ------------------------------------------------------------------ test shortcuts

        /// <summary>
        /// Command-line shortcuts for test runs of the exe (screenshots, quick checks):
        ///   -enterPainting   start inside the painting
        ///   -freeVoices      also free every painted-over voice at once (the real church opens)
        /// </summary>
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-enterPainting") < 0)
                return;
            var motor = FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
                return;
            Enter(motor.gameObject, FindFirstObjectByType<HauntedPainting>());
            if (Array.IndexOf(args, "-freeVoices") >= 0)
                StartCoroutine(FreeAllWhenInside());
        }

        IEnumerator FreeAllWhenInside()
        {
            while (State != RealmState.Inside)
                yield return null;
            foreach (var voice in voices)
                if (voice != null)
                    voice.Free();
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
            if (!ChordComplete)
                ShowSubtitle(arrivalLine, 4.5f);
            busy = false;
            yield return new WaitForSeconds(6f);
            if (State == RealmState.Inside && Freed == 0)
                ShowSubtitle(hintLine, 4f);
        }

        IEnumerator ReturnRoutine()
        {
            busy = true;
            yield return Fade(0f, 1f);
            if (choir != null)
                choir.End();
            if (entity != null)
                entity.Deactivate();
            SetInside(false);
            Teleport(returnPosition, returnRotation);
            if (painting != null)
            {
                painting.SetEmpty(false);
                painting.Burn();
            }
            State = RealmState.Banished;
            yield return Fade(1f, 0f);
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

        // ------------------------------------------------------------------ the voices

        /// <summary>Called by a <see cref="MutedVoice"/> when its paint is scraped off.</summary>
        public void OnVoiceFreed(MutedVoice voice)
        {
            if (State != RealmState.Inside)
                return;
            Freed++;
            UpdateCounter();
            if (entity != null)
                entity.Agitate();
            if (choir != null)
                choir.OnVoiceFreed();
            if (ChordComplete)
                ShowSubtitle(completeLine, 4.5f);
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

        /// <summary>The real church: step inside for the last stretch. Shut until the chord is complete.</summary>
        public void EnterChurch(Transform inside)
        {
            if (State != RealmState.Inside || busy || inside == null || !ChordComplete)
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

        void Update()
        {
            if (State != RealmState.Inside || busy || finaleStarted || !ChordComplete || altar == null || player == null)
                return;
            if (realChurchDoor != null && !InChurch)
                return;
            Vector3 d = altar.position - player.transform.position;
            d.y = 0f;
            if (d.magnitude <= altarReach)
                StartCoroutine(FinaleRoutine());
        }

        /// <summary>The whole choir at full voice. The thing stands up in front of the altar and comes apart.</summary>
        IEnumerator FinaleRoutine()
        {
            finaleStarted = true;
            busy = true;
            if (choir != null)
                choir.Crescendo();
            if (entity != null)
            {
                Vector3 spot = finaleSpot != null ? finaleSpot.position : altar.position;
                entity.EmergeAt(spot);
            }
            ShowSubtitle("Every voice at once.", 3f);
            yield return new WaitForSeconds(2.5f);
            if (entity != null)
                entity.Banish();
            if (paintFilter != null)
                paintFilter.Dread = 1f;
            yield return new WaitForSeconds(2.5f);
            Banished?.Invoke();
            yield return ReturnRoutine();
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
            counter.enabled = State == RealmState.Inside;
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
            if (!ChordComplete)
                counter.text = $"Voices freed {Freed} / {voices.Length}";
            else if (!InChurch && realChurchDoor != null)
                counter.text = "Find the church they are singing about";
            else
                counter.text = "Go to the altar";
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
