#nullable enable
  

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core.Analytics;
using Guru.SDK.Framework.Utils.Analytics;

/// <summary>
/// 打点优先级
/// </summary>
public enum EventPriority
{
    Unknown = -1,
    Emergence = 0,
    High = 5,
    Default = 10,
    Low = 15
}

/*
 * 项目组应该自己熟悉项目组的打点范围和详细的字段定义
 * 可参考测试项目的打点模版: https://docs.google.com/spreadsheets/d/1N47rXgjatRHFvzWWx0Hqv5C1D9NHHGbggi6pQ65c-zQ/edit?gid=732914073#gid=732914073
 */


// 打点代理
public partial class GameSDKService
{
    public const int LevelStartTypeNewGame = 0;
    public const int LevelStartTypeReplay = 1;
    public const int LevelStartTypeContinue = 2;
    
    public const int LevelEndTypeSuccess = 0;
    public const int LevelEndTypeFailed = 1;
    public const int LevelEndTypeExit = 2;
    
    
    /// <summary>
    /// 通用打点接口
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="param"></param>
    /// <param name="capabilities"></param>
    public void LogEvent(string eventName, Dictionary<string, object?>? param = null, AnalyticsCapabilities? capabilities = null )
    {
        AppEventOptions options = null;
        if (capabilities != null)
        {
            options = new AppEventOptions(capabilities: capabilities);
        }
        LogEventWithOptions(eventName, param, options);
    }
    
    /// <summary>
    /// 进阶打点接口
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="param"></param>
    /// <param name="options"></param>
    public void LogEventWithOptions(string eventName, Dictionary<string, object?>? param = null, AppEventOptions? options = null)
    {
        GuruAnalytics.Instance.LogEvent(eventName, param, options);
    }

    /// <summary>
    /// 用户属性上报
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    public void SetUserProperty(string propertyName, string value)
    {
        GuruAnalytics.Instance.SetUserProperty(propertyName, value).Forget();
    }

    /// <summary>
    /// 屏幕上报
    /// </summary>
    /// <param name="screenName"></param>
    public void SetScreen(string screenName)
    {
        GuruAnalytics.Instance.SetScreen(screenName);
    }
}
