using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// Json 文件存储提供者
    /// </summary>
    public class JsonSaveProvider : ISaveProvider
    {
        public StorageType StorageType => StorageType.Json;
        
        private readonly string _saveDirectory;
        
        public JsonSaveProvider()
        {
            _saveDirectory = Path.Combine(Application.persistentDataPath, "SaveData");
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }
        }
        
        private string GetFilePath(string key)
        {
            return Path.Combine(_saveDirectory, $"{key}.json");
        }
        
        public bool Save(string key, string data)
        {
            try
            {
                string filePath = GetFilePath(key);
                File.WriteAllText(filePath, data);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json Save failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> SaveAsync(string key, string data)
        {
            try
            {
                string filePath = GetFilePath(key);
                await File.WriteAllTextAsync(filePath, data);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json SaveAsync failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        public string Load(string key)
        {
            try
            {
                string filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json Load failed for key {key}: {ex.Message}");
                return null;
            }
        }
        
        public async Task<string> LoadAsync(string key)
        {
            try
            {
                string filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    return await File.ReadAllTextAsync(filePath);
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json LoadAsync failed for key {key}: {ex.Message}");
                return null;
            }
        }
        
        public bool HasKey(string key)
        {
            string filePath = GetFilePath(key);
            return File.Exists(filePath);
        }
        
        public bool Delete(string key)
        {
            try
            {
                string filePath = GetFilePath(key);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json Delete failed for key {key}: {ex.Message}");
                return false;
            }
        }
        
        public bool DeleteAll()
        {
            try
            {
                if (Directory.Exists(_saveDirectory))
                {
                    Directory.Delete(_saveDirectory, true);
                    Directory.CreateDirectory(_saveDirectory);
                    return true;
                }
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Json DeleteAll failed: {ex.Message}");
                return false;
            }
        }
    }
}

