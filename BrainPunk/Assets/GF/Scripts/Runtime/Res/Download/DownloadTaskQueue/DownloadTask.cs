using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GF
{
    /// <summary>
    /// 下载任务优先级
    /// </summary>
    public enum EDownloadTaskPriority
    {
        VeryLow = 0,
        Low,
        Normal,
        High,
        VeryHigh
    }

    /// <summary>
    /// 下载任务状态
    /// </summary>
    public enum EDownloadTaskStatus
    {
        DownloadWaitting = 0,       //等待下载
        Downloading,                //下载中
        DownloadCancel,             //下载取消
        DownloadSuccess,            //下载完成
        DownloadFailed,             //下载失败
    }

    /// <summary>
    /// 下载任务
    /// </summary>
    public class DownloadTask
    {
        //包名
        private string _packageName;
        
        //firebase storage generation
        private string _generation;
        
        //yooasset版本号
        private string _version;
        
        //下载优先级
        private EDownloadTaskPriority _priority;

        //下载状态
        private EDownloadTaskStatus _status;
        
        //成功回调 参数为packageName
        private Action<string> _onSuccess;
        
        //失败回调 参数为packageName
        private Action<string> _onFail;
        
        //下载进度回调 参数为packageName和进度
        private Action<string, float> _onProgress;

        //任务所属的任务组
        private DownloadTaskGroup _taskGroup;
        
        public string PackageName => _packageName;
        
        public string Generation => _generation;
        
        public string Version => _version;
        public EDownloadTaskStatus Status => _status;
        public EDownloadTaskPriority Priority
        {
            get { return _priority; }
            set { _priority = value; }
        }
        public Action<string> OnSuccess => _onSuccess;
        public Action<string> OnFail => _onFail;
        public Action<string, float> OnProgress => _onProgress;

        public DownloadTaskGroup TaskGroup
        {
            get { return _taskGroup; }
            set { _taskGroup = value; }
        }

        public DownloadTask(string packageName, string generation, string version = "", EDownloadTaskPriority priority = EDownloadTaskPriority.Normal, Action<string> onSuccess = null, Action<string> onFail = null, Action<string, float> onProgress = null)
        {
            _packageName = packageName;
            _generation = generation;
            _version = version;
            _priority = priority;
            _status = EDownloadTaskStatus.DownloadWaitting;
            _onSuccess = onSuccess;
            _onFail = onFail;
            _onProgress = onProgress;
            _taskGroup = null;
        }
        
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(_packageName) && !string.IsNullOrEmpty(_generation);
        }
        
        /// <summary>
        /// 在已经存在的任务上附加回调函数
        /// </summary>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <param name="onProgress"></param>
        public void AttachCallback(Action<string> onSuccess, Action<string> onFail, Action<string, float> onProgress)
        {
            if (null != onSuccess)
            {
                _onSuccess = null == _onSuccess ? onSuccess : _onSuccess + onSuccess;
            }

            if (null != onFail)
            {
                _onFail = null == _onFail ? onFail : _onFail + onFail;
            }

            if (null != _onProgress)
            {
                _onProgress = null == _onProgress ? onProgress : _onProgress + onProgress;
            }
        }

        public async UniTask Start()
        {
            if (!IsValid())
            {
                LogKit.E("DownloadTask can not start, it is invalid!");
                return;
            }
            
            //只有处于等待下载和取消下载状态的任务可以开启下载
            if (!(EDownloadTaskStatus.DownloadWaitting == _status || EDownloadTaskStatus.DownloadCancel == _status))
            {
                LogKit.E($"DownloadTask can not start, it in not correct status: {_status}");
                return;
            }
            
            _status = EDownloadTaskStatus.Downloading;
            await PackageDownloadMgr.Instance.DownLoadPackage(_packageName, _generation, _version, () =>
            {
                if (EDownloadTaskStatus.DownloadCancel == _status)
                {
                    return;
                }

                _status = EDownloadTaskStatus.DownloadSuccess;
                _taskGroup?.OnTaskDownloadFinish(this);
                _onSuccess?.Invoke(_packageName);
                LogKit.I($"下载完成 {_packageName}  {_priority}");
            }, () =>
            {
                if (EDownloadTaskStatus.DownloadCancel == _status)
                {
                    return;
                }

                _status = EDownloadTaskStatus.DownloadFailed;
                _taskGroup?.OnTaskDownloadFinish(this);
                if (!DownLoadTaskQueueMgr.Instance.CheckCanReDowndload(_priority))
                {
                    _onFail?.Invoke(_packageName);
                }
            }, (progress) =>
            {
                _onProgress?.Invoke(_packageName, progress);
            });
            
        }

        /// <summary>
        /// 取消任务
        /// </summary>
        public void Cancel()
        {
            if (EDownloadTaskStatus.Downloading == _status)
            {
                PackageDownloadMgr.Instance.AbortDownload(_packageName);
                _status = EDownloadTaskStatus.DownloadCancel;
            }
        }

        /// <summary>
        /// 重新激活下载失败的任务 并且设置到指定优先级
        /// </summary>
        public bool ReActive(EDownloadTaskPriority priority = EDownloadTaskPriority.Normal)
        {
            if (EDownloadTaskStatus.DownloadFailed != _status)
            {
                return false;
            }
            
            _priority = priority;
            _status = EDownloadTaskStatus.DownloadWaitting;
            return true;
        }

    }
}