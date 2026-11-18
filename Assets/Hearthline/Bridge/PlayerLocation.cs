using System.Collections.Generic;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Where the player is, as the simulation understands places: area tags, place name, Thin level, whether
    /// they are at the homestead, and distance from home (0 at home, 1 at the region's edge). Read from the
    /// <see cref="AreaVolume"/>s around them. Events, the storyteller and mark origins use it.
    /// </summary>
    public sealed class PlayerLocation : MonoBehaviour
    {
        [Tooltip("The homestead's centre; distance from home is measured from here.")]
        [SerializeField] private Transform home;
        [Tooltip("Distance from home, in metres, that counts as the far edge of the region.")]
        [SerializeField] private float regionRadius = 300f;
        [SerializeField] private string wildernessName = "the wilds";

        private readonly List<string> _tags = new List<string>();

        public string PlaceName { get; private set; } = "";
        public IReadOnlyList<string> AreaTags => _tags;
        public int ThinLevel { get; private set; }
        public bool AtHome { get; private set; }
        public double DistanceFromHome { get; private set; }
        /// <summary>The place the player is at (Content/places), or empty out in the open.</summary>
        public string PlaceId { get; private set; } = "";
        public string PlaceKind { get; private set; } = "";

        private void Update() => Refresh();

        public void Refresh()
        {
            _tags.Clear();
            ThinLevel = 0;
            AtHome = false;
            AreaVolume smallest = null, place = null;
            Vector3 p = transform.position;
            foreach (AreaVolume v in AreaVolume.All)
            {
                if (!v.Contains(p)) continue;
                foreach (string tag in v.AreaTags)
                    if (!_tags.Contains(tag)) _tags.Add(tag);
                ThinLevel = Mathf.Max(ThinLevel, v.ThinLevel);
                AtHome |= v.IsHomestead;
                if (smallest == null || v.Volume < smallest.Volume) smallest = v;
                if (v.PlaceKind.Length > 0 && (place == null || v.Volume < place.Volume)) place = v;
            }
            PlaceId = place != null ? place.PlaceId : "";
            PlaceKind = place != null ? place.PlaceKind : "";
            PlaceName = smallest != null && smallest.PlaceName.Length > 0 ? smallest.PlaceName : wildernessName;

            if (home == null) DistanceFromHome = 0;
            else
            {
                Vector3 d = p - home.position;
                d.y = 0f;
                DistanceFromHome = Mathf.Clamp01(d.magnitude / Mathf.Max(1f, regionRadius));
            }
        }

        /// <summary>The simulation's view of this place, for events and the storyteller.</summary>
        public EventContext ToEventContext(TimeOfDay timeOfDay)
        {
            Refresh();
            var context = new EventContext
            {
                ThinLevel = ThinLevel,
                TimeOfDay = timeOfDay,
                AtHome = AtHome,
                DistanceFromHome = DistanceFromHome,
                PlaceName = PlaceName,
                PlaceKind = PlaceKind.Length > 0 ? PlaceKind : null,
            };
            context.AreaTags.AddRange(_tags);
            return context;
        }
    }
}
