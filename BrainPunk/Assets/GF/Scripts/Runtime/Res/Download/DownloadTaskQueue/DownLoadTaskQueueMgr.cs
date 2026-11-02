using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace GF
{
    /// <summary>
    /// 下载任务队列管理器
    /// 队列中任务组按优先级排序 优先级高的先下载
    /// 同一个任务组中的任务并行下载
    /// </summary>
    public class DownLoadTaskQueueMgr : Singleton<DownLoadTaskQueueMgr>
    {
        private string _tag = "[DownLoadTaskQueueMgr]";
        /// <summary>
        /// 下载任务组优先级队列
        /// </summary>
        private DownloadTaskGroupPriorityQueue _priorityQueue;
        
        /// <summary>
        /// 当前正在下载的任务组
        /// </summary>
        private DownloadTaskGroup _downloadingTaskGroup;
        
        /// <summary>
        /// 暂停等待的CompletionSource
        /// </summary>
        private UniTaskCompletionSource _pauseTaskSource = null;
        
        /// <summary>
        /// 是否暂停下载
        /// </summary>
        private bool _isPause = false;
        
         
        
        public DownLoadTaskQueueMgr()
        {
            _priorityQueue = new DownloadTaskGroupPriorityQueue();
            _downloadingTaskGroup = null;
        }

        private DownloadTaskGroup GetTaskGroupInternal()
        {
            while (!_priorityQueue.IsEmpty())
            {
                DownloadTaskGroup taskGroup = _priorityQueue.Dequeue();
                if (taskGroup.NeedToDownload())
                {
                    return taskGroup;
                }
                else
                {
                    taskGroup.FinishAllTaskDirectly();
                }
            }

            return null;
        }

        private async UniTask StartDownloadInternal()
        {
            if (null != _downloadingTaskGroup)
            {
                LogKit.W($"{_tag} StartDownloadInternal: 当前有任务组正在下载中，请不要重复启动.");
                return;
            }

            while (!_priorityQueue.IsEmpty())
            {
                try
                {
                    _downloadingTaskGroup = GetTaskGroupInternal();
                    if (null == _downloadingTaskGroup)
                    {
                        return;
                    }

                    // 如果是暂停状态 则等待
                    if (_isPause)
                    {
                        LogKit.I($"{_tag} 暂停下载");
                        if (_pauseTaskSource == null)
                        {
                            _pauseTaskSource = new UniTaskCompletionSource();
                        }
                        await _pauseTaskSource.Task;
                        LogKit.I($"{_tag} 恢复下载");
                    }

                    await _downloadingTaskGroup.StartDownload();
                    
                    //任务完成
                    if (_downloadingTaskGroup.HaveDownloadFailedTask() && CheckCanReDowndload(_downloadingTaskGroup.GetGroupPriority()))
                    {
                        //有下载失败的任务 并且满足重新进入队列下载的条件
                        if (_downloadingTaskGroup.ReActiveTaskGroup(EDownloadTaskPriority.Normal))
                        {
                            _priorityQueue.Enqueue(_downloadingTaskGroup);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    //任务被取消了 重新插入到队列中(1有可能是有优先级更高的任务并且需要立即下载的任务 2.暂停下载)
                    _priorityQueue.Enqueue(_downloadingTaskGroup);
                }
                finally
                {
                    _downloadingTaskGroup = null;
                }
            }
        }

        /// <summary>
        /// 添加任务到优先队列中
        ///如果队列中有一个相同的任务了 则比较两个任务的优先级 
        ///1 如果已经在队列中的任务的优先级大于等于要新加入的 则直接把要新加入的任务的回调attach到已经存在的任务上
        ///2 如果已经在队列中的任务的优先级小于要新加入的任务 先找有没有相同优先级的任务组存在 有则直接将任务加入到对应组中；如果没有相同优先级的任务组 则创建一个新的任务组把任务加进去
        ///  同时原来已经存在的那个任务也要移动到新的任务组中去
        /// </summary>
        /// <param name="task"></param>
        private void AddTaskToQueue(DownloadTask task)
        {
            if (null == task)
            {
                return;
            }

            DownloadTask sameTask = _priorityQueue.GetTask(task.PackageName);
            if (null != sameTask)
            {
                if ((int)sameTask.Priority >= (int)task.Priority)
                {
                    //直接attach回调函数
                    sameTask.AttachCallback(task.OnSuccess, task.OnFail, task.OnProgress);
                    return;
                }
            }
           
            DownloadTaskGroup taskGroup = _priorityQueue.GetTaskGroupByPriority(task.Priority);
            if (null == taskGroup)
            {
                taskGroup = new DownloadTaskGroup(task.Priority);
                _priorityQueue.Enqueue(taskGroup);
            }

            if (null != sameTask)
            {
                //把已经存在的任务的回调加上
                sameTask.TaskGroup.TryRemoveTask(task.PackageName);
                task.AttachCallback(sameTask.OnSuccess, sameTask.OnFail, sameTask.OnProgress);
            }
            
            taskGroup.AddTask(task);
        }

        /// <summary>
        /// 检查任务是否符合重新下载的规定
        /// 符合重新下载条件的任务 会在任务组中的任务全部下载结束之后 重新激活任务组去下载之前下载失败的任务
        /// </summary>
        /// <param name="taskPriority"></param>
        /// <returns></returns>
        public bool CheckCanReDowndload(EDownloadTaskPriority taskPriority)
        {
            return taskPriority == EDownloadTaskPriority.High || taskPriority == EDownloadTaskPriority.VeryHigh;
        }

        /// <summary>
        /// 新加下载任务的接口
        /// </summary>
        /// <param name="packageName"></param>
        /// <param name="generation"></param>
        /// <param name="version"></param>
        /// <param name="priority"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFail"></param>
        /// <param name="onProgress"></param>
        /// <param name="immediately">如果当前有比新加任务优先级低的任务已经在下载，是否强制停止正在下载的任务，开始下载本任务</param>
        public void StartDownload(string packageName, string generation, string version = "", EDownloadTaskPriority priority = EDownloadTaskPriority.Normal, Action<string> onSuccess = null, Action<string> onFail = null, Action<string, float> onProgress = null, bool immediately = false)
        {
            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(generation))
            {
                LogKit.E($"{_tag} DownLoadTaskMgr StartDownload: Invalid Task packageName or generation!!");
                onFail?.Invoke(packageName);
                return;
            }

            if (PackageDownloadMgr.Instance.IsCachedPackage(packageName, generation, version))
            {
                LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: package({packageName}, {generation}) is allready cached!!!");
                onSuccess?.Invoke(packageName);
                return;
            }
            
            LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: {packageName}, {generation}, {priority}, {_downloadingTaskGroup?.GetGroupPriority()} ");
            DownloadTask newTask = new DownloadTask(packageName, generation, version, priority, onSuccess, onFail, onProgress);
            
            //如果有正在下载的任务
            if (null != _downloadingTaskGroup)
            {
                if (_downloadingTaskGroup.GetGroupPriority() == priority)
                {
                    //检查下是否相同优先级 相同的话直接加入
                    LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: 同一优先级任务加入下载队列: {packageName}, {priority}");
                    _downloadingTaskGroup.AddTask(newTask);
                }
                else
                {
                    DownloadTask downloagingTask = _downloadingTaskGroup.TryGetTask(newTask.PackageName);
                    if (null != downloagingTask && downloagingTask.Status != EDownloadTaskStatus.DownloadSuccess)
                    {
                        //优先级不同 但是存在相同目标package的任务 直接把新任务的回调attach到当前正在下载的任务上
                        LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: 相同packageName任务加入下载队列: {packageName}, {priority}, {downloagingTask.Status}");
                        downloagingTask.AttachCallback(newTask.OnSuccess, newTask.OnFail, newTask.OnProgress);
                    }
                    else
                    {
                        if (null != downloagingTask)
                        {
                            downloagingTask.TaskGroup.TryRemoveTask(downloagingTask.PackageName);
                        }
                        if (_downloadingTaskGroup.GetGroupPriority() < priority)
                        {
                            //新加入的任务优先级高于正在下载的任务组
                            if (immediately)
                            {
                                //中断正在下载的任务组 开始新的更高优先级的任务组下载
                                LogKit.I($"{_tag} DownLoadTaskMgr StartDownload 终止当前任务 开始新的高优先级任务: {packageName}, {priority}, {_downloadingTaskGroup?.GetGroupPriority()}");
                                AddTaskToQueue(newTask);
                                _downloadingTaskGroup.CancelDownload();
                            }
                            else
                            {
                                LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: 新任务优先级高于正在下载的任务组，加入等待队列: {packageName}, {priority}, {_downloadingTaskGroup?.GetGroupPriority()}");
                                AddTaskToQueue(newTask);
                            }
                        }
                        else
                        {
                            //低优先级的任务直接插入到等待队列
                            LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: 新任务优先级低于正在下载的任务组，加入等待队列: {packageName}, {priority}, {_downloadingTaskGroup?.GetGroupPriority()}");
                            AddTaskToQueue(newTask);
                        }
                    }
                }
                
            }
            else
            {
                //没有正在下载的任务组 先插入到队列 再启动第一个任务组的下载
                AddTaskToQueue(newTask);
                StartDownloadInternal().Forget();
                LogKit.I($"{_tag} DownLoadTaskMgr StartDownload: 没有正在下载的任务组，直接加入队列并开始下载: {packageName}, {priority}");
            }
        }
        
        /// <summary>
        /// 暂停下载
        /// </summary>
        public void PauseDownload()
        {
            if (_isPause)
            {
                return;
            }
            
            _isPause = true;
            _pauseTaskSource = new UniTaskCompletionSource();
            if (null != _downloadingTaskGroup)
            {
                _downloadingTaskGroup.CancelDownload();
            }
        }

        /// <summary>
        /// 恢复下载
        /// </summary>
        public void ResumeDownload()
        {
            if (_isPause)
            {
                _isPause = false;
                var tmp = _pauseTaskSource;
                _pauseTaskSource = null;
                tmp?.TrySetResult();
            }
        }
    }
}