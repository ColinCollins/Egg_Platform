using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public delegate void CustomButtonClick(CustomButton btn);

public sealed class CustomButton : MonoBehaviour,
IPointerClickHandler,
IPointerDownHandler,
IPointerUpHandler,
IPointerEnterHandler,
IPointerExitHandler,
ICancelHandler
{
    // 是否可以交互
    [SerializeField] private bool _interactable = true;
    [SerializeField] private bool _hasAim = true;
    [SerializeField] private bool _hasDelay = true;

    [SerializeField] private bool isListenEnter = false;

    [SerializeField] private bool isListenExit = false;

    // [ShowIf("_hasDelay")]
    public float DelayTime = .1f;

    private bool isTrigger = false;

    private float _waitingTime = 0;

    private Graphic _graphic;
    private Collider2D _collider2D;

    // 追踪指针状态，用于在 Disable 时强制复位
    private bool _isPointerInside = false;
    private bool _isPointerDown = false;
    // Interactable switch handle
    private delegate void WhenInteractableSwitch(bool isOn);
    private event WhenInteractableSwitch InteractableHandle;

    private Action<bool> OnButtonDownPerformer;
    private Action<bool> OnButtonUpPerformer;

    // 临时用于 Button 不要触发 Click 事件     
    public bool IsBlockClick { get; set; }

    public Graphic BGraphic => _graphic;

    public bool Interactable
    {
        get => _interactable;
        set
        {
            _interactable = value;
            if (_collider2D != null)
                _collider2D.enabled = value;

            if (_graphic != null)
                _graphic.raycastTarget = value;

            InteractableHandle?.Invoke(value);
        }
    }

    public event CustomButtonClick OnClick;

    public event CustomButtonClick OnClickDown;
    public event CustomButtonClick OnClickUp;
    public event CustomButtonClick OnClickEnter;
    public event CustomButtonClick OnClickExit;



    void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
        _graphic = GetComponent<Graphic>();

        var interactableComps = GetComponentsInChildren<ButtonInteractableBase>();
        foreach (var comp in interactableComps)
            InteractableHandle += comp.OnSwitchInteractable;

        var clickTriggerComps = GetComponentsInChildren<ButtonClickTrigger>();
        foreach (var comp in clickTriggerComps)
        {
            OnButtonDownPerformer += comp.OnButtonDown;
            OnButtonUpPerformer += comp.OnButtonUp;
        }
    }

    void OnEnable()
    {
        // 每次激活时强制初始化状态，防止重新显示时残留旧状态
        _isPointerInside = false;
        _isPointerDown = false;
        isTrigger = false;
        _waitingTime = 0;
    }

    void OnDisable()
    {
        isTrigger = false;
        _waitingTime = 0;

        // 如果在 Enter 状态被 Disable，强制补发视觉复位
        if (_isPointerInside)
        {
            _isPointerInside = false;
            // Exit 的视觉表现也是调用 UpPerformer，这里统一复位
            OnButtonUpPerformer?.Invoke(_hasAim);
            if (isListenExit)
                OnClickExit?.Invoke(this);
        }

        // 如果在 Down 状态被 Disable，强制补发 Up 让视觉组件复位
        if (_isPointerDown)
        {
            _isPointerDown = false;
            OnButtonUpPerformer?.Invoke(_hasAim);
            OnClickUp?.Invoke(this);
        }
    }

    void Update()
    {
        if (!isTrigger)
            return;

        _waitingTime += Time.unscaledDeltaTime;

        if (_waitingTime >= DelayTime)
        {
            _waitingTime = 0;
            isTrigger = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !_interactable || isTrigger || IsBlockClick)
            return;

        isTrigger = true;
        OnClick?.Invoke(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;

        _isPointerDown = true;
        OnButtonDownPerformer?.Invoke(_hasAim);
        OnClickDown?.Invoke(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;

        _isPointerDown = false;
        OnButtonUpPerformer?.Invoke(_hasAim);
        OnClickUp?.Invoke(this);
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!isActiveAndEnabled) return;
        this.OnPointerUp((PointerEventData)eventData);
    }

    private void OnDestroy()
    {
        InteractableHandle = null;
        OnButtonDownPerformer = null;
        OnButtonUpPerformer = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !_interactable || !isListenEnter)
            return;

        _isPointerInside = true;
        OnButtonDownPerformer?.Invoke(_hasAim);
        OnClickEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || !_interactable || !isListenExit)
            return;

        _isPointerInside = false;
        OnButtonUpPerformer?.Invoke(_hasAim);
        OnClickExit?.Invoke(this);
    }
}
