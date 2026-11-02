using UnityEngine;

namespace Game.Dig
{
    /// <summary>需要在 Dig 地形中预留出生空腔的对象基类；只提供本地配置，不主动挖掘。</summary>
    public class BaseDigComp : MonoBehaviour
    {
        #region 出生空腔配置
        /// <summary>圆形出生洞的世界半径；仅 InitMap 烘焙时读取，不随判定大小或成长变化。</summary>
        [Header("出生空腔")]
        [Tooltip("对象中心的出生洞世界半径；修改后在关卡 Prefab 点击 InitMap 并保存。")]
        [SerializeField, Min(0.01f)] private float radius = 0.75f;

        /// <summary>供 InitMap 读取的圆洞世界半径；未绑定 HoleShape 时必须为有限正数。</summary>
        public float Radius => radius;
        #endregion

        #region 预留洞形状配置
        /// <summary>可选的挖洞形状；绑定后使用本节点 Collider2D 轮廓，不读取圆形 Radius。</summary>
        [Header("预留洞（Sand 子节点）")]
        [Tooltip("显式拖入本节点 Collider2D 后按其轮廓挖洞；为空时使用 Radius 圆形。烘焙后只切换此碰撞体的启用状态，不改变对象 active。")]
        [SerializeField] private Collider2D holeShape;
        /// <summary>在 Collider2D 边界外额外挖出的世界距离；不修改碰撞体自身形状。</summary>
        [SerializeField, Min(0f)] private float outwardDistance;
        /// <summary>编辑器烘焙读取的预留洞形状；节点 inactive 时仍保留引用。</summary>
        public Collider2D HoleShape => holeShape;
        /// <summary>预留洞的世界外扩距离；供编辑器 InitMap 使用，必须为有限非负数。</summary>
        public float OutwardDistance => outwardDistance;
        /// <summary>InitMap 后是否启用显式绑定的 HoleShape；不切换对象 active，未绑定时不修改碰撞状态。</summary>
        [InspectorName("烘焙后启用碰撞体")]
        [Tooltip("InitMap 后按此值设置 Hole Shape 的 enabled；不改变对象 active。未绑定 Hole Shape 时不做切换，保留 isShow 字段以兼容已有配置。")]
        [SerializeField] private bool isShow;
        /// <summary>编辑器烘焙后 HoleShape 的启用状态；仅 InitMap 读取，保留旧属性名以兼容调用。</summary>
        public bool IsShow => isShow;
        #endregion
    }
}
