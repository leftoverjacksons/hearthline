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
    /// M2 step 2: the storyteller in the world. Events start from where the player stands, their beats are
    /// played through choices, marks name the place, and days wait while an event is open.
    /// </summary>
    public sealed class RoadEventTests
    {
        private SimHost _host;
        private GameObject _player;
        private PlayerLocation _location;
        private TripController _trip;
        private TravelClock _clock;
        private RoadEvents _events;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "eventtest_" + System.Guid.NewGuid().ToString("N") + ".json";
            _player = GameObject.Find("Player");
            _location = _player.GetComponent<PlayerLocation>();
            _trip = Object.FindAnyObjectByType<TripController>();
            _clock = Object.FindAnyObjectByType<TravelClock>();
            _events = Object.FindAnyObjectByType<RoadEvents>();
            Assert.That(_events, Is.Not.Null, "The region has no RoadEvents.");
            _clock.MinutesPerDay = 100000f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnEventStartedHereShowsItsTextAndChoices()
        {
            yield return SetOutTo(-150f, 10f); // Laurel Woods
            Assert.That(_events.StartHere("grave_with_your_name"), Is.Null);
            yield return null;

            ActiveEventView view = _host.Sim.GetActiveEvent();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Text, Does.Contain("Ward"), "The hero's surname should be cast into the text.");
            Assert.That(view.Choices.Count, Is.EqualTo(3));
            Assert.That(_events.PausesPlayer, Is.True);
            Assert.That(_player.GetComponent<PlayerInputRelease>().Released, Is.True);
        }

        [UnityTest]
        public IEnumerator ChoosingPlaysTheEventThroughAndSummarisesIt()
        {
            yield return SetOutTo(-150f, 10f);
            Assert.That(_events.StartHere("grave_with_your_name"), Is.Null);
            yield return null;

            Assert.That(_events.Choose(1), Is.Null); // Dig
            yield return null;

            Assert.That(_host.Sim.State.ActiveEvent, Is.Null);
            Assert.That(_events.ShowingSummary, Is.True);
            Assert.That(_events.Summary.Any(s => s.Contains("Old coin") && s.Contains("pack")), Is.True, string.Join("\n", _events.Summary));
            Assert.That(_host.Sim.PackCount("old_coin"), Is.EqualTo(3));

            // The mark names the place the player was standing in.
            MarkView mark = _host.Sim.MarkViewsOf(_host.Sim.Head).Single(m => m.DefId == "rite_guilt_shadow");
            Assert.That(mark.OriginText, Does.Contain("Laurel Woods"));
            Assert.That(_events.Summary.Any(s => s.Contains(mark.OriginText)), Is.True);

            _events.CloseSummary();
            Assert.That(_events.PausesPlayer, Is.False);
        }

        [UnityTest]
        public IEnumerator DaysWaitWhileAnEventIsOpen()
        {
            yield return SetOutTo(-150f, 10f);
            int day = _host.Sim.State.Day;
            Assert.That(_events.StartHere("grave_with_your_name"), Is.Null);
            yield return null;

            _clock.AdvanceHours(24f);
            yield return null;
            Assert.That(_host.Sim.State.Day, Is.EqualTo(day), "No day may pass in the middle of an event.");

            _events.Choose(2); // Leave it be
            yield return null;
            yield return null;
            Assert.That(_host.Sim.State.Day, Is.EqualTo(day + 1), "The owed day passes once the event is over.");
        }

        [UnityTest]
        public IEnumerator TravellingGivesTheStorytellerTurns()
        {
            yield return SetOutTo(-150f, 10f);
            int turns = _events.Turns;
            // Cover 160 m north in 8 m steps (tree trunks would stop a real walk through the woods).
            var controller = _player.GetComponent<CharacterController>();
            for (int i = 0; i < 20 && _host.Sim.State.ActiveEvent == null && !_events.ShowingSummary; i++)
            {
                controller.enabled = false;
                _player.transform.position += Vector3.forward * 8f;
                controller.enabled = true;
                yield return null;
            }
            Assert.That(_events.Turns, Is.GreaterThan(turns), "Travelling 160 m should give the storyteller at least one turn.");
        }

        [UnityTest]
        public IEnumerator ANightInCampGivesTheStorytellerATurn()
        {
            yield return SetOutTo(-150f, 10f);
            int turns = _events.Turns;
            _clock.MakeCamp();
            yield return null;
            yield return null;
            Assert.That(_events.Turns, Is.EqualTo(turns + 1));
        }

        // Teleports out of the homestead and sets out with an empty pack.
        private IEnumerator SetOutTo(float x, float z)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            float y = 2f;
            if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) y = hit.point.y + 0.05f;
            _player.transform.position = new Vector3(x, y, z);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.That(_trip.Current, Is.EqualTo(TripController.Phase.Outfitting));
            Assert.That(_trip.ConfirmSetOut(), Is.True, _trip.Status);
            yield return null;
            Assert.That(_location.PlaceName, Is.EqualTo("Laurel Woods"));
        }
    }
}
