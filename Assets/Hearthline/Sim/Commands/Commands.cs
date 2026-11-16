using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    public sealed class CommandResult
    {
        public bool Ok;
        /// <summary>Why the command was rejected. Null when <see cref="Ok"/>.</summary>
        public string Error;
        public System.Collections.Generic.IReadOnlyList<DomainEvent> Events = new DomainEvent[0];
    }

    /// <summary>
    /// A request to change the simulation. A command validates first and returns an error without touching
    /// state; if it returns null it has been applied in full.
    /// </summary>
    public abstract class Command
    {
        internal abstract string Apply(SimContext ctx);

        internal static string LivingCharacter(SimContext ctx, int id, out Character character)
        {
            character = ctx.FindCharacter(id);
            if (character == null) return "no character with id " + id;
            return character.Alive ? null : character.GivenName + " is dead";
        }

        internal static string FindMark(Character character, int markId, out Mark mark)
        {
            mark = null;
            foreach (Mark m in character.Marks) if (m.Id == markId) mark = m;
            return mark == null ? character.GivenName + " has no mark with id " + markId : null;
        }
    }

    /// <summary>Puts a mark on a character directly (events, rites, exposure, the cheat panel).</summary>
    public sealed class AddMark : Command
    {
        public int CharacterId;
        public string DefId;
        public BodyRegion Region;
        public int Severity = 1;
        public MarkOrigin Origin;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            MarkDef def = ctx.Content.FindMark(DefId);
            if (def == null) return "no mark definition '" + DefId + "'";
            error = MarkSystem.CheckCanAdd(def, Region, Severity, Origin);
            if (error != null) return error;
            MarkSystem.Add(ctx, character, def, Region, Severity, Origin);
            return null;
        }
    }

    /// <summary>Debug only. Marks are meant to last; nothing in normal play removes one outright.</summary>
    public sealed class RemoveMark : Command
    {
        public int CharacterId;
        public int MarkId;

        internal override string Apply(SimContext ctx)
        {
            Character character = ctx.FindCharacter(CharacterId);
            if (character == null) return "no character with id " + CharacterId;
            string error = FindMark(character, MarkId, out Mark mark);
            if (error != null) return error;
            character.Marks.Remove(mark);
            ctx.Emit(new MarkRemoved { Day = ctx.State.Day, CharacterId = character.Id, MarkId = mark.Id, DefId = mark.DefId });
            return null;
        }
    }

    /// <summary>A combat hit on a body region. Creates the matching wound.</summary>
    public sealed class InflictHit : Command
    {
        public int CharacterId;
        public BodyRegion Region;
        public DamageType Damage;
        public int Severity = 1;
        public MarkOrigin Origin;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            MarkDef def = MarkSystem.FindWoundFor(ctx.Content, Damage, Region);
            if (def == null) return "no wound is defined for " + Naming.ToSnake(Damage) + " on " + Naming.ToSnake(Region);
            error = MarkSystem.CheckCanAdd(def, Region, Severity, Origin);
            if (error != null) return error;
            if (character.Downed) return character.GivenName + " is already down";
            MarkSystem.Add(ctx, character, def, Region, Severity, Origin);
            LifeSystem.TakeDamage(ctx, character, Severity);
            return null;
        }
    }

    /// <summary>A companion gets a downed character back on their feet, barely.</summary>
    public sealed class ReviveCharacter : Command
    {
        public int CharacterId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            if (!character.Downed) return character.GivenName + " is not down";
            LifeSystem.Revive(ctx, character);
            return null;
        }
    }

    /// <summary>A downed character is left unattended: they die, or lose something for good. Also happens by itself when a day passes.</summary>
    public sealed class LeaveDowned : Command
    {
        public int CharacterId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            if (!character.Downed) return character.GivenName + " is not down";
            LifeSystem.ResolveDowned(ctx, character);
            return null;
        }
    }

    /// <summary>Field treatment: stabilizes a fresh wound, which buys time. It does not decide how the wound ends.</summary>
    public sealed class FieldTreatWound : Command
    {
        public int CharacterId;
        public int MarkId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            error = FindMark(character, MarkId, out Mark mark);
            if (error != null) return error;
            if (mark.Category != MarkCategory.Wound) return "only wounds can be treated";
            if (mark.Stage != WoundStages.Fresh) return "only a fresh wound can be stabilized in the field";
            MarkSystem.SetStage(ctx, character, mark, WoundStages.Stabilized, ctx.State.Day);
            ctx.Emit(new WoundTreated { Day = ctx.State.Day, CharacterId = character.Id, MarkId = mark.Id, AtInfirmary = false, Outcome = WoundStages.Stabilized });
            return null;
        }
    }

    /// <summary>
    /// Infirmary treatment: rolls how the wound will end (clean scar, bad scar, infection or loss).
    /// Unless it turns infected, the wound starts healing and resolves when the healing stage runs out.
    /// </summary>
    public sealed class InfirmaryTreatWound : Command
    {
        public int CharacterId;
        public int MarkId;
        /// <summary>1 cot, 2 sickroom, 3 surgery.</summary>
        public int InfirmaryTier = 1;
        /// <summary>The healer's Medicine skill, 0..10.</summary>
        public int HealerSkill;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            error = FindMark(character, MarkId, out Mark mark);
            if (error != null) return error;
            if (mark.Category != MarkCategory.Wound) return "only wounds can be treated";
            if (mark.Stage == WoundStages.Healing) return "this wound has already been treated and is healing";
            if (InfirmaryTier < TreatmentRules.MinTier || InfirmaryTier > TreatmentRules.MaxTier)
                return "infirmary tier must be " + TreatmentRules.MinTier + ".." + TreatmentRules.MaxTier;
            if (HealerSkill < TreatmentRules.MinSkill || HealerSkill > TreatmentRules.MaxSkill)
                return "healer skill must be " + TreatmentRules.MinSkill + ".." + TreatmentRules.MaxSkill;

            int day = ctx.State.Day;
            double[] weights = ctx.Treatment.Weights(InfirmaryTier, HealerSkill, mark.Severity, mark.Stage);
            string outcome = TreatmentRules.OutcomeOrder[ctx.Rng.PickWeighted(weights)];
            if (outcome == WoundOutcomes.Infection)
            {
                // Treating an already infected wound that stays infected does not restart its clock.
                if (mark.Stage != WoundStages.Infected) MarkSystem.SetStage(ctx, character, mark, WoundStages.Infected, day);
            }
            else
            {
                mark.PendingOutcome = outcome;
                MarkSystem.SetStage(ctx, character, mark, WoundStages.Healing, day);
            }
            ctx.Emit(new WoundTreated { Day = day, CharacterId = character.Id, MarkId = mark.Id, AtInfirmary = true, Outcome = outcome });
            return null;
        }
    }

    /// <summary>Moves the calendar forward one day at a time, running everything that happens with time.</summary>
    public sealed class AdvanceDays : Command
    {
        public int Days = 1;

        internal override string Apply(SimContext ctx)
        {
            if (Days < 1) return "days must be at least 1";
            if (ctx.State.ActiveEvent != null) return "finish the event in progress first";
            Run(ctx, Days);
            return null;
        }

        internal static void Run(SimContext ctx, int days)
        {
            int from = ctx.State.Day;
            for (int i = 0; i < days; i++)
            {
                ctx.State.Day++;
                foreach (Character c in ctx.State.Characters.ToArray())
                {
                    MarkSystem.Tick(ctx, c);
                    LifeSystem.DailyTick(ctx, c);
                }
            }
            ctx.Emit(new DateAdvanced { Day = ctx.State.Day, FromDay = from, ToDay = ctx.State.Day });
        }
    }

    /// <summary>Advances to the first day of the next season. The interlude simulation will hang off this later.</summary>
    public sealed class PassSeason : Command
    {
        internal override string Apply(SimContext ctx)
        {
            if (ctx.State.ActiveEvent != null) return "finish the event in progress first";
            var now = new GameDate(ctx.State.Day);
            GameDate next = now.NextSeasonStart;
            AdvanceDays.Run(ctx, next.TotalDays - now.TotalDays);
            LifeSystem.RunInterlude(ctx);
            WorldSystem.RunSeason(ctx);
            ctx.Emit(new SeasonPassed { Day = ctx.State.Day, Ended = now.Season, Began = next.Season });
            return null;
        }
    }

    /// <summary>Debug only: rewrites a character's birth date so they are the given age today.</summary>
    public sealed class SetAge : Command
    {
        public int CharacterId;
        public int AgeYears;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            if (AgeYears < 0 || AgeYears > 120) return "age must be 0..120";
            character.BirthDay = ctx.State.Day - AgeYears * GameDate.DaysPerYear;
            ctx.Emit(new CharacterAgeSet { Day = ctx.State.Day, CharacterId = character.Id, AgeYears = AgeYears });
            return null;
        }
    }

    public sealed class KillCharacter : Command
    {
        public int CharacterId;
        public string Cause;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            if (string.IsNullOrWhiteSpace(Cause)) return "a death needs a cause; the chronicle records it";
            LifeSystem.Kill(ctx, character, Cause);
            return null;
        }
    }

    /// <summary>Someone joins the household: a recruit, a rescued stranger, a ward, a partner-to-be.</summary>
    public sealed class AddHouseholdMember : Command
    {
        public string GivenName;
        /// <summary>Recruits keep their own surname. Null uses the household's.</summary>
        public string Surname;
        public int AgeYears = 25;
        public HouseholdBond Bond = HouseholdBond.Sworn;
        public System.Collections.Generic.List<string> Traits = new System.Collections.Generic.List<string>();

        internal override string Apply(SimContext ctx)
        {
            if (string.IsNullOrWhiteSpace(GivenName)) return "a household member needs a name";
            if (AgeYears < 0 || AgeYears > 120) return "age must be 0..120";
            if (Bond == HouseholdBond.Founder || Bond == HouseholdBond.Blood) return "people join as partner, ward or sworn; blood is born";
            foreach (string trait in Traits)
                if (ctx.Content.FindTrait(trait) == null) return "no trait '" + trait + "'";
            Character c = LifeSystem.CreateCharacter(ctx, GivenName, Surname ?? ctx.State.Household.Surname,
                ctx.State.Day - AgeYears * GameDate.DaysPerYear, Bond);
            foreach (string trait in Traits) if (!c.Traits.Contains(trait)) c.Traits.Add(trait);
            ctx.Emit(new CharacterJoined { Day = ctx.State.Day, CharacterId = c.Id });
            ctx.Chronicle(c, ChronicleKind.Joined, c.GivenName + " " + c.Surname + " joined the household.");
            return null;
        }
    }

    public sealed class SetPartners : Command
    {
        public int CharacterAId;
        public int CharacterBId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterAId, out Character a) ?? LivingCharacter(ctx, CharacterBId, out Character b0);
            if (error != null) return error;
            Character b = ctx.FindCharacter(CharacterBId);
            if (a.Id == b.Id) return "a partnership needs two people";
            foreach (Character c in new[] { a, b })
            {
                if (c.PartnerId.HasValue) return c.GivenName + " already has a partner";
                if (!LifeSystem.InHousehold(ctx, c)) return c.GivenName + " is not in the household";
                if (LifeSystem.AgeOf(ctx, c) < LifeRules.AdultAge) return c.GivenName + " is not an adult";
            }
            a.PartnerId = b.Id;
            b.PartnerId = a.Id;
            ctx.Emit(new PartnersJoined { Day = ctx.State.Day, CharacterAId = a.Id, CharacterBId = b.Id });
            ctx.Chronicle(a, ChronicleKind.Partnered, a.GivenName + " and " + b.GivenName + " became partners.");
            return null;
        }
    }

    /// <summary>A child is born now. Interludes do this by chance; events and the cheat panel do it directly.</summary>
    public sealed class BirthChild : Command
    {
        public int ParentAId;
        public int ParentBId;
        /// <summary>Null picks an unused name from Content/names.yaml.</summary>
        public string GivenName;

        internal override string Apply(SimContext ctx)
        {
            Character a = ctx.FindCharacter(ParentAId), b = ctx.FindCharacter(ParentBId);
            if (a == null || b == null) return "both parents must exist";
            string error = LifeSystem.CheckCanParent(ctx, a, b);
            if (error != null) return error;
            LifeSystem.Birth(ctx, a, b, string.IsNullOrWhiteSpace(GivenName) ? null : GivenName);
            return null;
        }
    }

    public sealed class SetTrait : Command
    {
        public int CharacterId;
        public string TraitId;
        /// <summary>True gives the trait, false takes it away.</summary>
        public bool Has = true;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character character);
            if (error != null) return error;
            if (ctx.Content.FindTrait(TraitId) == null) return "no trait '" + TraitId + "'";
            if (character.Traits.Contains(TraitId) == Has) return character.GivenName + (Has ? " already has " : " does not have ") + TraitId;
            if (Has) character.Traits.Add(TraitId); else character.Traits.Remove(TraitId);
            ctx.Emit(new TraitChanged { Day = ctx.State.Day, CharacterId = character.Id, TraitId = TraitId, Gained = Has });
            return null;
        }
    }

    /// <summary>After the head has died: the player picks who carries the household on.</summary>
    public sealed class ChooseHeir : Command
    {
        public int HeirId;

        internal override string Apply(SimContext ctx)
        {
            if (ctx.State.Household.HeadId.HasValue) return "the household already has a head";
            if (ctx.State.Household.LineEnded) return "the line has ended";
            Character heir = ctx.FindCharacter(HeirId);
            if (heir == null || !LifeSystem.EligibleHeirs(ctx).Contains(heir)) return "that person cannot inherit the household";
            LifeSystem.Succeed(ctx, heir);
            return null;
        }
    }

    /// <summary>An Elder head steps down in favour of an heir and stays home as a mentor.</summary>
    public sealed class RetireHead : Command
    {
        public int HeirId;

        internal override string Apply(SimContext ctx)
        {
            if (!ctx.State.Household.HeadId.HasValue) return "there is no head of household to retire";
            Character head = ctx.FindCharacter(ctx.State.Household.HeadId.Value);
            if (LifeStages.ForAge(LifeSystem.AgeOf(ctx, head)) < LifeStage.Elder) return head.GivenName + " is too young to retire";
            Character heir = ctx.FindCharacter(HeirId);
            if (heir == null || !LifeSystem.EligibleHeirs(ctx).Contains(heir)) return "that person cannot inherit the household";
            head.Retired = true;
            ctx.Emit(new HeadRetired { Day = ctx.State.Day, CharacterId = head.Id });
            ctx.Chronicle(head, ChronicleKind.Retired, head.GivenName + " stepped down.");
            LifeSystem.Succeed(ctx, heir);
            return null;
        }
    }
}
