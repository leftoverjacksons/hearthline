using System.Collections.Generic;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// A homestead building you walk up to and use. Pressing E opens its panel: what it is and does, what
    /// it is for (sleep in the dwelling, look over the household at the hearth, treat wounds in the
    /// infirmary), and what the next tier costs, with a button to build it. Everything goes through
    /// simulation commands; the model on show is the <see cref="StructureView"/>'s job.
    /// </summary>
    public sealed class HomeBuilding : Interactable, IPausesPlayer
    {
        public enum Kind { Dwelling, Hearth, Infirmary }

        [SerializeField] private Kind kind;
        [SerializeField] private StructureView view;
        [SerializeField] private TravelClock clock;
        [SerializeField] private RoadEvents roadEvents;

        private static readonly List<string> LastReport = new List<string>();
        private bool _open;
        private string _status = "";
        private Vector2 _scroll;
        private SimHost _host;

        public bool IsOpen => _open;
        public bool PausesPlayer => _open;
        public string Status => _status;
        public StructureView View => view;

        /// <summary>Used by scene builders.</summary>
        public void Configure(Kind buildingKind, StructureView structure, TravelClock travelClock, RoadEvents events)
        {
            kind = buildingKind;
            view = structure;
            clock = travelClock;
            roadEvents = events;
        }

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host != null && kind == Kind.Hearth) _host.EventRaised += OnDomainEvent;
        }

        private void OnDestroy()
        {
            if (_host != null && kind == Kind.Hearth) _host.EventRaised -= OnDomainEvent;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if (!(e is HomeReported report)) return;
            LastReport.Clear();
            foreach (HomeReportLine line in report.Lines)
            {
                Character who = Sim.FindCharacter(line.CharacterId);
                LastReport.Add((who != null ? who.GivenName : "someone") + " (" + line.JobId + "): " + (line.Problem ?? (line.Items.Count == 0 ? "kept things going" : string.Join(", ", line.Items.Select(kv => kv.Value + " " + ItemName(kv.Key))))));
            }
            if (report.HungryMembers > 0) LastReport.Add(report.HungryMembers + " went hungry.");
        }

        private Simulation Sim => _host != null ? _host.Sim : null;
        private StructureDef Def => _host != null && _host.Content.Structures.TryGetValue(view.StructureId, out StructureDef d) ? d : null;
        private int Tier => Sim != null ? Sim.StructureTier(view.StructureId) : 0;
        private string TierName => Tier == 0 ? "not built" : Def.Tiers[Tier - 1].Name;

        public override string Prompt => Def == null ? "" : Tier == 0 ? Def.Name + " (build site)" : TierName;
        public override bool CanInteract => Def != null && !_open;

        public override string Interact()
        {
            _open = true;
            _status = "";
            return "";
        }

        public void Close() => _open = false;

        // ---- Actions (also used by tests) ------------------------------------------------------------

        /// <summary>What the next tier needs that the stores do not have, or null when it can be built.</summary>
        public string Shortfall()
        {
            StructureDef def = Def;
            if (Tier >= def.Tiers.Count) return def.Name + " is as good as it gets";
            var missing = new List<string>();
            foreach (KeyValuePair<string, int> cost in def.Tiers[Tier].Cost.OrderBy(c => c.Key))
            {
                int have = Sim.ItemCount(cost.Key);
                if (have < cost.Value) missing.Add((cost.Value - have) + " more " + ItemName(cost.Key).ToLowerInvariant());
            }
            return missing.Count == 0 ? null : "needs " + string.Join(", ", missing);
        }

        public string Build()
        {
            StructureDef def = Def;
            string next = Tier < def.Tiers.Count ? def.Tiers[Tier].Name : def.Name;
            CommandResult result = Sim.Execute(new BuildStructure { StructureId = def.Id });
            return _status = result.Ok ? "Built the " + next.ToLowerInvariant() + "." : result.Error;
        }

        public string Sleep()
        {
            if (clock == null) return _status = "No clock.";
            _status = clock.MakeCamp();
            _open = false;
            if (roadEvents != null) roadEvents.StorytellerTurn();
            return _status;
        }

        public string Treat(int characterId, int markId)
        {
            CommandResult result = Sim.Execute(new TreatWoundAtHome { CharacterId = characterId, MarkId = markId });
            if (!result.Ok) return _status = result.Error;
            foreach (DomainEvent e in result.Events)
                if (e is WoundTreated t) return _status = "Treated. " + (t.Outcome != null ? "Likely outcome: " + t.Outcome.Replace('_', ' ') + "." : "");
            return _status = "Treated.";
        }

        // ---- Panel ---------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!_open || Sim == null || Def == null) return;
            GUILayout.Window(0x484F4D42 + (int)kind, new Rect((Screen.width - 520f) / 2f, Screen.height * 0.12f, 520f, 0f), DrawPanel, Def.Name + ": " + TierName);
        }

        private void DrawPanel(int id)
        {
            Simulation sim = Sim;
            StructureDef def = Def;
            var wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (Tier > 0) GUILayout.Label(Provides(def.Tiers[Tier - 1]), wrap);

            GUILayout.Space(6);
            switch (kind)
            {
                case Kind.Dwelling: DrawDwelling(sim); break;
                case Kind.Hearth: DrawHearth(sim, wrap); break;
                case Kind.Infirmary: DrawInfirmary(sim); break;
            }

            GUILayout.Space(8);
            if (Tier < def.Tiers.Count)
            {
                StructureTier next = def.Tiers[Tier];
                string cost = string.Join(", ", next.Cost.OrderBy(c => c.Key).Select(c => c.Value + " " + ItemName(c.Key).ToLowerInvariant() + " (have " + sim.ItemCount(c.Key) + ")"));
                GUILayout.Label("Next: " + next.Name + " — " + (cost.Length > 0 ? cost : "free") + ". " + Provides(next), wrap);
                string shortfall = Shortfall();
                GUI.enabled = shortfall == null && !sim.OnTrip;
                if (GUILayout.Button(shortfall == null ? "Build the " + next.Name.ToLowerInvariant() : "Cannot build: " + shortfall, GUILayout.Height(26))) Build();
                GUI.enabled = true;
            }
            else GUILayout.Label("Fully built.");

            if (_status.Length > 0) GUILayout.Label(_status, wrap);
            if (GUILayout.Button("Close", GUILayout.Height(24))) Close();
        }

        private void DrawDwelling(Simulation sim)
        {
            int living = sim.State.Household.MemberIds.Count(id => sim.FindCharacter(id)?.Alive == true);
            GUILayout.Label("Living here: " + living + " of " + sim.HomeStatOf(HomeStat.Capacity) + ".   " + (clock != null ? "It is " + clock.HourText + "." : ""));
            if (GUILayout.Button("Sleep until morning", GUILayout.Height(26))) Sleep();
        }

        private void DrawHearth(Simulation sim, GUIStyle wrap)
        {
            int food = sim.State.Household.Inventory.Where(kv => _host.Content.Items.TryGetValue(kv.Key, out ItemDef d) && d.Tags.Contains(HomeRules.FoodTag)).Sum(kv => kv.Value);
            GUILayout.Label("Morale " + sim.State.Household.Morale + ".   Food in the stores: " + food + ".   Preserving: " + sim.HomeStatOf(HomeStat.Preserving) + ".");
            foreach (int id in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(id);
                if (c == null || !c.Alive) continue;
                string job = c == sim.Head ? "head of household" : c.JobId != null ? _host.Content.Jobs[c.JobId].Name.ToLowerInvariant() : "no work";
                GUILayout.Label("  " + c.GivenName + ", " + sim.AgeOf(c) + " · " + job + (c.Downed ? " · laid up" : ""));
            }
            GUILayout.Label(LastReport.Count == 0 ? "No season has turned yet." : "Last season's report:");
            foreach (string line in LastReport) GUILayout.Label("  " + line, wrap);
        }

        private void DrawInfirmary(Simulation sim)
        {
            if (Tier == 0) { GUILayout.Label("There is nowhere to treat a wound until something is built here."); return; }
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
            bool any = false;
            foreach (int id in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(id);
                if (c == null || !c.Alive) continue;
                foreach (MarkView mark in sim.MarkViewsOf(c))
                {
                    if (mark.Category != MarkCategory.Wound) continue;
                    any = true;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(c.GivenName + ": " + mark.Name + " (" + mark.Region + ", " + mark.Stage + ")", GUILayout.Width(380));
                    if (GUILayout.Button("Treat")) Treat(c.Id, mark.MarkId);
                    GUILayout.EndHorizontal();
                }
            }
            if (!any) GUILayout.Label("Nobody here has a wound that needs treating.");
            GUILayout.EndScrollView();
        }

        private static string Provides(StructureTier tier) =>
            tier.Provides.Count == 0 ? "" : "Gives " + string.Join(", ", tier.Provides.Select(p => p.Key.ToString().ToLowerInvariant() + " " + p.Value)) + ".";

        private string ItemName(string id) => _host != null && _host.Content.Items.TryGetValue(id, out ItemDef d) ? d.Name : id;
    }
}
