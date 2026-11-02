using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// 数据查看器 - 查看和管理 PlayerPrefs 和 Json 存储的数据
    /// </summary>
    public class DataViewer : EditorWindow
    {
        private enum StorageTab
        {
            PlayerPrefs,
            Json
        }
        
        private StorageTab _currentTab = StorageTab.PlayerPrefs;
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        
        // DBSetting 引用
        private DBSetting _dbSetting;
        
        // PlayerPrefs 数据
        private List<PlayerPrefsData> _playerPrefsData = new List<PlayerPrefsData>();
        private int _selectedPlayerPrefsIndex = -1;
        
        // Json 数据
        private List<JsonData> _jsonData = new List<JsonData>();
        private int _selectedJsonIndex = -1;
        
        // 编辑状态
        private string _editingKey = "";
        private string _editingValue = "";
        private bool _isEditing = false;
        
        // JSON 树形展开状态: key = "文件名::json路径"
        private readonly Dictionary<string, bool> _jsonFoldoutStates = new Dictionary<string, bool>();
        
        // 编辑面板状态
        private JToken _editingJToken;
        private bool _editRawJsonMode = false;
        private readonly Dictionary<string, bool> _editFoldoutStates = new Dictionary<string, bool>();
        private float _editPanelHeight = 300f;
        private bool _isResizingEditPanel = false;
        private Vector2 _editScrollPosition;
        
        [MenuItem("Tools/DataViewer", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<DataViewer>("Data Viewer");
        }
        
        private void OnEnable()
        {
            LoadDBSetting();
            RefreshData();
        }
        
        private void LoadDBSetting()
        {
            if (_dbSetting == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:DBSetting");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _dbSetting = AssetDatabase.LoadAssetAtPath<DBSetting>(path);
                }
            }
        }
        
        private void OnGUI()
        {
            DrawHeader();
            DrawTabs();
            DrawToolbar();
            DrawDataList();
            
            GUILayout.FlexibleSpace();
            DrawEditPanel();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label("数据查看器", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
            {
                RefreshData();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();
            
            bool playerPrefsSelected = _currentTab == StorageTab.PlayerPrefs;
            bool playerPrefsNewValue = GUILayout.Toggle(playerPrefsSelected, "PlayerPrefs", EditorStyles.toolbarButton);
            
            if (playerPrefsNewValue != playerPrefsSelected)
            {
                if (playerPrefsNewValue)
                {
                    _currentTab = StorageTab.PlayerPrefs;
                    _selectedPlayerPrefsIndex = -1;
                    _selectedJsonIndex = -1;
                    _isEditing = false;
                }
            }
            
            bool jsonSelected = _currentTab == StorageTab.Json;
            bool jsonNewValue = GUILayout.Toggle(jsonSelected, "Json Files", EditorStyles.toolbarButton);
            
            if (jsonNewValue != jsonSelected)
            {
                if (jsonNewValue)
                {
                    _currentTab = StorageTab.Json;
                    _selectedPlayerPrefsIndex = -1;
                    _selectedJsonIndex = -1;
                    _isEditing = false;
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            GUILayout.Label("搜索:", GUILayout.Width(40));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
            
            GUILayout.FlexibleSpace();
            
            if (_currentTab == StorageTab.Json)
            {
                if (GUILayout.Button("打开保存目录", EditorStyles.toolbarButton))
                {
                    OpenJsonSaveDirectory();
                }
            }
            
            if (GUILayout.Button("删除选中", EditorStyles.toolbarButton))
            {
                DeleteSelected();
            }
            
            if (GUILayout.Button("删除所有", EditorStyles.toolbarButton))
            {
                DeleteAll();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        private void DrawDataList()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_currentTab == StorageTab.PlayerPrefs)
            {
                DrawPlayerPrefsList();
            }
            else
            {
                DrawJsonList();
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawPlayerPrefsList()
        {
            var filteredData = _playerPrefsData.Where(d => 
                string.IsNullOrEmpty(_searchFilter) || 
                d.Key.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                d.Value.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            if (filteredData.Count == 0)
            {
                EditorGUILayout.HelpBox("没有找到 PlayerPrefs 数据", MessageType.Info);
                return;
            }
            
            EditorGUILayout.LabelField($"找到 {filteredData.Count} 条数据", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            
            for (int i = 0; i < filteredData.Count; i++)
            {
                var data = filteredData[i];
                int originalIndex = _playerPrefsData.IndexOf(data);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // 标题行
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = _selectedPlayerPrefsIndex == originalIndex;
                bool newValue = GUILayout.Toggle(isSelected, "", GUILayout.Width(20));
                
                if (newValue != isSelected)
                {
                    if (newValue)
                    {
                        _selectedPlayerPrefsIndex = originalIndex;
                    }
                    else
                    {
                        _selectedPlayerPrefsIndex = -1;
                    }
                    _isEditing = false;
                }
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(data.Key, GUILayout.ExpandWidth(true));
                EditorGUILayout.TextField(data.Type, GUILayout.Width(80));
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("编辑", GUILayout.Width(50)))
                {
                    _selectedPlayerPrefsIndex = originalIndex;
                    _editingKey = data.Key;
                    _editingValue = data.Value;
                    _isEditing = true;
                    _editRawJsonMode = false;
                    _editFoldoutStates.Clear();
                    
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        try
                        {
                            _editingJToken = JToken.Parse(data.Value);
                        }
                        catch
                        {
                            _editingJToken = null;
                        }
                    }
                    else
                    {
                        _editingJToken = null;
                    }
                }
                
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除键 '{data.Key}' 吗？", "删除", "取消"))
                    {
                        DeletePlayerPrefsKey(data.Key);
                        RefreshData();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 值内容 - JSON 树形展示
                EditorGUILayout.Space(1);
                DrawPlayerPrefsValue(data);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }
        
        private void DrawPlayerPrefsValue(PlayerPrefsData data)
        {
            if (!data.ParseAttempted)
            {
                data.ParseAttempted = true;
                if (!string.IsNullOrEmpty(data.Value))
                {
                    try
                    {
                        data.ParsedToken = JToken.Parse(data.Value);
                    }
                    catch
                    {
                        data.ParsedToken = null;
                    }
                }
            }
            
            if (data.ParsedToken != null)
            {
                string statePrefix = $"pp:{data.Key}";
                DrawJToken(data.ParsedToken, "", statePrefix, 0);
            }
            else
            {
                GUIStyle style = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true
                };
                EditorGUILayout.LabelField(data.Value, style);
            }
        }
        
        private void DrawJsonList()
        {
            var filteredData = _jsonData.Where(d => 
                string.IsNullOrEmpty(_searchFilter) || 
                d.FileName.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                d.Content.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            if (filteredData.Count == 0)
            {
                EditorGUILayout.HelpBox("没有找到 Json 文件", MessageType.Info);
                return;
            }
            
            EditorGUILayout.LabelField($"找到 {filteredData.Count} 个文件", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            
            for (int i = 0; i < filteredData.Count; i++)
            {
                var data = filteredData[i];
                int originalIndex = _jsonData.IndexOf(data);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = _selectedJsonIndex == originalIndex;
                bool newValue = GUILayout.Toggle(isSelected, "", GUILayout.Width(20));
                
                if (newValue != isSelected)
                {
                    if (newValue)
                    {
                        _selectedJsonIndex = originalIndex;
                    }
                    else
                    {
                        _selectedJsonIndex = -1;
                    }
                    _isEditing = false;
                }
                
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("文件名:", data.FileName, GUILayout.ExpandWidth(true));
                EditorGUILayout.TextField("大小:", FormatFileSize(data.FileSize), GUILayout.Width(80));
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("打开", GUILayout.Width(50)))
                {
                    OpenJsonFileInExplorer(data.FilePath);
                }
                
                if (GUILayout.Button("编辑", GUILayout.Width(50)))
                {
                    _selectedJsonIndex = originalIndex;
                    _editingKey = data.FileName;
                    _editingValue = data.Content;
                    _isEditing = true;
                    _editRawJsonMode = false;
                    _editFoldoutStates.Clear();
                    
                    try
                    {
                        _editingJToken = JToken.Parse(data.Content);
                    }
                    catch
                    {
                        _editingJToken = null;
                    }
                }
                
                if (GUILayout.Button("删除", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除文件 '{data.FileName}' 吗？", "删除", "取消"))
                    {
                        DeleteJsonFile(data.FilePath);
                        RefreshData();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // JSON 结构化树形展示
                EditorGUILayout.Space(1);
                EditorGUILayout.LabelField("存储数据:", EditorStyles.miniLabel);
                DrawJsonTree(data);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }
        
        #region JSON Tree View
        
        private void DrawJsonTree(JsonData data)
        {
            try
            {
                if (data.ParsedToken == null)
                {
                    data.ParsedToken = JToken.Parse(data.Content);
                }
                
                if (data.ParsedToken != null)
                {
                    DrawJToken(data.ParsedToken, "", data.FileName, 0);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"JSON 解析失败: {ex.Message}", MessageType.Warning);
                EditorGUILayout.TextArea(data.Content, GUILayout.Height(60));
            }
        }
        
        private void DrawJToken(JToken token, string propertyName, string pathPrefix, int depth)
        {
            string currentPath = string.IsNullOrEmpty(pathPrefix) 
                ? propertyName 
                : (string.IsNullOrEmpty(propertyName) ? pathPrefix : $"{pathPrefix}.{propertyName}");
            
            if (string.IsNullOrEmpty(currentPath))
                currentPath = "root";
            
            switch (token.Type)
            {
                case JTokenType.Object:
                    DrawJObject((JObject)token, propertyName, currentPath, depth);
                    break;
                case JTokenType.Array:
                    DrawJArray((JArray)token, propertyName, currentPath, depth);
                    break;
                default:
                    DrawJValue((JValue)token, propertyName, currentPath, depth);
                    break;
            }
        }
        
        private void DrawJObject(JObject jObj, string propertyName, string currentPath, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            bool isExpanded = GetFoldoutState(currentPath);
            int count = jObj.Count;
            string label;
            
            if (string.IsNullOrEmpty(propertyName))
            {
                label = $"{{}}  (Object, {count} 个字段)";
            }
            else
            {
                label = $"{propertyName}: {{}}  (Object, {count} 个字段)";
            }
            
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
            SetFoldoutState(currentPath, newExpanded);
            EditorGUILayout.EndHorizontal();
            
            if (newExpanded)
            {
                foreach (var prop in jObj.Properties())
                {
                    DrawJToken(prop.Value, prop.Name, currentPath, depth + 1);
                }
            }
        }
        
        private void DrawJArray(JArray jArr, string propertyName, string currentPath, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            bool isExpanded = GetFoldoutState(currentPath);
            int count = jArr.Count;
            string label;
            
            if (string.IsNullOrEmpty(propertyName))
            {
                label = $"[]  (Array, {count} 个元素)";
            }
            else
            {
                label = $"{propertyName}: []  (Array, {count} 个元素)";
            }
            
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
            SetFoldoutState(currentPath, newExpanded);
            EditorGUILayout.EndHorizontal();
            
            if (newExpanded)
            {
                for (int i = 0; i < jArr.Count; i++)
                {
                    DrawJToken(jArr[i], $"[{i}]", currentPath, depth + 1);
                }
            }
        }
        
        private void DrawJValue(JValue jVal, string propertyName, string currentPath, int depth)
        {
            // 检测是否是嵌套 JSON 字符串（如 RemoteConfigCache.level_config）
            if (jVal.Type == JTokenType.String)
            {
                string strVal = jVal.Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(strVal))
                {
                    string trimmed = strVal.TrimStart();
                    if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                    {
                        try
                        {
                            JToken nestedToken = JToken.Parse(strVal);
                            DrawJToken(nestedToken, propertyName, currentPath, depth);
                            return;
                        }
                        catch
                        {
                            // 不是有效的 JSON，作为普通字符串渲染
                        }
                    }
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            // Key 列
            if (!string.IsNullOrEmpty(propertyName))
            {
                float keyWidth = Mathf.Min(180, EditorGUIUtility.currentViewWidth * 0.32f);
                EditorGUILayout.LabelField(propertyName + ":", GUILayout.Width(keyWidth));
            }
            
            // Value 列（带类型颜色）
            string formattedValue = FormatJValue(jVal);
            GUIStyle valueStyle = new GUIStyle(EditorStyles.label)
            {
                richText = true,
                wordWrap = false
            };
            
            EditorGUILayout.LabelField(formattedValue, valueStyle, GUILayout.ExpandWidth(true));
            
            EditorGUILayout.EndHorizontal();
        }
        
        private string FormatJValue(JValue value)
        {
            const int MAX_LEN = 300;
            string raw;
            string color;
            
            switch (value.Type)
            {
                case JTokenType.String:
                    raw = value.Value?.ToString() ?? "";
                    if (raw.Length > MAX_LEN)
                        raw = raw.Substring(0, MAX_LEN) + "...";
                    color = "#72E772"; // 绿色
                    return $"<color={color}>\"{EscapeRichText(raw)}\"</color>  <color=#808080><size=9>(string)</size></color>";
                    
                case JTokenType.Integer:
                case JTokenType.Float:
                    raw = value.Value?.ToString() ?? "0";
                    color = "#6BC7F7"; // 蓝色
                    string typeLabel = value.Type == JTokenType.Integer ? "(int)" : "(float)";
                    return $"<color={color}>{raw}</color>  <color=#808080><size=9>{typeLabel}</size></color>";
                    
                case JTokenType.Boolean:
                    raw = value.Value?.ToString().ToLower() ?? "false";
                    color = "#F7C66B"; // 橙色
                    return $"<color={color}>{raw}</color>  <color=#808080><size=9>(bool)</size></color>";
                    
                case JTokenType.Null:
                    color = "#9E9E9E"; // 灰色
                    return $"<color={color}>null</color>  <color=#808080><size=9>(null)</size></color>";
                    
                case JTokenType.Date:
                    raw = value.Value?.ToString() ?? "";
                    color = "#D78CF0"; // 紫色
                    return $"<color={color}>{raw}</color>  <color=#808080><size=9>(date)</size></color>";
                    
                default:
                    raw = value.ToString();
                    if (raw.Length > MAX_LEN)
                        raw = raw.Substring(0, MAX_LEN) + "...";
                    return EscapeRichText(raw);
            }
        }
        
        private string EscapeRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input
                .Replace("<", "\u003C")
                .Replace(">", "\u003E");
        }
        
        private bool GetFoldoutState(string path)
        {
            if (!_jsonFoldoutStates.TryGetValue(path, out bool state))
            {
                _jsonFoldoutStates[path] = false; // 默认折叠
                return false;
            }
            return state;
        }
        
        private void SetFoldoutState(string path, bool state)
        {
            _jsonFoldoutStates[path] = state;
        }
        
        #endregion
        
        private void DrawEditPanel()
        {
            if (!_isEditing)
            {
                return;
            }
            
            // 拖拽调整条
            Rect resizeRect = GUILayoutUtility.GetRect(position.width, 6f);
            EditorGUI.DrawRect(resizeRect, new Color(0.35f, 0.35f, 0.35f, 0.8f));
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
            
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && resizeRect.Contains(evt.mousePosition))
            {
                _isResizingEditPanel = true;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && _isResizingEditPanel)
            {
                _editPanelHeight -= evt.delta.y;
                _editPanelHeight = Mathf.Clamp(_editPanelHeight, 120f, position.height - 150f);
                evt.Use();
                Repaint();
            }
            else if (evt.type == EventType.MouseUp && _isResizingEditPanel)
            {
                _isResizingEditPanel = false;
                evt.Use();
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(_editPanelHeight));
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("编辑数据", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginDisabledGroup(_currentTab == StorageTab.Json);
            _editingKey = EditorGUILayout.TextField("键名:", _editingKey);
            EditorGUI.EndDisabledGroup();
            
            // 编辑模式切换
            if (_editingJToken != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("编辑模式:", GUILayout.Width(60));
                bool newTreeMode = GUILayout.Toggle(!_editRawJsonMode, "树形结构", EditorStyles.radioButton);
                bool newRawMode = GUILayout.Toggle(_editRawJsonMode, "原始 JSON", EditorStyles.radioButton);
                EditorGUILayout.EndHorizontal();
                
                if (newRawMode != _editRawJsonMode)
                {
                    if (newRawMode)
                    {
                        _editingValue = _editingJToken.ToString(Formatting.Indented);
                    }
                    else
                    {
                        try
                        {
                            _editingJToken = JToken.Parse(_editingValue);
                        }
                        catch (Exception ex)
                        {
                            EditorUtility.DisplayDialog("格式错误", $"无法解析为 JSON: {ex.Message}", "确定");
                            newRawMode = true;
                        }
                    }
                    _editRawJsonMode = newRawMode;
                }
            }
            
            EditorGUILayout.Space(5);
            
            // 根据模式显示不同编辑器
            float contentHeight = Mathf.Max(60f, _editPanelHeight - 110f);
            
            if (_editingJToken != null && !_editRawJsonMode)
            {
                _editScrollPosition = EditorGUILayout.BeginScrollView(_editScrollPosition, GUILayout.Height(contentHeight));
                DrawJsonEditor(_editingJToken, "", "edit", 0);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("值:");
                _editScrollPosition = EditorGUILayout.BeginScrollView(_editScrollPosition, GUILayout.Height(contentHeight));
                _editingValue = EditorGUILayout.TextArea(_editingValue, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("保存", GUILayout.Height(25)))
            {
                SaveEditedData();
            }
            
            if (GUILayout.Button("取消", GUILayout.Height(25)))
            {
                _isEditing = false;
                _editingKey = "";
                _editingValue = "";
                _editingJToken = null;
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            EditorGUILayout.EndVertical();
        }
        
        #region JSON Tree Editor
        
        private void DrawJsonEditor(JToken token, string propertyName, string pathPrefix, int depth)
        {
            string currentPath = string.IsNullOrEmpty(pathPrefix) 
                ? propertyName 
                : (string.IsNullOrEmpty(propertyName) ? pathPrefix : $"{pathPrefix}.{propertyName}");
            
            if (string.IsNullOrEmpty(currentPath))
                currentPath = "root";
            
            switch (token.Type)
            {
                case JTokenType.Object:
                    DrawJObjectEditor((JObject)token, propertyName, currentPath, depth);
                    break;
                case JTokenType.Array:
                    DrawJArrayEditor((JArray)token, propertyName, currentPath, depth);
                    break;
                default:
                    DrawJValueEditor((JValue)token, propertyName, currentPath, depth);
                    break;
            }
        }
        
        private void DrawJObjectEditor(JObject jObj, string propertyName, string currentPath, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            bool isExpanded = GetEditFoldoutState(currentPath);
            int count = jObj.Count;
            string label = string.IsNullOrEmpty(propertyName) 
                ? $"{{}}  (Object, {count} 个字段)" 
                : $"{propertyName}: {{}}  (Object, {count} 个字段)";
            
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
            SetEditFoldoutState(currentPath, newExpanded);
            EditorGUILayout.EndHorizontal();
            
            if (newExpanded)
            {
                foreach (var prop in jObj.Properties())
                {
                    DrawJsonEditor(prop.Value, prop.Name, currentPath, depth + 1);
                }
            }
        }
        
        private void DrawJArrayEditor(JArray jArr, string propertyName, string currentPath, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            bool isExpanded = GetEditFoldoutState(currentPath);
            int count = jArr.Count;
            string label = string.IsNullOrEmpty(propertyName) 
                ? $"[]  (Array, {count} 个元素)" 
                : $"{propertyName}: []  (Array, {count} 个元素)";
            
            bool newExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
            SetEditFoldoutState(currentPath, newExpanded);
            EditorGUILayout.EndHorizontal();
            
            if (newExpanded)
            {
                for (int i = 0; i < jArr.Count; i++)
                {
                    DrawJsonEditor(jArr[i], $"[{i}]", currentPath, depth + 1);
                }
            }
        }
        
        private void DrawJValueEditor(JValue jVal, string propertyName, string currentPath, int depth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 15f);
            
            if (!string.IsNullOrEmpty(propertyName))
            {
                float keyWidth = Mathf.Min(180, EditorGUIUtility.currentViewWidth * 0.32f);
                EditorGUILayout.LabelField(propertyName + ":", GUILayout.Width(keyWidth));
            }
            
            try
            {
                switch (jVal.Type)
                {
                    case JTokenType.String:
                        string strVal = jVal.Value?.ToString() ?? "";
                        string newStr = EditorGUILayout.TextField(strVal);
                        if (newStr != strVal) jVal.Value = newStr;
                        break;
                        
                    case JTokenType.Integer:
                        long longVal = jVal.Value is long l ? l : Convert.ToInt64(jVal.Value);
                        long newLong = EditorGUILayout.LongField(longVal);
                        if (newLong != longVal) jVal.Value = newLong;
                        break;
                        
                    case JTokenType.Float:
                        double doubleVal = jVal.Value is double d ? d : Convert.ToDouble(jVal.Value);
                        double newDouble = EditorGUILayout.DoubleField(doubleVal);
                        if (Math.Abs(newDouble - doubleVal) > 0.0000001) jVal.Value = newDouble;
                        break;
                        
                    case JTokenType.Boolean:
                        bool boolVal = jVal.Value is bool b ? b : Convert.ToBoolean(jVal.Value);
                        bool newBool = EditorGUILayout.Toggle(boolVal);
                        if (newBool != boolVal) jVal.Value = newBool;
                        break;
                        
                    case JTokenType.Null:
                        EditorGUILayout.LabelField("null");
                        break;
                        
                    default:
                        EditorGUILayout.LabelField(jVal.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.LabelField($"[Error: {ex.Message}]");
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private bool GetEditFoldoutState(string path)
        {
            if (!_editFoldoutStates.TryGetValue(path, out bool state))
            {
                _editFoldoutStates[path] = false; // 默认折叠
                return false;
            }
            return state;
        }
        
        private void SetEditFoldoutState(string path, bool state)
        {
            _editFoldoutStates[path] = state;
        }
        
        #endregion
        
        private void RefreshData()
        {
            RefreshPlayerPrefs();
            RefreshJsonFiles();
            _isEditing = false;
            _selectedPlayerPrefsIndex = -1;
            _selectedJsonIndex = -1;
        }
        
        private void RefreshPlayerPrefs()
        {
            _playerPrefsData.Clear();
            
            try
            {
                LoadDBSetting();
                
                List<string> saveModuleKeys = new List<string>();
                
                if (_dbSetting != null && _dbSetting.DataClasses != null)
                {
                    foreach (var dataClass in _dbSetting.DataClasses)
                    {
                        if (dataClass.storageType == StorageType.PlayerPrefs)
                        {
                            string key = "SaveData_" + dataClass.className;
                            saveModuleKeys.Add(key);
                        }
                    }
                }
                
                if (saveModuleKeys.Count == 0)
                {
                    Type baseSaveDataSOType = typeof(BaseSaveDataSO);
                    System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    
                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            Type[] types = assembly.GetTypes();
                            foreach (var type in types)
                            {
                                if (type.IsClass && !type.IsAbstract && baseSaveDataSOType.IsAssignableFrom(type) && type != baseSaveDataSOType)
                                {
                                    StorageType storageType = type.GetStorageType();
                                    if (storageType == StorageType.PlayerPrefs)
                                    {
                                        string key = "SaveData_" + type.Name;
                                        if (PlayerPrefs.HasKey(key) && !saveModuleKeys.Contains(key))
                                        {
                                            saveModuleKeys.Add(key);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                        }
                    }
                }
                
                string[] allKeys = GetAllPlayerPrefsKeys();
                foreach (var key in allKeys)
                {
                    if (!key.StartsWith("SaveData_") && PlayerPrefs.HasKey(key))
                    {
                        saveModuleKeys.Add(key);
                    }
                }
                
                foreach (var key in saveModuleKeys)
                {
                    if (PlayerPrefs.HasKey(key))
                    {
                        string value = "";
                        string type = "String";
                        
                        string stringValue = PlayerPrefs.GetString(key, null);
                        if (stringValue != null)
                        {
                            value = stringValue;
                            type = "String";
                        }
                        else
                        {
                            int testInt = PlayerPrefs.GetInt(key, int.MinValue);
                            if (testInt != int.MinValue)
                            {
                                value = testInt.ToString();
                                type = "Int";
                            }
                            else
                            {
                                float testFloat = PlayerPrefs.GetFloat(key, float.MinValue);
                                if (testFloat != float.MinValue)
                                {
                                    value = testFloat.ToString();
                                    type = "Float";
                                }
                                else
                                {
                                    value = PlayerPrefs.GetString(key, "");
                                    type = "String";
                                }
                            }
                        }
                        
                        _playerPrefsData.Add(new PlayerPrefsData
                        {
                            Key = key,
                            Value = value,
                            Type = type
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"刷新 PlayerPrefs 数据失败: {ex.Message}");
            }
        }
        
        private string[] GetAllPlayerPrefsKeys()
        {
            List<string> keys = new List<string>();
            
            keys.AddRange(new[] {
                "UnityGraphicsQuality",
                "UnitySelectMonitor",
                "Screenmanager Resolution Width",
                "Screenmanager Resolution Height",
                "Screenmanager Is Fullscreen mode",
                "Screenmanager Fullscreen mode"
            });
            
            return keys.Where(k => PlayerPrefs.HasKey(k)).ToArray();
        }
        
        private void RefreshJsonFiles()
        {
            _jsonData.Clear();
            
            try
            {
                LoadDBSetting();
                
                string saveDirectory = Path.Combine(Application.persistentDataPath, "SaveData");
                
                if (Directory.Exists(saveDirectory))
                {
                    HashSet<string> expectedFileNames = new HashSet<string>();
                    
                    if (_dbSetting != null && _dbSetting.DataClasses != null)
                    {
                        foreach (var dataClass in _dbSetting.DataClasses)
                        {
                            if (dataClass.storageType == StorageType.Json)
                            {
                                string fileName = dataClass.className + ".json";
                                expectedFileNames.Add(fileName);
                            }
                        }
                    }
                    
                    if (expectedFileNames.Count == 0)
                    {
                        Type baseSaveDataSOType = typeof(BaseSaveDataSO);
                        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                        
                        foreach (var assembly in assemblies)
                        {
                            try
                            {
                                Type[] types = assembly.GetTypes();
                                foreach (var type in types)
                                {
                                    if (type.IsClass && !type.IsAbstract && baseSaveDataSOType.IsAssignableFrom(type) && type != baseSaveDataSOType)
                                    {
                                        StorageType storageType = type.GetStorageType();
                                        if (storageType == StorageType.Json || storageType == StorageType.Auto)
                                        {
                                            string fileName = type.Name + ".json";
                                            expectedFileNames.Add(fileName);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"扫描程序集 {assembly.FullName} 时出错: {ex.Message}");
                            }
                        }
                    }
                    
                    string[] jsonFiles = Directory.GetFiles(saveDirectory, "*.json");
                    
                    foreach (var filePath in jsonFiles)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(filePath);
                            
                            if (expectedFileNames.Count > 0 && !expectedFileNames.Contains(fileName))
                            {
                                continue;
                            }
                            
                            string content = File.ReadAllText(filePath);
                            FileInfo fileInfo = new FileInfo(filePath);
                            
                            _jsonData.Add(new JsonData
                            {
                                FileName = fileName,
                                FilePath = filePath,
                                Content = content,
                                FileSize = fileInfo.Length
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"读取文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"刷新 Json 文件失败: {ex.Message}");
            }
        }
        
        private void DeleteSelected()
        {
            if (_currentTab == StorageTab.PlayerPrefs)
            {
                if (_selectedPlayerPrefsIndex >= 0 && _selectedPlayerPrefsIndex < _playerPrefsData.Count)
                {
                    var data = _playerPrefsData[_selectedPlayerPrefsIndex];
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除键 '{data.Key}' 吗？", "删除", "取消"))
                    {
                        DeletePlayerPrefsKey(data.Key);
                        RefreshData();
                    }
                }
            }
            else
            {
                if (_selectedJsonIndex >= 0 && _selectedJsonIndex < _jsonData.Count)
                {
                    var data = _jsonData[_selectedJsonIndex];
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除文件 '{data.FileName}' 吗？", "删除", "取消"))
                    {
                        DeleteJsonFile(data.FilePath);
                        RefreshData();
                    }
                }
            }
        }
        
        private void DeleteAll()
        {
            string message = _currentTab == StorageTab.PlayerPrefs 
                ? "确定要删除所有 PlayerPrefs 数据吗？此操作不可恢复！" 
                : "确定要删除所有 Json 文件吗？此操作不可恢复！";
            
            if (EditorUtility.DisplayDialog("确认删除所有", message, "删除", "取消"))
            {
                if (_currentTab == StorageTab.PlayerPrefs)
                {
                    foreach (var data in _playerPrefsData)
                    {
                        DeletePlayerPrefsKey(data.Key);
                    }
                }
                else
                {
                    foreach (var data in _jsonData)
                    {
                        try
                        {
                            File.Delete(data.FilePath);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"删除文件失败 {data.FilePath}: {ex.Message}");
                        }
                    }
                }
                
                RefreshData();
                EditorUtility.DisplayDialog("完成", "所有数据已删除", "确定");
            }
        }
        
        private void DeletePlayerPrefsKey(string key)
        {
            try
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"已删除 PlayerPrefs 键: {key}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"删除 PlayerPrefs 键失败 {key}: {ex.Message}");
            }
        }
        
        private void DeleteJsonFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                Debug.Log($"已删除文件: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"删除文件失败 {filePath}: {ex.Message}");
            }
        }
        
        private void OpenJsonSaveDirectory()
        {
            try
            {
                string saveDirectory = Path.Combine(Application.persistentDataPath, "SaveData");
                
                if (Directory.Exists(saveDirectory))
                {
                    EditorUtility.RevealInFinder(saveDirectory);
                    Debug.Log($"已打开保存目录: {saveDirectory}");
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", $"保存目录不存在: {saveDirectory}", "确定");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"打开保存目录失败: {ex.Message}", "确定");
                Debug.LogError($"打开保存目录失败: {ex.Message}");
            }
        }
        
        private void OpenJsonFileInExplorer(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string directory = Path.GetDirectoryName(filePath);
                    
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        EditorUtility.RevealInFinder(filePath);
                        Debug.Log($"已在文件资源管理器中打开: {filePath}");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", $"文件目录不存在: {directory}", "确定");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("错误", $"文件不存在: {filePath}", "确定");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("错误", $"打开文件失败: {ex.Message}", "确定");
                Debug.LogError($"打开文件失败 {filePath}: {ex.Message}");
            }
        }
        
        private void SaveEditedData()
        {
            if (string.IsNullOrEmpty(_editingKey))
            {
                EditorUtility.DisplayDialog("错误", "键名不能为空", "确定");
                return;
            }
            
            string contentToSave;
            
            if (_editingJToken != null && !_editRawJsonMode)
            {
                contentToSave = _editingJToken.ToString(Formatting.None);
            }
            else
            {
                contentToSave = _editingValue;
            }
            
            if (string.IsNullOrEmpty(contentToSave))
            {
                EditorUtility.DisplayDialog("错误", "值不能为空", "确定");
                return;
            }
            
            if (_currentTab == StorageTab.PlayerPrefs)
            {
                try
                {
                    PlayerPrefs.SetString(_editingKey, contentToSave);
                    PlayerPrefs.Save();
                    Debug.Log($"已保存 PlayerPrefs: {_editingKey}");
                    RefreshData();
                    _isEditing = false;
                    _editingJToken = null;
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("错误", $"保存失败: {ex.Message}", "确定");
                }
            }
            else
            {
                var data = _jsonData.FirstOrDefault(d => d.FileName == _editingKey);
                if (data == null)
                {
                    EditorUtility.DisplayDialog("错误", $"找不到文件 '{_editingKey}'", "确定");
                    return;
                }
                
                try
                {
                    try
                    {
                        JsonConvert.DeserializeObject(contentToSave);
                    }
                    catch
                    {
                        if (!EditorUtility.DisplayDialog("警告", 
                            "内容可能不是有效的 JSON 格式，是否继续保存？", "继续", "取消"))
                        {
                            return;
                        }
                    }
                    
                    File.WriteAllText(data.FilePath, contentToSave);
                    Debug.Log($"已保存文件: {data.FilePath}");
                    RefreshData();
                    _isEditing = false;
                    _editingJToken = null;
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("错误", $"保存失败: {ex.Message}", "确定");
                }
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        [Serializable]
        private class PlayerPrefsData
        {
            public string Key;
            public string Value;
            public string Type;
            [NonSerialized] public JToken ParsedToken;
            [NonSerialized] public bool ParseAttempted;
        }
        
        [Serializable]
        private class JsonData
        {
            public string FileName;
            public string FilePath;
            public string Content;
            public long FileSize;
            [NonSerialized] public JToken ParsedToken;
        }
    }
}
