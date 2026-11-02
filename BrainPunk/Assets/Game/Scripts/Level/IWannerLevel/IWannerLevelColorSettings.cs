using UnityEngine;

/// <summary>
/// IWanner 单个小关使用的场景与 HUD 配色资产。
/// 调用边界：由对应关卡 prefab 上的 IWannerLevelCtrl 绑定并在运行时读取；不负责保存、配置表或资源加载。
/// </summary>
[CreateAssetMenu(
    fileName = "IWannerLevelColorSettings",
    menuName = "BrainPunk/IWanner/Level Color Settings")]
public sealed class IWannerLevelColorSettings : ScriptableObject
{
    #region Serialized Fields

    /// <summary>
    /// 当前小关 Ground、Trap、Spring 与 Button 场景 SpriteRenderer 共享的颜色。
    /// </summary>
    [Header("场景颜色")]
    [SerializeField] private Color groundColor = Color.white;

    /// <summary>
    /// 当前小关 Camera 的 Solid Color 背景色；不会修改 Camera Clear Flags。
    /// </summary>
    [SerializeField] private Color cameraSolidColor = Color.black;

    /// <summary>
    /// 当前小关 GameplayPanel 的操作按钮与组内进度图片颜色。
    /// </summary>
    [Header("HUD 颜色")]
    [SerializeField] private Color gameplayPanelColor = Color.white;

    /// <summary>
    /// 当前小关 GameplayPanel 中 Skip 文本使用的颜色，只作用于 u_level_devil_btn_skip_txt。
    /// </summary>
    [SerializeField] private Color skipTextColor = Color.white;

    #endregion

    #region Public Properties

    /// <summary>
    /// 当前配色资产定义的 Ground、Trap、Spring 与 Button 共享颜色；仅供 IWanner 关卡运行时应用。
    /// </summary>
    public Color GroundColor => groundColor;

    /// <summary>
    /// 当前配色资产定义的 Camera Solid Color；仅供 IWanner 关卡运行时应用。
    /// </summary>
    public Color CameraSolidColor => cameraSolidColor;

    /// <summary>
    /// 当前配色资产定义的 GameplayPanel 颜色；仅供 IWanner HUD 初始化时应用。
    /// </summary>
    public Color GameplayPanelColor => gameplayPanelColor;

    /// <summary>
    /// 当前配色资产定义的 Skip 文本颜色；仅供 IWanner HUD 初始化时应用。
    /// </summary>
    public Color SkipTextColor => skipTextColor;

    #endregion
}
