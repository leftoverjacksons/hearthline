using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Hearthline.Sim
{
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>
    /// Versioned JSON of the simulation state. To change the shape of <see cref="SimState"/> in a way old
    /// saves cannot be read into: raise <see cref="CurrentVersion"/> and add a migration from the old version.
    /// </summary>
    public static class SaveFormat
    {
        public const int CurrentVersion = 1;

        /// <summary>Migrations[v] rewrites a version-v save document into version v+1.</summary>
        private static readonly Dictionary<int, Action<JObject>> Migrations = new Dictionary<int, Action<JObject>>();

        private static JsonSerializerSettings Settings()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                // Fields missing from a save keep their C# defaults; fields the code no longer has are a format error.
                MissingMemberHandling = MissingMemberHandling.Error,
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        public static string ToJson(SimState state)
        {
            state.SaveVersion = CurrentVersion;
            return JsonConvert.SerializeObject(state, Settings());
        }

        public static SimState FromJson(string json)
        {
            JObject doc;
            try { doc = JObject.Parse(json); }
            catch (JsonException e) { throw new SaveFormatException("The save is not valid JSON: " + e.Message, e); }

            JToken versionToken = doc[nameof(SimState.SaveVersion)];
            if (versionToken == null || versionToken.Type != JTokenType.Integer)
                throw new SaveFormatException("The save has no version number.");
            int version = versionToken.Value<int>();
            if (version > CurrentVersion)
                throw new SaveFormatException("The save is version " + version + ", newer than this build understands (" + CurrentVersion + ").");

            while (version < CurrentVersion)
            {
                if (!Migrations.TryGetValue(version, out Action<JObject> migrate))
                    throw new SaveFormatException("No migration exists from save version " + version + ".");
                migrate(doc);
                version++;
                doc[nameof(SimState.SaveVersion)] = version;
            }

            try
            {
                SimState state = doc.ToObject<SimState>(JsonSerializer.Create(Settings()));
                if (state.Rng == null) throw new SaveFormatException("The save has no RNG state.");
                return state;
            }
            catch (JsonException e) { throw new SaveFormatException("The save does not match the expected format: " + e.Message, e); }
        }
    }
}
