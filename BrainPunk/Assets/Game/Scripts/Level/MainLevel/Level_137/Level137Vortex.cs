using UnityEngine;

/// <summary>只由 137 控制器计时的洞口旋流；局部实例，无全局材质修改、物理力或常驻协程。</summary>
public sealed class Level137Vortex : MonoBehaviour
{
    // 历史旋流线条使用的局部材质；当前 Level137Ctrl 不再调用 Begin/Tick。
    [SerializeField] private Material lineMaterial;
    // 历史旋流线条颜色，仅保留旧资源序列化兼容。
    [SerializeField] private Color color = new Color(0.8f, 0.64f, 0.4f, 0.75f);
    // 历史旋流线条半径，仅保留旧资源序列化兼容。
    [SerializeField, Min(0.1f)] private float radius = 1.4f;
    private readonly LineRenderer[] lines = new LineRenderer[6];
    private float elapsed;
    private float duration;

    /// <summary>
    /// 启动历史线条旋流；当前 Level 137 玩法不调用此入口，仅保留旧资源兼容。
    /// </summary>
    public void Begin(Vector3 position, float seconds)
    {
        transform.position = position;
        elapsed = 0f;
        duration = Mathf.Max(0.1f, seconds);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
            {
                var part = new GameObject("Spiral_" + i);
                part.transform.SetParent(transform, false);
                var line = part.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.useWorldSpace = false;
                line.positionCount = 24;
                line.startWidth = 0.045f;
                line.endWidth = 0.005f;
                line.sortingLayerID = SortingLayer.NameToID("SceneLayer");
                line.sortingOrder = 105;
                lines[i] = line;
            }
            lines[i].enabled = true;
        }
    }

    /// <summary>
    /// 推进历史线条旋流；当前本关不从活动帧调用，避免与新的 Spine 旋涡重复表现。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (duration <= 0f) return;
        elapsed += deltaTime;
        float alpha = Mathf.Clamp01(elapsed / 0.2f) * Mathf.Clamp01((duration - elapsed) / 0.4f);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null) continue;
            line.startColor = new Color(color.r, color.g, color.b, color.a * alpha);
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            for (int p = 0; p < line.positionCount; p++)
            {
                float t = p / (float)(line.positionCount - 1);
                float r = radius * (1f - t);
                float angle = i * Mathf.PI / 3f + elapsed * 5f + t * 3.5f;
                line.SetPosition(p, new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * 0.6f, 0f));
            }
        }
        if (elapsed >= duration) Stop();
    }

    /// <summary>停止并隐藏历史线条，同时保留组件实例供关卡卸载清理。</summary>
    public void Stop()
    {
        duration = 0f;
        foreach (LineRenderer line in lines) if (line != null) line.enabled = false;
    }
}
