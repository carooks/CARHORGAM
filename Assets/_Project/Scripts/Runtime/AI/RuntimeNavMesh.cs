using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.AI
{
    /// <summary>
    /// Bakes a NavMesh (the walkable-area map monsters path-find on) for a box-shaped region when the
    /// scene starts. Baking at runtime from colliders means no baked NavMesh asset to keep in sync:
    /// change the facility layout, rebuild the scene, and the monster can walk the new layout.
    /// Uses only Unity's built-in AI module, no extra package.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class RuntimeNavMesh : MonoBehaviour
    {
        static readonly List<RuntimeNavMesh> Instances = new List<RuntimeNavMesh>();

        [Tooltip("Size of the baked region, centred on this object.")]
        [SerializeField] Vector3 size = new Vector3(60f, 10f, 60f);
        [Tooltip("Radius of the agent the NavMesh is baked for. Smaller lets monsters squeeze through tighter gaps.")]
        [SerializeField] float agentRadius = 0.4f;
        [SerializeField] float agentHeight = 2.2f;
        [SerializeField] float maxStepHeight = 0.45f;
        [SerializeField] float maxSlope = 40f;

        NavMeshDataInstance instance;

        public bool IsBuilt { get; private set; }
        public Bounds WorldBounds => new Bounds(transform.position, size);

        public Vector3 Size
        {
            get => size;
            set => size = value;
        }

        void Awake() => Build();

        void OnEnable() => Instances.Add(this);

        void OnDisable()
        {
            Instances.Remove(this);
            if (instance.valid)
                instance.Remove();
            IsBuilt = false;
        }

        /// <summary>Builds every NavMesh region in the scene that hasn't been built yet.</summary>
        public static void EnsureAllBuilt()
        {
            foreach (var navMesh in Instances)
                if (!navMesh.IsBuilt)
                    navMesh.Build();
        }

        /// <summary>(Re)builds this region from the colliders inside it.</summary>
        public void Build()
        {
            if (instance.valid)
                instance.Remove();

            var bounds = WorldBounds;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(IsMovingThing);

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentClimb = maxStepHeight;
            settings.agentSlope = maxSlope;

            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError("[Horror Game] NavMesh bake failed for " + name + ".", this);
                return;
            }
            instance = NavMesh.AddNavMeshData(data);
            IsBuilt = instance.valid;
        }

        /// <summary>Players, monsters, triggers and physics props must not become part of the walkable map.</summary>
        static bool IsMovingThing(NavMeshBuildSource source)
        {
            var component = source.component;
            if (component == null)
                return false;
            if (component is Collider collider && collider.isTrigger)
                return true;
            return component.GetComponentInParent<CharacterController>() != null
                   || component.GetComponentInParent<NavMeshAgent>() != null
                   || component.GetComponentInParent<DetectableTarget>() != null
                   || component.GetComponentInParent<MonsterBrain>() != null
                   || (component.GetComponentInParent<Rigidbody>() is Rigidbody body && !body.isKinematic);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instances.Clear();
    }
}
