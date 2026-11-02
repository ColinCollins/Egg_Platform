using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using UnityEngine;

public class Level6GamePanelFeature : MonoBehaviour
{
    [SerializeField] private string passwordPopupName = "PasswordPopup";

    private EventSubscriber _subscriber;

    public EventSubscriber Subscriber => _subscriber;

    public CustomButton Use_btn;

    void Awake()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchObjActiveEvent>(OnSwitchObj);

        Use_btn.OnClick += OnShowPasswordPopup;
    }

    private void OnTiggerItem(OnTiggerItemEvent evt)
    {
        if (evt.EventId == 1)
        {
            var trans = Use_btn.transform;
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f);
        }
    }

    private void OnSwitchObj(SwitchObjActiveEvent evt)
    {
        var trans = Use_btn.transform;
        if (evt.isShow)
        {
            Use_btn.gameObject.SetActive(evt.isShow);

            trans.DOKill();
            trans.localScale = Vector3.one * 0.5f;
            trans.DOScale(Vector3.one, 0.1f);
        }
        else
        {
            trans.DOKill();
            trans.localScale = Vector3.one;
            trans.DOScale(Vector3.zero, 0.1f);
        }
    }

    private void OnShowPasswordPopup(CustomButton btn)
    {
        PasswordPopup.Create(passwordPopupName);
    }

    /// <summary>
    /// 为复用本面板的关卡指定密码弹窗资源名。
    /// 调用边界：仅关卡初始化阶段调用；空值会恢复默认 PasswordPopup，避免影响 Level6 的原有入口。
    /// 注意事项：目标资源必须位于 Resources 并包含 PasswordPopup 组件。
    /// </summary>
    public void ConfigurePasswordPopup(string popupName)
    {
        passwordPopupName = string.IsNullOrEmpty(popupName) ? "PasswordPopup" : popupName;
    }

    void OnDestroy()
    {
        Use_btn.OnClick -= OnShowPasswordPopup;
        EventsUtils.ResetEvents(ref _subscriber);

        if (Use_btn)
        {
            Use_btn.transform.DOKill();
        }
    }
}
