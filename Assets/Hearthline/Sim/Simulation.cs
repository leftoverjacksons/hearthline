using System;
using System.Collections.Generic;
using System.Globalization;
using Hearthline.Sim.Content;

namespace Hearthline.Sim
{
    public sealed class NewGameOptions
    {
        public ulong Seed = 1;
        public string Surname = "Ward";
        public string HeadGivenName = "Ada";
        public int HeadAgeYears = 28;
        /// <summary>What the stores hold on day one: item id -> count. Unknown items and gear are skipped.</summary>
        public Dictionary<string, int> StartingStores = new Dictionary<string, int>();
        /// <summary>Gear the head starts with, worn or wielded. Firearms start loaded. Unknown ids are skipped.</summary>
        public List<string> StartingGear = new List<string>();
    }

    /// <summary>A mark as presentation needs it: instance data joined with its definition and current stage.</summary>
    public sealed class MarkView
    {
        public int MarkId;
        public string DefId;
        public string Name;
        public MarkCategory Category;
        public BodyRegion Region;
        public int Severity;
        public string Stage;
        public string OriginText;
        /// <summary>Asset keys for the current stage. Never null.</summary>
        public MarkVisual Visual;
        /// <summary>
        /// What the current stage does on this mark's region, one readable line per effect:
        /// "tracking +1", "aim ×0.9", "reaction: scarred", "ability: dowsing". Never null.
        /// </summary>
        public List<string> Effects = new List<string>();
        /// <summary>True when the save refers to a definition or stage that content no longer has.</summary>
        public bool Orphaned;
    }

    /// <summary>
    /// The simulation: state plus the rules that change it. The only way in is <see cref="Execute"/>;
    /// the only way out is reading <see cref="State"/>, the query methods, and <see cref="EventRaised"/>.
    /// </summary>
    public sealed class Simulation
    {
        private readonly SimRandom _rng;

        /// <summary>Read it, never write it. Changes go through commands.</summary>
        public SimState State { get; }
        public ContentDatabase Content { get; }
        public TreatmentRules Treatment { get; } = new TreatmentRules();
        public LifeRules Life { get; } = new LifeRules();
        public StorytellerRules Storyteller { get; } = new StorytellerRules();
        public WorldRules World { get; } = new WorldRules();
        public HomeRules Home { get; } = new HomeRules();
        public TripRules Trips { get; } = new TripRules();
        public GearRules Gear { get; } = new GearRules();
        public CombatRules Combat { get; } = new CombatRules();
        public WearRules Wear { get; } = new WearRules();
        private readonly List<MarkDef> _ageMarks = new List<MarkDef>();

        /// <summary>Raised once per domain event, in order, after the command that caused it has fully applied.</summary>
        public event Action<DomainEvent> EventRaised;

        public Simulation(SimState state, ContentDatabase content)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            if (state.Rng == null) throw new ArgumentException("state has no RNG state", nameof(state));
            _rng = new SimRandom(state.Rng);
            foreach (MarkDef def in content.Marks.Values) if (def.GainedAtAge.HasValue) _ageMarks.Add(def);
            _ageMarks.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        private SimContext NewContext() =>
            new SimContext { State = State, Content = Content, Rng = _rng, Treatment = Treatment, Life = Life, Storyteller = Storyteller, World = World, Home = Home, Trips = Trips, Gear = Gear, Combat = Combat, Wear = Wear, AgeMarks = _ageMarks };

        public static Simulation NewGame(ContentDatabase content, NewGameOptions options)
        {
            var state = new SimState { Rng = SimRandom.Seed(options.Seed) };
            var head = new Character
            {
                Id = state.NextCharacterId++,
                GivenName = options.HeadGivenName,
                Surname = options.Surname,
                BirthDay = state.Day - options.HeadAgeYears * GameDate.DaysPerYear,
                Bond = HouseholdBond.Founder,
            };
            state.Characters.Add(head);
            state.Household.Surname = options.Surname;
            state.Household.HeadId = head.Id;
            state.Household.MemberIds.Add(head.Id);
            state.Household.PastHeadIds.Add(head.Id);
            HomeSystem.EnsureHomestead(state, content);
            foreach (KeyValuePair<string, int> item in options.StartingStores)
                if (item.Value > 0 && content.Items.TryGetValue(item.Key, out ItemDef def) && def.Equip == null)
                    state.Household.Inventory[item.Key] = item.Value;
            // A separate stream, so that adding townsfolk to content does not reshuffle everything else in a seed.
            WorldSystem.EnsureSettlements(state, content, new SimRandom(SimRandom.Seed(options.Seed, 77)));
            WeatherSystem.Begin(state, content, options.Seed);
            var sim = new Simulation(state, content);
            foreach (string id in options.StartingGear)
            {
                if (!content.Items.TryGetValue(id ?? "", out ItemDef gearDef) || gearDef.Equip == null) continue;
                if (!sim.Execute(new GiveGear { DefId = id, CharacterId = head.Id }).Ok) continue;
                GearItem gear = head.Gear[head.Gear.Count - 1];
                sim.Execute(new EquipGear { GearId = gear.Id });
                if (gearDef.Weapon != null) gear.Loaded = gearDef.Weapon.Capacity;
            }
            return sim;
        }

