using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 顺序缩放动画：将一批 Transform 存入 cache，在 Update 中按索引延迟从 0 缩放到 1。
/// 通过 SetTargets / AddTarget 传入 Transform，调用 Play() 开始播放。
/// 
/// 直接在 GamePlayPanel 调用 DOScale 的话，不知道哪里冲突了，会导致前几秒的执行变得缓慢，和 brush 有关。和 Timescale 无关的样子
/// </summary>
public class SequentialScaleAnim : MonoBehaviour
{
    [Header("Anim Settings")]
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private float delayPerIndex = 0.06f;

    private readonly List<Transform> _cache = new List<Transform>();
    private float _startTimeUnscaled;
    private bool _isPlaying;

    /// <summary>当前是否正在播放</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>动画全部完成时回调</summary>
    public event Action Completed;

    /// <summary>单次缩放时长</summary>
    public float Duration { get => duration; set => duration = Mathf.Max(0.01f, value); }

    /// <summary>每个目标之间的延迟（秒）</summary>
    public float DelayPerIndex { get => delayPerIndex; set => delayPerIndex = Mathf.Max(0f, value); }

    /// <summary>清空 cache 并填入传入的 Transform（会过滤 null）</summary>
    public void SetTargets(params Transform[] targets)
    {
        _cache.Clear();
        if (targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                _cache.Add(targets[i]);
        }
    }

    /// <summary>清空 cache 并填入传入的 Transform 列表</summary>
    public void SetTargets(IList<Transform> targets)
    {
        _cache.Clear();
        if (targets == null) return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                _cache.Add(targets[i]);
        }
    }

    /// <summary>向 cache 追加一个 Transform</summary>
    public void AddTarget(Transform target)
    {
        if (target != null)
            _cache.Add(target);
    }

    /// <summary>清空 cache，不停止当前播放</summary>
    public void ClearTargets()
    {
        _cache.Clear();
    }

    /// <summary>开始播放：cache 中的 Transform 按索引顺序从 localScale 0 缩放到 1（使用 unscaledTime）</summary>
    public void Play()
    {
        for (int i = 0; i < _cache.Count; i++)
        {
            if (_cache[i] != null)
                _cache[i].localScale = Vector3.zero;
        }
        _startTimeUnscaled = Time.unscaledTime;
        _isPlaying = true;
    }

    /// <summary>设置目标并立即播放</summary>
    public void SetTargetsAndPlay(params Transform[] targets)
    {
        SetTargets(targets);
        Play();
    }

    private void Update()
    {
        if (!_isPlaying || _cache.Count == 0)
            return;

        float elapsed = Time.unscaledTime - _startTimeUnscaled;
        bool allDone = true;

        for (int i = 0; i < _cache.Count; i++)
        {
            Transform t = _cache[i];
            if (t == null)
                continue;

            float startDelay = delayPerIndex * i;
            float localT = (elapsed - startDelay) / duration;

            if (localT <= 0f)
            {
                t.localScale = Vector3.zero;
                allDone = false;
            }
            else if (localT >= 1f)
            {
                t.localScale = Vector3.one;
            }
            else
            {
                float s = Mathf.Clamp01(localT);
                t.localScale = Vector3.one * s;
                allDone = false;
            }
        }

        if (allDone)
        {
            _isPlaying = false;
            Completed?.Invoke();
        }
    }
}
