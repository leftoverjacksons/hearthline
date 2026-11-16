using System.Collections.Generic;
using System.Text;

namespace Hearthline.Sim.Content
{
    /// <summary>
    /// Builds a mark's name from its definition's origin_text template.
    /// Tokens: {cause} {place} {season} {year}. A [bracketed group] is dropped when a token inside it is empty,
    /// e.g. "Scar from {cause}[ at {place}], {season} of year {year}".
    /// </summary>
    public static class OriginText
    {
        public static readonly string[] Tokens = { "cause", "place", "season", "year" };

        public static string Format(string template, MarkOrigin origin, int dayGained)
        {
            var date = new GameDate(dayGained);
            var values = new Dictionary<string, string>
            {
                { "cause", origin?.Cause ?? "" },
                { "place", origin?.Place ?? "" },
                { "season", date.SeasonName },
                { "year", date.Year.ToString() },
            };

            var result = new StringBuilder();
            int i = 0;
            while (i < template.Length)
            {
                if (template[i] == '[')
                {
                    int close = template.IndexOf(']', i);
                    if (close < 0) { result.Append(Substitute(template.Substring(i), values, out _)); break; }
                    string group = Substitute(template.Substring(i + 1, close - i - 1), values, out bool anyEmpty);
                    if (!anyEmpty) result.Append(group);
                    i = close + 1;
                }
                else
                {
                    int open = template.IndexOf('[', i);
                    if (open < 0) open = template.Length;
                    result.Append(Substitute(template.Substring(i, open - i), values, out _));
                    i = open;
                }
            }
            return result.ToString();
        }

        /// <summary>True if the template needs a cause to read correctly ({cause} outside any bracket group).</summary>
        public static bool RequiresCause(string template)
        {
            int depth = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] == '[') depth++;
                else if (template[i] == ']') depth--;
                else if (depth == 0 && string.CompareOrdinal(template, i, "{cause}", 0, 7) == 0) return true;
            }
            return false;
        }

        /// <summary>Returns a problem description, or null if the template is well formed.</summary>
        public static string Validate(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return "origin_text is empty";
            int depth = 0;
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                if (c == '[') { if (++depth > 1) return "origin_text has nested [ ] groups"; }
                else if (c == ']') { if (--depth < 0) return "origin_text has an unmatched ]"; }
                else if (c == '{')
                {
                    int close = template.IndexOf('}', i);
                    if (close < 0) return "origin_text has an unclosed {";
                    string token = template.Substring(i + 1, close - i - 1);
                    if (System.Array.IndexOf(Tokens, token) < 0)
                        return "origin_text uses unknown token {" + token + "} (known: " + string.Join(", ", Tokens) + ")";
                    i = close;
                }
            }
            return depth != 0 ? "origin_text has an unmatched [" : null;
        }

        private static string Substitute(string text, Dictionary<string, string> values, out bool anyEmpty)
        {
            anyEmpty = false;
            foreach (var kv in values)
            {
                string token = "{" + kv.Key + "}";
                if (text.IndexOf(token, System.StringComparison.Ordinal) < 0) continue;
                if (kv.Value.Length == 0) anyEmpty = true;
                text = text.Replace(token, kv.Value);
            }
            return text;
        }
    }
}
