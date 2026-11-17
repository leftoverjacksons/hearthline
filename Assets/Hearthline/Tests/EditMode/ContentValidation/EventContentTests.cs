using System.Collections.Generic;
using Hearthline.Sim.Content;
using NUnit.Framework;

namespace Hearthline.Tests.ContentValidation
{
    public class EventContentTests
    {
        [Test]
        public void TheEventSetUsesEveryKindOfOutcome()
        {
            var used = new HashSet<string>();
            foreach (EventDef def in TestSupport.Content.Events.Values)
                foreach (EventBeat beat in def.Beats)
                    foreach (EventChoice choice in beat.Choices) choice.Outcome.CollectKinds(used);
            foreach (string kind in EventOutcome.AllKinds) Assert.That(used, Does.Contain(kind), "no event uses the '" + kind + "' outcome");
        }

        [Test]
        public void EveryItemAndLootTableAndFactionIsUsedBySomeEvent()
        {
            var items = new HashSet<string>(); var loot = new HashSet<string>(); var factions = new HashSet<string>();
            void Walk(EventOutcome o)
            {
                items.UnionWith(o.Items.Keys); factions.UnionWith(o.Reputation.Keys);
                if (o.LootTable != null) loot.Add(o.LootTable);
                foreach (ChanceBranch b in o.Chance) Walk(b.Outcome);
            }
            foreach (EventDef def in TestSupport.Content.Events.Values)
                foreach (EventBeat beat in def.Beats)
                    foreach (EventChoice choice in beat.Choices) Walk(choice.Outcome);
            foreach (LootTableDef table in TestSupport.Content.LootTables.Values)
                foreach (LootEntry e in table.Entries) items.Add(e.ItemId);
            foreach (JobDef job in TestSupport.Content.Jobs.Values)
                foreach (LootEntry e in job.Produces) items.Add(e.ItemId);
            foreach (EncounterDef encounter in TestSupport.Content.Encounters.Values)
                foreach (EncounterLootDef l in encounter.Loot) items.Add(l.ItemId);
            foreach (ItemDef gun in TestSupport.Content.Items.Values)
                if (gun.Weapon != null) items.Add(gun.Weapon.Ammo);
            foreach (HuntDef hunt in TestSupport.Content.Hunts.Values)
            {
                if (hunt.MeatItem != null) items.Add(hunt.MeatItem);
                items.UnionWith(hunt.Items.Keys);
            }

            foreach (string id in TestSupport.Content.LootTables.Keys) Assert.That(loot, Does.Contain(id));
            foreach (string id in TestSupport.Content.Factions.Keys) Assert.That(factions, Does.Contain(id));
            foreach (ItemDef item in TestSupport.Content.Items.Values)
                if (item.Equip == null && item.Gather == null) Assert.That(items, Does.Contain(item.Id), "item '" + item.Id + "' is never given or taken");
        }

        // ---- schema rules --------------------------------------------------------------------------

        private const string Vocab = @"
stats:
  - { id: stealth, name: Stealth, base: 0 }
reaction_tags: [guilty]
abilities: [night_sight]
area_tags: [road]
archetypes: [traveler]
encounters: [reavers_small]
visuals: {}
";
        private const string Rite = @"
id: rite_test
name: Test rite
category: rite
regions: [head]
heritable: none
origin_text: ""Carried since {cause}""
initial_stage: carried
stages:
  - id: carried
    effects: [{ reaction: guilty }]
";
        private const string Event = @"
id: test_event
tags: [encounter]
when: { area_tags_any: [road] }
cast:
  hero: { from: party, role: head }
  stranger: { generate: traveler }
beats:
  - text: ""{stranger.name} waves.""
    choices:
      - label: Wave back
        outcome:
          relationship: { stranger: 5 }
";

        private static void AssertError(string eventYaml, string expected)
        {
            ContentLoadResult result = ContentLoader.Load(new[]
            {
                new ContentFile { Path = "vocab.yaml", Text = Vocab },
                new ContentFile { Path = "marks/rite_test.yaml", Text = Rite },
                new ContentFile { Path = "events/test_event.yaml", Text = eventYaml },
                new ContentFile { Path = "encounters/reavers_small.yaml", Text = "id: reavers_small\nname: Test fight\nstaged: false\n" },
            });
            if (expected == null) { Assert.That(result.Errors, Is.Empty, string.Join("\n", result.Errors)); return; }
            foreach (ContentError e in result.Errors) if (e.Message.Contains(expected)) return;
            Assert.Fail("Expected an error containing \"" + expected + "\" but got:\n" + string.Join("\n", result.Errors));
        }

        [Test] public void BaselineEventIsValid() => AssertError(Event, null);

        [Test] public void PureFlavorIsNotAnEvent() =>
            AssertError(Event.Replace("relationship: { stranger: 5 }", "goto: beat_0"), "must change something");

        [Test] public void TextMayOnlyNameCastRoles() =>
            AssertError(Event.Replace("{stranger.name} waves", "{bandit.name} waves"), "no such cast role");

        [Test] public void TextTokensMustBeKnown() =>
            AssertError(Event.Replace("{stranger.name}", "{stranger.shoe_size}"), "unknown token");

        [Test] public void TheHeadMustBeCast() =>
            AssertError(Event.Replace("  hero: { from: party, role: head }", "  hero: { from: party, role: any }"), "must cast the head");

        [Test] public void UnknownAreaTagIsRejected() =>
            AssertError(Event.Replace("[road]", "[moon]"), "no area tag 'moon'");

        [Test] public void UnknownArchetypeIsRejected() =>
            AssertError(Event.Replace("generate: traveler", "generate: wizard"), "no archetype 'wizard'");

        [Test] public void GotoMustNameABeat() =>
            AssertError(Event + "          goto: nowhere\n", "goto unknown beat");

        [Test] public void UnknownOutcomeFieldIsRejected() =>
            AssertError(Event + "          gold: 5\n", "unknown field 'gold'");

        [Test] public void AMarkOutcomeNeedsACauseWhenTheMarkDoes() =>
            AssertError(Event + "          mark: { target: hero, def: rite_test }\n", "needs a 'cause'");

        [Test] public void AMarkOutcomeMustNameARealMark() =>
            AssertError(Event + "          mark: { target: hero, def: rite_nope, cause: x }\n", "no mark 'rite_nope'");

        [Test] public void OnlyGeneratedPeopleCanBeRecruited() =>
            AssertError(Event + "          recruit: hero\n", "only a generated person");

        [Test] public void UnknownEncounterIsRejected() =>
            AssertError(Event + "          ambush: dragons\n", "no encounter 'dragons'");

        [Test] public void EveryBeatNeedsAChoiceWithoutRequirements() =>
            AssertError(Event.Replace("      - label: Wave back", "      - label: Wave back\n        requires: { stat_min: { stealth: 1 } }"), "never stuck");

        [Test] public void ChanceCannotExceedOne() =>
            AssertError(Event + "          chance:\n            - { p: 0.7, ambush: reavers_small }\n            - { p: 0.6, ambush: reavers_small }\n", "more than 1");
    }
}
