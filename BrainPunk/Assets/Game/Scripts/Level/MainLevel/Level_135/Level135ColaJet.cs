using UnityEngine;

/// <summary>135 使用原两帧素材组成横向水柱；不带碰撞，清场逻辑由主控负责。</summary>
public sealed class Level135ColaJet : MonoBehaviour
{
    #region 显式素材绑定
    /// <summary>本关场景内预置的水柱、末端泡沫、瓶口泡沫和瓶盖。</summary>
    [SerializeField] private SpriteRenderer stream, endFoam, mouthFoam, cap;
    /// <summary>各部件的两帧序列，按动效素材原顺序播放。</summary>
    [SerializeField] private Sprite[] streamFrames, endFrames, mouthFrames;
    private Vector3 origin;
    private float elapsed, length;
    /// <summary>本帧前端浪花的世界X；主控用同一位置推动顾客。</summary>
    public float FrontX { get; private set; }
    #endregion

    #region 演出接口
    /// <summary>从瓶口沿世界左方向喷出；瓶盖同时弹开，长度由本关画面范围传入。</summary>
    public void Begin(Vector3 position, float distance)
    {
        origin = position; elapsed = 0f; length = distance;
        SetOpacity(1f);
        gameObject.SetActive(true);
        Tick(0f, 0f);
    }

    /// <summary>主控有效帧推进两帧切换及水柱伸出；暂停时不调用。</summary>
    public void Tick(float dt, float progress)
    {
        elapsed += dt;
        int frame = ((int)(elapsed / 0.1f)) & 1;
        stream.sprite = streamFrames[frame];
        endFoam.sprite = endFrames[frame];
        mouthFoam.sprite = mouthFrames[frame];
        float reach = Mathf.Max(0.01f, length * Mathf.Clamp01(progress));
        FrontX = origin.x - reach;
        stream.transform.SetPositionAndRotation(origin + Vector3.left * reach * 0.5f, Quaternion.Euler(0f, 0f, 90f));
        stream.transform.localScale = new Vector3(1f, reach / stream.sprite.bounds.size.y, 1f);
        endFoam.transform.SetPositionAndRotation(origin + Vector3.left * reach, Quaternion.Euler(0f, 0f, 90f));
        mouthFoam.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, 90f));
        cap.transform.position = origin + new Vector3(-5f * elapsed, 4f * elapsed - 4f * elapsed * elapsed, 0f);
        cap.transform.rotation = Quaternion.Euler(0f, 0f, elapsed * 600f);
    }

    /// <summary>清场完成后统一淡出水柱、前后浪花及瓶盖；暂停时由主控停止推进。</summary>
    public void SetOpacity(float opacity)
    {
        float alpha = Mathf.Clamp01(opacity);
        SetAlpha(stream, alpha);
        SetAlpha(endFoam, alpha);
        SetAlpha(mouthFoam, alpha);
        SetAlpha(cap, alpha);
    }

    private static void SetAlpha(SpriteRenderer target, float alpha)
    {
        Color color = target.color;
        color.a = alpha;
        target.color = color;
    }

    /// <summary>渐隐结束或旧关清理时停止显示，不影响原始素材或其它关卡。</summary>
    public void Hide() => gameObject.SetActive(false);
    #endregion
}
