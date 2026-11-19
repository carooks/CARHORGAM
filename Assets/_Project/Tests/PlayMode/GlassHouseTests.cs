using System;
using System.Collections;
using System.Collections.Generic;
using HorrorGame.AI;
using HorrorGame.Glass;
using HorrorGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    /// <summary>
    /// Checks the glass house pieces in a bare arena: panes block the monster's grab but not its sight,
    /// banging makes a noise, layouts never stack two panes over one gap, sound is muffled through glass,
    /// and the ghost only passes the panes marked for it. Window > General > Test Runner > PlayMode.
    /// </summary>
    public class GlassHouseTests
    {
        readonly List<Object> spawned = new List<Object>();
        float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 3f;
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

        [UnityTest]
        public IEnumerator Banging_OnGlass_EmitsANoise()
        {
            var pane = SpawnPane(Vector3.zero, false);
            yield return null;
            NoiseEvent? heard = null;
            Action<NoiseEvent> listener = n => heard = n;
            Noise.Emitted += listener;
            try
            {
                Assert.IsTrue(pane.CanInteract(null));
                pane.Interact(null);
            }
            finally
            {
                Noise.Emitted -= listener;
            }
            Assert.IsTrue(heard.HasValue, "Banging should be heard by monsters.");
            Assert.That(heard.Value.Radius, Is.GreaterThan(10f));
            Assert.IsFalse(pane.CanInteract(null), "There is a short cooldown between bangs.");
        }

        [UnityTest]
        public IEnumerator Monster_SeesThroughGlass_ButCannotGrabThroughIt()
        {
            Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(24f, 0.2f, 24f));
            // A wall of glass along z = 0, wider than the floor, so there is no way around it.
            for (int i = -4; i <= 4; i++)
                SpawnPane(new Vector3(i * 3f, 0f, 0f), false);
            Physics.SyncTransforms();
            var navMesh = new GameObject("TestNavMesh");
            navMesh.transform.position = new Vector3(0f, 2f, 0f);
            navMesh.AddComponent<RuntimeNavMesh>();
            spawned.Add(navMesh);

            var monster = SpawnMonster(new Vector3(0f, 0f, -5f));
            var target = SpawnTarget(new Vector3(0f, 0f, 0.6f));
            int blocked = 0, struck = 0;
            monster.StrikeBlocked += _ => blocked++;
            monster.Struck += _ => struck++;

            yield return WaitFor(() => monster.State == MonsterState.Chase, 3f, "see the player through the glass and chase");
            yield return WaitFor(() => blocked > 0, 10f, "swing at the player and hit the glass");
            Assert.AreEqual(0, struck, "The grab must not land through glass.");
            Assert.IsTrue(target.IsAlive);
            Assert.That(monster.transform.position.z, Is.LessThan(-0.2f), "The monster must stay on its side of the glass.");
        }

        [UnityTest]
        public IEnumerator Layout_OpensAtMostOnePanePerPair_AndPanesSlideThere()
        {
            var house = SpawnHouse(3);
            yield return null; // Start applies the first layout
            for (int round = 0; round < 10; round++)
            {
                house.Reshuffle();
                for (int s = 0; s < house.SectionCount; s++)
                {
                    bool firstClosed = house.Panes[s * 2].IsClosed, secondClosed = house.Panes[s * 2 + 1].IsClosed;
                    Assert.IsTrue(firstClosed || secondClosed, $"Section {s} has both panes open.");
                    Assert.AreEqual(house.Layout[s] == GlassHouse.Closed, firstClosed && secondClosed);
                }
            }
            yield return WaitFor(() => !house.IsShifting, 10f, "finish sliding");
            foreach (var pane in house.Panes)
            {
                Vector3 expected = pane.IsClosed ? pane.ClosedPosition : pane.OpenPosition;
                Assert.That(Vector3.Distance(pane.transform.position, expected), Is.LessThan(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator Pane_WaitsInsteadOfCrushingSomeone()
        {
            var pane = SpawnPane(Vector3.zero, false);
            yield return null;
            pane.SetClosed(false, instant: true);
            var body = SpawnTarget(Vector3.zero); // standing in the gap
            body.gameObject.AddComponent<CharacterController>();
            Physics.SyncTransforms();
            pane.SetClosed(true);
            yield return WaitSeconds(1.5f);
            Assert.IsTrue(pane.IsBlocked, "The pane should stop against the player.");
            Assert.That(Vector3.Distance(pane.transform.position, pane.ClosedPosition), Is.GreaterThan(0.3f));

            body.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();
            yield return WaitFor(() => !pane.IsMoving, 8f, "close once the way is clear");
        }

        [UnityTest]
        public IEnumerator Sound_ThroughGlass_IsCounted()
        {
            SpawnPane(Vector3.zero, false);
            SpawnPane(new Vector3(0f, 0f, 4f), false);
            yield return null;
            Physics.SyncTransforms();
            var ear = new Vector3(0f, 1.6f, -2f);
            Assert.AreEqual(0, GlassAudioMuffler.CountPanesBetween(ear, new Vector3(0f, 1.5f, -0.5f)));
            Assert.AreEqual(1, GlassAudioMuffler.CountPanesBetween(ear, new Vector3(0f, 1.5f, 2f)));
            Assert.AreEqual(2, GlassAudioMuffler.CountPanesBetween(ear, new Vector3(0f, 1.5f, 6f)));
        }

        [UnityTest]
        public IEnumerator Ghost_PassesOnlyHauntedGlass()
        {
            SpawnPane(Vector3.zero, false);
            SpawnPane(new Vector3(6f, 0f, 0f), true);
            yield return null;
            Physics.SyncTransforms();
            Assert.IsFalse(GlassGhost.IsPathClear(new Vector3(0f, 1.3f, -2f), new Vector3(0f, 1.3f, 2f), 0.25f), "Plain glass stops ghosts.");
            Assert.IsTrue(GlassGhost.IsPathClear(new Vector3(6f, 1.3f, -2f), new Vector3(6f, 1.3f, 2f), 0.25f), "Green glass lets them through.");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A 3 m pane along X at <paramref name="position"/> (its foot), opening 3 m east.</summary>
        GlassPane SpawnPane(Vector3 position, bool haunted)
        {
            var go = new GameObject("TestPane");
            go.transform.position = position;
            go.SetActive(false); // configure before Awake
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.75f, 0f);
            box.size = new Vector3(2.96f, 3.5f, 0.05f);
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = new Vector3(2.96f, 3.5f, 0.3f);
            obstacle.carving = true;
            var pane = go.AddComponent<GlassPane>();
            pane.Configure(new Vector3(3f, 0f, 0.1f), haunted);
            go.layer = GlassPane.Layer;
            go.SetActive(true);
            spawned.Add(go);
            return pane;
        }

        GlassHouse SpawnHouse(int sections)
        {
            var go = new GameObject("TestHouse");
            go.SetActive(false);
            var panes = new GlassPane[sections * 2];
            for (int i = 0; i < panes.Length; i++)
            {
                int s = i / 2, k = i % 2;
                panes[i] = SpawnPane(new Vector3(s * 6f + k * 3f, 0f, 20f), false);
                panes[i].Configure(new Vector3(k == 0 ? 3f : -3f, 0f, k == 0 ? 0.1f : -0.1f), false);
            }
            var house = go.AddComponent<GlassHouse>();
            house.SetPanes(panes);
            go.SetActive(true);
            spawned.Add(go);
            return house;
        }

        void Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = size;
            spawned.Add(go);
        }

        MonsterBrain SpawnMonster(Vector3 position)
        {
            var go = new GameObject("TestMonster");
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.forward));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.45f;
            capsule.height = 2.4f;
            capsule.center = new Vector3(0f, 1.2f, 0f);
            var brain = go.AddComponent<MonsterBrain>();
            brain.SetPatrolRoute(Array.Empty<Transform>(), false);
            spawned.Add(go);
            return brain;
        }

        DetectableTarget SpawnTarget(Vector3 position)
        {
            var go = new GameObject("TestPlayer");
            go.transform.position = position;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            go.AddComponent<PlayerHealth>();
            var target = go.AddComponent<DetectableTarget>();
            target.LitOverride = true;
            spawned.Add(go);
            return target;
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            float deadline = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > deadline)
                    Assert.Fail($"Timed out after {timeout} s waiting to {what}.");
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
