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
    /// Combat step 5: a fight begins with the quick-draw moment and ends by itself: won (loot on the fallen,
    /// a choice about the ones who gave up), got away, or downed (robbed, then lie still and let fate decide).
    /// </summary>
    public sealed class FightTests
    {
        private SimHost _host;
        private GameObject _player;
        private FightDirector _director;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "fighttest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            _player = GameObject.Find("Player");
            _director = Object.FindAnyObjectByType<FightDirector>();
            Assert.That(_director, Is.Not.Null);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private Simulation Sim => _host.Sim;

        private IEnumerator Fight(string encounter = "reavers_road")
        {
            Assert.That(Sim.Execute(new StartEncounter { EncounterId = encounter, PlaceName = "the homestead" }).Ok, Is.True);
            yield return null;
        }

        private void Down(Combatant x) =>
            Assert.That(Sim.Execute(new ShotHit { TargetId = x.CharacterId, Region = BodyRegion.Head, ShooterId = Sim.Head.Id, Distance = 10 }).Ok, Is.True);

        private static IEnumerator Until(System.Func<bool> condition, float seconds)
        {
            float t = 0f;
            while (!condition() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator AnAmbushOpensWithTheQuickDrawMoment()
        {
            yield return Fight();
            yield return null;
            Assert.That(Time.timeScale, Is.LessThan(0.6f));
            Assert.That(_director.InQuickDraw, Is.True);
            Assert.That(_player.GetComponent<PlayerWeapon>().Drawn, Is.EqualTo(GunVisuals.Drawn.LongGun), "The rifle comes up.");
            yield return Until(() => !_director.InQuickDraw, 4f);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator DowningThemAllWinsAndLeavesLootOnTheFallen()
        {
            yield return Fight();
            int fighters = Sim.Encounter.Combatants.Count;
            foreach (Combatant x in Sim.Encounter.Combatants.ToArray()) if (x.State == CombatantState.Fighting) Down(x);
            yield return Until(() => !Sim.InEncounter, 5f);
            Assert.That(Sim.InEncounter, Is.False, "The fight is called once nobody is fighting.");
            LootPickup[] pickups = Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude);
            Assert.That(pickups.Length, Is.EqualTo(fighters));

            int gear = Sim.Head.Gear.Count;
            string taken = pickups[0].Interact();
            Assert.That(taken, Does.Contain("Reaver's"), taken);
            Assert.That(Sim.Head.Gear.Count, Is.EqualTo(gear + 1));
            yield return null;
            Assert.That(Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude).Length, Is.EqualTo(fighters - 1));
        }

        [UnityTest]
        public IEnumerator WhenSomeGiveUpYouDecideWhatHappensToThem()
        {
            yield return Fight();
            Combatant[] all = Sim.Encounter.Combatants.ToArray();
            Down(all[0]);
            foreach (Combatant x in all.Skip(1))
                if (x.State == CombatantState.Fighting) Assert.That(Sim.Execute(new CombatantSurrenders { CharacterId = x.CharacterId }).Ok, Is.True);
            yield return Until(() => _director.PausesPlayer, 4f);
            Assert.That(_director.PausesPlayer, Is.True, "The surrender panel holds the player.");
            Assert.That(Sim.InEncounter, Is.True);
            _director.Resolve(SurrenderChoice.TakeGuns);
            yield return null;
            Assert.That(Sim.InEncounter, Is.False);
            Assert.That(_director.PausesPlayer, Is.False);
            Assert.That(Object.FindObjectsByType<LootPickup>(FindObjectsInactive.Exclude).Length, Is.EqualTo(all.Length), "Their guns are on the ground too.");
        }

        [UnityTest]
        public IEnumerator GoingDownEndsTheFightAndYouLieStill()
        {
            yield return Fight();
            Character head = Sim.Head;
            Assert.That(Sim.Execute(new ShotHit { TargetId = head.Id, Region = BodyRegion.Head, Distance = 10 }).Ok, Is.True);
            yield return Until(() => !Sim.InEncounter, 5f);
            Assert.That(Sim.InEncounter, Is.False);
            Assert.That(_director.PausesPlayer, Is.True, "Down and alone: the downed panel is up.");
            _director.LieStill();
            yield return null;
            Assert.That(!head.Alive || !head.Downed, Is.True, "Fate decided.");
            Assert.That(_director.PausesPlayer, Is.False);
        }

        [UnityTest]
        public IEnumerator PuttingDistanceBetweenYouGetsYouAway()
        {
            yield return Fight();
            yield return Until(() => !_director.InQuickDraw, 4f);
            var body = _player.GetComponent<CharacterController>();
            body.enabled = false;
            Vector3 far = _player.transform.position - _player.transform.forward * 150f;
            if (Physics.Raycast(new Vector3(far.x, 200f, far.z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)) far.y = hit.point.y + 0.05f;
            _player.transform.position = far;
            body.enabled = true;
            yield return Until(() => !Sim.InEncounter, 8f);
            Assert.That(Sim.InEncounter, Is.False, "Far enough, long enough.");
        }
    }
}