        public CommandResult Execute(Command command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var ctx = NewContext();
            string error = command.Apply(ctx);
            if (error != null) return new CommandResult { Ok = false, Error = error };

            var events = ctx.Events.ToArray();
            Action<DomainEvent> handler = EventRaised;
            if (handler != null) foreach (DomainEvent e in events) handler(e);
            return new CommandResult { Ok = true, Events = events };
        }

        // ---- Queries -------------------------------------------------------------------------------

        public GameDate Date => new GameDate(State.Day);

        public Character FindCharacter(int id)
        {
            foreach (Character c in State.Characters) if (c.Id == id) return c;
            return null;
        }

        /// <summary>The current head of household, or null if they have died and nobody has succeeded them.</summary>
        public Character Head => State.Household.HeadId.HasValue ? FindCharacter(State.Household.HeadId.Value) : null;

        /// <summary>Age in whole years today, or at death for the dead.</summary>
        public int AgeOf(Character character) =>
            GameDate.YearsBetween(character.BirthDay, character.DeathDay ?? State.Day);

        public LifeStage LifeStageOf(Character character) => LifeStages.ForAge(AgeOf(character));

        /// <summary>Who the player may pick as the next head, in order of precedence.</summary>
        public List<Character> EligibleHeirs() => LifeSystem.EligibleHeirs(NewContext());

        /// <summary>The event in progress as the player should see it, or null.</summary>
        public ActiveEventView GetActiveEvent() => EventSystem.View(NewContext());

        public int ReputationWith(string factionId) =>
            State.Household.Reputation.TryGetValue(factionId, out int value) ? value : 0;

        /// <summary>How many the household has in its stores at home.</summary>
        public int ItemCount(string itemId) =>
            State.Household.Inventory.TryGetValue(itemId, out int count) ? count : 0;

        /// <summary>How many the party is carrying. Zero when nobody is away.</summary>
        public int PackCount(string itemId) =>
            State.Trip != null && State.Trip.Pack.TryGetValue(itemId, out int count) ? count : 0;

        public bool OnTrip => State.Trip != null;

        /// <summary>How worn the ground is at a world position, 0..1.</summary>
        public double WearAt(double x, double z) => State.Wear.At(x, z);

        /// <summary>Days until a gathering spot yields again; 0 when it can be gathered now.</summary>
        public int DaysUntilRegrown(string spotId, string itemId) => GatherSystem.DaysUntilRegrown(State, Content, spotId, itemId);
        public double PackWeight => State.Trip == null ? 0 : TripSystem.Weight(Content, State.Trip.Pack);
        /// <summary>What the current party can carry. Over this, travel is slower and draws trouble.</summary>
        public double CarryCapacity => State.Trip == null ? 0 : TripSystem.Capacity(NewContext(), State.Trip.PartyIds);
        public bool Overloaded => TripSystem.Overloaded(NewContext());

        /// <summary>The named settlement, or the home settlement when id is null.</summary>
        public SettlementState FindSettlement(string id = null) => WorldSystem.Resolve(State, Content, id);

        public bool ServiceIsOpen(string serviceId, string settlementId = null)
        {
            SettlementState s = FindSettlement(settlementId);
            return s != null && s.OpenServices.Contains(serviceId);
        }

        /// <summary>The living person doing this work in the settlement, or null.</summary>
        public Character FindResident(string role, string settlementId = null)
        {
            SettlementState s = FindSettlement(settlementId);
            return s == null ? null : State.Characters.Find(c => c.Alive && c.SettlementId == s.Id && c.Role == role);
        }

