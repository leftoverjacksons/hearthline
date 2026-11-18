using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Shows a homestead structure at the tier the simulation says it is: tier 0 (not built) is usually a
    /// build site, then one model per tier (tent, cabin, farmhouse). Rebuilt from the simulation on load and
    /// whenever the structure changes, so it is never out of step with the save.
    /// </summary>
    public sealed class StructureView : MonoBehaviour
    {
        [SerializeField] private string structureId = "";
        [Tooltip("Index = tier. Index 0 is shown while the structure is not built; it may be empty.")]
        [SerializeField] private GameObject[] tiers = new GameObject[0];

        private SimHost _host;

        public string StructureId => structureId;
        public int Tier { get; private set; }

        /// <summary>
        /// True when a built model (any tier above 0) is on show. Unlike <see cref="Tier"/>, this also holds in the
        /// editor, where the sim isn't running and the scene shows what it was built with.
        /// </summary>
        public bool ShowsBuilt
        {
            get
            {
                for (int i = 1; i < tiers.Length; i++) if (tiers[i] != null && tiers[i].activeSelf) return true;
                return false;
            }
        }

        /// <summary>The model on show for the current tier, or null.</summary>
        public GameObject Current => Tier < tiers.Length ? tiers[Tier] : null;

        /// <summary>Used by scene builders.</summary>
        public void Configure(string id, GameObject[] tierVisuals)
        {
            structureId = id;
            tiers = tierVisuals;
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
            if (e is StructureChanged changed && changed.StructureId == structureId) Refresh();
        }

        public void Refresh()
        {
            Tier = _host != null && _host.Sim != null ? _host.Sim.StructureTier(structureId) : 0;
            int shown = Mathf.Clamp(Tier, 0, Mathf.Max(0, tiers.Length - 1));
            for (int i = 0; i < tiers.Length; i++)
                if (tiers[i] != null) tiers[i].SetActive(i == shown);
        }
    }
}
