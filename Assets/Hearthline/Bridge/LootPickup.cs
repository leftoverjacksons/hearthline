using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// What a fallen (or disarmed) enemy left on the ground after a fight (combat spec, step 5). Walk up and
    /// press E to take it: goods go into the pack on a trip (or the stores at home), guns and gear into your
    /// hands, each with a line in its history saying whose it was.
    /// </summary>
    public sealed class LootPickup : Interactable
    {
        [SerializeField] private int lootId;

        private SimHost _host;

        public int LootId => lootId;

        public void Configure(int id) => lootId = id;

        protected override void OnEnable()
        {
            base.OnEnable();
            _host = SimHost.Instance;
        }

        private DroppedLoot Loot => _host != null ? _host.Sim.State.Dropped.Find(l => l.Id == lootId) : null;

        public override bool CanInteract => Loot != null;

        public override string Prompt
        {
            get
            {
                DroppedLoot loot = Loot;
                if (loot == null) return "Nothing left";
                Character from = loot.FromCharacterId.HasValue ? _host.Sim.FindCharacter(loot.FromCharacterId.Value) : null;
                return from != null ? "Search " + (from.GivenName + " " + from.Surname).Trim() : "Take what was left";
            }
        }

        public override string Interact()
        {
            DroppedLoot loot = Loot;
            if (loot == null) return "Nothing left.";
            var taken = new List<string>();
            ContentDatabase content = _host.Sim.Content;
            foreach (var kv in loot.Items) taken.Add("+" + kv.Value + " " + (content.Items.TryGetValue(kv.Key, out ItemDef d) ? d.Name : kv.Key));
            foreach (GearItem g in loot.Gear) taken.Add(content.Items.TryGetValue(g.DefId, out ItemDef d) ? d.Name : g.DefId);
            CommandResult r = _host.Sim.Execute(new PickUpLoot { LootId = lootId });
            if (!r.Ok) return r.Error;
            Destroy(gameObject);
            return taken.Count > 0 ? string.Join(", ", taken) : "Nothing worth taking.";
        }
    }
}
