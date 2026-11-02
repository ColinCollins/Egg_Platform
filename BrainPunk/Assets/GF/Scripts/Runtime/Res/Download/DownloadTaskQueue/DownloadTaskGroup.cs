namespace GF
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    
    /// <summary>
    /// 相同优先级的任务组 同一个任务组里面的任务并行下载
    /// </summary>
    public class DownloadTaskGroup : IComparable<DownloadTaskGroup>
    {
        /// <summary>
        /// 任务组优先级
        /// </summary>
        private EDownloadTaskPriority _groupPriority;
        
        /// <summary>
        /// 任务组全部任务(包括还未开始下载以及下载中的任务)
        /// </summary>
        private Dictionary<string, DownloadTask> _groupTasks;

        /// <summary>
        /// 任务组下载任务异步等待CompletionSource
        /// </summary>
        private UniTaskCompletionSource _taskCompletionSource;
        
        /// <summary>
        /// 是否是降低优先级后重启的任务
        /// </summary>
        private bool _isReActiveTaskGroup;

        public bool IsReActiveTaskGroup => _isReActiveTaskGroup;
        
        public DownloadTaskGroup()
        {
            _groupPriority = EDownloadTaskPriority.Normal;
            _groupTasks = new Dictionary<string, DownloadTask>();
            _taskCompletionSource = null;
            _isReActiveTaskGroup = false;
        }

        public DownloadTaskGroup(EDownloadTaskPriority priority) : this()
        {
            _groupPriority = priority;
        }

        public bool IsStartDownload()
        {
            return null != _taskCompletionSource;
        }

        public EDownloadTaskPriority GetGroupPriority()
        {
            return _groupPriority;
        }
        

        /// <summary>
        /// 开始任务组中的任务下载
        /// </summary>
        public async UniTask StartDownload()
        {
            if (null != _taskCompletionSource)
            {
                LogKit.W("DownloadTaskGroup had start download allready!");
                return;
            }

            _taskCompletionSource = new UniTaskCompletionSource();
            foreach (var kv in _groupTasks)
            {
                kv.Value.Start().Forget();
            }
           
            await _taskCompletionSource.Task;
        }

        /// <summary>
        /// 取消任务组下载 停止所有正在下载的任务
        /// </summary>
        public void CancelDownload()
        {
            if (null == _taskCompletionSource)
            {
                //还没开始下载或者已经下载完成
                return;
            }
            
            //取消下载的时候 将已经下载成功或者下载失败又不能重新下载的任务移除掉
            List<string> canRemoveTasks = new List<string>();
            foreach (var kv in _groupTasks)
            {
                if (EDownloadTaskStatus.DownloadSuccess == kv.Value.Status ||
                    (EDownloadTaskStatus.DownloadFailed == kv.Value.Status && !DownLoadTaskQueueMgr.Instance.CheckCanReDowndload(kv.Value.Priority)))
                {
                    canRemoveTasks.Add(kv.Key);
                }
                else
                {
                    kv.Value.Cancel();
                }
            }

            foreach (var packageName in canRemoveTasks)
            {
                _groupTasks.Remove(packageName);
            }

            _taskCompletionSource.TrySetCanceled();
            _taskCompletionSource = null;
        }

        public bool AddTask(DownloadTask task)
        {
            if (null == task || !task.IsValid() || _groupPriority != task.Priority)
            {
                return false;
            }
            
            if (_groupTasks.TryGetValue(task.PackageName, out DownloadTask existTask))
            {
                //如果有相同的任务 则直接附加回调函数
                existTask.AttachCallback(task.OnSuccess, task.OnFail, task.OnProgress);
            }
            else
            {
                _groupTasks.Add(task.PackageName, task);
                task.TaskGroup = this;
                if (null != _taskCompletionSource)
                {
                    task.Start().Forget();
                }
            }

            return true;
        }

        public DownloadTask TryGetTask(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return null;
            }

            DownloadTask task = null;
            _groupTasks.TryGetValue(packageName, out task);
            return task;
        }

        public void TryRemoveTask(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                return;
            }

            if (_groupTasks.ContainsKey(packageName))
            {
                _groupTasks[packageName].TaskGroup = null;
                _groupTasks.Remove(packageName);
            }
        }

        public void OnTaskDownloadFinish(DownloadTask task)
        {
            if (null == task)
            {
                return;
            }

            bool allDownloadFinish = true;
            string packageName = task.PackageName;
            if (_groupTasks.ContainsKey(packageName))
            {
                foreach (var kv in _groupTasks)
                {
                    //下载成功和失败都算作结束
                    bool downloadFinish = EDownloadTaskStatus.DownloadSuccess == kv.Value.Status ||
                                          EDownloadTaskStatus.DownloadFailed == kv.Value.Status;
                    allDownloadFinish = downloadFinish && allDownloadFinish;
                    if (!allDownloadFinish)
                    {
                        break;
                    }
                }
            }
            else
            {
                allDownloadFinish = false;
            }

            if (allDownloadFinish)
            {
                UniTaskCompletionSource completionSource = _taskCompletionSource;
                _taskCompletionSource = null;
                completionSource?.TrySetResult();
            }
        }

        public bool HaveDownloadFailedTask()
        {
            foreach (var kv in _groupTasks)
            {
                if (EDownloadTaskStatus.DownloadFailed == kv.Value.Status)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 在任务组有任务下载失败的情况下 重新激活任务组
        /// </summary>
        public bool ReActiveTaskGroup(EDownloadTaskPriority priority = EDownloadTaskPriority.Normal)
        {
            if (null != _taskCompletionSource)
            {
                return false;
            }
            
            if (_groupTasks.Count > 0)
            {
                Dictionary<string, DownloadTask> newGroupTasks = new Dictionary<string, DownloadTask>();
                foreach (var kv in _groupTasks)
                {
                    if (EDownloadTaskStatus.DownloadFailed == kv.Value.Status)
                    {
                        //只有下载失败了的才会重新下载
                        if (kv.Value.ReActive(priority))
                        { 
                            newGroupTasks.TryAdd(kv.Value.PackageName, kv.Value);
                        }
                    }
                }

                if (newGroupTasks.Count > 0)
                {
                    _groupTasks = newGroupTasks;
                    _groupPriority = priority;
                    _isReActiveTaskGroup = true;
                    return true;
                }
            }
           
            return false;
        }
        
        public bool NeedToDownload()
        {
            if (0 == _groupTasks.Count)
            {
                return false;
            }

            bool allCached = true;
            foreach (var kv in _groupTasks)
            {
                bool isCached = PackageDownloadMgr.Instance.IsCachedPackage(kv.Value.PackageName,
                    kv.Value.Generation, kv.Value.Version);
                allCached = allCached && isCached;
                if (!allCached)
                {
                    break;
                }
            }

            return !allCached;
        }
        
        //目前下载器取消下载的接口似乎不能停止网络请求 取消后依然后有回调过来 所以会导致一些被取消了的任务在等待队列中变成完成状态
        //添加这个接口就是直接调用在等待中变成完成状态的任务的回调函数 不再启动下载流程
        public void FinishAllTaskDirectly()
        {
            if (!NeedToDownload())
            {
                foreach (var kv in _groupTasks)
                {
                    kv.Value.OnSuccess?.Invoke(kv.Value.PackageName);
                }
            }
        }

        public int CompareTo(DownloadTaskGroup otherTaskGroup)
        {
            if (null == otherTaskGroup)
            {
                return 1;
            }
            else
            {
                int thisPriority = (int)GetGroupPriority();
                int otherPriority = (int)otherTaskGroup.GetGroupPriority();
                int result = otherPriority.CompareTo(thisPriority);
                if (0 == result)
                {
                    if (IsReActiveTaskGroup && !otherTaskGroup.IsReActiveTaskGroup)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
                else
                {
                    return result;
                }
            }
        }
    }
}