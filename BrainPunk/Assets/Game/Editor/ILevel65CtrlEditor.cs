#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ILevel65Ctrl 的编辑器快速创建工具：复制指定场景 Target，并按世界坐标 X/Y 轴等距排列。
/// </summary>
[CustomEditor(typeof(ILevel65Ctrl))]
public sealed class ILevel65CtrlEditor : Editor
{
    #region Constants

    private const string UndoCreateName = "ILevel65 Quick Create Targets";
    private const string UndoStartPositionName = "ILevel65 Set Quick Create Start";

    #endregion

    #region Serialized Properties

    private SerializedProperty _quickCreateTarget;
    private SerializedProperty _quickCreateAxis;
    private SerializedProperty _quickCreateStartPosition;
    private SerializedProperty _quickCreateOffset;
    private SerializedProperty _quickCreateCount;

    #endregion

    #region Unity Editor Lifecycle

    private void OnEnable()
    {
        _quickCreateTarget = serializedObject.FindProperty("quickCreateTarget");
        _quickCreateAxis = serializedObject.FindProperty("quickCreateAxis");
        _quickCreateStartPosition = serializedObject.FindProperty("quickCreateStartPosition");
        _quickCreateOffset = serializedObject.FindProperty("quickCreateOffset");
        _quickCreateCount = serializedObject.FindProperty("quickCreateCount");
    }

