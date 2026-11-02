#if UNITY_EDITOR
using System.Text.RegularExpressions;

namespace Game.Editor.UIGenerator
{
    /// <summary>
    /// 将类 JavaScript 对象字面量（未加引号的 key、数组元素外包括号等）规范为 Newtonsoft.Json 可解析的 JSON。
    /// </summary>
    public static class UIGeneratorJsonNormalizer
    {
        private static readonly Regex UnquotedKeyRegex = new Regex(
            @"([{,]\s*)([a-zA-Z_][a-zA-Z0-9_]*)\s*:",
            RegexOptions.Compiled);

        /// <summary>
        /// 去掉最外层 ()、修正 layers 数组中的 ( { ... } ) 写法，并为未加引号的属性名加引号。
        /// </summary>
        public static string NormalizeToJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }

            string s = raw.Trim();
            if (s.Length > 0 && s[0] == '\uFEFF')
            {
                s = s.Substring(1).Trim();
            }

            while (s.Length >= 2 && s[0] == '(' && s[s.Length - 1] == ')')
            {
                s = s.Substring(1, s.Length - 2).Trim();
            }

            if (s.Length > 0 && s[0] == '(')
            {
                int j = s.IndexOf('{');
                if (j < 0)
                {
                    j = s.IndexOf('[');
                }

                if (j > 0)
                {
                    s = s.Substring(j).Trim();
                    if (s.Length > 0 && s[s.Length - 1] == ')')
                    {
                        s = s.Substring(0, s.Length - 1).TrimEnd();
                    }
                }
            }

            s = Regex.Replace(s, @"\[\s*\(\s*\{", "[{", RegexOptions.Compiled);
            s = Regex.Replace(s, @"\}\s*\)\s*,\s*\(\s*\{", "}, {", RegexOptions.Compiled);
            s = Regex.Replace(s, @"\)\s*\]", "]", RegexOptions.Compiled);

            s = UnquotedKeyRegex.Replace(s, m => m.Groups[1].Value + "\"" + m.Groups[2].Value + "\":");

            return s;
        }
    }
}
#endif
