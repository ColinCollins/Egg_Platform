using System;
using System.Collections.Generic;
using Game.Scripts.Effects;
using UnityEngine;

/// <summary>
/// 根据 sprites 及 count， 切换 front 和 back renderer 的 sprite， back 指向 ((index + 1) % count)。
/// brush complete 时，index ++ 并 重置 front sprite 和 back sprite
/// </summary>
public class Level55MisleadCtrl : MonoBehaviour
{
    [SerializeField] private List<Sprite> sprires;
    [SerializeField] private SceneSpriteBrushClear brush;
    [SerializeField] private SpriteRenderer frontSprite;
    [SerializeField] private SpriteRenderer backSprite;

    /// <summary>
    /// 误导图刷除完成回调；调用边界：由 Level_55Ctrl 在运行时绑定，用于统计完成次数。
    /// 注意事项：回调只通知完成事件，不负责切换 front/back sprite 或重置 brush 状态。
    /// </summary>
    public Action completeCallback;

    private int index;

    private void Start()
    {
        ApplySprites();
    }

    private void OnEnable()
    {
        if (brush != null)
        {
            brush.Completed += OnBrushCompleted;
        }
    }

    private void OnDisable()
    {
        if (brush != null)
        {
            brush.Completed -= OnBrushCompleted;
        }
    }

    private void OnBrushCompleted()
    {
        if (GetSpriteCount() <= 0)
        {
            return;
        }

        index = (index + 1) % GetSpriteCount();
        ApplySprites();
        completeCallback?.Invoke();
    }

    private void ApplySprites()
    {
        int count = GetSpriteCount();
        if (count <= 0)
        {
            SetRendererSprite(frontSprite, null);
            SetRendererSprite(backSprite, null);

            if (brush != null)
            {
                var brushRenderer = brush.GetComponent<SpriteRenderer>();
                if (brushRenderer != null)
                {
                    brushRenderer.sprite = null;
                }

                brush.RefreshSpriteState();
            }

            return;
        }

        index = Mathf.Clamp(index, 0, count - 1);
        Sprite currentSprite = sprires[index];
        Sprite nextSprite = sprires[(index + 1) % count];

        SetRendererSprite(frontSprite, currentSprite);
        SetRendererSprite(backSprite, nextSprite);

        if (brush != null)
        {
            var brushRenderer = brush.GetComponent<SpriteRenderer>();
            if (brushRenderer != null)
            {
                brushRenderer.sprite = currentSprite;
            }

            brush.RefreshSpriteState();
        }
    }

    private void SetRendererSprite(SpriteRenderer targetRenderer, Sprite sprite)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.sprite = sprite;
    }

    private int GetSpriteCount()
    {
        return sprires == null ? 0 : sprires.Count;
    }
}
