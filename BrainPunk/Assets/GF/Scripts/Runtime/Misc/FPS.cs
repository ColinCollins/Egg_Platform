using System;
using UnityEngine;

namespace GF
{
    public class FPS : MonoBehaviour
    {
        private static FPS _instance;
        public static bool Enable
        {
            set => _instance.enabled = value;
            get => _instance.enabled;
        }

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>
        /// 计算的更新频率
        /// </summary>
        public float updateInterval = 0.5F;

        /// <summary>
        /// 用来保存时间间隔
        /// </summary>
        private float lastInterval;

        /// <summary>
        /// 记录帧数
        /// </summary>
        private int frames = 0;

        /// <summary>
        /// 记录帧率
        /// </summary>
        private float fps;

        #region 属性信息

        private Color ColorGreen = new Color(0, 1, 0);
        private Color ColorYellow = new Color(1, 1, 0);
        private Color ColorRed = new Color(1f, 0, 0);
        private Rect FpsPosition = new Rect(0, 0, 500, 300);
        
        #endregion

        void Start()
        {
            lastInterval = Time.realtimeSinceStartup;

            frames = 0;
        }

        
        void OnGUI()
        {
            GUI.skin.label.fontSize = 40 * Screen.width / 750;
            GUI.skin.button.fontSize = 20 * Screen.width / 750;
            if (fps > 50)
            {
                GUI.color = ColorGreen;
            }
            else if (fps > 25)
            {
                GUI.color = ColorYellow;
            }
            else
            {
                GUI.color = ColorRed;
            }

            GUI.Label(FpsPosition, "FPS:" + fps.ToString("f1"));
        }

        void Update()
        {
            ++frames;

            if (Time.realtimeSinceStartup > lastInterval + updateInterval)
            {
                fps = frames / (Time.realtimeSinceStartup - lastInterval);

                frames = 0;

                lastInterval = Time.realtimeSinceStartup;
            }
        }
    }
}