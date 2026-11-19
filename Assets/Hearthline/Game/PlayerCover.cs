using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// Sticky cover for the player (combat spec, step 3; DESIGN: Cover).
    /// - **Q** snaps you to the nearest <see cref="CoverSurface"/> in front of you. You press against it,
    ///   crouched and facing low cover, standing with your back to high cover (backToHighCover).
    /// - **A / D** slide along it. You stop at a corner instead of walking off.
    /// - **Aiming** leans you out at an edge of high cover (or either side of a tree), or raises you over
    ///   low cover. Letting go of aim takes you back in. The camera's shoulder follows the side you lean from.
    /// - **Q again**, pulling back, sprinting or jumping leaves cover.
    ///
    /// While pressed in, the cover's own collider is what stops bullets; leaning out exposes the upper body
    /// past the edge. Takes over the Starter Assets controller's movement input while in cover, without
    /// changing the controller. Feel values are for Travis.
    /// </summary>
    [DefaultExecutionOrder(-55)] // before Crouch, PlayerSpeed and the controller
    [RequireComponent(typeof(CharacterController), typeof(StarterAssetsInputs), typeof(Crouch))]
    public sealed class PlayerCover : MonoBehaviour
    {
        [SerializeField] private Key coverKey = Key.Q;

        [Header("Feel (tune by feel)")]
        [Tooltip("How far away cover can be taken from, m.")]
        [SerializeField] private float reach = 1.7f;
        [Tooltip("Gap between the body and the cover, m.")]
        [SerializeField] private float gap = 0.06f;
        [Tooltip("Speed sliding along cover, m/s.")]
        [SerializeField] private float slideSpeed = 1.6f;
        [Tooltip("Seconds to snap into cover.")]
        [SerializeField] private float snapSeconds = 0.18f;
        [Tooltip("How close to a corner counts as 'at the edge' for leaning out, m.")]
        [SerializeField] private float edgeZone = 0.5f;
        [Tooltip("How far the body steps out past the edge to aim, m.")]
        [SerializeField] private float leanDistance = 0.6f;
        [Tooltip("The body stops this far short of a corner when sliding, m.")]
        [SerializeField] private float cornerMargin = 0.15f;
        [Tooltip("Hold back (S) this long to leave cover, seconds.")]
        [SerializeField] private float pullAwaySeconds = 0.3f;
        [Tooltip("Behind high cover, stand with your back to it (matches the Mixamo cover clips). Off: face it.")]
        [SerializeField] private bool backToHighCover = true;
        [Tooltip("Swap which sneak clip plays for which slide direction, if the feet look backwards.")]
        [SerializeField] private bool swapSlideClips;
        [Tooltip("How fast the slide animation catches up with the slide, per second.")]
        [SerializeField] private float slideBlendRate = 8f;

        /// <summary>Animator int: 0 not in high cover (or leaning out), 1 pressed against high cover.</summary>
        public const string CoverParameter = "Cover";
        /// <summary>Animator float: -1 sliding to the body's left, 0 still, 1 to its right.</summary>
        public const string SlideParameter = "CoverSlide";

        public enum Side { None, Left, Right }

        private CharacterController _body;
        private StarterAssetsInputs _inputs;
        private PlayerInput _playerInput;
        private InputAction _move;
        private Crouch _crouch;
        private AimController _aim;
        private PlayerInputRelease _release;
        private int _face;
        private Vector3 _normal;
        private float _snapTimer, _pullAway, _lean, _slideInput;
        private Vector3 _anchor;
        private bool _wasCrouched;
        private Animator _animator;
        private bool _animatorHasCover;
        private float _slideBlend;

        public CoverSurface Surface { get; private set; }
        /// <summary>Movement used instead of the keyboard while non-zero (tests, scripted scenes).</summary>
        public Vector2 ScriptedMove { get; set; }
        public bool InCover => Surface != null;
        public bool LowCover => Surface != null && Surface.Low;
        /// <summary>Which edge you are at (high cover), or None. Trees count as both: the shoulder picks.</summary>
        public Side Edge { get; private set; }
        /// <summary>Leaning out or risen over: the upper body is out of cover.</summary>
        public bool Exposed { get; private set; }
        /// <summary>Points away from the cover, toward you.</summary>
        public Vector3 CoverNormal => _normal;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _inputs = GetComponent<StarterAssetsInputs>();
            _playerInput = GetComponent<PlayerInput>();
            _crouch = GetComponent<Crouch>();
            _aim = GetComponent<AimController>();
            _release = GetComponent<PlayerInputRelease>();
            if (_playerInput != null && _playerInput.actions != null) _move = _playerInput.actions.FindAction("Move");
            _animator = GetComponent<Animator>();
            if (_animator != null)
                foreach (AnimatorControllerParameter p in _animator.parameters) _animatorHasCover |= p.name == CoverParameter;
        }

        private void Animate(int cover, float slide)
        {
            if (!_animatorHasCover) return;
            _slideBlend = Mathf.MoveTowards(_slideBlend, slide, Time.deltaTime * slideBlendRate);
            _animator.SetInteger(CoverParameter, cover);
            _animator.SetFloat(SlideParameter, _slideBlend);
        }

        /// <summary>Takes the nearest cover in reach (in front, or anywhere if <paramref name="anyDirection"/>). False if there is none.</summary>
        public bool TryEnter(bool anyDirection = false)
        {
            Vector3 pos = transform.position;
            Collider[] near = Physics.OverlapSphere(pos + Vector3.up * 0.8f, reach, ~0, QueryTriggerInteraction.Ignore);
            CoverSurface best = null;
            CoverSurface.Spot bestSpot = default;
            float bestScore = float.MaxValue;
            Vector3 facing = transform.forward;
            Camera cam = Camera.main;
            if (cam != null) { facing = cam.transform.forward; facing.y = 0f; facing.Normalize(); }
            foreach (Collider c in near)
            {
                CoverSurface surface = CoverSurface.Of(c);
                if (surface == null || surface.Height < CoverSurface.MinCoverHeight) continue;
                CoverSurface.Spot spot = surface.Nearest(pos);
                Vector3 to = spot.Point - pos;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance > reach) continue;
                float ahead = distance > 0.05f ? Vector3.Dot(to / distance, facing) : 1f;
                if (!anyDirection && ahead < -0.2f) continue;
                float score = distance - ahead * 0.5f;
                if (score < bestScore) { bestScore = score; best = surface; bestSpot = spot; }
            }
            if (best == null) return false;
            Surface = best;
            _face = bestSpot.Face;
            _normal = bestSpot.Normal;
            _anchor = bestSpot.Point;
            _snapTimer = snapSeconds;
            _wasCrouched = _crouch.IsCrouched;
            _lean = 0f;
            return true;
        }

        public void Leave()
        {
            if (Surface == null) return;
            Surface = null;
            Edge = Side.None;
            Exposed = false;
            _lean = 0f;
            Animate(0, 0f);
            _crouch.Set(_wasCrouched);
            if (_move != null) _inputs.MoveInput(_move.ReadValue<Vector2>()); // hand movement back to the controller
        }

        private void Update()
        {
            bool released = _release != null && _release.Released;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool toggle = !released && ((keyboard != null && keyboard[coverKey].wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame));
            if (toggle)
            {
                if (InCover) Leave();
                else TryEnter();
            }
            if (!InCover) return;

            Vector2 move = ScriptedMove != Vector2.zero ? ScriptedMove : released || _move == null ? Vector2.zero : _move.ReadValue<Vector2>();
            _inputs.MoveInput(Vector2.zero); // the controller doesn't move us while in cover
            if (_inputs.jump || (_inputs.sprint && move.sqrMagnitude > 0.1f)) { _inputs.JumpInput(false); Leave(); return; }
            _pullAway = move.y < -0.6f ? _pullAway + Time.deltaTime : 0f;
            if (_pullAway > pullAwaySeconds) { Leave(); return; }

            _slideInput = move.x;
        }

        // Positioning runs after the Starter Assets controller: it carries momentum from the body's velocity and
        // would push us along its last direction, so the final position is set here, from our own anchor on
        // the cover (never from the drifted body position).
        private void LateUpdate()
        {
            if (!InCover) return;
            bool aiming = _aim != null && _aim.Aiming;
            Vector3 pos = transform.position;
            CoverSurface.Spot spot = Spot(_anchor);
            _normal = spot.Normal;
            Vector3 right = Vector3.Cross(Vector3.up, -_normal);

            // Slide along the cover (not while leaning out), stopping short of corners.
            float slide = aiming ? 0f : _slideInput * slideSpeed * Time.deltaTime;
            if (spot.Face >= 0)
            {
                if (slide > 0f) slide = Mathf.Min(slide, Mathf.Max(0f, spot.ToRightEdge - cornerMargin));
                if (slide < 0f) slide = -Mathf.Min(-slide, Mathf.Max(0f, spot.ToLeftEdge - cornerMargin));
            }
            if (slide != 0f)
            {
                spot = Spot(spot.Point + spot.Normal * 0.01f + right * slide);
                _normal = spot.Normal;
                right = Vector3.Cross(Vector3.up, -_normal);
            }
            _anchor = spot.Point;

            // Which edge we're at. A tree has an edge on both sides: the camera's shoulder picks.
            if (spot.Face < 0) Edge = _aim != null && !_aim.RightShoulder ? Side.Left : Side.Right;
            else if (spot.ToRightEdge < edgeZone + cornerMargin) Edge = Side.Right;
            else if (spot.ToLeftEdge < edgeZone + cornerMargin) Edge = Side.Left;
            else Edge = Side.None;

            // Low cover: crouch, and rise over it to aim. High cover: lean out at an edge to aim.
            if (Surface.Low) _crouch.Set(!aiming);
            bool lean = aiming && !Surface.Low && Edge != Side.None;
            _lean = Mathf.MoveTowards(_lean, lean ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, snapSeconds));
            Exposed = aiming && (Surface.Low || Edge != Side.None);
            if (lean && _aim != null) _aim.SetShoulder(Edge == Side.Right);

            Vector3 home = _anchor + _normal * (_body.radius + gap);
            Vector3 target = home + (Edge == Side.Left ? -right : right) * leanDistance * _lean;
            Vector3 delta = target - pos;
            if (_snapTimer > 0f)
            {
                delta *= Mathf.Clamp01(Time.deltaTime / _snapTimer);
                _snapTimer -= Time.deltaTime;
            }
            _body.Move(new Vector3(delta.x, 0f, delta.z));

            bool backToWall = backToHighCover && !Surface.Low;
            if (!aiming) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(backToWall ? _normal : -_normal, Vector3.up), Mathf.Clamp01(Time.deltaTime * 14f));

            // Back to the wall, the body's right is the cover's left as the camera sees it.
            float moved = Time.deltaTime > 0f ? slide / (slideSpeed * Time.deltaTime) : 0f;
            float bodySlide = Mathf.Clamp(backToWall ? -moved : moved, -1f, 1f) * (swapSlideClips ? -1f : 1f);
            Animate(!Surface.Low && _lean < 0.5f && !aiming ? 1 : 0, bodySlide);
        }

        private CoverSurface.Spot Spot(Vector3 near) =>
            Surface.Kind == CoverSurface.Shape.Ring ? Surface.Nearest(near) : Surface.OnFace(_face, near);

        private void OnDisable() => Leave();

        private void OnGUI()
        {
            if (!InCover || (_release != null && _release.Released)) return;
            string hint = Surface.Low ? "Behind low cover: A/D slide, aim to rise over it, Q to leave"
                : Edge != Side.None ? "At the edge: aim to lean out, Q to leave"
                : "Behind cover: A/D slide to an edge to lean out, Q to leave";
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 14 };
            GUI.Label(new Rect(Screen.width / 2f - 300f, Screen.height - 150f, 600f, 24f), hint, style);
        }
    }
}
