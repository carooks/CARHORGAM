using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The entity's body never animates. It only moves where you are not looking, so it jumps from
    /// one pose to the next while unseen: every time you look back it is standing differently. Hung
    /// forward, lurching, reaching for you, listening with its head on its shoulder, down on all
    /// fours, stretched up to its full height. Its head turns to face you between looks.
    /// </summary>
    public class EntityPoser : MonoBehaviour
    {
        public enum Pose { Hang, Lurch, Reach, Listen, Crawl, Stretch }

        [SerializeField] Transform hips;
        [SerializeField] Transform spine;
        [SerializeField] Transform neck;
        [SerializeField] Transform head;
        [SerializeField] Transform shoulderL;
        [SerializeField] Transform elbowL;
        [SerializeField] Transform handL;
        [SerializeField] Transform shoulderR;
        [SerializeField] Transform elbowR;
        [SerializeField] Transform handR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        /// <summary>Joint angles (Euler, degrees) for one pose, and the hip height.</summary>
        struct PoseData
        {
            public float hipHeight;
            public Vector3 spine, neck, head, shoulderL, elbowL, handL, shoulderR, elbowR, handR, legL, legR;
        }

        // +X on the spine/neck bends forward. On arms and legs (hanging down) -X swings them forward.
        static readonly PoseData[] Poses =
        {
            new PoseData // Hang: hunched, head hanging, arms dangling to the ground.
            {
                hipHeight = 1.5f, spine = new Vector3(35f, 0f, 0f), neck = new Vector3(40f, 0f, 0f), head = new Vector3(-20f, 0f, 0f),
                shoulderL = new Vector3(-10f, 0f, -5f), elbowL = new Vector3(-15f, 0f, 0f), shoulderR = new Vector3(5f, 0f, 5f),
                elbowR = new Vector3(-10f, 0f, 0f), legL = new Vector3(-12f, 0f, 0f), legR = new Vector3(10f, 0f, 0f),
            },
            new PoseData // Lurch: thrown forward mid-step, head cocked.
            {
                hipHeight = 1.45f, spine = new Vector3(50f, 0f, 8f), neck = new Vector3(25f, 0f, -15f), head = new Vector3(-35f, 0f, 25f),
                shoulderL = new Vector3(-35f, 0f, 0f), elbowL = new Vector3(-40f, 0f, 0f), shoulderR = new Vector3(15f, 0f, 10f),
                elbowR = new Vector3(-5f, 0f, 0f), legL = new Vector3(15f, 0f, 0f), legR = new Vector3(-18f, 0f, 0f),
            },
            new PoseData // Reach: one long arm out toward you, fingers spread.
            {
                hipHeight = 1.55f, spine = new Vector3(20f, 0f, 0f), neck = new Vector3(30f, 0f, 0f), head = new Vector3(-25f, 0f, -20f),
                shoulderL = new Vector3(0f, 0f, -8f), shoulderR = new Vector3(-95f, 0f, 0f), elbowR = new Vector3(-10f, 0f, 0f),
                handR = new Vector3(-20f, 0f, 0f),
            },
            new PoseData // Listen: dead still, head laid over on its shoulder.
            {
                hipHeight = 1.55f, spine = new Vector3(10f, 0f, 0f), neck = new Vector3(10f, 0f, 40f), head = new Vector3(0f, 0f, 75f),
                shoulderL = new Vector3(0f, 0f, -10f), shoulderR = new Vector3(0f, 0f, 10f),
            },
            new PoseData // Crawl: down on all fours, head up and forward.
            {
                hipHeight = 0.95f, spine = new Vector3(85f, 0f, 0f), neck = new Vector3(-40f, 0f, 0f), head = new Vector3(-30f, 0f, 0f),
                shoulderL = new Vector3(-85f, 0f, 0f), elbowL = new Vector3(10f, 0f, 0f), shoulderR = new Vector3(-80f, 0f, 0f),
                elbowR = new Vector3(5f, 0f, 0f), legL = new Vector3(55f, 0f, 0f), legR = new Vector3(60f, 0f, 0f),
            },
            new PoseData // Stretch: pulled up to full height, head thrown back, mouth to the sky.
            {
                hipHeight = 1.6f, spine = new Vector3(-5f, 0f, 0f), neck = new Vector3(-10f, 0f, 0f), head = new Vector3(-25f, 0f, 0f),
                shoulderL = new Vector3(0f, 0f, -25f), elbowL = new Vector3(-10f, 0f, 0f), shoulderR = new Vector3(0f, 0f, 25f),
                elbowR = new Vector3(-10f, 0f, 0f),
            },
        };

        public Pose Current { get; private set; } = Pose.Hang;

        /// <summary>Bumped every time the pose changes. Tests use it to check it never moves while watched.</summary>
        public int Changes { get; private set; }

        public void Configure(Transform hips, Transform spine, Transform neck, Transform head,
            Transform shoulderL, Transform elbowL, Transform handL, Transform shoulderR, Transform elbowR, Transform handR,
            Transform legL, Transform legR)
        {
            this.hips = hips;
            this.spine = spine;
            this.neck = neck;
            this.head = head;
            this.shoulderL = shoulderL;
            this.elbowL = elbowL;
            this.handL = handL;
            this.shoulderR = shoulderR;
            this.elbowR = elbowR;
            this.handR = handR;
            this.legL = legL;
            this.legR = legR;
        }

        void Awake() => Apply(Pose.Hang, null);

        /// <summary>Jumps straight into <paramref name="pose"/>. With a target, the head turns to face it.</summary>
        public void Snap(Pose pose, Transform faceTarget)
        {
            Apply(pose, faceTarget);
            Changes++;
        }

        void Apply(Pose pose, Transform faceTarget)
        {
            Current = pose;
            var data = Poses[(int)pose];
            if (hips != null)
                hips.localPosition = new Vector3(hips.localPosition.x, data.hipHeight, hips.localPosition.z);
            Set(spine, data.spine);
            Set(neck, data.neck);
            Set(head, data.head);
            Set(shoulderL, data.shoulderL);
            Set(elbowL, data.elbowL);
            Set(handL, data.handL);
            Set(shoulderR, data.shoulderR);
            Set(elbowR, data.elbowR);
            Set(handR, data.handR);
            Set(legL, data.legL);
            Set(legR, data.legR);

            if (faceTarget != null && head != null && pose != Pose.Listen)
            {
                // Turned all the way round if it has to. Keeps the pose's sideways tilt.
                Vector3 to = faceTarget.position + Vector3.up * 1.5f - head.position;
                if (to.sqrMagnitude > 0.01f)
                    head.rotation = Quaternion.LookRotation(to.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, data.head.z);
            }
        }

        static void Set(Transform joint, Vector3 euler)
        {
            if (joint != null)
                joint.localRotation = Quaternion.Euler(euler);
        }

        /// <summary>A walking pose: mostly hanging and lurching, sometimes down on all fours, reaching when close.</summary>
        public static Pose NextWalkingPose(Pose current, float distanceToTarget, System.Func<float> random)
        {
            float r = random();
            if (distanceToTarget < 7f && r < 0.35f)
                return Pose.Reach;
            if (r < 0.15f)
                return Pose.Crawl;
            return current == Pose.Hang ? Pose.Lurch : Pose.Hang;
        }
    }
}
