using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Glass
{
    /// <summary>
    /// Makes sounds on the other side of glass muffled for whoever is listening. Every so often it
    /// finds the 3D audio sources in the scene; each frame it casts a ray from the AudioListener to each
    /// one, counts the glass panes in between, and lowers a low-pass filter on that source to match.
    ///
    /// Put one anywhere in a scene with glass. It works on the local listener only, which is what
    /// co-op needs: each player hears the house from where they stand.
    /// </summary>
    public class GlassAudioMuffler : MonoBehaviour
    {
        [Tooltip("Cutoff with one pane in the way (Hz). Lower is more muffled.")]
        [SerializeField] float onePaneCutoff = 1100f;
        [Tooltip("Cutoff with two or more panes in the way (Hz).")]
        [SerializeField] float twoPaneCutoff = 450f;
        [Tooltip("How fast the filter follows changes (higher is snappier).")]
        [SerializeField] float smoothing = 8f;
        [Tooltip("Seconds between scans for new audio sources.")]
        [SerializeField] float rescanInterval = 1f;
        [Tooltip("Sources farther than this are left alone.")]
        [SerializeField] float maxDistance = 40f;

        const float OpenCutoff = 22000f;
        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly HashSet<GlassPane> Counted = new HashSet<GlassPane>();

        sealed class Entry
        {
            public AudioSource Source;
            public AudioLowPassFilter Filter;
        }

        readonly Dictionary<GameObject, Entry> entries = new Dictionary<GameObject, Entry>();
        readonly List<GameObject> stale = new List<GameObject>();
        AudioListener listener;
        float nextScanAt;

        /// <summary>Number of glass panes a straight line from <paramref name="from"/> to <paramref name="to"/> passes through.</summary>
        public static int CountPanesBetween(Vector3 from, Vector3 to, GlassPane ignore = null)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f)
                return 0;
            int count = Physics.RaycastNonAlloc(from, direction / length, Hits, length, GlassPane.LayerMask,
                QueryTriggerInteraction.Ignore);
            Counted.Clear();
            for (int i = 0; i < count; i++)
            {
                var pane = Hits[i].collider.GetComponentInParent<GlassPane>();
                if (pane != null && pane != ignore)
                    Counted.Add(pane);
            }
            return Counted.Count;
        }

        public float CutoffFor(int panes) => panes <= 0 ? OpenCutoff : panes == 1 ? onePaneCutoff : twoPaneCutoff;

        void Update()
        {
            if (listener == null || !listener.isActiveAndEnabled)
            {
                listener = FindAnyObjectByType<AudioListener>();
                if (listener == null)
                    return;
            }
            if (Time.unscaledTime >= nextScanAt)
            {
                nextScanAt = Time.unscaledTime + rescanInterval;
                Rescan();
            }

            Vector3 ear = listener.transform.position;
            float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (entry.Source == null || entry.Filter == null)
                {
                    stale.Add(pair.Key);
                    continue;
                }
                Vector3 at = entry.Source.transform.position;
                int panes = (at - ear).sqrMagnitude > maxDistance * maxDistance
                    ? 0
                    : CountPanesBetween(ear, at, entry.Source.GetComponentInParent<GlassPane>());
                // Blend in log space so the sweep sounds even.
                float current = Mathf.Log(entry.Filter.cutoffFrequency);
                float target = Mathf.Log(CutoffFor(panes));
                entry.Filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(current, target, blend));
                entry.Filter.enabled = entry.Filter.cutoffFrequency < OpenCutoff * 0.9f;
            }
            foreach (var key in stale)
                entries.Remove(key);
            stale.Clear();
        }

        void Rescan()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source.spatialBlend < 0.5f || entries.ContainsKey(source.gameObject))
                    continue;
                var filter = source.GetComponent<AudioLowPassFilter>();
                if (filter == null)
                    filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = OpenCutoff;
                filter.lowpassResonanceQ = 1f;
                filter.enabled = false;
                entries[source.gameObject] = new Entry { Source = source, Filter = filter };
            }
        }
    }
}
