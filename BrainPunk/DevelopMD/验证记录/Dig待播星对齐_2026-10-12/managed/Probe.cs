using System;using System.IO;using System.Text;using System.Text.RegularExpressions;using System.Collections.Generic;using Config;using Newtonsoft.Json;using Bear.UI;using Bear.SaveModule;using UnityEngine;
public class DigPlayCtrl {
 private BaseUIView _victory; private DigLevelData _data; private Runtime _runtime=new();private int _levelId=1,_version; public bool Failed;public BaseUIView Victory=>_victory;
 public void Load(string id,int stars){_data=new(){Id=id};_runtime.InkState.StarCount=stars;}public void Win()=>ShowVictoryPanel();public bool Write(string id,int stars)=>TryRecordVictoryStars(id,stars);
 private void HandleFailure(int v,Exception ex){Failed=true;}
    private void ShowVictoryPanel()
    {
        try
        {
            var levels = GameManager.Instance.DigGameLevel;
            bool first = levels.IsFirstVictory(_levelId);
            int stars = Mathf.Clamp(_runtime.InkState.StarCount, 0, 3);
            TryRecordVictoryStars(_data.Id, stars);
            if (OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Dig))
            {
                _victory = GameVictoryPanel_DeepLink.CreateForDig(_data);
            }
            else
            {
                _victory = first ? DigGame_GameVictoryPanel_Reward.Create(_data) : DigGame_GameVictoryPanel.Create(_data);
            }
            if (!_victory) throw new InvalidOperationException("Dig 胜利面板加载失败。");
            // 与 Draw 的职责顺序一致：控制器创建胜利面板，再记录独立模式进度。
            levels.RecordVictory(_levelId, _data);
            this.Log($"[Dig] 胜利面板已创建。level:{_levelId}, panel:{_victory.GetType().Name}");
        }
        catch (Exception ex) { HandleFailure(_version, ex); }
    }
    private bool TryRecordVictoryStars(string levelDataId, int victoryStarCount)
    {
        var saveData = DB.DigGame;
        if (saveData == null || string.IsNullOrEmpty(levelDataId) || levelDataId == DigGameLevelCtrl.TbcDataId)
        {
            this.LogWarning($"[DigPlayCtrl] 无法记录 Dig 星级。levelId:{levelDataId}, sortId:{_levelId}");
            return false;
        }

        bool repairedCollections = saveData.EnsureValidCollections();
        int normalizedStarCount = Mathf.Clamp(victoryStarCount, 0, 3);
        bool hadCollectedCount = saveData.LevelCollectedStar.TryGetValue(levelDataId, out int originalCollectedCount);
        int collectedStarCount = Mathf.Clamp(originalCollectedCount, 0, 3);
        int addedStarCount = Mathf.Max(0, normalizedStarCount - collectedStarCount);
        if (addedStarCount <= 0)
        {
            if (!repairedCollections || DB.SaveDigGame())
                return true;

            this.LogWarning($"[DigPlayCtrl] 修复后的星级集合保存失败。levelId:{levelDataId}");
            return false;
        }

        bool hadWaitingCount = saveData.LevelWaitingAnimStar.TryGetValue(levelDataId, out int originalWaitingCount);
        saveData.LevelCollectedStar[levelDataId] = normalizedStarCount;
        saveData.LevelWaitingAnimStar[levelDataId] = Mathf.Max(0, originalWaitingCount) + addedStarCount;
        if (DB.SaveDigGame())
            return true;

        if (hadCollectedCount)
            saveData.LevelCollectedStar[levelDataId] = originalCollectedCount;
        else
            saveData.LevelCollectedStar.Remove(levelDataId);

        if (hadWaitingCount)
            saveData.LevelWaitingAnimStar[levelDataId] = originalWaitingCount;
        else
            saveData.LevelWaitingAnimStar.Remove(levelDataId);

        this.LogWarning($"[DigPlayCtrl] 星级保存失败。levelId:{levelDataId}, starCount:{normalizedStarCount}");
        return false;
    }
}
public class DrawPlayCtrl { private int _drawLevelDisplayId=1;public bool Write(string id,int stars)=>TryRecordVictoryStars(id,stars);
    private bool TryRecordVictoryStars(string levelDataId, int victoryStarCount)
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null || string.IsNullOrEmpty(levelDataId))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Cannot record Draw stars. levelId:{levelDataId}, sortId:{_drawLevelDisplayId}");
            return false;
        }

        bool repairedCollections = saveData.EnsureValidCollections();
        int normalizedStarCount = Mathf.Clamp(victoryStarCount, 0, 3);
        bool hadCollectedCount = saveData.LevelCollectedStar.TryGetValue(
            levelDataId,
            out int originalCollectedCount);
        int collectedStarCount = Mathf.Clamp(originalCollectedCount, 0, 3);
        int addedStarCount = Mathf.Max(0, normalizedStarCount - collectedStarCount);
        if (addedStarCount <= 0)
        {
            if (!repairedCollections || DB.SaveDrawGame())
                return true;

            this.LogWarning(
                $"[DrawPlayCtrl] Failed to save repaired Draw star collections. levelId:{levelDataId}");
            return false;
        }

        bool hadWaitingCount = saveData.LevelWaitingAnimStar.TryGetValue(
            levelDataId,
            out int originalWaitingCount);
        saveData.LevelCollectedStar[levelDataId] = normalizedStarCount;
        saveData.LevelWaitingAnimStar[levelDataId] =
            Mathf.Max(0, originalWaitingCount) + addedStarCount;

        if (DB.SaveDrawGame())
            return true;

        if (hadCollectedCount)
            saveData.LevelCollectedStar[levelDataId] = originalCollectedCount;
        else
            saveData.LevelCollectedStar.Remove(levelDataId);

        if (hadWaitingCount)
            saveData.LevelWaitingAnimStar[levelDataId] = originalWaitingCount;
        else
            saveData.LevelWaitingAnimStar.Remove(levelDataId);

        this.LogWarning(
            $"[DrawPlayCtrl] Failed to save Draw stars. levelId:{levelDataId}, starCount:{normalizedStarCount}");
        return false;
    }
}
public class DigGameLevelCtrl {public const string TbcDataId="40999";private List<DigLevelSort> _levelSorts=new(){new(){Id=1,Path="40001"},new(){Id=2,Path="40002"}}; public bool IsFirstVictory(int id)=>!DB.DigGame.PassedLevels.Contains(id);
    public bool RecordVictory(int id, DigLevelData data)
    {
        if (data == null || data.Id == TbcDataId) return false;
        var save = DB.DigGame;
        save.EnsureValidCollections();
        bool first = !save.PassedLevels.Contains(id);
        if (first) save.PassedLevels.Add(id);
        save.MaxLevel = Math.Max(save.MaxLevel, id);
        save.CurrentLevel = id;
        // 当前排序仍映射原内容时，才顺序解锁下一关，避免云控重排后解锁错误内容。
        int index = _levelSorts.FindIndex(item => item.Id == id && item.Path == data.Id);
        if (index >= 0 && index + 1 < _levelSorts.Count)
        {
            int next = _levelSorts[index + 1].Id;
            if (!save.UnlockLevels.Contains(next)) save.UnlockLevels.Add(next);
            save.CurrentLevel = next;
        }
        if (!DB.SaveDigGame()) this.LogError("[Dig] 通关进度保存失败。");
        return first;
    }
}
public class DigGame_GameVictoryPanel:BaseUIView {
 private int _collectedStars,_newStars;private bool _isConfigured,_isEntranceAnimationComplete=true,_starsStarted,_isPresentationReady;private int PresentationVersion;public Stars victoryStarGroup=new();private Share sharePart;private bool SupportsSharePart=>true;
 public int High=>_collectedStars;public int Added=>_newStars;public bool Ready=>_isPresentationReady;public void Close(){IsOpen=false;PresentationVersion++;}private void RefreshButtonStates(){}
    public void Configure(DigLevelData levelData)
    {
        CacheAndConsumeVictoryStars(levelData);
        if (SupportsSharePart && sharePart != null)
            sharePart.RefreshDigData(levelData);

        _isConfigured = true;

        if (_isEntranceAnimationComplete)
        {
            RefreshPresentation();
        }
    }
    private void CacheAndConsumeVictoryStars(DigLevelData levelData)
    {
        _collectedStars = 0;
        _newStars = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return;

        var saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[DigGame_GameVictoryPanel] 无法读取 Dig 星级存档。levelId:{levelData.Id}");
            return;
        }

