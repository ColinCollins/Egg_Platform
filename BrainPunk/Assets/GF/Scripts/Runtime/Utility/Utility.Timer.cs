using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GF
{
    public partial class Utility
    {
        public static partial class Timer
        {
            /// <summary>
            /// 设置延时计时器
            /// </summary>
            /// <param name="milliseconds"></param>
            /// <param name="callback"></param>
            /// <param name="cancellationToken"></param>
            public static void SetTimeout(int milliseconds, Action callback, CancellationToken cancellationToken = default)
            {
                Delay(milliseconds, callback, cancellationToken).Forget();
            }

            /// <summary>
            /// 设置interval计时器
            /// </summary>
            /// <param name="milliseconds"></param>
            /// <param name="callback"></param>
            /// <param name="complete"></param>
            /// <param name="cancellationToken"></param>
            /// <param name="loopTimes"></param>
            public static void SetInterval(int milliseconds, Action callback, Action complete = null, CancellationToken cancellationToken = default, int loopTimes = -1)
            {
                Interval(milliseconds, callback, complete, cancellationToken, loopTimes).Forget();
            }
            
            /// <summary>
            /// 设置interval计时器
            /// </summary>
            /// <param name="milliseconds"></param>
            /// <param name="callback"></param>
            /// <param name="totalMilliseconds">总共需要运行多长时间</param>
            /// <param name="complete"></param>
            /// <param name="cancellationToken"></param>
            public static void SetIntervalTime(int milliseconds, Action callback, int totalMilliseconds, Action complete = null,CancellationToken cancellationToken = default)
            {
                IntervalTime(milliseconds, callback, complete, cancellationToken, totalMilliseconds).Forget();
            }
            
            /// <summary>
            /// 延迟帧
            /// </summary>
            /// <param name="frame"></param>
            /// <param name="callback"></param>
            /// <param name="cancellationToken"></param>
            public static void DelayFrame(int frame, Action callback, CancellationToken cancellationToken = default)
            {
                DelayFrameInner(frame, callback, cancellationToken).Forget();
            }

            private static async UniTaskVoid Delay(int milliseconds, Action callback, CancellationToken cancellationToken)
            {
                await UniTask.Delay(milliseconds, DelayType.DeltaTime, cancellationToken: cancellationToken);
                callback?.Invoke();
            }
            
            private static async UniTaskVoid Interval(int milliseconds, Action callback, Action complete, CancellationToken cancellationToken, int loopTimes)
            {
                int count = 0;
                while (true)
                {
                    await UniTask.Delay(milliseconds, DelayType.DeltaTime, cancellationToken: cancellationToken);
                    callback?.Invoke();
                    count++;
                    if (loopTimes != -1 && count >= loopTimes)
                    {
                        break;
                    }
                }
                complete?.Invoke();
            }
            
            private static async UniTaskVoid IntervalTime(int milliseconds, Action callback, Action complete, CancellationToken cancellationToken, int totalMilliseconds)
            {
                bool isBreak = false;
                SetTimeout(totalMilliseconds, delegate
                {
                    isBreak = true;
                }, cancellationToken);
                while (true)
                {
                    await UniTask.Delay(milliseconds, DelayType.DeltaTime, cancellationToken: cancellationToken);
                    callback?.Invoke();
                    
                    if (isBreak)
                    {
                        break;
                    }
                }
                complete?.Invoke();
            }
            
            private static async UniTaskVoid DelayFrameInner(int frame, Action callback, CancellationToken cancellationToken)
            {
                await UniTask.DelayFrame(frame, cancellationToken: cancellationToken);
                callback?.Invoke();
            }
        }
    }
}