namespace HorrorGame.AI
{
    /// <summary>What a monster is doing right now. Each state has one job; see <see cref="MonsterBrain"/>.</summary>
    public enum MonsterState
    {
        /// <summary>Walking its route, listening and looking.</summary>
        Patrol,
        /// <summary>Walking to something it heard or glimpsed, then searching around it.</summary>
        Investigate,
        /// <summary>Running at a target it has seen.</summary>
        Chase,
        /// <summary>Close enough to grab: winds up, strikes, recovers.</summary>
        Attack,
    }
}
