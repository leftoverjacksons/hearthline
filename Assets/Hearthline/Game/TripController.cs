using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hearthline.Game
{
    /// <summary>
    /// The out-and-back loop in the world. Walking out of the homestead opens the outfit panel (who comes,
    /// what goes in the pack); setting out sends <see cref="SetOut"/>. Walking back in sends
    /// <see cref="ReturnHome"/> and shows what came home and what the household reported while you were away.
    /// Also draws the travel HUD (place, time, day, pack) and handles making camp.
    /// All state that matters is in the simulation; this only remembers what to show on the arrival panel.
    /// </summary>
    public sealed class TripController : MonoBehaviour, IPausesPlayer
    {
        public enum Phase { Home, Outfitting, Away, Arrived }

        [SerializeField] private PlayerLocation location;
        [SerializeField] private TravelClock clock;
        [SerializeField] private Key campKey = Key.C;

        private readonly List<int> _companions = new List<int>();
        private readonly SortedDictionary<string, int> _pack = new SortedDictionary<string, int>();
        private readonly List<string> _reportsWhileAway = new List<string>();
        private readonly List<string> _arrival = new List<string>();
        private Vector3 _lastHomePosition;
        private bool _haveHomePosition;
        private string _status = "";
        private Vector2 _scroll;
        private SimHost _host;

        public Phase Current { get; private set; } = Phase.Home;
        public bool PausesPlayer => Current == Phase.Outfitting || Current == Phase.Arrived;
        public IReadOnlyList<string> ArrivalLines => _arrival;
        public string Status => _status;

        private void Start()
        {
            _host = SimHost.Instance;
            if (_host != null)
            {
                _host.EventRaised += OnDomainEvent;
                _host.SimReplaced += OnSimReplaced;
            }
            if (location == null) location = GetComponent<PlayerLocation>();
            OnSimReplaced();
        }

        private void OnDestroy()
        {
            if (_host == null) return;
            _host.EventRaised -= OnDomainEvent;
            _host.SimReplaced -= OnSimReplaced;
        }

        private Simulation Sim => _host != null ? _host.Sim : null;

        private void OnSimReplaced()
        {
            _reportsWhileAway.Clear();
            Current = Sim != null && Sim.OnTrip ? Phase.Away : Phase.Home;
        }

        private void OnDomainEvent(DomainEvent e)
        {
            if (!(e is HomeReported report) || Current != Phase.Away) return;
            _reportsWhileAway.Add("Season's end at home: morale " + report.Morale + (report.HungryMembers > 0 ? ", " + report.HungryMembers + " went hungry" : ""));
            foreach (HomeReportLine line in report.Lines)
            {
                Character who = Sim.FindCharacter(line.CharacterId);
                string name = who != null ? who.GivenName : "someone";
                _reportsWhileAway.Add("  " + name + " (" + line.JobId + "): " + (line.Problem ?? (line.Items.Count == 0 ? "kept things going" : Describe(line.Items))));
            }
        }

        private void Update()
        {
            Simulation sim = Sim;
            if (sim == null || location == null) return;
            location.Refresh();

            if (location.AtHome)
            {
                _lastHomePosition = location.transform.position;
                _haveHomePosition = true;
            }

            switch (Current)
            {
                case Phase.Home:
                    if (sim.OnTrip) Current = Phase.Away;
                    else if (!location.AtHome) BeginOutfitting();
                    break;
                case Phase.Away:
                    if (!sim.OnTrip) Current = Phase.Home;
                    else if (location.AtHome) ComeHome();
                    break;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[campKey].wasPressedThisFrame && !PausesPlayer && clock != null && GUIUtility.keyboardControl == 0)
                _status = clock.MakeCamp();
        }

        // ---- Leaving -----------------------------------------------------------------------------------

        private void BeginOutfitting()
        {
            _companions.Clear();
            _pack.Clear();
            _status = "";
            Current = Phase.Outfitting;
        }

        /// <summary>Choose who comes along. The simulation checks age, health and the limit of two.</summary>
        public void SetCompanion(int characterId, bool coming)
        {
            if (coming && !_companions.Contains(characterId)) _companions.Add(characterId);
            if (!coming) _companions.Remove(characterId);
        }

        /// <summary>Put a number of an item from the stores in the pack (0 removes it).</summary>
        public void SetPacked(string itemId, int count)
        {
            if (count <= 0) _pack.Remove(itemId);
            else _pack[itemId] = Mathf.Min(count, Sim.ItemCount(itemId));
        }

        /// <summary>Sends <see cref="SetOut"/>. Returns false and keeps the panel open if the simulation refuses.</summary>
        public bool ConfirmSetOut()
        {
            var command = new SetOut { CompanionIds = new List<int>(_companions), Pack = new Dictionary<string, int>(_pack) };
            CommandResult result = Sim.Execute(command);
            if (!result.Ok)
            {
                _status = "Cannot set out: " + result.Error;
                return false;
            }
            _reportsWhileAway.Clear();
            _status = "Set out from home.";
            Current = Phase.Away;
            return true;
        }

        /// <summary>Change of mind at the gate: back inside the homestead.</summary>
        public void StayHome()
        {
            Current = Phase.Home;
            if (!_haveHomePosition) return;
            var controller = location.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            location.transform.position = _lastHomePosition;
            if (controller != null) controller.enabled = true;
            location.Refresh();
        }

        // ---- Coming home -------------------------------------------------------------------------------

        private void ComeHome()
        {
            Simulation sim = Sim;
            var pack = new SortedDictionary<string, int>(sim.State.Trip.Pack);
            int daysAway = sim.State.Day - sim.State.Trip.StartDay;
            CommandResult result = sim.Execute(new ReturnHome());
            if (!result.Ok)
            {
                _status = "Cannot come home yet: " + result.Error;
                return;
            }

            _arrival.Clear();
            _arrival.Add("Home after " + daysAway + (daysAway == 1 ? " day." : " days."));
            _arrival.Add(pack.Count == 0 ? "Nothing to unload." : "Unloaded into the stores: " + Describe(pack));
            _arrival.AddRange(_reportsWhileAway);
            _reportsWhileAway.Clear();
            Current = Phase.Arrived;
        }

        public void CloseArrival() => Current = Phase.Home;

        // ---- Drawing -----------------------------------------------------------------------------------

        private void OnGUI()
        {
            Simulation sim = Sim;
            if (sim == null || location == null) return;
            DrawHud(sim);
            if (Current == Phase.Outfitting) GUILayout.Window(0x54524950, CenteredRect(420, 460), DrawOutfit, "Setting out");
            if (Current == Phase.Arrived) GUILayout.Window(0x484F4D45, CenteredRect(460, 360), DrawArrival, "Home");
        }

        private static string WeatherWord(WeatherKind kind) =>
            kind == WeatherKind.Clear ? "clear" : kind == WeatherKind.Overcast ? "overcast" : kind == WeatherKind.Fog ? "fog" : kind == WeatherKind.Rain ? "rain" : "storm";

        private void DrawHud(Simulation sim)
        {
            string line = location.PlaceName + "   ·   " + (clock != null ? clock.HourText + " " + clock.TimeOfDay.ToString().ToLowerInvariant() + "   ·   " : "") + sim.Date;
            if (clock != null)
            {
                line += "   ·   " + WeatherWord(sim.WeatherAt(clock.Hour));
                // Stiff joints feel it coming (weather_sense).
                WeatherSpell next = sim.Forecast(clock.Hour);
                if (next != null) line += " (" + WeatherWord(next.Kind) + " by " + ((int)next.FromHour).ToString("00") + ":00, the joints say)";
            }
            if (sim.Rumour != null && _host.Content.Places.TryGetValue(sim.Rumour.Kind, out PlaceDef goal))
                line += "   ·   after the rumour at " + goal.Name + (PlaceDiscovery.Bearing(goal.Id, location.transform.position) is string where ? " (" + where + ")" : "");
            if (sim.OnTrip)
                line += "   ·   pack " + sim.PackWeight.ToString("0.#") + " / " + sim.CarryCapacity.ToString("0.#") + (sim.Overloaded ? "  OVERLOADED" : "");
            line += "   ·   " + campKey + ": make camp";
            var rect = new Rect(Screen.width / 2f - 360f, Screen.height - 54f, 720f, 22f);
            GUI.Box(rect, line);
            if (_status.Length > 0 && Current != Phase.Outfitting) GUI.Label(new Rect(rect.x, rect.y + 24f, rect.width, 22f), _status);
        }

        private static Rect CenteredRect(float w, float h) => new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);

        private void DrawOutfit(int id)
        {
            Simulation sim = Sim;
            ContentDatabase content = _host.Content;
            GUILayout.Label("Who comes along (two at most)?");
            int others = 0;
            foreach (int memberId in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(memberId);
                if (c == null || !c.Alive || c == sim.Head) continue;
                others++;
                bool coming = _companions.Contains(c.Id);
                bool now = GUILayout.Toggle(coming, " " + c.GivenName + ", " + sim.AgeOf(c));
                if (now != coming) SetCompanion(c.Id, now);
            }
            if (others == 0) GUILayout.Label("  Nobody else lives here yet.");

            GUILayout.Space(6);
            GUILayout.Label("The pack");
            double weight = 0;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(220));
            bool anything = false;
            foreach (KeyValuePair<string, int> stock in sim.State.Household.Inventory)
            {
                if (stock.Value <= 0) continue;
                anything = true;
                content.Items.TryGetValue(stock.Key, out ItemDef def);
                _pack.TryGetValue(stock.Key, out int packed);
                if (def != null) weight += def.Weight * packed;
                GUILayout.BeginHorizontal();
                GUILayout.Label((def != null ? def.Name : stock.Key) + "  (" + packed + " of " + stock.Value + ")", GUILayout.Width(260));
                if (GUILayout.Button("-", GUILayout.Width(28))) SetPacked(stock.Key, packed - 1);
                if (GUILayout.Button("+", GUILayout.Width(28))) SetPacked(stock.Key, packed + 1);
                if (GUILayout.Button("all", GUILayout.Width(36))) SetPacked(stock.Key, stock.Value);
                GUILayout.EndHorizontal();
            }
            if (!anything) GUILayout.Label("  The stores are empty.");
            GUILayout.EndScrollView();
            GUILayout.Label("Pack weight " + weight.ToString("0.#") + " (each person carries about 30)");
            if (_status.Length > 0) GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set out")) ConfirmSetOut();
            if (GUILayout.Button("Stay home")) StayHome();
            GUILayout.EndHorizontal();
        }

        private void DrawArrival(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(280));
            foreach (string line in _arrival) GUILayout.Label(line);
            GUILayout.EndScrollView();
            if (GUILayout.Button("Done")) CloseArrival();
        }

        private string Describe(IDictionary<string, int> items)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kv in items)
            {
                string name = _host.Content.Items.TryGetValue(kv.Key, out ItemDef def) ? def.Name : kv.Key;
                parts.Add(kv.Value + " " + name);
            }
            return string.Join(", ", parts);
        }
    }
}
