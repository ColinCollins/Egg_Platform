using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bear.UI
{
    /// <summary>
    /// UI 层级管理器
    /// </summary>
    public class UILayerManager
    {
        private Dictionary<UILayer, Canvas> _layerCanvases;
        private Dictionary<UILayer, Transform> _layerRoots;
        private Transform _uiRoot;

        private Vector2 defaultResolution;
        private CanvasScaler.ScreenMatchMode screenMatchMode;
        private float matchWidthOrHeight;

        public UILayerManager(Transform uiRoot, Vector2 resolution, CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, float matchWidthOrHeight = 0.5f)
        {
            _uiRoot = uiRoot;
            _layerCanvases = new Dictionary<UILayer, Canvas>();
            _layerRoots = new Dictionary<UILayer, Transform>();
            defaultResolution = resolution;
            this.screenMatchMode = screenMatchMode;
            this.matchWidthOrHeight = matchWidthOrHeight;
            InitializeLayers();
        }

        private void InitializeLayers()
        {
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                CreateLayer(layer);
            }
        }

        private void CreateLayer(UILayer layer)
        {
            GameObject layerGo = new GameObject(layer.ToString());
            layerGo.transform.SetParent(_uiRoot, false);

            Canvas canvas = layerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)layer * 100;

            CanvasScaler scaler = layerGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = defaultResolution;
            scaler.screenMatchMode = screenMatchMode;
            scaler.matchWidthOrHeight = matchWidthOrHeight;

            layerGo.AddComponent<GraphicRaycaster>();

            _layerCanvases[layer] = canvas;
            _layerRoots[layer] = layerGo.transform;
        }

        /// <summary>
        /// 获取指定层级的根节点
        /// </summary>
        /// <param name="layer">层级</param>
        /// <returns>根节点 Transform</returns>
        public Transform GetLayerRoot(UILayer layer)
        {
            _layerRoots.TryGetValue(layer, out Transform root);
            return root;
        }

        /// <summary>
        /// 获取指定层级的 Canvas
        /// </summary>
        /// <param name="layer">层级</param>
        /// <returns>Canvas 组件</returns>
        public Canvas GetLayerCanvas(UILayer layer)
        {
            _layerCanvases.TryGetValue(layer, out Canvas canvas);
            return canvas;
        }
    }
}

