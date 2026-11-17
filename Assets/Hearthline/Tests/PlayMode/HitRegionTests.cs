using System.Collections;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Physical hits in the greybox scene become wounds on the right body region: ray -> hit zone on a bone ->
    /// HitReceiver -> InflictHit in the simulation.
    /// </summary>
    public sealed class HitRegionTests
    {
        private SimHost _host;
        private GameObject _player;
        private HitReceiver _receiver;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M0_Greybox");
            yield return null;
            _host = SimHost.Instance;
            Assert.That(_host, Is.Not.Null, "The greybox scene has no SimHost.");
            _host.SaveFileName = "hittest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _player = GameObject.Find("Player");
            Assert.That(_player, Is.Not.Null, "The greybox scene has no Player.");
            _receiver = _player.GetComponent<HitReceiver>();
            Assert.That(_receiver, Is.Not.Null, "The player has no HitReceiver.");
            yield return null; // let the animator pose the body before casting at it
        }

        [UnityTest]
        public IEnumerator ShotFromTheFrontAtTheFaceWoundsTheFace()
        {
            yield return ShootAtZone("HitZone_Face", _player.transform.forward, BodyRegion.Face);
        }

        [UnityTest]
        public IEnumerator ShotFromBehindAtTheHeadWoundsTheHead()
        {
            yield return ShootAtZone("HitZone_Head", -_player.transform.forward, BodyRegion.Head);
        }

        [UnityTest]
        public IEnumerator ShotFromTheFrontAtTheLeftHandWoundsTheLeftHand()
        {
            yield return ShootAtZone("HitZone_HandL", _player.transform.forward, BodyRegion.HandL);
        }

        [UnityTest]
        public IEnumerator EachDamageTypeGivesAWoundOfThatType()
        {
            foreach (DamageType damage in System.Enum.GetValues(typeof(DamageType)))
            {
                _host.NewGame();
                yield return null;
                HitOutcome outcome = _receiver.Receive(BodyRegion.Torso, damage, 1, new MarkOrigin { Cause = "a test", Place = "the greybox" });
                Assert.That(outcome.Ok, Is.True, outcome.ToString());

                MarkView wound = OnlyWound();
                Assert.That(wound.Region, Is.EqualTo(BodyRegion.Torso));
                Assert.That(_host.Content.FindMark(wound.DefId).DamageTypes, Has.Member(damage), wound.DefId + " for " + damage);
            }
        }

        [UnityTest]
        public IEnumerator HitOnADownedCharacterIsRejectedCleanly()
        {
            var origin = new MarkOrigin { Cause = "a test", Place = "the greybox" };
            for (int i = 0; i < 10 && !_host.Sim.Head.Downed; i++)
                _receiver.Receive(BodyRegion.Torso, DamageType.Gunshot, 3, origin);
            Assert.That(_host.Sim.Head.Downed, Is.True, "Severe hits should down the head.");
            int marks = _host.Sim.Head.Marks.Count;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("rejected"));
            HitOutcome outcome = _receiver.Receive(BodyRegion.ArmL, DamageType.Blade, 1, origin);
            yield return null;

            Assert.That(outcome.Ok, Is.False);
            Assert.That(_host.Sim.Head.Marks.Count, Is.EqualTo(marks));
        }

        [UnityTest]
        public IEnumerator HitZonesDoNotGetInTheWayOfMovement()
        {
            int layer = LayerMask.NameToLayer(HitReceiver.LayerName);
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "There is no Hitbox layer.");
            foreach (HitRegion zone in _player.GetComponentsInChildren<HitRegion>())
            {
                Assert.That(zone.gameObject.layer, Is.EqualTo(layer), zone.name);
                Assert.That(zone.GetComponent<Collider>().isTrigger, Is.True, zone.name);
            }
            Assert.That(Physics.GetIgnoreLayerCollision(layer, _player.layer), Is.True);
            Assert.That(Physics.GetIgnoreLayerCollision(_player.layer, 0), Is.False, "The player must still collide with the ground.");

            var controller = _player.GetComponent<CharacterController>();
            Vector3 start = _player.transform.position;
            for (int i = 0; i < 10; i++)
            {
                controller.Move(_player.transform.forward * 0.1f);
                yield return null;
            }
            Vector3 moved = _player.transform.position - start;
            Assert.That(new Vector2(moved.x, moved.z).magnitude, Is.GreaterThan(0.9f), "The player was blocked by its own hit zones.");

            // And it stands on the ground rather than falling through it.
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(_player.transform.position.y, Is.GreaterThan(-0.5f), "The player fell through the ground.");
        }

        private IEnumerator ShootAtZone(string zoneName, Vector3 fromDirection, BodyRegion expected)
        {
            Transform zone = FindChild(zoneName);
            Assert.That(zone, Is.Not.Null, "No " + zoneName + " on the player.");
            Vector3 target = zone.GetComponent<Collider>().bounds.center;
            var ray = new Ray(target + fromDirection * 3f, -fromDirection);

            Assert.That(HitReceiver.Raycast(ray, 6f, out HitRegion struck, out _), Is.True, "The shot missed every hit zone.");
            Assert.That(struck.Region, Is.EqualTo(expected), "The shot struck " + struck.name);
            Assert.That(struck.Receiver, Is.SameAs(_receiver));

            HitOutcome outcome = struck.Receiver.Receive(struck.Region, DamageType.Gunshot, 1, new MarkOrigin { Cause = "a test", Place = "the greybox" });
            yield return null;
            Assert.That(outcome.Ok, Is.True, outcome.ToString());
            Assert.That(OnlyWound().Region, Is.EqualTo(expected));
        }

        private MarkView OnlyWound()
        {
            var wounds = new List<MarkView>();
            foreach (MarkView view in _host.Sim.MarkViewsOf(_host.Sim.Head))
                if (view.Category == MarkCategory.Wound) wounds.Add(view);
            Assert.That(wounds.Count, Is.EqualTo(1), "Expected exactly one wound.");
            return wounds[0];
        }

        private Transform FindChild(string name)
        {
            foreach (Transform t in _player.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
