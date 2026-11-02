#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
#if TextMeshPro
using TMPro;
#endif

namespace Game.Editor.UIGenerator
{
    public class UIGenerator
    {
        private UIGeneratorConfig config;
        /// <summary>Cache: DefaultSpriteFolder 下所有名为 "Sprites" 的子文件夹的 Assets 相对路径，首次使用时构建。</summary>
        private List<string> _defaultSpritesFolderPaths;
#if TextMeshPro
        /// <summary>缓存当前生成任务已命中的 TMP Outline 材质，避免 AssetDatabase 索引刷新前重复创建。</summary>
        private readonly List<Material> _outlineMaterialCache = new List<Material>();
#endif

        public UIGenerator(UIGeneratorConfig config = null)
        {
            this.config = config ?? new UIGeneratorConfig();
        }

        public GameObject GenerateUIPrefab(string jsonPath)
        {
#if TextMeshPro
            _outlineMaterialCache.Clear();
#endif
            // 统一路径格式，确保跨平台兼容（Unity 使用正斜杠）
            jsonPath = jsonPath.Replace('\\', '/');
            
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"JSON file not found: {jsonPath}");
                return null;
            }

            string rawJson = File.ReadAllText(jsonPath);
            string normalizedJson = UIGeneratorJsonNormalizer.NormalizeToJson(rawJson);

