using UnityEngine;

/// <summary>
/// DLevel020 的猫夹球控制器。
/// 复用 DLevel002 已验证的“可见入场、首笔开夹放球、重开直接就位”生命周期；
/// 水面浮力由本关 Prefab 内的 BuoyancyEffector2D 独立负责。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 20 Ctrl")]
public sealed class DLevel20Ctrl : DLevel2Ctrl
{
}
