using System;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public class ReferenceResolution : MonoBehaviour
    {
        [SerializeField]
        private float designWidth = 1080f;
        [SerializeField]
        private float designHeight = 1920f;

        void Awake()
        {
            float adjustor = 0f; //屏幕矫正比例
            //获取设备宽高
            float device_width = Screen.width; //当前设备宽度
            float device_height = Screen.height; //当前设备高度
            //计算宽高比例
            float widthScale = device_width / designWidth;
            float heightScale = device_height / designHeight;

            adjustor = Math.Abs(Mathf.Min(widthScale, heightScale) - widthScale) < 0.23f ? 0 : 1;

            CanvasScaler canvasScalerTemp = transform.GetComponent<CanvasScaler>();
            canvasScalerTemp.matchWidthOrHeight = adjustor;
        }
    }
}