        bool changed = saveData.EnsureValidCollections();
        if (saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int collectedStars))
            _collectedStars = Mathf.Clamp(collectedStars, 0, 3);
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingStars))
        {
            _newStars = Mathf.Clamp(waitingStars, 0, _collectedStars);
            saveData.LevelWaitingAnimStar.Remove(levelData.Id);
            changed = true;
        }
        if (changed && !DB.SaveDigGame())
            this.LogWarning($"[DigGame_GameVictoryPanel] 消费 Dig 待播星保存失败。levelId:{levelData.Id}");
    }
    private void RefreshPresentation()
    {
        if (!_isConfigured || !_isEntranceAnimationComplete || _starsStarted) return;
        _starsStarted = true;
        int version = PresentationVersion;
        void CompleteStars()
        {
            if (this == null || !IsOpen || version != PresentationVersion) return;
            _isPresentationReady = true;
            RefreshButtonStates();
        }
        if (!victoryStarGroup || !victoryStarGroup.Refresh(_collectedStars, _newStars, CompleteStars)) CompleteStars();
    }
    public static DigGame_GameVictoryPanel Create(DigLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        DigGame_GameVictoryPanel panel =
            UIManager.Instance.OpenUI<DigGame_GameVictoryPanel>(
                nameof(DigGame_GameVictoryPanel),
                UILayer.Popup);
        panel?.Configure(levelData);
        return panel;
    }
}
public class DigGame_GameVictoryPanel_Reward:DigGame_GameVictoryPanel {
    public new static DigGame_GameVictoryPanel_Reward Create(DigLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        DigGame_GameVictoryPanel_Reward panel =
            UIManager.Instance.OpenUI<DigGame_GameVictoryPanel_Reward>(
                nameof(DigGame_GameVictoryPanel_Reward),
                UILayer.Popup);
        panel?.Configure(levelData);
        return panel;
    }
}
public class DrawGame_GameVictoryPanel {
 private int _collectedStarCount,_waitingAnimStarCount;public int High=>_collectedStarCount;public int Added=>_waitingAnimStarCount;public void Consume(DrawLevelData d)=>CacheAndConsumeVictoryStars(d);
    private void CacheAndConsumeVictoryStars(DrawLevelData levelData)
    {
        _collectedStarCount = 0;
        _waitingAnimStarCount = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return;

        var saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogWarning(
                $"[DrawGame_GameVictoryPanel] Cannot read Draw stars. levelId:{levelData.Id}");
            return;
        }

