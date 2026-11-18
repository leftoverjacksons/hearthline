using System.Collections.Generic;
using System.Linq;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Something to go through at a place (docs/specs/ruins-and-places.md): a search spot (a heap, a cupboard,
    /// a car), or the place's one hidden find. Sends <see cref="SearchSpot"/> or <see cref="TakeFind"/>. What has
    /// been taken lives in the simulation, so it shows <see cref="full"/> or <see cref="searched"/> right after a load.
    /// </summary>
    public sealed class PlaceSpot : Interactable
    {
        [SerializeField] private string placeId = "";
        [SerializeField] private string placeKind = "";
        [Tooltip("Search spot index at this place; -1 for the place's hidden find.")]
        [SerializeField] private int spot;
        [Tooltip("What the prompt calls it: \"the altar step\", \"a heap of sacks\".")]
        [SerializeField] private string what = "the heap";
        [SerializeField] private GameObject full;
        [SerializeField] private GameObject searched;

        private SimHost _host;

        public string PlaceId => placeId;
        public string PlaceKind => placeKind;
        public int Spot => spot;
        public bool IsFind => spot < 0;

        /// <summary>Used by scene builders.</summary>
        public void Configure(string id, string kind, int index, string description, GameObject fullVisual, GameObject searchedVisual)
        {
            placeId = id;
            placeKind = kind;
            spot = index;
            what = description;
            full = fullVisual;
            searched = searchedVisual;
        }

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host == null) return;
            _host.SimReplaced += Refresh;
            _host.EventRaised += OnDomainEvent;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_host == null) return;
            _host.SimReplaced -= Refresh;
            _host.EventRaised -= OnDomainEvent;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if ((e is SpotSearched s && s.PlaceId == placeId) || (e is FindTaken f && f.PlaceId == placeId) || e is DateAdvanced) Refresh();
        }

        private Simulation Sim => _host != null ? _host.Sim : null;

        public bool Done => Sim != null && (IsFind ? Sim.IsFindTaken(placeId) : Sim.IsSearched(placeId, spot));

        private bool MissedToday => IsFind && Sim != null && Sim.Place(placeId) != null && Sim.Place(placeId).FindMissedDay == Sim.State.Day;

        public void Refresh()
        {
            bool done = Done;
            if (full != null) full.SetActive(!done);
            if (searched != null) searched.SetActive(done);
        }

        public override bool CanInteract => Sim != null && !Done && !MissedToday;

        public override string Prompt
        {
            get
            {
                if (Done) return "Gone through";
                if (!IsFind) return "Search " + what;
                if (MissedToday) return "Nothing more to see today";
                PlaceDef def = _host.Content.Places.TryGetValue(placeKind, out PlaceDef d) ? d : null;
                if (def?.Find?.CheckStat == null) return "Search " + what;
                string stat = _host.Content.Vocab.Stats.TryGetValue(def.Find.CheckStat, out StatDef s) ? s.Name : def.Find.CheckStat;
                return "Search " + what + "  (" + stat + " " + def.Find.CheckDifficulty + ": something was hidden here)";
            }
        }

        public override string Interact()
        {
            CommandResult r = IsFind
                ? Sim.Execute(new TakeFind { PlaceId = placeId, Kind = placeKind })
                : Sim.Execute(new SearchSpot { PlaceId = placeId, Kind = placeKind, Spot = spot });
            if (!r.Ok) return r.Error;
            foreach (DomainEvent e in r.Events)
            {
                if (e is SpotSearched s)
                    return s.Items.Count == 0 ? "Nothing worth carrying." : string.Join(", ", s.Items.OrderBy(kv => kv.Key).Select(kv => "+" + kv.Value + " " + ItemName(kv.Key))) + (s.IntoPack ? " (pack)" : " (stores)");
                if (e is FindTaken f) return f.Text + "  +" + f.Count + " " + ItemName(f.ItemId);
                if (e is FindMissed) return "Something was hidden here. You can't find it. Maybe another day.";
            }
            return "";
        }

        private string ItemName(string id) => _host.Content.Items.TryGetValue(id, out ItemDef def) ? def.Name.ToLowerInvariant() : id;

        /// <summary>All the spots at one place.</summary>
        public static List<PlaceSpot> At(string id) => Object.FindObjectsByType<PlaceSpot>(FindObjectsInactive.Exclude).Where(p => p.placeId == id).ToList();
    }
}
