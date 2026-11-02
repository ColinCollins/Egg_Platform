using System;
using System.Reflection;

namespace Game.ConfigModule
{
    /// <summary>
    /// 用途：集中解析 Luban ParamBase 派生参数，避免业务界面重复持有反射和基础类型转换逻辑。
    /// 调用边界：仅用于读取已生成配置对象中的参数值，不负责查表、发奖、存档或资源加载。
    /// 注意事项：优先识别当前已生成的 IntParam / StringParam；新增派生参数时会降级读取公开 Value 字段。
    /// </summary>
    public static class BaseParamUtils
    {
        #region Value Access

        /// <summary>
        /// 用途：从 Luban ParamBase 派生对象中读取实际 Value。
        /// 调用边界：配置值已经完成 Luban 反序列化后调用；paramBase 为空或没有可读取 Value 时返回 false。
        /// 注意事项：返回值类型由具体配置派生类决定，调用方仍需按业务语义继续转换。
        /// </summary>
        public static bool TryGetValue(Config.ParamBase paramBase, out object value)
        {
            value = null;
            if (paramBase == null)
                return false;

            switch (paramBase)
            {
                case Config.IntParam intParam:
                    value = intParam.Value;
                    return true;
                case Config.StringParam stringParam:
                    value = stringParam.Value;
                    return value != null;
            }

            var valueField = paramBase.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public);
            if (valueField == null)
                return false;

            value = valueField.GetValue(paramBase);
            return value != null;
        }

        #endregion

        #region Primitive Parsing

        /// <summary>
        /// 用途：读取明确由 Luban IntParam 承载的整数值。
        /// 调用边界：适用于 Shop 奖励等同时允许 IntParam 与 StringParam 的配置，只有 IntParam 返回 true。
        /// 注意事项：不会把数字形式的 StringParam（例如 Skin ID）转换为奖励数量。
        /// </summary>
        public static bool TryGetIntParam(Config.ParamBase paramBase, out int value)
        {
            if (paramBase is Config.IntParam intParam)
            {
                value = intParam.Value;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// 用途：把配置参数或普通对象转换为 int 数量。
        /// 调用边界：适用于奖励数量等整数语义配置；无法安全转换时返回 false。
        /// 注意事项：float / double 按向下取整处理，保持通用奖励弹窗既有数量解析口径。
        /// </summary>
        public static bool TryGetInt(object param, out int value)
        {
            switch (param)
            {
                case Config.ParamBase paramBase when TryGetValue(paramBase, out var innerValue):
                    return TryGetInt(innerValue, out value);
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue <= int.MaxValue && longValue >= int.MinValue:
                    value = (int)longValue;
                    return true;
                case float floatValue:
                    value = (int)Math.Floor(floatValue);
                    return true;
                case double doubleValue:
                    value = (int)Math.Floor(doubleValue);
                    return true;
                case string stringValue:
                    return int.TryParse(stringValue, out value);
                default:
                    value = 0;
                    return false;
            }
        }

        /// <summary>
        /// 用途：把配置参数或普通对象转换为字符串。
        /// 调用边界：适用于 skinId、资源 id 等字符串语义配置；空对象返回 false。
        /// 注意事项：非字符串对象会使用 ToString()，调用方需要自行判断业务 id 是否有效。
        /// </summary>
        public static bool TryGetString(object param, out string value)
        {
            switch (param)
            {
                case Config.ParamBase paramBase when TryGetValue(paramBase, out var innerValue):
                    return TryGetString(innerValue, out value);
                case string stringValue:
                    value = stringValue;
                    return value != null;
                default:
                    value = param?.ToString();
                    return value != null;
            }
        }

        #endregion
    }
}
