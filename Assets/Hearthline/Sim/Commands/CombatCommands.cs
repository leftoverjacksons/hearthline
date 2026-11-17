namespace Hearthline.Sim
{
    // Ranged combat (docs/specs/combat-and-ambush.md). Presentation does the aiming, moving and deciding;
    // these commands are how it tells the simulation what happened.

    /// <summary>Starts a fight: generates the enemies with their guns and ammunition. Usually sent on AmbushRequested.</summary>
    public sealed class StartEncounter : Command
    {
        public string EncounterId;
        public string EventId;
        public string PlaceName;
        /// <summary>Someone the event already cast who leads the fight (AmbushRequested.LeaderId), or null.</summary>
        public int? LeaderId;

        internal override string Apply(SimContext ctx) => CombatSystem.Start(ctx, EncounterId, EventId, PlaceName, LeaderId);
    }

    /// <summary>Pulls the trigger. Uses a round; the gun may jam (a jam is a result, not a refusal).</summary>
    public sealed class FireWeapon : Command
    {
        public int CharacterId;
        public int GearId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character c);
            if (error != null) return error;
            error = CombatSystem.Gun(ctx, c, GearId, out GearItem gun, out Content.WeaponDef weapon);
            if (error != null) return error;
            if (c.Downed) return c.GivenName + " is down";
            if (gun.Jammed) return "the gun is jammed";
            if (gun.Loaded <= 0) return "the gun is empty";
            CombatSystem.Fire(ctx, c, gun, weapon);
            return null;
        }
    }

    public sealed class ClearJam : Command
    {
        public int CharacterId;
        public int GearId;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character c);
            if (error != null) return error;
            error = CombatSystem.Gun(ctx, c, GearId, out GearItem gun, out _);
            if (error != null) return error;
            if (!gun.Jammed) return "the gun is not jammed";
            gun.Jammed = false;
            ctx.Emit(new JamCleared { Day = ctx.State.Day, CharacterId = c.Id, GearId = gun.Id });
            return null;
        }
    }

    /// <summary>
    /// Loads rounds: an enemy from their own supply, anyone else from the pack on a trip or the stores at home.
    /// <see cref="Rounds"/> 0 fills the gun.
    /// </summary>
    public sealed class ReloadWeapon : Command
    {
        public int CharacterId;
        public int GearId;
        public int Rounds;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, CharacterId, out Character c);
            if (error != null) return error;
            error = CombatSystem.Gun(ctx, c, GearId, out GearItem gun, out Content.WeaponDef weapon);
            if (error != null) return error;
            if (c.Downed) return c.GivenName + " is down";
            if (gun.Jammed) return "clear the jam first";
            if (Rounds < 0) return "rounds must not be negative";
            return CombatSystem.Reload(ctx, c, gun, weapon, Rounds);
        }
    }

    /// <summary>
    /// A bullet found a body: presentation saw which hit zone, how far it flew, and whose gun fired it. The
    /// sim decides how bad it is (the gun's severity, +1 to the head, -1 to the hands or past the gun's
    /// range) and makes the wound. A fighter who goes down leaves the fight and the gun's history notes it.
    /// </summary>
    public sealed class ShotHit : Command
    {
        public int TargetId;
        public BodyRegion Region;
        /// <summary>Who fired, or null for a stray or unknown shot.</summary>
        public int? ShooterId;
        public int? GearId;
        public double Distance;

        internal override string Apply(SimContext ctx)
        {
            string error = LivingCharacter(ctx, TargetId, out Character target);
            if (error != null) return error;
            if (target.Downed) return target.GivenName + " is already down";
            Character shooter = null;
            GearItem gun = null;
            Content.WeaponDef weapon = null;
            if (ShooterId.HasValue)
            {
                shooter = ctx.FindCharacter(ShooterId.Value);
                if (shooter == null) return "no character with id " + ShooterId.Value;
                if (GearId.HasValue)
                {
                    error = CombatSystem.Gun(ctx, shooter, GearId.Value, out gun, out weapon);
                    if (error != null) return error;
                }
            }
            if (Distance < 0) return "distance must not be negative";

            DamageType damage = weapon?.Damage ?? DamageType.Gunshot;
            Content.MarkDef wound = MarkSystem.FindWoundFor(ctx.Content, damage, Region);
            if (wound == null) return "no wound is defined for " + Naming.ToSnake(damage) + " on " + Naming.ToSnake(Region);
            int severity = CombatSystem.ShotSeverity(ctx, weapon, Region, Distance);
            var origin = new MarkOrigin
            {
                Cause = shooter == null ? "a stray bullet"
                    : LifeSystem.InHousehold(ctx, shooter) ? (shooter.GivenName + " " + shooter.Surname).Trim()
                    : shooter.Archetype != null ? "a " + shooter.Archetype : shooter.GivenName,
                Place = ctx.State.Encounter?.PlaceName,
            };
            error = MarkSystem.CheckCanAdd(wound, Region, severity, origin);
            if (error != null) return error;
            MarkSystem.Add(ctx, target, wound, Region, severity, origin);
            LifeSystem.TakeDamage(ctx, target, severity);
            CombatSystem.AfterHit(ctx, target, shooter, gun);
            return null;
        }
    }

    /// <summary>An enemy got away (out of range, out of sight). Counts toward the group breaking.</summary>
    public sealed class CombatantFlees : Command
    {
        public int CharacterId;

        internal override string Apply(SimContext ctx)
        {
            Combatant fighter = CombatSystem.FindCombatant(ctx, CharacterId);
            if (fighter == null) return "character " + CharacterId + " is not in the fight";
            if (fighter.State != CombatantState.Fighting) return "they are already out of the fight";
            CombatSystem.Leave(ctx, fighter, CombatantState.Fled);
            return null;
        }
    }

    /// <summary>An enemy threw down their gun. Decided what to do with them when the fight ends.</summary>
    public sealed class CombatantSurrenders : Command
    {
        public int CharacterId;

        internal override string Apply(SimContext ctx)
        {
            Combatant fighter = CombatSystem.FindCombatant(ctx, CharacterId);
            if (fighter == null) return "character " + CharacterId + " is not in the fight";
            if (fighter.State != CombatantState.Fighting) return "they are already out of the fight";
            Character c = ctx.FindCharacter(CharacterId);
            if (c == null || !c.Alive || c.Downed) return "they cannot surrender";
            CombatSystem.Leave(ctx, fighter, CombatantState.Surrendered);
            return null;
        }
    }

    /// <summary>
    /// Ends the fight. Won: nobody is still fighting; the fallen leave their gear and loot, and the
    /// surrendered are dealt with by <see cref="Surrender"/>. Fled: the player got away. Downed: the head is
    /// down (on a trip, the enemies take the pack; then the usual bleed-out rules).
    /// </summary>
    public sealed class EndEncounter : Command
    {
        public EncounterOutcome Outcome;
        public SurrenderChoice Surrender = SurrenderChoice.LetGo;

        internal override string Apply(SimContext ctx) => CombatSystem.End(ctx, Outcome, Surrender);
    }

    /// <summary>The head picks up what a fight left on the ground.</summary>
    public sealed class PickUpLoot : Command
    {
        public int LootId;

        internal override string Apply(SimContext ctx) => CombatSystem.PickUp(ctx, LootId);
    }

    /// <summary>Cheat panel and tests: sets a gun's condition, rounds and jam.</summary>
    public sealed class SetGunState : Command
    {
        public int GearId;
        public double? Condition;
        public int? Loaded;
        public bool? Jammed;

        internal override string Apply(SimContext ctx)
        {
            GearItem gun = GearSystem.Find(ctx, GearId, out _);
            if (gun == null) return "no gear with id " + GearId;
            if (!ctx.Content.Items.TryGetValue(gun.DefId, out Content.ItemDef def) || def.Weapon == null) return "not a firearm";
            if (Condition.HasValue && (Condition < 0 || Condition > 1)) return "condition must be 0..1";
            if (Loaded.HasValue && (Loaded < 0 || Loaded > def.Weapon.Capacity)) return "loaded must be 0.." + def.Weapon.Capacity;
            if (Condition.HasValue) gun.Condition = Condition.Value;
            if (Loaded.HasValue) gun.Loaded = Loaded.Value;
            if (Jammed.HasValue) gun.Jammed = Jammed.Value;
            return null;
        }
    }
}
