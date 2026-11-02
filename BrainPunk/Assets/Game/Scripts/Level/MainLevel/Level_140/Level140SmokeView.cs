using UnityEngine;

/// <summary>从编辑器手摆模板随机向左上或右上持续冒烟。由关卡时钟驱动，有限池循环，不使用往返/呼吸缩放。</summary>
public sealed class Level140SmokeView : MonoBehaviour
{
    private const float Lifetime = 1.2f;
    private const float Interval = 0.3f;
    private readonly Vector3[] velocities = new Vector3[5];
    private readonly SpriteRenderer[] puffs = new SpriteRenderer[5];
    private readonly float[] ages = new float[5];
    private readonly Vector3[] startPositions = new Vector3[5];
    private readonly Color[] startColors = new Color[5];
    private SpriteRenderer template;
    private bool templateWasEnabled;
    private float emissionTime;
    private int nextPuff;
    private bool emitting;

    /// <summary>用手摆 Renderer 初始化五个烟团及排序；每个视图只应调用一次，运行时隐藏模板并在销毁时恢复原启用状态。</summary>
    public void Configure(SpriteRenderer source, int sortingOrder)
    {
        template = source;
        templateWasEnabled = source.enabled;
        // 只隐藏运行实例中的静态预览，不隐藏/移动参考节点；清理时恢复。
        source.enabled = false;
        for (int i = 0; i < puffs.Length; i++)
        {
            puffs[i] = new GameObject("SmokePuff_" + i).AddComponent<SpriteRenderer>();
            puffs[i].transform.SetParent(transform, false);
            puffs[i].sortingOrder = sortingOrder;
            ages[i] = Lifetime;
            puffs[i].gameObject.SetActive(false);
        }
    }

    /// <summary>开始持续发烟并立即生成第一团；重复调用保持幂等，停止和销毁由所属关卡生命周期负责。</summary>
    public void Begin()
    {
        if (emitting) return;
        emitting = true;
        emissionTime = 0f;
        Emit();
    }

    /// <summary>由本关可暂停玩法时钟推进烟团移动、透明度和发射间隔；dt 必须为非负秒值，不应改用全局独立 Update。</summary>
    public void Tick(float dt)
    {
        for (int i = 0; i < puffs.Length; i++)
        {
            if (ages[i] >= Lifetime) continue;
            ages[i] = Mathf.Min(Lifetime, ages[i] + dt);
            if (ages[i] >= Lifetime)
            {
                puffs[i].gameObject.SetActive(false);
                continue;
            }
            float fadeIn = Mathf.Clamp01(ages[i] / 0.12f);
            float fadeOut = 1f - ages[i] / Lifetime;
            Color color = startColors[i];
            color.a *= 0.75f * fadeIn * fadeOut;
            puffs[i].color = color;
            puffs[i].transform.position = startPositions[i] + velocities[i] * ages[i];
        }
        if (!emitting) return;
        emissionTime += dt;
        while (emissionTime >= Interval)
        {
            emissionTime -= Interval;
            Emit();
        }
    }

    private void Emit()
    {
        if (template == null) return;
        ages[nextPuff] = 0f;
        var puff = puffs[nextPuff];
        puff.sprite = template.sprite;
        puff.sharedMaterial = template.sharedMaterial;
        puff.flipX = template.flipX;
        puff.flipY = template.flipY;
        // 烟图右下角是喷口，改变朝向时固定这个点，避免右上烟团从机器旁凭空冒出。
        Bounds bounds = template.sprite.bounds;
        Vector3 tip = new Vector3(bounds.max.x, bounds.min.y, 0f);
        if (template.flipX) tip.x = -tip.x;
        if (template.flipY) tip.y = -tip.y;
        Vector3 origin = template.transform.TransformPoint(tip);
        Vector3 originalDirection = template.transform.TransformVector(-tip);
        float angle = Random.value < 0.5f ? Random.Range(115f, 145f) : Random.Range(35f, 65f);
        float sourceAngle = Mathf.Atan2(originalDirection.y, originalDirection.x) * Mathf.Rad2Deg;
        puff.transform.rotation = Quaternion.AngleAxis(angle - sourceAngle, Vector3.forward) * template.transform.rotation;
        velocities[nextPuff] = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f)
            * Random.Range(1.45f, 1.9f);
        Vector3 sourceScale = template.transform.lossyScale;
        Vector3 parentScale = transform.lossyScale;
        puff.transform.localScale = new Vector3(
            RelativeScale(sourceScale.x, parentScale.x),
            RelativeScale(sourceScale.y, parentScale.y),
            RelativeScale(sourceScale.z, parentScale.z));
        startPositions[nextPuff] = origin - puff.transform.TransformVector(tip);
        puff.transform.position = startPositions[nextPuff];
        startColors[nextPuff] = template.color;
        Color color = template.color;
        color.a = 0f;
        puff.color = color;
        puff.gameObject.SetActive(true);
        nextPuff = (nextPuff + 1) % puffs.Length;
    }

    private static float RelativeScale(float source, float parent) => Mathf.Abs(parent) > 0.0001f ? source / parent : 0f;

    private void OnDestroy()
    {
        if (template != null) template.enabled = templateWasEnabled;
    }
}
