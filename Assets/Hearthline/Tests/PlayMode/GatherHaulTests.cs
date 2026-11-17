using System.Collections;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Going out and clearly bringing things back: gather spots, the pack on your back and its weight, and the
    /// stores shed filling at home.
    /// </summary>
    public sealed class GatherHaulTests
    {
        private SimHost _host;
        private GameObject _player;
        private TripController _trip;
        private Interactor _interactor;
        private PackLoad _pack;
        private StoresDisplay _shed;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "haultest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _player = GameObject.Find("Player");
            _trip = Object.FindAnyObjectByType<TripController>();
            _interactor = _player.GetComponent<Interactor>();
            _pack = _player.GetComponent<PackLoad>();
            _shed = Object.FindAnyObjectByType<StoresDisplay>();
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            Assert.That(_interactor, Is.Not.Null);
            Assert.That(_pack, Is.Not.Null);
            Assert.That(_shed, Is.Not.Null);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DeleteSave()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANewGameStartsWithSuppliesInTheShed()
        {
            yield return null;
            Assert.That(_host.Sim.ItemCount("jerky"), Is.GreaterThan(0));
            Assert.That(_shed.PropCount, Is.GreaterThan(0), "The starting supplies should be visible in the shed.");
        }

        [UnityTest]
        public IEnumerator GatheringFillsThePackAndPicksTheSpotOver()
        {
            GatherSpot spot = NearestSpot("berries");
            yield return SetOutBeside(spot);

            Assert.That(_interactor.Current, Is.SameAs(spot), "The bush in front should be what E would use.");
            string result = _interactor.TryInteract();
            yield return null;

            Assert.That(result, Does.StartWith("+").And.Contain("Wild berries (pack)"));
            Assert.That(_host.Sim.PackCount("wild_berries"), Is.GreaterThan(0));
            Assert.That(spot.PickedOver, Is.True);
            Assert.That(spot.CanInteract, Is.False);
            Assert.That(spot.Prompt, Does.StartWith("Picked over"));
            Assert.That(spot.transform.Find("Berry_Bush").gameObject.activeSelf, Is.False, "The berries are gone.");
            Assert.That(spot.transform.Find("Bush_A").gameObject.activeSelf, Is.True, "A bare bush is left.");
        }

        [UnityTest]
        public IEnumerator APickedSpotStaysPickedAfterLoadingAndRegrowsLater()
        {
            GatherSpot spot = NearestSpot("deadfall");
            yield return SetOutBeside(spot);
            Assert.That(_interactor.TryInteract(), Does.Contain("Firewood"));
            _host.Save();
            _host.NewGame();
            yield return null;
            Assert.That(spot.PickedOver, Is.False, "A new game has nothing picked.");

            Assert.That(_host.Load(), Is.True);
            yield return null;
            Assert.That(spot.PickedOver, Is.True, "The save remembers the picked spot.");

            Assert.That(_host.Sim.Execute(new AdvanceDays { Days = 20 }).Ok, Is.True);
            yield return null;
            Assert.That(spot.PickedOver, Is.False);
            Assert.That(spot.transform.Find("Deadfall").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator AHeavyPackShowsAndSlowsYouDown()
        {
            yield return null;
            Assert.That(PackPiece("Pack_Base").activeSelf, Is.False, "No pack at home.");

            GatherSpot spot = NearestSpot("berries");
            yield return SetOutBeside(spot);
            Assert.That(PackPiece("Pack_Base").activeSelf, Is.True, "The pack is on your back on a trip.");
            Assert.That(_pack.SpeedMultiplier, Is.EqualTo(1f).Within(0.01f));

            Assert.That(_host.Sim.Execute(new GiveItem { ItemId = "firewood", Count = 9 }).Ok, Is.True); // 27 of 30
            yield return null;
            Assert.That(_pack.Fill, Is.GreaterThan(0.8f));
            Assert.That(PackPiece("Pack_Sack").activeSelf && PackPiece("Pack_Bundle").activeSelf, Is.True);
            Assert.That(_pack.SpeedMultiplier, Is.LessThan(1f));

            Assert.That(_host.Sim.Execute(new GiveItem { ItemId = "firewood", Count = 4 }).Ok, Is.True);
            yield return null;
            Assert.That(_host.Sim.Overloaded, Is.True);
            Assert.That(_pack.SpeedMultiplier, Is.EqualTo(0.55f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator BringingFirewoodHomeStacksItInTheShed()
        {
            yield return null;
            int before = _shed.PropsFor("firewood");
            GatherSpot spot = NearestSpot("deadfall");
            yield return SetOutBeside(spot);
            Assert.That(_host.Sim.Execute(new GiveItem { ItemId = "firewood", Count = 8 }).Ok, Is.True);

            yield return MoveTo(new Vector3(-6f, 0f, -6f));
            yield return null;
            Assert.That(_host.Sim.OnTrip, Is.False, "Walking into the yard ends the trip.");
            Assert.That(_shed.PropsFor("firewood"), Is.GreaterThan(before));
        }

        // ---- helpers -------------------------------------------------------------------------------

        private GatherSpot NearestSpot(string kind)
        {
            GatherSpot best = null;
            foreach (GatherSpot s in Object.FindObjectsByType<GatherSpot>(FindObjectsInactive.Exclude))
                if (s.SpotId.StartsWith(kind + "_") && (best == null || s.transform.position.sqrMagnitude < best.transform.position.sqrMagnitude)) best = s;
            Assert.That(best, Is.Not.Null, "No " + kind + " spot in the region.");
            return best;
        }

        private GameObject PackPiece(string name)
        {
            foreach (Transform t in _player.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            Assert.Fail("No " + name + " on the player.");
            return null;
        }

        // Stands the player just in front of a spot, facing it, sets out (it is outside the yard), and waits a frame.
        private IEnumerator SetOutBeside(GatherSpot spot)
        {
            Vector3 target = spot.transform.position;
            Vector3 from = target + new Vector3(0f, 0f, -1.4f);
            yield return MoveTo(from);
            _player.transform.rotation = Quaternion.LookRotation(new Vector3(target.x - from.x, 0f, target.z - from.z));
            yield return null;
            if (_trip.Current == TripController.Phase.Outfitting) Assert.That(_trip.ConfirmSetOut(), Is.True, _trip.Status);
            yield return null;
            yield return null;
        }

        private IEnumerator MoveTo(Vector3 p)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            float y = p.y;
            if (Physics.Raycast(new Vector3(p.x, 200f, p.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y + 0.05f;
            _player.transform.position = new Vector3(p.x, y, p.z);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return null;
        }
    }
}
