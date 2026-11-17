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
    /// Combat step 6: events that end in an ambush start a real fight where the player stands, led by the
    /// person the event cast. Animal ambushes are still only reported.
    /// </summary>
    public sealed class AmbushEventTests
    {
        private SimHost _host;
        private RoadEvents _events;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "ambusheventtest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _events = Object.FindAnyObjectByType<RoadEvents>();
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private void Pick(string label)
        {
            ActiveEventView view = _host.Sim.GetActiveEvent();
            EventChoiceView choice = view.Choices.First(c => c.Label == label);
            Assert.That(_events.Choose(choice.Index), Is.Null);
        }

        [UnityTest]
        public IEnumerator DrawingAtTheTollStartsTheFightLedByTheTollman()
        {
            Assert.That(_events.StartHere("reaver_toll"), Is.Null);
            int tollman = _host.Sim.State.ActiveEvent.Cast["reaver"];
            Pick("Refuse");
            Pick("Draw");
            yield return null;
            yield return null;
            Assert.That(_host.Sim.InEncounter, Is.True, "The fight starts once the event is over.");
            Assert.That(_events.ShowingSummary, Is.False, "No summary to click through: it's a fight.");
            Assert.That(_host.Sim.Encounter.EncounterId, Is.EqualTo("reavers_road"));
            Assert.That(_host.Sim.Encounter.Combatants.Single(x => x.Leader).CharacterId, Is.EqualTo(tollman));
            Assert.That(Object.FindAnyObjectByType<EnemySpawner>().Bodies.ContainsKey(tollman), Is.True, "The tollman is there in person.");
        }

        [UnityTest]
        public IEnumerator AWolfAmbushIsStillOnlyReported()
        {
            Assert.That(_events.StartHere("wolves_at_dusk"), Is.Null);
            Pick("Put your back to a tree and wait for them");
            yield return null;
            yield return null;
            Assert.That(_host.Sim.InEncounter, Is.False);
            Assert.That(_events.ShowingSummary, Is.True);
            Assert.That(_events.Summary.Any(s => s.Contains("wolf pack")), Is.True, string.Join(" / ", _events.Summary));
        }
    }
}
