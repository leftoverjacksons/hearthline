using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new SimRandom(SimRandom.Seed(42));
            var b = new SimRandom(SimRandom.Seed(42));
            for (int i = 0; i < 100; i++) Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new SimRandom(SimRandom.Seed(1));
            var b = new SimRandom(SimRandom.Seed(2));
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextUInt() == b.NextUInt()) same++;
            Assert.That(same, Is.LessThan(5));
        }

        [Test]
        public void SequenceResumesFromCopiedState()
        {
            RngState state = SimRandom.Seed(7);
            var a = new SimRandom(state);
            for (int i = 0; i < 10; i++) a.NextUInt();
            var b = new SimRandom(new RngState { State = state.State, Inc = state.Inc });
            for (int i = 0; i < 50; i++) Assert.That(b.NextUInt(), Is.EqualTo(a.NextUInt()));
        }

        [Test]
        public void NextIntStaysInRangeAndHitsEveryValue()
        {
            var rng = new SimRandom(SimRandom.Seed(3));
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int v = rng.NextInt(-2, 4);
                Assert.That(v, Is.InRange(-2, 3));
                seen.Add(v);
            }
            Assert.That(seen.Count, Is.EqualTo(6));
        }

        [Test]
        public void NextDoubleIsInUnitInterval()
        {
            var rng = new SimRandom(SimRandom.Seed(5));
            for (int i = 0; i < 2000; i++) Assert.That(rng.NextDouble(), Is.InRange(0.0, 0.9999999999));
        }

        [Test]
        public void PickWeightedFollowsWeightsAndSkipsZero()
        {
            var rng = new SimRandom(SimRandom.Seed(9));
            var counts = new int[3];
            for (int i = 0; i < 6000; i++) counts[rng.PickWeighted(new[] { 1.0, 0.0, 3.0 })]++;
            Assert.That(counts[1], Is.EqualTo(0));
            Assert.That(counts[2] / (double)counts[0], Is.InRange(2.6, 3.4));
        }
    }

    public class GameDateTests
    {
        [Test]
        public void DayZeroIsFirstDayOfSpringYearOne()
        {
            var d = new GameDate(0);
            Assert.That(d.Year, Is.EqualTo(1));
            Assert.That(d.Season, Is.EqualTo(Season.Spring));
            Assert.That(d.DayOfSeason, Is.EqualTo(1));
        }

        [Test]
        public void SeasonsAndYearsRollOver()
        {
            Assert.That(new GameDate(GameDate.DaysPerSeason - 1).Season, Is.EqualTo(Season.Spring));
            Assert.That(new GameDate(GameDate.DaysPerSeason).Season, Is.EqualTo(Season.Summer));
            Assert.That(new GameDate(GameDate.DaysPerYear - 1).Season, Is.EqualTo(Season.Winter));
            Assert.That(new GameDate(GameDate.DaysPerYear).Year, Is.EqualTo(2));
            Assert.That(new GameDate(GameDate.DaysPerYear).Season, Is.EqualTo(Season.Spring));
        }

        [Test]
        public void NegativeDaysAreBeforeYearOne()
        {
            var d = new GameDate(-1);
            Assert.That(d.Year, Is.EqualTo(0));
            Assert.That(d.Season, Is.EqualTo(Season.Winter));
            Assert.That(d.DayOfSeason, Is.EqualTo(GameDate.DaysPerSeason));
        }

        [Test]
        public void NextSeasonStartLandsOnDayOne()
        {
            GameDate next = new GameDate(40).NextSeasonStart;
            Assert.That(next.TotalDays, Is.EqualTo(2 * GameDate.DaysPerSeason));
            Assert.That(new GameDate(0).NextSeasonStart.TotalDays, Is.EqualTo(GameDate.DaysPerSeason));
        }

        [Test]
        public void YearsBetweenFloors()
        {
            Assert.That(GameDate.YearsBetween(0, GameDate.DaysPerYear - 1), Is.EqualTo(0));
            Assert.That(GameDate.YearsBetween(0, GameDate.DaysPerYear), Is.EqualTo(1));
            Assert.That(GameDate.YearsBetween(-28 * GameDate.DaysPerYear, 0), Is.EqualTo(28));
        }
    }

    public class OriginTextTests
    {
        private const string Template = "Scar from {cause}[ at {place}], {season} of year {year}";

        [Test]
        public void FillsEveryToken()
        {
            int springOfYearThree = 2 * GameDate.DaysPerYear + 5;
            string text = OriginText.Format(Template, new MarkOrigin { Cause = "the boar", Place = "Miller's Ford" }, springOfYearThree);
            Assert.That(text, Is.EqualTo("Scar from the boar at Miller's Ford, spring of year 3"));
        }

        [Test]
        public void DropsBracketGroupWhenItsTokenIsEmpty()
        {
            string text = OriginText.Format(Template, new MarkOrigin { Cause = "the boar" }, 0);
            Assert.That(text, Is.EqualTo("Scar from the boar, spring of year 1"));
        }

        [Test]
        public void KnowsWhenACauseIsRequired()
        {
            Assert.That(OriginText.RequiresCause(Template), Is.True);
            Assert.That(OriginText.RequiresCause("Went grey[ after {cause}], {season} of year {year}"), Is.False);
        }

        [TestCase("Scar from {who}", "unknown token")]
        [TestCase("Scar [from [{cause}]]", "nested")]
        [TestCase("Scar from {cause}]", "unmatched ]")]
        [TestCase("Scar [from {cause}", "unmatched [")]
        [TestCase("  ", "empty")]
        public void ValidateRejectsMalformedTemplates(string template, string expected)
        {
            Assert.That(OriginText.Validate(template), Does.Contain(expected));
        }
    }
}
