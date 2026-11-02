using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
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

        /// <summary>
        /// 实例化并激活动效后，是否显式调用所有 ParticleSystem 的 Play。
        /// 注意事项：默认 false 以保持已有使用者行为；只控制当前组件创建的粒子实例。
        /// </summary>
        [SerializeField] private bool autoPlay = false;

        public override void Execute()
        {
            IsRunning = true;
            IsDone = false;

            var particleInstance = Instantiate(particle, target);
            particleInstance.SetActive(true);

            if (autoPlay)
            {
                ParticleSystem[] particleSystems =
                    particleInstance.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < particleSystems.Length; i++)
                    particleSystems[i].Play(false);
            }

            WaitForDone();
        }

        private async Task WaitForDone()
        {
            await UniTask.WaitForSeconds(0.1f);
            IsRunning = false;
            IsDone = true;
        }
    }
}