    /// <summary>
    /// 绘制 ILevel65Ctrl 默认字段及快速创建操作按钮。
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Level 65 Quick Create", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "第一个新对象位于 Start Position，后续对象沿 Axis 每次增加 Offset。" +
            "新建数量不包含模板，所有新对象与 Target 保持同一父节点，并可一次 Undo。",
            MessageType.Info);

        GameObject source = _quickCreateTarget.objectReferenceValue as GameObject;
        using (new EditorGUI.DisabledScope(Application.isPlaying || source == null || EditorUtility.IsPersistent(source)))
        {
            if (GUILayout.Button("Start Position = Target 当前位置"))
                SetStartPositionFromTarget(source);
        }

        bool canCreate = ValidateCreateSettings(out string validationMessage);
        if (!string.IsNullOrEmpty(validationMessage))
            EditorGUILayout.HelpBox(validationMessage, canCreate ? MessageType.Info : MessageType.Warning);

        using (new EditorGUI.DisabledScope(!canCreate))
        {
            if (GUILayout.Button("按配置快速创建 Target", GUILayout.Height(28f)))
                CreateTargets();
        }
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        GameObject source = _quickCreateTarget.objectReferenceValue as GameObject;
        if (source == null || EditorUtility.IsPersistent(source))
            return;

        Vector2 startPosition = _quickCreateStartPosition.vector2Value;
        Vector3 startWorldPosition = new Vector3(startPosition.x, startPosition.y, source.transform.position.z);

        EditorGUI.BeginChangeCheck();
        Vector3 changedWorldPosition = Handles.PositionHandle(startWorldPosition, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, UndoStartPositionName);
            _quickCreateStartPosition.vector2Value = new Vector2(changedWorldPosition.x, changedWorldPosition.y);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        DrawLayoutPreview(startWorldPosition);
    }

    #endregion

    #region Quick Create

    private bool ValidateCreateSettings(out string message)
    {
        if (Application.isPlaying)
        {
            message = "快速创建仅允许在 Edit Mode 使用。";
            return false;
        }

        GameObject source = _quickCreateTarget.objectReferenceValue as GameObject;
        if (source == null)
        {
            message = "请先指定场景中的 Target。";
            return false;
        }

        if (EditorUtility.IsPersistent(source) || !source.scene.IsValid())
        {
            message = "Target 必须是当前场景或 Prefab Stage 中的对象，不能直接使用 Project 中的 Prefab 资源。";
            return false;
        }

        int count = _quickCreateCount.intValue;
        if (count < 1)
        {
            message = "创建数量必须大于 0。";
            return false;
        }

        if (count > 1 && Mathf.Approximately(_quickCreateOffset.floatValue, 0f))
        {
            message = "创建多个对象时 Offset 不能为 0，避免所有对象重叠。";
            return false;
        }

        message = null;
        return true;
    }

    private void SetStartPositionFromTarget(GameObject source)
    {
        if (Application.isPlaying || source == null || EditorUtility.IsPersistent(source))
            return;

        Undo.RecordObject(target, UndoStartPositionName);
        Vector3 sourcePosition = source.transform.position;
        _quickCreateStartPosition.vector2Value = new Vector2(sourcePosition.x, sourcePosition.y);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        SceneView.RepaintAll();
    }

    private void CreateTargets()
    {
        if (!ValidateCreateSettings(out _))
            return;

        GameObject source = (GameObject)_quickCreateTarget.objectReferenceValue;
        Vector2 startPosition = _quickCreateStartPosition.vector2Value;
        Vector3 startWorldPosition = new Vector3(startPosition.x, startPosition.y, source.transform.position.z);
        Vector3 direction = _quickCreateAxis.enumValueIndex == 0 ? Vector3.right : Vector3.up;
        float offset = _quickCreateOffset.floatValue;
        int count = _quickCreateCount.intValue;
        Transform parent = source.transform.parent;
        int firstSiblingIndex = source.transform.GetSiblingIndex() + 1;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoCreateName);

        var createdObjects = new List<GameObject>(count);
        for (int index = 0; index < count; index++)
        {
            GameObject createdObject = InstantiateTarget(source, parent);
            createdObject.name = GetUniqueSiblingName(createdObject, source.name);
            createdObject.transform.position = startWorldPosition + direction * (offset * index);
            createdObject.transform.SetSiblingIndex(firstSiblingIndex + index);
            Undo.RegisterCreatedObjectUndo(createdObject, UndoCreateName);
            createdObjects.Add(createdObject);
        }

        if (createdObjects.Count > 0)
        {
            Scene createdScene = createdObjects[0].scene;
            if (createdScene.IsValid())
                EditorSceneManager.MarkSceneDirty(createdScene);

            Selection.objects = createdObjects.ToArray();
        }

        Undo.CollapseUndoOperations(undoGroup);
        SceneView.RepaintAll();
    }

    private static GameObject InstantiateTarget(GameObject source, Transform parent)
    {
        GameObject createdObject = Object.Instantiate(source, parent, true);
        if (parent == null && source.scene.IsValid() && createdObject.scene != source.scene)
            SceneManager.MoveGameObjectToScene(createdObject, source.scene);

        return createdObject;
    }

    private static string GetUniqueSiblingName(GameObject createdObject, string sourceName)
    {
        var siblingNames = new HashSet<string>();
        Transform parent = createdObject.transform.parent;
        if (parent != null)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform sibling = parent.GetChild(index);
                if (sibling != createdObject.transform)
                    siblingNames.Add(sibling.name);
            }
        }
        else
        {
            foreach (GameObject rootObject in createdObject.scene.GetRootGameObjects())
            {
                if (rootObject != createdObject)
                    siblingNames.Add(rootObject.name);
            }
        }

        if (!siblingNames.Contains(sourceName))
            return sourceName;

        int suffix = 1;
        string candidate;
        do
        {
            candidate = $"{sourceName} ({suffix})";
            suffix++;
        } while (siblingNames.Contains(candidate));

        return candidate;
    }

    #endregion

    #region Scene Preview

    private void DrawLayoutPreview(Vector3 startWorldPosition)
    {
        int count = Mathf.Max(1, _quickCreateCount.intValue);
        Vector3 direction = _quickCreateAxis.enumValueIndex == 0 ? Vector3.right : Vector3.up;
        Vector3 endWorldPosition = startWorldPosition + direction * (_quickCreateOffset.floatValue * (count - 1));

        Handles.color = Color.cyan;
        Handles.DrawDottedLine(startWorldPosition, endWorldPosition, 4f);
        Handles.Label(startWorldPosition, "Quick Create Start");
        if (count > 1)
            Handles.Label(endWorldPosition, $"End ({count})");
    }

    #endregion
}
#endif
