using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// The mirror moment, greybox: a camera in front of the character and a list of every mark on them with
    /// its origin, stage, severity and effects. Selecting a mark turns the camera to that body region and
    /// highlights its marker. Read-only: it never sends a command to the simulation.
    /// Opened from the cheat panel or a hotkey for now; the washhouse and creek reflections will open it later.
    /// </summary>
    public sealed class MirrorView : MonoBehaviour, IPausesPlayer
    {
        [SerializeField] private CharacterMarksView character;
        [SerializeField] private CinemachineCamera mirrorCamera;
        [SerializeField] private Key hotkey = Key.M;

        [Header("Framing (tune by eye)")]
        [Tooltip("Distance from the character when showing the whole body.")]
        [SerializeField] private float bodyDistance = 3.4f;
        [Tooltip("Height above the character's feet the whole-body view looks at.")]
        [SerializeField] private float bodyLookHeight = 1.0f;
        [Tooltip("Distance from a body region when a mark is selected.")]
        [SerializeField] private float closeDistance = 0.8f;
        [Tooltip("How far the camera swings to the side of the region it looks at, in degrees.")]
        [SerializeField] private float closeSideAngle = 15f;
        [Tooltip("How quickly the camera settles on a new framing. Higher is snappier.")]
        [SerializeField] private float settleSpeed = 4f;

        private static readonly BodyRegion[] RegionOrder = (BodyRegion[])Enum.GetValues(typeof(BodyRegion));
        private Rect _window = new Rect(0, 12, 420, 640);
        private const int WindowId = 0x4D495252;
        private Vector2 _scroll;
        private int? _selected;
        private bool _snapNextFrame;

        public bool IsOpen { get; private set; }
        public bool PausesPlayer => IsOpen;
        public int? SelectedMarkId => _selected;
        public CinemachineCamera Camera => mirrorCamera;

        private void Awake()
        {
            if (character == null) character = FindAnyObjectByType<CharacterMarksView>();
            if (mirrorCamera != null) mirrorCamera.gameObject.SetActive(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[hotkey].wasPressedThisFrame && GUIUtility.keyboardControl == 0)
                SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            _selected = null;
            if (character != null) character.Highlight(null);
            _snapNextFrame = open;
            if (mirrorCamera != null) mirrorCamera.gameObject.SetActive(open);
        }

        /// <summary>Select a mark (null for the whole body). The camera turns to its region and the marker is highlighted.</summary>
        public void Select(int? markId)
        {
            _selected = markId;
            if (character != null) character.Highlight(markId);
        }

        private void LateUpdate()
        {
            if (!IsOpen || mirrorCamera == null || character == null) return;
            FrameFor(out Vector3 position, out Vector3 lookAt);
            Transform cam = mirrorCamera.transform;
            Quaternion rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            if (_snapNextFrame)
            {
                cam.SetPositionAndRotation(position, rotation);
                _snapNextFrame = false;
                return;
            }
            float t = 1f - Mathf.Exp(-settleSpeed * Time.unscaledDeltaTime);
            cam.SetPositionAndRotation(Vector3.Lerp(cam.position, position, t), Quaternion.Slerp(cam.rotation, rotation, t));
        }

        /// <summary>Where the camera should be and what it should look at for the current selection.</summary>
        public void FrameFor(out Vector3 position, out Vector3 lookAt)
        {
            Transform root = character.transform;
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            MarkView mark = SelectedView();
            if (mark == null)
            {
                lookAt = root.position + Vector3.up * bodyLookHeight;
                position = lookAt + forward * bodyDistance;
                return;
            }
            lookAt = character.AnchorFor(mark.Region).position;
            float side = SideOf(mark.Region) * closeSideAngle;
            Vector3 direction = Quaternion.AngleAxis(side, Vector3.up) * forward;
            position = lookAt + direction * closeDistance;
        }

        private static float SideOf(BodyRegion region)
        {
            switch (region)
            {
                case BodyRegion.ArmL: case BodyRegion.HandL: case BodyRegion.LegL: return -1f;
                case BodyRegion.ArmR: case BodyRegion.HandR: case BodyRegion.LegR: return 1f;
                default: return 0f;
            }
        }

        private Simulation Sim => SimHost.Instance != null ? SimHost.Instance.Sim : null;

        private Character Subject => Sim == null || character == null ? null : character.GetComponent<SimCharacterLink>().Resolve(Sim);

        /// <summary>The marks the mirror lists, in the simulation's order. Empty when there is no one to look at.</summary>
        public List<MarkView> CurrentMarks() => Subject == null ? new List<MarkView>() : Sim.MarkViewsOf(Subject);

        private MarkView SelectedView()
        {
            if (!_selected.HasValue || Subject == null) return null;
            foreach (MarkView view in Sim.MarkViewsOf(Subject))
                if (view.MarkId == _selected.Value) return view;
            return null;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            _window.x = Screen.width - _window.width - 12;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Mirror (" + hotkey + ")");
        }

        private void DrawWindow(int id)
        {
            Character subject = Subject;
            if (subject == null)
            {
                GUILayout.Label("There is no one to look at.");
                if (GUILayout.Button("Close")) SetOpen(false);
                return;
            }

            Simulation sim = Sim;
            List<MarkView> marks = CurrentMarks();
            GUILayout.Label(subject.GivenName + " " + subject.Surname + ", " + sim.AgeOf(subject) + ". " +
                            (marks.Count == 0 ? "No marks yet." : marks.Count + (marks.Count == 1 ? " mark." : " marks.")));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_selected.HasValue, "Whole body", GUI.skin.button)) { if (_selected.HasValue) Select(null); }
            if (GUILayout.Button("Close", GUILayout.Width(70))) SetOpen(false);
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (BodyRegion region in RegionOrder)
            {
                bool header = false;
                foreach (MarkView mark in marks)
                {
                    if (mark.Region != region) continue;
                    if (!header)
                    {
                        GUILayout.Space(4);
                        GUILayout.Label("<b>" + region + "</b>", RichLabel);
                        header = true;
                    }
                    bool selected = _selected == mark.MarkId;
                    string effects = mark.Effects.Count == 0 ? "no effect" : string.Join(", ", mark.Effects);
                    string text = mark.Name + "  ·  " + mark.Stage + "  ·  severity " + mark.Severity + "\n" + mark.OriginText + "\n" + effects;
                    if (GUILayout.Toggle(selected, text, GUI.skin.button) != selected) Select(selected ? (int?)null : mark.MarkId);
                }
            }
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static GUIStyle _richLabel;
        private static GUIStyle RichLabel => _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
