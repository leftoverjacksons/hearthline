using System;
using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Debug panel (F1). Everything it does goes through simulation commands, like the rest of presentation.
    /// Not yet available, because the systems do not exist: spawn event by id, give item.
    /// </summary>
    public sealed class CheatPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool openOnStart = true;

        private static readonly string[] RegionNames = Enum.GetNames(typeof(BodyRegion));
        private static readonly string[] DamageNames = Enum.GetNames(typeof(DamageType));
        private static readonly string[] SeverityNames = { "1", "2", "3" };

        private Rect _window = new Rect(12, 12, 460, 640);
        private Vector2 _scroll;
        private Vector2 _defScroll;
        private bool _open;
        private string[] _defIds = new string[0];
        private int _defIndex;
        private int _regionIndex = (int)BodyRegion.Face;
        private int _severityIndex;
        private int _damageIndex;
        private int _tierIndex;
        private string _cause = "the boar";
        private string _place = "Miller's Ford";
        private string _age = "50";
        private string _status = "";

        private void Start()
        {
            _open = openOnStart;
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == toggleKey)
            {
                _open = !_open;
                e.Use();
            }
            if (!_open) return;
            _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "Hearthline cheats (" + toggleKey + ")");
        }

        private void DrawWindow(int id)
        {
            SimHost host = SimHost.Instance;
            if (host == null || host.Sim == null)
            {
                GUILayout.Label("No SimHost in the scene.");
                GUI.DragWindow();
                return;
            }
            Simulation sim = host.Sim;
            Character head = sim.Head;

            GUILayout.Label(sim.Date + (head == null
                ? "   |   no head of household"
                : "   |   " + head.GivenName + " " + head.Surname + ", " + sim.AgeOf(head) + " (" + sim.LifeStageOf(head) + ")"));
            if (_status.Length > 0) GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 day")) Run(sim, new AdvanceDays { Days = 1 });
            if (GUILayout.Button("Skip season")) Run(sim, new PassSeason());
            if (GUILayout.Button("Save")) { host.Save(); _status = "Saved."; }
            if (GUILayout.Button("Load")) _status = host.Load() ? "Loaded." : "Load failed; see console.";
            if (GUILayout.Button("New game")) { host.NewGame(); _status = "New game."; }
            GUILayout.EndHorizontal();

            if (head == null)
            {
                GUILayout.Label("The head of household is dead. Succession is not built yet; start a new game or load.");
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Age", GUILayout.Width(40));
            _age = GUILayout.TextField(_age, GUILayout.Width(50));
            if (GUILayout.Button("Set age"))
            {
                if (int.TryParse(_age, out int years)) Run(sim, new SetAge { CharacterId = head.Id, AgeYears = years });
                else _status = "Age must be a whole number.";
            }
            if (GUILayout.Button("Kill head")) Run(sim, new KillCharacter { CharacterId = head.Id, Cause = "the cheat panel" });
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Origin (every mark is named by where it came from)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cause", GUILayout.Width(40));
            _cause = GUILayout.TextField(_cause);
            GUILayout.Label("Place", GUILayout.Width(40));
            _place = GUILayout.TextField(_place);
            GUILayout.EndHorizontal();
            _regionIndex = GUILayout.SelectionGrid(_regionIndex, RegionNames, 5);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Severity", GUILayout.Width(60));
            _severityIndex = GUILayout.SelectionGrid(_severityIndex, SeverityNames, 3);
            GUILayout.EndHorizontal();
            var origin = new MarkOrigin { Cause = _cause, Place = _place };
            var region = (BodyRegion)_regionIndex;
            int severity = _severityIndex + 1;

            GUILayout.Space(6);
            GUILayout.Label("Add mark");
            RefreshDefIds(host.Content);
            _defScroll = GUILayout.BeginScrollView(_defScroll, GUILayout.Height(110));
            _defIndex = GUILayout.SelectionGrid(Mathf.Clamp(_defIndex, 0, Mathf.Max(0, _defIds.Length - 1)), _defIds, 3);
            GUILayout.EndScrollView();
            if (_defIds.Length > 0 && GUILayout.Button("Add " + _defIds[_defIndex] + " to " + region))
                Run(sim, new AddMark { CharacterId = head.Id, DefId = _defIds[_defIndex], Region = region, Severity = severity, Origin = origin });

            GUILayout.Space(6);
            GUILayout.Label("Hit (creates a wound)");
            _damageIndex = GUILayout.SelectionGrid(_damageIndex, DamageNames, 5);
            if (GUILayout.Button("Hit " + region + " with " + DamageNames[_damageIndex]))
                Run(sim, new InflictHit { CharacterId = head.Id, Region = region, Damage = (DamageType)_damageIndex, Severity = severity, Origin = origin });

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Marks (" + head.Marks.Count + ")   Infirmary tier", GUILayout.Width(200));
            _tierIndex = GUILayout.SelectionGrid(_tierIndex, SeverityNames, 3);
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (MarkView mark in sim.MarkViewsOf(head))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(mark.OriginText + "\n    " + mark.Name + " · " + mark.Region + " · " + mark.Stage + " · severity " + mark.Severity);
                if (mark.Category == MarkCategory.Wound)
                {
                    if (GUILayout.Button("Field", GUILayout.Width(50)))
                        Run(sim, new FieldTreatWound { CharacterId = head.Id, MarkId = mark.MarkId });
                    if (GUILayout.Button("Infirm.", GUILayout.Width(55)))
                        Run(sim, new InfirmaryTreatWound { CharacterId = head.Id, MarkId = mark.MarkId, InfirmaryTier = _tierIndex + 1 });
                }
                if (GUILayout.Button("X", GUILayout.Width(24)))
                    Run(sim, new RemoveMark { CharacterId = head.Id, MarkId = mark.MarkId });
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void Run(Simulation sim, Command command)
        {
            CommandResult result = sim.Execute(command);
            _status = result.Ok ? command.GetType().Name + " ok." : "Rejected: " + result.Error;
        }

        private void RefreshDefIds(ContentDatabase content)
        {
            if (_defIds.Length == content.Marks.Count) return;
            var ids = new List<string>(content.Marks.Keys);
            ids.Sort(StringComparer.Ordinal);
            _defIds = ids.ToArray();
        }
    }
}
