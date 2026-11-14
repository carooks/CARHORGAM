using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorGame.Painting.Strokes
{
    /// <summary>What a group of brushstrokes is, which decides how the stroke shader lays it down.</summary>
    public enum StrokeKind
    {
        /// <summary>Flat daubs lying on a surface (ground, walls, heads).</summary>
        Surface = 0,
        /// <summary>Upright blades standing on the ground, turned toward the viewer.</summary>
        Grass = 1,
        /// <summary>Huge strokes on a dome that follows the camera, drawn behind everything.</summary>
        Sky = 2,
        /// <summary>Like Surface, but the strokes never settle: they crawl and stay wet. The entity.</summary>
        Restless = 3,
        /// <summary>Wet black drips left on the ground where the entity walked.</summary>
        Trail = 4,
    }

    public enum StrokeShapeType { Box = 0, Sphere = 1, Cylinder = 2, Triangles = 3 }

    /// <summary>
    /// One primitive the strokes are scattered over, in the source's space. Boxes, spheres and
    /// cylinders are Unity's primitive meshes (sampled exactly, so no mesh has to be readable in a
    /// build); anything else is kept as a triangle list.
    /// </summary>
    [Serializable]
    public struct StrokeShape
    {
        public StrokeShapeType type;
        public Matrix4x4 matrix;
        /// <summary>Small features (eyes, a mouth) get more strokes and sit on top of the rest.</summary>
        public bool detail;
        public int triangleStart;
        public int triangleCount;
    }

    /// <summary>
    /// Covers this object (and children that have no source of their own) in brushstrokes, or
    /// scatters grass or a painted sky. The editor bakes the shapes with <see cref="SetShapes"/>; at
    /// runtime <see cref="BrushStrokeWorld"/> turns every source into strokes once and draws them all
    /// on the GPU.
    ///
    /// A <see cref="moving"/> source keeps its strokes in its own space and they follow its transform
    /// (the choir's heads, the entity's limbs); its strokes vanish while <see cref="visibility"/> is
    /// hidden or switched off. Everything else is baked into world space.
    /// </summary>
    public class BrushStrokeSource : MonoBehaviour
    {
        [SerializeField] public StrokeKind kind = StrokeKind.Surface;
        [Tooltip("Strokes follow this transform (heads, limbs). Off = baked in place.")]
        [SerializeField] public bool moving;
        [Tooltip("While moving: hide the strokes whenever this renderer is off or inactive.")]
        [SerializeField] public Renderer visibility;

        [Header("Strokes")]
        [Tooltip("Strokes per square metre (Sky: total number of strokes).")]
        [SerializeField] public float density = 20f;
        [Tooltip("Full stroke length range in metres (Grass: blade height).")]
        [SerializeField] public Vector2 length = new Vector2(0.25f, 0.45f);
        [Tooltip("Full stroke width range in metres.")]
        [SerializeField] public Vector2 width = new Vector2(0.08f, 0.13f);
        [Tooltip("How far strokes float above the surface, in metres.")]
        [SerializeField] public float lift = 0.008f;
        [Tooltip("Skip faces pointing at the ground (the underside of the ground, of roofs). Off for ceilings.")]
        [SerializeField] public bool skipDownFacing = true;

        [Header("Colour")]
        [Tooltip("1 = take each stroke's colour from the lit world under it. 0 = use the colours below.")]
        [Range(0f, 1f)] [SerializeField] public float sampleWorld = 1f;
        [SerializeField] public Color color = Color.white;
        [Tooltip("Strokes pick a colour between Color and this.")]
        [SerializeField] public Color color2 = Color.white;
        [Range(0f, 1f)] [SerializeField] public float colorJitter = 0.15f;

        [Header("Grass")]
        [Tooltip("Half size of the grass area on the local X/Z plane.")]
        [SerializeField] public Vector2 grassHalfSize = new Vector2(10f, 10f);
        [Tooltip("Circles kept clear of grass: x, z (local), radius.")]
        [SerializeField] public Vector3[] grassClearings = Array.Empty<Vector3>();

        [Header("Sky")]
        [SerializeField] public float skyRadius = 150f;
        [SerializeField] public Color skyZenith = new Color(0.1f, 0.07f, 0.09f);
        [SerializeField] public Color skyGlow = new Color(0.85f, 0.5f, 0.22f);
        [Tooltip("Direction toward the sun's glow in the sky.")]
        [SerializeField] public Vector3 skyGlowDirection = new Vector3(0.3f, 0.15f, 0.95f);

        [Header("Baked by the editor")]
        [SerializeField] List<StrokeShape> shapes = new List<StrokeShape>();
        [SerializeField] Vector3[] triangles = Array.Empty<Vector3>();

        public IReadOnlyList<StrokeShape> Shapes => shapes;
        public Vector3[] Triangles => triangles;

        /// <summary>Replaces the baked shapes. <paramref name="tris"/> holds 3 corners per triangle.</summary>
        public void SetShapes(List<StrokeShape> newShapes, Vector3[] tris)
        {
            shapes = newShapes ?? new List<StrokeShape>();
            triangles = tris ?? Array.Empty<Vector3>();
        }

        /// <summary>Whether a moving source's strokes are shown right now.</summary>
        public bool IsShown
        {
            get
            {
                if (!isActiveAndEnabled)
                    return false;
                if (visibility == null)
                    return true;
                return visibility.enabled && visibility.gameObject.activeInHierarchy;
            }
        }
    }
}
