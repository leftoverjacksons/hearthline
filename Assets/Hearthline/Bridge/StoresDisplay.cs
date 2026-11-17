using System;
using System.Collections.Generic;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Shows what is in the household's stores as things you can see: sacks of food, baskets of berries, a
    /// woodpile, bundles of herbs, scrap and crates, placed on the shed's slots. Rebuilt from the simulation
    /// whenever the stores change or a game is loaded; it holds no state of its own.
    /// </summary>
    public sealed class StoresDisplay : MonoBehaviour
    {
        [Serializable]
        public sealed class Rule
        {
            [Tooltip("Matches an item id exactly, or else any item carrying this tag.")]
            public string itemOrTag;
            public GameObject prop;
            [Tooltip("How many of the item one prop stands for.")]
            [Min(1)] public int unitsPerProp = 4;
            [Min(1)] public int maxProps = 4;
        }

        [Tooltip("First matching rule wins for each item. Put exact item ids before tags.")]
        [SerializeField] private Rule[] rules = new Rule[0];
        [Tooltip("Where props go, in order.")]
        [SerializeField] private Transform[] slots = new Transform[0];

        private readonly List<GameObject> _props = new List<GameObject>();
        private SimHost _host;

        public int PropCount => _props.Count;

        /// <summary>Used by scene builders.</summary>
        public void Configure(Rule[] storeRules, Transform[] propSlots)
        {
            rules = storeRules;
            slots = propSlots;
        }

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host == null) return;
            _host.SimReplaced += Rebuild;
            _host.EventRaised += OnDomainEvent;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_host == null) return;
            _host.SimReplaced -= Rebuild;
            _host.EventRaised -= OnDomainEvent;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if (e is InventoryChanged || e is TripEnded || e is TripStarted || e is ItemsSpoiled) Rebuild();
        }

        /// <summary>How many props a rule shows for the current stores. Public for tests.</summary>
        public int PropsFor(string itemOrTag)
        {
            int n = 0;
            foreach (GameObject p in _props) if (p.name.StartsWith(itemOrTag + ":")) n++;
            return n;
        }

        public void Rebuild()
        {
            foreach (GameObject p in _props) Destroy(p);
            _props.Clear();
            if (_host == null || _host.Sim == null) return;

            var totals = new int[rules.Length];
            foreach (KeyValuePair<string, int> stock in _host.Sim.State.Household.Inventory)
            {
                int r = RuleFor(stock.Key);
                if (r >= 0) totals[r] += stock.Value;
            }

            int slot = 0;
            for (int r = 0; r < rules.Length && slot < slots.Length; r++)
            {
                Rule rule = rules[r];
                if (rule.prop == null || totals[r] <= 0) continue;
                int count = Mathf.Min(rule.maxProps, Mathf.CeilToInt(totals[r] / (float)rule.unitsPerProp));
                for (int i = 0; i < count && slot < slots.Length; i++, slot++)
                {
                    GameObject prop = Instantiate(rule.prop, slots[slot], false);
                    prop.name = rule.itemOrTag + ":" + i;
                    _props.Add(prop);
                }
            }
        }

        private int RuleFor(string itemId)
        {
            for (int r = 0; r < rules.Length; r++)
                if (rules[r].itemOrTag == itemId) return r;
            if (_host.Content.Items.TryGetValue(itemId, out ItemDef def))
                for (int r = 0; r < rules.Length; r++)
                    if (def.Tags.Contains(rules[r].itemOrTag)) return r;
            return -1;
        }
    }
}
