using HorrorGame.Interaction;
using UnityEngine;

namespace HorrorGame.Painting
{
    /// <summary>
    /// The way back out: the back of the frame you came through, standing where you arrived.
    /// It stays shut until every anchor in the realm has burned.
    /// </summary>
    public class RealmExit : MonoBehaviour, IInteractable
    {
        [SerializeField] PaintedRealm realm;
        [SerializeField] string prompt = "Climb back out";
        [SerializeField] string lockedPrompt = "It won't move";

        public PaintedRealm Realm
        {
            get => realm;
            set => realm = value;
        }

        public string GetPrompt(PlayerInteractor interactor)
        {
            if (realm == null || realm.State != PaintedRealm.RealmState.Inside)
                return null;
            return realm.ExitOpen ? prompt : lockedPrompt;
        }

        public bool CanInteract(PlayerInteractor interactor) =>
            realm != null && realm.State == PaintedRealm.RealmState.Inside && realm.ExitOpen;

        public void Interact(PlayerInteractor interactor) => realm.Leave();
    }
}
