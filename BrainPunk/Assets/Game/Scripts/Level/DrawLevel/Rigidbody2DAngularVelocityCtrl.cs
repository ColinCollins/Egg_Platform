using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 为显式绑定的 Rigidbody2D 设置角速度，支持启用时应用和 Inspector 事件调用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Rigidbody2DAngularVelocityCtrl : MonoBehaviour
    {
        #region Inspector

        /// <summary>
        /// 要旋转的刚体；必须在 Inspector 显式绑定，不自动查找或改变刚体类型、约束。
        /// </summary>
        [SerializeField] private Rigidbody2D targetRigidbody;

        /// <summary>
        /// 配置角速度，单位度/秒；正数逆时针、负数顺时针，0 停止旋转。
        /// 只在启用或调用 ApplyAngularVelocity 时应用，不逐帧覆盖物理结果。
        /// </summary>
        [SerializeField] private float angularVelocity = 90f;

        /// <summary>
        /// 运行态启用组件时是否应用配置角速度；关闭后由外部 API 或 UnityEvent 触发。
        /// </summary>
        [SerializeField] private bool applyOnEnable = true;

        #endregion

        #region Public API

        /// <summary>
        /// 应用 Inspector 配置角速度；可绑定无参数 UnityEvent，只在运行态且目标已绑定时生效。
        /// </summary>
        public void ApplyAngularVelocity()
        {
            SetAngularVelocity(angularVelocity);
        }

        /// <summary>
        /// 设置目标当前角速度，单位度/秒；可直接调用或绑定 float UnityEvent，传入 0 可停止旋转。
        /// 只在运行态生效，不修改 Inspector 配置，也不负责持续恒速、暂停或重置。
        /// </summary>
        public void SetAngularVelocity(float value)
        {
            if (!Application.isPlaying || targetRigidbody == null)
                return;

            targetRigidbody.angularVelocity = value;
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (Application.isPlaying && applyOnEnable)
                ApplyAngularVelocity();
        }

        #endregion
    }
}
