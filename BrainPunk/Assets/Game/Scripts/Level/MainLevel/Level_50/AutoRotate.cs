using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Game.Level
{
    /// <summary>
    /// 让目标持续自转，旋转轴和速度都可以在 Inspector 中配置。
    /// </summary>
    [MovedFrom(false, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "AutoRotate")]
    [DisallowMultipleComponent]
    public class AutoRotate : MonoBehaviour
    {
        private enum RotationAxis
        {
            X,
            Y,
            Z,
            Custom,
        }

        [Header("Target")]
        [Tooltip("需要旋转的目标；为空时默认旋转当前物体。")]
        [SerializeField] private Transform target;

        [Header("Rotation")]
        [Tooltip("旋转轴。")]
        [SerializeField] private RotationAxis axis = RotationAxis.Z;

        [Tooltip("当旋转轴为 Custom 时使用该向量。")]
        [SerializeField] private Vector3 customAxis = Vector3.forward;

        [Tooltip("角速度，单位为度/秒。")]
        [SerializeField] private float speed = 90f;

        [Tooltip("旋转使用本地坐标还是世界坐标。")]
        [SerializeField] private Space rotateSpace = Space.Self;

        [Tooltip("是否忽略 Time.timeScale。")]
        [SerializeField] private bool ignoreTimeScale;

        public float Speed
        {
            get => speed;
            set => speed = value;
        }

        private void Reset()
        {
            target = transform;
        }

        private void Awake()
        {
            if (target == null)
            {
                target = transform;
            }
        }

        private void Update()
        {
            if (target == null || Mathf.Approximately(speed, 0f))
            {
                return;
            }

            Vector3 rotateAxis = GetRotateAxis();
            if (rotateAxis.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            float deltaTime = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            target.Rotate(rotateAxis, speed * deltaTime, rotateSpace);
        }

        public void SetSpeed(float value)
        {
            Speed = value;
        }

        public void SetTarget(Transform value)
        {
            target = value != null ? value : transform;
        }

        private Vector3 GetRotateAxis()
        {
            switch (axis)
            {
                case RotationAxis.X:
                    return Vector3.right;
                case RotationAxis.Y:
                    return Vector3.up;
                case RotationAxis.Z:
                    return Vector3.forward;
                case RotationAxis.Custom:
                    return customAxis.normalized;
                default:
                    return Vector3.forward;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (target == null)
            {
                target = transform;
            }

            if (axis == RotationAxis.Custom && customAxis == Vector3.zero)
            {
                customAxis = Vector3.forward;
            }
        }
#endif
    }
}
