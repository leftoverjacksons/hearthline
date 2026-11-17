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
            if (_status.Length > 0) GUILayout.Label(_status);
            if (GUILayout.Button("Close", GUILayout.Height(24))) Close();
        }
    }
}
