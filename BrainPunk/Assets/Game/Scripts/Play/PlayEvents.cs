using Bear.EventSystem;
using Config;
using Config.Game;
using GameCommon;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Events
{
    #region Playing
    // 角色控制，向右
    public class PlayerRightMoveEvent : EventBase
    {

    }

    // 角色控制，向左

    public class PlayerLeftMoveEvent : EventBase
    {

    }

    // 取消移动操作
    public class PlayerMoveCancelEvent : EventBase
    {

    }

    // 角色控制，跳跃

    public class PlayerJumpEvent : EventBase
    {

    }

    // 角色下潜

    public class PlayerDiveEvent : EventBase<bool>
    {
        public bool isDown => Field1;
    }

    // 暂停游戏

    public class GamePauseEvent : EventBase
    {

    }

    // 重置游戏
    public class GameResetEvent : EventBase<GameResetType>
    {
        public GameResetType Type => Field1;
    }

    /// <summary>
    /// Draw 玩法的重置请求。
    /// 用途：保持 Draw HUD、关卡失败来源与 DrawPlayCtrl 的重置链和主线结构一致，
    /// 同时避免触发只面向主线的 GameResetEvent 广告、统计和关卡状态订阅。
    /// </summary>
    public class DrawGameResetEvent : EventBase<GameResetType>
    {
        /// <summary>本次重置是玩家手动触发还是关卡失败触发。</summary>
        public GameResetType Type => Field1;
    }

    /// <summary>
    /// IWanner 玩法的当前小关重置请求。
    /// 用途：让 IWanner HUD 与活动 IWannerPlayCtrl 沿用 DrawGame 的玩法专属 Reset 事件边界，
    /// 避免进入只面向主线的 GameResetEvent 统计和关卡状态链。
    /// </summary>
    public class IWannerGameResetEvent : EventBase<GameResetType>
    {
        /// <summary>本次重置是玩家手动触发还是关卡失败触发。</summary>
        public GameResetType Type => Field1;
    }

    /// <summary>
    /// Draw 画线长度、剩余墨水或星级发生变化。
    /// 用途：由 DrawLineFeature 派发，DrawGame_GamePlayPanel 直接订阅并刷新 HUD。
    /// </summary>
    public class DrawInkStateChangedEvent : EventBase<DrawInkState>
    {
        /// <summary>当前帧只读的画线长度与墨水状态快照。</summary>
        public DrawInkState InkState => Field1;
    }

    /// <summary>
    /// Draw 关卡倒计时的 HUD 显示状态发生变化。
    /// 用途：由关卡内 DrawLevelCountdownCtrl 派发，DrawGame_GamePlayPanel 只负责显示最后五秒。
    /// </summary>
    public class DrawGameCountdownChangedEvent : EventBase<bool, int>
    {
        /// <summary>倒计时文本当前是否应显示。</summary>
        public bool IsVisible => Field1;

        /// <summary>HUD 应显示的剩余整数秒；隐藏事件中固定为 0。</summary>
        public int Seconds => Field2;
    }

    /// <summary>
    /// Draw 玩法已经成功获取当前关卡提示。
    /// 用途：由 DrawGame_GameTipsPopup 通知活动中的 DrawPlayCtrl 记录运行时状态并显示 TipsObject。
    /// </summary>
    public class DrawGameHintEvent : EventBase
    {
    }

    /// <summary>
    /// 重玩当前关卡
    /// </summary>
    public class GameReplayEvent : EventBase
    {

    }

    // 开始游戏
    public class GameStartPlayEvent : EventBase
    {

    }

    // 用于触发游戏场景中的一些特殊事件
    public class OnTiggerItemEvent : EventBase<int>
    {
        public int EventId => Field1;
    }

    // 触发陷阱
    public class OnTriggerTrapEvent : EventBase { }

    // 失败区域
    public class OnTriggerFailAreaEvent : EventBase { }

    // 气泡提示功能
    public class ShowHintBubbleEvent : EventBase<LevelData>
    {
        public LevelData Data => Field1;
    }

    #endregion

    #region GameState 

    public class SwitchGameStateEvent : EventBase<string>
    {
        public string NewState => Field1;
    }

    /// <summary>
    /// 进入对应关卡
    /// </summary>
    public class EnterLevelEvent : EventBase<LevelSort>
    {
        public LevelSort Data => Field1;
    }

    public class EnterNextLevelEvent : EventBase
    {

    }

    #endregion 

    #region UI

    // gamepanel pause 状态恢复事件
    public class GameResumeEvent : EventBase { }

    public class GamePlayPanelFadeInEvent : EventBase { }

    public class MusicToggleEvent : EventBase<bool>
    {
        public bool isOn => Field1;
    }

    public class SfxToggleEvent : EventBase<bool>
    {
        public bool isOn => Field1;
    }

    public class VibrationToggleEvent : EventBase<bool>
    {
        public bool isOn => Field1;
    }

    public class SwitchObjActiveEvent : EventBase<bool>
    {
        public bool isShow => Field1;
    }

    // 展示遮罩    
    public class GamePlayPanelSwitchBlockEvent : EventBase<bool>
    {
        public bool IsShow => Field1;
    }

    // tips 引导提示
    public class GamePlayPanelSwitchTipsEvent : EventBase<bool>
    {
        public bool IsShow => Field1;
    }

    /// <summary>
    /// 切换语言
    /// </summary>
    public class SwitchLanguageEvent : EventBase<string>
    {

    }

    /// <summary>
    /// 内购恢复成功后
    /// </summary>
    public class OnRestoreSuccessEvent : EventBase { }


    #endregion

    #region Bag

    /// <summary>
    /// 道具数量更新事件
    /// </summary>
    public class UpdatePropEvent : EventBase<GameProps, int, int>
    {
        public GameProps Prop => Field1;

        public int OldCount => Field2;

        public int NewCount => Field3;
    }

    public class UseTipsEvent : EventBase { }

    /// <summary>
    /// 购买道具事件，通过 rewards 控制发放奖励
    /// </summary>
    public class PurchaseEvent : EventBase<string, Dictionary<GameProps, int>>
    {
        public string GoodsId => Field1;
        public Dictionary<GameProps, int> Rewards => Field2;
    }

    /// <summary>
    /// 明信片星星被收集后通知玩法 HUD：参数为场景星星的 worldPosition，供 GamePlayPanel 生成 UI 表现星星并在动画后刷新星星道具块。
    /// </summary>
    public class PostcardGetStarEvent : EventBase<Vector3, bool>
    {
        /// <summary>
        /// 用途：提供本次收集到的场景星星世界坐标，GamePlayPanel 会把它换算成 UI 起点。
        /// 调用边界：仅由明信片星星收集成功并完成存档/发奖后派发，监听方不能用它反向判断存档状态。
        /// 注意事项：如果无法换算到当前 Canvas，监听方应跳过飞行动画并保留 HUD 刷新降级。
        /// </summary>
        public Vector3 StarWorldPosition => Field1;

        public bool IsJumpOut => Field2;
    }

    #endregion 

    #region AD

    // 播放激励
    public class PlayRewardADEvent : EventBase
    {

    }


    // 播放插屏
    public class PlayInterstitialAdEvent : EventBase
    {

    }

    #endregion 
}
