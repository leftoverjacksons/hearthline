using System;
using System.Collections.Generic;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Greybox mark visuals: one small coloured blob per mark, placed at the anchor for its body region.
    /// It holds no state of its own; it is rebuilt from the simulation whenever marks change, which is
    /// why marks are still there after a load. Replace with decals/materials/part swaps in M1.
    /// </summary>
    [RequireComponent(typeof(SimCharacterLink))]
    public sealed class CharacterMarksView : MonoBehaviour
    {
        [Serializable]
        public struct RegionAnchor
        {
            public BodyRegion region;
            public Transform anchor;
        }

        [Tooltip("A bone or empty transform per body region. Regions without an anchor fall back to this object.")]
        [SerializeField] private RegionAnchor[] anchors = new RegionAnchor[0];
        [SerializeField] private float markerSize = 0.06f;
        [Tooltip("How much bigger the highlighted marker is drawn, and how fast it pulses.")]
        [SerializeField] private float highlightScale = 1.8f;
        [SerializeField] private float highlightPulseSpeed = 6f;

        private readonly Dictionary<int, GameObject> _markers = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Vector3> _baseScales = new Dictionary<int, Vector3>();
        private SimHost _host;
        private SimCharacterLink _link;

        private void Start()
        {
            _link = GetComponent<SimCharacterLink>();
            _host = SimHost.Instance;
            if (_host == null)
            {
                Debug.LogError("CharacterMarksView needs a SimHost in the scene.", this);
                enabled = false;
                return;
            }
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
            if (e is MarkAdded || e is MarkRemoved || e is MarkStageChanged || e is MarkTransformed || e is CharacterDied)
                Rebuild();
        }

        /// <summary>The mark drawn highlighted (the mirror view's selection), or null for none.</summary>
        public int? HighlightedMarkId { get; private set; }

        public void Highlight(int? markId)
        {
            if (HighlightedMarkId.HasValue) ResetScale(HighlightedMarkId.Value);
            HighlightedMarkId = markId;
        }

        /// <summary>The marker drawn for a mark, or null if it is not drawn.</summary>
        public Transform MarkerFor(int markId) => _markers.TryGetValue(markId, out GameObject m) && m != null ? m.transform : null;

        private void Update()
        {
            if (!HighlightedMarkId.HasValue) return;
            int id = HighlightedMarkId.Value;
            if (!_markers.TryGetValue(id, out GameObject marker) || marker == null) return;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.unscaledTime * highlightPulseSpeed);
            marker.transform.localScale = _baseScales[id] * (highlightScale * pulse);
        }

        private void ResetScale(int markId)
        {
            if (_markers.TryGetValue(markId, out GameObject marker) && marker != null)
                marker.transform.localScale = _baseScales[markId];
        }

        public void Rebuild()
        {
            foreach (GameObject marker in _markers.Values) Destroy(marker);
            _markers.Clear();
            _baseScales.Clear();

            Simulation sim = _host.Sim;
            Character character = _link.Resolve(sim);
            if (character == null) return;

            var countPerRegion = new Dictionary<BodyRegion, int>();
            foreach (MarkView view in sim.MarkViewsOf(character))
            {
                countPerRegion.TryGetValue(view.Region, out int index);
                countPerRegion[view.Region] = index + 1;

                Transform anchor = AnchorFor(view.Region);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Mark " + view.MarkId + " " + view.DefId + " (" + view.Stage + ")";
                Destroy(marker.GetComponent<Collider>());
                marker.transform.SetParent(anchor, false);
                // Stack marks that share an anchor so each one stays visible.
                marker.transform.localPosition = Vector3.up * (index * markerSize * 1.2f);
                marker.transform.localScale = Vector3.one * (markerSize * (0.7f + 0.3f * view.Severity));
                marker.GetComponent<Renderer>().material.color = ColorFor(view.Category);
                _markers[view.MarkId] = marker;
                _baseScales[view.MarkId] = marker.transform.localScale;
            }
        }

        /// <summary>Where marks on a region are drawn; the character root if the region has no anchor.</summary>
        public Transform AnchorFor(BodyRegion region)
        {
            foreach (RegionAnchor a in anchors)
                if (a.region == region && a.anchor != null) return a.anchor;
            return transform;
        }

        private static Color ColorFor(MarkCategory category)
        {
            switch (category)
            {
                case MarkCategory.Wound: return new Color(0.8f, 0.05f, 0.05f);
                case MarkCategory.Scar: return new Color(0.85f, 0.6f, 0.55f);
                case MarkCategory.Loss: return new Color(0.1f, 0.1f, 0.1f);
                case MarkCategory.Exposure: return new Color(0.65f, 0.6f, 1f);
                case MarkCategory.Knack: return new Color(0.2f, 0.9f, 0.9f);
                case MarkCategory.Rite: return new Color(0.9f, 0.75f, 0.2f);
                default: return new Color(0.75f, 0.75f, 0.75f);
            }
        }
    }
}
