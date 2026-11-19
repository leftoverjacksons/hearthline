using System.Collections;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using StarterAssets;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// The player's guns (combat spec, step 2):
    /// - 1 and 2 draw or holster the long gun and the sidearm;
    /// - right mouse aims (drawing the long gun if nothing is drawn);
    /// - left mouse fires;
    /// - R reloads one round at a time, or clears a jam.
    ///
    /// The simulation decides whether a round fires, jams or is missing, and what a hit does. This component
    /// decides where the bullet goes: a ray through the crosshair with spread from the gun, the character's
    /// aim steadiness and stance, and how long they have held still. Feel values are for Travis.
    /// </summary>
    [RequireComponent(typeof(AimController), typeof(GunVisuals))]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        public enum ShotKind { None, Fired, Jammed, Refused }

        /// <summary>What one pull of the trigger did.</summary>
        public struct ShotResult
        {
            public ShotKind Kind;
            public string Message;
            public bool HitSomething;
            public Vector3 Point;
            public HitRegion Zone;
            public int Severity;
        }

        [SerializeField] private Key longGunKey = Key.Digit1;
        [SerializeField] private Key sidearmKey = Key.Digit2;
        [SerializeField] private Key reloadKey = Key.R;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private Light muzzleFlash;

        [Header("Feel (tune by feel)")]
        [SerializeField] private float drawSeconds = 0.45f;
        [SerializeField] private float clearJamSeconds = 1.2f;
        [Tooltip("Spread multiplier when firing from the hip.")]
        [SerializeField] private float hipSpread = 3f;
        [Tooltip("Spread multiplier while moving.")]
        [SerializeField] private float movingSpread = 1.8f;
        [Tooltip("Spread multiplier while crouched.")]
        [SerializeField] private float crouchedSpread = 0.8f;
        [Tooltip("Camera kick per shot (impulse strength).")]
        [SerializeField] private float recoil = 0.35f;
        [SerializeField] private float maxRange = 300f;

        private AimController _aim;
        private GunVisuals _visuals;
        private HitReceiver _ownReceiver;
        private PlayerInputRelease _release;
        private Crouch _crouch;
        private CharacterController _body;
        private CinemachineImpulseSource _impulse;
        private Animator _animator;
        /// <summary>Animator bool: the gun is shouldered (aim layer: Rifle Aiming Idle instead of the low ready).</summary>
        public const string AimingParameter = "Aiming";
        private bool _animatorHasAiming;
        private bool _animatorHasWeapon;
        private int _aimLayer = -1;
        private float _drawTimer, _cooldown, _flashTimer, _messageTimer, _hitMarkerTimer;
        private string _message;
        private Coroutine _reloading;

        /// <summary>Raised when any shot is fired (where from), so animals can hear it.</summary>
        public static event System.Action<Vector3> ShotFired;

        public GunVisuals.Drawn Drawn { get; private set; }
        public bool Ready => Drawn != GunVisuals.Drawn.None && _drawTimer <= 0f;
        public bool Reloading => _reloading != null;
        public ShotResult LastShot { get; private set; }
        /// <summary>Holds aim without the mouse (tests, screenshots, the cheat panel).</summary>
        public bool ForceAim { get; set; }

        public void Configure(LineRenderer tracerLine, Light flash) { tracer = tracerLine; muzzleFlash = flash; }

        private void Awake()
        {
            _aim = GetComponent<AimController>();
            _visuals = GetComponent<GunVisuals>();
            _ownReceiver = GetComponent<HitReceiver>();
            _release = GetComponent<PlayerInputRelease>();
            _crouch = GetComponent<Crouch>();
            _body = GetComponent<CharacterController>();
            _impulse = GetComponent<CinemachineImpulseSource>();
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    _animatorHasWeapon |= p.name == "Weapon";
                    _animatorHasAiming |= p.name == AimingParameter;
                }
                _aimLayer = _animator.GetLayerIndex("Aim");
            }
            if (tracer != null) tracer.enabled = false;
            if (muzzleFlash != null) muzzleFlash.enabled = false;
        }

        // ---- What the head is carrying -------------------------------------------------------------

        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        private GearItem Gun(GunVisuals.Drawn which)
        {
            Simulation sim = Sim;
            Character head = sim?.Head;
            if (head == null || which == GunVisuals.Drawn.None) return null;
            EquipSlot slot = which == GunVisuals.Drawn.LongGun ? EquipSlot.LongGun : EquipSlot.Sidearm;
            foreach (GearItem g in head.Gear)
                if (g.Equipped && sim.Content.Items.TryGetValue(g.DefId, out ItemDef def) && def.Equip != null && def.Equip.Slot == slot && def.Weapon != null) return g;
            return null;
        }

        public GearItem DrawnGun => Gun(Drawn);

        public void Draw(GunVisuals.Drawn which)
        {
            if (which != GunVisuals.Drawn.None && Gun(which) == null) { Say("Nothing to draw."); return; }
            if (which == Drawn) return;
            CancelReload();
            Drawn = which;
            _drawTimer = which == GunVisuals.Drawn.None ? 0f : drawSeconds;
        }

        // ---- Input ---------------------------------------------------------------------------------

        private void Update()
        {
            float dt = Time.deltaTime;
            _drawTimer = Mathf.Max(0f, _drawTimer - dt);
            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _messageTimer -= Time.unscaledDeltaTime;
            _hitMarkerTimer -= Time.unscaledDeltaTime;
            if (_flashTimer > 0f && (_flashTimer -= dt) <= 0f)
            {
                if (tracer != null) tracer.enabled = false;
                if (muzzleFlash != null) muzzleFlash.enabled = false;
            }
            if (Drawn != GunVisuals.Drawn.None && Gun(Drawn) == null) Drawn = GunVisuals.Drawn.None; // gun taken away

            bool released = _release != null && _release.Released;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            bool aimHeld = false;
            if (!released)
            {
                if (keyboard != null && keyboard[longGunKey].wasPressedThisFrame) Draw(Drawn == GunVisuals.Drawn.LongGun ? GunVisuals.Drawn.None : GunVisuals.Drawn.LongGun);
                if (keyboard != null && keyboard[sidearmKey].wasPressedThisFrame) Draw(Drawn == GunVisuals.Drawn.Sidearm ? GunVisuals.Drawn.None : GunVisuals.Drawn.Sidearm);
                aimHeld = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);
                if (aimHeld && Drawn == GunVisuals.Drawn.None)
                    Draw(Gun(GunVisuals.Drawn.LongGun) != null ? GunVisuals.Drawn.LongGun : GunVisuals.Drawn.Sidearm);
                bool fire = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (pad != null && pad.rightTrigger.wasPressedThisFrame);
                if (fire && Ready && _cooldown <= 0f) Fire();
                bool reload = (keyboard != null && keyboard[reloadKey].wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
                if (reload) StartReload();
            }
            if (ForceAim && Drawn == GunVisuals.Drawn.None) Draw(GunVisuals.Drawn.LongGun);
            _aim.Aiming = (aimHeld || ForceAim) && Drawn != GunVisuals.Drawn.None;

            _visuals.InHand = Drawn;
            _visuals.AimPoint = _aim.Amount > 0.5f && Ready ? AimRay(0f).GetPoint(40f) : (Vector3?)null;
            if (_animatorHasWeapon) _animator.SetInteger("Weapon", (int)Drawn);
            if (_animatorHasAiming) _animator.SetBool(AimingParameter, _aim.Aiming);
            if (_aimLayer >= 0) _animator.SetLayerWeight(_aimLayer, Mathf.MoveTowards(_animator.GetLayerWeight(_aimLayer), Drawn != GunVisuals.Drawn.None ? 1f : 0f, dt * 6f));
        }

        // ---- Firing --------------------------------------------------------------------------------

        private Ray AimRay(float spreadDegrees)
        {
            Camera cam = Camera.main;
            Ray ray = cam != null ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)) : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);
            if (spreadDegrees <= 0f) return ray;
            Vector2 jitter = Random.insideUnitCircle * spreadDegrees; // presentation randomness, not simulation state
            Quaternion offset = Quaternion.AngleAxis(jitter.x, cam != null ? cam.transform.up : Vector3.up) * Quaternion.AngleAxis(jitter.y, cam != null ? cam.transform.right : Vector3.right);
            return new Ray(ray.origin, offset * ray.direction);
        }

        /// <summary>Current spread in degrees for the drawn gun.</summary>
        public float Spread
        {
            get
            {
                Simulation sim = Sim;
                GearItem gun = DrawnGun;
                WeaponDef weapon = gun != null ? sim.WeaponOf(gun.DefId) : null;
                if (weapon == null) return 0f;
                double steadiness = sim.Head != null ? sim.GetStat(sim.Head, "aim_steadiness") : 1;
                float spread = (float)(weapon.Spread / System.Math.Max(0.2, steadiness));
                if (_aim.Amount < 0.9f) spread *= hipSpread;
                if (_body != null && new Vector2(_body.velocity.x, _body.velocity.z).magnitude > 0.3f) spread *= movingSpread;
                if (_crouch != null && _crouch.IsCrouched) spread *= crouchedSpread;
                return spread;
            }
        }

        /// <summary>Pulls the trigger with the drawn gun, through the crosshair.</summary>
        public ShotResult Fire() => FireAt(AimRay(Spread));

        /// <summary>Pulls the trigger and sends the bullet along <paramref name="ray"/> (tests aim this directly).</summary>
        public ShotResult FireAt(Ray ray)
        {
            var result = new ShotResult();
            Simulation sim = Sim;
            GearItem gun = DrawnGun;
            if (sim == null || gun == null) { result.Kind = ShotKind.Refused; result.Message = "No gun drawn."; return LastShot = result; }
            CancelReload();
            WeaponDef weapon = sim.WeaponOf(gun.DefId);
            CommandResult fired = sim.Execute(new FireWeapon { CharacterId = sim.Head.Id, GearId = gun.Id });
            if (!fired.Ok)
            {
                result.Kind = ShotKind.Refused;
                result.Message = fired.Error.Contains("empty") ? "Empty. R to reload." : fired.Error.Contains("jammed") ? "Jammed. R to clear it." : fired.Error;
                Say(result.Message);
                return LastShot = result;
            }
            _cooldown = (float)weapon.FireInterval;
            foreach (DomainEvent e in fired.Events)
                if (e is WeaponJammed)
                {
                    result.Kind = ShotKind.Jammed;
                    result.Message = "Jammed! R to clear it.";
                    Say(result.Message);
                    return LastShot = result;
                }

            result.Kind = ShotKind.Fired;
            ShotFired?.Invoke(transform.position);
            if (_impulse != null) _impulse.GenerateImpulseWithVelocity(new Vector3(Random.Range(-0.3f, 0.3f), 1f, -0.4f) * recoil);
            Vector3 from = _visuals.Muzzle != null ? _visuals.Muzzle.position : ray.origin;
            Vector3 to = ray.GetPoint(maxRange);
            if (Trace(ray, out RaycastHit hit, out HitRegion zone))
            {
                result.HitSomething = true;
                result.Point = to = hit.point;
                result.Zone = zone;
                if (zone != null)
                {
                    Transform chest = _visuals.Muzzle != null ? _visuals.Muzzle : transform;
                    double distance = Vector3.Distance(chest.position, hit.point);
                    PracticeTarget practice = zone.GetComponentInParent<PracticeTarget>();
                    if (practice != null)
                    {
                        result.Severity = sim.ShotSeverity(gun.DefId, zone.Region, distance);
                        practice.Register(zone.Region, result.Severity);
                    }
                    else if (zone.Receiver != null)
                    {
                        Hearthline.Bridge.HitOutcome outcome = zone.Receiver.ReceiveShot(zone.Region, sim.Head.Id, gun.Id, distance);
                        result.Severity = outcome.Severity;
                    }
                    _hitMarkerTimer = 0.25f;
                }
            }
            ShowShot(from, to);
            return LastShot = result;
        }

        // Nearest thing the bullet meets: solid geometry, or a hit zone that isn't the shooter's own.
        private bool Trace(Ray ray, out RaycastHit nearest, out HitRegion zone)
        {
            nearest = default;
            zone = null;
            float skip = 0f;
            Camera cam = Camera.main;
            if (cam != null) skip = Vector3.Distance(cam.transform.position, transform.position + Vector3.up * 1.4f) * 0.8f;
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.distance < skip) continue;
                if (h.collider.transform.IsChildOf(transform)) continue; // our own body and zones
                if (h.collider is CharacterController) continue;           // movement capsules: hit zones decide
                HitRegion region = h.collider.GetComponent<HitRegion>();
                if (h.collider.isTrigger && region == null) continue;  // areas, interaction volumes
                nearest = h;
                zone = region;
                return true;
            }
            return false;
        }

        private void ShowShot(Vector3 from, Vector3 to)
        {
            _flashTimer = 0.06f;
            if (tracer != null)
            {
                tracer.enabled = true;
                tracer.SetPosition(0, from);
                tracer.SetPosition(1, to);
            }
            if (muzzleFlash != null)
            {
                muzzleFlash.transform.position = from;
                muzzleFlash.enabled = true;
            }
        }

        // ---- Reloading -----------------------------------------------------------------------------

        /// <summary>Reloads the drawn gun a round at a time, or clears its jam. False if it can't start now.</summary>
        public bool StartReload()
        {
            if (!Ready || _reloading != null) return false;
            _reloading = StartCoroutine(ReloadOrClear());
            return true;
        }

        private IEnumerator ReloadOrClear()
        {
            Simulation sim = Sim;
            GearItem gun = DrawnGun;
            if (sim == null || gun == null) { _reloading = null; yield break; }
            if (gun.Jammed)
            {
                Say("Clearing the jam...");
                yield return new WaitForSeconds(clearJamSeconds);
                sim.Execute(new ClearJam { CharacterId = sim.Head.Id, GearId = gun.Id });
                Say("Cleared.");
                _reloading = null;
                yield break;
            }
            WeaponDef weapon = sim.WeaponOf(gun.DefId);
            double speed = System.Math.Max(0.2, sim.GetStat(sim.Head, "reload_speed"));
            while (true)
            {
                yield return new WaitForSeconds((float)(weapon.ReloadPerRound / speed));
                CommandResult r = sim.Execute(new ReloadWeapon { CharacterId = sim.Head.Id, GearId = gun.Id, Rounds = 1 });
                if (!r.Ok)
                {
                    if (!r.Error.Contains("full")) Say(char.ToUpperInvariant(r.Error[0]) + r.Error.Substring(1) + ".");
                    break;
                }
                if (gun.Loaded >= weapon.Capacity) break;
            }
            _reloading = null;
        }

        private void CancelReload()
        {
            if (_reloading == null) return;
            StopCoroutine(_reloading);
            _reloading = null;
        }

        private void Say(string message)
        {
            _message = message;
            _messageTimer = 2.5f;
        }

        private void OnDisable()
        {
            CancelReload();
            if (_aim != null) _aim.Aiming = false;
        }

        // ---- HUD -----------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (_release != null && _release.Released) return;
            Simulation sim = Sim;
            GearItem gun = DrawnGun;
            if (sim == null || gun == null) return;
            WeaponDef weapon = sim.WeaponOf(gun.DefId);

            // Crosshair: a dot and four ticks that open up with the spread.
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            Camera cam = Camera.main;
            float pixelsPerDegree = cam != null ? Screen.height / cam.fieldOfView : 20f;
            float gap = 4f + Spread * pixelsPerDegree * 0.5f;
            GUI.color = Ready ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 1f, 2f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - gap - 6f, cy - 1f, 6f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + gap, cy - 1f, 6f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - gap - 6f, 2f, 6f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy + gap, 2f, 6f), Texture2D.whiteTexture);
            if (_hitMarkerTimer > 0f)
            {
                GUI.color = new Color(1f, 0.85f, 0.6f, 0.9f);
                GUI.DrawTexture(new Rect(cx - 9f, cy - 9f, 5f, 5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4f, cy - 9f, 5f, 5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 9f, cy + 4f, 5f, 5f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4f, cy + 4f, 5f, 5f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            int spare = sim.OnTrip ? sim.PackCount(weapon.Ammo) : sim.ItemCount(weapon.Ammo);
            string name = sim.Content.Items[gun.DefId].Name;
            string state = gun.Jammed ? "  JAMMED" : Reloading ? "  reloading" : "";
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = 16 };
            GUI.Label(new Rect(Screen.width - 330f, Screen.height - 60f, 310f, 40f), name + "   " + gun.Loaded + " / " + spare + state, style);
            if (_messageTimer > 0f && _message != null)
            {
                var centre = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 15 };
                GUI.Label(new Rect(cx - 200f, cy + 40f, 400f, 30f), _message, centre);
            }
        }
    }
}
