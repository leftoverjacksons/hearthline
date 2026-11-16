using System.Collections.Generic;
using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace Hearthline.Sim.Content
{
    /// <summary>
    /// Strict reader over a YAML mapping. Every read records the key; <see cref="Done"/> reports keys nobody
    /// read, so unknown fields are errors rather than silently ignored.
    /// </summary>
    internal sealed class YamlMap
    {
        private readonly Dictionary<string, YamlNode> _children = new Dictionary<string, YamlNode>();
        private readonly HashSet<string> _used = new HashSet<string>();
        private readonly string _file;
        private readonly string _where;
        private readonly List<ContentError> _errors;

        public YamlMap(YamlMappingNode node, string file, string where, List<ContentError> errors)
        {
            _file = file;
            _where = where;
            _errors = errors;
            foreach (var kv in node.Children)
            {
                if (kv.Key is YamlScalarNode key && key.Value != null) _children[key.Value] = kv.Value;
                else Error("mapping keys must be plain strings");
            }
        }

        public IEnumerable<string> Keys => _children.Keys;
        public bool Has(string key) => _children.ContainsKey(key);

        public void Error(string message)
        {
            _errors.Add(new ContentError { File = _file, Message = (_where.Length > 0 ? _where + ": " : "") + message });
        }

        private YamlNode Take(string key, bool required)
        {
            _used.Add(key);
            if (_children.TryGetValue(key, out var node)) return node;
            if (required) Error("missing required field '" + key + "'");
            return null;
        }

        public string Str(string key, bool required = true)
        {
            var node = Take(key, required);
            if (node == null) return null;
            if (node is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value)) return s.Value;
            Error("'" + key + "' must be a non-empty string");
            return null;
        }

        public double? Num(string key, bool required = false)
        {
            var node = Take(key, required);
            if (node == null) return null;
            if (node is YamlScalarNode s && double.TryParse(s.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return v;
            Error("'" + key + "' must be a number");
            return null;
        }

        public int? Int(string key, bool required = false)
        {
            var node = Take(key, required);
            if (node == null) return null;
            if (node is YamlScalarNode s && int.TryParse(s.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            Error("'" + key + "' must be a whole number");
            return null;
        }

        public List<string> StrList(string key, bool required = false)
        {
            var result = new List<string>();
            var node = Take(key, required);
            if (node == null) return result;
            if (!(node is YamlSequenceNode seq)) { Error("'" + key + "' must be a list"); return result; }
            foreach (var item in seq.Children)
            {
                if (item is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value)) result.Add(s.Value);
                else Error("'" + key + "' must contain only non-empty strings");
            }
            return result;
        }

        public List<T> EnumList<T>(string key, bool required = false) where T : struct, System.Enum
        {
            var result = new List<T>();
            foreach (string s in StrList(key, required))
            {
                if (Naming.TryParseSnake(s, out T value))
                {
                    if (result.Contains(value)) Error("'" + key + "' lists '" + s + "' twice");
                    else result.Add(value);
                }
                else Error("'" + key + "' has unknown value '" + s + "' (allowed: " + Naming.AllSnake<T>() + ")");
            }
            return result;
        }

        public T? Enum<T>(string key, bool required = true) where T : struct, System.Enum
        {
            string s = Str(key, required);
            if (s == null) return null;
            if (Naming.TryParseSnake(s, out T value)) return value;
            Error("'" + key + "' has unknown value '" + s + "' (allowed: " + Naming.AllSnake<T>() + ")");
            return null;
        }

        public YamlMap Map(string key, string where, bool required = false)
        {
            var node = Take(key, required);
            if (node == null) return null;
            if (node is YamlMappingNode m) return new YamlMap(m, _file, where, _errors);
            Error("'" + key + "' must be a mapping");
            return null;
        }

        public List<YamlMap> MapList(string key, string where, bool required = false)
        {
            var result = new List<YamlMap>();
            var node = Take(key, required);
            if (node == null) return result;
            if (!(node is YamlSequenceNode seq)) { Error("'" + key + "' must be a list"); return result; }
            int i = 0;
            foreach (var item in seq.Children)
            {
                if (item is YamlMappingNode m) result.Add(new YamlMap(m, _file, where + "[" + i + "]", _errors));
                else Error("'" + key + "' must contain only mappings");
                i++;
            }
            return result;
        }

        public Dictionary<string, double> NumMap(string key)
        {
            var result = new Dictionary<string, double>();
            var map = Map(key, _where + "." + key);
            if (map == null) return result;
            foreach (string k in new List<string>(map.Keys))
            {
                double? v = map.Num(k, true);
                if (v.HasValue) result[k] = v.Value;
            }
            return result;
        }

        /// <summary>Reports any field that was present but never read.</summary>
        public void Done()
        {
            foreach (string key in _children.Keys)
                if (!_used.Contains(key)) Error("unknown field '" + key + "'");
        }
    }
}