        /// <summary>Everything this person carries, worn or not, with asset keys and history.</summary>
        public List<GearView> GearOf(Character character) => GearSystem.Views(Content, character.Gear);

        public List<GearView> GearInStores() => GearSystem.Views(Content, State.Household.GearStore);

        public int StructureTier(string structureId) => HomeSystem.TierOf(State, structureId);

        /// <summary>A homestead total, from built structures and from people doing jobs.</summary>
        public int HomeStatOf(HomeStat stat) => HomeSystem.Stat(NewContext(), stat);

        public bool HasGrudge(string factionId) => WorldSystem.HasGrudge(State, factionId);

        public bool HasFlag(string flag) => State.Flags.Contains(flag);

        /// <summary>The fight in progress, or null. Read it, never write it.</summary>
        public ActiveEncounter Encounter => State.Encounter;

        public bool InEncounter => State.Encounter != null;

        /// <summary>The hunt in progress, or null. Read it, never write it.</summary>
        public ActiveHunt Hunt => State.Hunt;

        /// <summary>What the family knows of a place, or null if nothing yet. Read it, never write it.</summary>
        public PlaceState Place(string placeId) => PlaceSystem.Get(State, placeId);
        public bool IsDiscovered(string placeId) => PlaceSystem.Get(State, placeId)?.DiscoveredDay >= 0;
        public bool IsSearched(string placeId, int spot) => PlaceSystem.Get(State, placeId)?.Searched.Contains(spot) == true;
        public bool IsFindTaken(string placeId) => PlaceSystem.Get(State, placeId)?.FindTaken == true;
        /// <summary>Kinds of place the job board has a rumour about.</summary>
        public List<string> Rumours() => PlaceSystem.Rumours(State, Content);
        /// <summary>The rumour being followed, or null.</summary>
        public FollowedRumour Rumour => State.Rumour;
        /// <summary>The chance the head finds a place's checked find today (1 without a check).</summary>
        public double FindChance(string kind)
        {
            if (!Content.Places.TryGetValue(kind ?? "", out PlaceDef def) || def.Find == null || def.Find.CheckStat == null) return 1;
            Character head = State.Household.HeadId.HasValue ? State.Characters.Find(c => c.Id == State.Household.HeadId.Value) : null;
            return head == null ? 0 : PlaceSystem.FindChance(StatSystem.GetStat(Content, head, def.Find.CheckStat), def.Find.CheckDifficulty);
        }

        /// <summary>The weather at this hour today.</summary>
        public WeatherKind WeatherAt(double hour) => WeatherSystem.At(State.Weather, hour);

        /// <summary>
        /// The next change in the weather after this hour today, if someone in the household can feel it coming
        /// (the weather_sense ability); null otherwise, or if it holds till night.
        /// </summary>
        public WeatherSpell Forecast(double hour)
        {
            bool sense = false;
            foreach (int id in State.Household.MemberIds)
            {
                Character c = FindCharacter(id);
                if (c != null && c.Alive && System.Linq.Enumerable.Contains(AbilitiesOf(c), WeatherSystem.SenseAbility)) { sense = true; break; }
            }
            return sense ? WeatherSystem.NextChange(State.Weather, hour) : null;
        }

        public HuntDef HuntDefOf(string huntId) => huntId != null && Content.Hunts.TryGetValue(huntId, out HuntDef def) ? def : null;

        /// <summary>True once that hunt's animal has been brought down.</summary>
        public bool HuntDone(string huntId) => State.Flags.Contains(HuntSystem.DoneFlag(huntId));

        /// <summary>Why the game cannot be saved right now, or null when it can.</summary>
        public string WhyCannotSave() => State.Encounter != null ? "You can't save during a fight." : null;

        /// <summary>How severe a hit from this gun at this distance on this region would be (1..3).</summary>
        public int ShotSeverity(string weaponItemId, BodyRegion region, double distance) =>
            CombatSystem.ShotSeverity(NewContext(), WeaponOf(weaponItemId), region, distance);

        /// <summary>The firearm stats of an item, or null if it is not a firearm.</summary>
        public WeaponDef WeaponOf(string itemId) => itemId != null && Content.Items.TryGetValue(itemId, out ItemDef def) ? def.Weapon : null;

