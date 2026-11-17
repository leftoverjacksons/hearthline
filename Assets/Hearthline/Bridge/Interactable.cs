using System.Collections.Generic;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Something in the world the player can walk up to and use with the interact key: a berry bush, a
    /// salvage pile, a building slot, a bed. The player's interactor finds the nearest one in front of them,
    /// shows <see cref="Prompt"/>, and calls <see cref="Interact"/>. Subclasses act only through simulation
    /// commands.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        private static readonly List<Interactable> Registry = new List<Interactable>();

        [Tooltip("How close the player must be, in metres.")]
        [SerializeField] private float reach = 2.2f;

        public static IReadOnlyList<Interactable> All => Registry;
        public float Reach => reach;

        /// <summary>What pressing the key would do ("Pick wild berries"), or why it cannot ("Picked over").</summary>
        public abstract string Prompt { get; }
        public abstract bool CanInteract { get; }

        /// <summary>Does it. Returns a line for the HUD ("+3 Wild berries (pack)") or why it failed.</summary>
        public abstract string Interact();

        /// <summary>Where the player should be measured from.</summary>
        public virtual Vector3 Position => transform.position;

        protected virtual void OnEnable() => Registry.Add(this);
        protected virtual void OnDisable() => Registry.Remove(this);
    }
}
