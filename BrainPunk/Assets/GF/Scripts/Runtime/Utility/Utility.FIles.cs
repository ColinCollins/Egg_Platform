using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GF
{
    public static partial class Utility
    {
        /// <summary>
        /// 文件工具
        /// 正则表达式测试地址：https://www.regexpal.com
        /// </summary>
        public static partial class Files
        {
            //从StreamingAssets拷贝到读写目录
            public static void CopyStreamingAssetsFile(string form, string dest)
            {
                if (Application.platform == RuntimePlatform.Android)
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(Application.streamingAssetsPath + "/" + form))
                    {
                        request.timeout = 3;
                        request.downloadHandler = new DownloadHandlerFile(dest);//直接将文件下载到外存
                        request.SendWebRequest();

                        //等待下载完成
                        while (!request.isDone){}

                        request.Abort();
                        //默认值是true，调用该方法不需要设置Dispose()，Unity就会自动在完成后调用Dispose()释放资源。
                        request.disposeDownloadHandlerOnDispose = true;
                        request.Dispose();
                    }
                }
                else
                {
                    File.Copy(Application.streamingAssetsPath + "/" + form, dest, true);
                }
            }
            
            public static string ReadStringByFile(string path)
            {
                StringBuilder line = new StringBuilder();
                try
                {
                    if (!File.Exists(path))
                    {
                        LogKit.I("path dont exists ! : " + path);
                        return "";
                    }

                    StreamReader sr = File.OpenText(path);
                    line.Append(sr.ReadToEnd());

                    sr.Close();
                    sr.Dispose();
                }
                catch (Exception e)
                {
                    LogKit.E("Load text fail ! message:" + e.Message);
                }

                return line.ToString();
            }

            public static byte[] ReadBytesByFile(string path)
            {
                FileStream fileStream = null;
                try
                {
                    if (!File.Exists(path))
                    {
                        LogKit.I("path dont exists ! : " + path);
                        return null;
                    }
                    //创建文件读取流
                    fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
                    fileStream.Seek(0, SeekOrigin.Begin);
                    //创建文件长度缓冲区
                    byte[] bytes = new byte[fileStream.Length];
                    //读取文件
                    fileStream.Read(bytes, 0, (int)fileStream.Length);
                    //释放文件读取流
                    fileStream.Close();
                    fileStream.Dispose();
                    return bytes;
                }
                catch (Exception e)
                {
                    LogKit.E(e.ToString());
                    fileStream?.Dispose();
                }

                return null;
            }
            
            public static void WriteStringByFile(string path, string content)
            {
                byte[] dataByte = Encoding.GetEncoding("UTF-8").GetBytes(content);

                WriteBytesByFile(path, dataByte);
            }
            
            public static void WriteBytesByFile(string path, byte[] dataByte)
            {
                try
                {
                    string newPathDir = System.IO.Path.GetDirectoryName(path);

                    if (!Directory.Exists(newPathDir))
                    {
                        Directory.CreateDirectory(newPathDir);
                    }

                    File.WriteAllBytes(path, dataByte);
                }
                catch (Exception e)
                {
                    Debug.LogError("File Write Fail! \n" + e.Message);
                }
            }
            
            public static async UniTask WriteBytesByFileAsync(string filePath, byte[] data)
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                {
                    await fileStream.WriteAsync(data, 0, data.Length);
                }
            }
        }
    }
}
