namespace Game.Play
{
    /// <summary>
    /// 游戏状态名称常量（用于 FSM）
    /// </summary>
    public class GamePlayStateName
    {
        public const string START = "START";
        public const string PLAYING = "PLAYING";
        public const string PAUSE = "PAUSE";
        public const string SUCCESS = "SUCCESS";
        public const string FAILED = "FAILED";
    }
}

