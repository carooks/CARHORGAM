using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The core rule of the painting: the world only finishes painting itself where you look.
    /// Keeps a ring of "how finished" values (0 = wet, unfinished strokes, 1 = finished paint) for
    /// every compass direction around the viewer. Directions inside the camera's view paint in over
    /// about half a second; directions you turn away from slowly dissolve back into wet paint.
    ///
    /// The paint filter shader reads the ring (as a global float array) to decide how finished each
    /// pixel looks, and the entity reads it to decide whether it may move. One ring per viewer; for
    /// co-op each player would get their own and the entity would check all of them.
    /// </summary>
    public class PaintAttention : MonoBehaviour
    {
        /// <summary>Number of compass directions tracked. Must match PAINT_BINS in the shader.</summary>
        public const int Bins = 64;

        static readonly int AttentionId = Shader.PropertyToID("_PaintAttention");
        static readonly int CamRightId = Shader.PropertyToID("_PaintCamRight");
        static readonly int CamUpId = Shader.PropertyToID("_PaintCamUp");
        static readonly int CamForwardId = Shader.PropertyToID("_PaintCamForward");
        static readonly int TanHalfFovId = Shader.PropertyToID("_PaintTanHalfFov");

        [Tooltip("Camera that does the looking. Found on the player when the realm is entered.")]
        [SerializeField] Camera viewer;
        [Tooltip("Seconds for a direction to finish painting once it is in view.")]
        [SerializeField] float paintInTime = 0.6f;
        [Tooltip("Seconds for a direction to dissolve back into wet paint once it is out of view.")]
        [SerializeField] float dissolveTime = 2.5f;
        [Tooltip("Extra degrees on each side of the view that also count as looked at.")]
        [SerializeField] float viewMargin = 4f;

        readonly float[] values = new float[Bins];

        public Camera Viewer
        {
            get => viewer;
            set => viewer = value;
        }

        /// <summary>Everything starts wet: nothing has been looked at yet.</summary>
        public void ResetAll(float value = 0f)
        {
            for (int i = 0; i < Bins; i++)
                values[i] = value;
            Push();
        }

        /// <summary>How finished the world is in the direction of <paramref name="worldPosition"/> as seen from the viewer (0..1).</summary>
        public float At(Vector3 worldPosition)
        {
            if (viewer == null)
                return 1f;
            Vector3 d = worldPosition - viewer.transform.position;
            return AtYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
        }

        /// <summary>How finished the world is toward a compass yaw in degrees (0 = +Z, 90 = +X).</summary>
        public float AtYaw(float yawDegrees)
        {
            float f = Mathf.Repeat(yawDegrees, 360f) / 360f * Bins;
            int a = Mathf.FloorToInt(f) % Bins;
            int b = (a + 1) % Bins;
            return Mathf.Lerp(values[a], values[b], f - Mathf.Floor(f));
        }

        /// <summary>Half the horizontal field of view of the viewer in degrees.</summary>
        public float HalfHorizontalFov
        {
            get
            {
                if (viewer == null)
                    return 45f;
                float halfV = viewer.fieldOfView * 0.5f * Mathf.Deg2Rad;
                return Mathf.Atan(Mathf.Tan(halfV) * viewer.aspect) * Mathf.Rad2Deg;
            }
        }

        void OnEnable() => ResetAll();

        void Update()
        {
            if (viewer == null)
                return;
            Tick(Time.deltaTime);
            Push();
        }

        /// <summary>Advances painting and dissolving by <paramref name="dt"/> seconds. Public for tests.</summary>
        public void Tick(float dt)
        {
            Vector3 f = viewer.transform.forward;
            float viewYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            float half = HalfHorizontalFov + viewMargin;
            float up = dt / Mathf.Max(0.01f, paintInTime);
            float down = dt / Mathf.Max(0.01f, dissolveTime);
            for (int i = 0; i < Bins; i++)
            {
                float binYaw = (i + 0.5f) * 360f / Bins;
                bool seen = Mathf.Abs(Mathf.DeltaAngle(binYaw, viewYaw)) <= half;
                values[i] = Mathf.Clamp01(values[i] + (seen ? up : -down));
            }
        }

        void Push()
        {
            Shader.SetGlobalFloatArray(AttentionId, values);
            if (viewer == null)
                return;
            var t = viewer.transform;
            Shader.SetGlobalVector(CamRightId, t.right);
            Shader.SetGlobalVector(CamUpId, t.up);
            Shader.SetGlobalVector(CamForwardId, t.forward);
            float tanV = Mathf.Tan(viewer.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shader.SetGlobalVector(TanHalfFovId, new Vector4(tanV * viewer.aspect, tanV, 0f, 0f));
        }
    }
}
