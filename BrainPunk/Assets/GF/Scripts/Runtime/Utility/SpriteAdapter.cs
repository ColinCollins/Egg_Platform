using System.Collections.Generic;
using UnityEngine;

namespace GF
{
    [RequireComponent(typeof(Camera))]
    public class SpriteAdapter : MonoBehaviour
    {

        [System.Serializable]
        public class SpriteInfo
        {
            public SpriteRenderer Value = null;
            public EFillModel Model = EFillModel.ShowAll;
        }

        public enum EFillModel
        {
            /// <summary>
            /// 显示图片的所有内容
            /// </summary>
            ShowAll,

            /// <summary>
            /// 使图片内容填满屏幕
            /// </summary>
            Full,

            /// <summary>
            /// 根据图片高度填充屏幕
            /// </summary>
            WithHeight,

            /// <summary>
            /// 根据图片宽度填充屏幕
            /// </summary>
            WithWidth,
            
            /// <summary>
            /// 显示图片的90%内容
            /// </summary>
            Show90Percent
        }

        public enum EUpdateType
        {
            /// <summary>
            /// 只在唤醒时更新一次
            /// </summary>
            UpdateOnAwake,

            /// <summary>
            /// 再每次视口发生变化的时候更新一次
            /// </summary>
            UpdateOnViewportChanged
        }

        public EUpdateType TickType = EUpdateType.UpdateOnAwake;
        public List<SpriteInfo> Members;
        Camera Viewport;
        float ScreenRatio;

        void Awake()
        {
            Viewport = GetComponent<Camera>();
        }

        /// <summary>
        /// 使sprite铺满整个屏幕
        /// </summary>
        public void AdaptSpriteRender(SpriteInfo spriteInfo)
        {
            SpriteRenderer spriteRenderer = spriteInfo.Value;
            Vector3 scale = spriteRenderer.transform.localScale;
            float cameraheight = Viewport.orthographicSize * 2;
            float camerawidth = cameraheight * Viewport.aspect;
            float texr = (float)spriteRenderer.sprite.texture.width / spriteRenderer.sprite.texture.height;
            float viewr = camerawidth / cameraheight;
            switch (spriteInfo.Model)
            {
                case EFillModel.WithHeight:
                    //> 根据图片高度进行填充
                    scale *= cameraheight / spriteRenderer.bounds.size.y;
                    break;
                case EFillModel.WithWidth:
                    //> 根据图片宽度进行填充
                    scale *= camerawidth / spriteRenderer.bounds.size.x;
                    break;
                case EFillModel.Show90Percent:
                    //> 根据图片宽度90%进行填充
                    scale *= camerawidth / (spriteRenderer.bounds.size.x * 0.9f);
                    break;
                case EFillModel.Full:
                    //> 填满整个屏幕
                    if (viewr >= texr)
                    {
                        if (viewr >= 1 && texr >= 1 || texr < 1)
                            scale *= camerawidth / spriteRenderer.bounds.size.x;
                        else
                            scale *= cameraheight / spriteRenderer.bounds.size.y;
                    }
                    else
                    {
                        if (viewr <= 1 || texr > 1)
                            scale *= cameraheight / spriteRenderer.bounds.size.y;
                        else
                            scale *= camerawidth / spriteRenderer.bounds.size.x;
                    }

                    break;
                default:
                    //> 在屏幕上显示图片的全部内容
                    if (viewr >= texr)
                    {
                        scale *= cameraheight / spriteRenderer.bounds.size.y;
                    }
                    else
                    {
                        scale *= camerawidth / spriteRenderer.bounds.size.x;
                    }

                    break;
            }

            spriteRenderer.transform.localScale = scale;
        }
    }
}