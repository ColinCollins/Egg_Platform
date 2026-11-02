using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 用于控制 SpriteRenderFragilePlatform 脚本的 actor 接触开关以及 selectbox 表现。
/// selectBOx 是一个 spriteRenderer 控制是否展示。
/// </summary>
public class Level62BlockItemCtrl : MonoBehaviour
{
    [Header("选中表现")]
    [SerializeField] private SpriteRenderer selectBox;

    [Header("易碎平台")]
    [SerializeField] private SpriteRenderFragilePlatform fragilePlatform;

    [Header("透明度表现")]
    [SerializeField] private SpriteRendererGroupAlpha alphaGroup;
    [Range(0f, 1f)]
    [SerializeField] private float selectedAlpha = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float unselectedAlpha = 0.7f;

    [Header("框选判定")]
    [SerializeField] private Transform centerPointOverride;
    [SerializeField] private float hitPadding = 0.05f;

    private bool _isSelected;

    public bool IsSelected => _isSelected;

    public Vector3 CenterPosition => centerPointOverride != null ? centerPointOverride.position : transform.position;

    private void Awake()
    {
        ResolveReferences();
        ResetSelection();
    }

    public void ResetSelection()
    {
        SetSelected(false);
    }

    public void RestoreUnselectedAfterFragileFade()
    {
        ResolveReferences();

        _isSelected = false;

        if (selectBox != null)
        {
            selectBox.enabled = false;
        }

        ApplySelectionAlpha(false);

        if (fragilePlatform != null)
        {
            fragilePlatform.SetActorContactEnabled(false);
        }
    }

    public void SetSelected(bool selected)
    {
        ResolveReferences();

        _isSelected = selected;

        bool canApplyPresentation = fragilePlatform == null || !fragilePlatform.IsBreaking;

        if (selectBox != null)
        {
            selectBox.enabled = selected && canApplyPresentation;
        }

        if (canApplyPresentation)
        {
            ApplySelectionAlpha(selected);
        }

        if (fragilePlatform != null)
        {
            fragilePlatform.SetActorContactEnabled(selected);
        }

        if (selected)
            AudioManager.PlaySound("level62Select");
    }

    public void Select()
    {
        SetSelected(true);
    }

    public void Deselect()
    {
        SetSelected(false);
    }

    public bool HasSelectBox()
    {
        ResolveReferences();
        return selectBox != null;
    }

    public bool HasFragilePlatform()
    {
        ResolveReferences();
        return fragilePlatform != null;
    }

    public bool ContainsWorldPoint(Vector3 worldPosition)
    {
        ResolveReferences();

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer rendererItem = renderers[i];
            if (rendererItem == null || rendererItem == selectBox)
            {
                continue;
            }

            Bounds bounds = rendererItem.bounds;
            float minX = bounds.min.x - hitPadding;
            float maxX = bounds.max.x + hitPadding;
            float minY = bounds.min.y - hitPadding;
            float maxY = bounds.max.y + hitPadding;

            if (worldPosition.x >= minX && worldPosition.x <= maxX &&
                worldPosition.y >= minY && worldPosition.y <= maxY)
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (fragilePlatform == null)
        {
            fragilePlatform = GetComponent<SpriteRenderFragilePlatform>();
        }

        if (alphaGroup == null)
        {
            alphaGroup = GetComponent<SpriteRendererGroupAlpha>();
        }

        if (selectBox == null)
        {
            selectBox = FindSelectBoxInChildren();
        }
    }

    private void ApplySelectionAlpha(bool selected)
    {
        if (alphaGroup == null)
        {
            return;
        }

        alphaGroup.SetAlpha(selected ? selectedAlpha : unselectedAlpha);
    }

    private SpriteRenderer FindSelectBoxInChildren()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var rendererItem = renderers[i];
            if (rendererItem == null || rendererItem.transform == transform)
            {
                continue;
            }

            string itemName = rendererItem.name.ToLowerInvariant();
            if (itemName.Contains("select") || itemName.Contains("xzk"))
            {
                return rendererItem;
            }
        }

        return null;
    }
}
