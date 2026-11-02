using UnityEditor;
using UnityEngine;

/// <summary>区域本地 Inspector 与 Scene 预览。不创建编辑器 Mesh/对象，不访问 DB 或单例。</summary>
[CustomEditor(typeof(DLevel88GravityZone))]
public sealed class DLevel88GravityZoneEditor : Editor
{
    #region 属性面板与撤销
    /// <summary>根据形状显示尺寸或顶点，显式同步已绑定触发器并支持撤销。</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var zone = (DLevel88GravityZone)target;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shape"),new GUIContent("区域形状"));
        int shape=serializedObject.FindProperty("shape").enumValueIndex;
        string field=shape==0 ? "rectangleSize" : shape==1 ? "circleRadius" : "polygonPoints";
        EditorGUILayout.PropertyField(serializedObject.FindProperty(field),new GUIContent(shape==0 ? "矩形宽高" : shape==1 ? "圆形半径" : "多边形顶点"),true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regionColor"),new GUIContent("区域颜色（含透明度）"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patternTileSize"),new GUIContent("内部纹理平铺尺寸", "每张纹理占用的本地宽高；数值越小，手绘线越密。"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("patternStrength"),new GUIContent("内部纹理强度", "0为纯色；只调节手绘笔触，不改变区域整体透明度。"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineWidth"),new GUIContent("描边宽度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineRepeatLength"),new GUIContent("描边重复长度", "沿边界每隔多少单位重复一张素材；不改变填色或触发范围。"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("affectedLayers"),new GUIContent("受影响的物体层"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regionCollider"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regionMesh"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regionRenderer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regionOutline"),new GUIContent("描边渲染器"));
        if (EditorGUI.EndChangeCheck())
        {
            if (zone.RegionCollider!=null) Undo.RecordObject(zone.RegionCollider,"调整反重力区域");
            serializedObject.ApplyModifiedProperties();
            zone.ApplyEditorCollider();
            zone.RefreshRuntimePreview();
            if (zone.RegionCollider!=null) PrefabUtility.RecordPrefabInstancePropertyModifications(zone.RegionCollider);
            SceneView.RepaintAll();
        }
        if (!zone.TryBuildShape(out _,out _)) EditorGUILayout.HelpBox("轮廓无效：需要 3～64 个不重复、不自交的顶点。区域暂不生效。",MessageType.Error);
        EditorGUILayout.HelpBox("移动/旋转 Transform 调整位置；矩形改宽高，圆形改半径，多边形可拖动 Scene 中的顶点。填色 Scene 预览需开启 Gizmos，Game 中运行时显示。离开最后一个区域恢复原重力；不反转当前速度。",MessageType.Info);
        EditorGUILayout.HelpBox("内部平铺 Sp_hx_x_02，边界重复 Sp_hx_x_01。Scene 非运行态辅助描边跟随宽度，实际贴图请看运行画面；运行/暂停时改参数立即刷新。描边保持素材原色，区域透明度只影响内部填色。",MessageType.Info);
    }
    #endregion

    #region Scene 纯本地预览
    private void OnSceneGUI()
    {
        var zone=(DLevel88GravityZone)target;
        if(zone.Shape!=DLevel88GravityZone.ZoneShape.Polygon) return;
        serializedObject.Update();
        var points=serializedObject.FindProperty("polygonPoints");
        for(int i=0;i<points.arraySize;i++)
        {
            var item=points.GetArrayElementAtIndex(i);
            Vector3 world=zone.transform.TransformPoint(item.vector2Value);
            EditorGUI.BeginChangeCheck();
            var moved=Handles.PositionHandle(world,zone.transform.rotation);
            if(!EditorGUI.EndChangeCheck()) continue;
            if(zone.RegionCollider!=null) Undo.RecordObject(zone.RegionCollider,"移动区域顶点");
            item.vector2Value=zone.transform.InverseTransformPoint(moved);
            serializedObject.ApplyModifiedProperties(); zone.ApplyEditorCollider();
            zone.RefreshRuntimePreview();
            if(zone.RegionCollider!=null) PrefabUtility.RecordPrefabInstancePropertyModifications(zone.RegionCollider);
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawPreview(DLevel88GravityZone zone,GizmoType flags)
    {
        if(Application.isPlaying || !zone.enabled || !zone.gameObject.activeInHierarchy || !zone.TryBuildShape(out var p,out var triangles)) return;
        using(new Handles.DrawingScope(zone.RegionColor,zone.transform.localToWorldMatrix))
        {
            for(int i=0;i<triangles.Length;i+=3)
                Handles.DrawAAConvexPolygon((Vector3)p[triangles[i]],(Vector3)p[triangles[i+1]],(Vector3)p[triangles[i+2]]);
            Handles.color=new Color(zone.RegionColor.r,zone.RegionColor.g,zone.RegionColor.b,0.85f);
            float halfWidth=zone.OutlineWidth*0.5f;
            for(int i=0;i<p.Length;i++)
            {
                Vector3 a=p[i],b=p[(i+1)%p.Length];
                Vector3 normal=Vector3.Cross(Vector3.forward,(b-a).normalized)*halfWidth;
                Handles.DrawAAConvexPolygon(a-normal,a+normal,b+normal,b-normal);
                Handles.DrawSolidDisc(a,Vector3.forward,halfWidth);
            }
        }
    }
    #endregion
}
