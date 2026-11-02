using System;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public static partial class Utility
    {
        public enum BtnEffectType
        {
            None,
            Scale,              //缩放当前按钮
            ScaleInner          //缩放内部名为inner得节点
        }
        
        public enum BtnSoundType
        {
            None,
            NormalClick,        //普通点击
        }
        
        public enum BtnVibrationType
        {
            None,
            LightImpact,        //轻微震动
            MediumImpact,       //中等震动
            HeavyImpact,        //强烈震动
            Success,            //成功
            Warning,            //警告
            Failure             //失败
        }
        
        public static partial class UIButton
        {
            public static Action<BtnSoundType, BtnVibrationType> BtnClickPreCallback = null;
            public static Action<BtnSoundType, BtnVibrationType> BtnTriggerPreCallback = null;

            /// <summary>
            /// 修改按钮点击效果
            /// </summary>
            /// <param name="obj"></param>
            /// <param name="effectType"></param>
            /// <param name="pressScale"></param>
            private static void ChangeClickHandler(GameObject obj, BtnEffectType effectType, float pressScale = 0.96f)
            {
                UIButtonClickScaleEffect uiButtonClickScaleEffect = obj.GetComponent<UIButtonClickScaleEffect>();
                if (uiButtonClickScaleEffect)
                {
                    GameObject.Destroy(uiButtonClickScaleEffect);
                }
                UIButtonClickScaleInnerEffect uiButtonClickScaleInnerEffect = obj.GetComponent<UIButtonClickScaleInnerEffect>();
                if (uiButtonClickScaleInnerEffect)
                {
                    GameObject.Destroy(uiButtonClickScaleInnerEffect);
                }

                switch (effectType)
                {
                    case BtnEffectType.None:
                        break;
                    case BtnEffectType.Scale:
                        var scaleEffect = obj.AddComponent<UIButtonClickScaleEffect>();
                        scaleEffect.SetPressScale(pressScale);
                        break;
                    case BtnEffectType.ScaleInner:
                        var innerEffect = obj.AddComponent<UIButtonClickScaleInnerEffect>();
                        innerEffect.SetPressScale(pressScale);
                        break;
                }
            }

            /// <summary>
            /// 为UI按钮添加点击事件
            /// </summary>
            /// <param name="go"></param>
            /// <param name="action"></param>
            /// <param name="effectType"></param>
            /// <param name="soundType"></param>
            /// <param name="vibrationType"></param>
            /// <param name="pressScale"></param>
            public static void AddButtonListener(GameObject go, Action action, BtnEffectType effectType = BtnEffectType.Scale, BtnSoundType soundType = BtnSoundType.NormalClick, BtnVibrationType vibrationType = BtnVibrationType.LightImpact, float pressScale = 0.96f)
            {
                Button btn = go.GetComponent<Button>();
                AddButtonListener(btn, action, effectType, soundType, vibrationType);
            }

            /// <summary>
            /// 为UI按钮添加点击事件
            /// </summary>
            /// <param name="btn"></param>
            /// <param name="action"></param>
            /// <param name="effectType"></param>
            /// <param name="soundType"></param>
            /// <param name="vibrationType"></param>
            /// <param name="pressScale"></param>
            public static void AddButtonListener(Button btn, Action action, BtnEffectType effectType = BtnEffectType.Scale, BtnSoundType soundType = BtnSoundType.NormalClick, BtnVibrationType vibrationType = BtnVibrationType.LightImpact, float pressScale = 0.96f)
            {
                // 添加点击效果
                ChangeClickHandler(btn.gameObject, effectType, pressScale);
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    BtnClickPreCallback?.Invoke(soundType, vibrationType);
                    action?.Invoke();
                });
            }
            
            /// <summary>
            /// 为场景的按钮添加点击事件
            /// </summary>
            /// <param name="go"></param>
            /// <param name="action"></param>
            /// <param name="eventTriggerType"></param>
            /// <param name="effectType"></param>
            /// <param name="soundType"></param>
            /// <param name="vibrationType"></param>
            /// <param name="pressScale"></param>
            public static void AddEventTriggerListener(GameObject go, Action action, UnityEngine.EventSystems.EventTriggerType eventTriggerType, BtnEffectType effectType = BtnEffectType.Scale, BtnSoundType soundType = BtnSoundType.NormalClick, BtnVibrationType vibrationType = BtnVibrationType.LightImpact, float pressScale = 0.96f)
            {
                UnityEngine.EventSystems.EventTrigger trigger = go.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                if (trigger == null)
                {
                    trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                }
                UnityEngine.EventSystems.EventTrigger.Entry entry = new UnityEngine.EventSystems.EventTrigger.Entry();
                entry.eventID = eventTriggerType;
                entry.callback.AddListener((data) =>
                {
                    BtnTriggerPreCallback?.Invoke(soundType, vibrationType);
                    action?.Invoke();
                });
                trigger.triggers.Add(entry);
                // 添加点击效果
                ChangeClickHandler(go, effectType, pressScale);
            }
        }
    }
}