using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

namespace Game.Dig
{
    /// <summary>单次爆炸死亡的值副本；由 Bomb 传给关卡，不存档、不回写 Actor 默认死亡配置。</summary>
    internal readonly struct DigExplosionDeath
    {
        #region 当次参数
        /// <summary>引发本次死亡的炸弹世界中心，直接命中与范围命中均在关闭碰撞前确定。</summary>
        public readonly Vector2 Center;
        /// <summary>击飞力度，乘左右上抛向量 (±0.5,0.7) 得到初速度，须为正有限值。</summary>
        public readonly float Speed;
        /// <summary>世界向下的演出加速度，允许零，不修改 Rigidbody2D。</summary>
        public readonly float Gravity;
        /// <summary>Body 每秒旋转角度，允许零及负数。</summary>
        public readonly float Spin;
        /// <summary>本实例烧焦颜色，只使用 RGB，保留 Spine 原 Alpha。</summary>
        public readonly Color BurnColor;
        /// <summary>供失败门禁检查数值，非法配置不能先锁定关卡再中断演出。</summary>
        public bool IsValid => Finite(Center.x) && Finite(Center.y) && Finite(Speed) && Speed > 0f &&
            Finite(Gravity) && Gravity >= 0f && Finite(Spin) &&
            Finite(BurnColor.r) && Finite(BurnColor.g) && Finite(BurnColor.b);

        /// <summary>仅保存本次命中的世界中心和 Inspector 参数；调用方需先通过 IsValid。</summary>
        public DigExplosionDeath(Vector2 center, float speed, float gravity, float spin, Color burnColor)
        {
            Center = center;
            Speed = speed;
            Gravity = gravity;
            Spin = spin;
            BurnColor = burnColor;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        #endregion
    }

    public sealed partial class DigActorCtrl
    {
        #region 爆炸专用显式绑定
        /// <summary>角色已有的 Spine 实例；用于爆炸烧焦 RGB，必须在 Prefab 绑定，不搜索节点、不改共享材质。</summary>
        [SerializeField, LabelText("爆炸烧焦 Spine")] private SkeletonAnimation burnSkeleton;
        /// <summary>上述 Spine 的已有 Renderer；用实际 Bounds 计算旋转后的完整离屏范围，旧无 Spine 角色可留空。</summary>
        [SerializeField, LabelText("爆炸离屏 Renderer")] private Renderer burnRenderer;
        #endregion

        #region 烧焦与相对方向飞出
        /// <summary>仅由关卡接受的爆炸失败调用；相对方向飞出并旋转至完整离屏，共用死亡 Tween 的暂停、取消与唯一完成回调。</summary>
        /// <param name="levelCamera">本关显式绑定的 2D 正交相机。</param>
        /// <param name="completed">完整飞出屏幕后调用，关卡仍检查 generation 与最短失败等待。</param>
        /// <param name="explosion">当前爆炸中心、速度、加速度、旋转及烧焦颜色，关卡已校验。</param>
        internal void PlayExplosionDeath(Camera levelCamera, Action completed, DigExplosionDeath explosion)
        {
            Vector3 origin = transform.position;
            Vector2 actorCenter = circle.transform.TransformPoint(circle.offset);
            Vector2 direction = new Vector2(actorCenter.x < explosion.Center.x ? -0.5f : 0.5f, 0.7f);
            Vector3 velocity = direction * explosion.Speed;
            Vector3 acceleration = Vector3.down * explosion.Gravity;
            float radius = GetExplosionExitRadius(origin, actorCenter);
            float duration = GetExplosionExitTime(levelCamera, origin, velocity, acceleration, radius);
            Quaternion rotation = body.localRotation;

            _dead = true;
            if (deathSortingGroup)
            {
                deathSortingGroup.sortingLayerName = "SceneLayer";
                deathSortingGroup.sortingOrder = 200;
                deathSortingGroup.sortAtRoot = true;
                deathSortingGroup.enabled = true;
            }
            DisposeAnimationState();
            physicsBody.simulated = false;
            circle.enabled = false;
            _animation?.Kill();
            if (burnSkeleton && burnSkeleton.Skeleton != null)
            {
                burnSkeleton.Skeleton.R = explosion.BurnColor.r;
                burnSkeleton.Skeleton.G = explosion.BurnColor.g;
                burnSkeleton.Skeleton.B = explosion.BurnColor.b;
            }

            // 同一 Tween 驱动弹道和旋转，避免暂停／退出只停位移却遗留旋转；不重新开启角色物理。
            _animation = DOVirtual.Float(0f, duration, duration, time =>
            {
                transform.position = origin + velocity * time + acceleration * (0.5f * time * time);
                body.localRotation = rotation * Quaternion.Euler(0f, 0f, explosion.Spin * time);
            }).SetEase(Ease.Linear).OnComplete(() => completed?.Invoke());
        }

        private float GetExplosionExitRadius(Vector3 origin, Vector2 actorCenter)
        {
            Vector3 scale = circle.transform.lossyScale;
            float radius = Vector2.Distance(origin, actorCenter) +
                circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            if (burnRenderer)
            {
                Bounds bounds = burnRenderer.localBounds;
                Matrix4x4 toBody = body.worldToLocalMatrix * burnRenderer.localToWorldMatrix;
                float localRadius = 0f;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    localRadius = Mathf.Max(localRadius, toBody.MultiplyPoint3x4(corner).magnitude);
                }
                // 父级非均匀缩放时，Body 转动后的世界范围可能大于当前 Bounds；用矩阵范数包住各个旋转角度。
                Matrix4x4 parentMatrix = body.parent ? body.parent.localToWorldMatrix : Matrix4x4.identity;
                float parentStretch = Mathf.Sqrt(parentMatrix.MultiplyVector(Vector3.right).sqrMagnitude +
                    parentMatrix.MultiplyVector(Vector3.up).sqrMagnitude + parentMatrix.MultiplyVector(Vector3.forward).sqrMagnitude);
                Vector3 bodyScale = body.localScale;
                float bodyStretch = Mathf.Max(Mathf.Abs(bodyScale.x), Mathf.Abs(bodyScale.y), Mathf.Abs(bodyScale.z));
                float visualRadius = Vector3.Distance(origin, body.position) + localRadius * bodyStretch * parentStretch;
                radius = Mathf.Max(radius, visualRadius);
            }
            return radius + 0.2f;
        }
        #endregion

        #region 完整离屏时间
        private static float GetExplosionExitTime(Camera camera, Vector3 origin, Vector3 velocity,
            Vector3 acceleration, float radius)
        {
            float depth = camera.WorldToViewportPoint(origin).z;
            Vector3 lower = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 upper = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            float x = Vector3.Dot(origin, right), y = Vector3.Dot(origin, up);
            float left = Vector3.Dot(lower, right) - radius, bottom = Vector3.Dot(lower, up) - radius;
            float top = Vector3.Dot(upper, up) + radius, edgeRight = Vector3.Dot(upper, right) + radius;
            if (x <= left || x >= edgeRight || y <= bottom || y >= top) return 0.01f;

            float vx = Vector3.Dot(velocity, right), vy = Vector3.Dot(velocity, up);
            float ax = Vector3.Dot(acceleration, right), ay = Vector3.Dot(acceleration, up);
            float time = Mathf.Min(TimeToBoundary(x, vx, ax, left), TimeToBoundary(x, vx, ax, edgeRight));
            time = Mathf.Min(time, TimeToBoundary(y, vy, ay, bottom));
            time = Mathf.Min(time, TimeToBoundary(y, vy, ay, top));
            return Mathf.Max(0.01f, time);
        }

        private static float TimeToBoundary(float position, float velocity, float acceleration, float boundary)
        {
            if (Mathf.Abs(acceleration) < 0.000001f)
            {
                if (Mathf.Abs(velocity) < 0.000001f) return float.PositiveInfinity;
                float time = (boundary - position) / velocity;
                return time > 0f ? time : float.PositiveInfinity;
            }
            float discriminant = velocity * velocity - 2f * acceleration * (position - boundary);
            if (discriminant < 0f) return float.PositiveInfinity;
            float root = Mathf.Sqrt(discriminant);
            float first = (-velocity - root) / acceleration;
            float second = (-velocity + root) / acceleration;
            return Mathf.Min(first > 0f ? first : float.PositiveInfinity, second > 0f ? second : float.PositiveInfinity);
        }
        #endregion
    }
}
