using System;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>What happened when a hit reached the simulation.</summary>
    public readonly struct HitOutcome
    {
        public readonly BodyRegion Region;
        public readonly DamageType Damage;
        public readonly int Severity;
        /// <summary>Null when the simulation accepted the hit; otherwise why it was rejected.</summary>
        public readonly string Error;

        public HitOutcome(BodyRegion region, DamageType damage, int severity, string error)
        {
            Region = region;
            Damage = damage;
            Severity = severity;
            Error = error;
        }

        public bool Ok => Error == null;

        public override string ToString() =>
            Damage + " to " + Region + " (severity " + Severity + ")" + (Ok ? "" : ": rejected, " + Error);
    }

    /// <summary>
    /// Sits on a character root with <see cref="HitRegion"/> zones under its bones. Turns a physical hit into
    /// an <see cref="InflictHit"/> command for the character it stands for; the simulation picks the wound,
    /// records the origin and applies damage. It never changes simulation state any other way.
    /// </summary>
    [RequireComponent(typeof(SimCharacterLink))]
    public sealed class HitReceiver : MonoBehaviour
    {
        public const string LayerName = "Hitbox";

        /// <summary>Raised after every hit sent to the simulation, accepted or not.</summary>
        public event Action<HitOutcome> Hit;

        /// <summary>Layer mask for raycasts against hit zones only.</summary>
        public static int LayerMask => UnityEngine.LayerMask.GetMask(LayerName);

        public HitOutcome Receive(BodyRegion region, DamageType damage, int severity, MarkOrigin origin)
        {
            SimHost host = SimHost.Instance;
            Character character = host == null ? null : GetComponent<SimCharacterLink>().Resolve(host.Sim);
            string error;
            if (character == null)
            {
                error = "no simulation character for " + name;
            }
            else
            {
                CommandResult result = host.Sim.Execute(new InflictHit
                {
                    CharacterId = character.Id,
                    Region = region,
                    Damage = damage,
                    Severity = severity,
                    Origin = origin,
                });
                error = result.Ok ? null : result.Error;
            }

            var outcome = new HitOutcome(region, damage, severity, error);
            if (!outcome.Ok) Debug.LogWarning("Hit on " + name + " " + outcome, this);
            Hit?.Invoke(outcome);
            return outcome;
        }

        /// <summary>
        /// A bullet hit this body (combat, ADR 0021): sends <see cref="ShotHit"/>, and the simulation decides
        /// the severity from the gun, the region and the distance. Returns the outcome with that severity.
        /// </summary>
        public HitOutcome ReceiveShot(BodyRegion region, int? shooterId, int? gearId, double distance)
        {
            SimHost host = SimHost.Instance;
            Character character = host == null ? null : GetComponent<SimCharacterLink>().Resolve(host.Sim);
            string error = null;
            int severity = 0;
            if (character == null) error = "no simulation character for " + name;
            else
            {
                int marksBefore = character.Marks.Count;
                CommandResult result = host.Sim.Execute(new ShotHit
                {
                    TargetId = character.Id, Region = region, ShooterId = shooterId, GearId = gearId, Distance = distance,
                });
                if (!result.Ok) error = result.Error;
                else if (character.Marks.Count > marksBefore) severity = character.Marks[character.Marks.Count - 1].Severity;
            }
            var outcome = new HitOutcome(region, DamageType.Gunshot, severity, error);
            if (!outcome.Ok) Debug.LogWarning("Shot on " + name + " " + outcome, this);
            Hit?.Invoke(outcome);
            return outcome;
        }

        /// <summary>
        /// Casts against hit zones only (triggers included) and returns the nearest zone the ray strikes.
        /// </summary>
        public static bool Raycast(Ray ray, float maxDistance, out HitRegion zone, out RaycastHit hit)
        {
            zone = null;
            if (!Physics.Raycast(ray, out hit, maxDistance, LayerMask, QueryTriggerInteraction.Collide)) return false;
            zone = hit.collider.GetComponent<HitRegion>();
            return zone != null;
        }
    }
}
