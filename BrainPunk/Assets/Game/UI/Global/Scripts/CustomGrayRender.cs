using System.Collections.Generic;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies grayscale rendering to Graphics under this node and restores their original render state when disabled.
/// </summary>
public class CustomGrayRender : MonoBehaviour
{
#if UNITY_EDITOR
    private const string DefaultGrayMaterialAssetPath = "Assets/Game/ArtSrc/Shader/UI/CustomGray.mat";
#endif

    [SerializeField] private bool isGray;
    [SerializeField] private Material grayMaterial;
    [SerializeField] private bool includeInactive = true;
    // 是否在启用、刷新或 Inspector 校验时自动收集当前节点下的 Graphic；关闭后只处理 graphics 中手动记录的对象。
    [SerializeField] private bool autoCollectGraphics = true;
    // 需要统一切换灰度表现的 UI Graphic 记录，通常由 CollectGraphics 从当前节点子层级收集。
    [SerializeField] private Graphic[] graphics;

    private readonly Dictionary<Graphic, Material> _originalMaterials = new Dictionary<Graphic, Material>();
    private readonly Dictionary<Graphic, Color> _originalTmpTextColors = new Dictionary<Graphic, Color>();
    private readonly List<Graphic> _restoreBuffer = new List<Graphic>();

    private static Material _defaultGrayMaterial;

    public bool IsGray => isGray;

    private void OnEnable()
    {
        AutoCollectGraphics();
        ApplyGrayState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoCollectGraphics();

        if (Application.isPlaying)
        {
            ApplyGrayState();
        }
    }

    /// <summary>
    /// Inspector/Odin 按钮入口，用于手动切换当前节点 UI 灰度预览。
    /// 调用边界：仅用于编辑器调试或运行时临时验证；业务代码需要指定目标状态时优先调用 SetGray(bool)。
    /// 注意事项：切换前会按 autoCollectGraphics 规则刷新 graphics，关闭灰度时会恢复已经缓存的原材质。
    /// </summary>
    [Button("SwitchGray")]
    public void SwitchRenderState()
    {
        const string UndoName = "Switch Custom Gray Render";

        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(this, UndoName);
        }

        isGray = !isGray;
        AutoCollectGraphics();

        if (!Application.isPlaying)
        {
            RecordGraphicsForUndo(UndoName);
        }

        if (isGray)
        {
            ApplyGrayState();
        }
        else
        {
            RestoreOriginalMaterials();
        }

        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    // 仅供编辑器按钮预览使用：记录将被灰度切换影响的 Graphic，保证 Inspector 手动切换可撤销。
    private void RecordGraphicsForUndo(string undoName)
    {
        if (graphics != null)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null)
                {
                    UnityEditor.Undo.RecordObject(graphic, undoName);
                }
            }
        }

        foreach (Graphic graphic in _originalMaterials.Keys)
        {
            if (graphic != null)
            {
                UnityEditor.Undo.RecordObject(graphic, undoName);
            }
        }

        foreach (Graphic graphic in _originalTmpTextColors.Keys)
        {
            if (graphic != null)
            {
                UnityEditor.Undo.RecordObject(graphic, undoName);
            }
        }
    }
