using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// A hit zone on a character's body: a trigger collider on the Hitbox layer, parented to a bone, that
    /// says which body region a hit there lands on. The <see cref="HitReceiver"/> on the character root
    /// turns hits on it into simulation wounds.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HitRegion : MonoBehaviour
    {
        [SerializeField] private BodyRegion region;

        public BodyRegion Region => region;

        /// <summary>The receiver for the character this zone belongs to.</summary>
        public HitReceiver Receiver => GetComponentInParent<HitReceiver>();

        /// <summary>Used by the scene builder; regions are not meant to change at runtime.</summary>
        public void Configure(BodyRegion bodyRegion) => region = bodyRegion;
    }
}
