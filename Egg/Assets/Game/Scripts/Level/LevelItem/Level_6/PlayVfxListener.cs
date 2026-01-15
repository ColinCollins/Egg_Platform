using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 指定播放动效
    /// </summary>
    public class PlayVfxListener : BaseItemEventHandle, IDebuger
    {
        [SerializeField] private Transform target;
        [SerializeField] private GameObject particle;

        public override void Execute()
        {
            IsDone = false;

            Instantiate(particle, target);

            WaitForDone();
        }

        private async Task WaitForDone()
        {
            await UniTask.WaitForSeconds(0.1f);
            IsDone = true;
        }
    }
}
