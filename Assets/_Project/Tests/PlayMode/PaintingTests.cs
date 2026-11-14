using System;
using System.Collections;
using System.Collections.Generic;
using HorrorGame.AI;
using HorrorGame.Painting;
using HorrorGame.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HorrorGame.Tests
{
    /// <summary>
    /// The haunted painting's rules: the world finishes painting where you look, the entity can only
    /// move where it is unfinished, it kills on contact, and burning every anchor banishes it and
    /// throws the player back out. Built in a bare arena; game time runs 3x faster.
    /// </summary>
    public class PaintingTests
    {
        const float TimeScale = 3f;

        readonly List<Object> spawned = new List<Object>();
        float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = TimeScale;
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

        [Test]
        public void Attention_PaintsWhereYouLook_AndDissolvesBehindYou()
        {
            var camera = SpawnViewer(new Vector3(0f, 1.6f, 0f), Vector3.forward);
            var attention = Track(new GameObject("Attention")).AddComponent<PaintAttention>();
            attention.Viewer = camera;
            attention.ResetAll();

            Assert.AreEqual(0f, attention.At(new Vector3(0f, 1.6f, 10f)), 0.001f, "Nothing is painted before you look.");
            attention.Tick(1f);
            Assert.AreEqual(1f, attention.At(new Vector3(0f, 1.6f, 10f)), 0.001f, "Straight ahead paints in within a second.");
            Assert.AreEqual(0f, attention.At(new Vector3(0f, 1.6f, -10f)), 0.001f, "Behind you stays wet.");

            camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
            attention.Tick(0.5f);
            float fading = attention.At(new Vector3(0f, 1.6f, 10f));
            Assert.That(fading, Is.InRange(0.5f, 0.99f), "Turning away starts dissolving it, slowly.");
            attention.Tick(3f);
            Assert.AreEqual(0f, attention.At(new Vector3(0f, 1.6f, 10f)), 0.001f, "Out of view long enough, it is wet again.");
        }

        [UnityTest]
        public IEnumerator Entity_IsPinnedWhileLookedAt_ComesWhenYouLookAway_AndKills()
        {
            BuildArena();
            var player = SpawnPlayer(new Vector3(0f, 0f, -10f), Vector3.forward, out var camera, out var health);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(0f, 0f, 8f));
            realm.Configure(Track(new GameObject("Arrival")).transform, entity, Array.Empty<BanishAnchor>(), attention, null);
            attention.enabled = true;
            attention.ResetAll(1f); // as if the player has been looking ahead all along
            entity.Activate(player);

            Vector3 start = entity.transform.position;
            yield return WaitSeconds(2f);
            Assert.That(Vector3.Distance(entity.transform.position, start), Is.LessThan(0.2f), "Looked at, it must not move.");
            Assert.IsFalse(entity.CanMove);

            // Turn around. The paint behind us dissolves and it comes.
            player.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return WaitFor(() => entity.CanMove, 3f, "be allowed to move once the paint there dissolves");
            yield return WaitFor(() => !health.IsAlive, 15f, "reach the player and kill them");
            Assert.AreSame(entity.gameObject, health.Killer);
        }

        [UnityTest]
        public IEnumerator BurningEveryAnchor_OpensTheFrame_AndEscapingBanishesIt()
        {
            BuildArena();
            Vector3 outsidePosition = new Vector3(0f, 0f, -15f);
            var player = SpawnPlayer(outsidePosition, Vector3.forward, out var camera, out var health);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(0f, 0f, 15f));
            var arrival = Track(new GameObject("Arrival")).transform;
            arrival.position = new Vector3(-10f, 0f, 0f);
            var anchors = new[]
            {
                Track(new GameObject("AnchorA")).AddComponent<BanishAnchor>(),
                Track(new GameObject("AnchorB")).AddComponent<BanishAnchor>(),
            };
            realm.Configure(arrival, entity, anchors, attention, null);

            realm.Enter(player, null);
            yield return WaitFor(() => realm.State == PaintedRealm.RealmState.Inside, 5f, "take the player inside");
            Assert.That(Vector3.Distance(player.transform.position, arrival.position), Is.LessThan(0.1f));
            attention.ResetAll(1f); // keep it pinned so it cannot interfere

            anchors[0].Ignite();
            yield return WaitFor(() => realm.Burned == 1, 5f, "burn the first anchor");
            Assert.AreEqual(PaintedRealm.RealmState.Inside, realm.State, "One of two burned is not enough.");
            realm.Leave();
            yield return WaitSeconds(1f);
            Assert.AreEqual(PaintedRealm.RealmState.Inside, realm.State, "The frame stays shut until every anchor burns.");

            anchors[1].Ignite();
            yield return WaitFor(() => realm.ExitOpen, 5f, "open the frame once the last anchor burns");
            Assert.IsFalse(entity.IsBanished, "It is only gone once you escape.");
            realm.Leave();
            yield return WaitFor(() => realm.State == PaintedRealm.RealmState.Banished, 10f, "escape and break the curse");

            Assert.IsTrue(entity.IsBanished);
            Assert.IsTrue(health.IsAlive);
            Assert.That(Vector3.Distance(player.transform.position, outsidePosition), Is.LessThan(0.1f),
                "The player should be back where they touched the painting.");
            Assert.IsFalse(realm.CanEnter, "A banished painting cannot be entered again.");
        }

        // ------------------------------------------------------------------ helpers

        void BuildArena()
        {
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(60f, 0.2f, 60f);
            Physics.SyncTransforms();
            var navMesh = Track(new GameObject("TestNavMesh"));
            navMesh.transform.position = new Vector3(0f, 3f, 0f);
            navMesh.AddComponent<RuntimeNavMesh>();
        }

        Camera SpawnViewer(Vector3 position, Vector3 facing)
        {
            var go = Track(new GameObject("Viewer"));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            var camera = go.AddComponent<Camera>();
            camera.enabled = false; // no rendering needed
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            return camera;
        }

        GameObject SpawnPlayer(Vector3 position, Vector3 facing, out Camera camera, out PlayerHealth health)
        {
            var go = Track(new GameObject("TestPlayer"));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
            health = go.AddComponent<PlayerHealth>();
            var head = new GameObject("Head");
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camera = head.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 60f;
            camera.aspect = 16f / 9f;
            return go;
        }

        PaintedRealm SpawnRealm(Camera viewer, out PaintAttention attention)
        {
            var go = Track(new GameObject("TestRealm"));
            attention = go.AddComponent<PaintAttention>();
            attention.Viewer = viewer;
            return go.AddComponent<PaintedRealm>();
        }

        PaintedEntity SpawnEntity(Vector3 position)
        {
            var go = Track(new GameObject("TestEntity"));
            go.transform.position = position;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2.6f;
            return go.AddComponent<PaintedEntity>();
        }

        T Track<T>(T obj) where T : Object
        {
            spawned.Add(obj);
            return obj;
        }

        static IEnumerator WaitFor(Func<bool> condition, float timeout, string what)
        {
            float deadline = Time.time + timeout;
            while (!condition())
            {
                if (Time.time > deadline)
                    Assert.Fail($"Timed out after {timeout} s waiting for it to {what}.");
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
