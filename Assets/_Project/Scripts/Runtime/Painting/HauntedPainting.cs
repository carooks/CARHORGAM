using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The portrait on the wall. Look at it and press E to be pulled into its <see cref="PaintedRealm"/>.
    /// The canvas wakes as the player comes close (the smear moves, eyes open), is empty while the
    /// player is inside (the thing in it has stepped out), and is left burnt once it is banished.
    /// Sits on the canvas, which needs a collider for the interaction ray.
    /// </summary>
    public class HauntedPainting : MonoBehaviour, IInteractable
    {
        static readonly int WakeId = Shader.PropertyToID("_Wake");
        static readonly int EmptyId = Shader.PropertyToID("_Empty");
        static readonly int BurntId = Shader.PropertyToID("_Burnt");

        [SerializeField] PaintedRealm realm;
        [SerializeField] Renderer canvas;
        [SerializeField] string enterPrompt = "Touch the wet paint";
        [SerializeField] string burntPrompt = "A burnt canvas";
        [Tooltip("Distance at which the portrait starts to wake.")]
        [SerializeField] float wakeDistance = 6f;
        [Tooltip("Seconds the burn takes to spread across the canvas.")]
        [SerializeField] float burnTime = 3f;

        MaterialPropertyBlock block;
        Transform viewer;
        float wake, empty, burnt;
        float burntTarget;

        public PaintedRealm Realm
        {
            get => realm;
            set => realm = value;
        }

        public bool IsBurnt => burntTarget > 0f;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (canvas == null)
                canvas = GetComponent<Renderer>();
            Apply();
        }

        public string GetPrompt(PlayerInteractor interactor) => IsBurnt ? burntPrompt : enterPrompt;

        public bool CanInteract(PlayerInteractor interactor) => !IsBurnt && realm != null && realm.CanEnter;

        public void Interact(PlayerInteractor interactor)
        {
            viewer = interactor.transform;
            realm.Enter(interactor.gameObject, this);
        }

        public void SetEmpty(bool isEmpty)
        {
            empty = isEmpty ? 1f : 0f;
            Apply();
        }

        public void Burn() => burntTarget = 1f;

        void Update()
        {
            if (viewer == null && Camera.main != null)
                viewer = Camera.main.transform;
            float target = 0f;
            if (viewer != null && !IsBurnt)
                target = Mathf.InverseLerp(wakeDistance, 1.2f, Vector3.Distance(viewer.position, transform.position));
            wake = Mathf.MoveTowards(wake, target, Time.deltaTime * 0.5f);
            burnt = Mathf.MoveTowards(burnt, burntTarget, Time.deltaTime / Mathf.Max(0.01f, burnTime));
            Apply();
        }

        void Apply()
        {
            if (canvas == null)
                return;
            canvas.GetPropertyBlock(block);
            block.SetFloat(WakeId, wake);
            block.SetFloat(EmptyId, empty);
            block.SetFloat(BurntId, burnt);
            canvas.SetPropertyBlock(block);
        }
    }
}
