using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Brings the storyteller into the world. While the party travels it gives the storyteller a turn every so
    /// often (by distance walked and by time passed, and after a night in camp), telling it where the party is.
    /// The simulation decides whether anything happens and what. When an event runs, this shows its text and
    /// choices, sends the player's choice, and afterwards a short summary of what changed.
    /// At home the storyteller gets a turn less often, for home events. Presentation only: the event itself
    /// lives in the simulation, so it survives save and load.
    /// </summary>
    public sealed class RoadEvents : MonoBehaviour, IPausesPlayer
    {
        [SerializeField] private PlayerLocation location;
        [SerializeField] private TravelClock clock;

        [Header("Pace (tune by feel)")]
        [Tooltip("Metres walked away from home between storyteller turns.")]
        [SerializeField, Min(1f)] private float metresPerTurn = 120f;
        [Tooltip("Game hours away from home between storyteller turns, even standing still.")]
        [SerializeField, Min(0.1f)] private float hoursPerTurn = 3f;
        [Tooltip("Game hours at home between storyteller turns (home events).")]
        [SerializeField, Min(0.1f)] private float hoursPerTurnAtHome = 8f;

        private readonly List<string> _summary = new List<string>();
        private AmbushRequested _ambush;
        private readonly SortedDictionary<string, int> _items = new SortedDictionary<string, int>();
        private SimHost _host;
        private Vector3 _lastPosition;
        private bool _havePosition;
        private float _lastHour = -1f;
        private float _metres, _hours;
        private bool _collecting, _showingSummary, _tickAfterNight;
        private string _eventName = "";
        private Vector2 _scroll;

        /// <summary>True while an event or its summary is on screen.</summary>
        public bool PausesPlayer => _showingSummary || (Sim != null && Sim.State.ActiveEvent != null);
        public bool ShowingSummary => _showingSummary;
        public IReadOnlyList<string> Summary => _summary;
        public int Turns { get; private set; }

        private Simulation Sim => _host != null ? _host.Sim : null;

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host != null) _host.EventRaised += OnDomainEvent;
        }

        private void OnDestroy()
        {
            if (_host != null) _host.EventRaised -= OnDomainEvent;
        }

        /// <summary>Where the party is, as the simulation needs it for events.</summary>
        public EventContext ContextHere() =>
            location != null ? location.ToEventContext(clock != null ? clock.TimeOfDay : TimeOfDay.Day) : new EventContext();

        /// <summary>Gives the storyteller a turn now, here. Returns true if an event started.</summary>
        public bool StorytellerTurn()
        {
            Simulation sim = Sim;
            if (sim == null || sim.State.ActiveEvent != null || _showingSummary) return false;
            _metres = 0f;
            _hours = 0f;
            Turns++;
            sim.Execute(new StorytellerTick { Context = ContextHere() });
            return sim.State.ActiveEvent != null;
        }

        /// <summary>Starts a particular event here (the cheat panel's "start here"). Returns null or why not.</summary>
        public string StartHere(string eventId)
        {
            CommandResult result = Sim.Execute(new StartEvent { EventId = eventId, Context = ContextHere() });
            return result.Ok ? null : result.Error;
        }

        /// <summary>Sends a choice for the beat on screen.</summary>
        public string Choose(int choiceIndex)
        {
            CommandResult result = Sim.Execute(new ChooseEventOption { ChoiceIndex = choiceIndex });
            return result.Ok ? null : result.Error;
        }

        public void CloseSummary()
        {
            _showingSummary = false;
            _summary.Clear();
        }

        private void Update()
        {
            Simulation sim = Sim;
            if (sim == null || location == null) return;
            if (_ambush != null && sim.State.ActiveEvent == null)
            {
                // The event is over: the fight it called for starts here.
                AmbushRequested a = _ambush;
                _ambush = null;
                if (!sim.InEncounter)
                    sim.Execute(new StartEncounter { EncounterId = a.EncounterId, EventId = a.EventId, PlaceName = a.PlaceName, LeaderId = a.LeaderId });
            }

            // Distance walked, ignoring teleports and the panels' pauses.
            Vector3 p = location.transform.position;
            if (_havePosition)
            {
                Vector3 step = p - _lastPosition;
                step.y = 0f;
                if (step.magnitude < 20f && !PausesPlayer) _metres += step.magnitude;
            }
            _lastPosition = p;
            _havePosition = true;

            if (clock != null)
            {
                if (_lastHour >= 0f)
                {
                    float dh = clock.Hour - _lastHour;
                    if (dh < 0f) dh += 24f;
                    if (dh < 12f) _hours += dh; // a night in camp is handled by DateAdvanced
                }
                _lastHour = clock.Hour;
            }

            if (sim.State.ActiveEvent != null || _showingSummary) return;
            if (_tickAfterNight)
            {
                _tickAfterNight = false;
                StorytellerTurn();
                return;
            }
            bool away = sim.OnTrip && !location.AtHome;
            if (away && (_metres >= metresPerTurn || _hours >= hoursPerTurn)) StorytellerTurn();
            else if (location.AtHome && !sim.OnTrip && _hours >= hoursPerTurnAtHome) StorytellerTurn();
        }

        // ---- What happened -----------------------------------------------------------------------------

        private void OnDomainEvent(DomainEvent e)
        {
            Simulation sim = Sim;
            switch (e)
            {
                case EventStarted started:
                    _collecting = true;
                    _summary.Clear();
                    _items.Clear();
                    _eventName = started.EventId;
                    return;
                case EventEnded _:
                    _collecting = false;
                    foreach (KeyValuePair<string, int> kv in _items)
                        _summary.Insert(0, (kv.Value > 0 ? "+" : "") + kv.Value + " " + ItemName(kv.Key) + (sim.OnTrip ? " (pack)" : " (stores)"));
                    if (_summary.Count == 0) _summary.Add("Nothing changed hands.");
                    // An ambush skips the summary: the fight starts where you stand (combat step 6).
                    _showingSummary = _ambush == null;
                    if (_ambush != null) _summary.Clear();
                    return;
                case DateAdvanced _:
                    if (sim.OnTrip && !_collecting) _tickAfterNight = true;
                    return;
            }
            if (!_collecting) return;
            switch (e)
            {
                case InventoryChanged inv:
                    _items.TryGetValue(inv.ItemId, out int n);
                    _items[inv.ItemId] = n + inv.Delta;
                    break;
                case MarkAdded mark:
                {
                    Character who = sim.FindCharacter(mark.CharacterId);
                    MarkView view = who == null ? null : sim.MarkViewsOf(who).Find(v => v.MarkId == mark.MarkId);
                    _summary.Add((who != null ? who.GivenName + ": " : "") + (view != null ? view.Name + ". " + view.OriginText : mark.DefId));
                    break;
                }
                case ReputationChanged rep:
                    _summary.Add("Reputation with the " + rep.FactionId + " " + (rep.Delta > 0 ? "+" : "") + rep.Delta);
                    break;
                case CharacterJoined joined:
                    _summary.Add(NameOf(joined.CharacterId) + " joined the household.");
                    break;
                case CharacterDowned downed:
                    _summary.Add(NameOf(downed.CharacterId) + " is down.");
                    break;
                case CharacterDied died:
                    _summary.Add(NameOf(died.CharacterId) + " died: " + died.Cause + ".");
                    break;
                case GearChanged gear when gear.CharacterId.HasValue:
                {
                    Character who = sim.FindCharacter(gear.CharacterId.Value);
                    GearView piece = who == null ? null : sim.GearOf(who).Find(g => g.GearId == gear.GearId);
                    _summary.Add(NameOf(gear.CharacterId.Value) + (piece != null ? " took the " + piece.Name.ToLowerInvariant() + "." : " has new gear."));
                    break;
                }
                case AmbushRequested ambush:
                    if (sim.Content.Encounters.TryGetValue(ambush.EncounterId, out var encounter) && encounter.Staged) _ambush = ambush;
                    else _summary.Add((encounter != null ? encounter.Name : ambush.EncounterId) + ". (Fights with animals aren't staged yet.)");
                    break;
            }
        }

        private string NameOf(int id)
        {
            Character c = Sim.FindCharacter(id);
            return c != null ? c.GivenName : "someone";
        }

        private string ItemName(string id) => _host.Content.Items.TryGetValue(id, out ItemDef def) ? def.Name : id;

        // ---- Drawing -----------------------------------------------------------------------------------

        private void OnGUI()
        {
            Simulation sim = Sim;
            if (sim == null) return;
            var rect = new Rect((Screen.width - 520f) / 2f, Screen.height * 0.18f, 520f, 0f);
            if (_showingSummary) GUILayout.Window(0x45564E54, rect, DrawSummary, "What came of it");
            else if (sim.State.ActiveEvent != null) GUILayout.Window(0x45564E54, rect, DrawEvent, location != null ? location.PlaceName : "");
        }

        private void DrawEvent(int id)
        {
            ActiveEventView view = Sim.GetActiveEvent();
            if (view == null) return;
            var text = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
            GUILayout.Label(view.Text, text, GUILayout.Width(500));
            GUILayout.Space(10);
            foreach (EventChoiceView choice in view.Choices)
            {
                if (choice.Available)
                {
                    if (GUILayout.Button(choice.Label, GUILayout.Height(28))) Choose(choice.Index);
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button(choice.Label + "  (" + choice.UnavailableReason + ")", GUILayout.Height(28));
                    GUI.enabled = true;
                }
            }
        }

        private void DrawSummary(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(220f, 26f * _summary.Count + 12f)), GUILayout.Width(500));
            var line = new GUIStyle(GUI.skin.label) { wordWrap = true };
            foreach (string s in _summary) GUILayout.Label(s, line);
            GUILayout.EndScrollView();
            if (GUILayout.Button("Continue", GUILayout.Height(28))) CloseSummary();
        }
    }
}