#endif

    private void OnDisable()
    {
        RestoreOriginalMaterials();
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterials();
    }

    public void SetGray(bool value)
    {
        if (isGray == value)
        {
            return;
        }

        isGray = value;
        ApplyGrayState();
    }

    public void SetGrayWithoutCheck(bool value)
    {
        isGray = value;
        ApplyGrayState();
    }

    public void Refresh()
    {
        AutoCollectGraphics();
        ApplyGrayState();
    }

    private void ApplyGrayState()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (isGray)
        {
            ApplyGrayMaterials();
        }
        else
        {
            RestoreOriginalMaterials();
        }
    }

    private void ApplyGrayMaterials()
    {
        RemoveMissingOrOutOfScopeGraphics();

        if (graphics == null)
        {
            return;
        }

        Material material = null;
        bool resolvedGrayMaterial = false;
        bool grayMaterialUnavailable = false;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            if (graphic is TMP_Text tmpText)
            {
                ApplyTmpTextGrayColor(graphic, tmpText);
                continue;
            }

            if (grayMaterialUnavailable)
            {
                continue;
            }

            if (!resolvedGrayMaterial)
            {
                material = ResolveGrayMaterial();
                resolvedGrayMaterial = true;

                if (material == null)
                {
                    RestoreOriginalGraphicMaterials();
                    grayMaterialUnavailable = true;
                    continue;
                }
            }

            ApplyGraphicGrayMaterial(graphic, material);
        }
    }

    private void ApplyGraphicGrayMaterial(Graphic graphic, Material material)
    {
        if (!_originalMaterials.ContainsKey(graphic))
        {
            _originalMaterials.Add(graphic, graphic.material);
        }

        if (graphic.material != material)
        {
            graphic.material = material;
        }
    }

    private void ApplyTmpTextGrayColor(Graphic graphic, TMP_Text tmpText)
    {
        if (!_originalTmpTextColors.ContainsKey(graphic))
        {
            _originalTmpTextColors.Add(graphic, tmpText.color);
        }

        Color grayColor = ToGrayColor(_originalTmpTextColors[graphic]);
        if (tmpText.color != grayColor)
        {
            tmpText.color = grayColor;
        }
    }

    private static Color ToGrayColor(Color color)
    {
        float gray = color.grayscale;
        return new Color(gray, gray, gray, color.a);
    }

    private void RestoreOriginalMaterials()
    {
        RestoreOriginalGraphicMaterials();
        RestoreOriginalTmpTextColors();
    }

    private void RestoreOriginalGraphicMaterials()
    {
        if (_originalMaterials.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Graphic, Material> pair in _originalMaterials)
        {
            Graphic graphic = pair.Key;
            if (graphic == null)
            {
                continue;
            }

            if (graphic.material != pair.Value)
            {
                graphic.material = pair.Value;
            }
        }

        _originalMaterials.Clear();
    }

    private void RestoreOriginalTmpTextColors()
    {
        if (_originalTmpTextColors.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<Graphic, Color> pair in _originalTmpTextColors)
        {
            TMP_Text tmpText = pair.Key as TMP_Text;
            if (tmpText == null)
            {
                continue;
            }

            if (tmpText.color != pair.Value)
            {
                tmpText.color = pair.Value;
            }
        }

        _originalTmpTextColors.Clear();
    }

    private Material ResolveGrayMaterial()
    {
        if (grayMaterial != null)
        {
            return grayMaterial;
        }

        if (_defaultGrayMaterial != null)
        {
            return _defaultGrayMaterial;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultGrayMaterialAssetPath);
        }
#endif

        Shader shader = Shader.Find("UI/CustomGray");
        if (shader == null)
        {
            return null;
        }

        _defaultGrayMaterial = new Material(shader)
        {
            name = "CustomGrayRender_DefaultGrayMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        return _defaultGrayMaterial;
    }

    /// <summary>
    /// 从当前节点子层级收集需要统一灰度切换的 Graphic。只读取本地子节点组件，适合动态 UI 子节点变化后显式刷新记录。
    /// </summary>
    public void CollectGraphics()
    {
        graphics = GetComponentsInChildren<Graphic>(includeInactive);
    }

    private void AutoCollectGraphics()
    {
        if (autoCollectGraphics)
        {
            CollectGraphics();
        }
    }

    private void RemoveMissingOrOutOfScopeGraphics()
    {
        RestoreMissingOrOutOfScopeMaterials();
        RestoreMissingOrOutOfScopeTmpTextColors();
    }

    private void RestoreMissingOrOutOfScopeMaterials()
    {
        if (_originalMaterials.Count == 0)
        {
            return;
        }

        _restoreBuffer.Clear();
        foreach (Graphic graphic in _originalMaterials.Keys)
        {
            if (graphic == null || !ContainsGraphic(graphic))
            {
                _restoreBuffer.Add(graphic);
            }
        }

        for (int i = 0; i < _restoreBuffer.Count; i++)
        {
            Graphic graphic = _restoreBuffer[i];
            if (graphic != null && _originalMaterials.TryGetValue(graphic, out Material originalMaterial))
            {
                graphic.material = originalMaterial;
            }

            _originalMaterials.Remove(graphic);
        }

        _restoreBuffer.Clear();
    }

    private void RestoreMissingOrOutOfScopeTmpTextColors()
    {
        if (_originalTmpTextColors.Count == 0)
        {
            return;
        }

        _restoreBuffer.Clear();
        foreach (Graphic graphic in _originalTmpTextColors.Keys)
        {
            if (graphic == null || !ContainsGraphic(graphic) || !(graphic is TMP_Text))
            {
                _restoreBuffer.Add(graphic);
            }
        }

        for (int i = 0; i < _restoreBuffer.Count; i++)
        {
            Graphic graphic = _restoreBuffer[i];
            if (graphic is TMP_Text tmpText && _originalTmpTextColors.TryGetValue(graphic, out Color originalColor))
            {
                tmpText.color = originalColor;
            }

            _originalTmpTextColors.Remove(graphic);
        }

        _restoreBuffer.Clear();
    }

    private bool ContainsGraphic(Graphic target)
    {
        if (graphics == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == target)
            {
                return true;
            }
        }

        return false;
    }
}
