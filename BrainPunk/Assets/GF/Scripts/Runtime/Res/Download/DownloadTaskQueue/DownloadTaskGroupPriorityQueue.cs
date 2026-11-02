using System;
using System.Collections.Generic;

namespace GF
{
    
    /// <summary>
    /// 优先级下载任务组管理器
    /// </summary>
    public class DownloadTaskGroupPriorityQueue
    {
        private List<DownloadTaskGroup> _taskGroupQueue;

        public DownloadTaskGroupPriorityQueue()
        {
            _taskGroupQueue = new List<DownloadTaskGroup>();
        }

        public void Enqueue(DownloadTaskGroup taskGroup)
        {
            if (null == taskGroup)
            {
                return;
            }

            _taskGroupQueue.Add(taskGroup);
            _taskGroupQueue.Sort();
        }

        public DownloadTaskGroup Dequeue()
        {
            if (0 == _taskGroupQueue.Count)
            {
                return null;
            }

            DownloadTaskGroup taskGroup = _taskGroupQueue[0];
            _taskGroupQueue.RemoveAt(0);
            return taskGroup;
        }

        public DownloadTaskGroup Peek()
        {
            return 0 == _taskGroupQueue.Count ? null : _taskGroupQueue[0];
        }

        public bool IsEmpty()
        {
            return 0 == _taskGroupQueue.Count;
        }

        public DownloadTaskGroup GetTaskGroupByPriority(EDownloadTaskPriority priority)
        {
            foreach (var taskGroup in _taskGroupQueue)
            {
                if (priority == taskGroup.GetGroupPriority())
                {
                    return taskGroup;
                }
            }

            return null;
        }

        public DownloadTask GetTask(string taskPacakgeName)
        {
            if (string.IsNullOrEmpty(taskPacakgeName))
            {
                return null;
            }

            DownloadTask task = null;
            for (int i = 0; i < _taskGroupQueue.Count; i++)
            {
                task = _taskGroupQueue[i].TryGetTask(taskPacakgeName);
                if (null != task)
                {
                    break;
                }
            }

            return task;
        }

    }
}