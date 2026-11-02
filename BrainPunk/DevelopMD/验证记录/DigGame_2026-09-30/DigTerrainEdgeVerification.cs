using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Game.Dig;
using Game.Scripts.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>仅用于隔离 PlayMode：测量真实复合碰撞与 GPU 擦除边缘，不进入生产资源。</summary>
public sealed class DigTerrainEdgeVerification : MonoBehaviour
{
    #region 测试状态
    /// <summary>编辑器测试入口设置；基线模式只记录修改前误差。</summary>
    public bool baselineOnly;
    const string Output = "C:/Users/admin/AppData/Local/Temp/brainpunk-diggame-20260930/";
    readonly Vector2 center = new Vector2(2f, 2f);
    DigTerrainCtrl terrain;
    Tilemap map;
    GameObject live;
    int checks;
    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("EDGE CHECK: " + message); }
    #endregion

    #region 真实物理与视觉测量
    void Dig()
    {
        terrain.Initialize();
        terrain.Stamp(center);
        terrain.FlushCollider();
        Physics2D.SyncTransforms();
    }
    float Distance(Vector2 direction)
    {
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(center, direction, 1.2f))
            if (hit.collider.gameObject == map.gameObject) return hit.distance;
        throw new Exception("射线未命中沙土复合碰撞");
    }
    float MeanError(float expected)
    {
        float total = 0;
        for (int i = 0; i < 72; i++)
        {
            float angle = i * Mathf.PI * 2 / 72;
            total += Mathf.Abs(Distance(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) - expected);
        }
        return total / 72;
    }
    float VisualRadius()
    {
        var rt = Field<RenderTexture>(Field<SceneSpriteBrushClear>(terrain, "visual"), "_maskRT");
        var previous = RenderTexture.active;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        int row = Mathf.FloorToInt((center.y + 4) / 8 * rt.height);
        int column = Mathf.FloorToInt((center.x + 4) / 8 * rt.width);
        while (column < rt.width && texture.GetPixel(column, row).r > .5f) column++;
        float result = column * 8f / rt.width - 4f - center.x;
        Destroy(texture);
        return result;
    }
    #endregion

    #region 运行与收尾
    IEnumerator Start()
    {
        Physics2D.simulationMode = SimulationMode2D.Script;
        live = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/DigLevel/DigLevel_001.prefab"));
        terrain = live.GetComponent<DigTerrainCtrl>();
        map = Field<Tilemap>(terrain, "collisionMap");
        yield return null;
        try
        {
            Dig();
            float visualRadius = VisualRadius();
            float error = MeanError(.75f);
            string metrics = $"视觉右侧半径={visualRadius:F5}；碰撞右侧半径={Distance(Vector2.right):F5}；72方向平均绝对误差={error:F5}";
            Debug.Log("EDGE METRICS: " + metrics);
            if (!baselineOnly)
            {
                Check(Mathf.Abs(visualRadius - .75f) <= 8f / 512, "GPU 视觉半径维持 0.75");
                Check(error < .018f, "当前六倍细分平均误差小于 0.018");
                Check(Mathf.Abs(Field<TilemapCollider2D>(terrain, "tileCollider").extrusionFactor * map.transform.lossyScale.x - .001f) < .00001f, "挤出参数 0.001 生效");
                float zero = Distance(Vector2.right);
                Set(terrain, "collisionRadiusOffset", .08f); Dig();
                Check(Distance(Vector2.right) > zero + .04f, "正偏移扩大碰撞洞口并遍历新增范围");
                Check(Mathf.Abs(VisualRadius() - visualRadius) < .0001f, "碰撞微调不改变视觉笔刷");
                Set(terrain, "collisionRadiusOffset", -.08f); Dig();
                Check(Distance(Vector2.right) < zero - .04f, "负偏移缩小碰撞洞口");
                Set(terrain, "collisionRadiusOffset", 0f);
                Set(terrain, "colliderExtrusion", .01f); Dig();
                Check(Distance(Vector2.right) < zero - .005f, "增加挤出量使洞口变窄");
                Set(terrain, "colliderExtrusion", .001f);
                foreach (int subdivisions in new[] { 1, 2, 4, 6, 8 })
                {
                    Set(terrain, "collisionSubdivisions", subdivisions); Dig();
                    float tolerance = .2f / subdivisions * Mathf.Sqrt(2) / 2 + .003f;
                    Check(MeanError(.75f) <= tolerance, "细分 " + subdivisions + " 误差保持半格对角线量级");
                }
                Set(terrain, "collisionSubdivisions", 6); Dig();
                float restored = Distance(Vector2.right);
                terrain.Stamp(center + new Vector2(.5f, 0)); terrain.FlushCollider(); Physics2D.SyncTransforms();
                Check(!map.HasTile(map.WorldToCell(center + new Vector2(.9f, 0))), "连续盖章扩大洞口");
                Dig();
                Check(Mathf.Abs(Distance(Vector2.right) - restored) < .001f, "重建恢复相同碰撞边界");
            }
            File.WriteAllText(Output + (baselineOnly ? "edge-before.txt" : "edge-after.txt"), $"通过 {checks} 项断言。{metrics}");
            live.SetActive(false); Destroy(live);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText(Output + "edge-failed.txt", e.ToString());
            EditorApplication.Exit(1);
        }
    }
    #endregion
}
