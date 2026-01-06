using System;
using System.Collections;
using System.Collections.Generic;
using Bear.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 当前场景，用状态机，处理一些资源加载或者配置初始化的功能设计
public class LoadManager : MonoBehaviour
{
    public Slider ProcessBar;

    private Queue<Func<IEnumerator<float>>> functionQueue = new Queue<Func<IEnumerator<float>>>();
    private Coroutine currentCoroutine;

    public void Awake()
    {
        // On Finshed
        EnqueueFunction(OnPostProcess);

        ProcessQueue();
    }

    // 后处理
    private IEnumerator<float> OnPostProcess()
    {
        yield return 1;
    }

    // 添加函数到队列
    public void EnqueueFunction(Func<IEnumerator<float>> function)
    {
        functionQueue.Enqueue(function);

        // 如果当前没有在执行，开始执行队列
        if (currentCoroutine == null)
        {
            currentCoroutine = StartCoroutine(ProcessQueue());
        }
    }

    // 处理队列中的函数
    private IEnumerator ProcessQueue()
    {
        while (functionQueue.Count > 0)
        {
            Func<IEnumerator<float>> function = functionQueue.Dequeue();
            IEnumerator<float> coroutine = function();
            ProcessBar.value = 0;

            // 执行协程并获取返回值
            while (coroutine.MoveNext())
            {
                float value = coroutine.Current;

                // 更新进度条
                if (ProcessBar != null)
                {
                    if (ProcessBar.value < value)
                    {
                        DOTween.To(() => ProcessBar.value, d =>
                        {
                            ProcessBar.value = d;
                        }, value, value - ProcessBar.value);
                    }
                    else
                    {
                        ProcessBar.value = value;
                    }

                }

                yield return null;
            }
        }

        currentCoroutine = null;
        LoadSceneAsync("Main");
    }

    // 异步加载场景（推荐）
    public void LoadSceneAsync(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // 等待场景加载完成
        while (!asyncLoad.isDone)
        {
            // 获取加载进度值（0-0.9 映射到 0-1）
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // 更新进度条
            if (ProcessBar != null)
            {
                ProcessBar.value = progress;
            }

            // 当进度达到 0.9 时，允许场景激活
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
