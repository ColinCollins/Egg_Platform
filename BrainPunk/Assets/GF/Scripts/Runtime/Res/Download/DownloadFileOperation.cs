using System;
using YooAsset;

namespace GF
{
    /// <summary>
    /// 文件下载异步操作
    /// </summary>
    public class DownloadFileOperation: GameAsyncOperation
    {
        private enum ESteps
        {
            None,
            Waiting,
            Downloading,
            Done,
        }
        
        private string _url;
        private string _fileSavePath;
        private int _timeout;
        private int _retryTimes = 3;
        private ESteps _steps = ESteps.None;
        private int _currentRetryTimes = 0;
        private UnityWebFileRequester _downloader;
        public Action<float> ProgressCallback;

        public DownloadFileOperation(string url, string fileSavePath, int retryTimes = 3, int timeout = 60)
        {
            _url= url;
            _fileSavePath = fileSavePath;
            _timeout = timeout;
            _retryTimes = retryTimes;
        }
        
        protected override void OnStart()
        {
            _steps = ESteps.Waiting;
            _currentRetryTimes = 1;
        }

        protected override void OnUpdate()
        {
            if (_steps == ESteps.None || _steps == ESteps.Done)
            {
                return;
            }

            if (_steps == ESteps.Waiting && _downloader == null)
            {
                YooLogger.Log($"Beginning to download package hash file : {_url}");
                _downloader = new UnityWebFileRequester();
                _downloader.SendRequest(_url, _fileSavePath, _timeout);
                _steps = ESteps.Downloading;
            }

            _downloader.CheckTimeout();
            
            if (_steps == ESteps.Downloading)
            {
                ProgressCallback?.Invoke(_downloader.Progress());
            }
            
            if (_downloader.IsDone() == false)
            {
                return;
            }

            _steps = ESteps.Done;
            
            if (_downloader.HasError())
            {
                // 判断是否重试
                if(_currentRetryTimes < _retryTimes)
                {
                    _currentRetryTimes++;
                    LogKit.E($"重试 {_currentRetryTimes}");
                    
                    _steps = ESteps.Waiting;
                    _downloader = null;
                    return;
                }
                Status = EOperationStatus.Failed;
                Error = _downloader.GetError();
            }
            else
            {
                Status = EOperationStatus.Succeed;
            }

            _downloader.Dispose();
        }

        protected override void OnAbort()
        {
            
        }
    }
}