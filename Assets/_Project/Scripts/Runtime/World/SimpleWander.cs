using UnityEngine;

namespace HorrorGame.World
{
    /// <summary>
    /// Look-test wanderer for unanimated creature models: walks a loop of points with a lurching,
    /// swaying gait, pauses at each, and stops to stare when the player comes close. No pathfinding
    /// and no collisions, so give it points with clear straight lines between them.
    /// Put this on an empty parent; the model goes in <see cref="body"/> so the gait doesn't fight the path.
    /// </summary>
    public class SimpleWander : MonoBehaviour
    {
        [SerializeField] Vector3[] points = new Vector3[0];
        [SerializeField] float speed = 0.8f;
        [SerializeField] float turnSpeed = 50f;
        [SerializeField] Vector2 pauseSeconds = new Vector2(1.5f, 5f);

        [Header("Gait")]
        [SerializeField] Transform body;
        [SerializeField] float stepsPerSecond = 1.1f;
        [SerializeField] float bob = 0.1f;
        [SerializeField] float sway = 7f;
        [SerializeField] float lean = 8f;

        [Header("Arms (optional)")]
        [Tooltip("Upper-arm bones of a T-posed rig. They are swung down by droopAngle and swing as it walks.")]
        [SerializeField] Transform[] armBones = new Transform[0];
        [SerializeField] float droopAngle = 65f;
        [SerializeField] float armSwing = 10f;

        [Header("Noticing")]
        [SerializeField] float noticeDistance = 9f;

        int target;
        float pauseLeft;
        float phase;
        float moving;
        Transform player;
        Quaternion[] armBase;
        Vector3[] armAxis;
        Vector3 bodyBasePosition;

        void Start()
        {
            if (body != null)
                bodyBasePosition = body.localPosition;
            if (Camera.main != null)
                player = Camera.main.transform;

            armBase = new Quaternion[armBones.Length];
            armAxis = new Vector3[armBones.Length];
            for (int i = 0; i < armBones.Length; i++)
            {
                var bone = armBones[i];
                if (bone == null)
                    continue;
                // Which way does the arm point sideways? Swing it down around the creature's forward axis.
                Vector3 tip = bone.childCount > 0 ? bone.GetChild(0).position : bone.position + bone.up;
                float side = Mathf.Sign(transform.InverseTransformDirection(tip - bone.position).x);
                bone.rotation = Quaternion.AngleAxis(-side * droopAngle, transform.forward) * bone.rotation;
                armBase[i] = bone.localRotation;
                armAxis[i] = bone.InverseTransformDirection(transform.right);
            }
        }

        void Update()
        {
            if (points.Length == 0)
                return;

            bool staring = player != null && Vector3.Distance(Flat(player.position), Flat(transform.position)) < noticeDistance;
            Vector3 goal = staring ? player.position : points[target];
            Vector3 to = Flat(goal) - Flat(transform.position);

            float wantMove = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                var face = Quaternion.LookRotation(to, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
                bool facing = Quaternion.Angle(transform.rotation, face) < 20f;
                if (!staring && pauseLeft <= 0f && facing)
                    wantMove = 1f;
            }
            if (pauseLeft > 0f)
                pauseLeft -= Time.deltaTime;

            moving = Mathf.MoveTowards(moving, wantMove, Time.deltaTime * 1.5f);
            transform.position += transform.forward * (speed * moving * Time.deltaTime);

            if (!staring && to.magnitude < 0.3f)
            {
                target = (target + 1) % points.Length;
                pauseLeft = Random.Range(pauseSeconds.x, pauseSeconds.y);
            }

            Gait();
        }

        void Gait()
        {
            phase += Time.deltaTime * stepsPerSecond * Mathf.PI * 2f * Mathf.Max(moving, 0.15f);
            float s = Mathf.Sin(phase);
            if (body != null)
            {
                // Heavy, uneven steps: the drop is sharper than the rise, and it rolls side to side.
                float step = Mathf.Abs(s);
                body.localPosition = bodyBasePosition + Vector3.up * (bob * moving * (step * step));
                body.localRotation = Quaternion.Euler(lean * moving + Mathf.Sin(phase * 0.5f) * 2f, 0f, s * sway * Mathf.Max(moving, 0.2f));
            }
            for (int i = 0; i < armBones.Length; i++)
                if (armBones[i] != null)
                    armBones[i].localRotation = armBase[i] * Quaternion.AngleAxis(s * armSwing * moving * (i % 2 == 0 ? 1f : -1f), armAxis[i]);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < points.Length; i++)
                Gizmos.DrawLine(points[i], points[(i + 1) % points.Length]);
        }
    }
}
