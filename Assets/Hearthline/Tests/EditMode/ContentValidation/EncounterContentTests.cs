using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests.ContentValidation
{
    /// <summary>The encounter and weapon schema (docs/content-schema.md, Encounters and Weapons).</summary>
    public class EncounterContentTests
    {
        private const string Vocab = @"
stats:
  - { id: stealth, name: Stealth, base: 0 }
reaction_tags: [guilty]
abilities: [night_sight]
area_tags: [road]
archetypes: [reaver]
encounters: [test_fight]
visuals: { gear: [rifle_lever] }
";
        private const string Rifle = @"
id: test_rifle
name: Test rifle
weight: 3
equip: { slot: long_gun, visual: rifle_lever }
weapon: { ammo: test_rounds, capacity: 5, severity: 2, range: 50, fire_interval: 1, reload_per_round: 0.5, spread: 1 }
";
        private const string Rounds = "id: test_rounds\nname: Test rounds\nweight: 0.1\n";
        private const string Faction = "id: reavers\nname: Reavers\ndescription: Them.\n";
        private const string Fight = @"
id: test_fight
name: A test fight
enemies:
  - { archetype: reaver, count: [1, 2], weapon: test_rifle, ammo: [2, 4] }
morale: { break_when_down: 0.5, surrender_chance: 0.3 }
loot:
  - { item: test_rounds, count: [1, 3], chance: 0.5 }
outcomes:
  won: { reputation: { reavers: -5 }, grudge: reavers }
  downed: { robbed: true }
";

        private static void AssertError(string fight, string expected, string rifle = Rifle)
        {
            ContentLoadResult result = ContentLoader.Load(new[]
            {
                new ContentFile { Path = "vocab.yaml", Text = Vocab },
                new ContentFile { Path = "items/test_rifle.yaml", Text = rifle },
                new ContentFile { Path = "items/test_rounds.yaml", Text = Rounds },
                new ContentFile { Path = "factions/reavers.yaml", Text = Faction },
                new ContentFile { Path = "encounters/test_fight.yaml", Text = fight },
            });
            if (expected == null) { Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors)); return; }
            foreach (ContentError e in result.Errors) if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", result.Errors));
        }

        [Test] public void BaselineIsValid() => AssertError(Fight, null);

        [Test] public void EnemiesMustBeKnownArchetypes() =>
            AssertError(Fight.Replace("archetype: reaver,", "archetype: bandit,"), "no archetype 'bandit'");

        [Test] public void EnemiesMustCarryAWeapon() =>
            AssertError(Fight.Replace("weapon: test_rifle", "weapon: test_rounds"), "not an item with a weapon block");

        [Test] public void CountsMustBeRanges() =>
            AssertError(Fight.Replace("count: [1, 2]", "count: [3, 1]"), "'count' must be [min, max]");

        [Test] public void SomeoneMustAlwaysShowUp() =>
            AssertError(Fight.Replace("count: [1, 2]", "count: [0, 2]"), "must always appear");

        [Test] public void OnlyTheDownedOutcomeCanRob() =>
            AssertError(Fight.Replace("grudge: reavers }", "grudge: reavers, robbed: true }"), "'robbed' only applies");

        [Test] public void OutcomesUseKnownKeysAndFactions()
        {
            AssertError(Fight.Replace("won:", "victory:"), "unknown outcome 'victory'");
            AssertError(Fight.Replace("reputation: { reavers: -5 }", "reputation: { bandits: -5 }"), "no faction 'bandits'");
        }

        [Test] public void EveryVocabEncounterNeedsAFile() =>
            AssertError(Fight.Replace("id: test_fight", "id: other_fight"), "has no file encounters/test_fight.yaml");

        [Test] public void LootMustBeStackableItems() =>
            AssertError(Fight.Replace("item: test_rounds", "item: test_rifle"), "is gear");

        [Test] public void WeaponsNeedRealAmmoAndAGunSlot()
        {
            AssertError(Fight, "no item 'nothing'", Rifle.Replace("ammo: test_rounds", "ammo: nothing"));
            AssertError(Fight, "long_gun or sidearm", Rifle.Replace("slot: long_gun", "slot: head"));
            AssertError(Fight, "'capacity' must be at least 1", Rifle.Replace("capacity: 5", "capacity: 0"));
        }

        [Test] public void UnstagedEncountersNeedNoEnemies() =>
            AssertError("id: test_fight\nname: Animals\nstaged: false\n", null);
    }
}
