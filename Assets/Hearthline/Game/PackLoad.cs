using Hearthline.Bridge;
using Hearthline.Sim;
using StarterAssets;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// What the pack looks and feels like. On a trip the pack is on the player's back and grows as it fills; a
    /// sack and then a lashed bundle appear as it gets heavy. A heavy pack slows walking and running, and an
    /// overloaded one slows them a lot. Reads the simulation's pack weight and carry capacity every frame;
    /// wraps the Starter Assets controller's speeds without changing the controller.
    /// </summary>
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class PackLoad : MonoBehaviour
    {
        [SerializeField] private GameObject pack;
        [SerializeField] private GameObject sack;
        [SerializeField] private GameObject bundle;

        [Header("Feel (tune by feel)")]
        [Tooltip("Pack scale when empty and when full.")]
        [SerializeField] private Vector2 packScale = new Vector2(0.8f, 1.0f);
        [Tooltip("Fill (0-1 of capacity) at which the sack, then the bundle, appear.")]
        [SerializeField] private Vector2 extrasAt = new Vector2(0.4f, 0.8f);
        [Tooltip("Speed multiplier at full capacity (it eases in from half full).")]
        [SerializeField, Range(0.2f, 1f)] private float fullSpeed = 0.85f;
        [Tooltip("Speed multiplier when overloaded.")]
        [SerializeField, Range(0.2f, 1f)] private float overloadedSpeed = 0.55f;

        private ThirdPersonController _controller;
        private float _baseMove, _baseSprint;

        public float Fill { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;

        /// <summary>Used by scene builders.</summary>
        public void Configure(GameObject packVisual, GameObject sackVisual, GameObject bundleVisual)
        {
            pack = packVisual;
            sack = sackVisual;
            bundle = bundleVisual;
        }

        private void Awake()
        {
            _controller = GetComponent<ThirdPersonController>();
            _baseMove = _controller.MoveSpeed;
            _baseSprint = _controller.SprintSpeed;
        }

        private void Update()
        {
            Simulation sim = SimHost.Instance != null ? SimHost.Instance.Sim : null;
            bool onTrip = sim != null && sim.OnTrip;
            Fill = onTrip && sim.CarryCapacity > 0 ? (float)(sim.PackWeight / sim.CarryCapacity) : 0f;
            bool overloaded = onTrip && sim.Overloaded;

            if (pack != null)
            {
                pack.SetActive(onTrip);
                pack.transform.localScale = Vector3.one * Mathf.Lerp(packScale.x, packScale.y, Mathf.Clamp01(Fill));
            }
            if (sack != null) sack.SetActive(onTrip && Fill >= extrasAt.x);
            if (bundle != null) bundle.SetActive(onTrip && Fill >= extrasAt.y);

            SpeedMultiplier = overloaded ? overloadedSpeed : Mathf.Lerp(1f, fullSpeed, Mathf.InverseLerp(0.5f, 1f, Fill));
            _controller.MoveSpeed = _baseMove * SpeedMultiplier;
            _controller.SprintSpeed = _baseSprint * SpeedMultiplier;
        }

        private void OnDisable()
        {
            if (_controller == null) return;
            _controller.MoveSpeed = _baseMove;
            _controller.SprintSpeed = _baseSprint;
        }
    }
}
