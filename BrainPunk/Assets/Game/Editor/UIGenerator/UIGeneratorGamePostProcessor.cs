#if UNITY_EDITOR
using Game.Editor.UIGenerator;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// UIGenerator 保存 Prefab 后的项目定制后处理（通过 <see cref="IUIGeneratorPostProcessor"/> 自动接入管线）。
    /// <paramref name="prefabRoot"/> 为生成流程返回的根物体，当前实现一般为带 <see cref="Canvas"/> 的节点。
    /// </summary>
    public sealed class UIGeneratorGamePostProcessor : IUIGeneratorPostProcessor
    {
        /// <summary>数值越小越先执行；若再加其它 Processor，可用 100、200 排队。</summary>
        public int Order => 0;

        /// <summary>生成 UI 根 Canvas 的 <see cref="Canvas.sortingOrder"/>，用于对齐现有弹窗 prefab 的默认层级。</summary>
        private const int DefaultCanvasSortingOrder = 301;

        /// <summary>UI 资源默认 Layer 名称；不存在时保持原 Layer，避免生成流程因项目 Layer 配置缺失中断。</summary>
        private const string DefaultUILayerName = "UI";

        public void OnPostProcessPrefab(GameObject prefabRoot, string prefabAssetPath, string sourceJsonPath)
        {
            if (prefabRoot == null)
            {
                return;
            }

            EnsureCanvasUtilityComponents(prefabRoot);
            EnsureClickVibrationOnCustomButtons(prefabRoot);
            UIGeneratorUIViewScriptGenerator.GenerateFromPrefabHierarchy(prefabRoot, prefabAssetPath);

            // 在此追加：替换字体、统一 Sorting、挂项目基类、按 JSON 名打 Tag 等。
            // 可仅用 prefabAssetPath / sourceJsonPath 做规则判断。
            ApplyProjectRules(prefabRoot, prefabAssetPath, sourceJsonPath);
        }

        private static void EnsureCanvasUtilityComponents(GameObject prefabRoot)
        {
            Canvas canvas = prefabRoot.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = prefabRoot.GetComponentInChildren<Canvas>(true);
            }

            if (canvas == null)
            {
                Debug.LogWarning($"[UIGeneratorGamePostProcessor] No Canvas under '{prefabRoot.name}', skip canvas setup.");
                return;
            }

            ApplyDefaultCanvasState(canvas);
        }

        /// <summary>
        /// 与生成器内对 _btn 绑定 <see cref="ClickAudio"/> / <see cref="ClickScaleAnim"/> 一致，补上 <see cref="ClickVibration"/>（避免改 Bear 包源码）。
        /// </summary>
        private static void EnsureClickVibrationOnCustomButtons(GameObject prefabRoot)
        {
            CustomButton[] buttons = prefabRoot.GetComponentsInChildren<CustomButton>(true);
            foreach (CustomButton btn in buttons)
            {
                if (btn == null)
                {
                    continue;
                }

                if (btn.GetComponent<ClickVibration>() != null)
                {
                    continue;
                }

                btn.gameObject.AddComponent<ClickVibration>();
            }
        }

        private static void ApplyProjectRules(GameObject prefabRoot, string prefabAssetPath, string sourceJsonPath)
        {
            // 示例：按 sourceJsonPath Contains("Shop") 挂脚本、改 Canvas.sortingOrder 等。
        }

        private static void ApplyDefaultCanvasState(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            GameObject canvasGo = canvas.gameObject;
            int uiLayer = LayerMask.NameToLayer(DefaultUILayerName);
            if (uiLayer >= 0)
            {
                canvasGo.layer = uiLayer;
            }

            RectTransform rectTransform = canvasGo.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.one;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            // Canvas：对齐现有弹窗根节点默认展示状态，生成后可由具体 prefab 再按需覆盖。
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            canvas.pixelPerfect = false;
            canvas.overrideSorting = true;
            canvas.sortingOrder = DefaultCanvasSortingOrder;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;
            canvas.vertexColorAlwaysGammaSpace = true;

            CanvasRenderer canvasRenderer = canvasGo.GetComponent<CanvasRenderer>();
            if (canvasRenderer == null)
            {
                canvasGo.AddComponent<CanvasRenderer>();
            }

            // uGUI 射线检测（点击/拖拽）
            var raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvasGo.AddComponent<GraphicRaycaster>();
            }

            raycaster.ignoreReversedGraphics = true;
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.TwoD;
        }
    }
}
#endif
