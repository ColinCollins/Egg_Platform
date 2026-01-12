using Bear.EventSystem;
using Config;
using UnityEngine;

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

    // 暂停游戏

    public class GamePauseEvent : EventBase
    {
        
    }


    // 重置游戏

    public class GameResetEvent : EventBase
    {
        
    }

    // 暂停游戏,设置面板

    public class GameSettingEvent : EventBase
    {
        
    }

    // 弹出提示弹窗
    public class GameTipsEvent : EventBase
    {
        
    }

    // 开始游戏
    public class GameStartPlayEvent : EventBase
    {
        
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
    public class EnterLevelEvent : EventBase<LevelData>
    {
        public LevelData Data => Field1;
    }

    public class EnterNextLevelEvent : EventBase
    {
        
    }

    #endregion 

    #region UI

    // gamepanel pause 状态恢复事件
    public class GameResumeEvent : EventBase {}

    #endregion 
}