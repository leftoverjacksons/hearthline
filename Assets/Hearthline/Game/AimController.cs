using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// The over-the-shoulder aim (combat spec, step 2). While <see cref="Aiming"/>:
    /// - the follow camera moves in over the shoulder and narrows;
    /// - the character faces where the camera looks (moving becomes strafing);
    /// - walking slows to aim speed, with no sprint.
    ///
    /// V swaps the shoulder, aiming or not. It adjusts the Starter Assets follow camera's third-person settings
    /// rather than switching cameras, so the mirror camera's blends are untouched. Feel values are for Travis.
    /// </summary>
    [DefaultExecutionOrder(-45)] // before PlayerSpeed
    public sealed class AimController : MonoBehaviour, IMoveSpeedFactor
    {
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private Key swapShoulderKey = Key.V;

        [Header("Feel (tune by feel)")]
        [Tooltip("Camera distance behind the shoulder while aiming, m.")]
        [SerializeField] private float aimDistance = 1.6f;
        [Tooltip("Shoulder offset while aiming (x is pushed to the current side).")]
        [SerializeField] private Vector3 aimShoulderOffset = new Vector3(0.55f, 0.05f, 0f);
        [Tooltip("Field of view while aiming, degrees.")]
        [SerializeField] private float aimFieldOfView = 30f;
        [Tooltip("How quickly the camera moves in and out (per second).")]
        [SerializeField] private float blendSpeed = 9f;
        [Tooltip("Walking speed multiplier while aiming.")]
        [SerializeField, Range(0.2f, 1f)] private float aimSpeed = 0.55f;

        private CinemachineThirdPersonFollow _follow;
        private PlayerInputRelease _release;
        private float _hipDistance, _hipFov, _hipSide;
        private Vector3 _hipShoulder;
        private bool _right = true;
        private float _side = 1f;

        /// <summary>Set by whoever owns the aim input (PlayerWeapon).</summary>
        public bool Aiming { get; set; }
        /// <summary>0 at the hip, 1 fully aimed in.</summary>
        public float Amount { get; private set; }
        public bool RightShoulder => _right;

        float IMoveSpeedFactor.MoveFactor => Aiming ? aimSpeed : 1f;
        bool IMoveSpeedFactor.BlocksSprint => Aiming;

        public void Configure(CinemachineCamera camera) => followCamera = camera;

        private void Awake()
        {
            _release = GetComponent<PlayerInputRelease>();
            if (followCamera == null) return;
            _follow = followCamera.GetComponent<CinemachineThirdPersonFollow>();
            _hipFov = followCamera.Lens.FieldOfView;
            if (_follow != null)
            {
                _hipDistance = _follow.CameraDistance;
                _hipShoulder = _follow.ShoulderOffset;
                _hipSide = _follow.CameraSide;
            }
        }

        public void SwapShoulder() => _right = !_right;

        /// <summary>Puts the camera over the given shoulder (cover leans pick the side they lean to).</summary>
        public void SetShoulder(bool right) => _right = right;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool released = _release != null && _release.Released;
            if (!released && ((keyboard != null && keyboard[swapShoulderKey].wasPressedThisFrame) || (pad != null && pad.rightStickButton.wasPressedThisFrame)))
                SwapShoulder();

            float step = blendSpeed * Time.unscaledDeltaTime;
            Amount = Mathf.MoveTowards(Amount, Aiming ? 1f : 0f, step);
            float s = Mathf.SmoothStep(0f, 1f, Amount);
            float hipSide = _right ? _hipSide : 1f - _hipSide;
            _side = Mathf.MoveTowards(_side, Mathf.Lerp(hipSide, _right ? 1f : 0f, s), step);
            if (followCamera == null) return;
            LensSettings lens = followCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(_hipFov, aimFieldOfView, s);
            followCamera.Lens = lens;
            if (_follow == null) return;
            _follow.CameraDistance = Mathf.Lerp(_hipDistance, aimDistance, s);
            _follow.ShoulderOffset = Vector3.Lerp(_hipShoulder, aimShoulderOffset, s);
            _follow.CameraSide = _side;
        }

        private void LateUpdate()
        {
            // Face where the camera looks while aiming; the controller still moves by input, so this strafes.
            if (Amount < 0.3f) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(forward.normalized, Vector3.up);
            // Fully aimed in, the body locks to the camera (the controller turns it toward input each Update).
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Amount);
        }
    }
}
