using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraFollowRange))]
public class CameraFollowRangeEditor : Editor
{
    private SerializedProperty _boundaryCenter;
    private SerializedProperty _boundarySize;
    private const float MIN_SIZE = 0.1f;

    private void OnEnable()
    {
        _boundaryCenter = serializedObject.FindProperty("boundaryCenter");
        _boundarySize = serializedObject.FindProperty("boundarySize");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        var range = (CameraFollowRange)target;
        if (range == null) return;

        serializedObject.Update();

        Vector2 center = _boundaryCenter.vector2Value;
        Vector2 size = _boundarySize.vector2Value;
        size.x = Mathf.Max(MIN_SIZE, size.x);
        size.y = Mathf.Max(MIN_SIZE, size.y);

        Vector2 half = size * 0.5f;
        Vector3 bl = new Vector3(center.x - half.x, center.y - half.y, 0f);
        Vector3 tl = new Vector3(center.x - half.x, center.y + half.y, 0f);
        Vector3 tr = new Vector3(center.x + half.x, center.y + half.y, 0f);
        Vector3 br = new Vector3(center.x + half.x, center.y - half.y, 0f);

        Handles.color = Color.cyan;
        Handles.DrawLine(bl, tl);
        Handles.DrawLine(tl, tr);
        Handles.DrawLine(tr, br);
        Handles.DrawLine(br, bl);

        float handleSize = HandleUtility.GetHandleSize((Vector3)center) * 0.08f;
        Handles.color = Color.white;

        // 4 corner resize handles:
        // Dragging one corner updates the rect while keeping it axis-aligned.
        // Opposite corner is treated as the anchor (so dragging inward works with a single handle).
        bool changed = false;
        float minX = bl.x, maxX = tr.x, minY = bl.y, maxY = tr.y;

        changed |= TryHandleCorner("BL", bl, handleSize, ref minX, ref maxX, ref minY, ref maxY, anchorX: tr.x, anchorY: tr.y, setMinX: true, setMaxX: false, setMinY: true, setMaxY: false);
        changed |= TryHandleCorner("TL", tl, handleSize, ref minX, ref maxX, ref minY, ref maxY, anchorX: br.x, anchorY: br.y, setMinX: true, setMaxX: false, setMinY: false, setMaxY: true);
        changed |= TryHandleCorner("TR", tr, handleSize, ref minX, ref maxX, ref minY, ref maxY, anchorX: bl.x, anchorY: bl.y, setMinX: false, setMaxX: true, setMinY: false, setMaxY: true);
        changed |= TryHandleCorner("BR", br, handleSize, ref minX, ref maxX, ref minY, ref maxY, anchorX: tl.x, anchorY: tl.y, setMinX: false, setMaxX: true, setMinY: true, setMaxY: false);

        if (changed)
        {
            NormalizeMinMax(ref minX, ref maxX, MIN_SIZE);
            NormalizeMinMax(ref minY, ref maxY, MIN_SIZE);

            Vector2 newCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            Vector2 newSize = new Vector2(maxX - minX, maxY - minY);

            Undo.RecordObject(range, "Edit Camera Follow Range");
            _boundaryCenter.vector2Value = newCenter;
            _boundarySize.vector2Value = newSize;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(range);
        }

        // Small label
        Handles.color = Color.cyan;
        Handles.Label(new Vector3(center.x, center.y, 0f), $"Range\nCenter: {center}\nSize: {size}");
    }

    private static bool TryHandleCorner(
        string label,
        Vector3 pos,
        float handleSize,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        float anchorX,
        float anchorY,
        bool setMinX,
        bool setMaxX,
        bool setMinY,
        bool setMaxY)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.Slider2D(pos, Vector3.forward, Vector3.right, Vector3.up, handleSize, Handles.DotHandleCap, 0f);
        if (!EditorGUI.EndChangeCheck()) return false;

        // Keep handles in 2D plane.
        newPos.z = 0f;

        // Apply dragged corner to min/max, anchored by opposite corner.
        if (setMinX) minX = Mathf.Min(newPos.x, anchorX);
        if (setMaxX) maxX = Mathf.Max(newPos.x, anchorX);
        if (setMinY) minY = Mathf.Min(newPos.y, anchorY);
        if (setMaxY) maxY = Mathf.Max(newPos.y, anchorY);

        return true;
    }

    private static void NormalizeMinMax(ref float min, ref float max, float minSize)
    {
        if (min > max)
        {
            float t = min;
            min = max;
            max = t;
        }

        if (max - min < minSize)
        {
            float c = (min + max) * 0.5f;
            min = c - minSize * 0.5f;
            max = c + minSize * 0.5f;
        }
    }
}

