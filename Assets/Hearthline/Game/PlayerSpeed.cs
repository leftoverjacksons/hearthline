using StarterAssets;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>Something on the player that changes how fast they move (pack weight, crouching, later wounds).</summary>
    public interface IMoveSpeedFactor
    {
        /// <summary>Multiplies walking and running speed. 1 = no change.</summary>
        float MoveFactor { get; }

        /// <summary>True while sprinting is not allowed (sprint then moves at walking speed).</summary>
        bool BlocksSprint { get; }
    }

    /// <summary>
    /// Owns the Starter Assets controller's walk and sprint speeds and applies every
    /// <see cref="IMoveSpeedFactor"/> on the player to them, so several slowdowns stack instead of overwriting
    /// each other. Runs before the controller each frame. Wraps the controller without changing it.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class PlayerSpeed : MonoBehaviour
    {
        private ThirdPersonController _controller;
        private IMoveSpeedFactor[] _factors;
        private float _baseMove, _baseSprint;

        public float BaseMove => _baseMove;
        public float Factor { get; private set; } = 1f;
        public bool SprintBlocked { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<ThirdPersonController>();
            _baseMove = _controller.MoveSpeed;
            _baseSprint = _controller.SprintSpeed;
            _factors = GetComponents<IMoveSpeedFactor>();
        }

        private void Update()
        {
            float factor = 1f;
            bool noSprint = false;
            foreach (IMoveSpeedFactor f in _factors)
            {
                if (f == null || !((Behaviour)f).isActiveAndEnabled) continue;
                factor *= f.MoveFactor;
                noSprint |= f.BlocksSprint;
            }
            Factor = factor;
            SprintBlocked = noSprint;
            _controller.MoveSpeed = _baseMove * factor;
            _controller.SprintSpeed = noSprint ? _controller.MoveSpeed : _baseSprint * factor;
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.MoveSpeed = _baseMove;
            _controller.SprintSpeed = _baseSprint;
        }
    }
}