        /// <summary>A gear item anywhere (carried, in the stores, or lying on the ground after a fight), or null.</summary>
        public GearItem FindGear(int gearId)
        {
            foreach (GearItem g in State.Household.GearStore) if (g.Id == gearId) return g;
            foreach (Character c in State.Characters) foreach (GearItem g in c.Gear) if (g.Id == gearId) return g;
            foreach (DroppedLoot l in State.Dropped) foreach (GearItem g in l.Gear) if (g.Id == gearId) return g;
            return null;
        }

        public List<ChronicleEntry> ChronicleOf(Character character)
        {
            var entries = new List<ChronicleEntry>();
            foreach (ChronicleEntry e in State.Chronicle) if (e.CharacterId == character.Id) entries.Add(e);
            return entries;
        }

        public double GetStat(Character character, string statId) => StatSystem.GetStat(Content, character, statId);

        /// <summary>Tags NPCs and factions react to ("scarred", "changed"), from the character's marks.</summary>
        public IReadOnlyCollection<string> ReactionTagsOf(Character character) =>
            StatSystem.Collect(Content, character, EffectKind.Reaction);

        public IReadOnlyCollection<string> AbilitiesOf(Character character) =>
            StatSystem.Collect(Content, character, EffectKind.Ability);

        public List<MarkView> MarkViewsOf(Character character)
        {
            var views = new List<MarkView>();
            foreach (Mark mark in character.Marks)
            {
                MarkDef def = Content.FindMark(mark.DefId);
                MarkStageDef stage = def?.FindStage(mark.Stage);
                views.Add(new MarkView
                {
                    MarkId = mark.Id,
                    DefId = mark.DefId,
                    Name = def?.Name ?? mark.DefId,
                    Category = mark.Category,
                    Region = mark.Region,
                    Severity = mark.Severity,
                    Stage = mark.Stage,
                    OriginText = mark.OriginText,
                    Visual = stage?.Visual ?? new MarkVisual(),
                    Effects = DescribeEffects(stage, mark.Region),
                    Orphaned = stage == null,
                });
            }
            return views;
        }

        private static List<string> DescribeEffects(MarkStageDef stage, BodyRegion region)
        {
            var lines = new List<string>();
            if (stage == null) return lines;
            foreach (MarkEffect effect in stage.Effects)
            {
                if (!effect.AppliesTo(region)) continue;
                switch (effect.Kind)
                {
                    case EffectKind.Reaction: lines.Add("reaction: " + effect.Target); break;
                    case EffectKind.Ability: lines.Add("ability: " + effect.Target); break;
                    default:
                        string line = effect.Target;
                        if (effect.Add != 0) line += (effect.Add > 0 ? " +" : " ") + effect.Add.ToString(CultureInfo.InvariantCulture);
                        if (effect.Mul != 1.0) line += " ×" + effect.Mul.ToString(CultureInfo.InvariantCulture);
                        lines.Add(line);
                        break;
                }
            }
            return lines;
        }

        /// <summary>Marks whose definition or stage no longer exists in content. Check after loading a save.</summary>
        public List<string> FindOrphanedMarks()
        {
            var problems = new List<string>();
            foreach (Character c in State.Characters)
                foreach (Mark mark in c.Marks)
                    if (Content.FindMark(mark.DefId)?.FindStage(mark.Stage) == null)
                        problems.Add(c.GivenName + " " + c.Surname + ": mark " + mark.Id + " refers to missing '" + mark.DefId + "' stage '" + mark.Stage + "'");
            return problems;
        }

        // ---- Persistence ---------------------------------------------------------------------------

        public string ToJson() => SaveFormat.ToJson(State);

        public static Simulation FromJson(string json, ContentDatabase content)
        {
            SimState state = SaveFormat.FromJson(json);
            HomeSystem.EnsureHomestead(state, content);
            // A save made before content gained a settlement gets that settlement now.
            WorldSystem.EnsureSettlements(state, content, new SimRandom(SimRandom.Seed((ulong)state.NextCharacterId, 77)));
            return new Simulation(state, content);
        }
    }

    public static class LifeStages
    {
        /// <summary>Boundaries from the design doc: Child 0-12, Youth 13-17, Adult 18-45, Elder 46-65, Old above.</summary>
        public static LifeStage ForAge(int years)
        {
            if (years <= 12) return LifeStage.Child;
            if (years <= 17) return LifeStage.Youth;
            if (years <= 45) return LifeStage.Adult;
            if (years <= 65) return LifeStage.Elder;
            return LifeStage.Old;
        }
    }
}