            try
            {
                JToken token = JToken.Parse(normalizedJson);
                if (token is JObject jObject && jObject["document"] != null && jObject["layers"] != null)
                {
                    PsdExportRoot psdRoot = jObject.ToObject<PsdExportRoot>();
                    if (psdRoot?.Document != null && psdRoot.Layers != null)
                    {
                        return GenerateUIPrefabFromPsd(psdRoot, jsonPath);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UIGenerator] PSD/document 格式解析未采用: {ex.Message}");
            }

            // 必须以规范化后的字符串反序列化：PSD 导出常以 ( 开头，rawJson 无法被 Json.NET 解析
            List<ArtboardData> artboards = null;
            try
            {
                artboards = JsonConvert.DeserializeObject<List<ArtboardData>>(normalizedJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UIGenerator] Artboard 列表解析 (normalized): {ex.Message}");
            }

            if (artboards == null || artboards.Count == 0)
            {
                string t = rawJson.TrimStart();
                if (t.Length > 0 && t[0] != '(' && (t[0] == '[' || t[0] == '{'))
                {
                    try
                    {
                        artboards = JsonConvert.DeserializeObject<List<ArtboardData>>(rawJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[UIGenerator] Artboard 列表解析 (raw): {ex.Message}");
                    }
                }
            }

            if (artboards == null || artboards.Count == 0)
            {
                Debug.LogError("No artboard data found in JSON");
                return null;
            }

            ArtboardData artboard = artboards[0];
            return GenerateUIPrefab(artboard, jsonPath);
        }

        private GameObject GenerateUIPrefabFromPsd(PsdExportRoot psd, string jsonPath)
        {
            _defaultSpritesFolderPaths = null;

            PsdDocumentInfo doc = psd.Document;
            float w = doc != null && doc.Width > 0f ? doc.Width : 1920f;
            float h = doc != null && doc.Height > 0f ? doc.Height : 1080f;
            string origin = SanitizeHierarchyName(
                doc != null && !string.IsNullOrEmpty(doc.Name)
                    ? Path.GetFileNameWithoutExtension(doc.Name)
                    : null);
            if (string.IsNullOrEmpty(origin))
            {
                origin = SanitizeHierarchyName(Path.GetFileNameWithoutExtension(jsonPath));
            }

            if (string.IsNullOrEmpty(origin))
            {
                origin = "Root";
            }

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                canvasRect = canvasObj.AddComponent<RectTransform>();
            }

            // 参考 CommonPopup.prefab 根节点 Canvas 的 RectTransform 标准
            canvasRect.localScale = Vector3.zero;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.pivot = Vector2.zero;

            GameObject rootObj = new GameObject(origin);
            rootObj.transform.SetParent(canvasObj.transform, false);

            RectTransform rootRect = rootObj.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localRotation = Quaternion.identity;

            MonoBehaviour[] components = rootObj.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is BaseAutoUIBind autoUIBind)
                {
                    Debug.Log($"[UIGenerator] AutoBind() called on {rootObj.name} via {component.GetType().Name}");
                    break;
                }
            }

            config.DesignResolution = new Vector2(w, h);

            // PS 图层面板自上而下为「从顶到底」；Unity UI 同父节点中越后添加越后绘制（越靠前）。
            // 正向创建会使 PS 顶层落在 Hierarchy 最上一条（渲染最底层），故逆序创建以对齐叠放顺序。
            for (int i = psd.Layers.Count - 1; i >= 0; i--)
            {
                CreatePsdLayerNode(psd.Layers[i], rootObj.transform, jsonPath, null);
            }

            return canvasObj;
        }

        private void CreatePsdLayerNode(PsdLayerNode node, Transform parent, string jsonPath, Vector2? parentAbsCenter)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsGroup)
            {
                if (node.Bounds == null)
                {
                    return;
                }

                CreatePsdGroupContainer(node, parent, jsonPath, parentAbsCenter);
                return;
            }

            LayerData layer = BuildLayerDataFromPsd(node);
            if (layer == null)
            {
                return;
            }

            CreateLayerGameObject(layer, parent, jsonPath, parentAbsCenter, node.Visible);
        }

        private void CreatePsdGroupContainer(PsdLayerNode node, Transform parent, string jsonPath, Vector2? parentAbsCenter)
        {
            PsdBounds b = node.Bounds;
            if (b == null)
            {
                return;
            }

            float bw = Mathf.Max(1f, b.Width);
            float bh = Mathf.Max(1f, b.Height);
            Vector2 absCenter = ConvertPosition(b.Left, b.Top, bw, bh);
            Vector2 anchored = parentAbsCenter.HasValue ? absCenter - parentAbsCenter.Value : absCenter;

            string groupName = SanitizeHierarchyName(node.Name);
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = DefaultPsdGroupNameFromKind(node);
            }

            groupName = GetUniquePsdGroupHierarchyName(parent, node, groupName);

            GameObject go = new GameObject(groupName);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one;
            go.SetActive(node.Visible);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(bw, bh);
            rt.anchoredPosition = anchored;
            rt.localRotation = Quaternion.identity;

            InteractivityType groupInteractivityType = ClassifyInteractivityFromName(node.Name);
            if (groupInteractivityType != InteractivityType.NonInteractive)
            {
                go.AddComponent<CanvasRenderer>();
                UIEmptyRaycastTarget emptyRaycastTarget = go.AddComponent<UIEmptyRaycastTarget>();
                AssembleInteractiveBehaviour(go, groupInteractivityType, emptyRaycastTarget);
            }

            if (node.Children == null || node.Children.Count == 0)
            {
                return;
            }

            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                CreatePsdLayerNode(node.Children[i], go.transform, jsonPath, absCenter);
            }
        }

        private static LayerData BuildLayerDataFromPsd(PsdLayerNode node)
        {
            PsdBounds b = node.Bounds;
            if (b == null)
            {
                return null;
            }

            float w = Mathf.Max(0f, b.Width);
            float h = Mathf.Max(0f, b.Height);
            string type = MapPsdKindToLayerType(node.Kind);
            var layer = new LayerData
            {
                Name = node.Name ?? "layer",
                X = b.Left,
                Y = b.Top,
                Width = w,
                Height = h,
                Rotation = node.Rotation,
                Opacity = NormalizeOpacityPercent(node.Opacity),
                Type = type,
                Src = node.Src,
            };

            if (layer.IsTextLayer)
            {
                layer.TextInfo = new TextInfoData
                {
                    Content = ResolvePsdTextContent(node),
                    FontSize = ResolvePsdTextFontSize(node, b),
                    Color = BuildTextColorStringFromPsd(node),
                    FontName = "",
                    Bold = false,
                    Italic = false,
                    Opacity = NormalizeOpacityPercent(node.Opacity),
                    Outline = BuildTextOutlineDataFromPsd(node.Outline),
                };
            }

            return layer;
        }

        private static float? NormalizeOpacityPercent(float? opacity)
        {
            return opacity.HasValue && opacity.Value >= 0f && opacity.Value <= 100f
                ? opacity
                : null;
        }

        /// <summary>
        /// 优先使用导出字段 content，否则退回图层名（换行在 GetDisplayTextFromLayer 中统一处理）。
        /// </summary>
        private static string ResolvePsdTextContent(PsdLayerNode node)
        {
            return !string.IsNullOrEmpty(node?.Content) ? node.Content : (node?.Name ?? string.Empty);
        }

        private static string NormalizeTextLineEndings(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return s;
            }

            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// 优先 TextInfo.content，否则图层名；并统一换行（含 Artboard JSON）。
        /// </summary>
        private static string GetDisplayTextFromLayer(LayerData layer)
        {
            if (layer == null)
            {
                return string.Empty;
            }

            TextInfoData info = layer.TextInfo;
            string raw = info != null && !string.IsNullOrEmpty(info.Content)
                ? info.Content
                : (layer.Name ?? string.Empty);
            return NormalizeTextLineEndings(raw);
        }

        /// <summary>
        /// 优先使用导出 fontSize（大于 0），否则按 bounds 高度估算。
        /// </summary>
        private static float ResolvePsdTextFontSize(PsdLayerNode node, PsdBounds b)
        {
            if (node != null && node.FontSize > 0f)
            {
                return Mathf.Clamp(node.FontSize, 1f, 500f);
            }

            return EstimatePsdTextFontSize(b);
        }

        /// <summary>
        /// 新 JSON 中颜色为 { hex, r, g, b }；无则白底不透明。
        /// </summary>
        private static string BuildTextColorStringFromPsd(PsdLayerNode node)
        {
            return BuildColorStringFromPsd(node?.LayerColor);
        }

        /// <summary>
        /// 把 PSD 文本描边数据转换为两个 JSON 入口共用的 TextInfo 数据。
        /// </summary>
        private static TextOutlineData BuildTextOutlineDataFromPsd(PsdTextOutlineData outline)
        {
            if (outline == null)
            {
                return null;
            }

            return new TextOutlineData
            {
                Enabled = outline.Enabled,
                Color = BuildColorStringFromPsd(outline.Color),
                Width = outline.Width,
                WidthUnit = outline.WidthUnit,
            };
        }

        private static string BuildColorStringFromPsd(PsdLayerColor c)
        {
            if (c == null)
            {
                return "#FFFFFFFF";
            }

            if (!string.IsNullOrWhiteSpace(c.Hex))
            {
                return NormalizeHexColorStringForTextInfo(c.Hex.Trim());
            }

            int r = Mathf.Clamp(c.R, 0, 255);
            int g = Mathf.Clamp(c.G, 0, 255);
            int b = Mathf.Clamp(c.B, 0, 255);
            return $"#{r:X2}{g:X2}{b:X2}FF";
        }

        /// <summary>
        /// 转为 TextInfo / ParseColor 可用的 #RRGGBB 或 #RRGGBBAA。
        /// </summary>
        private static string NormalizeHexColorStringForTextInfo(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return "#FFFFFFFF";
            }

            hex = hex.Trim();
            if (hex.Length > 0 && hex[0] != '#')
            {
                hex = "#" + hex;
            }

            if (hex.Length == 4 && hex[0] == '#')
            {
                char r = hex[1];
                char g = hex[2];
                char b = hex[3];
                hex = $"#{r}{r}{g}{g}{b}{b}";
            }

            if (hex.Length == 7)
            {
                return hex + "FF";
            }

            return hex;
        }

        /// <summary>
        /// 无 fontSize 字段时，用图层高度估算字号（与常见 PS 行高比例接近）。
        /// </summary>
        private static float EstimatePsdTextFontSize(PsdBounds b)
        {
            if (b == null || b.Height <= 0f)
            {
                return 24f;
            }

            return Mathf.Clamp(b.Height * 0.82f, 8f, 200f);
        }

        private static string MapPsdKindToLayerType(string kind)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return "layer";
            }

            if (kind.IndexOf("TEXT", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "textLayer";
            }

            if (kind.IndexOf("SOLIDFILL", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "shapeLayer";
            }

            return "layer";
        }

        private static string SanitizeHierarchyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string s = name;
            foreach (char c in invalid)
            {
                s = s.Replace(c, '_');
            }

            s = s.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        private static bool HasSiblingWithName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                if (string.Equals(parent.GetChild(i).name, name, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// PSD 未提供可用组名时，用 kind（如 LayerKind.groupLayer → groupLayer）作为兜底前缀。
        /// </summary>
        private static string DefaultPsdGroupNameFromKind(PsdLayerNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Kind))
            {
                return "group";
            }

            string k = node.Kind.Trim();
            int dot = k.LastIndexOf('.');
            if (dot >= 0 && dot < k.Length - 1)
            {
                k = k.Substring(dot + 1);
            }

            k = SanitizeHierarchyName(k);
            if (string.IsNullOrEmpty(k))
            {
                return "group";
            }

            return k.ToLowerInvariant();
        }

        private enum InteractivityType
        {
            NonInteractive,
            Button,
            Toggle,
        }

        private static InteractivityType ClassifyInteractivityFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return InteractivityType.NonInteractive;
            }

            if (name.IndexOf("_btn", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InteractivityType.Button;
            }

            if (name.IndexOf("_toggle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return InteractivityType.Toggle;
            }

            return InteractivityType.NonInteractive;
        }

        private static InteractivityType ClassifyGroupChildInteractivity(PsdLayerNode child)
        {
            return ClassifyInteractivityFromName(child?.Name);
        }

        /// <summary>
        /// 仅按直接子节点自身组件类型判断重名后缀：btn 绝对优先；无 btn 时才考虑 toggle。
        /// </summary>
        private static string BuildGroupDisambiguationSuffixFromChildren(PsdLayerNode node)
        {
            if (node?.Children == null || node.Children.Count == 0)
            {
                return string.Empty;
            }

            foreach (PsdLayerNode child in node.Children)
            {
                InteractivityType type = ClassifyGroupChildInteractivity(child);
                if (type == InteractivityType.Button)
                {
                    return "_btn";
                }
                else if (type == InteractivityType.Toggle)
                {
                    return "_toggle";
                }
            }
            
            return string.Empty;
        }

        /// <summary>
        /// 与父节点下已有子物体不重名；冲突时仅按直接子节点的 btn / toggle 后缀区分，再退化为 _2、_3…
        /// </summary>
        private static string GetUniquePsdGroupHierarchyName(Transform parent, PsdLayerNode node, string sanitizedBase)
        {
            string primary = string.IsNullOrEmpty(sanitizedBase) ? DefaultPsdGroupNameFromKind(node) : sanitizedBase;

            if (parent == null || !HasSiblingWithName(parent, primary))
            {
                return primary;
            }

            string suffix = BuildGroupDisambiguationSuffixFromChildren(node);
            if (!string.IsNullOrEmpty(suffix))
            {
                string withSuffix = primary + suffix;
                if (!HasSiblingWithName(parent, withSuffix))
                {
                    return withSuffix;
                }
            }

            for (int n = 2; n < 10000; n++)
            {
                string candidate = primary + "_" + n;
                if (!HasSiblingWithName(parent, candidate))
                {
                    return candidate;
                }
            }

            return primary + "_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private GameObject GenerateUIPrefab(ArtboardData artboard, string jsonPath)
        {
            _defaultSpritesFolderPaths = null;

            // 创建 Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
/*             CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // 配置 CanvasScaler - 全屏适配
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.CanvasResolution;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; */

            // 确保 Canvas 的 RectTransform 和 Scale 正确设置
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                canvasRect = canvasObj.AddComponent<RectTransform>();
            }
            // 参考 CommonPopup.prefab 根节点 Canvas 的 RectTransform 标准
            canvasRect.localScale = Vector3.zero;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.pivot = Vector2.zero;

            // 创建根 GameObject - 全屏填充 Canvas
            GameObject rootObj = new GameObject(artboard.Origin);
            rootObj.transform.SetParent(canvasObj.transform, false);

            RectTransform rootRect = rootObj.AddComponent<RectTransform>();
            // 设置锚点为全屏填充（stretch）
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localRotation = Quaternion.identity;

            // 检查根对象是否继承 BaseAutoUIBind，如果实现了则调用 AutoBind
            MonoBehaviour[] components = rootObj.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is BaseAutoUIBind autoUIBind)
                {
                    // autoUIBind.AutoBind();
                    Debug.Log($"[UIGenerator] AutoBind() called on {rootObj.name} via {component.GetType().Name}");
                    break; // 只调用第一个实现的组件
                }
            }

            // 更新设计分辨率
            config.DesignResolution = new Vector2(artboard.Bounds.Width, artboard.Bounds.Height);

            // 按 index 排序 layers
            List<LayerData> sortedLayers = artboard.Layers.OrderBy(l => l.Index).ToList();

            // 与 PSD 导出一致：设计工具中越靠前越在上层；Unity 需逆序创建子节点才能对齐叠放。
            for (int i = sortedLayers.Count - 1; i >= 0; i--)
            {
                CreateLayerGameObject(sortedLayers[i], rootObj.transform, jsonPath);
            }

            // 步骤 1：按 anchorPosition/rect 覆盖关系整理为树状层级（暂时关闭，按原始平铺结构生成）
            // RebuildHierarchyByRectContainment(rootObj.transform);

            return canvasObj;
        }

        private GameObject CreateLayerGameObject(
            LayerData layer,
            Transform parent,
            string jsonPath,
            Vector2? parentAbsCenter = null,
            bool? visibleOverride = null)
        {
            InteractivityType interactivityType = ClassifyInteractivityFromName(layer?.Name);

            // 移除扩展名
            string nameWithoutExtension = RemoveExtension(layer.Name);

            // 文本：小写；导出已带 _txt 时不重复追加
            string objectName = nameWithoutExtension;
            if (layer.IsTextLayer)
            {
                string lower = nameWithoutExtension.ToLowerInvariant();
                objectName = lower.EndsWith("_txt", System.StringComparison.OrdinalIgnoreCase)
                    ? lower
                    : lower + "_txt";
            }

            GameObject layerObj = new GameObject(objectName);
            layerObj.transform.SetParent(parent, false);
            layerObj.transform.localScale = Vector3.one;

            RectTransform rectTransform = layerObj.AddComponent<RectTransform>();
            Vector2 absPos = ConvertPosition(layer.X, layer.Y, layer.Width, layer.Height);
            Vector2 anchored = parentAbsCenter.HasValue ? absPos - parentAbsCenter.Value : absPos;
            Vector2 size = ConvertSize(layer.Width, layer.Height);

            // 设置锚点与 pivot 到中心；非文字图层转换到 Unity UI 时角度取反，文字图层按导出约定反向处理。
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchored;
            rectTransform.sizeDelta = size;
            float rotation = layer.Rotation.GetValueOrDefault();

            // 根据图层类型创建不同的 UI 组件
            if (layer.IsTextLayer)
            {
                CreateTextLayer(layer, layerObj);
            }
            else if (
                layer.IsShapeLayer
                && (
                    !string.IsNullOrWhiteSpace(layer.Src)
                    || layer.Name.Contains("_pic")
                    || layer.Name.Contains("_img")
                    || interactivityType != InteractivityType.NonInteractive))
            {
                // shapeLayer：有 src 或命名可识别为图片/可交互层时按图片层加载
                CreateImageLayer(layer, layerObj, jsonPath);
            }
            else if (layer.IsShapeLayer)
            {
                CreateShapeLayer(layer, layerObj);
            }
            else if (layer.IsImageLayer)
            {
                CreateImageLayer(layer, layerObj, jsonPath);
            }

            // 图层组件初始化完成后再落地最终角度，避免生成产物丢失 JSON rotation。
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, layer.IsTextLayer ? rotation : -rotation);

            if (visibleOverride.HasValue)
            {
                layerObj.SetActive(visibleOverride.Value);
            }

            return layerObj;
        }

        private void CreateTextLayer(LayerData layer, GameObject obj)
        {
#if TextMeshPro
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.enableAutoSizing = true;

            TextInfoData info = layer.TextInfo;
            if (info != null)
            {
                text.fontSize = info.FontSize > 0f ? info.FontSize : 24f;

                TMP_FontAsset fontAsset = LoadTMPFont(info.FontName);
                if (fontAsset != null)
                {
                    text.font = fontAsset;
                }
                else if (config.DefaultFont != null)
                {
                    text.font = config.DefaultFont;
                }
                else
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }

                FontStyles fontStyle = FontStyles.Normal;
                if (info.Bold && info.Italic)
                {
                    fontStyle = FontStyles.Bold | FontStyles.Italic;
                }
                else if (info.Bold)
                {
                    fontStyle = FontStyles.Bold;
                }
                else if (info.Italic)
                {
                    fontStyle = FontStyles.Italic;
                }

                text.fontStyle = fontStyle;
                text.alignment = TextAlignmentOptions.Midline;
            }
            else
            {
                text.fontSize = 24f;
                if (config.DefaultFont != null)
                {
                    text.font = config.DefaultFont;
                }
                else
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }
                text.alignment = TextAlignmentOptions.Midline;
            }

            text.fontSizeMin = GetAutoSizeMinFontSize(text.fontSize);
            text.fontSizeMax = Mathf.Max(1f, layer.Height);
            text.text = GetDisplayTextFromLayer(layer);
            text.color = ResolveTextColor(info);
            ApplyTextOutline(text, info?.Outline);
