#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using I2.Loc;
using UnityEditor;
using UnityEngine;

public class ExportCsvCharsWindow : EditorWindow
{
    [MenuItem("Tools/Localization/导出 I2Languages 文本到 TXT")]
    private static void ExportI2LanguagesToTxt()
    {
        const string assetPath = "Assets/Resources/I2Languages.asset";
        var asset = AssetDatabase.LoadAssetAtPath<LanguageSourceAsset>(assetPath);
        if (asset == null || asset.mSource == null)
        {
            EditorUtility.DisplayDialog("导出失败", $"未找到 I2Languages 资源或 mSource 为空：\n{assetPath}", "OK");
            return;
        }

        var source = asset.mSource;
        var seen = new HashSet<char>();
        var sb = new StringBuilder();

        if (source.mTerms != null)
        {
            foreach (var term in source.mTerms)
            {
                if (term == null || term.Languages == null)
                    continue;

                foreach (var text in term.Languages)
                {
                    if (string.IsNullOrEmpty(text))
                        continue;

                    foreach (char c in text)
                    {
                        if (seen.Add(c))
                        {
                            sb.Append(c);
                        }
                    }
                }
            }
        }

        string fullPath = Path.GetFullPath(assetPath);
        string directory = Path.GetDirectoryName(fullPath);
        string outPath = Path.Combine(directory ?? string.Empty, "I2Languages_chars.txt");

        try
        {
            // 显式使用带 BOM 的 UTF-8，确保在记事本等工具中中文不乱码
            var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            File.WriteAllText(outPath, sb.ToString(), utf8WithBom);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("导出完成",
                $"导出字符数：{seen.Count}\n输出文件：\n{outPath}",
                "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("写入失败", $"写入 TXT 失败：\n{e.Message}", "OK");
        }
    }
}
#endif

