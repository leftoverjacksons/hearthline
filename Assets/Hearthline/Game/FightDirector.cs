using System.Collections.Generic;
using Hearthline.Bridge;
using Hearthline.Sim;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// Runs a fight's beginning and end around the player (combat spec, step 5):
    /// - **Start:** the quick-draw moment (time slows for a breath while the rifle comes up) and a banner.
    /// - **Won:** once nobody is still fighting. If some gave up, a panel asks what to do with them.
    /// - **Got away:** when every enemy still fighting is far off and out of sight for a few seconds.
    /// - **Downed:** the head goes down. The fight ends (on a trip they take the pack), and a panel offers to
    ///   lie still and let the bleed-out rules decide.
    ///
    /// During the fight it shows how many are left and flashes the screen edge when the head is hit. Decisions
    /// go to the simulation as commands. Feel values are for Travis.
    /// </summary>
    public sealed class FightDirector : MonoBehaviour, IPausesPlayer
    {
        [SerializeField] private Transform player;

        [Header("Feel (tune by feel)")]
        [Tooltip("Time scale during the quick-draw moment.")]
        [SerializeField, Range(0.1f, 1f)] private float quickDrawScale = 0.35f;
        [Tooltip("Real seconds the quick-draw moment lasts.")]
        [SerializeField] private float quickDrawSeconds = 2f;
        [Tooltip("Getting away: every enemy still fighting is at least this far off, m...")]
        [SerializeField] private float escapeDistance = 65f;
        [Tooltip("...for this many seconds.")]
        [SerializeField] private float escapeSeconds = 4f;
        [Tooltip("Seconds between the last enemy falling and the fight being called.")]
        [SerializeField] private float settleSeconds = 1.5f;

        private SimHost _host;
        private float _quickDraw, _escape, _settle, _bannerTimer, _hitFlash, _downTimer;
        private string _banner;
        private bool _askSurrender;
        private string _downedResult;

        public bool PausesPlayer => _askSurrender || AskDowned;

        // Down, alone, and the fight over: nobody is coming.
        private bool AskDowned
        {
            get
            {
                Simulation sim = _host != null ? _host.Sim : null;
                Character head = sim?.Head;
                return head != null && head.Alive && head.Downed && !sim.InEncounter && _downedResult == null;
            }
        }
        public bool InQuickDraw => _quickDraw > 0f;
        public string Banner => _bannerTimer > 0f ? _banner : null;

        public void Configure(Transform playerTransform) => player = playerTransform;

        private void OnEnable()
        {
            _host = SimHost.Instance;
            if (_host != null) _host.EventRaised += OnDomainEvent;
        }

        private void OnDisable()
        {
            if (_host != null) _host.EventRaised -= OnDomainEvent;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            if (_host == null) { _host = SimHost.Instance; if (_host != null) _host.EventRaised += OnDomainEvent; }
        }

        private void OnDomainEvent(DomainEvent e)
        {
            Simulation sim = _host.Sim;
            switch (e)
            {
                case EncounterStarted started:
                    _quickDraw = quickDrawSeconds;
                    Time.timeScale = quickDrawScale;
                    Say("Ambush! " + (sim.Content.Encounters.TryGetValue(started.EncounterId, out var def) ? def.Name : ""), 3f);
                    if (player != null) player.GetComponent<PlayerWeapon>()?.Draw(GunVisuals.Drawn.LongGun);
                    _escape = _settle = 0f;
                    break;
                case MarkAdded added when sim.Head != null && added.CharacterId == sim.Head.Id && sim.InEncounter:
                    _hitFlash = 0.6f;
                    break;
                case EncounterEnded ended:
                    _askSurrender = false;
                    Say(ended.Outcome == "won" ? "It's over." + (ended.LootIds.Count > 0 ? " Search the fallen (E)." : "")
                        : ended.Outcome == "fled" ? "You got away."
                        : "They took what you carried and left you in the dirt.", 4f);
                    break;
            }
        }

        private void Say(string text, float seconds)
        {
            _banner = text;
            _bannerTimer = seconds;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _bannerTimer -= dt;
            _hitFlash -= dt;
            if (_quickDraw > 0f)
            {
                _quickDraw -= dt;
                // Ease back to full speed over the last half second.
                Time.timeScale = _quickDraw <= 0f ? 1f : Mathf.Lerp(1f, quickDrawScale, Mathf.Clamp01(_quickDraw / 0.5f));
            }

            Simulation sim = _host != null ? _host.Sim : null;
            if (sim == null) return;
            Character head = sim.Head;
            if (head != null && !head.Downed) _downedResult = null;
            if (!sim.InEncounter || _askSurrender) return;

            if (head != null && head.Downed)
            {
                _downTimer += dt;
                if (_downTimer > 2f) { _downTimer = 0f; sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Downed }); }
                return;
            }
            _downTimer = 0f;

            bool anyFighting = false, anySurrendered = false, allFar = true;
            EnemySpawner spawner = FindAnyObjectByType<EnemySpawner>();
            foreach (Combatant x in sim.Encounter.Combatants)
            {
                if (x.State == CombatantState.Surrendered) anySurrendered = true;
                if (x.State != CombatantState.Fighting) continue;
                anyFighting = true;
                if (spawner != null && spawner.Bodies.TryGetValue(x.CharacterId, out GameObject body) && body != null && player != null
                    && Vector3.Distance(body.transform.position, player.position) < escapeDistance) allFar = false;
            }

            if (!anyFighting)
            {
                _settle += dt;
                if (_settle < settleSeconds) return;
                if (anySurrendered) _askSurrender = true;
                else sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won });
                return;
            }
            _settle = 0f;
            _escape = allFar ? _escape + dt : 0f;
            if (_escape > escapeSeconds) sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Fled });
        }

        /// <summary>Settles a won fight with the ones who gave up (the surrender panel's buttons; tests).</summary>
        public void Resolve(SurrenderChoice choice)
        {
            _askSurrender = false;
            _host.Sim.Execute(new EndEncounter { Outcome = EncounterOutcome.Won, Surrender = choice });
        }

        /// <summary>The downed panel's button: nobody is coming.</summary>
        public void LieStill()
        {
            Simulation sim = _host.Sim;
            Character head = sim.Head;
            if (head == null) return;
            int marks = head.Marks.Count;
            sim.Execute(new LeaveDowned { CharacterId = head.Id });
            _downedResult = !head.Alive ? head.GivenName + " didn't get up." : head.GivenName + " got up. It cost something for good.";
            Say(_downedResult, 6f);
        }

        private void OnGUI()
        {
            Simulation sim = _host != null ? _host.Sim : null;
            if (sim == null) return;
            if (_hitFlash > 0f)
            {
                GUI.color = new Color(0.6f, 0f, 0f, Mathf.Clamp01(_hitFlash) * 0.5f);
                float w = 40f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, w), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, Screen.height - w, Screen.width, w), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, w, Screen.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(Screen.width - w, 0, w, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            var big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
            if (Banner != null) Backed(new Rect(Screen.width / 2f - 320f, Screen.height * 0.18f, 640f, 40f), Banner, big);
            if (sim.InEncounter)
            {
                int left = sim.Encounter.Combatants.FindAll(x => x.State == CombatantState.Fighting).Count;
                var small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 15 };
                Backed(new Rect(Screen.width / 2f - 90f, 10f, 180f, 26f), left == 1 ? "1 still fighting" : left + " still fighting", small);
            }
            if (_askSurrender) DrawPanel("Some of them threw down their guns.", new[] { "Let them go", "Take their guns", "Shoot them" },
                i => Resolve(i == 0 ? SurrenderChoice.LetGo : i == 1 ? SurrenderChoice.TakeGuns : SurrenderChoice.Execute));
            if (AskDowned) DrawPanel("You are down. Nobody is coming.", new[] { "Lie still" }, _ => LieStill());
        }

        // Text on a dark band, readable over a bright sky.
        private static void Backed(Rect rect, string text, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private static void DrawPanel(string text, string[] choices, System.Action<int> choose)
        {
            float w = 420f, h = 70f + 34f * choices.Length;
            var rect = new Rect((Screen.width - w) / 2f, Screen.height * 0.55f, w, h);
            GUI.Box(rect, GUIContent.none);
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, w - 20f, 44f), text, style);
            for (int i = 0; i < choices.Length; i++)
                if (GUI.Button(new Rect(rect.x + 60f, rect.y + 56f + 34f * i, w - 120f, 28f), choices[i])) choose(i);
        }
    }
}
