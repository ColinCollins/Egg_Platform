using UnityEngine;

/// <summary>
/// DLevel_058 的猫夹角色控制器。
/// 复用 DLevel_002 的可见入场、首笔开夹和夹持物释放流程；
/// 本关把夹持球替换为 Actor，释放后由 Actor 根 Rigidbody2D 接管物理。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 58 Actor Release Ctrl")]
public sealed class DLevel58ActorReleaseCtrl : DLevel2Ctrl
{
}
