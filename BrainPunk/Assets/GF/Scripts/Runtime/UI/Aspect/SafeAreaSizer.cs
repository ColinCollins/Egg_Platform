using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public class SafeAreaSizer : MonoBehaviour
    {
        private string _tag = "[SafeAreaSizer]";
        private static GameObject _uiRoot;
        public static GameObject UIRoot
        {
            get
            {
                if (_uiRoot == null)
                {
                    _uiRoot = GameObject.Find("UIRoot");
                }
                return _uiRoot;
            }
        }
        private float offset = 0;
        public float TopVal { set; get; }
        public float BottomVal { set; get; }
        public float Padding { set; get; }
        // Start is called before the first frame update
        void Awake() 
        {
            offset = 0;
            
            float screenHeight = Screen.height;
            float screenWidth = Screen.width;

            float top = Screen.height - Screen.safeArea.height - Screen.safeArea.y - offset;
            float bottom = Screen.safeArea.y;

            TopVal = top;
            BottomVal = bottom;
            LogKit.I($"{_tag} 原始值 TopVal: {TopVal}, BottomVal: {BottomVal}");
            Rect rect = UIRoot.GetComponent<RectTransform>().rect;
            TopVal = rect.height * top / screenHeight;
            BottomVal = rect.height * bottom / screenHeight;
            LogKit.I($"{_tag} 纠正值 TopVal: {TopVal}, BottomVal: {BottomVal}");
                //平板
            bool? isTablet = App.UI?.IsTablet?.Invoke();
            if (isTablet is true)
                {
                Padding = CalTabletPadding(rect.width);
            }
            

            // transform.GetComponent<RectTransform>().offsetMin = new Vector2(0.0f, bottom);
            // transform.GetComponent<RectTransform>().offsetMax = new Vector2(0.0f, -top);

            AdaptComp[] adaptComps = GetComponentsInChildren<AdaptComp>();
            if (adaptComps != null && adaptComps.Length > 0)
            {
                foreach (AdaptComp adaptComp in adaptComps)
                {
                    adaptComp.Adapt(TopVal, BottomVal, Padding, Padding);
                }
            }
        }
        public static float CalTabletPadding(float width)
        {
            return width * (1 - 0.846f) / 2f;
        }
        public float GetTopVal()
        {
            return TopVal;
        }
        public float GetBottomVal()
        {
            return BottomVal;
        }
    }
}