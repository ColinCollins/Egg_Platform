using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只在变色组件的 Inspector 中提供编辑预览，保存和退出时恢复临时显示。</summary>
[CustomEditor(typeof(SpineHeatTint))]
public sealed class SpineHeatTintEditor : Editor
{
    #region 预览生命周期
    private bool previewEnabled;
    private float previewHeat = 1f;

    private void OnEnable()
    {
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        PrefabStage.prefabSaving += OnPrefabSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private void OnDisable()
    {
        StopPreview();
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) StopPreview();
    }

    private void OnUndoRedo()
    {
        if (!previewEnabled || target == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        serializedObject.Update();
        RefreshPreview();
        Repaint();
    }

    private void OnPrefabSaving(GameObject root)
    {
        // Spine 会临时更新 Renderer 材质；保存前还原，避免将预览材质写入 prefab。
        StopPreview();
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        StopPreview();
    }
    #endregion

    #region 组件 Inspector
    /// <summary>颜色正常保存到物体组件；预览开关与热度只在当前 Inspector 会话有效。</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hotTint"), new GUIContent("亮部颜色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hotGlow"), new GUIContent("暗部发光色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pulseAmount"), new GUIContent("运行时呼吸幅度"));

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(target) || !((SpineHeatTint)target).isActiveAndEnabled))
        {
            bool enabled = EditorGUILayout.ToggleLeft("开启编辑预览", previewEnabled);
            if (enabled != previewEnabled)
            {
                if (enabled) previewEnabled = StartPreview();
                else StopPreview();
            }
            using (new EditorGUI.DisabledScope(!previewEnabled))
                previewHeat = EditorGUILayout.Slider("预览热度", previewHeat, 0f, 1f);
        }
        EditorGUILayout.HelpBox("选中场景或预制体中的此物体，开启预览即可调色。热度 0 为原色、1 为最热。保存、切换选择或进入运行时会结束临时预览，颜色参数会保留。", MessageType.Info);
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(previewEnabled))
            DrawPropertiesExcluding(serializedObject, "m_Script", "hotTint", "hotGlow", "pulseAmount");
        serializedObject.ApplyModifiedProperties();
        if (previewEnabled) RefreshPreview();
    }
    #endregion

    #region 组件预览调用
    private bool StartPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(target)) return false;
        bool ready = ((SpineHeatTint)target).BeginEditorPreview();
        if (!ready) Debug.LogWarning("请确认变色组件已启用、引用已绑定，并等待当前物体的 Spine 加载完成。", target);
        return ready;
    }

    private void RefreshPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || target == null) return;
        if (!((SpineHeatTint)target).isActiveAndEnabled) { StopPreview(); return; }
        ((SpineHeatTint)target).SetEditorPreviewHeat(previewHeat);
        SceneView.RepaintAll();
    }

    private void StopPreview()
    {
        if (!previewEnabled) return;
        previewEnabled = false;
        if (target != null) ((SpineHeatTint)target).RestoreAppearance();
        SceneView.RepaintAll();
        Repaint();
    }
    #endregion
}
