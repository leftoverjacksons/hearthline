using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// Crouching (ADR 0019). C toggles it (B / east button on a pad). Crouched, the player moves slowly,
    /// can't sprint, is shorter (the capsule shrinks, so low cover hides them), and the camera drops. Jump or
    /// sprint stands back up. Standing up waits until there is headroom. Presentation only: crouching is a
    /// moment-to-moment stance, not saved state; stealth will read <see cref="IsCrouched"/> later.
    /// Wraps the Starter Assets controller and inputs without changing them. Feel values are for Travis.
    /// </summary>
    [DefaultExecutionOrder(-50)] // before PlayerSpeed and the controller
    [RequireComponent(typeof(CharacterController), typeof(StarterAssetsInputs), typeof(PlayerSpeed))]
    public sealed class Crouch : MonoBehaviour, IMoveSpeedFactor
    {
        public const string AnimatorParameter = "Crouched";

        [SerializeField] private Key key = Key.C;

        [Header("Feel (tune by feel)")]
        [Tooltip("Crouched walking speed, m/s.")]
        [SerializeField] private float crouchSpeed = 1.4f;
        [Tooltip("Capsule height while crouched, m.")]
        [SerializeField] private float crouchedHeight = 1.15f;
        [Tooltip("How far the camera target drops when crouched, m.")]
        [SerializeField] private float cameraDrop = 0.5f;
        [Tooltip("Seconds to go fully down or up.")]
        [SerializeField] private float transitionTime = 0.18f;
        [Tooltip("Does pressing sprint stand the player up?")]
        [SerializeField] private bool sprintStandsUp = true;

        private CharacterController _body;
        private StarterAssetsInputs _inputs;
        private PlayerSpeed _speed;
        private PlayerInputRelease _release;
        private Animator _animator;
        private bool _animatorHasParameter;
        private Transform _cameraRoot;
        private float _standHeight, _standCenterY, _cameraRootY;
        private float _amount; // 0 standing .. 1 crouched
        private int _blockMask;

        /// <summary>True while the player wants to be (or is held) crouched.</summary>
        public bool IsCrouched { get; private set; }

        /// <summary>0 when standing, 1 when fully down.</summary>
        public float Amount => _amount;

        public float CrouchedHeight => crouchedHeight;

        float IMoveSpeedFactor.MoveFactor => IsCrouched && _speed.BaseMove > 0f ? crouchSpeed / _speed.BaseMove : 1f;
        bool IMoveSpeedFactor.BlocksSprint => IsCrouched;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _inputs = GetComponent<StarterAssetsInputs>();
            _speed = GetComponent<PlayerSpeed>();
            _release = GetComponent<PlayerInputRelease>();
            _animator = GetComponent<Animator>();
            _standHeight = _body.height;
            _standCenterY = _body.center.y;
            var controller = GetComponent<ThirdPersonController>();
            if (controller != null && controller.CinemachineCameraTarget != null)
            {
                _cameraRoot = controller.CinemachineCameraTarget.transform;
                _cameraRootY = _cameraRoot.localPosition.y;
            }
            if (_animator != null)
                foreach (AnimatorControllerParameter p in _animator.parameters)
                    if (p.name == AnimatorParameter && p.type == AnimatorControllerParameterType.Bool) _animatorHasParameter = true;

            int hitbox = LayerMask.NameToLayer("Hitbox");
            _blockMask = ~(1 << gameObject.layer);
            if (hitbox >= 0) _blockMask &= ~(1 << hitbox);
        }

        /// <summary>Crouch or stand. Standing is refused (stays crouched) when something is overhead.</summary>
        public void Set(bool crouched)
        {
            if (crouched) IsCrouched = true;
            else if (HasHeadroom()) IsCrouched = false;
        }

        private void Update()
        {
            bool released = _release != null && _release.Released;
            if (!released)
            {
                Keyboard keyboard = Keyboard.current;
                Gamepad pad = Gamepad.current;
                bool pressed = (keyboard != null && keyboard[key].wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
                if (pressed) Set(!IsCrouched);
            }

            if (IsCrouched)
            {
                // Jump (and optionally sprint) means "stand up" while crouched.
                if (_inputs.jump) { _inputs.JumpInput(false); Set(false); }
                else if (sprintStandsUp && _inputs.sprint && _inputs.move != Vector2.zero) Set(false);
            }

            float step = transitionTime > 0f ? Time.deltaTime / transitionTime : 1f;
            _amount = Mathf.MoveTowards(_amount, IsCrouched ? 1f : 0f, step);
            Apply();
        }

        private void Apply()
        {
            float height = Mathf.Lerp(_standHeight, crouchedHeight, _amount);
            _body.height = height;
            // Keep the feet where they are: the capsule shrinks from the top.
            _body.center = new Vector3(_body.center.x, _standCenterY - (_standHeight - height) * 0.5f, _body.center.z);
            if (_cameraRoot != null)
            {
                Vector3 p = _cameraRoot.localPosition;
                _cameraRoot.localPosition = new Vector3(p.x, _cameraRootY - cameraDrop * _amount, p.z);
            }
            if (_animatorHasParameter) _animator.SetBool(AnimatorParameter, IsCrouched);
        }

        // Is there room to stand up from the current (crouched) capsule?
        private bool HasHeadroom()
        {
            float radius = _body.radius * 0.95f;
            Vector3 top = transform.position + Vector3.up * (_body.center.y + _body.height * 0.5f - radius);
            float rise = _standHeight - _body.height;
            if (rise <= 0.01f) return true;
            return !Physics.SphereCast(top, radius, Vector3.up, out _, rise, _blockMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDisable()
        {
            IsCrouched = false;
            _amount = 0f;
            if (_body != null) Apply();
        }
    }
}
