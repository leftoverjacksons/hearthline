using System;
using System.Collections.Generic;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    /// <summary>
    /// The hunt (docs/specs/hunt.md): one particular animal, found by reading sign along a trail, approached
    /// without being noticed, shot, butchered and hauled home. Presentation lays the trail from the seed, runs
    /// the animal and reports what happens through the commands below.
    /// </summary>
    internal static class HuntSystem
    {
        public static string DoneFlag(string huntId) => "hunt_done_" + huntId;

        /// <summary>The hunted animal hit in a leg: one step less severe (it runs on three).</summary>
        public static int AdjustSeverity(SimContext ctx, Character target, BodyRegion region, int severity)
        {
            if (ctx.State.Hunt == null || ctx.State.Hunt.AnimalId != target.Id) return severity;
            bool limb = region == BodyRegion.ArmL || region == BodyRegion.ArmR || region == BodyRegion.LegL || region == BodyRegion.LegR;
            return limb ? Math.Max(MarkSystem.MinSeverity, severity - 1) : severity;
        }

        public static string Start(SimContext ctx, string huntId)
        {
            if (ctx.State.Hunt != null) return "a hunt is already on";
            if (!ctx.Content.Hunts.TryGetValue(huntId ?? "", out HuntDef def)) return "no hunt '" + huntId + "'";
            if (ctx.State.Flags.Contains(DoneFlag(def.Id))) return def.AnimalName + " is already dead";
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household";
            var animal = new Character
            {
                Id = ctx.State.NextCharacterId++,
                GivenName = def.AnimalName,
                Surname = "",
                BirthDay = ctx.State.Day - 9 * GameDate.DaysPerYear,
                Archetype = def.AnimalArchetype,
                Health = def.Health,
            };
            ctx.State.Characters.Add(animal);
            ctx.State.Hunt = new ActiveHunt { HuntId = def.Id, AnimalId = animal.Id, StartDay = ctx.State.Day, Seed = ctx.Rng.NextInt(1, int.MaxValue) };
            ctx.Emit(new HuntStarted { Day = ctx.State.Day, HuntId = def.Id, AnimalId = animal.Id, Seed = ctx.State.Hunt.Seed });
            return null;
        }

        /// <summary>
        /// Reads the next sign. One step along the trail, or two for a good tracker (Tracking 1+: a seeded
        /// chance of 25% per point, at most 75%).
        /// </summary>
        public static string ReadSign(SimContext ctx, int index)
        {
            ActiveHunt hunt = ctx.State.Hunt;
            if (hunt == null) return "no hunt is on";
            if (hunt.Revealed) return "the trail has already led to him";
            if (index != hunt.Progress) return "that sign is not the next one (expected " + hunt.Progress + ")";
            HuntDef def = ctx.Content.Hunts[hunt.HuntId];
            Character head = ctx.FindCharacter(ctx.State.Household.HeadId ?? -1);
            double tracking = head != null ? StatSystem.GetStat(ctx.Content, head, "tracking") : 0;
            int gain = tracking >= 1 && ctx.Rng.Chance(Math.Min(0.75, 0.25 * tracking)) ? 2 : 1;
            hunt.Progress = Math.Min(def.Sign, hunt.Progress + gain);
            hunt.Revealed = hunt.Progress >= def.Sign;
            ctx.Emit(new SignRead { Day = ctx.State.Day, HuntId = def.Id, Index = index, Progress = hunt.Progress, Remaining = def.Sign - hunt.Progress, Revealed = hunt.Revealed });
            return null;
        }

        public static string Bolted(SimContext ctx)
        {
            ActiveHunt hunt = ctx.State.Hunt;
            if (hunt == null) return "no hunt is on";
            if (!hunt.Revealed) return "he hasn't been found yet";
            Character animal = ctx.FindCharacter(hunt.AnimalId);
            if (animal == null || !animal.Alive || animal.Downed) return "he can't run";
            HuntDef def = ctx.Content.Hunts[hunt.HuntId];
            hunt.Progress = Math.Max(0, def.Sign - Math.Max(1, def.BoltCost));
            hunt.Revealed = false;
            hunt.Bolts++;
            ctx.Emit(new AnimalBolted { Day = ctx.State.Day, HuntId = def.Id, Progress = hunt.Progress, Bolts = hunt.Bolts });
            return null;
        }

        /// <summary>Takes what the animal gives, once it is down: meat (more with a knife), its unique items, a place in the gun's history and the chronicle.</summary>
        public static string Butcher(SimContext ctx)
        {
            ActiveHunt hunt = ctx.State.Hunt;
            if (hunt == null) return "no hunt is on";
            Character animal = ctx.FindCharacter(hunt.AnimalId);
            if (animal == null || (animal.Alive && !animal.Downed)) return "he isn't down";
            Character head = ctx.FindCharacter(ctx.State.Household.HeadId ?? -1);
            if (head == null || !head.Alive || head.Downed) return "nobody can butcher him";
            HuntDef def = ctx.Content.Hunts[hunt.HuntId];

            var yields = new Dictionary<string, int>();
            bool knife = head.Gear.Exists(g => ctx.Content.Items.TryGetValue(g.DefId, out ItemDef d) && d.Equip != null && d.Equip.Slot == EquipSlot.Knife);
            if (def.MeatItem != null) yields[def.MeatItem] = def.MeatCount + (knife ? def.MeatWithKnife : 0);
            foreach (var kv in def.Items) yields[kv.Key] = kv.Value;
            var ids = new List<string>(yields.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) if (yields[id] > 0) EventSystem.ChangeItem(ctx, id, yields[id]);

            if (hunt.KillGearId.HasValue)
            {
                GearItem gun = GearSystem.Find(ctx, hunt.KillGearId.Value, out _);
                if (gun != null) GearSystem.Note(ctx, gun, "Brought down " + def.AnimalName + ".");
            }
            if (def.Chronicle != null) ctx.Chronicle(head, ChronicleKind.Deed, def.Chronicle.Replace("{hero.name}", head.GivenName));
            if (animal.Alive) LifeSystem.Kill(ctx, animal, "hunted by " + (head.GivenName + " " + head.Surname).Trim());
            EventSystem.SetFlag(ctx, DoneFlag(def.Id), true);
            ctx.State.Hunt = null;
            ctx.Emit(new HuntEnded { Day = ctx.State.Day, HuntId = def.Id, Outcome = "won", Yields = yields });
            return null;
        }

        public static string GiveUp(SimContext ctx)
        {
            ActiveHunt hunt = ctx.State.Hunt;
            if (hunt == null) return "no hunt is on";
            ctx.State.Hunt = null;
            ctx.Emit(new HuntEnded { Day = ctx.State.Day, HuntId = hunt.HuntId, Outcome = "gave_up", Yields = new Dictionary<string, int>() });
            return null;
        }
    }

    public sealed class StartHunt : Command
    {
        public string HuntId;
        internal override string Apply(SimContext ctx) => HuntSystem.Start(ctx, HuntId);
    }

    /// <summary>Reads the next sign on the trail (its index along the trail, from 0).</summary>
    public sealed class ReadSign : Command
    {
        public int Index;
        internal override string Apply(SimContext ctx) => HuntSystem.ReadSign(ctx, Index);
    }

    /// <summary>The animal saw or heard too much and ran off. The trail goes colder.</summary>
    public sealed class ReportAnimalBolted : Command
    {
        internal override string Apply(SimContext ctx) => HuntSystem.Bolted(ctx);
    }

    public sealed class ButcherAnimal : Command
    {
        internal override string Apply(SimContext ctx) => HuntSystem.Butcher(ctx);
    }

    public sealed class GiveUpHunt : Command
    {
        internal override string Apply(SimContext ctx) => HuntSystem.GiveUp(ctx);
    }
}
