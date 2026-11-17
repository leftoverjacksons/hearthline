using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;
using UnityEngine.AI;

namespace Hearthline.Game
{
    /// <summary>
    /// The hunted deer (docs/specs/hunt.md):
    /// - **graze:** wander slowly, head down;
    /// - **uneasy:** head up, standing still, staring;
    /// - **flee:** bolt away on the NavMesh;
    /// - **down:** lie still.
    ///
    /// Awareness fills from sight (a cone ahead, shorter while grazing or when you crouch) and sound (your
    /// stance and speed), and drains when you're quiet. A gunshot in earshot sends him off at once. The legs and
    /// neck are moved by code until there is a real animated deer. Sense values come from the hunt's content;
    /// the rest are feel values for Travis.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(SimCharacterLink))]
    public sealed class DeerAI : MonoBehaviour, ISpawnedCombatant
    {
        public enum State { Graze, Uneasy, Flee, Down, Gone }

        [SerializeField] private Transform neck;
        [SerializeField] private Transform[] legs = new Transform[4]; // FL, FR, BL, BR

        [Header("Feel (tune by feel)")]
        [SerializeField] private float walkSpeed = 1.1f;
        [SerializeField] private float runSpeed = 9f;
        [Tooltip("Awareness at which he lifts his head and watches.")]
        [SerializeField] private float uneasyAt = 0.3f;
        [Tooltip("Awareness gained per second while he can see you (at the edge of sight; faster closer).")]
        [SerializeField] private float sightRate = 0.35f;
        [Tooltip("Awareness gained per second while he can hear you.")]
        [SerializeField] private float soundRate = 0.6f;
        [Tooltip("Awareness lost per second when you're out of sight and quiet.")]
        [SerializeField] private float calmRate = 0.12f;
        [Tooltip("How far he runs before he's gone, m.")]
        [SerializeField] private float fleeDistance = 75f;
        [Tooltip("Sight multiplier while you crouch.")]
        [SerializeField] private float crouchedSight = 0.55f;
        [Tooltip("Sight multiplier while his head is down grazing.")]
        [SerializeField] private float grazingSight = 0.6f;

        private NavMeshAgent _agent;
        private SimCharacterLink _link;
        private Transform _player;
        private CharacterController _playerBody;
        private Crouch _playerCrouch;
        private HuntSenses _senses = new HuntSenses();
        private State _state = State.Graze;
        private float _awareness, _timer, _gait, _headDown, _fleeTimer;
        private Vector3 _home;
        private bool _reported;

        public State Current => _state;
        public float Awareness => _awareness;
        public string Mood => _state == State.Flee ? "running" : _awareness > uneasyAt ? (_awareness > 0.7f ? "watching you" : "uneasy") : "grazing";

        public void Configure(Transform neckPivot, Transform[] legPivots) { neck = neckPivot; legs = legPivots; }

        private static Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _link = GetComponent<SimCharacterLink>();
        }

        private void OnEnable()
        {
            PlayerWeapon.ShotFired += OnShot;
            if (SimHost.Instance != null) SimHost.Instance.EventRaised += OnDomainEvent;
        }

        private void OnDisable()
        {
            PlayerWeapon.ShotFired -= OnShot;
            if (SimHost.Instance != null) SimHost.Instance.EventRaised -= OnDomainEvent;
        }

        // Hit but still standing: he runs.
        private void OnDomainEvent(DomainEvent e)
        {
            if (e is MarkAdded hit && hit.CharacterId == _link.BoundId) Bolt();
        }

        public void Begin(Transform player)
        {
            _player = player;
            _playerBody = player.GetComponent<CharacterController>();
            _playerCrouch = player.GetComponent<Crouch>();
            _home = transform.position;
            Simulation sim = Sim;
            HuntDef def = sim?.Hunt != null ? sim.HuntDefOf(sim.Hunt.HuntId) : null;
            if (def != null) _senses = def.Senses;
            _agent.speed = walkSpeed;
            _state = State.Graze;
            _timer = Random.Range(2f, 5f);
        }

        private void OnShot(Vector3 from)
        {
            if (_state == State.Down || _state == State.Gone || _player == null) return;
            if (Vector3.Distance(from, transform.position) <= _senses.HearGunshot) Bolt();
        }

