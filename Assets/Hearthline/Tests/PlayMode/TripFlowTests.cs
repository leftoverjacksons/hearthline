using System.Collections;
using System.Linq;
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
    /// M2 step 1 in the greybox region: where the player is, leaving home and outfitting, setting out with a
    /// pack, changing your mind at the gate, coming home, and the clock's days.
    /// </summary>
    public sealed class TripFlowTests
    {
        private SimHost _host;
        private GameObject _player;
        private PlayerLocation _location;
        private TripController _trip;
        private TravelClock _clock;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            Assert.That(_host, Is.Not.Null, "The region has no SimHost.");
            _host.SaveFileName = "triptest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _player = GameObject.Find("Player");
            _location = _player.GetComponent<PlayerLocation>();
            _trip = Object.FindAnyObjectByType<TripController>();
            _clock = Object.FindAnyObjectByType<TravelClock>();
            Assert.That(_location, Is.Not.Null);
            Assert.That(_trip, Is.Not.Null);
            Assert.That(_clock, Is.Not.Null);
            _clock.MinutesPerDay = 100000f; // keep real time from turning the day during a test
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayerStartsAtTheHomestead()
        {
            yield return null;
            Assert.That(_location.AtHome, Is.True);
            Assert.That(_location.PlaceName, Is.EqualTo("the homestead"));
            Assert.That(_location.AreaTags, Has.Member("homestead"));
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Home));
            Assert.That(_host.Sim.OnTrip, Is.False);
        }

        [UnityTest]
        public IEnumerator PlacesReportTheirAreaTagsNameAndDistance()
        {
            yield return MoveTo(0f, -48f);
            Assert.That(_location.PlaceName, Is.EqualTo("the guardrail bridge"));
            Assert.That(_location.AreaTags, Has.Member("creek").And.Member("river_crossing"));

            yield return MoveTo(170f, -92f);
            Assert.That(_location.PlaceName, Is.EqualTo("the fallen overpass"));
            Assert.That(_location.AreaTags, Has.Member("road").And.Member("ruin"));

            yield return MoveTo(-260f, 0f);
            Assert.That(_location.AreaTags, Has.Member("deep_woods"));
            Assert.That(_location.ThinLevel, Is.EqualTo(1));
            Assert.That(_location.DistanceFromHome, Is.EqualTo(260.0 / 300.0).Within(0.01));

            EventContext context = _location.ToEventContext(TimeOfDay.Dusk);
            Assert.That(context.AtHome, Is.False);
            Assert.That(context.PlaceName, Is.EqualTo("the deep hollow"));
            Assert.That(context.AreaTags, Has.Member("hollow"));
            Assert.That(context.TimeOfDay, Is.EqualTo(TimeOfDay.Dusk));
        }

        [UnityTest]
        public IEnumerator LeavingHomeOpensTheOutfitPanelAndHoldsThePlayer()
        {
            yield return MoveTo(0f, -92f);
            yield return null;
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Outfitting));
            Assert.That(_trip.PausesPlayer, Is.True);
            Assert.That(_player.GetComponent<PlayerInputRelease>().Released, Is.True);
            Assert.That(_host.Sim.OnTrip, Is.False, "Leaving the clearing alone must not start a trip.");
        }

        [UnityTest]
        public IEnumerator ChangingYourMindAtTheGatePutsThePlayerBackHome()
        {
            yield return MoveTo(0f, 0f);
            yield return MoveTo(0f, -92f);
            yield return null;
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Outfitting));

            _trip.StayHome();
            yield return null;
            yield return null;
            Assert.That(_location.AtHome, Is.True);
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Home));
            Assert.That(_host.Sim.OnTrip, Is.False);
        }

        [UnityTest]
        public IEnumerator SettingOutMovesThePackOutOfTheStores()
        {
            Assert.That(_host.Sim.Execute(new GiveItem { ItemId = "jerky", Count = 5 }).Ok, Is.True);
            yield return MoveTo(0f, -92f);
            yield return null;

            _trip.SetPacked("jerky", 3);
            Assert.That(_trip.ConfirmSetOut(), Is.True, _trip.Status);
            yield return null;

            Assert.That(_host.Sim.OnTrip, Is.True);
            Assert.That(_host.Sim.PackCount("jerky"), Is.EqualTo(3));
            Assert.That(_host.Sim.ItemCount("jerky"), Is.EqualTo(2));
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Away));
            Assert.That(_trip.PausesPlayer, Is.False);
        }

        [UnityTest]
        public IEnumerator ComingHomeEndsTheTripUnloadsAndReports()
        {
            Assert.That(_host.Sim.Execute(new GiveItem { ItemId = "jerky", Count = 4 }).Ok, Is.True);
            yield return MoveTo(0f, -92f);
            yield return null;
            _trip.SetPacked("jerky", 4);
            Assert.That(_trip.ConfirmSetOut(), Is.True, _trip.Status);
            yield return null;

            _clock.AdvanceHours(24f); // a day on the road
            yield return MoveTo(0f, 5f);
            yield return null;

            Assert.That(_host.Sim.OnTrip, Is.False);
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Arrived));
            Assert.That(_trip.ArrivalLines.First(), Is.EqualTo("Home after 1 day."));
            Assert.That(_trip.ArrivalLines.Any(l => l.StartsWith("Unloaded into the stores:")), Is.True, string.Join("\n", _trip.ArrivalLines));
            Assert.That(_host.Sim.ItemCount("jerky"), Is.GreaterThan(0), "What was carried comes back to the stores.");

            _trip.CloseArrival();
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Home));
        }

        [UnityTest]
        public IEnumerator MidnightAdvancesTheSimulationDay()
        {
            int day = _host.Sim.State.Day;
            _clock.AdvanceHours(24f);
            yield return null;
            Assert.That(_host.Sim.State.Day, Is.EqualTo(day + 1));
        }

        [UnityTest]
        public IEnumerator MakingCampSleepsUntilMorning()
        {
            _clock.AdvanceHours(13f - _clock.Hour); // 1 pm
            int day = _host.Sim.State.Day;
            _clock.MakeCamp();
            yield return null;
            Assert.That(_host.Sim.State.Day, Is.EqualTo(day + 1));
            Assert.That(_clock.Hour, Is.EqualTo(6f).Within(0.01f));
            Assert.That(_clock.TimeOfDay, Is.EqualTo(TimeOfDay.Dawn));
        }

        private IEnumerator MoveTo(float x, float z)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            float y = 2f;
            if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y + 0.05f;
            _player.transform.position = new Vector3(x, y, z);
            controller.enabled = true;
            Physics.SyncTransforms();
            _location.Refresh();
            yield return null;
        }
    }
}
