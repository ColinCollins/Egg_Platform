using System;
using UnityEngine;

namespace GF
{
    /// <summary>
    /// 解决层级问题
    /// </summary>
    public class SortingOrderHelper : MonoBehaviour
    {
        //原始层级
        private int _originCanvasSortingOrder;
        private int _originParticleRendererSortingOrder;
        
        private Canvas _canvas;
        private Renderer _particleSystemRenderer;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            _canvas = GetComponent<Canvas>();
            if (_canvas != null)
            {
                _canvas.overrideSorting = true;
                _originCanvasSortingOrder = _canvas.sortingOrder;
            }

            _particleSystemRenderer = GetComponent<Renderer>();
            if (_particleSystemRenderer != null)
            {
                _originParticleRendererSortingOrder = _particleSystemRenderer.sortingOrder;
            }
        }

        /// <summary>
        /// 设置层级
        /// </summary>
        /// <param name="uiBase"></param>
        public void SetSortingOrder(UIBase uiBase)
        {
            Canvas baseCanvas = uiBase.GetComponent<Canvas>();
            if (baseCanvas == null)
            {
                return;
            }
            
            int baseCanvasSortingOrder = baseCanvas.sortingOrder;
            
            // if (_canvas)
            if (_canvas != null && baseCanvas != _canvas)
            {
                _canvas.sortingLayerName = baseCanvas.sortingLayerName;
                _canvas.sortingOrder = _originCanvasSortingOrder + baseCanvasSortingOrder;

                // Debug.Log($"<color=#88ff00> [Sorting Order Helper] baseCanvas: [{baseCanvas.gameObject.name}] :: baseCanvasOrder: {baseCanvasSortingOrder} + originCanvasOrder: {_originCanvasSortingOrder} = {_canvas.sortingOrder}</color>");
            }

            if (_particleSystemRenderer)
            {
                _particleSystemRenderer.sortingLayerName = baseCanvas.sortingLayerName;
                _particleSystemRenderer.sortingOrder = _originParticleRendererSortingOrder + baseCanvasSortingOrder;
            }
        }
    }
}