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
    public sealed class CharacterMarksView : MonoBehaviour
    {
        [Serializable]
        public struct RegionAnchor
        {
            public BodyRegion region;
            public Transform anchor;
        }

        [Tooltip("Show whoever is head of household. Turn off to show a fixed character id.")]
        [SerializeField] private bool followHeadOfHousehold = true;
        [SerializeField] private int characterId;
        [Tooltip("A bone or empty transform per body region. Regions without an anchor fall back to this object.")]
        [SerializeField] private RegionAnchor[] anchors = new RegionAnchor[0];
        [SerializeField] private float markerSize = 0.06f;

        private readonly List<GameObject> _markers = new List<GameObject>();
        private SimHost _host;

        private void Start()
        {
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

        public void Rebuild()
        {
            foreach (GameObject marker in _markers) Destroy(marker);
            _markers.Clear();

            Simulation sim = _host.Sim;
            Character character = followHeadOfHousehold ? sim.Head : sim.FindCharacter(characterId);
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
                _markers.Add(marker);
            }
        }

        private Transform AnchorFor(BodyRegion region)
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
