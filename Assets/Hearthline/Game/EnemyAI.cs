using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.Game
{
    /// <summary>
    /// One enemy in a fight (combat spec, step 4): a small state machine on a spawned body.
    /// - **Engage:** run to cover on the far side from the player, within the gun's range.
    /// - **In cover:** crouch behind low cover or press against high cover; every few seconds pop out (rise,
    ///   or step to the edge) and shoot while the player is in sight; reload and clear jams in cover.
    /// - **Flank:** one enemy at a time, when the player has sat in the same cover too long, moves to cover at
    ///   an angle to them.
    /// - **Down:** lie still. **Flee / surrender:** when the group breaks (sim GroupBroke).
    ///
    /// Shots go through the same simulation commands as the player's (FireWeapon, ShotHit); the sim decides
    /// rounds, jams and wounds. Accuracy is presentation: it starts poor and improves while the enemy keeps the
    /// player in sight. Feel values are for Travis.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(SimCharacterLink), typeof(GunVisuals))]
    public sealed class EnemyAI : MonoBehaviour, ISpawnedCombatant
    {
        public enum State { Engage, InCover, Flank, Fleeing, Surrendered, Down, Gone }

        [SerializeField] private LineRenderer tracer;

        [Header("Feel (tune by feel)")]
        [Tooltip("Run speed, m/s.")]
        [SerializeField] private float runSpeed = 3.6f;
        [Tooltip("How far to look for cover, m.")]
        [SerializeField] private float coverSearchRadius = 28f;
        [Tooltip("Seconds behind cover between popping out to shoot (min, max).")]
        [SerializeField] private Vector2 hideSeconds = new Vector2(1.4f, 3.2f);
        [Tooltip("Seconds out of cover each time (min, max).")]
        [SerializeField] private Vector2 exposedSeconds = new Vector2(1.2f, 2.2f);
        [Tooltip("Spread multiplier on first sight; it shrinks toward the steady value while they keep you in sight.")]
        [SerializeField] private float firstSightSpread = 5f;
        [SerializeField] private float steadySpread = 1.6f;
        [Tooltip("Seconds of keeping you in sight to reach the steady spread.")]
        [SerializeField] private float trackingSeconds = 4f;
        [Tooltip("Delay before the first shot after popping out, seconds.")]
        [SerializeField] private float reactionSeconds = 0.6f;
        [Tooltip("Seconds to clear a jam.")]
        [SerializeField] private float clearJamSeconds = 1.6f;
        [Tooltip("How far a fleeing enemy runs before they count as gone, m.")]
        [SerializeField] private float fleeDistance = 45f;
        [Tooltip("How far an enemy steps sideways to lean out of high cover, m.")]
        [SerializeField] private float leanDistance = 0.6f;

        private NavMeshAgent _agent;
        private Animator _animator;
        private GunVisuals _gun;
        private SimCharacterLink _link;
        private Transform _player;
        private State _state = State.Engage;
        private CoverSurface _cover;
        private Vector3 _coverPoint, _coverNormal;
        private float _timer, _fireCooldown, _tracking, _tracerTimer, _reloadTimer, _fleeTimer;
        private bool _exposed, _reported;
        private readonly HashSet<CoverSurface> _avoid = new HashSet<CoverSurface>();

        public State Current => _state;
        public CoverSurface Cover => _cover;
        public bool Exposed => _exposed;
        public int CharacterId => _link.BoundId ?? 0;
        /// <summary>Shared by the spawner: when one enemy flanks, the others hold.</summary>
        public static EnemyAI Flanker { get; set; }

        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        public void Configure(LineRenderer tracerLine) => tracer = tracerLine;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();
            _gun = GetComponent<GunVisuals>();
            _link = GetComponent<SimCharacterLink>();
            if (tracer != null) tracer.enabled = false;
        }

        /// <summary>Called by the spawner once the body is placed.</summary>
        public void Begin(Transform player)
        {
            _player = player;
            _agent.speed = runSpeed;
            _state = State.Engage;
            _timer = 0f;
            PickCover(flank: false);
        }

        private Character Me() => _link.Resolve(Sim);

        private Combatant MyPart()
        {
            Simulation sim = Sim;
            if (sim?.Encounter == null) return null;
            return sim.Encounter.Combatants.Find(x => x.CharacterId == CharacterId);
        }

        private GearItem MyGun(out WeaponDef weapon)
        {
            weapon = null;
            Character me = Me();
            Combatant part = MyPart();
            if (me == null) return null;
            GearItem gun = part != null ? me.Gear.Find(g => g.Id == part.GearId) : me.Gear.Find(g => g.Equipped && Sim.WeaponOf(g.DefId) != null);
            if (gun != null) weapon = Sim.WeaponOf(gun.DefId);
            return weapon != null ? gun : null;
        }

        private Vector3 PlayerChest => _player.position + Vector3.up * 1.3f;
        private Vector3 Eye => transform.position + Vector3.up * (Crouched ? 1.0f : 1.55f);
        private bool Crouched { get; set; }

        // ---- Update ------------------------------------------------------------------------------------

        private void Update()
        {
            Simulation sim = Sim;
            Character me = Me();
            if (_tracerTimer > 0f && (_tracerTimer -= Time.deltaTime) <= 0f && tracer != null) tracer.enabled = false;
            if (sim == null || me == null || _player == null) return;

            if (!me.Alive || me.Downed) { if (_state != State.Down) GoDown(); Animate(); return; }
            Combatant part = MyPart();
            if (part != null && part.State == CombatantState.Fled) { _state = State.Gone; return; }
            if (part != null && part.Intent == BreakIntent.Flee && _state != State.Fleeing) StartFleeing();
            if (part != null && part.Intent == BreakIntent.Surrender && _state != State.Surrendered) Surrender(sim, part);
            if (part == null && _state != State.Down && _state != State.Surrendered && _state != State.Fleeing && _state != State.Gone)
            {
                // The fight is over and they are still standing: walk off.
                StartFleeing();
            }

            _timer -= Time.deltaTime;
            _fireCooldown -= Time.deltaTime;
            switch (_state)
            {
                case State.Engage:
                case State.Flank:
                    if (!_agent.pathPending && _agent.remainingDistance < 0.4f) ArriveInCover();
                    else if (_timer < -8f) { _avoid.Add(_cover); PickCover(_state == State.Flank); } // stuck: try another
                    break;
                case State.InCover:
                    InCover(sim);
                    break;
                case State.Fleeing:
                    _fleeTimer += Time.deltaTime;
                    if (part != null && !_reported && (Vector3.Distance(transform.position, _player.position) > fleeDistance || _fleeTimer > 12f))
                    {
                        _reported = true;
                        sim.Execute(new CombatantFlees { CharacterId = CharacterId });
                    }
                    break;
            }
            Animate();
        }

        // ---- Cover -------------------------------------------------------------------------------------

        /// <summary>Picks cover facing the player, in range of the gun. With <paramref name="flank"/>, at an angle to the player's.</summary>
        public bool PickCover(bool flank)
        {
            MyGun(out WeaponDef weapon);
            float range = weapon != null ? (float)weapon.Range : 40f;
            Vector3 me = transform.position;
            Vector3 toPlayer = _player.position - me;
            toPlayer.y = 0f;
            CoverSurface best = null;
            CoverSurface.Spot bestSpot = default;
            float bestScore = float.MaxValue;
            PlayerCover playerCover = _player.GetComponent<PlayerCover>();
            Vector3 playerFacing = playerCover != null && playerCover.InCover ? -playerCover.CoverNormal : Vector3.zero;

            foreach (Collider c in Physics.OverlapSphere(me, coverSearchRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                CoverSurface surface = CoverSurface.Of(c);
                if (surface == null || surface == _cover || _avoid.Contains(surface) || surface.Height < CoverSurface.MinCoverHeight) continue;
                if (EnemySpawnerTaken(surface)) continue;
                // The side of the cover away from the player.
                Vector3 away = surface.Centre - _player.position;
                away.y = 0f;
                CoverSurface.Spot spot = surface.Nearest(surface.Centre + away.normalized * 30f);
                float toPlayerDist = Vector3.Distance(spot.Point, _player.position);
                if (toPlayerDist > range * 0.95f || toPlayerDist < 7f) continue;
                if (!NavMesh.SamplePosition(spot.Point + spot.Normal * 0.5f, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) continue;
                float score = Vector3.Distance(me, hit.position) + Mathf.Abs(toPlayerDist - range * 0.55f) * 0.5f;
                if (flank && playerFacing != Vector3.zero)
                {
                    Vector3 fromPlayer = (spot.Point - _player.position).normalized;
                    float angle = Vector3.Angle(playerFacing, fromPlayer);
                    if (angle < 50f) continue;            // still in front of their cover: not a flank
                    score -= angle * 0.1f;
                }
                if (score < bestScore) { bestScore = score; best = surface; bestSpot = spot; }
            }

            _cover = best;
            _state = flank ? State.Flank : State.Engage;
            _timer = 0f;
            Crouched = false;
            _agent.isStopped = false;
            _agent.updateRotation = true;
            if (best == null)
            {
                // Nowhere to hide: close to two thirds of the gun's range and fight in the open.
                Vector3 dir = toPlayer.normalized;
                Vector3 goal = _player.position - dir * Mathf.Min(range * 0.6f, toPlayer.magnitude);
                if (NavMesh.SamplePosition(goal, out NavMeshHit open, 4f, NavMesh.AllAreas)) _agent.SetDestination(open.position);
                _coverPoint = goal;
                _coverNormal = -dir;
                return false;
            }
            _coverPoint = bestSpot.Point;
            _coverNormal = bestSpot.Normal;
            NavMesh.SamplePosition(bestSpot.Point + bestSpot.Normal * 0.5f, out NavMeshHit dest, 1.0f, NavMesh.AllAreas);
            _agent.SetDestination(dest.position);
            return true;
        }

        private bool EnemySpawnerTaken(CoverSurface surface)
        {
            foreach (EnemyAI other in FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
                if (other != this && other._cover == surface && other._state != State.Down) return true;
            return false;
        }

        private void ArriveInCover()
        {
            _state = State.InCover;
            _agent.isStopped = true;
            _agent.updateRotation = false;
            _exposed = false;
            _timer = Random.Range(hideSeconds.x * 0.5f, hideSeconds.y * 0.5f);
            Crouched = _cover != null && _cover.Low;
        }

        private void InCover(Simulation sim)
        {
            GearItem gun = MyGun(out WeaponDef weapon);
            Vector3 lookAt = _player.position - transform.position;
            lookAt.y = 0f;
            if (lookAt.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookAt), Time.deltaTime * 6f);

            // Housekeeping behind cover: clear a jam, reload.
            if (gun != null && !_exposed)
            {
                if (gun.Jammed)
                {
                    _reloadTimer += Time.deltaTime;
                    if (_reloadTimer >= clearJamSeconds) { sim.Execute(new ClearJam { CharacterId = CharacterId, GearId = gun.Id }); _reloadTimer = 0f; }
                    return;
                }
                bool spare = (MyPart()?.Ammo ?? 0) > 0;
                if (spare && (gun.Loaded == 0 || gun.Loaded < weapon.Capacity / 2))
                {
                    _reloadTimer += Time.deltaTime;
                    if (_reloadTimer >= weapon.ReloadPerRound) { sim.Execute(new ReloadWeapon { CharacterId = CharacterId, GearId = gun.Id, Rounds = 1 }); _reloadTimer = 0f; }
                    return;
                }
            }

            if (_timer <= 0f)
            {
                _exposed = !_exposed;
                if (_exposed && (gun == null || gun.Loaded == 0 || gun.Jammed)) _exposed = false; // nothing to shoot with yet
                _timer = _exposed ? Random.Range(exposedSeconds.x, exposedSeconds.y) : Random.Range(hideSeconds.x, hideSeconds.y);
                if (_exposed) _fireCooldown = Mathf.Max(_fireCooldown, reactionSeconds);
                MaybeFlank();
            }

            // Out of cover: rise over low cover, or step out past the edge of high cover.
            Crouched = _cover != null && _cover.Low && !_exposed;
            Vector3 home = _coverPoint + _coverNormal * 0.45f;
            Vector3 side = Vector3.Cross(Vector3.up, -_coverNormal);
            if (_exposed && _cover != null && !_cover.Low) home += side * leanDistance * (CharacterId % 2 == 0 ? 1f : -1f);
            Vector3 step = home - transform.position;
            step.y = 0f;
            if (step.magnitude > 0.02f) _agent.Move(Vector3.ClampMagnitude(step, Time.deltaTime * 2.5f));

            Character target = sim.Head;
            bool sight = _exposed && target != null && target.Alive && !target.Downed && CanSeePlayer();
            _tracking = sight ? Mathf.Min(trackingSeconds, _tracking + Time.deltaTime) : Mathf.Max(0f, _tracking - Time.deltaTime * 0.5f);
            _gun.AimPoint = _exposed ? PlayerChest : (Vector3?)null;
            if (sight && gun != null && _fireCooldown <= 0f && !gun.Jammed && gun.Loaded > 0)
            {
                float spread = (float)weapon.Spread * Mathf.Lerp(firstSightSpread, steadySpread, _tracking / trackingSeconds);
                ShootAt(PlayerChest, spread);
                _fireCooldown = (float)weapon.FireInterval * Random.Range(1.2f, 2.0f);
            }
        }

        private void MaybeFlank()
        {
            PlayerCover pc = _player.GetComponent<PlayerCover>();
            if (Flanker != null && Flanker != this && Flanker._state == State.Flank) return;
            if (pc == null || !pc.InCover || Random.value > 0.25f) return;
            if (PickCover(flank: true)) Flanker = this;
            else PickCover(flank: false);
        }

        private bool CanSeePlayer()
        {
            Vector3 from = Eye, to = PlayerChest;
            if (!Physics.Raycast(from, (to - from).normalized, out RaycastHit hit, Vector3.Distance(from, to) + 0.5f, ~0, QueryTriggerInteraction.Collide))
                return true;
            while (hit.collider != null && hit.collider.isTrigger && hit.collider.GetComponent<HitRegion>() == null)
            {
                // Step past trigger volumes (areas, interaction zones).
                Vector3 next = hit.point + (to - from).normalized * 0.05f;
                if (!Physics.Raycast(next, (to - from).normalized, out hit, Vector3.Distance(next, to) + 0.5f, ~0, QueryTriggerInteraction.Collide)) return true;
            }
            if (hit.collider.transform.IsChildOf(transform)) return true;
            return hit.collider.transform.IsChildOf(_player);
        }

        // ---- Shooting ----------------------------------------------------------------------------------

        /// <summary>Fires at a point with this much spread (degrees). Returns the region hit, or null.</summary>
        public BodyRegion? ShootAt(Vector3 target, float spreadDegrees)
        {
            Simulation sim = Sim;
            GearItem gun = MyGun(out WeaponDef weapon);
            if (sim == null || gun == null) return null;
            CommandResult fired = sim.Execute(new FireWeapon { CharacterId = CharacterId, GearId = gun.Id });
            if (!fired.Ok) return null;
            foreach (DomainEvent e in fired.Events) if (e is WeaponJammed) return null;

            Vector3 from = _gun.Muzzle != null ? _gun.Muzzle.position : Eye;
            Vector3 dir = (target - from).normalized;
            Vector2 jitter = Random.insideUnitCircle * spreadDegrees;
            dir = Quaternion.AngleAxis(jitter.x, Vector3.up) * Quaternion.AngleAxis(jitter.y, Vector3.Cross(Vector3.up, dir)) * dir;
            Vector3 end = from + dir * 200f;
            BodyRegion? region = null;
            RaycastHit[] hits = Physics.RaycastAll(from, dir, 200f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider is CharacterController) continue; // movement capsules: bullets meet the hit zones inside
                HitRegion zone = h.collider.GetComponent<HitRegion>();
                if (h.collider.isTrigger && zone == null) continue;
                end = h.point;
                if (zone != null && zone.Receiver != null)
                {
                    Hearthline.Bridge.HitOutcome outcome = zone.Receiver.ReceiveShot(zone.Region, CharacterId, gun.Id, h.distance);
                    if (outcome.Ok) region = zone.Region;
                }
                break;
            }
            if (tracer != null)
            {
                tracer.enabled = true;
                tracer.SetPosition(0, from);
                tracer.SetPosition(1, end);
                _tracerTimer = 0.06f;
            }
            return region;
        }

        // ---- Breaking, going down ----------------------------------------------------------------------

        private void StartFleeing()
        {
            _state = State.Fleeing;
            _exposed = false;
            Crouched = false;
            _gun.AimPoint = null;
            _agent.isStopped = false;
            _agent.updateRotation = true;
            _agent.speed = runSpeed * 1.15f;
            Vector3 away = transform.position - _player.position;
            away.y = 0f;
            Vector3 goal = transform.position + away.normalized * (fleeDistance + 15f);
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 20f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        private void Surrender(Simulation sim, Combatant part)
        {
            _state = State.Surrendered;
            _exposed = false;
            _agent.isStopped = true;
            Crouched = true;
            _gun.InHand = GunVisuals.Drawn.None;
            _gun.AimPoint = null;
            if (part.State == CombatantState.Fighting) sim.Execute(new CombatantSurrenders { CharacterId = CharacterId });
        }

        private void GoDown()
        {
            _state = State.Down;
            _exposed = false;
            _gun.InHand = GunVisuals.Drawn.None;
            _gun.AimPoint = null;
            if (_agent.enabled) { _agent.isStopped = true; _agent.enabled = false; }
            if (_animator != null) _animator.enabled = false;
            // Stand-in for a death or wounded fall: lie on the ground.
            transform.rotation = Quaternion.Euler(-90f, transform.eulerAngles.y, 0f);
            transform.position += Vector3.up * 0.2f;
            if (Flanker == this) Flanker = null;
        }

        // Animation events in the Starter Assets clips (footsteps, landing). Hooks for sound later.
        private void OnFootstep(AnimationEvent e) { }
        private void OnLand(AnimationEvent e) { }

        private void Animate()
        {
            if (_animator == null || !_animator.enabled) return;
            GearItem gun = MyGun(out WeaponDef weapon);
            bool armed = _state != State.Surrendered && _state != State.Fleeing && gun != null;
            if (armed)
            {
                Simulation sim = Sim;
                _gun.InHand = sim.Content.Items[gun.DefId].Equip.Slot == EquipSlot.LongGun ? GunVisuals.Drawn.LongGun : GunVisuals.Drawn.Sidearm;
            }
            else if (_state != State.Surrendered) _gun.InHand = GunVisuals.Drawn.None;
            float speed = _agent.enabled ? new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude : 0f;
            _animator.SetFloat("Speed", speed);
            _animator.SetFloat("MotionSpeed", 1f);
            _animator.SetBool("Grounded", true);
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.name == Crouch.AnimatorParameter) _animator.SetBool(p.name, Crouched);
                if (p.name == "Weapon") _animator.SetInteger("Weapon", (int)_gun.InHand);
            }
            int aimLayer = _animator.GetLayerIndex("Aim");
            if (aimLayer >= 0) _animator.SetLayerWeight(aimLayer, _gun.InHand != GunVisuals.Drawn.None ? 1f : 0f);
        }
    }
}
