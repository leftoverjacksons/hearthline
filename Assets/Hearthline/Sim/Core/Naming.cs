using System;
using System.Collections.Generic;
using System.Text;

namespace Hearthline.Sim
{
    /// <summary>Maps C# enum names to the snake_case spelling used in content files (ArmL &lt;-&gt; arm_l).</summary>
    public static class Naming
    {
        public static string ToSnake(string pascal)
        {
            var sb = new StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string ToSnake<T>(T value) where T : struct, Enum => ToSnake(value.ToString());

        public static bool TryParseSnake<T>(string snake, out T value) where T : struct, Enum
        {
            foreach (T candidate in (T[])Enum.GetValues(typeof(T)))
            {
                if (ToSnake(candidate.ToString()) == snake) { value = candidate; return true; }
            }
            value = default;
            return false;
        }

        public static string AllSnake<T>() where T : struct, Enum
        {
            var names = new List<string>();
            foreach (T v in (T[])Enum.GetValues(typeof(T))) names.Add(ToSnake(v.ToString()));
            return string.Join(", ", names);
        }

        public static bool IsSnakeId(string s)
        {
            if (string.IsNullOrEmpty(s) || !(s[0] >= 'a' && s[0] <= 'z')) return false;
            foreach (char c in s)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) return false;
            }
            return true;
        }
    }
}
