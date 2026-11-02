using Game.Scripts.Common;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 触发一次 SFX 播放的事件监听器。
    /// </summary>
    public class PlaySfxListener : BaseItemEventHandle
    {
        [SerializeField] private string sfxTag;
        [SerializeField] private int clipIndex = -1;

        public override void Execute()
        {
            IsRunning = true;
            AudioManager.PlaySound(sfxTag, clipIndex: clipIndex);
            IsRunning = false;
            IsDone = true;
        }
    }
}
