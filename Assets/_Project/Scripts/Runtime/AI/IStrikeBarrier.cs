namespace HorrorGame.AI
{
    /// <summary>
    /// Something a monster can see through but not reach through, such as a glass pane. When a
    /// monster's grab is stopped by it, the barrier is told so it can react (a thud, a shudder).
    /// </summary>
    public interface IStrikeBarrier
    {
        void OnStrikeBlocked(MonsterBrain monster, UnityEngine.Vector3 point);
    }
}
