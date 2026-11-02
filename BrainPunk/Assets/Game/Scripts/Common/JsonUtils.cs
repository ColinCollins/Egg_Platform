using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class JsonUtils
{
    public static bool TryGetValue<T>(string json, string key, out T value)
    {
        value = default;

        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            var jObject = JObject.Parse(json);

            if (!jObject.TryGetValue(key, StringComparison.Ordinal, out JToken token) || token == null)
            {
                return false;
            }

            value = token.ToObject<T>();
            return true;
        }
        catch (JsonReaderException e)
        {
            Debug.LogError($"Json parse error, key={key}, json={json}\n{e}");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Json get value error, key={key}, json={json}\n{e}");
            return false;
        }
    }
}

