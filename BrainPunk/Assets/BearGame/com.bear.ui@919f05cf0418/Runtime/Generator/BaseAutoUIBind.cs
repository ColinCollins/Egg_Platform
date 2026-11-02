using UnityEngine;

/// <summary>
/// 用于自动绑定 UI 规则
/// 继承此类的 MonoBehaviour 脚本可以使用编辑器扩展进行自动 UI 绑定
/// </summary>
public abstract class BaseAutoUIBind : MonoBehaviour
{
    public abstract void Init();
}
