using System.Collections;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Game;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hearthline.Tests.PlayMode
{
    /// <summary>
    /// Ruins and places in the region (docs/specs/ruins-and-places.md): each place has its area and spots,
    /// walking in discovers it, searching stays searched across a reload, the find is taken once, and the
    /// Elder Works holds its breath.
    /// </summary>
    public sealed class PlacePlayTests
    {
        private SimHost _host;
        private GameObject _player;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "placetest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private static AreaVolume PlaceArea(string kind) => AreaVolume.All.First(a => a.PlaceKind == kind);

        private IEnumerator MoveTo(Vector3 p)
        {
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            if (Physics.Raycast(new Vector3(p.x, p.y + 100f, p.z), Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y + 0.05f;
            _player.transform.position = p;
            body.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
        }

        [Test]
        public void EveryPlaceHasItsAreaAndSpots()
        {
            var places = AreaVolume.All.Where(a => a.PlaceId.Length > 0).ToList();
            Assert.That(places.Select(a => a.PlaceKind).Distinct().Count(), Is.GreaterThanOrEqualTo(6), "The ruin kinds, the overpass and the Elder Works.");
            Assert.That(places.Any(a => a.PlaceKind == "elder_works"), Is.True);
            foreach (AreaVolume area in places)
            {
                PlaceDef def = _host.Content.Places[area.PlaceKind];
                Assert.That(area.PlaceName, Is.EqualTo(def.Name));
                Assert.That(area.ThinLevel, Is.EqualTo(def.ThinLevel));
                var spots = PlaceSpot.At(area.PlaceId);
                Assert.That(spots.Count(s => !s.IsFind), Is.InRange(1, def.SearchMax), area.PlaceId + " has search spots.");
                Assert.That(spots.Any(s => s.IsFind), Is.EqualTo(def.Find != null), area.PlaceId + " has its find if it should.");
                foreach (PlaceSpot s in spots) Assert.That(area.Contains(s.transform.position + Vector3.up * 0.5f), Is.True, s.name + " at " + area.PlaceId + " is inside its place.");
            }
        }

        [UnityTest]
        public IEnumerator WalkingInDiscoversThePlaceAndShowsItsName()
        {
            AreaVolume chapel = AreaVolume.All.First(a => a.PlaceKind.Length > 0 && a.PlaceKind != "elder_works" && a.PlaceKind != "overpass");
            Assert.That(_host.Sim.IsDiscovered(chapel.PlaceId), Is.False);
            yield return MoveTo(PlaceSpot.At(chapel.PlaceId).First().transform.position + Vector3.right);
            for (float t = 0f; t < 1f && !_host.Sim.IsDiscovered(chapel.PlaceId); t += Time.deltaTime) yield return null;
            Assert.That(_host.Sim.IsDiscovered(chapel.PlaceId), Is.True);
            Assert.That(Object.FindAnyObjectByType<PlaceDiscovery>().Title.ToLowerInvariant(), Is.EqualTo(chapel.PlaceName.ToLowerInvariant()));
            Assert.That(_host.Sim.State.Chronicle.Last().Text, Does.Contain("first came to " + chapel.PlaceName));
        }

        [UnityTest]
        public IEnumerator SearchingFillsThePackAndStaysSearchedAfterAReload()
        {
            Assert.That(_host.Sim.Execute(new SetOut()).Ok, Is.True);
            AreaVolume area = AreaVolume.All.First(a => a.PlaceKind == "feed_store" || a.PlaceKind == "filling_station" || a.PlaceKind == "chapel");
            PlaceSpot spot = PlaceSpot.At(area.PlaceId).First(s => !s.IsFind);
            yield return MoveTo(spot.transform.position + Vector3.right);
            int before = _host.Sim.State.Trip.Pack.Values.Sum();
            string said = spot.Interact();
            Assert.That(said, Does.Contain("(pack)").Or.Contain("Nothing worth carrying"));
            Assert.That(spot.Done, Is.True);
            Assert.That(spot.CanInteract, Is.False);
            Assert.That(_host.Sim.State.Trip.Pack.Values.Sum(), Is.GreaterThanOrEqualTo(before));

            _host.Save();
            Assert.That(_host.Load(), Is.True);
            yield return null;
            Assert.That(_host.Sim.IsSearched(area.PlaceId, spot.Spot), Is.True);
            Assert.That(spot.Done, Is.True, "Still gone through after loading.");
            Assert.That(spot.transform.Find("Gone through") != null && spot.transform.Find("Gone through").gameObject.activeSelf, Is.True, "And looks it.");
        }

        [UnityTest]
        public IEnumerator TheFindIsTakenOnce()
        {
            AreaVolume area = PlaceArea("feed_store");
            PlaceSpot find = PlaceSpot.At(area.PlaceId).Single(s => s.IsFind);
            yield return MoveTo(find.transform.position + Vector3.right);
            Assert.That(find.Interact(), Does.Contain("crate of shells"));
            Assert.That(find.Done, Is.True);
            Assert.That(_host.Sim.Execute(new TakeFind { PlaceId = area.PlaceId, Kind = "feed_store" }).Ok, Is.False);
        }

        [UnityTest]
        public IEnumerator TheElderWorksHoldsItsBreath()
        {
            AreaVolume elder = PlaceArea("elder_works");
            yield return MoveTo(elder.transform.position + elder.transform.forward * 12f);
            var location = _player.GetComponent<PlayerLocation>();
            location.Refresh();
            Assert.That(location.PlaceKind, Is.EqualTo("elder_works"));
            Assert.That(location.ThinLevel, Is.GreaterThanOrEqualTo(2));
            var aura = Object.FindAnyObjectByType<ThinAura>();
            var life = Object.FindAnyObjectByType<AmbientLife>();
            for (float t = 0f; t < 5f && aura.Hush < 0.95f; t += Time.deltaTime) yield return null;
            Assert.That(aura.Hush, Is.GreaterThan(0.95f));
            Assert.That(Object.FindAnyObjectByType<WeatherView>().WindBoost, Is.LessThan(0.05f), "The wind drops.");
            Assert.That(aura.HumVolume, Is.GreaterThan(0.1f), "A hum comes up.");
            Assert.That(life.Density, Is.EqualTo(0f), "No birds.");
        }

        [UnityTest]
        public IEnumerator ARumourPointsTheWay()
        {
            Assert.That(_host.Sim.Execute(new FollowRumour { Kind = "filling_station" }).Ok, Is.True);
            string where = PlaceDiscovery.Bearing("filling_station", _player.transform.position);
            Assert.That(where, Does.Match(@"^\d+ m (north|south|east|west)"));
            yield return null;
        }
    }
}
