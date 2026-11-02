#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ChestGiftPopup Prefab 构建工具。
/// 仅在显式菜单或批处理命令中运行，不通过 OnValidate/Reset 访问运行时 DB 或 Manager。
/// </summary>
public static class ChestGiftPopupPrefabBuilder
{
    #region 配置

    private const string BasePrefabPath = "Assets/Game/UI/GiftPopup/Prefabs/ChestGiftPopup.prefab";
    private const string ItemPrefabPath = "Assets/Game/UI/GiftPopup/Prefabs/ChestGiftItem.prefab";

    private static readonly IReadOnlyDictionary<string, string> TextBindings =
        new Dictionary<string, string>
        {
            ["UiLibao201Txt"] = "ui_libao2_01_txt",
            ["UiLibao202Txt"] = "ui_libao2_02_txt",
            ["UiLibao204Txt"] = "ui_libao2_04_txt",
            ["UiLibao205Txt"] = "ui_libao2_05_txt",
        };

    #endregion

    #region 构建入口

    /// <summary>
    /// 只补齐 ChestGift 母版的组件绑定。
    /// Resources 下的颜色变体由美术维护，工具不得重建或覆盖其视觉、Transform 和激活状态。
    /// </summary>
    [MenuItem("Tools/BrainPunk/Chest Gift/Bind Base Popup")]
    public static void BindBasePopup()
    {
        ConfigureBasePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ChestGiftPopupPrefabBuilder] Bound ChestGift base popup. Resources color variants were not modified.");
    }

    /// <summary>供 Unity -executeMethod 使用的无参入口。</summary>
    public static void BindBasePopupBatch()
    {
        BindBasePopup();
    }

    /// <summary>
    /// 兼容旧的编辑器调用入口；现在只绑定母版，不再重建 Resources 颜色变体。
    /// 新调用应改用 <see cref="BindBasePopup"/>。
    /// </summary>
    [Obsolete("Use BindBasePopup. Resource variants are designer-owned and are no longer rebuilt.")]
    public static void RebuildPopupVariants()
    {
        BindBasePopup();
    }

    /// <summary>
    /// 兼容旧的批处理入口；现在只绑定母版，不再重建 Resources 颜色变体。
    /// 新批处理应改用 <see cref="BindBasePopupBatch"/>。
    /// </summary>
    [Obsolete("Use BindBasePopupBatch. Resource variants are designer-owned and are no longer rebuilt.")]
    public static void RebuildPopupVariantsBatch()
    {
        BindBasePopup();
    }

    #endregion

    #region Prefab 构建

    private static void ConfigureBasePrefab()
    {
        GameObject itemPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
        ChestGiftItem itemPrefab = itemPrefabAsset != null
            ? itemPrefabAsset.GetComponent<ChestGiftItem>()
            : null;
        if (itemPrefab == null)
            throw new InvalidOperationException($"ChestGift item prefab or component not found: {ItemPrefabPath}");

        GameObject root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            ChestGiftPopup popup = root.GetComponent<ChestGiftPopup>();
            if (popup == null)
                popup = root.AddComponent<ChestGiftPopup>();

            SerializedObject serializedPopup = new SerializedObject(popup);
            SetObjectReference(
                serializedPopup,
                "UIZhuyeDa01Btn",
                FindRequiredComponent<CustomButton>(root.transform, "UI_zhuye_btn_da_01"));
            SetObjectReference(
                serializedPopup,
                "itemContainer",
                FindRequiredComponent<RectTransform>(root.transform, "Container"));
            SetObjectReference(serializedPopup, "itemPrefab", itemPrefab);

            foreach (KeyValuePair<string, string> binding in TextBindings)
            {
                TMPro.TextMeshProUGUI text = FindOptionalComponent<TMPro.TextMeshProUGUI>(
                    root.transform,
                    binding.Value);
                if (text != null)
                    SetObjectReference(serializedPopup, binding.Key, text);
            }

            serializedPopup.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    #endregion

    #region 查找与绑定

    private static T FindRequiredComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindDescendant(root, objectName);
        if (target == null)
            throw new InvalidOperationException($"ChestGift object not found: {objectName}");

        T component = target.GetComponent<T>();
        if (component == null)
            throw new InvalidOperationException($"ChestGift component {typeof(T).Name} not found on: {objectName}");

        return component;
    }

    private static T FindOptionalComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindDescendant(root, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException($"ChestGift serialized property not found: {propertyName}");

        property.objectReferenceValue = value;
    }

    #endregion
}
#endif
