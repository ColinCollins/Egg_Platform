using System;
using System.IO;
using UnityEngine;
using YooAsset;

namespace GF
{
    /// <summary>
    /// Head-Tail 异或加密
    /// </summary>
    public class EncryptoHTXOR : IEncryptionServices
    {
        private static byte[] XORBytes(byte[] bytes, byte hKey = 0, byte tKey = 0)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i < bytes.Length / 4)
                {
                    bytes[i] ^= hKey;    
                }else
                {
                    bytes[i] ^= tKey;
                }
            }

            return bytes;
        }
        /// <summary>
        /// 加密文件
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <param name="hKey">头部密钥</param>
        /// <param name="tKey">尾部秘钥</param>
        /// <returns></returns>
        public static byte[] Encrypt(string path, byte hKey = 0, byte tKey = 0)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                XORBytes(bytes, hKey, tKey);
                return bytes;
            }
            catch(Exception)
            {
                Debug.Log($"Encrypt {path} failed");
            }

            return null;
        }
        
        public EncryptResult Encrypt(EncryptFileInfo fileInfo)
        {
            EncryptResult result = new EncryptResult();
            byte[] encryptedData = Encrypt(fileInfo.FilePath, Define.Crypto.HKEY, Define.Crypto.TKEY);
            result.Encrypted = true;
            result.EncryptedData = encryptedData;
            return result;
        }
    }
}