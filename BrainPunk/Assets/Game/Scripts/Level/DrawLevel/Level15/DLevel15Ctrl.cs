using UnityEngine;

/// <summary>
/// DLevel_015 的单套镜像 Alien 夹子球控制器。
/// 复用 DLevel_002 已验证的入场、首笔开夹与球释放流程，并通过基类的镜像释放边界
/// 保证球进入 Dynamic 后刚体根保持正缩放。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 15 Ctrl")]
public sealed class DLevel15Ctrl : DLevel2Ctrl
{
}
