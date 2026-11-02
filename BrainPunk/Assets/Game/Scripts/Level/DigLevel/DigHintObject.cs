using UnityEngine;

namespace Game.Dig
{
    /// <summary>
    /// 标识一组完整提示的根对象，供 DigLevelCtrl 编辑器按钮收集。
    /// 图标、路径等放在此根节点下；运行时显隐由关卡负责，本组件不自行初始化或扫描层级。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Hint Object")]
    public sealed class DigHintObject : MonoBehaviour
    {
    }
}
