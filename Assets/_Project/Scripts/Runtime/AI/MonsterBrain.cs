using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorGame.AI
{
    /// <summary>
    /// The monster's decision-making: a small state machine with four states.
    ///
    ///   Patrol      walk the patrol route, pausing at each point
    ///     -> Investigate   on hearing a noise, or half-noticing a target
    ///     -> Chase         once fully aware of a target (seen for long enough, or very close)
    ///   Investigate walk to the noise or glimpse, search around it for a few seconds
    ///     -> Chase         on seeing a target
    ///     -> Patrol        when the search finds nothing
    ///   Chase       run at the target, tracking where it was last seen or heard
    ///     -> Attack        when within grabbing range and in sight
    ///     -> Investigate   when the target has been out of sight for a few seconds
    ///   Attack      wind up, strike if still in reach, recover
    ///     -> Chase         if the target survived
    ///
    /// Senses: sight (a cone that reaches far for lit targets and short for targets in the dark,
    /// blocked by walls, plus a small all-round "feel" range) and hearing (<see cref="Noise"/> events,
    /// muffled by walls). Every value is on this component so it can be tuned in the Inspector.
    /// Movement uses a NavMeshAgent on the NavMesh baked by <see cref="RuntimeNavMesh"/>.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public class MonsterBrain : MonoBehaviour
    {
        static readonly List<MonsterBrain> Registered = new List<MonsterBrain>();

        /// <summary>Every active monster (used by the debug readout).</summary>
        public static IReadOnlyList<MonsterBrain> All => Registered;

        [SerializeField] string displayName = "The Foreman";

        [Header("Movement (metres per second)")]
        [Tooltip("The player walks at 3.5 and sprints at 6.")]
        [SerializeField] float patrolSpeed = 1.6f;
        [SerializeField] float investigateSpeed = 2.8f;
        [SerializeField] float chaseSpeed = 5.2f;
        [SerializeField] float acceleration = 20f;
        [Tooltip("Degrees per second.")]
        [SerializeField] float turnSpeed = 420f;
        [SerializeField] float bodyRadius = 0.45f;
        [SerializeField] float bodyHeight = 2.4f;

        [Header("Patrol")]
        [SerializeField] Transform[] patrolPoints = Array.Empty<Transform>();
        [SerializeField] bool randomPatrolOrder = true;
        [Tooltip("Seconds to stand at each patrol point (random between min and max).")]
        [SerializeField] Vector2 patrolPause = new Vector2(1.5f, 4f);

        [Header("Sight")]
        [SerializeField] float eyeHeight = 2.1f;
        [Tooltip("Width of the view cone in degrees.")]
        [SerializeField] float fieldOfView = 110f;
        [Tooltip("How far it sees a target whose flashlight is on or who stands in lamplight.")]
        [SerializeField] float litSightRange = 22f;
        [Tooltip("How far it sees a target in the dark.")]
        [SerializeField] float darkSightRange = 7f;
        [Tooltip("Within this distance it notices a target in any direction, instantly (if nothing is in between).")]
        [SerializeField] float nearSenseRange = 2.5f;
        [Tooltip("Seconds of steady sight needed to notice a target at middle distance. Closer is faster, farther is slower.")]
        [SerializeField] float noticeTime = 0.8f;
        [Tooltip("Awareness (0-1) at which it walks over to look at a half-seen target.")]
        [Range(0f, 1f)] [SerializeField] float suspicionThreshold = 0.35f;
        [Tooltip("Awareness lost per second while it can't see anyone.")]
        [SerializeField] float awarenessDecay = 0.3f;
        [Tooltip("Layers that block sight and sound.")]
        [SerializeField] LayerMask blockingLayers = Physics.DefaultRaycastLayers;

        [Header("Hearing")]
        [Tooltip("Multiplies how far every noise carries for this monster.")]
        [SerializeField] float hearingMultiplier = 1f;
        [Tooltip("Noise through a wall carries this fraction of its normal distance.")]
        [Range(0f, 1f)] [SerializeField] float wallMuffle = 0.5f;

        [Header("Chase")]
        [Tooltip("Seconds without seeing or hearing the target before it gives up and searches.")]
        [SerializeField] float loseTargetTime = 4f;

        [Header("Investigate")]
        [Tooltip("Seconds spent looking around once it reaches the spot.")]
        [SerializeField] float searchTime = 5f;
        [Tooltip("Gives up on an investigation that takes longer than this (e.g. unreachable spot).")]
        [SerializeField] float investigateTimeout = 30f;

        [Header("Attack")]
        [Tooltip("Starts an attack when the target is this close (horizontal distance between centres).")]
        [SerializeField] float attackRange = 1.7f;
        [Tooltip("The strike lands if the target is still within this distance after the wind-up.")]
        [SerializeField] float attackReach = 2.3f;
        [SerializeField] float attackWindUp = 0.5f;
        [SerializeField] float attackRecover = 1.2f;
        [Tooltip("Player health is 100, so 100 kills in one grab.")]
        [SerializeField] float attackDamage = 100f;

        [Header("Debug")]
        [Tooltip("Seconds between sight checks. Sight raycasts are the most expensive part of the brain.")]
        [SerializeField] float perceptionInterval = 0.1f;
        [SerializeField] bool logStateChanges = true;

        NavMeshAgent agent;
        float stateEnteredAt;
        float lastPerceptionAt;
        float nextRepathAt;
        int destinationSetFrame = -1;

        int patrolIndex = -1;
        float patrolWaitUntil = -1f;

        bool investigateArrived;
        float searchUntil;
        float searchTurnSign = 1f;
        float nextSearchTurnFlipAt;

        float lastSeenOrHeardAt;
        DetectableTarget visibleTarget;
        float visibleDistance;

        bool attackStruck;

        public string DisplayName => displayName;
        public MonsterState State { get; private set; } = MonsterState.Patrol;
        /// <summary>0 = unaware, 1 = fully aware (chasing). Shown in the F3 readout.</summary>
        public float Awareness { get; private set; }
        /// <summary>The target being chased or attacked, if any.</summary>
        public DetectableTarget Target { get; private set; }
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Vector3 InvestigatePoint { get; private set; }
        public float TimeInState => Time.time - stateEnteredAt;
        /// <summary>The noise most recently heard (for the debug readout).</summary>
        public NoiseEvent? LastHeard { get; private set; }
        public bool IsSeeingTarget => visibleTarget != null;
        public NavMeshAgent Agent => agent;
        public float ChaseSpeed => chaseSpeed;
        public float LitSightRange => litSightRange;
        public float DarkSightRange => darkSightRange;

        /// <summary>Raised with (previous, next) whenever the state changes.</summary>
        public event Action<MonsterState, MonsterState> StateChanged;
        /// <summary>Raised when a strike lands.</summary>
        public event Action<DetectableTarget> Struck;
        /// <summary>Raised when it starts a wind-up (for sound and animation).</summary>
        public event Action AttackStarted;
        /// <summary>Raised for each noise it actually heard.</summary>
        public event Action<NoiseEvent> Heard;

        Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.radius = bodyRadius;
            agent.height = bodyHeight;
            agent.baseOffset = 0f;
            agent.acceleration = acceleration;
            agent.angularSpeed = turnSpeed;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.speed = patrolSpeed;
        }

        void OnEnable()
        {
            Registered.Add(this);
            Noise.Emitted += OnNoise;
        }

        void OnDisable()
        {
            Registered.Remove(this);
            Noise.Emitted -= OnNoise;
        }

        void Start()
        {
            RuntimeNavMesh.EnsureAllBuilt();
            if (!agent.enabled)
                agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[Horror Game] {displayName} is not on a NavMesh, so it can't move. " +
                               "Is there a RuntimeNavMesh object covering it?", this);
                enabled = false;
                return;
            }
            EnterPatrol();
        }

        /// <summary>Replaces the patrol route (used by scene builders and tests).</summary>
        public void SetPatrolRoute(Transform[] points, bool randomOrder)
        {
            patrolPoints = points ?? Array.Empty<Transform>();
            randomPatrolOrder = randomOrder;
            patrolIndex = -1;
            if (State == MonsterState.Patrol && agent != null && agent.isOnNavMesh)
                EnterPatrol();
        }

        void Update()
        {
            if (!agent.isOnNavMesh)
                return;

            if (Time.time - lastPerceptionAt >= perceptionInterval)
            {
                float dt = lastPerceptionAt <= 0f ? perceptionInterval : Time.time - lastPerceptionAt;
                lastPerceptionAt = Time.time;
                Perceive(dt);
            }

            switch (State)
            {
                case MonsterState.Patrol: UpdatePatrol(); break;
                case MonsterState.Investigate: UpdateInvestigate(); break;
                case MonsterState.Chase: UpdateChase(); break;
                case MonsterState.Attack: UpdateAttack(); break;
            }
        }

        // ------------------------------------------------------------------ senses

        void Perceive(float dt)
        {
            visibleTarget = null;
            visibleDistance = float.MaxValue;
            foreach (var target in DetectableTarget.All)
            {
                if (!target.IsAlive || !CanSee(target, out float distance))
                    continue;
                // Keep chasing the current target if it's still visible, otherwise take the closest.
                if (target == Target && (State == MonsterState.Chase || State == MonsterState.Attack))
                {
                    visibleTarget = target;
                    visibleDistance = distance;
                    break;
                }
                if (distance < visibleDistance)
                {
                    visibleTarget = target;
                    visibleDistance = distance;
                }
            }

            if (visibleTarget == null)
            {
                if (State != MonsterState.Chase && State != MonsterState.Attack)
                    Awareness = Mathf.Max(0f, Awareness - awarenessDecay * dt);
                return;
            }

            LastKnownTargetPosition = visibleTarget.transform.position;
            lastSeenOrHeardAt = Time.time;

            if (State == MonsterState.Chase || State == MonsterState.Attack)
            {
                Awareness = 1f;
                if (State == MonsterState.Chase)
                    Target = visibleTarget; // lost sight of the old target but sees another: go for that one
                return;
            }

            if (FlatDistance(visibleTarget.transform.position) <= nearSenseRange)
                Awareness = 1f;
            else
                Awareness = Mathf.Min(1f, Awareness + dt * NoticeRate(visibleDistance, visibleTarget.IsLit));

            if (Awareness >= 1f)
                EnterChase(visibleTarget);
            else if (Awareness >= suspicionThreshold)
                BeginInvestigate(visibleTarget.transform.position);
        }

        /// <summary>How quickly awareness fills per second at this distance: three times faster up close, half speed at the edge of sight.</summary>
        float NoticeRate(float distance, bool lit)
        {
            float range = lit ? litSightRange : darkSightRange;
            float closeness = 1f - Mathf.Clamp01(distance / range);
            return Mathf.Lerp(0.5f, 3f, closeness) / Mathf.Max(0.05f, noticeTime);
        }

        /// <summary>
        /// True if the target is within sight range (long if lit, short if dark), inside the view cone
        /// (or within the all-round near range), and not hidden behind anything.
        /// </summary>
        public bool CanSee(DetectableTarget target, out float distance)
        {
            Vector3 eye = EyePosition;
            Vector3 toTarget = target.ChestPosition - eye;
            distance = toTarget.magnitude;
            float range = target.IsLit ? litSightRange : darkSightRange;
            if (distance > range)
                return false;

            bool near = FlatDistance(target.transform.position) <= nearSenseRange;
            if (!near)
            {
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                if (Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f)
                    return false;
            }
            return IsClear(eye, target.HeadPosition, target.transform, 0f) ||
                   IsClear(eye, target.ChestPosition, target.transform, 0f);
        }

        /// <summary>
        /// True if nothing but <paramref name="allowed"/> (and its children) blocks the line. A hit within
        /// <paramref name="endTolerance"/> of the end point is ignored: that's the surface the point sits on,
        /// such as the floor under a footstep.
        /// </summary>
        bool IsClear(Vector3 from, Vector3 to, Transform allowed, float endTolerance)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f)
                return true;
            if (!Physics.Raycast(from, direction / length, out var hit, length, blockingLayers, QueryTriggerInteraction.Ignore))
                return true;
            if (hit.collider.transform.IsChildOf(transform))
                return true;
            if (allowed != null && hit.collider.transform.IsChildOf(allowed))
                return true;
            return (hit.point - to).sqrMagnitude <= endTolerance * endTolerance;
        }

        void OnNoise(NoiseEvent noise)
        {
            if (!isActiveAndEnabled || agent == null || !agent.isOnNavMesh)
                return;
            if (noise.Source != null && noise.Source.transform.IsChildOf(transform))
                return;

            float radius = noise.Radius * hearingMultiplier;
            float distance = Vector3.Distance(EyePosition, noise.Position);
            if (distance > radius)
                return;
            var sourceTransform = noise.Source != null ? noise.Source.transform : null;
            if (distance > radius * wallMuffle && !IsClear(EyePosition, noise.Position, sourceTransform, 0.6f))
                return;

            LastHeard = noise;
            Heard?.Invoke(noise);

            switch (State)
            {
                case MonsterState.Patrol:
                case MonsterState.Investigate:
                    BeginInvestigate(noise.Position);
                    break;
                case MonsterState.Chase:
                    // Hearing the target it's chasing keeps the chase going: running keeps it on your heels.
                    if (Target != null && sourceTransform != null && sourceTransform.IsChildOf(Target.transform))
                    {
                        LastKnownTargetPosition = noise.Position;
                        lastSeenOrHeardAt = Time.time;
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ states

        void SetState(MonsterState next)
        {
            var previous = State;
            State = next;
            stateEnteredAt = Time.time;
            agent.isStopped = next == MonsterState.Attack;
            if (previous == next)
                return;
            if (logStateChanges)
                Debug.Log($"[Horror Game] {displayName}: {previous} -> {next}", this);
            StateChanged?.Invoke(previous, next);
        }

        void EnterPatrol()
        {
            Target = null;
            agent.speed = patrolSpeed;
            SetState(MonsterState.Patrol);
            patrolWaitUntil = -1f;
            patrolIndex = NearestPatrolIndex();
            GoToPatrolPoint();
        }

        void UpdatePatrol()
        {
            if (patrolPoints.Length == 0)
                return;
            if (patrolWaitUntil >= 0f)
            {
                if (Time.time < patrolWaitUntil)
                    return;
                patrolWaitUntil = -1f;
                patrolIndex = NextPatrolIndex();
                GoToPatrolPoint();
                return;
            }
            if (HasArrived())
                patrolWaitUntil = Time.time + UnityEngine.Random.Range(patrolPause.x, patrolPause.y);
        }

        void GoToPatrolPoint()
        {
            if (patrolIndex >= 0 && patrolIndex < patrolPoints.Length && patrolPoints[patrolIndex] != null)
                MoveTo(patrolPoints[patrolIndex].position);
        }

        int NearestPatrolIndex()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                    continue;
                float d = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        int NextPatrolIndex()
        {
            if (patrolPoints.Length <= 1)
                return patrolPoints.Length - 1;
            if (!randomPatrolOrder)
                return (patrolIndex + 1) % patrolPoints.Length;
            int next = UnityEngine.Random.Range(0, patrolPoints.Length - 1);
            return next >= patrolIndex ? next + 1 : next;
        }

        void BeginInvestigate(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out var hit, 3f, NavMesh.AllAreas))
                point = hit.position;
            InvestigatePoint = point;
            investigateArrived = false;
            agent.speed = investigateSpeed;
            MoveTo(point);
            SetState(MonsterState.Investigate); // also restarts the timeout when already investigating
        }

        void UpdateInvestigate()
        {
            if (TimeInState > investigateTimeout)
            {
                EnterPatrol();
                return;
            }
            if (!investigateArrived)
            {
                if (!HasArrived())
                    return;
                investigateArrived = true;
                searchUntil = Time.time + searchTime;
                nextSearchTurnFlipAt = Time.time + 1.2f;
                agent.ResetPath();
            }

            // Search: turn one way, then the other, looking around the spot.
            if (Time.time >= nextSearchTurnFlipAt)
            {
                searchTurnSign = -searchTurnSign;
                nextSearchTurnFlipAt = Time.time + 1.6f;
            }
            transform.Rotate(0f, searchTurnSign * 90f * Time.deltaTime, 0f);
            if (Time.time >= searchUntil)
                EnterPatrol();
        }

        void EnterChase(DetectableTarget target)
        {
            Target = target;
            Awareness = 1f;
            LastKnownTargetPosition = target.transform.position;
            lastSeenOrHeardAt = Time.time;
            agent.speed = chaseSpeed;
            SetState(MonsterState.Chase);
            MoveTo(LastKnownTargetPosition);
            nextRepathAt = Time.time + 0.15f;
        }

        void UpdateChase()
        {
            if (Target == null || !Target.isActiveAndEnabled || !Target.IsAlive ||
                Time.time - lastSeenOrHeardAt > loseTargetTime)
            {
                LoseTarget();
                return;
            }

            // LastKnownTargetPosition is refreshed by Perceive (sight) and OnNoise (hearing), never
            // read straight from the target, so it can't track someone it can't actually sense.
            bool seeing = visibleTarget == Target;
            if (Time.time >= nextRepathAt)
            {
                MoveTo(LastKnownTargetPosition);
                nextRepathAt = Time.time + 0.15f;
            }

            if (seeing && FlatDistance(Target.transform.position) <= attackRange)
                EnterAttack();
        }

        void LoseTarget()
        {
            Target = null;
            Awareness = suspicionThreshold; // still on edge
            BeginInvestigate(LastKnownTargetPosition);
        }

        void EnterAttack()
        {
            attackStruck = false;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            SetState(MonsterState.Attack);
            AttackStarted?.Invoke();
        }

        void UpdateAttack()
        {
            if (Target != null)
                FaceTowards(Target.transform.position);

            if (!attackStruck && TimeInState >= attackWindUp)
            {
                attackStruck = true;
                if (Target != null && Target.IsAlive && FlatDistance(Target.transform.position) <= attackReach &&
                    CanSee(Target, out _))
                {
                    if (Target.Health != null)
                        Target.Health.TakeHit(attackDamage, gameObject);
                    Struck?.Invoke(Target);
                }
            }

            if (TimeInState < attackWindUp + attackRecover)
                return;
            if (Target != null && Target.isActiveAndEnabled && Target.IsAlive)
            {
                agent.isStopped = false;
                EnterChase(Target);
            }
            else
            {
                EnterPatrol();
            }
        }

        // ------------------------------------------------------------------ helpers

        void MoveTo(Vector3 destination)
        {
            agent.SetDestination(destination);
            destinationSetFrame = Time.frameCount;
        }

        bool HasArrived()
        {
            // remainingDistance can still describe the old path on the frame a new one is requested.
            if (agent.pathPending || Time.frameCount == destinationSetFrame)
                return false;
            return agent.remainingDistance <= agent.stoppingDistance + 0.25f;
        }

        float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;
            var look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            DrawCone(eye, litSightRange, new Color(1f, 0.9f, 0.3f, 0.5f));
            DrawCone(eye, darkSightRange, new Color(0.4f, 0.6f, 1f, 0.8f));
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            DrawCircle(transform.position + Vector3.up * 0.1f, nearSenseRange);

            if (!Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                foreach (var point in patrolPoints)
                    if (point != null)
                        Gizmos.DrawWireSphere(point.position, 0.4f);
                return;
            }
            Gizmos.color = State switch
            {
                MonsterState.Patrol => Color.green,
                MonsterState.Investigate => Color.yellow,
                MonsterState.Chase => new Color(1f, 0.5f, 0f),
                _ => Color.red,
            };
            Gizmos.DrawSphere(transform.position + Vector3.up * (bodyHeight + 0.4f), 0.2f);
            if (agent != null && agent.hasPath)
            {
                var corners = agent.path.corners;
                for (int i = 1; i < corners.Length; i++)
                    Gizmos.DrawLine(corners[i - 1], corners[i]);
            }
        }

        void DrawCone(Vector3 eye, float range, Color color)
        {
            Gizmos.color = color;
            float half = fieldOfView * 0.5f;
            Vector3 left = Quaternion.Euler(0f, -half, 0f) * transform.forward * range;
            Vector3 right = Quaternion.Euler(0f, half, 0f) * transform.forward * range;
            Gizmos.DrawLine(eye, eye + left);
            Gizmos.DrawLine(eye, eye + right);
            const int steps = 16;
            Vector3 previous = eye + left;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 next = eye + Quaternion.Euler(0f, -half + fieldOfView * i / steps, 0f) * transform.forward * range;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        static void DrawCircle(Vector3 centre, float radius)
        {
            const int steps = 24;
            Vector3 previous = centre + Vector3.forward * radius;
            for (int i = 1; i <= steps; i++)
            {
                float a = i * Mathf.PI * 2f / steps;
                Vector3 next = centre + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registered.Clear();
    }
}
