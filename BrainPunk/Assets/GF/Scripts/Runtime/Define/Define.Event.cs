namespace GF
{
    public partial class Define
    {
        public class Event
        {
            // 云控拉取成功
            public const string RemoteConfigFetchSuccess = "RemoteConfigFetchSuccess";
            
            // 进入Launcher Procedure
            public const string EnterLauncherProcedure = "EnterLauncherProcedure";
            
            // 结束Launcher Procedure
            public const string ExitLauncherProcedure = "ExitLauncherProcedure";
            
            //当uiView Enter或者onRefresh 时发送,表示此ui现在是在最高层显示
            public const string OnUIViewShowAtTop = "OnUIViewShowAtTop";
            
            //游戏内部切换语言
            public static string CHANGE_LANGUAGE_INTERNAL = "CHANGE_LANGUAGE_INTERNAL";
        }
    }
}