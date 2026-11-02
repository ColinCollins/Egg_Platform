using System;using System.Collections.Generic;using System.Globalization;using System.Text;using System.Text.RegularExpressions;using Newtonsoft.Json.Linq;
public static class GeneratorProbe {
 private const string DigGameConfigKey="diggame_config",GLOBALCONST_JSON_ASSET_PATH="Assets/Game/Configs/lubanconfig_tbglobalconst.json";
private static readonly string[] GlobalConstDigGameConfigKeys =
        {
            "digGameIsOpen",
            "digGameShowTipsFailCount",
            "digGameShowTipsTimeLimit",
            "digGame_interstitialCD",
            "digGame_rewardResetInterstitialCD",
            "digGame_interstitialShowMinLevel",
            "digGame_interstitialShowIntervalCount",
            "digGame_interstitialShowFailedCount",
            "digGame_interstitalAdLevelRange",
            "digGame_interstitalAdRangeLevelCount",
            "digGame_interstititalAdRangeFailedCount",
            "digGame_dailyNOAdPopupAutoShowCount",
            "digGame_gameTipsPopupCoinCost",
            "digGame_gameTipsPopupPassLevelCoinCost"
        };
 public static string[] Keys=>GlobalConstDigGameConfigKeys;
 public static bool Merge(string yaml,JObject global,IReadOnlyList<string> paths,out string result,out string error)=>TryMergeDigGameConfig(yaml,global,paths,"fixture.yaml",out result,out error);

        private static bool TryMergeDigGameConfig(
            string yamlContent,
            JObject globalRow,
            IReadOnlyList<string> digLevelSortPaths,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            Regex digGameLineRegex = new Regex(
                $@"^(?<indent>\s*){Regex.Escape(DigGameConfigKey)}:\s*'(?<json>\{{[^']*\}})'\s*$",
                RegexOptions.Multiline);
            Match digGameMatch = digGameLineRegex.Match(yamlContent);
            JObject digGameConfig;
            if (digGameMatch.Success)
            {
                try
                {
                    digGameConfig = JObject.Parse(digGameMatch.Groups["json"].Value);
                }
                catch (Exception e)
                {
                    error = $"{DigGameConfigKey} JSON 解析失败：{e.Message}\n\n{yamlPathForErrors}";
                    return false;
                }
            }
            else
            {
                digGameConfig = new JObject
                {
                    ["enabled"] = false
                };
            }

            foreach (string key in GlobalConstDigGameConfigKeys)
            {
                if (!globalRow.TryGetValue(key, out JToken sourceToken))
                {
                    error = $"Global 表缺少 DigGame 字段 `{key}`。\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                digGameConfig[key] = sourceToken.DeepClone();
            }

            var levelSort = new JArray();
            for (int i = 0; i < digLevelSortPaths.Count; i++)
            {
                string path = digLevelSortPaths[i];
                levelSort.Add(int.TryParse(path, out int numericPath)
                    ? new JValue(numericPath)
                    : new JValue(path));
            }

            digGameConfig["level_sort"] = levelSort;

            var keyOrder = new List<string> { "enabled" };
            foreach (string key in GlobalConstDigGameConfigKeys)
            {
                if (!keyOrder.Contains(key))
                    keyOrder.Add(key);
            }

            keyOrder.Add("level_sort");
            foreach (JProperty property in digGameConfig.Properties())
            {
                if (!keyOrder.Contains(property.Name))
                    keyOrder.Add(property.Name);
            }

            string newJson = SerializeLevelConfigJson(digGameConfig, keyOrder);
            if (digGameMatch.Success)
            {
                string replacementLine =
                    $"{digGameMatch.Groups["indent"].Value}{DigGameConfigKey}: '{newJson}'";
                newYamlContent = ReplaceFirst(yamlContent, digGameMatch.Value, replacementLine);
                return true;
            }

            if (!TryGetLevelConfigLineMatch(
                    yamlContent,
                    yamlPathForErrors,
                    out Match levelConfigMatch,
                    out error))
            {
                return false;
            }

            string newLine = yamlContent.Contains("\r\n") ? "\r\n" : "\n";
            string indent = levelConfigMatch.Groups["indent"].Value;
            string insertedLines =
                levelConfigMatch.Value + newLine + $"{indent}{DigGameConfigKey}: '{newJson}'";
            newYamlContent = ReplaceFirst(yamlContent, levelConfigMatch.Value, insertedLines);
            return true;
        }

        private static bool TryGetLevelConfigLineMatch(
            string yamlContent,
            string yamlPathForErrors,
            out Match match,
            out string error)
        {
            match = null;
            error = null;

            if (string.IsNullOrEmpty(yamlContent))
            {
                error = "YAML 内容为空。";
                return false;
            }

            Regex lineRegex = new Regex(@"^(?<indent>\s*)level_config:\s*'(?<json>\{[^']*\})'\s*$", RegexOptions.Multiline);
            match = lineRegex.Match(yamlContent);
            if (!match.Success)
            {
                error = $"未在 `{yamlPathForErrors}` 中找到 level_config: '{{...}}' 行。";
                return false;
            }

            return true;
        }

        private static string SerializeLevelConfigJson(JObject levelConfig, IList<string> keyOrder)
        {
            var builder = new StringBuilder();
            builder.Append('{');

            bool first = true;
            foreach (string key in keyOrder)
            {
                if (!levelConfig.TryGetValue(key, out JToken value))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(' ');
                builder.Append('"');
                builder.Append(key);
                builder.Append("\":");
                AppendLevelConfigValue(builder, value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendLevelConfigValue(StringBuilder builder, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                builder.Append("null");
                return;
            }

            switch (value.Type)
            {
                case JTokenType.Boolean:
                    builder.Append(value.Value<bool>() ? "true" : "false");
                    return;
                case JTokenType.Integer:
                    builder.Append(value.Value<long>().ToString(CultureInfo.InvariantCulture));
                    return;
                case JTokenType.Float:
                    double number = value.Value<double>();
                    if (Math.Abs(number - Math.Round(number)) < 0.0001d)
                    {
                        builder.Append(((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    }

                    return;
                case JTokenType.Array:
                    builder.Append('[');
                    bool first = true;
                    foreach (JToken child in value.Children())
                    {
                        if (!first)
                        {
                            builder.Append(',');
                        }

                        first = false;
                        AppendLevelConfigValue(builder, child);
                    }

                    builder.Append(']');
                    return;
                case JTokenType.String:
                    builder.Append('"');
                    builder.Append(EscapeJsonString(value.Value<string>() ?? string.Empty));
                    builder.Append('"');
                    return;
                default:
                    builder.Append(value.ToString());
                    return;
            }
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search, System.StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
}