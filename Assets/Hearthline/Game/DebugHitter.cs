using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// Debug stand-in for combat until M2: fires a real ray at the target character from a random direction
    /// and height. Whichever hit zone the ray strikes decides the body region, so the whole physical path
    /// (zones on bones, raycast, receiver, simulation) is exercised. Used by the cheat panel and a hotkey.
    /// </summary>
    public sealed class DebugHitter : MonoBehaviour
    {
        [SerializeField] private HitReceiver target;
        [SerializeField] private Key hotkey = Key.F2;

        [Header("Hotkey hit")]
        [SerializeField] private DamageType damage = DamageType.Gunshot;
        [SerializeField, Range(1, 3)] private int severity = 1;
        [SerializeField] private string cause = "a stray shot";
        [SerializeField] private string place = "the greybox";

        [Header("Shot geometry")]
        [Tooltip("How far from the target the shot starts.")]
        [SerializeField] private float distance = 4f;
        [Tooltip("Heights on the target (metres above its feet) the shot aims between.")]
        [SerializeField] private Vector2 aimHeight = new Vector2(0.15f, 1.85f);
        [SerializeField] private int attemptsPerShot = 12;

        public string LastResult { get; private set; } = "";

        private void Awake()
        {
            if (target == null) target = FindAnyObjectByType<HitReceiver>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[hotkey].wasPressedThisFrame)
                Fire(damage, severity, new MarkOrigin { Cause = cause, Place = place });
        }

        /// <summary>
        /// Shoots at a random spot on the target. Retries a few random lines if one passes between limbs.
        /// Returns a line for the cheat panel.
        /// </summary>
        public string Fire(DamageType damageType, int hitSeverity, MarkOrigin origin)
        {
            if (target == null) return LastResult = "No HitReceiver to shoot at.";
            Vector3 feet = target.transform.position;
            for (int attempt = 0; attempt < attemptsPerShot; attempt++)
            {
                Vector3 aim = feet + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(aimHeight.x, aimHeight.y), Random.Range(-0.15f, 0.15f));
                Vector2 around = Random.insideUnitCircle.normalized * distance;
                Vector3 from = new Vector3(feet.x + around.x, aim.y + Random.Range(-0.5f, 0.5f), feet.z + around.y);
                var ray = new Ray(from, (aim - from).normalized);
                if (!HitReceiver.Raycast(ray, distance * 2f, out HitRegion zone, out RaycastHit hit)) continue;
                if (zone.Receiver != target) continue;

                Debug.DrawLine(from, hit.point, Color.red, 3f);
                HitOutcome outcome = target.Receive(zone.Region, damageType, hitSeverity, origin);
                return LastResult = (outcome.Ok ? "Hit: " : "") + outcome;
            }
            return LastResult = "Missed (" + attemptsPerShot + " shots passed by).";
        }
    }
}
