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
        public IEnumerator Entity_IsPinnedWhileLookedAt_WalksToSounds_AndKillsSomeoneNoisy()
        {
            BuildArena();
            var player = SpawnPlayer(new Vector3(0f, 0f, -10f), Vector3.forward, out var camera, out var health);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(0f, 0f, 8f));
            realm.Configure(Track(new GameObject("Arrival")).transform, entity, Array.Empty<MutedVoice>(), attention, null);
            attention.enabled = true;
            attention.ResetAll(1f); // as if the player has been looking ahead all along
            entity.Activate(player);
            entity.Emerge(false, false);
            Assert.IsTrue(entity.Hunting);

            Vector3 start = entity.transform.position;
            yield return WaitSeconds(2f);
            Assert.That(Vector3.Distance(entity.transform.position, start), Is.LessThan(0.2f), "Looked at, it must not move.");
            Assert.IsFalse(entity.CanMove);

            // Turn around and keep stamping. The paint behind dissolves, it hears us and walks over.
            player.transform.rotation = Quaternion.LookRotation(Vector3.back);
            yield return WaitFor(() => entity.CanMove, 3f, "be allowed to move once the paint there dissolves");
            float deadline = Time.time + 25f;
            while (health.IsAlive && Time.time < deadline)
            {
                Noise.Emit(player.transform.position, 13f, player);
                yield return WaitSeconds(0.4f);
            }
            Assert.IsFalse(health.IsAlive, "A noisy player it reaches should die.");
            Assert.AreSame(entity.gameObject, health.Killer);
        }

        [UnityTest]
        public IEnumerator Entity_PassesAPlayerWhoStaysStillAndQuiet()
        {
            BuildArena();
            var player = SpawnPlayer(new Vector3(0f, 0f, -10f), Vector3.back, out var camera, out var health);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(0f, 0f, 2f));
            realm.Configure(Track(new GameObject("Arrival")).transform, entity, Array.Empty<MutedVoice>(), attention, null);
            attention.enabled = false;
            attention.ResetAll(0f); // all wet: it can always move
            entity.Activate(player);
            entity.Emerge(false, false); // heads for where the player is standing

            yield return WaitFor(() => Vector3.Distance(entity.transform.position, player.transform.position) < 2f, 20f,
                "walk up to where the player was");
            yield return WaitSeconds(8f);
            Assert.IsTrue(health.IsAlive, "Standing still and quiet, it should pass you by.");
            Assert.That(Vector3.Distance(entity.Goal, player.transform.position), Is.GreaterThan(3f),
                "After listening it should wander off somewhere else.");
        }

        [UnityTest]
        public IEnumerator FreeingEveryVoice_OpensTheRealChurch_AndTheFullChordDestroysIt()
        {
            BuildArena();
            Vector3 outsidePosition = new Vector3(0f, 0f, -15f);
            var player = SpawnPlayer(outsidePosition, Vector3.forward, out var camera, out var health);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(15f, 0f, 15f));
            var arrival = Point("Arrival", new Vector3(-10f, 0f, 0f));
            var churchDoor = Point("ChurchDoor", new Vector3(20f, 0f, 0f));
            var churchInside = Point("ChurchInside", new Vector3(0f, 0f, 10f));
            var altar = Point("Altar", new Vector3(0f, 0f, 25f));
            var voices = new[] { SpawnVoice(new Vector3(-10f, 0f, 5f)), SpawnVoice(new Vector3(-10f, 0f, 12f)) };
            realm.Configure(arrival, entity, voices, attention, null, null, churchDoor, altar);

            realm.Enter(player, null);
            // Wait for the fade-in to finish too: nothing can be used while the realm is busy.
            yield return WaitFor(() => realm.State == PaintedRealm.RealmState.Inside && !realm.Busy, 15f, "take the player inside");
            Assert.That(Vector3.Distance(player.transform.position, arrival.position), Is.LessThan(0.1f));
            // Freeze the paint fully finished everywhere, so the entity stays pinned and cannot interfere.
            attention.enabled = false;
            attention.ResetAll(1f);

            Assert.That(Vector3.Distance(realm.CurrentGoal.Value, voices[0].transform.position), Is.LessThan(0.01f),
                "The song should lead to the nearest painted-over voice first.");
            voices[0].Free();
            Assert.AreEqual(1, realm.Freed);
            Assert.AreEqual(0.5f, realm.Harmony, 0.001f);
            Assert.That(Vector3.Distance(realm.CurrentGoal.Value, voices[1].transform.position), Is.LessThan(0.01f));

            realm.EnterChurch(churchInside);
            yield return WaitSeconds(1.5f);
            Assert.IsFalse(realm.InChurch, "The real church stays shut until the chord is complete.");

            voices[1].Free();
            Assert.IsTrue(realm.ChordComplete);
            Assert.That(Vector3.Distance(realm.CurrentGoal.Value, churchDoor.position), Is.LessThan(0.01f),
                "With the chord complete, the song should lead to the real church.");
            realm.EnterChurch(churchInside);
            // Fades run on real time while the test runs game time 3x faster, so allow plenty.
            yield return WaitFor(() => realm.InChurch && !realm.Busy, 15f, "go into the church");
            Assert.That(Vector3.Distance(realm.CurrentGoal.Value, altar.position), Is.LessThan(0.01f));
            yield return WaitSeconds(1f);
            Assert.AreEqual(PaintedRealm.RealmState.Inside, realm.State, "Nothing happens until you reach the altar.");

            player.transform.position = new Vector3(0f, 0f, 21f);
            yield return WaitFor(() => realm.State == PaintedRealm.RealmState.Banished, 30f, "destroy it with the full chord and let you go");
            Assert.IsTrue(entity.IsBanished);
            Assert.IsTrue(health.IsAlive);
            Assert.That(Vector3.Distance(player.transform.position, outsidePosition), Is.LessThan(0.1f),
                "The player should be back where they touched the painting.");
            Assert.IsFalse(realm.CanEnter, "A banished painting cannot be entered again.");
        }

        [Test]
        public void Alignment_IsHighFacingTheGoal_AndSoursWhenPlayersSplitUp()
        {
            var goal = new Vector3(0f, 0f, 20f);
            var a = Track(new GameObject("A")).transform;
            var b = Track(new GameObject("B")).transform;
            a.rotation = b.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.AreEqual(1f, Choir.ComputeAlignment(new[] { a, b }, goal), 0.001f, "Both heading for it: full harmony.");
            b.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.AreEqual(0.5f, Choir.ComputeAlignment(new[] { a, b }, goal), 0.001f, "Split up: the chord sours.");
            a.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.AreEqual(0f, Choir.ComputeAlignment(new[] { a, b }, goal), 0.001f, "Both heading away: no harmony.");
        }

        [UnityTest]
        public IEnumerator Choir_PointsAtTheNextMutedVoice_AndWhenItFallsSilent_TheThingComesOut()
        {
            BuildArena();
            var player = SpawnPlayer(new Vector3(0f, 0f, -10f), Vector3.forward, out var camera, out _);
            var realm = SpawnRealm(camera, out var attention);
            var entity = SpawnEntity(new Vector3(0f, 0f, 15f));
            var muted = SpawnVoice(new Vector3(12f, 0f, -10f)); // due east of the player
            var singerObject = Track(new GameObject("Singer"));
            singerObject.transform.position = new Vector3(0f, 0f, 0f);
            var singer = singerObject.AddComponent<ChoirSinger>();
            var choir = Track(new GameObject("Choir")).AddComponent<Choir>();
            choir.Configure(realm, entity, new[] { singer });
            realm.Configure(Track(new GameObject("Arrival")).transform, entity, new[] { muted }, attention, null, choir);
            attention.enabled = true;
            attention.ResetAll(1f);

            realm.Enter(player, null);
            yield return WaitFor(() => realm.State == PaintedRealm.RealmState.Inside, 15f, "take the player inside");
            Assert.AreEqual(Choir.ChoirState.Singing, choir.State);
            Assert.IsTrue(singer.Singing);
            Assert.IsFalse(entity.Hunting, "While they sing, nothing is out.");

            yield return WaitSeconds(3f);
            Vector3 look = singer.Head.forward;
            Vector3 toVoice = (muted.transform.position - singer.transform.position).normalized;
            Assert.That(Vector3.Angle(look, toVoice), Is.LessThan(10f), "Every head should turn to the next painted-over voice.");

            choir.FallSilent(false);
            Assert.AreEqual(Choir.ChoirState.Silent, choir.State);
            Assert.IsFalse(singer.Singing);
            Assert.IsTrue(entity.Hunting, "When the song stops, the thing stands up.");
            yield return WaitSeconds(0.5f);
            Vector3 toEntity = entity.transform.position - singer.transform.position;
            toEntity.y = 0f;
            Assert.That(Vector3.Angle(singer.Head.forward, toEntity), Is.LessThan(10f), "Every head should snap to the thing.");

            yield return WaitFor(() => choir.State == Choir.ChoirState.Singing, 15f, "start singing again");
            Assert.IsFalse(entity.Hunting, "When the song comes back, it goes under.");
        }

        // ------------------------------------------------------------------ helpers

        Transform Point(string name, Vector3 position)
        {
            var t = Track(new GameObject(name)).transform;
            t.position = position;
            return t;
        }

        MutedVoice SpawnVoice(Vector3 position)
        {
            var go = Track(new GameObject("MutedVoice"));
            go.transform.position = position;
            go.AddComponent<ChoirSinger>();
            return go.AddComponent<MutedVoice>();
        }

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
