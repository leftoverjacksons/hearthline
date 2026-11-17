using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Says which simulation character a GameObject stands for. Other bridge components on the same
    /// character (marks view, hit receiver) ask it rather than each keeping their own id. Holds no state
    /// that needs saving: it is either "whoever heads the household" or a fixed character id.
    /// </summary>
    public sealed class SimCharacterLink : MonoBehaviour
    {
        [Tooltip("Stand for whoever is head of household. Turn off to stand for a fixed character id.")]
        [SerializeField] private bool followHeadOfHousehold = true;
        [SerializeField] private int characterId;

        /// <summary>Makes this object stand for one particular character (spawned enemies, figures).</summary>
        public void Bind(int id)
        {
            followHeadOfHousehold = false;
            characterId = id;
        }

        public int? BoundId => followHeadOfHousehold ? (int?)null : characterId;

        /// <summary>The character this object stands for, or null if there is none (or they are gone).</summary>
        public Character Resolve(Simulation sim)
        {
            if (sim == null) return null;
            return followHeadOfHousehold ? sim.Head : sim.FindCharacter(characterId);
        }
    }
}
