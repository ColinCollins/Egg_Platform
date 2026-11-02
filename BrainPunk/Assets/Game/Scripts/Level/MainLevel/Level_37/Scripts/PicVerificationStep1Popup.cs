using System;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;

public partial class PicVerificationStep1Popup : BaseUIView
{
    public event Action<bool> OnVerificationEnd;
    public event Action OnVerificationSuccess;

    private bool isFinished = false;

    public override void OnCreate()
    {
        base.OnCreate();
        // TODO: 注册按钮等（示例见 PictureVerifticationPopup）
        CloseBtn.OnClick += ClosePopup;
        UIRjyzXzk01Toggle.onValueChanged.AddListener(OnToggleClick);
    }

    private void OnToggleClick(bool arg0)
    {
        if (arg0 && !isFinished)
        {
            isFinished = true;
            CloseBtn.OnClick -= ClosePopup;
            ShowVeriftication().Forget();
            AudioManager.PlaySound("verifticationStep1");
        }
    }

    private async UniTask ShowVeriftication()
    {
        RjyzGouImg.transform.localScale = Vector3.zero;
        RjyzGouImg.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        await UniTask.WaitForSeconds(.7f);

        var step2 = PictureVerifticationPopup.Create();
        step2.OnVerificationEnd += OnVerificationEnd;
        step2.OnVerificationSuccess += OnVerificationSuccess;

        OnVerificationEnd = null;
        ClosePopup(null);
    }

    private void ClosePopup(CustomButton btn)
    {
        UIManager.Instance.DestroyUI(this);
    }

    public override void OnClose()
    {
        base.OnClose();
        OnVerificationEnd?.Invoke(false);
    }

    public static PicVerificationStep1Popup Create()
    {
        return UIManager.Instance.OpenUI<PicVerificationStep1Popup>(nameof(PicVerificationStep1Popup), UILayer.Popup);
    }
}
