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
    /// Tabs: Marks (add/remove marks, hits, treatment), Events (spawn by id, storyteller, items),
    /// House (members, partners, births, heirs, chronicle).
    /// </summary>
    public sealed class CheatPanel : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private bool openOnStart = true;

        private static readonly string[] Tabs = { "Marks", "Events", "House" };
        private static readonly string[] RegionNames = Enum.GetNames(typeof(BodyRegion));
        private static readonly string[] DamageNames = Enum.GetNames(typeof(DamageType));
        private static readonly string[] TimeNames = Enum.GetNames(typeof(TimeOfDay));
        private static readonly string[] OneToThree = { "1", "2", "3" };

        private Rect _window = new Rect(12, 12, 480, 680);
        private Vector2 _scroll, _pickScroll;
        private bool _open;
        private int _tab, _defIndex, _eventIndex, _itemIndex, _severityIndex, _damageIndex, _tierIndex, _timeIndex, _thinLevel;
        private int _regionIndex = (int)BodyRegion.Face;
        private bool _atHome;
        private string _cause = "the boar", _place = "Miller's Ford", _age = "50", _areaTags = "road", _newName = "Levi", _status = "";
        private string[] _defIds = new string[0], _eventIds = new string[0], _itemIds = new string[0];

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
            Refresh(host.Content);

            GUILayout.Label(sim.Date + (head == null
                ? "   |   no head of household"
                : "   |   " + head.GivenName + " " + head.Surname + ", " + sim.AgeOf(head) + " (" + sim.LifeStageOf(head) + ")  health " + head.Health + (head.Downed ? " DOWNED" : "")));
            if (_status.Length > 0) GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 day")) Run(sim, new AdvanceDays { Days = 1 });
            if (GUILayout.Button("Skip season")) Run(sim, new PassSeason());
            if (GUILayout.Button("Save")) { host.Save(); _status = "Saved."; }
            if (GUILayout.Button("Load")) _status = host.Load() ? "Loaded." : "Load failed; see console.";
            if (GUILayout.Button("New game")) { host.NewGame(); _status = "New game."; }
            GUILayout.EndHorizontal();

            _tab = GUILayout.Toolbar(_tab, Tabs);
            if (head == null && _tab != 2)
            {
                GUILayout.Label(sim.State.Household.LineEnded ? "The line has ended. Start a new game or load." : "The head is dead. Choose an heir on the House tab.");
            }
            else if (_tab == 0) DrawMarks(sim, head);
            else if (_tab == 1) DrawEvents(sim);
            else DrawHouse(sim, head);

            GUI.DragWindow();
        }

        private void DrawMarks(Simulation sim, Character head)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Age", GUILayout.Width(40));
            _age = GUILayout.TextField(_age, GUILayout.Width(50));
            if (GUILayout.Button("Set age"))
            {
                if (int.TryParse(_age, out int years)) Run(sim, new SetAge { CharacterId = head.Id, AgeYears = years });
                else _status = "Age must be a whole number.";
            }
            if (GUILayout.Button("Kill head")) Run(sim, new KillCharacter { CharacterId = head.Id, Cause = "the cheat panel" });
            if (head.Downed && GUILayout.Button("Revive")) Run(sim, new ReviveCharacter { CharacterId = head.Id });
            GUILayout.EndHorizontal();

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
            _severityIndex = GUILayout.SelectionGrid(_severityIndex, OneToThree, 3);
            GUILayout.EndHorizontal();
            var origin = new MarkOrigin { Cause = _cause, Place = _place };
            var region = (BodyRegion)_regionIndex;
            int severity = _severityIndex + 1;

            _pickScroll = GUILayout.BeginScrollView(_pickScroll, GUILayout.Height(100));
            _defIndex = GUILayout.SelectionGrid(Clamp(_defIndex, _defIds), _defIds, 3);
            GUILayout.EndScrollView();
            if (_defIds.Length > 0 && GUILayout.Button("Add " + _defIds[_defIndex] + " to " + region))
                Run(sim, new AddMark { CharacterId = head.Id, DefId = _defIds[_defIndex], Region = region, Severity = severity, Origin = origin });

            _damageIndex = GUILayout.SelectionGrid(_damageIndex, DamageNames, 5);
            if (GUILayout.Button("Hit " + region + " with " + DamageNames[_damageIndex]))
                Run(sim, new InflictHit { CharacterId = head.Id, Region = region, Damage = (DamageType)_damageIndex, Severity = severity, Origin = origin });

            GUILayout.BeginHorizontal();
            GUILayout.Label("Marks (" + head.Marks.Count + ")   Infirmary tier", GUILayout.Width(200));
            _tierIndex = GUILayout.SelectionGrid(_tierIndex, OneToThree, 3);
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
        }

        private void DrawEvents(Simulation sim)
        {
            ActiveEventView active = sim.GetActiveEvent();
            if (active != null)
            {
                GUILayout.Label("[" + active.EventId + " / " + active.BeatId + "]");
                GUILayout.Label(active.Text);
                foreach (EventChoiceView choice in active.Choices)
                {
                    if (choice.Available)
                    {
                        if (GUILayout.Button(choice.Label)) Run(sim, new ChooseEventOption { ChoiceIndex = choice.Index });
                    }
                    else GUILayout.Label("  (" + choice.Label + ": " + choice.UnavailableReason + ")");
                }
                GUILayout.Space(8);
            }

            GUILayout.Label("Where the party is");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Area tags", GUILayout.Width(70));
            _areaTags = GUILayout.TextField(_areaTags);
            if (GUILayout.Button(_atHome ? "At home" : "Away", GUILayout.Width(70))) _atHome = !_atHome;
            if (GUILayout.Button("Thin " + _thinLevel, GUILayout.Width(60))) _thinLevel = (_thinLevel + 1) % 4;
            GUILayout.EndHorizontal();
            _timeIndex = GUILayout.SelectionGrid(_timeIndex, TimeNames, 4);
            var where = new EventContext { ThinLevel = _thinLevel, TimeOfDay = (TimeOfDay)_timeIndex, AtHome = _atHome, PlaceName = _place, DistanceFromHome = _atHome ? 0 : 0.5 };
            foreach (string tag in _areaTags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)) where.AreaTags.Add(tag);

            if (active == null)
            {
                if (GUILayout.Button("Storyteller tick (may start an event that fits)")) Run(sim, new StorytellerTick { Context = where });
                _pickScroll = GUILayout.BeginScrollView(_pickScroll, GUILayout.Height(130));
                _eventIndex = GUILayout.SelectionGrid(Clamp(_eventIndex, _eventIds), _eventIds, 2);
                GUILayout.EndScrollView();
                if (_eventIds.Length > 0 && GUILayout.Button("Start " + _eventIds[_eventIndex]))
                    Run(sim, new StartEvent { EventId = _eventIds[_eventIndex], Context = where });
            }

            GUILayout.Space(8);
            GUILayout.Label("Items");
            _itemIndex = GUILayout.SelectionGrid(Clamp(_itemIndex, _itemIds), _itemIds, 4);
            if (_itemIds.Length > 0 && GUILayout.Button("Give 1 " + _itemIds[_itemIndex]))
                Run(sim, new GiveItem { ItemId = _itemIds[_itemIndex], Count = 1 });

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (KeyValuePair<string, int> kv in sim.State.Household.Inventory) GUILayout.Label(kv.Value + " × " + kv.Key);
            foreach (KeyValuePair<string, int> kv in sim.State.Household.Reputation) GUILayout.Label("Reputation with " + kv.Key + ": " + kv.Value);
            if (sim.State.Flags.Count > 0) GUILayout.Label("Flags: " + string.Join(", ", sim.State.Flags));
            GUILayout.EndScrollView();
        }

        private void DrawHouse(Simulation sim, Character head)
        {
            if (head == null && !sim.State.Household.LineEnded)
            {
                GUILayout.Label("Choose an heir:");
                foreach (Character heir in sim.EligibleHeirs())
                    if (GUILayout.Button(heir.GivenName + " " + heir.Surname + " (" + heir.Bond + ", " + sim.AgeOf(heir) + ")"))
                        Run(sim, new ChooseHeir { HeirId = heir.Id });
            }

            GUILayout.BeginHorizontal();
            _newName = GUILayout.TextField(_newName, GUILayout.Width(120));
            if (GUILayout.Button("Add partner")) Run(sim, new AddHouseholdMember { GivenName = _newName, Surname = "Pruitt", AgeYears = 28, Bond = HouseholdBond.Partner });
            if (GUILayout.Button("Add ward")) Run(sim, new AddHouseholdMember { GivenName = _newName, Surname = "Pruitt", AgeYears = 19, Bond = HouseholdBond.Ward });
            if (GUILayout.Button("Add sworn")) Run(sim, new AddHouseholdMember { GivenName = _newName, Surname = "Pruitt", AgeYears = 30, Bond = HouseholdBond.Sworn });
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (int memberId in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(memberId);
                if (c == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label((c.Alive ? "" : "† ") + c.GivenName + " " + c.Surname + ", " + sim.AgeOf(c) + " · " + c.Bond
                    + (c.Traits.Count > 0 ? " · " + string.Join(", ", c.Traits) : "") + " · " + c.Marks.Count + " marks");
                if (head != null && c.Alive && c.Id != head.Id)
                {
                    if (head.PartnerId == c.Id)
                    {
                        if (GUILayout.Button("Child", GUILayout.Width(50))) Run(sim, new BirthChild { ParentAId = head.Id, ParentBId = c.Id });
                    }
                    else if (GUILayout.Button("Partner", GUILayout.Width(60))) Run(sim, new SetPartners { CharacterAId = head.Id, CharacterBId = c.Id });
                    if (GUILayout.Button("Retire to", GUILayout.Width(70))) Run(sim, new RetireHead { HeirId = c.Id });
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            GUILayout.Label("Chronicle");
            List<ChronicleEntry> chronicle = sim.State.Chronicle;
            for (int i = chronicle.Count - 1; i >= 0 && i >= chronicle.Count - 40; i--)
                GUILayout.Label(new GameDate(chronicle[i].Day) + ": " + chronicle[i].Text);
            GUILayout.EndScrollView();
        }

        private void Run(Simulation sim, Command command)
        {
            CommandResult result = sim.Execute(command);
            _status = result.Ok ? command.GetType().Name + " ok." : "Rejected: " + result.Error;
        }

        private static int Clamp(int index, string[] options) => Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));

        private void Refresh(ContentDatabase content)
        {
            if (_defIds.Length != content.Marks.Count) _defIds = SortedKeys(content.Marks.Keys);
            if (_eventIds.Length != content.Events.Count) _eventIds = SortedKeys(content.Events.Keys);
            if (_itemIds.Length != content.Items.Count) _itemIds = SortedKeys(content.Items.Keys);
        }

        private static string[] SortedKeys(IEnumerable<string> keys)
        {
            var ids = new List<string>(keys);
            ids.Sort(StringComparer.Ordinal);
            return ids.ToArray();
        }
    }
}
