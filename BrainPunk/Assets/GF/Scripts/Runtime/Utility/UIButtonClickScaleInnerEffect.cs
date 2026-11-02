using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GF
{
    [DisallowMultipleComponent]
    public class UIButtonClickScaleInnerEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {        
        [SerializeField]
        [Range(0.1f, 2.0f)]
        [Tooltip("按下时的缩放比例")]
        private float pressScale = 0.96f;

        private void Start()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Transform inner = transform.Find("inner");
            if (inner)
            {
                inner.localScale = Vector3.one * pressScale;
            }
        }
 
        public void OnPointerUp(PointerEventData eventData)
        {
            Transform inner = transform.Find("inner");
            if (inner)
            {
                inner.localScale = Vector3.one;
            }
        }
        
        // 提供公共方法动态修改缩放比例
        public void SetPressScale(float scale)
        {
            pressScale = Mathf.Clamp(scale, 0.1f, 2.0f);
        }

        // 获取当前缩放比例
        public float GetPressScale()
        {
            return pressScale;
        }
    }
}