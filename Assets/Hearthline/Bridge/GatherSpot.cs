using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// A place in the world to gather from: a berry bush, an herb patch, deadfall, a salvage pile. Sends
    /// <see cref="Gather"/> with this spot's stable id. Whether it is picked over lives in the simulation, so
    /// the spot looks right after a load; it shows <see cref="full"/> while it can be gathered and
    /// <see cref="picked"/> (optional) while it regrows.
    /// </summary>
    public sealed class GatherSpot : Interactable
    {
        [SerializeField] private string spotId = "";
        [SerializeField] private string itemId = "";
        [Tooltip("Verb for the prompt: Pick, Gather, Search.")]
        [SerializeField] private string verb = "Gather";
        [SerializeField] private GameObject full;
        [SerializeField] private GameObject picked;

        private SimHost _host;

        public string SpotId => spotId;
        public string ItemId => itemId;

        /// <summary>Used by scene builders.</summary>
        public void Configure(string id, string item, string promptVerb, GameObject fullVisual, GameObject pickedVisual)
        {
            spotId = id;
            itemId = item;
            verb = promptVerb;
            full = fullVisual;
            picked = pickedVisual;
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
            if (e is DateAdvanced || (e is Gathered g && g.SpotId == spotId)) Refresh();
        }

        private int DaysLeft => _host != null && _host.Sim != null ? _host.Sim.DaysUntilRegrown(spotId, itemId) : 0;

        public bool PickedOver => DaysLeft > 0;

        public void Refresh()
        {
            bool spent = PickedOver;
            if (full != null) full.SetActive(!spent);
            if (picked != null) picked.SetActive(spent);
        }

        private string ItemName => _host != null && _host.Content.Items.TryGetValue(itemId, out ItemDef def) ? def.Name : itemId;

        public override bool CanInteract => _host != null && !PickedOver;

        public override string Prompt
        {
            get
            {
                int left = DaysLeft;
                return left > 0 ? "Picked over (back in " + left + (left == 1 ? " day)" : " days)") : verb + " " + ItemName.ToLowerInvariant();
            }
        }

        public override string Interact()
        {
            CommandResult result = _host.Sim.Execute(new Gather { SpotId = spotId, ItemId = itemId });
            if (!result.Ok) return result.Error;
            foreach (DomainEvent e in result.Events)
                if (e is Gathered g) return "+" + g.Count + " " + ItemName + (g.IntoPack ? " (pack)" : " (stores)");
            return "";
        }
    }
}
