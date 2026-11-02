using Bear.Logger;
using UnityEngine;

/// <summary>
/// IWanner Actor 的世界坐标循环边界控制器。
/// Actor 越过矩形任意一边时，会保留越界距离并从相对边回到矩形内。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannarGame_UnLimitBoardCtrl : MonoBehaviour, IDebuger
{
    #region Inspector

    /// <summary>
    /// 用途：指定需要进行四向循环的位置主体。
    /// 调用边界：优先使用 Inspector 绑定；为空时只在当前控制器子层级查找 Actor_IWanner。
    /// 注意事项：脚本只修改 Actor 的世界坐标，不改变速度、重力、输入或朝向。
    /// </summary>
    [Header("Target")]
    [Tooltip("需要在循环边界内移动的 IWanner Actor；为空时从当前子层级查找")]
    [SerializeField] private Actor_IWanner actor;

    /// <summary>
    /// 用途：定义 Actor 位置允许存在的世界坐标矩形。
    /// 调用边界：X/Y 分别使用 Rect 的 min/max 作为左右和下上边界。
    /// 注意事项：宽高必须大于 0；边界判断基于 Actor 根节点位置，不包含 Collider2D 尺寸。
    /// </summary>
    [Header("Board")]
    [Tooltip("世界坐标循环范围；Actor 越界后会从相对边出现")]
    [SerializeField] private Rect boardRect = new Rect(-9.6f, -5.4f, 19.2f, 10.8f);

    #endregion

    #region Runtime State

    private Rigidbody2D _actorBody;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (actor == null)
        {
            this.LogWarning(
                "[IWannarGame_UnLimitBoardCtrl] Actor_IWanner is missing; position wrapping is disabled.");
            enabled = false;
            return;
        }

        if (!HasValidBoardRect())
        {
            this.LogWarning(
                "[IWannarGame_UnLimitBoardCtrl] Board Rect width and height must be greater than 0; position wrapping is disabled.");
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (actor == null || !HasValidBoardRect())
            return;

        Vector2 currentPosition = _actorBody != null
            ? _actorBody.position
            : (Vector2)actor.transform.position;

        if (!TryWrapPosition(currentPosition, out Vector2 wrappedPosition))
            return;

        if (_actorBody != null)
        {
            _actorBody.position = wrappedPosition;
            return;
        }

        Vector3 actorPosition = actor.transform.position;
        actorPosition.x = wrappedPosition.x;
        actorPosition.y = wrappedPosition.y;
        actor.transform.position = actorPosition;
    }

    #endregion

    #region Wrapping

    private bool TryWrapPosition(Vector2 position, out Vector2 wrappedPosition)
    {
        wrappedPosition = position;
        bool isOutsideX = position.x < boardRect.xMin || position.x > boardRect.xMax;
        bool isOutsideY = position.y < boardRect.yMin || position.y > boardRect.yMax;
        if (!isOutsideX && !isOutsideY)
            return false;

        if (isOutsideX)
            wrappedPosition.x = WrapCoordinate(position.x, boardRect.xMin, boardRect.width);
        if (isOutsideY)
            wrappedPosition.y = WrapCoordinate(position.y, boardRect.yMin, boardRect.height);

        return true;
    }

    private static float WrapCoordinate(float value, float minimum, float length)
    {
        return minimum + Mathf.Repeat(value - minimum, length);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponentInChildren<Actor_IWanner>(true);

        if (actor == null)
            return;

        _actorBody = actor.RB != null
            ? actor.RB
            : actor.GetComponent<Rigidbody2D>();
    }

    private bool HasValidBoardRect()
    {
        return boardRect.width > 0f && boardRect.height > 0f;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (!HasValidBoardRect())
            return;

        Vector3 center = new Vector3(boardRect.center.x, boardRect.center.y, transform.position.z);
        Vector3 size = new Vector3(boardRect.width, boardRect.height, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);
    }

    #endregion
}
