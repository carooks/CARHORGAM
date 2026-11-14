using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The door of one of the black churches standing in the fog. They all look the same. Only one
    /// is real, and the choir's song leads to it. A false door opens onto more field: you are
    /// somewhere else in the fog and the singing stops. The real one takes you inside.
    /// </summary>
    public class ChurchDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] PaintedRealm realm;
        [SerializeField] bool isReal;
        [Tooltip("Real door: where you stand once inside. False door: ignored (you land somewhere in the field).")]
        [SerializeField] Transform inside;
        [SerializeField] string prompt = "Open the church door";
        [SerializeField] string lockedPrompt = "It won't open. The song isn't finished";

        public bool IsReal => isReal;

        public PaintedRealm Realm
        {
            get => realm;
            set => realm = value;
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            if (realm == null || realm.State != PaintedRealm.RealmState.Inside)
                return null;
            return isReal && !realm.ChordComplete ? lockedPrompt : prompt;
        }

        public bool CanInteract(PlayerInteractor interactor) =>
            realm != null && realm.State == PaintedRealm.RealmState.Inside && !realm.Busy
            && (!isReal || realm.ChordComplete);

        public void Interact(PlayerInteractor interactor)
        {
            if (isReal)
                realm.EnterChurch(inside);
            else
                realm.FalseDoor(transform.position);
        }
    }
}
