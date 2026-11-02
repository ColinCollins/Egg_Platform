using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VerificationItem : MonoBehaviour
{
    public Image Img;
    public CustomButton Btn;

    public GameObject RightIcon;

    [SerializeField]
    [Tooltip("缩放作用节点；不拖则使用本物体 transform")]
    private RectTransform root;

    [SerializeField]
    [Tooltip("选中时的目标缩放，与 ClickScaleAnim 默认按下态一致")]
    private float selectedScale = 0.8f;
    [SerializeField]
    private float delay = 0f;
    [SerializeField]
    private float duration = 0.1f;
    [SerializeField]
    private bool ignoreScaleTime = true;

    [SerializeField]
    [Tooltip("调试：子物体上挂 TMP 并拖到这里；仅编辑器 Play 时刷新文本（正式包不赋值即可）")]
    private TextMeshProUGUI debugCountLabel;

    /// <summary> 当前格子对应的图片序号（1~16）；隐藏格为 -1 </summary>
    public int AssignedCount { get; private set; }

    public bool isSelected { get; private set; }

    private Vector3 _startScale = Vector3.one;
    private Vector3 _targetScale = Vector3.one;
    private float _timer;
    private float _currentDelay;
    private bool _isAnimating;

    private Transform ScaleTarget => root != null ? root : transform;

    private void Start()
    {
        Btn.OnClick += SwitchState;
        ScaleTarget.localScale = Vector3.one;
    }

    /// <summary> 设置本格展示的图片与序号，并重置为未选中。assignedCount 为 -1 表示隐藏格 </summary>
    public void SetContent(int assignedCount, Sprite sprite)
    {
        AssignedCount = assignedCount;
        if (sprite != null)
            Img.sprite = sprite;
        else if (assignedCount < 0)
            Img.sprite = null;
        isSelected = false;
        RightIcon.SetActive(false);
        _isAnimating = false;
        _timer = 0f;
        _currentDelay = 0f;
        ScaleTarget.localScale = Vector3.one;
    }

    /// <summary> 立即切换选中外观（用于 Reset 等，无插值动画） </summary>
    public void SetSelectionState(bool selected)
    {
        if (isSelected == selected)
            return;
        isSelected = selected;
        RightIcon.SetActive(isSelected);
        _isAnimating = false;
        _timer = 0f;
        _currentDelay = 0f;
        ScaleTarget.localScale = isSelected ? Vector3.one * selectedScale : Vector3.one;
    }

    private void SwitchState(CustomButton btn)
    {
        isSelected = !isSelected;
        RightIcon.SetActive(isSelected);
        BeginScaleToSelectionState();
    }

    private void BeginScaleToSelectionState()
    {
        _startScale = ScaleTarget.localScale;
        _targetScale = isSelected ? Vector3.one * selectedScale : Vector3.one;
        _timer = 0f;
        _currentDelay = delay;
        _isAnimating = true;
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying && debugCountLabel != null)
        {
            debugCountLabel.text = $"{AssignedCount}";
            if (!debugCountLabel.gameObject.activeSelf)
                debugCountLabel.gameObject.SetActive(true);
        }
#endif

        if (!_isAnimating)
            return;

        float deltaTime = ignoreScaleTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (_currentDelay > 0f)
        {
            _currentDelay -= deltaTime;
            if (_currentDelay > 0f)
                return;
            deltaTime = -_currentDelay;
            _currentDelay = 0f;
        }

        if (duration <= 0f)
        {
            ScaleTarget.localScale = _targetScale;
            _isAnimating = false;
            return;
        }

        _timer += deltaTime;
        float t = Mathf.Clamp01(_timer / duration);
        ScaleTarget.localScale = Vector3.LerpUnclamped(_startScale, _targetScale, t);

        if (t >= 1f)
        {
            ScaleTarget.localScale = _targetScale;
            _isAnimating = false;
        }
    }
}
