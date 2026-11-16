using System;
using System.IO;
using Hearthline.Sim;
using Hearthline.Sim.Content;
using UnityEngine;

namespace Hearthline.Bridge
{
    /// <summary>
    /// Owns the running simulation inside Unity: loads content, starts or loads a game, saves it, and
    /// forwards domain events. Put one in the scene. Everything in the scene is rebuilt from the sim when
    /// <see cref="SimReplaced"/> fires (new game or load).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class SimHost : MonoBehaviour
    {
        public static SimHost Instance { get; private set; }

        [Header("New game")]
        [SerializeField] private int seed = 1;
        [SerializeField] private string surname = "Ward";
        [SerializeField] private string headGivenName = "Ada";
        [SerializeField] private int headAgeYears = 28;

        [Header("Save")]
        [SerializeField] private string saveFileName = "save.json";

        public Simulation Sim { get; private set; }
        public ContentDatabase Content { get; private set; }

        /// <summary>The simulation object was replaced (new game or load). Rebuild views from scratch.</summary>
        public event Action SimReplaced;
        /// <summary>Domain events from whichever simulation is current, so listeners need not resubscribe.</summary>
        public event Action<DomainEvent> EventRaised;

        public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        /// <summary>
        /// In the Editor, content is read from the repository's Content/ folder next to Assets/.
        /// A player build will need it copied into StreamingAssets/Content (not set up yet).
        /// </summary>
        public static string ContentRoot
        {
            get
            {
                string streaming = Path.Combine(Application.streamingAssetsPath, "Content");
                return Directory.Exists(streaming) ? streaming : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Content"));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("There are two SimHosts in the scene. Destroying the newer one.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            ContentLoadResult result = ContentLoader.LoadDirectory(ContentRoot);
            foreach (ContentError error in result.Errors) Debug.LogError("Content: " + error);
            Content = result.Database;
            Debug.Log("Hearthline content loaded from " + ContentRoot + ": " + Content.Marks.Count + " marks, " + result.Errors.Count + " errors.");
            NewGame();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void NewGame()
        {
            Replace(Simulation.NewGame(Content, new NewGameOptions
            {
                Seed = (ulong)Mathf.Max(0, seed),
                Surname = surname,
                HeadGivenName = headGivenName,
                HeadAgeYears = headAgeYears,
            }));
        }

        public void Save()
        {
            File.WriteAllText(SavePath, Sim.ToJson());
            Debug.Log("Saved to " + SavePath);
        }

        /// <summary>Returns false (and keeps the current game) if there is no save or it cannot be read.</summary>
        public bool Load()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("No save at " + SavePath);
                return false;
            }
            try
            {
                Simulation loaded = Simulation.FromJson(File.ReadAllText(SavePath), Content);
                foreach (string problem in loaded.FindOrphanedMarks()) Debug.LogWarning("Save: " + problem);
                Replace(loaded);
                Debug.Log("Loaded " + SavePath);
                return true;
            }
            catch (SaveFormatException e)
            {
                Debug.LogError("Could not load " + SavePath + ": " + e.Message);
                return false;
            }
        }

        private void Replace(Simulation sim)
        {
            if (Sim != null) Sim.EventRaised -= Forward;
            Sim = sim;
            Sim.EventRaised += Forward;
            SimReplaced?.Invoke();
        }

        private void Forward(DomainEvent e) => EventRaised?.Invoke(e);
    }
}
