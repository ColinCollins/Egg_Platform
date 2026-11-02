using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Dig
{
    /// <summary>南瓜目标成功后的单次弹性动画与彩纸表现；由目标OnReached显式调用，不参与胜负或地图初始化。</summary>
    [DisallowMultipleComponent]
    public sealed class DigPumpkinSuccessEffect : MonoBehaviour
    {
        #region Inspector 绑定与表现参数
        /// <summary>南瓜美术节点；显式绑定无Collider／Rigidbody的Visual子节点，摇摆时保持其它角色的碰撞轮廓稳定。</summary>
        [SerializeField, LabelText("南瓜节点")] private Transform pumpkin;
        /// <summary>成功彩纸的根粒子系统；显式绑定后连同子粒子播放，禁用或重播时先清除旧粒子。</summary>
        [SerializeField, LabelText("成功彩纸")] private ParticleSystem confetti;
        /// <summary>整段动画的游戏秒数；当前胜利延迟为1秒，建议保持动效更短，不延长结算。</summary>
        [SerializeField, Min(0.1f), LabelText("动画时长")] private float duration = 0.36f;
        /// <summary>相对原始缩放的弹性幅度；只改变XY，保持原始Z缩放。</summary>
        [SerializeField, Range(0f, 0.25f), LabelText("缩放幅度")] private float scaleStrength = 0.1f;
        /// <summary>左右摆动的最大本地Z角度；相对原始旋转，收尾恢复。</summary>
        [SerializeField, Range(0f, 15f), LabelText("摇摆角度")] private float swayAngle = 6f;
        #endregion

        #region 单次播放与清理
        private Sequence _animation;
        private Vector3 _initialScale;
        private Quaternion _initialRotation;
        private bool _hasInitialPose;

        /// <summary>仅由运行中的成功回调播放；先终止本组件旧动画并归位，再重新播放，不累乘缩放。</summary>
        public void PlaySuccess()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !pumpkin) return;
            StopAndRestore();
            RestartConfetti();
            _initialScale = pumpkin.localScale;
            _initialRotation = pumpkin.localRotation;
            _hasInitialPose = true;

            float time = Mathf.Max(0.1f, duration);
            float strength = Mathf.Clamp(scaleStrength, 0f, 0.25f);
            float angle = Mathf.Clamp(swayAngle, 0f, 15f);
            _animation = DOTween.Sequence();
            _animation.Append(pumpkin.DOScale(Scaled(1f + strength, 1f - strength * 1.2f), time * 0.14f).SetEase(Ease.InQuad))
                .Join(pumpkin.DOLocalRotateQuaternion(Rotated(-angle), time * 0.14f).SetEase(Ease.OutQuad))
                .Append(pumpkin.DOScale(Scaled(1f - strength * 0.7f, 1f + strength * 1.4f), time * 0.25f).SetEase(Ease.OutQuad))
                .Join(pumpkin.DOLocalRotateQuaternion(Rotated(angle), time * 0.25f).SetEase(Ease.InOutSine))
                .Append(pumpkin.DOScale(Scaled(1f + strength * 0.4f, 1f - strength * 0.4f), time * 0.22f).SetEase(Ease.InOutSine))
                .Join(pumpkin.DOLocalRotateQuaternion(Rotated(-angle * 0.45f), time * 0.22f).SetEase(Ease.InOutSine))
                .Append(pumpkin.DOScale(_initialScale, time * 0.39f).SetEase(Ease.OutCubic))
                .Join(pumpkin.DOLocalRotateQuaternion(_initialRotation, time * 0.39f).SetEase(Ease.OutCubic))
                .OnComplete(() =>
                {
                    _animation = null;
                    RestorePose();
                });
        }

        private Vector3 Scaled(float x, float y) => Vector3.Scale(_initialScale, new Vector3(x, y, 1f));
        private Quaternion Rotated(float angle) => _initialRotation * Quaternion.Euler(0f, 0f, angle);

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            StopAndRestore();
            StopConfetti();
        }

        private void RestartConfetti()
        {
            if (!confetti) return;
            confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            confetti.Play(true);
        }

        private void StopConfetti()
        {
            if (confetti) confetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void StopAndRestore()
        {
            _animation?.Kill();
            _animation = null;
            RestorePose();
        }

        private void RestorePose()
        {
            if (!_hasInitialPose) return;
            if (pumpkin)
            {
                pumpkin.localScale = _initialScale;
                pumpkin.localRotation = _initialRotation;
            }
            _hasInitialPose = false;
        }
        #endregion
    }
}
