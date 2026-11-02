using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace GF
{
    public class HttpKit
    {
        private class SimpleCertificate : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return true;
            }
        }

        private readonly SimpleCertificate _sslCert = new SimpleCertificate();

        private Dictionary<string, string> _defaultHeaders = new Dictionary<string, string>();

        public void AddHeader(string key, string value)
        {
            _defaultHeaders[key] = value;
        }

        public void RemoveHeader(string key)
        {
            if (_defaultHeaders.ContainsKey(key))
            {
                _defaultHeaders.Remove(key);
            }
        }

        public async UniTask<JObject> GetJsonWithDecrypto(string url, Dictionary<string, string> queryDict = null,
            Dictionary<string, string> headers = null)
        {
            JObject result = await GetJson(url, queryDict, headers);

            if (result["err_code"]?.ToString() == "0")
            {
                string data = result["data"]?.ToString();
                data = CryptoAES.Decrypt(data);
                LogKit.I($"decrypt data: {data}");
                JObject json = JObject.Parse(data);
                result["data"] = json;
            }

            return result;
        }

        public async UniTask<JObject> GetJson(string url, Dictionary<string, string> queryDict = null,
            Dictionary<string, string> headers = null)
        {
            string result = await GetString(url, queryDict, headers);
            
            if (!string.IsNullOrEmpty(result))
            {
                JObject json = JObject.Parse(result);
                return json;
            }else
            {
                throw new Exception($"GetJson {url} failed");
            }
        }

        public async UniTask<string> GetString(string url, Dictionary<string, string> queryDict = null,
            Dictionary<string, string> headers = null)
        {
            UnityWebRequest request = await Get(url, queryDict, headers);
            if (request == null || request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            string result = request.downloadHandler.text;
            request.Dispose();
            return result;
        }

        public async UniTask<byte[]> GetBinary(string url, Dictionary<string, string> queryDict = null,
            Dictionary<string, string> headers = null)
        {
            UnityWebRequest request = await Get(url, queryDict, headers);
            if (request == null || request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            byte[] result = request.downloadHandler.data;
            request.Dispose();
            return result;
        }

        public async UniTask<UnityWebRequest> Get(string url, Dictionary<string, string> queryDict = null,
            Dictionary<string, string> headers = null)
        {
            try
            {
                string param = UrlAppendParams(url, queryDict);
                UnityWebRequest req = UnityWebRequest.Get(param);

                req.SetRequestHeader("Content-type", "application/json");
                foreach (var kvp in _defaultHeaders)
                {
                    req.SetRequestHeader(kvp.Key, kvp.Value);
                }
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        req.SetRequestHeader(kvp.Key, kvp.Value);
                    }
                }

                req.certificateHandler = _sslCert;
                req.disposeDownloadHandlerOnDispose = true;
                LogKit.I($"Http: Send==>{param}");
                req.timeout = 30;
                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogKit.E(req.error);
                }
                LogKit.I($"Http: Receive==>{url} ,{req.downloadHandler?.text}");
                return req;
            }
            catch (Exception e)
            {
                LogKit.E(e.ToString());
            }

            return null;
        }

        public async UniTask<UnityWebRequest> Post(string url, Dictionary<string, string> postData,
            Dictionary<string, string> headers)
        {
            try
            {
                //先使用put方式再手动改成post，不然会有编码错误
                // var req = UnityWebRequest.Post(url, postData);
                if (postData == null)
                {
                    return null;
                }

                string postDataStr = Utility.Json.Serialize(postData);
                LogKit.I($"Http: Send==>{url} ,{postDataStr}");
                var req = UnityWebRequest.Put(url, postDataStr);
                req.method = UnityWebRequest.kHttpVerbPOST;

                req.certificateHandler = _sslCert;
                req.disposeDownloadHandlerOnDispose = true;

                req.SetRequestHeader("Content-type", "application/json");
                foreach (var kvp in _defaultHeaders)
                {
                    req.SetRequestHeader(kvp.Key, kvp.Value);
                }
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        req.SetRequestHeader(kvp.Key, kvp.Value);
                    }
                }

                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    LogKit.E(req.error);
                }
                LogKit.I($"Http: Receive==>{url} ,{req.downloadHandler?.text}");
                return req;
            }
            catch (Exception e)
            {
                LogKit.E(e.ToString());
            }

            return null;
        }

        public async UniTask<JObject> PostJsonWithDecrypto(string url, Dictionary<string, string> postData, Dictionary<string, string> headers = null)
        {
            Dictionary<string, string> queryDictEncryto = new Dictionary<string, string>();
            if (postData != null)
            {
                queryDictEncryto["data"] = CryptoAES.Encrypt(Utility.Json.Serialize(postData));
            }

            JObject result = await PostJson(url, queryDictEncryto, headers);

            if (result["err_code"]?.ToString() == "0")
            {
                string data = result["data"]?.ToString();
                data = CryptoAES.Decrypt(data);
                LogKit.I($"decrypt data: {data}");
                JObject json = JObject.Parse(data);
                result["data"] = json;
            }

            return result;
        }
        
        public async UniTask<JObject> PostJson(string url, Dictionary<string, string> postData, Dictionary<string, string> headers = null)
        {
            string result = await PostString(url, postData, headers);
            
            if (!string.IsNullOrEmpty(result))
            {
                JObject json = JObject.Parse(result);
                return json;
            }else
            {
                throw new Exception($"GetJson {url} failed");
            }
        }

        public async UniTask<string> PostString(string url, Dictionary<string, string> postData, Dictionary<string, string> headers = null)
        {
            UnityWebRequest request = await Post(url, postData, headers);
            if (request == null || request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            string result = request.downloadHandler.text;
            request.Dispose();
            return result;
        }

        public async UniTask<byte[]> PostBinary(string url, Dictionary<string, string> postData, Dictionary<string, string> headers)
        {
            UnityWebRequest request = await Post(url, postData, headers);
            if (request == null || request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            byte[] result = request.downloadHandler.data;
            request.Dispose();
            return result;
        }

        private string UrlAppendParams(string url, Dictionary<string, string> queryDict)
        {
            if (queryDict == null)
            {
                return url;
            }

            var p = new ArrayList();
            foreach (var kvp in queryDict)
            {
                var k = kvp.Key;
                var v = kvp.Value;

                p.Add(UnityWebRequest.EscapeURL(k) + "=" + UnityWebRequest.EscapeURL(v));
            }

            if (p.Count <= 0)
            {
                return url;
            }

            url += (url.IndexOf("?") != -1 ? "&" : "?") + (string.Join("&", p.ToArray()));

            return url;
        }
    }
}