        bool changed = saveData.EnsureValidCollections();
        if (saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int collectedStarCount))
            _collectedStarCount = Mathf.Clamp(collectedStarCount, 0, 3);

        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingAnimStarCount))
        {
            _waitingAnimStarCount = Mathf.Clamp(
                waitingAnimStarCount,
                0,
                _collectedStarCount);
            saveData.LevelWaitingAnimStar.Remove(levelData.Id);
            changed = true;
        }

        if (changed && !DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[DrawGame_GameVictoryPanel] Failed to consume Draw waiting stars. levelId:{levelData.Id}");
        }
    }
}
public class GameVictoryPanel_DeepLink:BaseUIView {
 public Stars victoryStarGroup=new();public bool _isStarPresentationComplete,_isEntranceComplete=true,_isNavigationTriggered;public Block clickBlock=new();public CustomButton homeButton=new(),replayButton=new(),nextButton=new();public CanvasGroup nextButtonGroup=new();
    public static GameVictoryPanel_DeepLink CreateForDig(DigLevelData levelData)
    {
        if (levelData == null || UIManager.Instance == null)
            return null;

        GameVictoryPanel_DeepLink panel = OpenPanel();
        panel?.ConfigureDigStars(levelData);
        return panel;
    }
    private static GameVictoryPanel_DeepLink OpenPanel()
    {
        return UIManager.Instance.OpenUI<GameVictoryPanel_DeepLink>(
            nameof(GameVictoryPanel_DeepLink),
            UILayer.Popup);
    }
    public void ConfigureDigStars(DigLevelData levelData)
    {
        int collectedStarCount = ConsumeDigWaitingStars(levelData, out int waitingAnimStarCount);
        if (victoryStarGroup == null)
        {
            CompleteStarPresentation();
            return;
        }

        bool isAnimationPlaying = victoryStarGroup.RefreshStars(
            collectedStarCount,
            waitingAnimStarCount,
            CompleteStarPresentation);
        SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            CompleteStarPresentation();
    }
    private int ConsumeDigWaitingStars(DigLevelData levelData, out int waitingAnimStarCount)
    {
        waitingAnimStarCount = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return 0;

        var saveData = DB.DigGame;
        if (saveData == null)
        {
            this.LogWarning($"[GameVictoryPanel_DeepLink] Dig 星级存档不可用。levelId:{levelData.Id}");
            return 0;
        }

        bool changed = saveData.EnsureValidCollections();
        int collectedStarCount = saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int count)
            ? Mathf.Clamp(count, 0, 3)
            : 0;
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingCount))
            waitingAnimStarCount = Mathf.Clamp(waitingCount, 0, collectedStarCount);
        if (saveData.LevelWaitingAnimStar.Remove(levelData.Id))
            changed = true;

        if (changed && !DB.SaveDigGame())
            this.LogWarning($"[GameVictoryPanel_DeepLink] 消费 Dig 待播星保存失败。levelId:{levelData.Id}");

        return collectedStarCount;
    }
    public void ConfigureDrawStars(DrawLevelData levelData)
    {
        int collectedStarCount = ConsumeDrawWaitingStars(levelData, out int waitingAnimStarCount);
        if (victoryStarGroup == null)
        {
            CompleteStarPresentation();
            return;
        }

        bool isAnimationPlaying = victoryStarGroup.RefreshStars(
            collectedStarCount,
            waitingAnimStarCount,
            CompleteStarPresentation);
        SetStarAnimationBlocked(isAnimationPlaying);
        if (!isAnimationPlaying)
            CompleteStarPresentation();
    }
    private int ConsumeDrawWaitingStars(DrawLevelData levelData, out int waitingAnimStarCount)
    {
        waitingAnimStarCount = 0;
        if (levelData == null || string.IsNullOrEmpty(levelData.Id))
            return 0;

        var saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogWarning($"[GameVictoryPanel_DeepLink] Draw star save is unavailable. levelId:{levelData.Id}");
            return 0;
        }

        bool changed = saveData.EnsureValidCollections();
        int collectedStarCount = saveData.LevelCollectedStar.TryGetValue(levelData.Id, out int count)
            ? Mathf.Clamp(count, 0, 3)
            : 0;
        if (saveData.LevelWaitingAnimStar.TryGetValue(levelData.Id, out int waitingCount))
            waitingAnimStarCount = Mathf.Clamp(waitingCount, 0, collectedStarCount);
        if (saveData.LevelWaitingAnimStar.Remove(levelData.Id))
            changed = true;

        if (changed && !DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[GameVictoryPanel_DeepLink] Failed to consume Draw waiting stars. levelId:{levelData.Id}");
        }

        return collectedStarCount;
    }
    private void CompleteStarPresentation()
    {
        _isStarPresentationComplete = true;
        SetStarAnimationBlocked(false);
        RefreshButtonStates();
    }
    private void SetStarAnimationBlocked(bool isBlocked)
    {
        if (clickBlock != null)
            clickBlock.SetActive(isBlocked);
    }
    private void RefreshButtonStates()
    {
        bool interactable = _isEntranceComplete && _isStarPresentationComplete && !_isNavigationTriggered;
        if (homeButton != null)
            homeButton.Interactable = interactable;
        if (replayButton != null)
            replayButton.Interactable = interactable;
        if (nextButton != null)
            nextButton.Interactable = interactable;
        if (nextButtonGroup != null)
        {
            nextButtonGroup.interactable = interactable;
            nextButtonGroup.blocksRaycasts = interactable;
        }
    }
}
public class GeneratorProbe {public static string Preview(string path)=>GeneratePartialClassCodeStatic(path);
        private static string GeneratePartialClassCodeStatic(string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                return null;
            }

