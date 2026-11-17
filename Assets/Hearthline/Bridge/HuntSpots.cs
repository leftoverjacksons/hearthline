using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// One sign on a hunt's trail (docs/specs/hunt.md): tracks, a rub, droppings or a bed. Walk up and press E
    /// to read it; the simulation decides how far along the trail that gets you.
    /// </summary>
    public sealed class SignSpot : Interactable
    {
        public static readonly string[] Kinds = { "Fresh tracks", "A rub on a sapling", "Droppings, still soft", "Pressed-down grass where he lay", "Nibbled browse" };

        [SerializeField] private int index;
        [SerializeField] private string kind = "Fresh tracks";

        public int Index => index;
        public string Kind => kind;

        public void Configure(int signIndex, string signKind)
        {
            index = signIndex;
            kind = signKind;
        }

        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        public override bool CanInteract => Sim?.Hunt != null && !Sim.Hunt.Revealed && Sim.Hunt.Progress == index;
        public override string Prompt => CanInteract ? "Read the sign: " + kind.ToLowerInvariant() : kind;

        public override string Interact()
        {
            CommandResult r = Sim.Execute(new ReadSign { Index = index });
            return r.Ok ? null : r.Error;
        }
    }

    /// <summary>The hunted animal, down. E to butcher it: meat, hide and the rest into the pack (or the stores).</summary>
    public sealed class ButcherSpot : Interactable
    {
        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        public override bool CanInteract
        {
            get
            {
                Simulation sim = Sim;
                Character animal = sim?.Hunt != null ? sim.FindCharacter(sim.Hunt.AnimalId) : null;
                return animal != null && (!animal.Alive || animal.Downed);
            }
        }

        public override string Prompt
        {
            get
            {
                Simulation sim = Sim;
                Character animal = sim?.Hunt != null ? sim.FindCharacter(sim.Hunt.AnimalId) : null;
                return animal != null ? "Butcher " + animal.GivenName : "Nothing left";
            }
        }

        public override string Interact()
        {
            CommandResult r = Sim.Execute(new ButcherAnimal());
            if (!r.Ok) return r.Error;
            foreach (DomainEvent e in r.Events)
                if (e is HuntEnded ended)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var kv in ended.Yields)
                        parts.Add("+" + kv.Value + " " + (Sim.Content.Items.TryGetValue(kv.Key, out var d) ? d.Name : kv.Key));
                    return string.Join(", ", parts);
                }
            return null;
        }
    }
}
