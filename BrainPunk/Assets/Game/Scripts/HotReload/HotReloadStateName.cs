namespace Game.HotReload
{
    /// <summary>
    /// 热更新状态名称常量（用于 FSM）
    /// </summary>
    public class HotReloadStateName
    {
        public const string INITIALIZE_PACKAGE = "INITIALIZE_PACKAGE";
        public const string REQUEST_PACKAGE_VERSION = "REQUEST_PACKAGE_VERSION";
        public const string UPDATE_PACKAGE_MANIFEST = "UPDATE_PACKAGE_MANIFEST";
        public const string CREATE_DOWNLOADER = "CREATE_DOWNLOADER";
        public const string DOWNLOAD_PACKAGE_FILES = "DOWNLOAD_PACKAGE_FILES";
        public const string DOWNLOAD_PACKAGE_OVER = "DOWNLOAD_PACKAGE_OVER";
        public const string CLEAR_CACHE_BUNDLE = "CLEAR_CACHE_BUNDLE";
        public const string START_GAME = "START_GAME";
    }
}
