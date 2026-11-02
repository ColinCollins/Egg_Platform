using UnityEngine;

/// <summary>
/// 两颗原地球的首笔释放与尝试重置控制器。
/// 适用于没有猫或夹子、第一笔有效画线后让双球同步进入 Dynamic 的 Draw 关卡。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Two Ball Release Ctrl")]
public sealed class DrawTwoBallReleaseCtrl : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 左侧蓝球刚体，Prefab 初态必须为 Kinematic。
    /// </summary>
    [Header("双球")]
    [SerializeField] private Rigidbody2D firstBall;

    /// <summary>
    /// 右侧蓝球刚体，Prefab 初态必须为 Kinematic。
    /// </summary>
    [SerializeField] private Rigidbody2D secondBall;

    #endregion

    #region Runtime State

    private sealed class BallInitialState
    {
        public Transform Parent;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public CircleCollider2D Collider;
        public Vector2 ColliderOffset;
        public Transform PayloadContentRoot;
        public Vector3 PayloadLocalPosition;
        public Quaternion PayloadLocalRotation;
        public Vector3 PayloadLocalScale;
        public bool IsCached;
    }

    private readonly BallInitialState _firstBallState = new();
    private readonly BallInitialState _secondBallState = new();
    private bool _hasReleasedBalls;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialState(firstBall, _firstBallState);
        CacheInitialState(secondBall, _secondBallState);
        RestoreInitialState();
    }

    #endregion

    #region Draw Level Handlers

    /// <summary>
    /// 第一笔有效画线完成后同步释放两颗球，后续笔画不重复修改物理状态。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasReleasedBalls)
            return;

        _hasReleasedBalls = true;
        ReleaseBall(firstBall, _firstBallState);
        ReleaseBall(secondBall, _secondBallState);
    }

    /// <summary>
    /// Draw 尝试重置时恢复两颗球的父级、位姿、镜像和 Kinematic 初态。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        RestoreInitialState();
    }

    #endregion

    #region Initial State

    private void ResolveReferences()
    {
        if (firstBall != null && secondBall != null)
            return;

        Rigidbody2D[] candidates = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            Rigidbody2D candidate = candidates[i];
            if (candidate.name != "yellow_ball")
                continue;

            if (firstBall == null ||
                candidate.transform.position.x < firstBall.transform.position.x)
            {
                secondBall = firstBall;
                firstBall = candidate;
            }
            else if (secondBall == null ||
                     candidate.transform.position.x < secondBall.transform.position.x)
            {
                secondBall = candidate;
            }
        }
    }

    private static void CacheInitialState(
        Rigidbody2D ball,
        BallInitialState state)
    {
        if (ball == null || state.IsCached)
            return;

        Transform ballTransform = ball.transform;
        state.Parent = ballTransform.parent;
        state.LocalPosition = ballTransform.localPosition;
        state.LocalRotation = ballTransform.localRotation;
        state.LocalScale = ballTransform.localScale;
        state.Collider = ball.GetComponent<CircleCollider2D>();
        if (state.Collider != null)
            state.ColliderOffset = state.Collider.offset;

        state.PayloadContentRoot = ballTransform.Find("payloadContentRoot");
        if (state.PayloadContentRoot != null)
        {
            state.PayloadLocalPosition = state.PayloadContentRoot.localPosition;
            state.PayloadLocalRotation = state.PayloadContentRoot.localRotation;
            state.PayloadLocalScale = state.PayloadContentRoot.localScale;
        }

        state.IsCached = true;
    }

    private void RestoreInitialState()
    {
        _hasReleasedBalls = false;
        RestoreBall(firstBall, _firstBallState);
        RestoreBall(secondBall, _secondBallState);
    }

    private static void RestoreBall(
        Rigidbody2D ball,
        BallInitialState state)
    {
        if (ball == null || !state.IsCached)
            return;

        ball.bodyType = RigidbodyType2D.Kinematic;
        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;

        Transform ballTransform = ball.transform;
        ballTransform.SetParent(state.Parent, false);
        ballTransform.localPosition = state.LocalPosition;
        ballTransform.localRotation = state.LocalRotation;
        ballTransform.localScale = state.LocalScale;

        if (state.Collider != null)
            state.Collider.offset = state.ColliderOffset;

        if (state.PayloadContentRoot != null)
        {
            state.PayloadContentRoot.localPosition = state.PayloadLocalPosition;
            state.PayloadContentRoot.localRotation = state.PayloadLocalRotation;
            state.PayloadContentRoot.localScale = state.PayloadLocalScale;
        }
    }

    #endregion

    #region Release

    private static void ReleaseBall(
        Rigidbody2D ball,
        BallInitialState state)
    {
        if (ball == null || !state.IsCached)
            return;

        ball.bodyType = RigidbodyType2D.Kinematic;
        ball.linearVelocity = Vector2.zero;
        ball.angularVelocity = 0f;
        NormalizeMirroredBall(ball, state);
        ball.bodyType = RigidbodyType2D.Dynamic;
        ball.WakeUp();
    }

    /// <summary>
    /// 把球根负缩放转移到内容根，避免 Dynamic Rigidbody2D 继续携带镜像缩放。
    /// </summary>
    private static void NormalizeMirroredBall(
        Rigidbody2D ball,
        BallInitialState state)
    {
        Transform ballTransform = ball.transform;
        Vector3 scale = ballTransform.localScale;
        bool mirrorX = scale.x < 0f;
        bool mirrorY = scale.y < 0f;
        if (!mirrorX && !mirrorY)
            return;

        ballTransform.localScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));

        if (state.Collider != null)
        {
            Vector2 offset = state.ColliderOffset;
            if (mirrorX)
                offset.x = -offset.x;
            if (mirrorY)
                offset.y = -offset.y;
            state.Collider.offset = offset;
        }

        if (state.PayloadContentRoot == null)
            return;

        Quaternion mirrorRotation = Quaternion.identity;
        if (mirrorX)
            mirrorRotation *= Quaternion.Euler(0f, 180f, 0f);
        if (mirrorY)
            mirrorRotation *= Quaternion.Euler(180f, 0f, 0f);

        state.PayloadContentRoot.localPosition = state.PayloadLocalPosition;
        state.PayloadContentRoot.localRotation =
            state.PayloadLocalRotation * mirrorRotation;
        state.PayloadContentRoot.localScale = state.PayloadLocalScale;
    }

    #endregion
}
