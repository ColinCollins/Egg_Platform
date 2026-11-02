using Cysharp.Threading.Tasks;
using DG.Tweening;
using MelenitasDev.SoundsGood;
using UnityEngine;

namespace Game.Turntable
{
    /// <summary>
    /// version 1.0
    /// 从两个角度范围[minA,maxA][minB, maxB]内随机一个角度范围作为停止范围。期间的旋转动画不要变。
    /// </summary>
    public class TurntableItemChoiceCtrl : MonoBehaviour
    {
        private const float FullRotationAngle = 360f * 20f;
        private const float RotationDuration = 3f;

        [SerializeField] private GameObject key;
        [SerializeField] private GameObject keyStartPoint;
        [SerializeField] private Transform turntable;
        [SerializeField] private ThrowItemHandle throwHandle;

        // [SerializeField] private TriggerAreaHandle triggerKey;

        [SerializeField] private TurntableButton button;


        [Header("补充")]
        [SerializeField] private GameObject otherStuff;

        [Header("停止角度范围")]
        // 用途：配置转盘可停下的第一组 Z 轴角度范围；调用边界：仅在 Play 单次旋转开始时读取；注意：默认 0,0 会保持旧停点。
        [Tooltip("第一组停止角度范围，单位为 Z 轴角度；默认 0,0 时保持旧逻辑停在 0 度附近。")]
        [SerializeField] private Vector2 stopAngleRangeA = Vector2.zero;

        // 用途：配置转盘可停下的第二组 Z 轴角度范围；调用边界：仅在 Play 单次旋转开始时读取；注意：两组范围会等概率随机。
        [Tooltip("第二组停止角度范围，单位为 Z 轴角度；播放时会在两组范围中随机选择一组。")]
        [SerializeField] private Vector2 stopAngleRangeB = Vector2.zero;

        private bool isKey = false;
        private bool isOther = false;
        private bool isPlaying = false;
        private bool isFinished = false;
        private Sound _turntableSound;
        private Tween _turntableTween;
        private bool isStopRangeA = true;

        /// <summary>
        /// 用途：读取本次旋转选择的停止区间；调用边界：Play 后读取；注意：true 表示 A 区间，false 表示 B 区间。
        /// </summary>
        public bool IsStopRangeA => isStopRangeA;

        void Start()
        {
            if (button != null)
                button.SetEnter(Play);
        }

        public void Play()
        {
            if (isPlaying || isFinished || turntable == null)
                return;

            isPlaying = true;
            float stopAngle = GetRandomStopAngle();
            _turntableSound = Game.Scripts.Common.AudioManager.PlaySound("turnntableStart", loop: true);
            _turntableTween = turntable
                .DORotate(new Vector3(0, 0, FullRotationAngle + stopAngle), RotationDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.InOutCirc)
                .OnComplete(() =>
                {
                    StopTurntableSound();
                    _turntableTween = null;
                    ReadyToThrow().Forget();
                });
        }

        public void EnterArea()
        {
            isKey = true;
        }

        public void ExitArea()
        {
            isKey = false;
        }

        public void EnterOtherArea()
        {
            isOther = true;
        }

        public void ExitOtherArea()
        {
            isOther = false;
        }


        async UniTask ReadyToThrow()
        {
            await UniTask.WaitForSeconds(0.1f);
            if (this == null)
                return;

            Game.Scripts.Common.AudioManager.PlaySound("turntableGetReward");
            isPlaying = false;
            if (IsStopRangeA)
            {
                // A 区间
                if (throwHandle != null)
                    throwHandle.SpawnItem();
            }
            else
            {
                if (otherStuff != null && !otherStuff.activeSelf)
                {
                    // B 区间
                    otherStuff.SetActive(true);
                    otherStuff = null;
                }
            }
        }

        private float GetRandomStopAngle()
        {
            isStopRangeA = Random.Range(0, 2) == 0;
            Vector2 selectedRange = isStopRangeA ? stopAngleRangeA : stopAngleRangeB;
            return GetRandomAngleInRange(selectedRange);
        }

        private static float GetRandomAngleInRange(Vector2 range)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);

            if (Mathf.Approximately(min, max))
                return NormalizeAngle(min);

            return NormalizeAngle(Random.Range(min, max));
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f)
                angle += 360f;

            return angle;
        }

        private void StopTurntableSound()
        {
            if (_turntableSound != null && _turntableSound.Using)
                _turntableSound.Stop(0f);

            _turntableSound = null;
        }

        void OnDestroy()
        {
            _turntableTween?.Kill();
            _turntableTween = null;

            if (turntable != null)
                turntable.DOKill();

            StopTurntableSound();
        }
    }
}
