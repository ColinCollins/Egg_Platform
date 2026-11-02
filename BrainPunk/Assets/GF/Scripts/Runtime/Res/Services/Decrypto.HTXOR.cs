using System;
using System.IO;
using UnityEngine;
using YooAsset;

namespace GF
{
    /// <summary>
    /// Head-Tail 异或加密
    /// </summary>
    public class DecryptoHTXOR : IDecryptionServices
    {
        private static byte[] XORBytes(byte[] bytes, byte hKey = 0, byte tKey = 0)
        {
            long mscnd1 = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
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
            long mscnd2 = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            Debug.Log($"解密耗时 = {mscnd2 - mscnd1} ms");

            return bytes;
        }

        /// <summary>
        /// 同步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundle IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            MemoryStream msDecrypt = null;
            
            using (FileStream fsDecrypt = new FileStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                byte[] buffer = new byte[(int)fsDecrypt.Length];
                fsDecrypt.Read(buffer, 0, buffer.Length);
                buffer = XORBytes(buffer, Define.Crypto.HKEY, Define.Crypto.TKEY);
                msDecrypt = new MemoryStream(buffer);
            }
            
            managedStream = msDecrypt;
            return AssetBundle.LoadFromStream(managedStream, fileInfo.ConentCRC, GetManagedReadBufferSize());
        }

        /// <summary>
        /// 异步方式获取解密的资源包对象
        /// 注意：加载流对象在资源包对象释放的时候会自动释放
        /// </summary>
        AssetBundleCreateRequest IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo, out Stream managedStream)
        {
            MemoryStream msDecrypt = null;
            
            using (FileStream fsDecrypt = new FileStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                byte[] buffer = new byte[(int)fsDecrypt.Length];
                fsDecrypt.Read(buffer, 0, buffer.Length);
                buffer = XORBytes(buffer, Define.Crypto.HKEY, Define.Crypto.TKEY);
                msDecrypt = new MemoryStream(buffer);
            }
            
            managedStream = msDecrypt;
            return AssetBundle.LoadFromStreamAsync(managedStream, fileInfo.ConentCRC, GetManagedReadBufferSize());
        }

        private static uint GetManagedReadBufferSize()
        {
            return 1024;
        }
        
        /// <summary>
        /// 从文件解密到Stream
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Stream DecryptStream(string path)
        {
            MemoryStream msDecrypt = null;

            try
            {
                using (FileStream fsDecrypt = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    byte[] buffer = new byte[(int)fsDecrypt.Length];
                    fsDecrypt.Read(buffer, 0, buffer.Length);
                    buffer = XORBytes(buffer, Define.Crypto.HKEY, Define.Crypto.TKEY);
                    msDecrypt = new MemoryStream(buffer);
                }
            }
            catch(Exception e)
            {
                LogKit.E($"DecryptStream {path} failed, {e}");
            }

            return msDecrypt;
        }
    }
}