#else
            Text text = obj.AddComponent<Text>();
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;

            TextInfoData info = layer.TextInfo;
            if (info != null)
            {
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(info.FontSize > 0f ? info.FontSize : 24f));

                Font legacyFont = LoadLegacyFont(info.FontName);
                if (legacyFont != null)
                {
                    text.font = legacyFont;
                }
                else if (config.DefaultLegacyFont != null)
                {
                    text.font = config.DefaultLegacyFont;
                }
                else
                {
                    text.font = GetDefaultLegacyUIFont();
                }

                if (info.Bold && info.Italic)
                {
                    text.fontStyle = FontStyle.BoldAndItalic;
                }
                else if (info.Bold)
                {
                    text.fontStyle = FontStyle.Bold;
                }
                else if (info.Italic)
                {
                    text.fontStyle = FontStyle.Italic;
                }
                else
                {
                    text.fontStyle = FontStyle.Normal;
                }

                text.alignment = TextAnchor.MiddleCenter;
            }
            else
            {
                text.fontSize = 24;
                if (config.DefaultLegacyFont != null)
                {
                    text.font = config.DefaultLegacyFont;
                }
                else
                {
                    text.font = GetDefaultLegacyUIFont();
                }
                text.alignment = TextAnchor.MiddleCenter;
            }

            text.resizeTextMinSize = GetAutoSizeMinFontSize(text.fontSize);
            text.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(layer.Height));
            text.text = GetDisplayTextFromLayer(layer);
            text.color = ResolveTextColor(info);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
