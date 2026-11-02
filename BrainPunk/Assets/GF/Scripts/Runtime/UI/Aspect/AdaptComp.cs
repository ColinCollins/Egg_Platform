using System;
using UnityEngine;

namespace GF
{
    public class AdaptComp: MonoBehaviour
    {
        public enum AdaptType
        {
            Up,     // 只适配上面刘海屏，穿孔屏
            Down,   // 只适配下面的bar，异形屏
            All,
            None
        }

        public bool adaptPad = false;
        public AdaptType type;

        public void Adapt(float top, float bottom, float left, float right)
        {
            if (type == AdaptType.All || type == AdaptType.Up)
            {
                transform.GetComponent<RectTransform>().offsetMax = new Vector2(0.0f, -top);
            }

            if (type == AdaptType.All || type == AdaptType.Down)
            {
                transform.GetComponent<RectTransform>().offsetMin = new Vector2(0.0f, bottom);
            }
            if (adaptPad)
            {
                var oldMin = transform.GetComponent<RectTransform>().offsetMin;
                transform.GetComponent<RectTransform>().offsetMin = new Vector2(left, oldMin.y);
                var oldMax = transform.GetComponent<RectTransform>().offsetMax;
                transform.GetComponent<RectTransform>().offsetMax = new Vector2(-right, oldMax.y);
            }
        }
    }
}