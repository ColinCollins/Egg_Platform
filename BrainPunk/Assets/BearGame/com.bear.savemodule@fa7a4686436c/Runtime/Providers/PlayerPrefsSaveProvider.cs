using System.Threading.Tasks;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// PlayerPrefs 存储提供者
    /// </summary>
    public class PlayerPrefsSaveProvider : ISaveProvider
    {
        public StorageType StorageType => StorageType.PlayerPrefs;
        
        private const string KEY_PREFIX = "SaveData_";
        
        public bool Save(string key, string data)
        {
            try
            {
                string fullKey = KEY_PREFIX + key;
                PlayerPrefs.SetString(fullKey, data);
                PlayerPrefs.Save();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PlayerPrefs Save failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        public Task<bool> SaveAsync(string key, string data)
        {
            return Task.FromResult(Save(key, data));
        }
        
        public string Load(string key)
        {
            try
            {
                string fullKey = KEY_PREFIX + key;
                if (PlayerPrefs.HasKey(fullKey))
                {
                    return PlayerPrefs.GetString(fullKey);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PlayerPrefs Load failed for key {key}: {ex.Message}");
                return null;
            }
        }
        
        public Task<string> LoadAsync(string key)
        {
            return Task.FromResult(Load(key));
        }
        
        public bool HasKey(string key)
        {
            string fullKey = KEY_PREFIX + key;
            return PlayerPrefs.HasKey(fullKey);
        }
        
        public bool Delete(string key)
        {
            try
            {
                string fullKey = KEY_PREFIX + key;
                if (PlayerPrefs.HasKey(fullKey))
                {
                    PlayerPrefs.DeleteKey(fullKey);
                    PlayerPrefs.Save();
                    return true;
                }
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PlayerPrefs Delete failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        public bool DeleteAll()
        {
            try
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PlayerPrefs DeleteAll failed: {ex.Message}");
                return false;
            }
        }
    }
}

