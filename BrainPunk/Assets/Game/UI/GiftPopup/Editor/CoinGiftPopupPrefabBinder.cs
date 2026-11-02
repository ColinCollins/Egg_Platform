#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CoinGiftPopup Prefab 序列化绑定工具。
/// 只补齐脚本引用与空的 Shop Id，不修改任何节点的 Transform、层级或美术属性。
/// </summary>
public static class CoinGiftPopupPrefabBinder
{
    #region 常量

    private const string PrefabPath = "Assets/Game/UI/GiftPopup/Resources/CoinGiftPopup.prefab";

    private static readonly IReadOnlyDictionary<string, string> ObjectBindings =
        new Dictionary<string, string>
        {
            ["CloseBtn"] = "close_btn",
            ["UTopupcoins01Btn"] = "u_topupcoins_btn_01",
            ["UILibao1Libao101Btn"] = "UI_libao1_btn_libao1_01",
            ["UILibao1Libao201Btn"] = "UI_libao1_btn_libao2_01",
            ["UILibao1Libao202Btn"] = "UI_libao1_btn_libao2_02",
            ["UTopupcoinsTitle01Txt"] = "u_topupcoins_title_01_txt",
            ["UTopupcoinsDes01Txt"] = "u_topupcoins_des_01_txt",
            ["UTopupcoinsDes02Txt"] = "u_topupcoins_des_02_txt",
            ["UTopupcoinsDes03Txt"] = "u_topupcoins_des_03_txt",
            ["UTopupcoinsDes04Txt"] = "u_topupcoins_des_04_txt",
            ["UiLibao101Txt"] = "ui_libao1_01_txt",
            ["UiLibao102Txt"] = "ui_libao1_02_txt",
            ["UiLibao103Txt"] = "ui_libao1_03_txt",
            ["UiLibao104Txt"] = "ui_libao1_04_txt",
            ["UiLibao105Txt"] = "ui_libao1_05_txt",
            ["UiLibao106Txt"] = "ui_libao1_06_txt",
            ["UiLibao107Txt"] = "ui_libao1_07_txt",
            ["FirstPurchaseState"] = "firstPurchase_state",
            ["NormalPurchaseState"] = "normalPurchase_state",
        };

    #endregion

    #region 菜单入口

    /// <summary>
    /// 补齐 CoinGiftPopup 的序列化引用。
    /// 调用边界：只允许在 Unity Editor 中对固定 Prefab 执行；节点缺失或重名时中止保存。
    /// </summary>
    [MenuItem("Tools/BrainPunk/Coin Gift/Bind Popup References")]
    public static void BindPopup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CoinGiftPopup popup = root.GetComponent<CoinGiftPopup>();
            if (popup == null)
                throw new InvalidOperationException("CoinGiftPopup component is missing on prefab root.");

            SerializedObject serializedPopup = new SerializedObject(popup);
            serializedPopup.Update();
            SetStringIfEmpty(serializedPopup, "rewardAdShopId", "20000");
            SetStringIfEmpty(serializedPopup, "firstPurchaseShopId", "20013");
            SetStringIfEmpty(serializedPopup, "normalPurchaseShopId", "20003");

            foreach (KeyValuePair<string, string> binding in ObjectBindings)
            {
                Transform target = FindUniqueTransform(root.transform, binding.Value);
                SerializedProperty property = serializedPopup.FindProperty(binding.Key);
                if (property == null)
                    throw new InvalidOperationException($"Serialized field is missing: {binding.Key}");

                property.objectReferenceValue = ResolveReference(property, target);
            }

            serializedPopup.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(popup);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CoinGiftPopupPrefabBinder] CoinGiftPopup references were bound without Transform changes.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    #endregion

    #region 绑定辅助

    private static void SetStringIfEmpty(SerializedObject serializedObject, string fieldName, string defaultValue)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException($"Serialized field is missing: {fieldName}");

        if (string.IsNullOrWhiteSpace(property.stringValue))
            property.stringValue = defaultValue;
    }

    private static Transform FindUniqueTransform(Transform root, string objectName)
    {
        Transform found = null;
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                continue;

            if (found != null)
                throw new InvalidOperationException($"Prefab node name is duplicated: {objectName}");

            found = candidate;
        }

        return found ?? throw new InvalidOperationException($"Prefab node is missing: {objectName}");
    }

    private static UnityEngine.Object ResolveReference(SerializedProperty property, Transform target)
    {
        if (property.type == "PPtr<$GameObject>")
            return target.gameObject;

        if (property.name.EndsWith("Btn", StringComparison.Ordinal))
            return target.GetComponent<CustomButton>();

        if (property.name.EndsWith("Txt", StringComparison.Ordinal))
            return target.GetComponent<TMPro.TextMeshProUGUI>();

        throw new InvalidOperationException($"Unsupported binding type: {property.name} ({property.type})");
    }

    #endregion
}
#endif
