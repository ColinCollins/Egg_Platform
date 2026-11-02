using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Bear.SaveModule
{
    /// <summary>
    /// 服务器同步提供者示例实现
    /// </summary>
    public class ServerSyncProvider : IServerSyncProvider
    {
        private readonly string _serverUrl;
        
        public ServerSyncProvider(string serverUrl)
        {
            _serverUrl = serverUrl;
        }
        
        public async Task<bool> UploadAsync(string key, string data)
        {
            try
            {
                string url = $"{_serverUrl}/save/{key}";
                using (UnityWebRequest request = UnityWebRequest.Put(url, data))
                {
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SendWebRequest();
                    
                    while (!request.isDone)
                    {
                        await Task.Yield();
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"Upload failed: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Upload exception: {ex.Message}");
                return false;
            }
        }
        
        public async Task<string> DownloadAsync(string key)
        {
            try
            {
                string url = $"{_serverUrl}/save/{key}";
                using (UnityWebRequest request = UnityWebRequest.Get(url))
                {
                    request.SendWebRequest();
                    
                    while (!request.isDone)
                    {
                        await Task.Yield();
                    }
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return request.downloadHandler.text;
                    }
                    else
                    {
                        Debug.LogError($"Download failed: {request.error}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Download exception: {ex.Message}");
                return null;
            }
        }
        
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                string url = $"{_serverUrl}/save/{key}/exists";
                using (UnityWebRequest request = UnityWebRequest.Head(url))
                {
                    request.SendWebRequest();
                    
                    while (!request.isDone)
                    {
                        await Task.Yield();
                    }
                    
                    return request.result == UnityWebRequest.Result.Success && 
                           request.responseCode == 200;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exists check exception: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                string url = $"{_serverUrl}/save/{key}";
                using (UnityWebRequest request = UnityWebRequest.Delete(url))
                {
                    request.SendWebRequest();
                    
                    while (!request.isDone)
                    {
                        await Task.Yield();
                    }
                    
                    return request.result == UnityWebRequest.Result.Success;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Delete exception: {ex.Message}");
                return false;
            }
        }
    }
}

