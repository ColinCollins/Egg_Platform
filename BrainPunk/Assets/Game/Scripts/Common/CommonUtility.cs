using UnityEngine;

namespace Game.Scripts.Common
{
    public static class CommonUtility
    {
        /// <summary>
        /// 将 UI 元素位置转换为场景世界位置
        /// </summary>
        /// <param name="targetUI">目标 UI 元素的 RectTransform</param>
        /// <param name="mainCamera">主摄像机（用于将屏幕坐标转换为世界坐标）</param>
        /// <param name="targetZPosition">目标在世界空间中的 Z 轴位置</param>
        /// <returns>转换后的世界坐标</returns>
        public static Vector3 ConvertUIToWorldPosition(RectTransform targetUI, Camera mainCamera, float targetZPosition = 0f)
        {
            if (targetUI == null || mainCamera == null)
            {
                Debug.LogWarning("[CommonUtility] targetUI or mainCamera is null");
                return Vector3.zero;
            }

            Canvas canvas = targetUI.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[CommonUtility] Canvas not found in parent of targetUI");
                return Vector3.zero;
            }

            Vector2 screenPoint;
            Camera uiCamera = null;

            if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                uiCamera = canvas.worldCamera;
            }

            // 根据 Canvas 渲染模式获取屏幕坐标
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Screen Space - Overlay: 直接使用 RectTransform 的世界位置转换为屏幕坐标
                screenPoint = RectTransformUtility.WorldToScreenPoint(null, targetUI.position);
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
            {
                // Screen Space - Camera: 使用 UI 相机转换
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, targetUI.position);
            }
            else if (canvas.renderMode == RenderMode.WorldSpace)
            {
                // World Space: 已经是世界坐标，直接返回调整 Z 轴后的位置
                return new Vector3(targetUI.position.x, targetUI.position.y, targetZPosition);
            }
            else
            {
                // 默认情况，尝试使用 UI 相机或主相机
                screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera ?? mainCamera, targetUI.position);
            }

            // 将屏幕坐标转换为场景世界坐标
            // 计算物体在主相机前方的实际距离，以便投影到正确的平面
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - targetZPosition);
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));

            // 强制设置 Z 轴位置
            worldPos.z = targetZPosition;

            return worldPos;
        }


        /// <summary>
        /// 是否是 string.Format 支持格式
        /// </summary>
        /// <returns></returns>
        public static bool IsValidStrFormat(this string str)
        {
            return str.Contains("{") || str.Contains("}");
        }
    }

}