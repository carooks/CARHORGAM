using System;
using System.Collections;
using System.Collections.Generic;
using HorrorGame.AI;
using HorrorGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    /// <summary>
    /// Drives the monster brain through every state change in a bare test arena (a floor, sometimes
    /// a wall) and checks it reacts the same way every time. Run from Window > General > Test Runner >
    /// PlayMode > Run All. Game time runs 3x faster so the suite finishes in about a minute.
    ///
    /// Arena: 40 x 40 m floor centred on the origin. The optional wall runs east-west along z = 0
    /// from x = -6 to x = 6, 4 m tall. The monster usually starts at z = -10 facing north (+z).
    /// </summary>
    public class MonsterBrainTests
    {
        const float TimeScale = 3f;

        readonly List<Object> spawned = new List<Object>();
        readonly List<MonsterState> transitions = new List<MonsterState>();
        float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = TimeScale;
            transitions.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in spawned)
                if (obj != null)
                    Object.Destroy(obj);
            spawned.Clear();
            Time.timeScale = savedTimeScale;
            yield return null;
        }

        // ------------------------------------------------------------------ patrol

        [UnityTest]
        public IEnumerator Patrol_WalksBetweenItsPoints()
        {
            BuildArena(false);
            var a = new Vector3(-8f, 0f, -8f);
            var b = new Vector3(8f, 0f, -8f);
            var monster = SpawnMonster(a, Vector3.right, a, b);

            yield return WaitFor(() => Near(monster, b, 1f), 20f, "reach the second patrol point");
            yield return WaitFor(() => Near(monster, a, 1f), 25f, "walk back to the first patrol point");
            Assert.That(transitions, Is.Empty, "Nothing to react to, so it should never leave Patrol.");
        }

        // ------------------------------------------------------------------ hearing

        [UnityTest]
        public IEnumerator Noise_InRange_IsInvestigated_ThenItReturnsToPatrol()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            yield return null;

            var noiseAt = new Vector3(8f, 0f, -10f);
            Noise.Emit(noiseAt, 15f, null);
            Assert.AreEqual(MonsterState.Investigate, monster.State, "An 8 m noise that carries 15 m should be heard.");

            yield return WaitFor(() => Near(monster, noiseAt, 1.5f), 10f, "walk to the noise");
            yield return WaitFor(() => monster.State == MonsterState.Patrol, 10f, "give up the search and patrol again");
            CollectionAssert.AreEqual(new[] { MonsterState.Investigate, MonsterState.Patrol }, transitions);
        }

        [UnityTest]
        public IEnumerator Noise_OutOfRange_IsIgnored()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            yield return null;

            Noise.Emit(new Vector3(12f, 0f, -10f), 5f, null);
            yield return WaitSeconds(1f);
            Assert.AreEqual(MonsterState.Patrol, monster.State);
        }

        [UnityTest]
        public IEnumerator Noise_ThroughAWall_CarriesHalfAsFar()
        {
            BuildArena(true);
            var monster = SpawnMonster(new Vector3(0f, 0f, -3f), Vector3.forward);
            yield return null;
            var behindWall = new Vector3(0f, 0f, 3f); // ~6.3 m from its ears, wall in between

            Noise.Emit(behindWall, 8f, null); // would be heard in the open, but 8 x 0.5 = 4 m through a wall
            Assert.AreEqual(MonsterState.Patrol, monster.State, "Muffled noise should not be heard.");

            Noise.Emit(behindWall, 14f, null); // 14 x 0.5 = 7 m: heard
            Assert.AreEqual(MonsterState.Investigate, monster.State, "Loud noise should be heard through the wall.");
        }

        // ------------------------------------------------------------------ sight

        [UnityTest]
        public IEnumerator Sight_LitTargetAhead_StartsChase()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            SpawnTarget(new Vector3(0f, 0f, 5f), lit: true); // 15 m ahead, inside the 22 m lit range

            yield return WaitFor(() => monster.State == MonsterState.Chase, 3f, "notice a lit target 15 m ahead");
        }

        [UnityTest]
        public IEnumerator Sight_DarkTargetBeyondDarkRange_IsNotSeen()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            SpawnTarget(new Vector3(0f, 0f, 5f), lit: false); // 15 m ahead, beyond the 7 m dark range

            yield return WaitSeconds(3f);
            Assert.AreEqual(MonsterState.Patrol, monster.State);
            Assert.AreEqual(0f, monster.Awareness);
        }

        [UnityTest]
        public IEnumerator Sight_TargetBehindIt_IsNotSeen()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -5f), Vector3.forward);
            SpawnTarget(new Vector3(0f, 0f, -15f), lit: true); // 10 m directly behind

            yield return WaitSeconds(3f);
            Assert.AreEqual(MonsterState.Patrol, monster.State);
        }

        [UnityTest]
        public IEnumerator Sight_TargetBehindAWall_IsNotSeen()
        {
            BuildArena(true);
            var monster = SpawnMonster(new Vector3(0f, 0f, -4f), Vector3.forward);
            SpawnTarget(new Vector3(0f, 0f, 4f), lit: true);

            yield return WaitSeconds(3f);
            Assert.AreEqual(MonsterState.Patrol, monster.State);
        }

        [UnityTest]
        public IEnumerator Sight_TargetRightBehindIt_InTheDark_IsSensed()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            SpawnTarget(new Vector3(0f, 0f, -12f), lit: false); // 2 m behind: inside the 2.5 m all-round range

            yield return WaitFor(() => monster.State == MonsterState.Chase, 1f, "sense a target standing right behind it");
        }

        // ------------------------------------------------------------------ chase and attack

        [UnityTest]
        public IEnumerator Chase_CatchesAStandingTarget_AndKillsIt()
        {
            BuildArena(false);
            var monster = SpawnMonster(new Vector3(0f, 0f, -10f), Vector3.forward);
            var target = SpawnTarget(new Vector3(0f, 0f, -2f), lit: true);
            var health = target.GetComponent<PlayerHealth>();
            GameObject killer = null;
            health.Died += k => killer = k;

            yield return WaitFor(() => !health.IsAlive, 8f, "reach and kill a target standing 8 m away");
            Assert.AreSame(monster.gameObject, killer);
            CollectionAssert.IsSubsetOf(new[] { MonsterState.Chase, MonsterState.Attack }, transitions);
            yield return WaitFor(() => monster.State == MonsterState.Patrol, 5f, "go back to patrolling after the kill");
        }

        [UnityTest]
        public IEnumerator Chase_LosingTheTarget_SearchesWhereItWasLastSeen_ThenPatrols()
        {
            BuildArena(true);
            var monster = SpawnMonster(new Vector3(0f, 0f, -16f), Vector3.forward);
            var lastSeen = new Vector3(0f, 0f, -6f);
            var target = SpawnTarget(lastSeen, lit: true);

            yield return WaitFor(() => monster.State == MonsterState.Chase, 2f, "start chasing");
            // The target slips away: behind the wall, far off, and in the dark.
            target.transform.position = new Vector3(0f, 0f, 15f);
            target.LitOverride = false;
            float lostAt = Time.time;

            yield return WaitFor(() => monster.State == MonsterState.Investigate, 8f, "give up the chase");
            Assert.That(Time.time - lostAt, Is.GreaterThan(3.5f), "It should keep chasing for about 4 s before searching.");
            Assert.That(Vector3.Distance(monster.InvestigatePoint, lastSeen), Is.LessThan(1.5f),
                "It should search where it last saw the target.");

            yield return WaitFor(() => monster.State == MonsterState.Patrol, 20f, "finish searching and patrol again");
        }

        // ------------------------------------------------------------------ arena helpers

        void BuildArena(bool withWall)
        {
            Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(40f, 0.2f, 40f));
            if (withWall)
                Box("Wall", new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 0.4f));
            Physics.SyncTransforms();

            var navMesh = new GameObject("TestNavMesh");
            navMesh.transform.position = new Vector3(0f, 3f, 0f);
            navMesh.AddComponent<RuntimeNavMesh>(); // bakes in Awake
            spawned.Add(navMesh);
        }

        void Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = size;
            spawned.Add(go);
        }

        MonsterBrain SpawnMonster(Vector3 position, Vector3 facing, params Vector3[] patrol)
        {
            var go = new GameObject("TestMonster");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 2.4f;
            capsule.center = new Vector3(0f, 1.2f, 0f);
            var brain = go.AddComponent<MonsterBrain>();
            spawned.Add(go);

            var points = new Transform[patrol.Length];
            for (int i = 0; i < patrol.Length; i++)
            {
                points[i] = new GameObject($"Patrol_{i}").transform;
                points[i].position = patrol[i];
                spawned.Add(points[i].gameObject);
            }
            brain.SetPatrolRoute(points, false);
            brain.StateChanged += (_, next) => transitions.Add(next);
            return brain;
        }

        DetectableTarget SpawnTarget(Vector3 position, bool lit)
        {
            var go = new GameObject("TestPlayer");
            go.transform.position = position;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<PlayerHealth>();
            var target = go.AddComponent<DetectableTarget>();
            target.LitOverride = lit;
            spawned.Add(go);
            return target;
        }

        static bool Near(Component c, Vector3 point, float distance)
        {
            Vector3 d = c.transform.position - point;
            d.y = 0f;
            return d.magnitude <= distance;
        }

        /// <summary>Waits until the condition holds, failing the test after <paramref name="timeout"/> seconds of game time.</summary>
        static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            float deadline = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > deadline)
                    Assert.Fail($"Timed out after {timeout} s waiting for the monster to {what}.");
                yield return null;
            }
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
                yield return null;
        }
    }
}
