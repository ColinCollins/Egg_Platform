using System;
using Debug = UnityEngine.Debug;

namespace GF
{
    /// <summary>
    /// 内置logger，兼容了YooAsset.ILogger
    /// todo:LogLevel,LogFile
    /// </summary>
    public class DefaultLogger: ILogger,YooAsset.ILogger
    {
        #region YooAsset

        public void Log(string message)
        {
            Debug.Log(message);
        }

        public void Warning(string message)
        {
            Debug.LogWarning(message);
        }

        public void Error(string message)
        {
            Debug.LogError(message);
        }

        public void Exception(Exception exception)
        {
            Debug.LogException(exception);
        }

        #endregion
        
    }
}