            string scriptContent = File.ReadAllText(scriptPath);

            string className = ExtractClassNameStatic(scriptContent);
            if (string.IsNullOrEmpty(className))
            {
                return null;
            }

            string namespaceName = ExtractNamespaceStatic(scriptContent);

            // 检查是否继承自 BaseSaveDataSO
            bool inheritsFromBaseSaveDataSO = CheckInheritsFromBaseSaveDataSOStatic(scriptContent);

            List<FieldInfo> fields = ExtractPrivateFieldsStatic(scriptContent);

            if (fields.Count == 0)
            {
                return null;
            }

            return BuildPartialClassCodeStatic(className, namespaceName, fields, inheritsFromBaseSaveDataSO, scriptPath);
        }
        private static bool CheckInheritsFromBaseSaveDataSOStatic(string scriptContent)
        {
            return scriptContent.Contains("BaseSaveDataSO") ||
                   Regex.IsMatch(scriptContent, @":\s*.*BaseSaveDataSO");
        }
        private static string ExtractClassNameStatic(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"public\s+(?:partial\s+)?class\s+(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
        private static string ExtractNamespaceStatic(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"namespace\s+([\w.]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }
        private static List<FieldInfo> ExtractPrivateFieldsStatic(string scriptContent)
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            // 使用两步方法：
            // 1. 先找到所有 [SerializeField] private 的位置
            // 2. 然后解析每个位置的类型和字段名

            // 匹配模式：允许前面有注释，然后是 [SerializeField] private
            string basePattern = @"(?:^\s*//[^\r\n]*[\r\n]+)?\s*\[SerializeField\]\s+private\s+";
            MatchCollection baseMatches = Regex.Matches(scriptContent, basePattern, RegexOptions.Multiline);

            foreach (Match baseMatch in baseMatches)
            {
                int startIndex = baseMatch.Index + baseMatch.Length;
                string remaining = scriptContent.Substring(startIndex);

                // 解析类型（可能包含泛型和数组）
                string type = ExtractTypeStatic(remaining);
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                // 跳过类型后的空白字符
                int typeEndIndex = type.Length;
                while (typeEndIndex < remaining.Length && char.IsWhiteSpace(remaining[typeEndIndex]))
                {
                    typeEndIndex++;
                }

                // 提取字段名（标识符）
                string remainingAfterType = remaining.Substring(typeEndIndex);
                Match fieldNameMatch = Regex.Match(remainingAfterType, @"^(\w+)");
                if (!fieldNameMatch.Success)
                {
                    continue;
                }

                string fieldName = fieldNameMatch.Groups[1].Value;

                // 提取初始化值（如果有）
                string defaultValue = ExtractDefaultValueStatic(remainingAfterType, fieldName);

                // 排除 storageType 字段
                if (fieldName != "storageType" && type != "StorageType")
                {
                    fields.Add(new FieldInfo
                    {
                        Type = type.Trim(),
                        Name = fieldName.Trim(),
                        DefaultValue = defaultValue
                    });
                }
            }

            return fields;
        }
        private static string ExtractDefaultValueStatic(string remainingText, string fieldName)
        {
            // 跳过字段名
            int index = fieldName.Length;

            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }

            // 检查是否有 = 号
            if (index >= remainingText.Length || remainingText[index] != '=')
            {
                return null; // 没有初始化值
            }

            index++; // 跳过 =

            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }

            // 提取初始化值，直到遇到分号
            StringBuilder valueBuilder = new StringBuilder();
            int depth = 0; // 用于处理嵌套的括号

            while (index < remainingText.Length)
            {
                char c = remainingText[index];

                if (c == ';' && depth == 0)
                {
                    break; // 遇到分号且不在括号内，结束
                }

                if (c == '(' || c == '[' || c == '{' || c == '<')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}' || c == '>')
                {
                    depth--;
                }

                valueBuilder.Append(c);
                index++;
            }

            string defaultValue = valueBuilder.ToString().Trim();
            return string.IsNullOrEmpty(defaultValue) ? null : defaultValue;
        }
        private static string ExtractTypeStatic(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            int index = 0;
            StringBuilder typeBuilder = new StringBuilder();

            // 跳过前导空白
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            // 提取基本类型名（可能包含命名空间，如 System.Collections.Generic.List）
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '.'))
            {
                typeBuilder.Append(text[index]);
                index++;
            }

            // 处理泛型参数 <>
            if (index < text.Length && text[index] == '<')
            {
                typeBuilder.Append('<');
                index++;

                int depth = 1;
                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '<')
                    {
                        depth++;
                    }
                    else if (text[index] == '>')
                    {
                        depth--;
                    }
                    typeBuilder.Append(text[index]);
                    index++;
                }
            }

            // 处理数组声明 []
            while (index < text.Length && text[index] == '[')
            {
                typeBuilder.Append('[');
                index++;

                // 匹配数组内容（可能包含逗号，如 [,]）
                while (index < text.Length && text[index] != ']')
                {
                    typeBuilder.Append(text[index]);
                    index++;
                }

                if (index < text.Length)
                {
                    typeBuilder.Append(']');
                    index++;
                }
            }

            return typeBuilder.ToString();
        }
        private static string BuildPartialClassCodeStatic(string className, string namespaceName, List<FieldInfo> fields, bool inheritsFromBaseSaveDataSO, string scriptPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections.Generic;");
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine("#if UNITY_EDITOR");
                sb.AppendLine("using UnityEditor;");
                sb.AppendLine("#endif");
            }
            sb.AppendLine();

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {className}");
                sb.AppendLine("    {");
            }
            else
            {
                sb.AppendLine($"public partial class {className}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(namespaceName) ? "    " : "        ";

            // 如果继承自 BaseSaveDataSO，添加 Init() 方法
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine($"{indent}/// <summary>");
                sb.AppendLine($"{indent}/// 初始化数据（设置默认值）");
                sb.AppendLine($"{indent}/// </summary>");
                sb.AppendLine($"{indent}public override void Init()");
                sb.AppendLine($"{indent}{{");

                // 为每个字段生成初始化代码
                foreach (var field in fields)
                {
                    string initValue = GetDefaultValueForTypeStatic(field.Type, field.DefaultValue);
                    sb.AppendLine($"{indent}    {field.Name} = {initValue};");
                }

                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            // 生成属性
            foreach (var field in fields)
            {
                string propertyName = ToPascalCaseStatic(field.Name);
                sb.AppendLine($"{indent}public {field.Type} {propertyName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get => {field.Name};");
                sb.AppendLine($"{indent}    set => {field.Name} = value;");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
        private static string GetDefaultValueForTypeStatic(string type, string defaultValue)
        {
            // 如果有显式的初始化值，直接使用
            if (!string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            // 根据类型返回默认值
            type = type.Trim();

            // 处理泛型类型（如 List<int>）
            if (type.Contains("<"))
            {
                int genericStart = type.IndexOf('<');
                string baseType = type.Substring(0, genericStart).Trim();

                if (baseType == "List" || baseType.Contains("List"))
                {
                    return "new " + type + "()";
                }

                if (baseType == "Dictionary" || baseType.Contains("Dictionary"))
                {
                    return "new " + type + "()";
                }
            }

            // 处理数组类型
            if (type.EndsWith("[]"))
            {
                return "new " + type.Replace("[]", "[0]");
            }

            // 基本类型默认值
            switch (type)
            {
                case "int":
                case "Int32":
                    return "0";
                case "long":
                case "Int64":
                    return "0L";
                case "float":
                case "Single":
                    return "0f";
                case "double":
                case "Double":
                    return "0d";
                case "bool":
                case "Boolean":
                    return "false";
                case "string":
                case "String":
                    return "\"\"";
                case "Vector2":
                    return "Vector2.zero";
                case "Vector3":
                    return "Vector3.zero";
                case "Vector4":
                    return "Vector4.zero";
                case "Color":
                    return "Color.white";
                case "Quaternion":
                    return "Quaternion.identity";
                default:
                    // 对于其他类型，尝试使用 new 构造
                    if (type.Contains("List") || type.Contains("Dictionary") || type.Contains("HashSet"))
                    {
                        return "new " + type + "()";
                    }
                    return "default(" + type + ")";
            }
        }
        private static string ToPascalCaseStatic(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return char.ToUpper(input[0]) + input.Substring(1);
        }
private class FieldInfo{public string Type,Name,DefaultValue;}}
namespace Bear.SaveModule { public class BaseSaveDataSO:UnityEngine.ScriptableObject {public virtual void Init(){}
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
        public virtual void FromJson(string json)
        {
            JsonConvert.PopulateObject(json, this);
        }
}}
