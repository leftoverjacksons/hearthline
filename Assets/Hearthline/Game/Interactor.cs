using Hearthline.Bridge;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// On the player: finds the nearest <see cref="Interactable"/> within reach and roughly in front, shows
    /// its prompt, and uses it when the interact key is pressed. Results ("+3 Wild berries (pack)") show
    /// briefly above the HUD.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        [SerializeField] private Key key = Key.E;
        [Tooltip("How far off straight ahead a thing may be and still be picked (0 = anywhere around, 1 = dead ahead).")]
        [SerializeField, Range(-1f, 1f)] private float facing = -0.2f;
        [SerializeField] private float messageSeconds = 3f;
        [SerializeField] private PlayerInputRelease playerInput;

        private string _message = "";
        private float _messageUntil;

        public Interactable Current { get; private set; }
        public string LastMessage => _message;

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInputRelease>();
        }

        private void Update()
        {
            Current = Nearest();
            if (Current == null || (playerInput != null && playerInput.Released)) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[key].wasPressedThisFrame && GUIUtility.keyboardControl == 0) TryInteract();
        }

        /// <summary>Uses whatever is in front of the player. Returns the result line, or null if nothing is there.</summary>
        public string TryInteract()
        {
            Current = Nearest();
            if (Current == null) return null;
            Show(Current.CanInteract ? Current.Interact() : Current.Prompt);
            return _message;
        }

        public void Show(string message)
        {
            _message = message ?? "";
            _messageUntil = Time.time + messageSeconds;
        }

        private Interactable Nearest()
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            Vector3 p = transform.position, forward = transform.forward;
            foreach (Interactable i in Interactable.All)
            {
                Vector3 d = i.Position - p;
                d.y = 0f;
                float distance = d.magnitude;
                if (distance > i.Reach || distance >= bestDistance) continue;
                if (distance > 0.5f && Vector3.Dot(forward, d / distance) < facing) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        private void OnGUI()
        {
            if (playerInput != null && playerInput.Released) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            float y = Screen.height - 110f;
            if (Current != null)
                GUI.Box(new Rect(Screen.width / 2f - 170f, y, 340f, 28f), (Current.CanInteract ? key + ":  " : "") + Current.Prompt, style);
            if (_message.Length > 0 && Time.time < _messageUntil)
                GUI.Box(new Rect(Screen.width / 2f - 170f, y - 34f, 340f, 28f), _message, style);
        }
    }
}
