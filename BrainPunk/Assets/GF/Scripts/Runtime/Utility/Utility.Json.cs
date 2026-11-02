using System;

namespace GF
{
    public static partial class Utility
    {
        public static partial class Json
        {
            public static string Serialize(object value)
            {
                string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                return jsonData;
            }

            public static T Deserialize<T>(string value)
            {
                try
                {
                    T obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value);
                    return obj;
                }
                catch (Exception e)
                {
                    LogKit.E($"Json Deserialize Error : { e.Message}");
                    return default(T);
                }
            }
        }
    }
}