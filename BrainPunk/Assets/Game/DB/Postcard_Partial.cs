using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class Postcard
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            levelCollectedStar = new Dictionary<string, List<int>>();
            completedId = new List<string>();
            currentId = "";
            lastRecordProgress = 0;
            currentCollectedPuzzledId = new List<int>();
            levelWaitingAnimStar = new Dictionary<string, int>();
            hasGuide = false;
            isGuiding = false;
            hasShowTipsBubble = false;
        }

        public Dictionary<string, List<int>> LevelCollectedStar
        {
            get => levelCollectedStar;
            set => levelCollectedStar = value;
        }

        public List<string> CompletedId
        {
            get => completedId;
            set => completedId = value;
        }

        public string CurrentId
        {
            get => currentId;
            set => currentId = value;
        }

        public int LastRecordProgress
        {
            get => lastRecordProgress;
            set => lastRecordProgress = value;
        }

        public List<int> CurrentCollectedPuzzledId
        {
            get => currentCollectedPuzzledId;
            set => currentCollectedPuzzledId = value;
        }

        public Dictionary<string, int> LevelWaitingAnimStar
        {
            get => levelWaitingAnimStar;
            set => levelWaitingAnimStar = value;
        }

        public bool HasGuide
        {
            get => hasGuide;
            set => hasGuide = value;
        }

        public bool IsGuiding
        {
            get => isGuiding;
            set => isGuiding = value;
        }

        /// <summary>
        /// 用途：记录明信片引导结束后的提示气泡是否已经展示过。
        /// 调用边界：仅由运行态 PostcardPopup 的一次性展示门槛读取和写入。
        /// 注意事项：置为 true 并保存后，后续打开 PostcardPopup 不再展示该气泡。
        /// </summary>
        public bool HasShowTipsBubble
        {
            get => hasShowTipsBubble;
            set => hasShowTipsBubble = value;
        }

    }
}
