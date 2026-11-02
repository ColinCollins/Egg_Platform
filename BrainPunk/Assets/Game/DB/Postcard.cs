using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    public partial class Postcard : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;
        /// <summary>
        /// 对应 levelData 收集的 star 数量
        /// victory 默认可能给到 number = 3, 其他放在场景中
        /// </summary>
        [SerializeField] private Dictionary<string, List<int>> levelCollectedStar = new Dictionary<string, List<int>>();

        /// <summary>
        /// 已经完成的 postcard 内容。
        /// 若 postcard 当前进度完成但是还没有记录 complete，则进入的时候播放 getRewardAnim
        /// </summary>
        [SerializeField] private List<string> completedId = new List<string>();

        /// <summary>
        /// 当前 postcard 第几章
        /// </summary>
        [SerializeField] private string currentId;

        /// <summary>
        /// chest progress 上一次记录的进度状态
        /// </summary>
        [SerializeField] private int lastRecordProgress;

        /// <summary>
        /// 当前 postcard 已经解锁的部件 id，根据这个 count 判断 chest 的 progress
        /// </summary>
        [SerializeField] private List<int> currentCollectedPuzzledId = new List<int>();

        /// <summary>
        /// levelCollectedStar 已经收集到的星星数量。用于判断 victoryPanel 收集的 star 的动画表现。
        /// </summary>
        [SerializeField] private Dictionary<string, int> levelWaitingAnimStar = new Dictionary<string, int>();

        /// <summary>
        /// 是否有引导
        /// </summary>
        [SerializeField] private bool hasGuide;
        /// <summary>
        /// 是否正在引导流程中
        /// </summary>
        [SerializeField] private bool isGuiding;


        /// <summary>
        /// 是否展示 TipsBubble
        /// </summary>
        [SerializeField] private bool hasShowTipsBubble;

    }
}
