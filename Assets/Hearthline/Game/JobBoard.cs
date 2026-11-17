using System.Collections.Generic;
using System.Linq;
using Hearthline.Bridge;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Game
{
    /// <summary>
    /// The job board by the cabin: who in the household does what while the head is away. Each member gets a
    /// row of jobs; picking one sends <see cref="AssignJob"/>. What they produce is reported when a season turns.
    /// </summary>
    public sealed class JobBoard : Interactable, IPausesPlayer
    {
        private bool _open;
        private string _status = "";
        private Vector2 _scroll;
        private SimHost _host;

        public bool PausesPlayer => _open;
        public override string Prompt => "Job board";
        public override bool CanInteract => !_open;

        private void Start() => _host = SimHost.Instance;

        public override string Interact()
        {
            _open = true;
            _status = "";
            return "";
        }

        public void Close() => _open = false;

        /// <summary>Gives a member a job (null for none). Returns null or why not.</summary>
        public string Assign(int characterId, string jobId)
        {
            CommandResult result = _host.Sim.Execute(new AssignJob { CharacterId = characterId, JobId = jobId });
            _status = result.Ok ? "" : result.Error;
            return result.Ok ? null : result.Error;
        }

        private void OnGUI()
        {
            if (!_open || _host == null || _host.Sim == null) return;
            GUILayout.Window(0x4A4F4253, new Rect((Screen.width - 640f) / 2f, Screen.height * 0.12f, 640f, 0f), Draw, "Who does what");
        }

        // Hunts (docs/specs/hunt.md): take one up, or give it up.
        private void DrawHunts(Simulation sim)
        {
            GUILayout.Space(6);
            GUILayout.Label("Hunts");
            if (sim.Hunt != null)
            {
                HuntDef on = sim.HuntDefOf(sim.Hunt.HuntId);
                GUILayout.Label((on != null ? on.Name : sim.Hunt.HuntId) + ": " + (sim.Hunt.Revealed ? "he's been found." : "sign read " + sim.Hunt.Progress + " of " + (on != null ? on.Sign : 0) + "."));
                if (GUILayout.Button("Give up the hunt", GUILayout.Height(24))) _status = GiveUpHunt() ?? "You let him be, for now.";
                return;
            }
            bool any = false;
            foreach (HuntDef def in _host.Content.Hunts.Values.OrderBy(h => h.Name))
            {
                if (sim.HuntDone(def.Id)) continue;
                any = true;
                GUILayout.Label(def.Offer);
                if (GUILayout.Button("Take up " + def.Name.ToLowerInvariant(), GUILayout.Height(26)))
                {
                    _status = TakeHunt(def.Id) ?? "";
                    if (_status.Length == 0) Close();
                }
            }
            if (!any) GUILayout.Label("No talk of game worth chasing.");
        }

        /// <summary>Takes up a hunt. Returns why not, or null.</summary>
        public string TakeHunt(string huntId)
        {
            CommandResult r = _host.Sim.Execute(new StartHunt { HuntId = huntId });
            return r.Ok ? null : r.Error;
        }

        public string GiveUpHunt()
        {
            CommandResult r = _host.Sim.Execute(new GiveUpHunt());
            return r.Ok ? null : r.Error;
        }

        private void Draw(int id)
        {
            Simulation sim = _host.Sim;
            List<JobDef> jobs = _host.Content.Jobs.Values.OrderBy(j => j.Name).ToList();
            string[] labels = new[] { "None" }.Concat(jobs.Select(j => j.Name)).ToArray();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            bool any = false;
            foreach (int memberId in sim.State.Household.MemberIds)
            {
                Character c = sim.FindCharacter(memberId);
                if (c == null || !c.Alive || c == sim.Head) continue;
                any = true;
                GUILayout.Label(c.GivenName + ", " + sim.AgeOf(c) + (c.Downed ? " (laid up)" : ""));
                int current = c.JobId == null ? 0 : jobs.FindIndex(j => j.Id == c.JobId) + 1;
                int picked = GUILayout.SelectionGrid(current, labels, 4);
                if (picked != current) Assign(c.Id, picked == 0 ? null : jobs[picked - 1].Id);
                GUILayout.Space(4);
            }
            if (!any) GUILayout.Label("Nobody else lives here yet. People join through events on the road and at the gate.");
            GUILayout.EndScrollView();
            DrawHunts(sim);
            if (_status.Length > 0) GUILayout.Label(_status);
            if (GUILayout.Button("Close", GUILayout.Height(24))) Close();
        }
    }
}
