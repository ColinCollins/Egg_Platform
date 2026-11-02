using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Game.Scripts.Common
{
    public static class InputUtils
    {
        /// <summary>
        /// 检查当前点击或触摸是否在 UI 上
        /// </summary>
        /// <returns>如果在 UI 上返回 true，否则返回 false</returns>
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            // 如果是移动端且有触摸，取第一个触摸点
            if (Input.touchCount > 0)
            {
                return IsPointerOverUI(Input.GetTouch(0).position);
            }

            // 否则检查鼠标位置
            return IsPointerOverUI(Input.mousePosition);
        }

        /// <summary>
        /// 检查特定屏幕坐标是否在 UI 上
        /// </summary>
        public static bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;
            
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            
            // 只有当射线命中的物体层级是 UI 时才拦截
            // 或者简单点，只要命中了任何 Raycast Target 就算
            return results.Count > 0;
        }

        /// <summary>
        /// 兼容旧接口：检查特定手指 ID 是否在 UI 上
        /// </summary>
        public static bool IsPointerOverUI(int fingerId)
        {
            if (EventSystem.current == null) return false;
            
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.fingerId == fingerId)
                {
                    return IsPointerOverUI(t.position);
                }
            }
            
            return EventSystem.current.IsPointerOverGameObject(fingerId);
        }
    }
}