#endif
        }

        private static int GetAutoSizeMinFontSize(float baseFontSize)
        {
            return Mathf.Max(10, Mathf.FloorToInt(baseFontSize * 0.5f));
        }

#if TextMeshPro
        /// <summary>
        /// 为 TMP 文本复用或创建字体材质预设，避免通过 fontMaterial 生成仅存在于当前对象的实例材质。
        /// </summary>
        private void ApplyTextOutline(TextMeshProUGUI text, TextOutlineData outline)
        {
            if (text == null || outline == null || !outline.Enabled || outline.Width <= 0f)
            {
                return;
            }

            if (!TryParseHtmlColor(outline.Color, out Color outlineColor))
            {
                Debug.LogWarning($"[UIGenerator] Text Outline 颜色无效，已忽略: {outline.Color}");
                return;
            }

            TMP_FontAsset fontAsset = text.font;
            Material sourceMaterial = text.fontSharedMaterial;
            if (fontAsset == null || sourceMaterial == null)
            {
                Debug.LogWarning("[UIGenerator] Text Outline 缺少 TMP 字体或基础材质，已保持默认材质。");
                return;
            }

            float outlineWidth = ResolveTmpOutlineWidth(sourceMaterial, outline);
            if (outlineWidth <= 0f)
            {
                return;
            }

            Material outlineMaterial = FindOrCreateOutlineMaterial(
                fontAsset,
                sourceMaterial,
                outlineColor,
                outlineWidth);
            if (outlineMaterial != null)
            {
                text.fontSharedMaterial = outlineMaterial;
            }
        }

        private Material FindOrCreateOutlineMaterial(
            TMP_FontAsset fontAsset,
            Material sourceMaterial,
            Color outlineColor,
            float outlineWidth)
        {
            string fontAssetPath = AssetDatabase.GetAssetPath(fontAsset);
            string fontFolder = Path.GetDirectoryName(fontAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(fontFolder))
            {
                Debug.LogWarning($"[UIGenerator] 无法确定字体 {fontAsset.name} 的资源目录，未创建 Outline 材质。");
                return null;
            }

            Material existing = FindMatchingOutlineMaterial(
                fontFolder,
                sourceMaterial,
                outlineColor,
                outlineWidth);
            if (existing != null)
            {
                return existing;
            }

            if (!fontFolder.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning(
                    $"[UIGenerator] 字体目录不可写，未创建 Outline 材质: {fontFolder}");
                return null;
            }

            string colorHex = ColorUtility.ToHtmlStringRGB(outlineColor).ToUpperInvariant();
            string fontBaseName = fontAsset.name.EndsWith(" SDF", System.StringComparison.OrdinalIgnoreCase)
                ? fontAsset.name.Substring(0, fontAsset.name.Length - 4)
                : fontAsset.name;
            string materialName = $"{fontBaseName} with {colorHex} outline";
            string preferredMaterialPath = $"{fontFolder}/{materialName}.mat";
            Material preferredMaterial = LoadMaterialAsset(preferredMaterialPath);
            if (IsMatchingOutlineMaterial(preferredMaterial, sourceMaterial, outlineColor, outlineWidth))
            {
                CacheOutlineMaterial(preferredMaterial);
                return preferredMaterial;
            }

            string materialPath = preferredMaterial == null
                ? preferredMaterialPath
                : AssetDatabase.GenerateUniqueAssetPath(preferredMaterialPath);

            Material created = new Material(sourceMaterial)
            {
                name = materialName,
                hideFlags = HideFlags.None,
            };
            created.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            created.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            ShaderUtilities.UpdateShaderRatios(created);
            AssetDatabase.CreateAsset(created, materialPath);
            CacheOutlineMaterial(created);
            Debug.Log($"[UIGenerator] 已创建 Text Outline 材质: {materialPath}");
            return created;
        }

        private Material FindMatchingOutlineMaterial(
            string fontFolder,
            Material sourceMaterial,
            Color outlineColor,
            float outlineWidth)
        {
            foreach (Material cachedMaterial in _outlineMaterialCache)
            {
                if (IsMatchingOutlineMaterial(cachedMaterial, sourceMaterial, outlineColor, outlineWidth))
                {
                    return cachedMaterial;
                }
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { fontFolder });
            foreach (string guid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(guid);
                string materialFolder = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
                if (!string.Equals(materialFolder, fontFolder, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (IsMatchingOutlineMaterial(material, sourceMaterial, outlineColor, outlineWidth))
                {
                    CacheOutlineMaterial(material);
                    return material;
                }
            }

            return null;
        }

        private static Material LoadMaterialAsset(string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null && File.Exists(materialPath))
            {
                AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            return material;
        }

        private void CacheOutlineMaterial(Material material)
        {
            if (material != null && !_outlineMaterialCache.Contains(material))
            {
                _outlineMaterialCache.Add(material);
            }
        }

        private static bool IsMatchingOutlineMaterial(
            Material material,
            Material sourceMaterial,
            Color outlineColor,
            float outlineWidth)
        {
            if (material == null
                || sourceMaterial == null
                || material.shader != sourceMaterial.shader
                || !material.HasProperty(ShaderUtilities.ID_OutlineColor)
                || !material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                return false;
            }

            bool sourceHasMainTexture = sourceMaterial.HasProperty(ShaderUtilities.ID_MainTex);
            if (sourceHasMainTexture != material.HasProperty(ShaderUtilities.ID_MainTex))
            {
                return false;
            }

            Texture sourceTexture = sourceHasMainTexture
                ? sourceMaterial.GetTexture(ShaderUtilities.ID_MainTex)
                : null;
            if (sourceHasMainTexture
                && material.GetTexture(ShaderUtilities.ID_MainTex) != sourceTexture)
            {
                return false;
            }

            return ColorsApproximately(material.GetColor(ShaderUtilities.ID_OutlineColor), outlineColor)
                && Mathf.Abs(material.GetFloat(ShaderUtilities.ID_OutlineWidth) - outlineWidth) <= 0.0001f;
        }

        private static float ResolveTmpOutlineWidth(Material sourceMaterial, TextOutlineData outline)
        {
            float width = Mathf.Max(0f, outline.Width);
            if (!string.Equals(outline.WidthUnit, "px", System.StringComparison.OrdinalIgnoreCase))
            {
                return Mathf.Clamp01(width);
            }

            float gradientScale = sourceMaterial.HasProperty(ShaderUtilities.ID_GradientScale)
                ? sourceMaterial.GetFloat(ShaderUtilities.ID_GradientScale)
                : 1f;
            return Mathf.Clamp01(width / Mathf.Max(1f, gradientScale));
        }

        private static bool TryParseHtmlColor(string value, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string html = value.Trim();
            if (html[0] != '#')
            {
                html = "#" + html;
            }

            return ColorUtility.TryParseHtmlString(html, out color);
        }

        private static bool ColorsApproximately(Color a, Color b)
        {
            const float tolerance = 1f / 255f;
            return Mathf.Abs(a.r - b.r) <= tolerance
                && Mathf.Abs(a.g - b.g) <= tolerance
                && Mathf.Abs(a.b - b.b) <= tolerance
                && Mathf.Abs(a.a - b.a) <= tolerance;
        }
#endif

        private static Color ResolveTextColor(TextInfoData info)
        {
            Color c = ParseColor(info?.Color);
            if (info != null && info.Opacity.HasValue)
            {
                float o = Mathf.Clamp(info.Opacity.Value, 0f, 100f);
                c.a *= o / 100f;
            }

            return c;
        }

        private void CreateShapeLayer(LayerData layer, GameObject obj)
        {
            Image image = obj.AddComponent<Image>();
            image.color = ApplyLayerOpacity(config.DefaultShapeColor, layer.Opacity);
        }

        private void CreateImageLayer(LayerData layer, GameObject obj, string jsonPath)
        {
            Image image = obj.AddComponent<Image>();
            Sprite sprite = LoadSprite(layer, jsonPath);
            // 无 src 时：图层名含 _pic 则再按 _img 命名查一次
            if (sprite == null
                && string.IsNullOrWhiteSpace(layer.Src)
                && layer.Name.IndexOf("_pic", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string nameAsImg = layer.Name.Replace("_pic", "_img");
                sprite = LoadSpriteFromLayerName(nameAsImg, jsonPath);
            }
            InteractivityType interactivityType = ClassifyInteractivityFromName(layer.Name);
            bool isButtonLayer = interactivityType == InteractivityType.Button;
            bool isToggleLayer = interactivityType == InteractivityType.Toggle;

            if (sprite != null)
            {
                image.sprite = sprite;
                Debug.Log($"[CreateImageLayer] layer={layer.Name}, sprite={sprite.name}");

                RectTransform rt = obj.GetComponent<RectTransform>();
                Vector2 layoutSize = rt != null ? rt.sizeDelta : new Vector2(layer.Width, layer.Height);
                bool sizeMismatch = SpriteSizeMismatchWithLayout(sprite.rect.size, layoutSize);

                // JSON 布局与 Sprite 原始像素尺寸不一致时，用 Sliced 拉伸九宫（有 border 时效果正确；无 border 时仍按中心区填充）
                if (sizeMismatch)
                {
                    image.type = Image.Type.Sliced;
                }
                else if ((isButtonLayer || isToggleLayer) && HasBorder(sprite))
                {
                    image.type = Image.Type.Sliced;
                }
                else
                {
                    image.type = Image.Type.Simple;
                }
            }
            else
            {
                // 创建占位符
                image.color = config.PlaceholderColor;
                Debug.LogWarning($"Sprite not found for layer: {layer.Name}");
            }

            image.color = ApplyLayerOpacity(image.color, layer.Opacity);

            // 可交互层保持 Raycast；非交互 icon 默认关闭 Raycast
            if (layer.Name.Contains("_icon_") && interactivityType == InteractivityType.NonInteractive)
            {
                image.raycastTarget = false;
            }

            // 如果名称包含 _btn_，自动绑定 CustomButton 及 ClickAudio、ClickScaleAnim
            if (isButtonLayer || isToggleLayer)
            {
                AssembleInteractiveBehaviour(obj, interactivityType, image);
            }
        }

        /// <summary>
        /// 把 JSON 的 0–100 图层不透明度乘到组件现有 Alpha，保留 Sprite 像素 Alpha 与项目颜色预设。
        /// </summary>
        private static Color ApplyLayerOpacity(Color baseColor, float? opacity)
        {
            if (!opacity.HasValue)
            {
                return baseColor;
            }

            baseColor.a *= Mathf.Clamp(opacity.Value, 0f, 100f) / 100f;
            return baseColor;
        }

        private static bool HasBorder(Sprite sprite)
        {
            return sprite != null && sprite.border.sqrMagnitude > 0f;
        }

        /// <summary>
        /// 比较 Sprite 纹理像素尺寸与 UI 布局尺寸（与 JSON 图层宽高一致），允许少量浮点误差。
        /// </summary>
        private static bool SpriteSizeMismatchWithLayout(Vector2 spritePixelSize, Vector2 layoutSize, float tolerance = 1f)
        {
            return Mathf.Abs(spritePixelSize.x - layoutSize.x) > tolerance
                || Mathf.Abs(spritePixelSize.y - layoutSize.y) > tolerance;
        }

        private static System.Type GetTypeFromAssemblies(string typeName)
        {
            System.Type t = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (t != null) return t;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = assembly.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static void BindButtonTriggerIfMissing(GameObject obj, string typeName)
        {
            System.Type triggerType = GetTypeFromAssemblies(typeName);
            if (triggerType == null) return;
            if (obj.GetComponent(triggerType) != null) return;
            obj.AddComponent(triggerType);
        }

        private static void AssembleInteractiveBehaviour(GameObject obj, InteractivityType interactivityType, Graphic targetGraphic)
        {
            if (interactivityType == InteractivityType.Button)
            {
                System.Type customButtonType = GetTypeFromAssemblies("CustomButton");
                if (customButtonType != null)
                {
                    if (obj.GetComponent(customButtonType) == null)
                    {
                        obj.AddComponent(customButtonType);
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to find CustomButton type for object: {obj.name}");
                }

                BindButtonTriggerIfMissing(obj, "ClickAudio");
                BindButtonTriggerIfMissing(obj, "ClickScaleAnim");
                return;
            }

            if (interactivityType == InteractivityType.Toggle)
            {
                Toggle toggle = obj.GetComponent<Toggle>();
                if (toggle == null)
                {
                    toggle = obj.AddComponent<Toggle>();
                }

                if (targetGraphic != null)
                {
                    targetGraphic.raycastTarget = true;
                    toggle.targetGraphic = targetGraphic;
                }
            }
        }

        private Vector2 ConvertPosition(float x, float y, float width, float height)
        {
            // 直接使用原始 JSON 位置信息，不进行缩放
            float unityX = x + width / 2 - config.DesignResolution.x / 2;
            float unityY = -(y + height / 2 - config.DesignResolution.y / 2);
            return new Vector2(unityX, unityY);
        }

        private Vector2 ConvertSize(float width, float height)
        {
            // 直接使用原始 JSON 尺寸信息，不进行缩放
            return new Vector2(width, height);
        }

        /// <summary>将绝对路径或相对路径转为 Unity 可用的 Assets 相对路径（正斜杠）。</summary>
        private static string ToAssetsRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            path = path.Replace('\\', '/').Trim();
            if (path.StartsWith("/")) path = path.Substring(1);
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (path.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
                return "Assets" + path.Substring(dataPath.Length);
            if (!path.StartsWith("Assets/") && !path.Equals("Assets"))
                return path;
            return path;
        }

        /// <summary>
        /// JSON 所在文件夹的 Assets 相对路径，用于将 Prefab 与 JSON 放在同一目录下。
        /// </summary>
        /// <param name="jsonPath">绝对路径或 Assets 相对路径。</param>
        /// <returns>例如 Assets/.../Prefabs；无法解析时返回 null。</returns>
        public static string GetJsonSiblingOutputDirectory(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath))
            {
                return null;
            }

            string assetPath = ToAssetsRelativePath(jsonPath.Replace('\\', '/').Trim());
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            int lastSlash = assetPath.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                return "Assets";
            }

            return assetPath.Substring(0, lastSlash);
        }

        /// <summary>
        /// 收集 Sprite 兜底目录：DefaultSpriteFolder 根目录本身，以及其下递归所有名为 Sprites 的子文件夹（Assets 相对路径）。
        /// </summary>
        private void EnsureDefaultSpritesFolders()
        {
            if (_defaultSpritesFolderPaths != null || string.IsNullOrEmpty(config.DefaultSpriteFolder))
                return;
            string folder = config.DefaultSpriteFolder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets")) return;

            string dataPath = Application.dataPath.Replace('\\', '/');
            string relativeToData = folder.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? folder.Substring(7)
                : folder.Equals("Assets", System.StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : folder;
            string fullBasePath = string.IsNullOrEmpty(relativeToData)
                ? dataPath
                : Path.Combine(dataPath, relativeToData).Replace('\\', '/');

            if (!Directory.Exists(fullBasePath))
                return;

            var paths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            paths.Add(folder);

            foreach (string dir in Directory.GetDirectories(fullBasePath, "*", SearchOption.AllDirectories))
            {
                string dirName = Path.GetFileName(dir);
                if (string.Equals(dirName, "Sprites", System.StringComparison.OrdinalIgnoreCase))
                {
                    string dirNorm = dir.Replace('\\', '/');
                    string relative = dirNorm.Length > dataPath.Length ? dirNorm.Substring(dataPath.Length).TrimStart('/') : "";
                    string assetsPath = "Assets/" + relative;
                    paths.Add(assetsPath);
                }
            }

            _defaultSpritesFolderPaths = paths.ToList();
        }

        /// <summary>从路径加载 Sprite：先按主资源加载，若失败则从该路径下所有子资源中按名称匹配（图集/多图模式）。</summary>
        private static Sprite LoadSpriteAtPath(string spritePath, string nameWithoutExt)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null) return sprite;
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            if (all == null) return null;
            foreach (Object o in all)
            {
                if (o is Sprite s && string.Equals(s.name, nameWithoutExt, System.StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            foreach (Object o in all)
            {
                if (o is Sprite s) return s;
            }
            return null;
        }

        private static string ToFullPathFromAssetsFolder(string assetsFolder)
        {
            if (string.IsNullOrEmpty(assetsFolder))
            {
                return null;
            }

            string folder = assetsFolder.Replace('\\', '/').TrimEnd('/');
            if (folder.Equals("Assets", System.StringComparison.OrdinalIgnoreCase))
            {
                return Application.dataPath.Replace('\\', '/');
            }

            if (!folder.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.Combine(Application.dataPath, folder.Substring(7)).Replace('\\', '/');
        }

        private static Sprite LoadSpriteFromFolderRecursive(string assetsFolder, string fileName, string nameWithoutExt)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string fullFolder = ToFullPathFromAssetsFolder(assetsFolder);
            if (string.IsNullOrEmpty(fullFolder) || !Directory.Exists(fullFolder))
            {
                return null;
            }

            foreach (string fullPath in Directory.GetFiles(fullFolder, fileName, SearchOption.AllDirectories))
            {
                string assetPath = ToAssetsRelativePath(fullPath.Replace('\\', '/'));
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Sprite sprite = LoadSpriteAtPath(assetPath, nameWithoutExt);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// 有 Src 时仅按 src 解析（与 JSON 同目录及 DefaultSpriteFolder 兜底）；否则按图层名 + 扩展名走旧逻辑。
        /// </summary>
        private Sprite LoadSprite(LayerData layer, string jsonPath)
        {
            if (layer == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(layer.Src))
            {
                return LoadSpriteFromSrc(layer.Src, jsonPath);
            }

            return LoadSpriteFromLayerName(layer.Name, jsonPath);
        }

        /// <summary>按导出 src 字段相对 JSON 目录找贴图；文件名用于子资源匹配。</summary>
        private Sprite LoadSpriteFromSrc(string srcRaw, string jsonPath)
        {
            string jsonDir = Path.GetDirectoryName(jsonPath) ?? "";
            string dataPathNorm = Application.dataPath.Replace('\\', '/');
            string srcTrim = srcRaw.Trim().Replace('\\', '/');
            string fileNameOnly = Path.GetFileName(srcTrim);
            string nameWithoutExt = RemoveExtension(fileNameOnly);

            if (srcTrim.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                Sprite direct = LoadSpriteAtPath(srcTrim, nameWithoutExt);
                if (direct != null)
                {
                    return direct;
                }
            }

            var relativeAttempts = new List<string>();
            if (!string.IsNullOrEmpty(srcTrim))
            {
                relativeAttempts.Add(srcTrim);
            }

            if (!string.IsNullOrEmpty(fileNameOnly)
                && !string.Equals(fileNameOnly, srcTrim, System.StringComparison.OrdinalIgnoreCase))
            {
                relativeAttempts.Add(fileNameOnly);
            }

            Sprite tryPath(string relativeFragment)
            {
                if (string.IsNullOrEmpty(relativeFragment))
                {
                    return null;
                }

                string combined = Path.Combine(jsonDir, relativeFragment).Replace('\\', '/');
                if (!Path.IsPathRooted(combined) && !combined.StartsWith("Assets", System.StringComparison.OrdinalIgnoreCase))
                {
                    combined = Path.Combine(dataPathNorm, combined).Replace('\\', '/');
                }

                string assetPath = ToAssetsRelativePath(combined);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets"))
                {
                    return null;
                }

                return LoadSpriteAtPath(assetPath, nameWithoutExt);
            }

            foreach (string rel in relativeAttempts.Distinct(System.StringComparer.OrdinalIgnoreCase))
            {
                Sprite s = tryPath(rel);
                if (s != null)
                {
                    return s;
                }
            }

            EnsureDefaultSpritesFolders();
            if (_defaultSpritesFolderPaths != null && _defaultSpritesFolderPaths.Count > 0)
            {
                foreach (string spritesFolder in _defaultSpritesFolderPaths)
                {
                    string root = spritesFolder.TrimEnd('/');
                    foreach (string rel in relativeAttempts.Distinct(System.StringComparer.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(rel))
                        {
                            continue;
                        }

                        string spritePath = (root + "/" + rel).Replace('\\', '/');
                        Sprite s = LoadSpriteAtPath(spritePath, nameWithoutExt);
                        if (s != null)
                        {
                            return s;
                        }

                        s = LoadSpriteFromFolderRecursive(root, Path.GetFileName(rel), nameWithoutExt);
                        if (s != null)
                        {
                            return s;
                        }
                    }
                }
            }

            Debug.LogWarning(
                $"[UIGenerator] Sprite 未找到 (src): \"{srcTrim}\", JSON 目录=\"{jsonDir}\"");
            return null;
        }

        /// <summary>旧版：按图层名尝试常见图片扩展名，先 JSON 同目录再 DefaultSpriteFolder。</summary>
        private Sprite LoadSpriteFromLayerName(string layerName, string jsonPath)
        {
            string jsonDir = Path.GetDirectoryName(jsonPath) ?? "";
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            string nameWithoutExt = RemoveExtension(layerName);

            foreach (string ext in extensions)
            {
                string spritePath = Path.Combine(jsonDir, layerName + ext).Replace('\\', '/');
                if (!Path.IsPathRooted(spritePath) && !spritePath.StartsWith("Assets"))
                {
                    spritePath = Path.Combine(Application.dataPath, spritePath).Replace('\\', '/');
                }

                spritePath = ToAssetsRelativePath(spritePath);
                if (string.IsNullOrEmpty(spritePath) || !spritePath.StartsWith("Assets"))
                {
                    continue;
                }

                var sprite = LoadSpriteAtPath(spritePath, nameWithoutExt);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            if (string.IsNullOrEmpty(nameWithoutExt))
            {
                return null;
            }

            EnsureDefaultSpritesFolders();
            if (_defaultSpritesFolderPaths == null || _defaultSpritesFolderPaths.Count == 0)
            {
                return null;
            }

            foreach (string spritesFolder in _defaultSpritesFolderPaths)
            {
                foreach (string ext in extensions)
                {
                    string spritePath = (spritesFolder.TrimEnd('/') + "/" + nameWithoutExt + ext).Replace('\\', '/');
                    var sprite = LoadSpriteAtPath(spritePath, nameWithoutExt);
                    if (sprite != null)
                    {
                        return sprite;
                    }

                    sprite = LoadSpriteFromFolderRecursive(spritesFolder, nameWithoutExt + ext, nameWithoutExt);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            string searchedFolders = _defaultSpritesFolderPaths != null && _defaultSpritesFolderPaths.Count > 0
                ? string.Join(", ", _defaultSpritesFolderPaths)
                : "(无 DefaultSpriteFolder 或未找到 Sprites 子文件夹)";
            Debug.LogWarning(
                $"[UIGenerator] Sprite 未找到: 图层名=\"{layerName}\", 无扩展名=\"{nameWithoutExt}\", JSON 目录=\"{jsonDir}\", 已遍历=[{searchedFolders}]");
            return null;
        }

#if TextMeshPro
        private TMP_FontAsset LoadTMPFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset != null && (fontAsset.name == fontName || fontAsset.name.Contains(fontName)))
                {
                    return fontAsset;
                }
            }

            return null;
        }
#else
        private static Font LoadLegacyFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets("t:Font");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font != null && (font.name == fontName || font.name.Contains(fontName)))
                {
                    return font;
                }
            }

            return null;
        }

        private static Font GetDefaultLegacyUIFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null)
            {
                return f;
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
#endif

        private string RemoveExtension(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // 移除常见的图片扩展名
            string[] extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".psd" };
            string lowerName = name.ToLower();
            
            foreach (string ext in extensions)
            {
                if (lowerName.EndsWith(ext))
                {
                    return name.Substring(0, name.Length - ext.Length);
                }
            }

            return name;
        }

        private static Color ParseColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
            {
                return Color.white;
            }

            hexColor = hexColor.Trim().TrimStart('#');
            if (hexColor.Length == 6)
            {
                if (!int.TryParse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) ||
                    !int.TryParse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) ||
                    !int.TryParse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
                {
                    return Color.white;
                }

                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }

            if (hexColor.Length == 8)
            {
                if (!int.TryParse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) ||
                    !int.TryParse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) ||
                    !int.TryParse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b) ||
                    !int.TryParse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out int a))
                {
                    return Color.white;
                }

                return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
            }

            return Color.white;
        }

        /// <summary>
        /// 按 rect 覆盖关系将根节点下的平铺子节点整理为树状结构：被某节点 rect 完全包含的节点成为其子节点，父节点取包含它的最小 rect。
        /// </summary>
        private static void RebuildHierarchyByRectContainment(Transform root)
        {
            if (root == null) return;

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform t = root.GetChild(i);
                if (t.GetComponent<RectTransform>() != null)
                    children.Add(t);
            }

            if (children.Count <= 1) return;

            const float tolerance = 1f;

            // 在 root 本地空间中的 rect（与 RectTransform pivot 0.5 一致）
            static Rect GetRectInRootSpace(Transform node, Transform rootSpace)
            {
                RectTransform rt = node.GetComponent<RectTransform>();
                if (rt == null) return default;
                Vector2 pos = rt.anchoredPosition;
                Vector2 size = rt.sizeDelta;
                return new Rect(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f, size.x, size.y);
            }

            static bool ContainsRect(Rect outer, Rect inner, float tol)
            {
                return outer.xMin <= inner.xMin + tol && outer.yMin <= inner.yMin + tol
                    && outer.xMax >= inner.xMax - tol && outer.yMax >= inner.yMax - tol;
            }

            static float RectArea(Rect r) => r.width * r.height;

            var rects = new Dictionary<Transform, Rect>();
            foreach (Transform t in children)
                rects[t] = GetRectInRootSpace(t, root);

            // 为每个节点选父节点：包含它的、面积最小的节点（不能是自己）
            var parentChoice = new Dictionary<Transform, Transform>();
            foreach (Transform node in children)
            {
                Rect r = rects[node];
                Transform bestParent = null;
                float bestArea = float.MaxValue;
                foreach (Transform other in children)
                {
                    if (other == node) continue;
                    Rect otherRect = rects[other];
                    if (!ContainsRect(otherRect, r, tolerance)) continue;
                    float area = RectArea(otherRect);
                    if (area < bestArea)
                    {
                        bestArea = area;
                        bestParent = other;
                    }
                }
                parentChoice[node] = bestParent;
            }

            // 按面积从大到小 reparent，避免父节点尚未就位
            var byArea = children.OrderByDescending(t => RectArea(rects[t])).ToList();
            foreach (Transform node in byArea)
            {
                Transform newParent = parentChoice[node];
                Transform targetParent = newParent != null ? newParent : root;
                if (node.parent != targetParent)
                    node.SetParent(targetParent, true);
            }
        }
    }
}
#endif

