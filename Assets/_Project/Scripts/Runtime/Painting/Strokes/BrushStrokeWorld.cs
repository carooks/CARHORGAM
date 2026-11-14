#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorGame.Painting.Strokes
{
    /// <summary>
    /// Paints the inside of the painting with real brushstrokes: hundreds of thousands of small
    /// textured quads lying on every surface, grass blades, and a painted sky, all drawn by the GPU
    /// in one call.
    ///
    /// On Start it gathers every <see cref="BrushStrokeSource"/> under it and generates the strokes
    /// on a worker thread (the player is still in the parlor). Each frame, while the player is inside
    /// (its <see cref="PaintAttention"/> is on), a compute shader throws away strokes that are off
    /// screen or lost in the fog, thins out distant ones (they are drawn bigger to make up for it),
    /// and the survivors are drawn. Strokes take their colour from the lit world under them, so
    /// lights, shadows and fog all still work; the shader reads <see cref="PaintAttention"/> so the
    /// world paints itself in, stroke by stroke, where you look.
    ///
    /// F7 cycles stroke density (full, 60%, 35%) to check the frame rate.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BrushStrokeWorld : MonoBehaviour
    {
        public const int IdentityBone = 0;
        public const int HiddenBone = 1;
        public const int SkyBone = 2;
        const int FirstMovingBone = 3;

        static readonly int StrokesId = Shader.PropertyToID("_Strokes");
        static readonly int VisibleId = Shader.PropertyToID("_Visible");
        static readonly int VisibleOutId = Shader.PropertyToID("_VisibleOut");
        static readonly int BonesId = Shader.PropertyToID("_Bones");
        static readonly int PlanesId = Shader.PropertyToID("_Planes");
        static readonly int CamPosId = Shader.PropertyToID("_CullCameraPos");
        static readonly int CountId = Shader.PropertyToID("_StrokeCount");
        static readonly int MaxDistanceId = Shader.PropertyToID("_StrokeMaxDistance");
        static readonly int LodNearId = Shader.PropertyToID("_StrokeLodNear");
        static readonly int LodMinId = Shader.PropertyToID("_StrokeLodMin");
        static readonly int AmbientId = Shader.PropertyToID("_StrokeAmbient");

        [SerializeField] Shader strokeShader;
        [SerializeField] ComputeShader cullShader;
        [Tooltip("Strokes are drawn for this component's viewer, only while it is enabled (the player is inside).")]
        [SerializeField] PaintAttention attention;
        [Tooltip("Strokes further than this are lost in the fog and not drawn (the sky always is).")]
        [SerializeField] float maxDistance = 95f;
        [Tooltip("Within this distance every stroke is drawn; beyond it they thin out with the square of distance.")]
        [SerializeField] float lodNear = 14f;
        [Tooltip("Never thin out below this share of the strokes.")]
        [SerializeField] float lodMin = 0.05f;
        [Tooltip("Room for drips the entity leaves behind (oldest are painted over).")]
        [SerializeField] int trailCapacity = 4096;

        static readonly float[] QualitySteps = { 1f, 0.6f, 0.35f };
        static int quality;

        readonly List<BrushStrokeSource> movingSources = new List<BrushStrokeSource>();
        readonly Plane[] planes = new Plane[6];
        readonly Vector4[] planeVectors = new Vector4[4];
        readonly GpuStroke[] one = new GpuStroke[1];
        readonly System.Random dripRandom = new System.Random(7);

        Task<GpuStroke[]> generating;
        GraphicsBuffer strokes, visible, bones, args;
        Matrix4x4[] boneMatrices;
        Material material;
        MaterialPropertyBlock props;
        int kernel = -1;
        int staticCount;
        int nextDrip;
        bool started;
        Camera colorTextureCamera;

        /// <summary>True once the strokes are on the GPU and can be drawn.</summary>
        public bool Ready { get; private set; }
        /// <summary>Generated strokes, not counting room for drips.</summary>
        public int StrokeCount => staticCount;

        public PaintAttention Attention
        {
            get => attention;
            set => attention = value;
        }

        void Start() => Begin();

        /// <summary>Gathers the sources and starts generating strokes in the background. Safe to call twice.</summary>
        public void Begin()
        {
            if (started)
                return;
            started = true;
            if (!SystemInfo.supportsComputeShaders || cullShader == null || strokeShader == null)
            {
                Debug.LogWarning("[Horror Game] Brushstrokes are off: no compute shader support, or the stroke shaders are missing.", this);
                enabled = false;
                return;
            }

            var jobs = new List<StrokeJob>();
            var sources = GetComponentsInChildren<BrushStrokeSource>(true);
            movingSources.Clear();
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                int bone = IdentityBone;
                if (source.kind == StrokeKind.Sky)
                    bone = SkyBone;
                else if (source.moving)
                {
                    bone = FirstMovingBone + movingSources.Count;
                    movingSources.Add(source);
                }
                jobs.Add(StrokeJob.From(source, bone, 1000 + i * 7919));
            }
            boneMatrices = new Matrix4x4[FirstMovingBone + movingSources.Count];
            generating = Task.Run(() => StrokeGenerator.Generate(jobs));
        }

        void Update()
        {
            if (generating != null && generating.IsCompleted)
            {
                if (generating.IsFaulted)
                    Debug.LogException(generating.Exception, this);
                else
                    Upload(generating.Result);
                generating = null;
            }
            CycleQuality();
        }

        void Upload(GpuStroke[] generated)
        {
            staticCount = generated.Length;
            int total = staticCount + Mathf.Max(0, trailCapacity);
            if (total == 0)
                return;

            strokes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, total, GpuStroke.Stride);
            strokes.SetData(generated);
            if (trailCapacity > 0)
            {
                // Drip slots start hidden.
                var empty = new GpuStroke[trailCapacity];
                for (int i = 0; i < empty.Length; i++)
                    empty[i].misc = GpuStroke.PackMisc(HiddenBone, StrokeKind.Trail, 0f);
                strokes.SetData(empty, 0, staticCount, trailCapacity);
            }
            visible = new GraphicsBuffer(GraphicsBuffer.Target.Append, total, sizeof(uint));
            bones = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boneMatrices.Length, 64);
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawArgs.size);
            args.SetData(new[] { new GraphicsBuffer.IndirectDrawArgs { vertexCountPerInstance = 6, instanceCount = 0 } });

            material = new Material(strokeShader) { name = "BrushStrokes (runtime)", hideFlags = HideFlags.HideAndDontSave };
            props = new MaterialPropertyBlock();
            props.SetBuffer(StrokesId, strokes);
            props.SetBuffer(VisibleId, visible);
            props.SetBuffer(BonesId, bones);
            kernel = cullShader.FindKernel("Cull");
            Ready = true;
            Debug.Log($"[Horror Game] Painted {staticCount:N0} brushstrokes.");
        }

        void LateUpdate()
        {
            if (!Ready || attention == null || !attention.isActiveAndEnabled)
                return;
            var cam = attention.Viewer;
            if (cam == null)
                return;
            EnsureColorTexture(cam);
            UpdateBones(cam);
            Cull(cam);
            Draw(cam);
        }

        /// <summary>Strokes take their colour from the rendered world, so the camera must keep a copy of it.</summary>
        void EnsureColorTexture(Camera cam)
        {
            if (colorTextureCamera == cam)
                return;
            colorTextureCamera = cam;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
                data.requiresColorOption = CameraOverrideOption.On;
        }

        void UpdateBones(Camera cam)
        {
            boneMatrices[IdentityBone] = Matrix4x4.identity;
            boneMatrices[HiddenBone] = Matrix4x4.zero;
            boneMatrices[SkyBone] = Matrix4x4.Translate(cam.transform.position);
            for (int i = 0; i < movingSources.Count; i++)
            {
                var source = movingSources[i];
                boneMatrices[FirstMovingBone + i] = source != null && source.IsShown ? source.transform.localToWorldMatrix : Matrix4x4.zero;
            }
            bones.SetData(boneMatrices);
        }

        float LodNear => lodNear * QualitySteps[quality];

        void Cull(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            // Left, right, bottom, top. A little slack so strokes at the edge of the view don't pop.
            for (int i = 0; i < 4; i++)
                planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance + 1.5f);
            int total = staticCount + Mathf.Max(0, trailCapacity);
            cullShader.SetBuffer(kernel, StrokesId, strokes);
            cullShader.SetBuffer(kernel, BonesId, bones);
            cullShader.SetBuffer(kernel, VisibleOutId, visible);
            cullShader.SetVectorArray(PlanesId, planeVectors);
            cullShader.SetVector(CamPosId, cam.transform.position);
            cullShader.SetInt(CountId, total);
            cullShader.SetFloat(MaxDistanceId, maxDistance);
            cullShader.SetFloat(LodNearId, LodNear);
            cullShader.SetFloat(LodMinId, lodMin);
            visible.SetCounterValue(0);
            cullShader.Dispatch(kernel, Mathf.CeilToInt(total / 256f), 1, 1);
            GraphicsBuffer.CopyCount(visible, args, sizeof(uint));
        }

        void Draw(Camera cam)
        {
            props.SetFloat(LodNearId, LodNear);
            props.SetFloat(LodMinId, lodMin);
            props.SetColor(AmbientId, RenderSettings.ambientLight);
            var rp = new RenderParams(material)
            {
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 4000f),
                matProps = props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                camera = cam,
                layer = gameObject.layer,
            };
            Graphics.RenderPrimitivesIndirect(rp, MeshTopology.Triangles, args);
        }

        /// <summary>
        /// Leaves a wet black drip on the ground (where the entity walked). The oldest drips are
        /// painted over once there are too many.
        /// </summary>
        public void AddDrip(Vector3 position, Vector3 direction, float size = 1f)
        {
            if (!Ready || trailCapacity <= 0)
                return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f)
                direction = Vector3.forward;
            float angle = ((float)dripRandom.NextDouble() - 0.5f) * 1.2f;
            direction = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * direction.normalized;
            float len = Mathf.Lerp(0.25f, 0.55f, (float)dripRandom.NextDouble()) * size;
            float wid = Mathf.Lerp(0.07f, 0.14f, (float)dripRandom.NextDouble()) * size;
            float shade = Mathf.Lerp(0.02f, 0.07f, (float)dripRandom.NextDouble());
            one[0] = new GpuStroke
            {
                position = position,
                normal = GpuStroke.PackDirection(Vector3.up),
                tangent = GpuStroke.PackDirection(direction),
                color = GpuStroke.PackColor(new Color(shade * 1.6f, shade * 0.5f, shade * 0.45f), false),
                size = GpuStroke.PackSize(len * 0.5f, wid * 0.5f),
                misc = GpuStroke.PackMisc(IdentityBone, StrokeKind.Trail, 0.01f),
            };
            strokes.SetData(one, 0, staticCount + nextDrip, 1);
            nextDrip = (nextDrip + 1) % trailCapacity;
        }

        void CycleQuality()
        {
            if (attention == null || !attention.isActiveAndEnabled)
                return;
            bool pressed;
#if ENABLE_INPUT_SYSTEM
            pressed = Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame;
#else
            pressed = Input.GetKeyDown(KeyCode.F7);
#endif
            if (!pressed)
                return;
            quality = (quality + 1) % QualitySteps.Length;
            var realm = GetComponentInParent<PaintedRealm>();
            if (realm != null)
                realm.ShowSubtitle($"Brushstrokes: {Mathf.RoundToInt(QualitySteps[quality] * 100f)}%", 1.5f);
        }

        void OnDestroy()
        {
            strokes?.Release();
            visible?.Release();
            bones?.Release();
            args?.Release();
            if (material != null)
                Destroy(material);
        }
    }
}
