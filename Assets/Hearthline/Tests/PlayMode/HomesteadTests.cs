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
    /// The working homestead: buildings show their real tier and can be built up on the spot, the dwelling's
    /// bed ends the day, the infirmary treats wounds, and household members stand where their job is.
    /// </summary>
    public sealed class HomesteadTests
    {
        private SimHost _host;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            SceneManager.LoadScene("M2_Region");
            yield return null;
            _host = SimHost.Instance;
            _host.SaveFileName = "hometest_" + System.Guid.NewGuid().ToString("N") + ".json";
            Object.FindAnyObjectByType<TravelClock>().MinutesPerDay = 100000f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DeleteSave()
        {
            if (_host != null && System.IO.File.Exists(_host.SavePath)) System.IO.File.Delete(_host.SavePath);
            yield return null;
        }

        private static HomeBuilding Building(HomeBuilding.Kind kind) =>
            Object.FindObjectsByType<HomeBuilding>(FindObjectsInactive.Exclude).Single(b => b.name == "Use " + kind.ToString().ToLowerInvariant());

        [UnityTest]
        public IEnumerator BuildingsShowTheTierTheSimulationSays()
        {
            yield return null;
            StructureView dwelling = Building(HomeBuilding.Kind.Dwelling).View;
            Assert.That(dwelling.Tier, Is.EqualTo(1), "A new game starts in a tent.");
            Assert.That(dwelling.Current.name, Is.EqualTo("Dwelling_Tent"));
            Assert.That(dwelling.transform.Find("Cabin").gameObject.activeSelf, Is.False);

            StructureView infirmary = Building(HomeBuilding.Kind.Infirmary).View;
            Assert.That(infirmary.Tier, Is.EqualTo(0));
            Assert.That(infirmary.Current.name, Is.EqualTo("Build_Site"));
        }

        [UnityTest]
        public IEnumerator BuildingTheCabinSwapsTheModelAndSurvivesALoad()
        {
            HomeBuilding dwelling = Building(HomeBuilding.Kind.Dwelling);
            yield return null;
            Assert.That(dwelling.Shortfall(), Is.Null, "The starting lumber should pay for the cabin.");
            int lumber = _host.Sim.ItemCount("lumber");

            Assert.That(dwelling.Build(), Is.EqualTo("Built the cabin."));
            yield return null;
            Assert.That(dwelling.View.Current.name, Is.EqualTo("Cabin"));
            Assert.That(dwelling.View.Current.activeSelf, Is.True);
            Assert.That(_host.Sim.ItemCount("lumber"), Is.EqualTo(lumber - 10));

            _host.Save();
            _host.NewGame();
            yield return null;
            Assert.That(dwelling.View.Current.name, Is.EqualTo("Dwelling_Tent"));
            Assert.That(_host.Load(), Is.True);
            yield return null;
            Assert.That(dwelling.View.Current.name, Is.EqualTo("Cabin"));
        }

        [UnityTest]
        public IEnumerator ABuildYouCannotAffordSaysWhatIsMissing()
        {
            HomeBuilding dwelling = Building(HomeBuilding.Kind.Dwelling);
            yield return null;
            Assert.That(dwelling.Build(), Is.EqualTo("Built the cabin."));
            // The farmhouse needs 24 lumber and 10 coin; the stores have 2 lumber and no coin.
            Assert.That(dwelling.Shortfall(), Does.Contain("more lumber").And.Contain("more old coin"));
            string result = dwelling.Build();
            Assert.That(result, Does.Contain("Farmhouse needs"));
            Assert.That(dwelling.View.Current.name, Is.EqualTo("Cabin"));
        }

        [UnityTest]
        public IEnumerator SleepingInTheDwellingEndsTheDay()
        {
            int day = _host.Sim.State.Day;
            Building(HomeBuilding.Kind.Dwelling).Sleep();
            yield return null;
            yield return null;
            Assert.That(_host.Sim.State.Day, Is.EqualTo(day + 1));
            Assert.That(Object.FindAnyObjectByType<TravelClock>().Hour, Is.EqualTo(6f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TheInfirmaryTreatsAWoundOnceItIsBuilt()
        {
            HomeBuilding infirmary = Building(HomeBuilding.Kind.Infirmary);
            Character head = _host.Sim.Head;
            Assert.That(_host.Sim.Execute(new InflictHit { CharacterId = head.Id, Region = BodyRegion.ArmL, Damage = DamageType.Blade, Severity = 1, Origin = new MarkOrigin { Cause = "a test" } }).Ok, Is.True);
            int wound = _host.Sim.MarkViewsOf(head).Single(m => m.Category == MarkCategory.Wound).MarkId;
            yield return null;

            Assert.That(infirmary.Treat(head.Id, wound), Does.Contain("nowhere to treat"));
            Assert.That(infirmary.Build(), Is.EqualTo("Built the cot."));
            yield return null;
            Assert.That(infirmary.View.Current.name, Is.EqualTo("Infirmary_Cot"));
            Assert.That(infirmary.Treat(head.Id, wound), Does.StartWith("Treated"));
        }

        [UnityTest]
        public IEnumerator HouseholdMembersStandWhereTheirJobIs()
        {
            var figures = Object.FindAnyObjectByType<HouseholdFigures>();
            var board = Object.FindAnyObjectByType<JobBoard>();
            yield return null;
            Assert.That(figures.Count, Is.EqualTo(0), "Nobody else lives here at the start.");

            Assert.That(_host.Sim.Execute(new AddHouseholdMember { GivenName = "Mara", Surname = "Pruitt", AgeYears = 30, Bond = HouseholdBond.Sworn }).Ok, Is.True);
            Character mara = _host.Sim.State.Characters.Last();
            yield return null;
            Assert.That(figures.Count, Is.EqualTo(1));

            Assert.That(board.Assign(mara.Id, "woodcutter"), Is.Null);
            yield return null;
            GameObject figure = figures.FigureOf(mara.Id);
            Vector3 woodcutterSpot = GameObject.Find("Spot woodcutter").transform.position;
            Assert.That(Vector3.Distance(figure.transform.position, woodcutterSpot), Is.LessThan(0.5f));

            Assert.That(board.Assign(_host.Sim.Head.Id, "cook"), Does.Contain("head of household"));
        }
    }
}
