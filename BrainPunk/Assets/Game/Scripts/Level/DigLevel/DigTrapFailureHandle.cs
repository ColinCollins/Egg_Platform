using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

namespace Game.Dig
{
    /// <summary>可复用的Dig陷阱失败脚本；支持Trigger及普通碰撞，只上报本关实际参与角色的接触。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Trap Failure Handle")]
    public sealed class DigTrapFailureHandle : DigFailAreaHandle
    {
        #region Inspector 绑定
        /// <summary>本陷阱独占的击中粒子实例；Inspector显式绑定，关闭Play On Awake和Loop，未绑定时只执行原失败流程。</summary>
        [SerializeField] private ParticleSystem knockEffect;
        #endregion

        #region 失败表现
        /// <summary>陷阱保留原先上跳再下坠的死亡表现，覆盖FailArea的直接下坠策略。</summary>
        protected override bool PlayDeathHop => true;
        /// <summary>公共判定接受失败后保留陷阱原有Knock与音效；FailArea基类不执行这些表现。</summary>
        protected override void OnFailureAccepted(Vector2 point)
        {
            PlayKnock(point);
            AudioManager.PlaySound("trap");
        }
        #endregion

        #region 击中表现与清理
        /// <summary>失败已接受后仅播放一次；XY取命中位置，保留粒子现有Z，不移动陷阱／Actor或重新提交结果。</summary>
        private void PlayKnock(Vector2 point)
        {
            if (!knockEffect) return;
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            knockEffect.transform.position = new Vector3(point.x, point.y, knockEffect.transform.position.z);
            knockEffect.Play(true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !knockEffect) return;
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        #endregion
    }
}