        private void Update()
        {
            Character me = _link.Resolve(Sim);
            if (me == null || _player == null) return;
            if ((!me.Alive || me.Downed) && _state != State.Down) GoDown();
            if (_state == State.Down || _state == State.Gone) { Pose(0f); return; }

            Sense();
            switch (_state)
            {
                case State.Graze:
                    _timer -= Time.deltaTime;
                    if (_awareness >= uneasyAt) { _state = State.Uneasy; _agent.isStopped = true; break; }
                    if (_timer <= 0f || (!_agent.pathPending && _agent.remainingDistance < 0.3f && _timer < 2f))
                    {
                        Vector3 wander = _home + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                        if (NavMesh.SamplePosition(wander, out NavMeshHit hit, 3f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
                        _agent.isStopped = false;
                        _timer = Random.Range(5f, 10f);
                    }
                    break;
                case State.Uneasy:
                    Vector3 look = _player.position - transform.position;
                    look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 3f);
                    if (_awareness >= 1f) Bolt();
                    else if (_awareness < uneasyAt * 0.5f) { _state = State.Graze; _agent.isStopped = false; }
                    break;
                case State.Flee:
                    _fleeTimer += Time.deltaTime;
                    if (!_reported && (Vector3.Distance(transform.position, _player.position) > fleeDistance || _fleeTimer > 12f))
                    {
                        _reported = true;
                        Simulation sim = Sim;
                        if (sim?.Hunt != null && sim.Hunt.Revealed) sim.Execute(new ReportAnimalBolted());
                        _state = State.Gone;
                    }
                    break;
            }
            Pose(_agent.velocity.magnitude);
        }

        private void Sense()
        {
            Vector3 eye = transform.position + Vector3.up * (_headDown > 0.5f ? 0.6f : 1.5f);
            Vector3 chest = _player.position + Vector3.up * 1.2f;
            Vector3 to = chest - eye;
            float distance = to.magnitude;
            bool crouched = _playerCrouch != null && _playerCrouch.IsCrouched;
            float speed = _playerBody != null ? new Vector3(_playerBody.velocity.x, 0f, _playerBody.velocity.z).magnitude : 0f;

            float range = (float)_senses.SightRange * (crouched ? crouchedSight : 1f) * (_state == State.Graze ? grazingSight : 1f);
            float angle = Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z));
            bool sees = distance < range && angle < (float)_senses.SightAngle / 2f && Clear(eye, chest);
            float hearing = speed < 0.2f ? 0f : crouched ? (float)_senses.HearCrouched : speed > 4f ? (float)_senses.HearRunning : (float)_senses.HearWalking;
            bool hears = distance < hearing;

            float gain = 0f;
            if (sees) gain += sightRate * Mathf.Lerp(3f, 1f, distance / Mathf.Max(1f, range));
            if (hears) gain += soundRate * Mathf.Lerp(2f, 1f, distance / Mathf.Max(1f, hearing));
            _awareness = Mathf.Clamp01(gain > 0f ? _awareness + gain * Time.deltaTime : _awareness - calmRate * Time.deltaTime);
        }

        // Nothing solid between his eye and the player (triggers and grass don't block; trunks and rocks do).
        private bool Clear(Vector3 from, Vector3 to)
        {
            foreach (RaycastHit h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.transform.IsChildOf(transform) || h.collider.transform.IsChildOf(_player)) continue;
                if (h.collider is CharacterController) continue;
                return false;
            }
            return true;
        }

        /// <summary>Runs away from the player. Called on a gunshot, on being hit, or when awareness fills.</summary>
        public void Bolt()
        {
            if (_state == State.Flee || _state == State.Down || _state == State.Gone) return;
            _state = State.Flee;
            _awareness = 1f;
            _agent.isStopped = false;
            _agent.speed = runSpeed;
            _agent.acceleration = 30f;
            Vector3 away = transform.position - _player.position;
            away.y = 0f;
            Vector3 goal = transform.position + away.normalized * (fleeDistance + 20f);
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 25f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        private void GoDown()
        {
            _state = State.Down;
            if (_agent.enabled) { _agent.isStopped = true; _agent.enabled = false; }
            // Stand-in for a fall: on his side.
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 82f);
            transform.position += Vector3.up * 0.25f;
            if (GetComponent<ButcherSpot>() == null) gameObject.AddComponent<ButcherSpot>().SetReach(2.6f);
        }

        // Procedural gait and head: legs swing with speed; the head drops to graze and rises when uneasy.
        private void Pose(float speed)
        {
            bool down = _state == State.Graze && speed < 0.4f;
            _headDown = Mathf.MoveTowards(_headDown, down ? 1f : 0f, Time.deltaTime * (down ? 0.7f : 3f));
            if (neck != null) neck.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 62f, _headDown) * (_state == State.Down ? 0f : 1f), 0f, 0f);
            float stride = Mathf.Clamp01(speed / runSpeed);
            _gait += Time.deltaTime * Mathf.Lerp(4f, 13f, stride) * (speed > 0.1f ? 1f : 0f);
            float swing = Mathf.Lerp(22f, 48f, stride) * Mathf.Clamp01(speed / 0.6f);
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                // Diagonal pairs move together when walking; front and back pairs when running (bounding).
                float phase = stride > 0.5f ? (i < 2 ? 0f : Mathf.PI) : (i == 0 || i == 3 ? 0f : Mathf.PI);
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(_gait + phase) * swing, 0f, 0f);
            }
        }

        private void OnGUI()
        {
            if (_state == State.Down || _state == State.Gone || _player == null || Camera.main == null) return;
            if (Vector3.Distance(_player.position, transform.position) > 70f) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (screen.z < 0f) return;
            Character me = _link.Resolve(Sim);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(screen.x - 90f, Screen.height - screen.y - 10f, 180f, 20f), (me != null ? me.GivenName : "") + ": " + Mood, style);
            GUI.color = Color.white;
        }
    }
}
