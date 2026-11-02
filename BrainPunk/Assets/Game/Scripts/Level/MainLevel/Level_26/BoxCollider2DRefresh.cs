using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using TMPro;
using UnityEngine;

/// <summary>
/// 根据 TMP_Text 的实际渲染边界精确设置 BoxCollider2D。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BoxCollider2DRefresh : MonoBehaviour
{
    [SerializeField] private BoxCollider2D boxCollider;
    [Tooltip("目标 TMP 文本，为空时会尝试从当前物体获取")]
    [SerializeField] private TMP_Text tmpText;

    [Header("无 TMP 时的兜底")]
    [Tooltip("单个字符宽度")]
    [SerializeField] private float charWidth = 0.5f;
    [Tooltip("单行高度")]
    [SerializeField] private float lineHeight = 1f;

    private EventSubscriber _subscriber;

    private void Reset()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        tmpText = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (boxCollider == null)
            boxCollider = GetComponent<BoxCollider2D>();
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        AddListener();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchLanguageEvent>(OnSwitchLanguage);
    }

    private async void OnSwitchLanguage(SwitchLanguageEvent evt)
    {
        Refresh();

        await UniTask.WaitForSeconds(0.1f);

        Refresh();
    }

    /// <summary>
    /// 根据 TMP 文本实际渲染边界刷新 BoxCollider2D。
    /// 无 TMP 时按字符数量兜底。
    /// </summary>
    [ContextMenu("Refresh Size")]
    public void Refresh(int charCount = 1)
    {
        if (boxCollider == null)
            return;

        if (tmpText != null)
        {
            // 强制刷新 mesh，确保 textBounds 是最新的
            tmpText.ForceMeshUpdate();
            Bounds b = tmpText.textBounds;

            // textBounds 有效时直接用（精确对齐）
            if (b.size.x > 0 && b.size.y > 0)
            {
                boxCollider.size = new Vector2(b.size.x, b.size.y);
                boxCollider.offset = new Vector2(b.center.x, b.center.y);
                return;
            }

            // textBounds 异常时，用 fontSize 估算
            float fontSize = tmpText.fontSize;
            int count = string.IsNullOrEmpty(tmpText.text) ? 1 : tmpText.text.Length;
            boxCollider.size = new Vector2(count * fontSize * 0.5f, fontSize);
            boxCollider.offset = Vector2.zero;
        }
        else
        {
            // 无 TMP 时按传入字符数兜底
            boxCollider.size = new Vector2(charCount * charWidth, lineHeight);
            boxCollider.offset = Vector2.zero;
        }
    }

    /// <summary>
    /// 直接设置指定 size 和 offset。
    /// </summary>
    public void SetSizeAndOffset(Vector2 newSize, Vector2 newOffset)
    {
        if (boxCollider == null)
            return;

        boxCollider.size = newSize;
        boxCollider.offset = newOffset;
    }
}
