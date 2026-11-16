using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class EventTests
    {
        private static EventContext Creek => new EventContext { AreaTags = new List<string> { "creek" }, TimeOfDay = TimeOfDay.Dusk, PlaceName = "Miller's Ford", DistanceFromHome = 0.5 };

        private static Simulation Started(string eventId, ulong seed = 1, EventContext where = null)
        {
            Simulation sim = TestSupport.NewSim(seed);
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = eventId, Context = where ?? Creek }));
            return sim;
        }

        private static bool Has<T>(CommandResult result) where T : DomainEvent
        {
            foreach (DomainEvent e in result.Events) if (e is T) return true;
            return false;
        }

        [Test]
        public void StartingAnEventCastsItAndWritesNamesIntoTheText()
        {
            Simulation sim = Started("wounded_stranger_creek");
            ActiveEventView view = sim.GetActiveEvent();
            Character stranger = sim.FindCharacter(view.Cast["stranger"]);

            Assert.That(view.Cast["hero"], Is.EqualTo(sim.Head.Id));
            Assert.That(stranger.Archetype, Is.EqualTo("traveler"));
            Assert.That(sim.State.Household.MemberIds, Does.Not.Contain(stranger.Id));
            Assert.That(view.Text, Does.StartWith(stranger.GivenName + " is slumped against a cairn"));
            Assert.That(view.Text, Does.Not.Contain("{"));
            Assert.That(view.Choices.Count, Is.EqualTo(3));
        }

        [Test]
        public void StrangersAreNotWrittenIntoTheFamilyChronicle()
        {
            Simulation sim = Started("peddler_on_the_road");
            Character peddler = sim.FindCharacter(sim.GetActiveEvent().Cast["peddler"]);
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 1 }));
            TestSupport.Ok(sim.Execute(new SetAge { CharacterId = peddler.Id, AgeYears = 70 }));
            TestSupport.Ok(sim.Execute(new AdvanceDays { Days = 2 }));
            TestSupport.Ok(sim.Execute(new KillCharacter { CharacterId = peddler.Id, Cause = "fever" }));

            Assert.That(peddler.Marks.Count, Is.GreaterThan(0), "people in the world still age");
            Assert.That(sim.ChronicleOf(peddler), Is.Empty);
            Assert.That(sim.Head, Is.Not.Null, "a stranger's death does not touch the household");
        }

        [Test]
        public void ChoicesAreGatedByWhatTheHouseholdHas()
        {
            Simulation sim = Started("wounded_stranger_creek");
            EventChoiceView bind = sim.GetActiveEvent().Choices[0];
            Assert.That(bind.Available, Is.False);
            Assert.That(bind.UnavailableReason, Is.EqualTo("needs bandage or medkit"));
            TestSupport.Rejected(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }), "not available");
            TestSupport.Rejected(sim.Execute(new ChooseEventOption { ChoiceIndex = 7 }), "no choice");

            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "bandage" }));
            Assert.That(sim.GetActiveEvent().Choices[0].Available, Is.True);
        }

        [Test]
        public void RobbingTheStrangerHasConsequences()
        {
            int guilt = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Simulation sim = Started("wounded_stranger_creek", seed);
                int strangerId = sim.GetActiveEvent().Cast["stranger"];
                Character stranger = sim.FindCharacter(strangerId);
                CommandResult result = sim.Execute(new ChooseEventOption { ChoiceIndex = 1 });
                TestSupport.Ok(result);

                Assert.That(sim.GetActiveEvent(), Is.Null);
                Assert.That(Has<EventEnded>(result), Is.True);
                Assert.That(sim.ReputationWith("homesteaders"), Is.EqualTo(-5));
                Assert.That(stranger.Opinions[sim.Head.Id], Is.EqualTo(-60));
                Assert.That(sim.State.Household.Inventory.Count, Is.GreaterThan(0), "the pack had something in it");
                if (sim.Head.Marks.Count == 0) continue;
                guilt++;
                Mark mark = sim.Head.Marks[0];
                Assert.That(mark.DefId, Is.EqualTo("rite_guilt_shadow"));
                Assert.That(mark.Origin.EventId, Is.EqualTo("wounded_stranger_creek"));
                Assert.That(mark.OriginText, Is.EqualTo("Carried since robbing " + stranger.GivenName + " " + stranger.Surname + " at Miller's Ford, spring of year 1"));
            }
            Assert.That(guilt, Is.InRange(4, 25), "the guilt mark has a 0.2 chance");
        }

        [Test]
        public void HelpingTheStrangerEndsInARecruitOrAnAmbush()
        {
            int recruits = 0, ambushes = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                Simulation sim = Started("wounded_stranger_creek", seed);
                int strangerId = sim.GetActiveEvent().Cast["stranger"];
                TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "bandage" }));
                CommandResult result = sim.Execute(new ChooseEventOption { ChoiceIndex = 0 });
                TestSupport.Ok(result);
                Assert.That(sim.ItemCount("bandage"), Is.EqualTo(0));

                if (Has<AmbushRequested>(result)) { ambushes++; Assert.That(sim.State.Household.MemberIds, Does.Not.Contain(strangerId)); }
                else
                {
                    recruits++;
                    Assert.That(sim.State.Household.MemberIds, Does.Contain(strangerId));
                    Assert.That(sim.FindCharacter(strangerId).Bond, Is.EqualTo(HouseholdBond.Sworn));
                    Assert.That(sim.ChronicleOf(sim.Head).Exists(e => e.Kind == ChronicleKind.Deed && e.Text.StartsWith("Ada bound up")), Is.True);
                }
            }
            Assert.That(recruits, Is.GreaterThan(ambushes));
            Assert.That(ambushes, Is.GreaterThan(5));
        }

        [Test]
        public void AnEventCanRunThroughSeveralBeats()
        {
            Simulation sim = Started("peddler_on_the_road");
            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "old_coin", Count = 1 }));
            CommandResult result = sim.Execute(new ChooseEventOption { ChoiceIndex = 0 });
            TestSupport.Ok(result);
            Assert.That(Has<EventBeatEntered>(result), Is.True);
            Assert.That(sim.GetActiveEvent().BeatId, Is.EqualTo("wares"));

            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            Assert.That(sim.GetActiveEvent(), Is.Null);
            Assert.That(sim.ItemCount("old_coin"), Is.EqualTo(0));
            Assert.That(sim.ItemCount("bandage"), Is.EqualTo(2));
        }

        [Test]
        public void EventsCanWound()
        {
            int wounded = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = Started("boar_in_the_corn", seed);
                TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
                Assert.That(sim.ItemCount("boar_meat"), Is.EqualTo(2));
                if (sim.Head.Marks.Count == 0) continue;
                wounded++;
                Assert.That(sim.Head.Marks[0].DefId, Is.EqualTo("wound_claw"));
                Assert.That(sim.Head.Marks[0].OriginText, Is.EqualTo("Clawed by the boar at Miller's Ford, spring of year 1"));
                Assert.That(sim.Head.Health, Is.EqualTo(1));
            }
            Assert.That(wounded, Is.InRange(8, 26));
        }

        [Test]
        public void TimeDoesNotPassInTheMiddleOfAnEvent()
        {
            Simulation sim = Started("wounded_stranger_creek");
            TestSupport.Rejected(sim.Execute(new AdvanceDays { Days = 1 }), "finish the event");
            TestSupport.Rejected(sim.Execute(new PassSeason()), "finish the event");
            TestSupport.Rejected(sim.Execute(new StartEvent { EventId = "boar_in_the_corn" }), "already in progress");
        }

        [Test]
        public void AnEventThatNeedsSomeoneElseCannotStartAlone()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Rejected(sim.Execute(new StartEvent { EventId = "fever_in_the_house" }), "nobody fits");
            TestSupport.Rejected(sim.Execute(new StartEvent { EventId = "no_such_event" }), "no event");
            TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = "Levi", AgeYears = 30 }));
            TestSupport.Ok(sim.Execute(new StartEvent { EventId = "fever_in_the_house" }));
            Assert.That(sim.GetActiveEvent().Text, Does.StartWith("Levi did not come to breakfast"));
        }

        [Test]
        public void AnEventInProgressSurvivesSaveAndLoad()
        {
            Simulation sim = Started("peddler_on_the_road", 4);
            TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = 0 }));
            Simulation loaded = Simulation.FromJson(sim.ToJson(), TestSupport.Content);
            Assert.That(loaded.GetActiveEvent().BeatId, Is.EqualTo("wares"));
            Assert.That(loaded.GetActiveEvent().Text, Is.EqualTo(sim.GetActiveEvent().Text));
            foreach (Simulation s in new[] { sim, loaded }) TestSupport.Ok(s.Execute(new ChooseEventOption { ChoiceIndex = 2 }));
            Assert.That(loaded.ToJson(), Is.EqualTo(sim.ToJson()));
        }

        [Test]
        public void ItemsAndReputationCanBeChangedDirectly()
        {
            Simulation sim = TestSupport.NewSim();
            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "jerky", Count = 3 }));
            TestSupport.Ok(sim.Execute(new GiveItem { ItemId = "jerky", Count = -2 }));
            Assert.That(sim.ItemCount("jerky"), Is.EqualTo(1));
            TestSupport.Rejected(sim.Execute(new GiveItem { ItemId = "jerky", Count = -5 }), "only has 1");
            TestSupport.Rejected(sim.Execute(new GiveItem { ItemId = "gold_bar" }), "no item");

            TestSupport.Ok(sim.Execute(new ChangeReputation { FactionId = "reavers", Delta = -250 }));
            Assert.That(sim.ReputationWith("reavers"), Is.EqualTo(-100), "reputation is clamped");
            TestSupport.Rejected(sim.Execute(new ChangeReputation { FactionId = "nobody", Delta = 1 }), "no faction");
        }

        /// <summary>Walks every event to its end many times, taking random available choices. Nothing may throw or hang.</summary>
        [Test]
        public void EveryEventCanBePlayedToItsEndWhateverIsChosen()
        {
            var picker = new SimRandom(SimRandom.Seed(99));
            foreach (string eventId in TestSupport.Content.Events.Keys)
            {
                for (ulong seed = 1; seed <= 25; seed++)
                {
                    Simulation sim = TestSupport.NewSim(seed);
                    TestSupport.Ok(sim.Execute(new AddHouseholdMember { GivenName = "Levi", AgeYears = 30 }));
                    if (seed % 2 == 0)
                        foreach (string item in TestSupport.Content.Items.Keys) sim.Execute(new GiveItem { ItemId = item, Count = 5 });
                    TestSupport.Ok(sim.Execute(new StartEvent { EventId = eventId, Context = Creek }));

                    int steps = 0;
                    while (sim.GetActiveEvent() != null)
                    {
                        Assert.That(++steps, Is.LessThan(20), eventId + " does not end");
                        List<EventChoiceView> open = sim.GetActiveEvent().Choices.FindAll(c => c.Available);
                        Assert.That(open, Is.Not.Empty, eventId + " left the player with no choice");
                        TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = open[picker.NextInt(0, open.Count)].Index }));
                    }
                    Assert.That(sim.ToJson(), Is.EqualTo(Simulation.FromJson(sim.ToJson(), TestSupport.Content).ToJson()));
                }
            }
        }
    }

    public class StorytellerTests
    {
        private static EventContext Road => new EventContext { AreaTags = new List<string> { "road" }, TimeOfDay = TimeOfDay.Day, DistanceFromHome = 0.5 };

        /// <summary>Ticks the storyteller, resolving each event with its last choice, and returns what fired per tick ("" for quiet).</summary>
        private static List<string> Run(ulong seed, EventContext where, int ticks, bool advanceDays = true)
        {
            Simulation sim = TestSupport.NewSim(seed);
            var fired = new List<string>();
            for (int i = 0; i < ticks; i++)
            {
                TestSupport.Ok(sim.Execute(new StorytellerTick { Context = where }));
                ActiveEventView view = sim.GetActiveEvent();
                fired.Add(view == null ? "" : view.EventId);
                while (sim.GetActiveEvent() != null)
                {
                    List<EventChoiceView> open = sim.GetActiveEvent().Choices.FindAll(c => c.Available);
                    TestSupport.Ok(sim.Execute(new ChooseEventOption { ChoiceIndex = open[open.Count - 1].Index }));
                }
                if (advanceDays && sim.Head != null && !sim.Head.Downed) sim.Execute(new AdvanceDays { Days = 1 });
            }
            return fired;
        }

        [Test]
        public void OnlyEventsThatFitThePlaceAndTimeAreOffered()
        {
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++) seen.UnionWith(Run(seed, Road, 60));
            seen.Remove("");
            Assert.That(seen, Is.EquivalentTo(new[] { "peddler_on_the_road", "continuity_patrol", "child_on_the_road", "road_work_party" }));
        }

        [Test]
        public void HomeEventsOnlyHappenAtHome()
        {
            var home = new EventContext { AreaTags = new List<string> { "homestead" }, TimeOfDay = TimeOfDay.Night, AtHome = true };
            var seen = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++) seen.UnionWith(Run(seed, home, 60));
            seen.Remove("");
            Assert.That(seen, Is.EquivalentTo(new[] { "seeker_at_the_gate", "brand_night", "night_raid" }), "the others need a second household member, a guard, or low morale");
        }

        [Test]
        public void QuietStretchesSeparateEvents()
        {
            List<string> fired = Run(5, Road, 200);
            int events = fired.FindAll(f => f != "").Count;
            Assert.That(events, Is.InRange(15, 110));
            Assert.That(fired[0], Is.EqualTo(""), "nothing fires before the budget has built up");
            for (int i = 1; i < fired.Count; i++)
                Assert.That(fired[i] != "" && fired[i - 1] != "", Is.False, "two events fired back to back at tick " + i);
        }

        [Test]
        public void CooldownsKeepAnEventFromRepeating()
        {
            // No days pass, so nothing with a cooldown may fire twice.
            List<string> fired = Run(8, Road, 300, advanceDays: false).FindAll(f => f != "");
            Assert.That(fired.Count, Is.GreaterThan(0));
            Assert.That(new HashSet<string>(fired).Count, Is.EqualTo(fired.Count));
        }

        [Test]
        public void TheThinAndDistanceMakeThingsHappenMoreOften()
        {
            int Count(EventContext where)
            {
                int n = 0;
                for (ulong seed = 1; seed <= 10; seed++) n += Run(seed, where, 80).FindAll(f => f != "").Count;
                return n;
            }
            var near = new EventContext { AreaTags = new List<string> { "woods" }, TimeOfDay = TimeOfDay.Dusk, DistanceFromHome = 0 };
            var far = new EventContext { AreaTags = new List<string> { "woods" }, TimeOfDay = TimeOfDay.Dusk, DistanceFromHome = 1, ThinLevel = 2 };
            Assert.That(Count(far), Is.GreaterThan(Count(near)));
        }

        [Test]
        public void TheSameSeedTellsTheSameStoryAndDifferentSeedsDoNot()
        {
            Assert.That(Run(21, Road, 80), Is.EqualTo(Run(21, Road, 80)));
            var stories = new HashSet<string>();
            for (ulong seed = 1; seed <= 5; seed++) stories.Add(string.Join(",", Run(seed, Road, 80)));
            Assert.That(stories.Count, Is.EqualTo(5), "five playthroughs of the same trip should differ");
        }
    }
}
