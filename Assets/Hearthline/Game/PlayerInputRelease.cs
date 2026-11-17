using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>Something on screen that needs the mouse and stops the player walking while it is up.</summary>
    public interface IPausesPlayer
    {
        bool PausesPlayer { get; }
    }

    /// <summary>
    /// While any <see cref="IPausesPlayer"/> (cheat panel, mirror, outfit and arrival panels) wants it, frees
    /// the mouse and stops the player character from moving or turning the camera. Put it on the player next
    /// to the Starter Assets controller. Wraps the third-party input component without changing it.
    /// </summary>
    [RequireComponent(typeof(StarterAssetsInputs))]
    public sealed class PlayerInputRelease : MonoBehaviour
    {
        private readonly List<IPausesPlayer> _sources = new List<IPausesPlayer>();
        private StarterAssetsInputs _inputs;
        private PlayerInput _playerInput;
        private bool? _released;

        /// <summary>True while the player is held still for a panel.</summary>
        public bool Released => _released == true;

        private void Awake()
        {
            _inputs = GetComponent<StarterAssetsInputs>();
            _playerInput = GetComponent<PlayerInput>();
        }

        private void Start()
        {
            foreach (MonoBehaviour b in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (b is IPausesPlayer source) _sources.Add(source);
        }

        private void Update()
        {
            bool release = false;
            foreach (IPausesPlayer source in _sources)
                if (source != null && source.PausesPlayer) { release = true; break; }
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
