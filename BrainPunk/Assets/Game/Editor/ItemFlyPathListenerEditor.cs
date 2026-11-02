#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.ItemEvent;
using System.Collections.Generic;

[CustomEditor(typeof(ItemFlyPathListener))]
[CanEditMultipleObjects]
public class ItemFlyPathListenerEditor : Editor
{
    private ItemFlyPathListener targetScript;
    private SerializedProperty useBezierCurveProperty;
    private SerializedProperty bezierControlPointsProperty;
    private SerializedProperty targetProperty;
    private SerializedProperty waypointsProperty;
    private SerializedProperty endTargetProperty;

    private void OnEnable()
    {
        targetScript = (ItemFlyPathListener)target;
        useBezierCurveProperty = serializedObject.FindProperty("useBezierCurve");
        bezierControlPointsProperty = serializedObject.FindProperty("bezierControlPoints");
        targetProperty = serializedObject.FindProperty("target");
        waypointsProperty = serializedObject.FindProperty("waypoints");
        endTargetProperty = serializedObject.FindProperty("endTarget");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();
        
        if (targetScript == null || !useBezierCurveProperty.boolValue)
            return;

        // 获取路径点
        var pathPoints = GetPathPoints();
        if (pathPoints == null || pathPoints.Count < 2)
            return;

        // 确保控制点数量匹配
        int segmentCount = pathPoints.Count - 1;
        while (bezierControlPointsProperty.arraySize < segmentCount)
        {
            bezierControlPointsProperty.arraySize++;
            int index = bezierControlPointsProperty.arraySize - 1;
            Vector3 start = pathPoints[index];
            Vector3 end = pathPoints[index + 1];
            Vector3 midPoint = (start + end) * 0.5f;
            SerializedProperty controlPointProperty = bezierControlPointsProperty.GetArrayElementAtIndex(index);
            controlPointProperty.vector3Value = midPoint + Vector3.up * 1f;
        }
        while (bezierControlPointsProperty.arraySize > segmentCount)
        {
            bezierControlPointsProperty.arraySize--;
        }

        // 为每个路径段绘制可编辑的控制点
        Handles.color = Color.magenta;
        for (int i = 0; i < segmentCount; i++)
        {
            SerializedProperty controlPointProperty = bezierControlPointsProperty.GetArrayElementAtIndex(i);
            Vector3 currentPos = controlPointProperty.vector3Value;

            // 绘制控制点手柄
            EditorGUI.BeginChangeCheck();
            float handleSize = HandleUtility.GetHandleSize(currentPos) * 0.15f;
            var fmh_76_17_639059014147728668 = Quaternion.identity; Vector3 newPosition = Handles.FreeMoveHandle(
                currentPos,
                handleSize,
                Vector3.zero,
                Handles.SphereHandleCap
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetScript, "Move Bezier Control Point");
                controlPointProperty.vector3Value = newPosition;
                serializedObject.ApplyModifiedProperties();
            }

            // 绘制标签
            Handles.Label(currentPos + Vector3.up * 0.3f, $"CP {i}");
            
            // 绘制辅助线
            Vector3 start = pathPoints[i];
            Vector3 end = pathPoints[i + 1];
            Handles.color = new Color(1f, 0f, 1f, 0.3f);
            Handles.DrawLine(start, currentPos);
            Handles.DrawLine(currentPos, end);
            Handles.color = Color.magenta;
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    private List<Vector3> GetPathPoints()
    {
        var pathPoints = new List<Vector3>();

        Transform target = targetProperty.objectReferenceValue as Transform;
        Transform endTarget = endTargetProperty.objectReferenceValue as Transform;

        if (target == null)
            target = targetScript.transform;

        // 起始点
        pathPoints.Add(target.position);

        // 中间路径点
        for (int i = 0; i < waypointsProperty.arraySize; i++)
        {
            SerializedProperty waypointProperty = waypointsProperty.GetArrayElementAtIndex(i);
            Transform waypoint = waypointProperty.objectReferenceValue as Transform;
            if (waypoint != null)
            {
                pathPoints.Add(waypoint.position);
            }
        }

        // 结束点
        if (endTarget != null)
        {
            pathPoints.Add(endTarget.position);
        }
        else if (pathPoints.Count > 1)
        {
            // 如果没有指定 endTarget，使用最后一个 waypoint 的位置
            pathPoints.Add(pathPoints[pathPoints.Count - 1]);
        }

        return pathPoints;
    }
}
#endif
