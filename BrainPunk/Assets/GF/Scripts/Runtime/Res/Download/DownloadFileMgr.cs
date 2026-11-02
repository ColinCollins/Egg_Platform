using System.Collections.Generic;
using YooAsset;

namespace GF
{
    /// <summary>
    /// 文件下载管理器
    /// </summary>
    public class DownloadFileMgr : Singleton<DownloadFileMgr>
    {
        private Dictionary<string, DownloadFileOperation> _downloadOperations = new();
        
        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="url"></param>
        /// <param name="savePath"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <param name="onProgress"></param>
        /// <param name="retryTimes"></param>
        /// <param name="timeout"></param>
        public DownloadFileOperation DownloadFile(string url, string savePath, System.Action onSuccess = null, System.Action onFail = null,
            System.Action<float> onProgress = null, int retryTimes = 3, int timeout = 60)
        {
            if(_downloadOperations.TryGetValue(url, out DownloadFileOperation operation))
            {
                if(operation.Status== EOperationStatus.Succeed || operation.Status== EOperationStatus.Failed)
                {
                    _downloadOperations.Remove(url);
                }
                else
                {
                    AddCallback(operation, onSuccess, onFail, onProgress);
                    return operation;
                }
            }
            
            operation = new DownloadFileOperation(url, savePath, retryTimes, timeout);
            AddCallback(operation, onSuccess, onFail, onProgress);
            OperationSystem.StartOperation(operation);
            _downloadOperations.Add(url, operation);

            return operation;
        }

        /// <summary>
        /// 添加回调
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <param name="onProgress"></param>
        private void AddCallback(DownloadFileOperation operation, System.Action onSuccess, System.Action onFail, System.Action<float> onProgress)
        {
            operation.Completed+= (op) =>
            {
                if (op.Status == EOperationStatus.Succeed)
                {
                    onSuccess?.Invoke();
                }
                else
                {
                    onFail?.Invoke();
                }
            };

            if (onProgress != null)
            {
                operation.ProgressCallback+= onProgress;
            }
        }
    }
}