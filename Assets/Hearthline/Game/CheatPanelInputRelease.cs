using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// While the cheat panel is open, frees the mouse and stops the player character from moving or turning
    /// the camera, so the panel can be clicked and typed into. Put it on the player next to the Starter Assets
    /// controller. Wraps the third-party input component without changing it.
    /// </summary>
    [RequireComponent(typeof(StarterAssetsInputs))]
    public sealed class CheatPanelInputRelease : MonoBehaviour
    {
        [SerializeField] private CheatPanel panel;

        private StarterAssetsInputs _inputs;
        private PlayerInput _playerInput;
        private bool? _released;

        private void Awake()
        {
            _inputs = GetComponent<StarterAssetsInputs>();
            _playerInput = GetComponent<PlayerInput>();
            if (panel == null) panel = FindAnyObjectByType<CheatPanel>();
        }

        private void Update()
        {
            bool release = panel != null && panel.IsOpen;
            if (_released == release) return;
            _released = release;

            if (release)
            {
                if (_playerInput != null) _playerInput.DeactivateInput();
                _inputs.MoveInput(Vector2.zero);
                _inputs.LookInput(Vector2.zero);
                _inputs.JumpInput(false);
                _inputs.SprintInput(false);
            }
            else if (_playerInput != null)
            {
                _playerInput.ActivateInput();
            }

            _inputs.cursorLocked = !release;
            _inputs.cursorInputForLook = !release;
            Cursor.lockState = release ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = release;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _released = null;
        }
    }
}
