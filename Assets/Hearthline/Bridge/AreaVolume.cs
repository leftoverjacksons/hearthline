using System.Collections.Generic;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// A box in the world that says what kind of place it is: area tags from the vocabulary (creek, woods,
    /// road), a place name for mark origins ("at the fallen overpass"), how Thin it is, and whether it is the
    /// homestead. <see cref="PlayerLocation"/> reads these to tell the simulation where the party is.
    /// Boxes may overlap; the smallest one containing a point names the place.
    /// It has no collider, so it never gets in the way of movement or shots.
    /// </summary>
    public sealed class AreaVolume : MonoBehaviour
    {
        private static readonly List<AreaVolume> Registry = new List<AreaVolume>();

        [SerializeField] private string placeName = "";
        [SerializeField] private string[] areaTags = new string[0];
        [SerializeField, Min(0)] private int thinLevel;
        [SerializeField] private bool homestead;
        [Tooltip("Set when this box is a place (docs/specs/ruins-and-places.md): its id and kind from Content/places.")]
        [SerializeField] private string placeId = "";
        [SerializeField] private string placeKind = "";
        [Tooltip("Size of the box in local space, centred on this transform.")]
        [SerializeField] private Vector3 size = new Vector3(20f, 40f, 20f);

        public static IReadOnlyList<AreaVolume> All => Registry;
        public string PlaceName => placeName;
        /// <summary>The box's size in local space (centred on the transform).</summary>
        public Vector3 Size => size;
        public IReadOnlyList<string> AreaTags => areaTags;
        public int ThinLevel => thinLevel;
        public bool IsHomestead => homestead;
        public string PlaceId => placeId;
        public string PlaceKind => placeKind;
        public float Volume => Mathf.Abs(size.x * size.y * size.z * transform.lossyScale.x * transform.lossyScale.y * transform.lossyScale.z);

        /// <summary>Used by scene builders.</summary>
        public void Configure(string place, string[] tags, Vector3 boxSize, int thin = 0, bool isHomestead = false)
        {
            placeName = place;
            areaTags = tags;
            size = boxSize;
            thinLevel = thin;
            homestead = isHomestead;
        }

        /// <summary>Used by scene builders: marks this box as a place the simulation remembers.</summary>
        public void SetPlace(string id, string kind)
        {
            placeId = id;
            placeKind = kind;
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = homestead ? new Color(1f, 0.8f, 0.2f, 0.6f) : new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
