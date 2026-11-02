using UnityEngine;
using System;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

public partial class ActorCtrl
{
    #region Editor

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器手动绑定入口：只用于 Inspector 按钮补齐 Actor 常用引用和默认碰撞层，不会在运行时构建中编译。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        // 自动绑定 pushCollider 以及 SpineCtrl
        // 如果 WhatIsGround 为 nothing，自动填充Layer: Ground
        // 如果 WhatIsWall 为 nothing，自动填充Layer: Ground
        // 如果 WhatIsStuff 为 nothing，自动填充Layer: Stuff
        GameObject bindPushCollider = FindChildGameObject(transform, "pushCollider");
        ActorSpineCtrl bindSpineCtrl = FindActorSpineCtrl();
        bool shouldBindPushCollider = bindPushCollider != null && pushCollider != bindPushCollider;
        bool shouldBindSpineCtrl = bindSpineCtrl != null && spineCtrl != bindSpineCtrl;
        bool shouldBindGround = ShouldSetLayerMask(whatIsGround, "Ground");
        bool shouldBindWall = ShouldSetLayerMask(whatIsWall, "Ground");
        bool shouldBindStuff = ShouldSetLayerMask(whatIsStuff, "Stuff");

        if (!shouldBindPushCollider && !shouldBindSpineCtrl && !shouldBindGround && !shouldBindWall && !shouldBindStuff)
            return;

        Undo.RecordObject(this, "ActorCtrl AutoBind");

        if (shouldBindPushCollider)
        {
            pushCollider = bindPushCollider;
        }

        if (shouldBindSpineCtrl)
        {
            spineCtrl = bindSpineCtrl;
        }

        if (shouldBindGround)
            whatIsGround = LayerMask.GetMask("Ground");
        if (shouldBindWall)
            whatIsWall = LayerMask.GetMask("Ground");
        if (shouldBindStuff)
            whatIsStuff = LayerMask.GetMask("Stuff");

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private ActorSpineCtrl FindActorSpineCtrl()
    {
        Transform spineTransform = transform.Find("spine");
        ActorSpineCtrl foundSpineCtrl = spineTransform != null ? spineTransform.GetComponent<ActorSpineCtrl>() : null;
        return foundSpineCtrl != null ? foundSpineCtrl : GetComponentInChildren<ActorSpineCtrl>(true);
    }

    private static GameObject FindChildGameObject(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child.gameObject;

            GameObject result = FindChildGameObject(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static bool ShouldSetLayerMask(LayerMask layerMask, string layerName)
    {
        if (layerMask.value != 0)
            return false;

        return LayerMask.GetMask(layerName) != 0;
    }
#endif

    #endregion
}
