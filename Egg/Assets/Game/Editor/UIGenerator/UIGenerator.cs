#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace Game.Editor.UIGenerator
{
    public class UIGenerator
    {
        private UIGeneratorConfig config;

        public UIGenerator(UIGeneratorConfig config = null)
        {
            this.config = config ?? new UIGeneratorConfig();
        }

        public GameObject GenerateUIPrefab(string jsonPath)
        {
            // 统一路径格式，确保跨平台兼容（Unity 使用正斜杠）
            jsonPath = jsonPath.Replace('\\', '/');
            
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"JSON file not found: {jsonPath}");
                return null;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            List<ArtboardData> artboards = JsonConvert.DeserializeObject<List<ArtboardData>>(jsonContent);

            if (artboards == null || artboards.Count == 0)
            {
                Debug.LogError("No artboard data found in JSON");
                return null;
            }

            ArtboardData artboard = artboards[0];
            return GenerateUIPrefab(artboard, jsonPath);
        }

        private GameObject GenerateUIPrefab(ArtboardData artboard, string jsonPath)
        {
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
            canvasRect.localScale = Vector3.one;
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.anchoredPosition = Vector2.zero;

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

            // 检查根对象是否实现了 IAutoUIBind 接口，如果实现了则调用 AutoBind
            MonoBehaviour[] components = rootObj.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                if (component is IAutoUIBind autoUIBind)
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

            // 创建图层
            foreach (LayerData layer in sortedLayers)
            {
                CreateLayerGameObject(layer, rootObj.transform, jsonPath);
            }

            return canvasObj;
        }

        private void CreateLayerGameObject(LayerData layer, Transform parent, string jsonPath)
        {
            // TextMeshPro 名称处理：改为小写并添加 _txt 后缀
            string objectName = layer.Name;
            if (layer.IsTextLayer)
            {
                objectName = layer.Name.ToLower() + "_txt";
            }

            GameObject layerObj = new GameObject(objectName);
            layerObj.transform.SetParent(parent, false);
            layerObj.transform.localScale = Vector3.one;

            RectTransform rectTransform = layerObj.AddComponent<RectTransform>();
            Vector2 position = ConvertPosition(layer.X, layer.Y, layer.Width, layer.Height);
            Vector2 size = ConvertSize(layer.Width, layer.Height);

            // 设置锚点到中心
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            // 根据图层类型创建不同的 UI 组件
            if (layer.IsTextLayer)
            {
                CreateTextLayer(layer, layerObj);
            }
            else if (layer.IsShapeLayer)
            {
                CreateShapeLayer(layer, layerObj);
            }
            else if (layer.IsImageLayer)
            {
                CreateImageLayer(layer, layerObj, jsonPath);
            }
        }

        private void CreateTextLayer(LayerData layer, GameObject obj)
        {
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            
            if (layer.TextInfo != null)
            {
                text.text = layer.TextInfo.Content;
                text.fontSize = layer.TextInfo.FontSize * config.ScaleFactor;
                text.color = ParseColor(layer.TextInfo.Color);
                
                // 尝试加载 TextMeshPro 字体资源
                TMP_FontAsset fontAsset = LoadTMPFont(layer.TextInfo.FontName);
                if (fontAsset != null)
                {
                    text.font = fontAsset;
                }
                else
                {
                    // 使用默认字体
                    text.font = TMP_Settings.defaultFontAsset;
                }

                // 设置字体样式
                FontStyles fontStyle = FontStyles.Normal;
                if (layer.TextInfo.Bold && layer.TextInfo.Italic)
                    fontStyle = FontStyles.Bold | FontStyles.Italic;
                else if (layer.TextInfo.Bold)
                    fontStyle = FontStyles.Bold;
                else if (layer.TextInfo.Italic)
                    fontStyle = FontStyles.Italic;
                
                text.fontStyle = fontStyle;

                // 设置对齐方式（居中）
                text.alignment = TextAlignmentOptions.Midline;
            }
        }

        private void CreateShapeLayer(LayerData layer, GameObject obj)
        {
            Image image = obj.AddComponent<Image>();
            image.color = config.DefaultShapeColor;
        }

        private void CreateImageLayer(LayerData layer, GameObject obj, string jsonPath)
        {
            Image image = obj.AddComponent<Image>();
            Sprite sprite = LoadSprite(layer.Name, jsonPath);

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
            }
            else
            {
                // 创建占位符
                image.color = config.PlaceholderColor;
                Debug.LogWarning($"Sprite not found for layer: {layer.Name}");
            }

            // 如果名称包含 _icon_，关闭 Raycast 功能
            if (layer.Name.Contains("_icon_"))
            {
                image.raycastTarget = false;
            }

            // 如果名称包含 _btn_，自动绑定 CustomButton 脚本
            if (layer.Name.Contains("_btn_"))
            {
                // 使用反射查找 CustomButton 类型（因为它在全局命名空间）
                System.Type customButtonType = System.Type.GetType("CustomButton, Assembly-CSharp");
                if (customButtonType == null)
                {
                    // 尝试从所有程序集中查找
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        customButtonType = assembly.GetType("CustomButton");
                        if (customButtonType != null)
                            break;
                    }
                }

                if (customButtonType != null)
                {
                    obj.AddComponent(customButtonType);
                }
                else
                {
                    Debug.LogWarning($"Failed to find CustomButton type for layer: {layer.Name}");
                }
            }
        }

        private Vector2 ConvertPosition(float x, float y, float width, float height)
        {
            float scale = config.ScaleFactor;
            float unityX = (x + width / 2 - config.DesignResolution.x / 2) * scale;
            float unityY = -(y + height / 2 - config.DesignResolution.y / 2) * scale;
            return new Vector2(unityX, unityY);
        }

        private Vector2 ConvertSize(float width, float height)
        {
            float scale = config.ScaleFactor;
            return new Vector2(width * scale, height * scale);
        }

        private Sprite LoadSprite(string layerName, string jsonPath)
        {
            string jsonDirectory = Path.GetDirectoryName(jsonPath);
            string[] extensions = { ".png", ".jpg", ".jpeg" };

            foreach (string ext in extensions)
            {
                string spritePath = Path.Combine(jsonDirectory, layerName + ext);
                
                // 统一转换为 Unity 资源路径格式（使用正斜杠）
                spritePath = spritePath.Replace('\\', '/');
                
                // 转换为相对于 Assets 的路径
                // 统一路径格式，确保跨平台兼容
                string dataPath = Application.dataPath.Replace('\\', '/');
                if (spritePath.StartsWith(dataPath))
                {
                    spritePath = "Assets" + spritePath.Substring(dataPath.Length);
                }
                else if (!spritePath.StartsWith("Assets"))
                {
                    // 如果路径不是以 Assets 开头，尝试转换为相对路径
                    string normalizedPath = spritePath.Replace('\\', '/');
                    if (normalizedPath.StartsWith("/"))
                    {
                        normalizedPath = normalizedPath.Substring(1);
                    }
                    spritePath = normalizedPath;
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private TMP_FontAsset LoadTMPFont(string fontName)
        {
            if (string.IsNullOrEmpty(fontName))
                return null;

            // 搜索所有 TextMeshPro 字体资源
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

        private Color ParseColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
                return Color.white;

            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            if (hexColor.Length == 6)
            {
                int r = int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                int g = int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                int b = int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return new Color(r / 255f, g / 255f, b / 255f, 1f);
            }

            return Color.white;
        }
    }
}
#endif

