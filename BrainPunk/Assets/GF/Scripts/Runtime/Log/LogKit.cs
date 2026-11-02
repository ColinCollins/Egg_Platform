using System;
using System.Diagnostics;

namespace GF
{
    public static class LogKit
    {
        private static ILogger _logger;

        private static ILogger Logger
        {
            set { _logger = value; }

            get
            {
                if (_logger == null)
                {
                    _logger = new DefaultLogger();
                }

                return _logger;
            }
        }

        public static void SetLogger(ILogger loggerIns = null)
        {
            if (loggerIns == null)
            {
                _logger = new DefaultLogger();
                return;
            }
            _logger = loggerIns;
        }

        /// <summary>
        /// Info日志
        /// </summary>
        /// <param name="msg">消息</param>
        [Conditional("ENABLE_LOG")]
        public static void I(object msg)
        {
            Logger.Log(msg.ToString());
        }

        /// <summary>
        /// Warning日志
        /// </summary>
        /// <param name="msg">消息</param>
        [Conditional("ENABLE_LOG")]
        public static void W(object msg)
        {
            Logger.Warning(msg.ToString());
        }

        /// <summary>
        /// Error日志
        /// </summary>
        /// <param name="msg">消息</param>
        [Conditional("ENABLE_LOG")]
        public static void E(object msg)
        {
            Logger.Error(msg.ToString());
        }

        /// <summary>
        /// Exception日志
        /// </summary>
        /// <param name="exception">Exception</param>
        [Conditional("ENABLE_LOG")]
        public static void Exception(Exception exception)
        {
            Logger.Exception(exception);
        }
    }
}