using System;
using Bear.UI;
using Game.ConfigModule;
using I2.Loc;
using UnityEngine;

public partial class BulletinBoardPopup : BaseUIView
{
    private Action _onClosed;

    public override void OnOpen()
    {
        base.OnOpen();

        var title = LocalizationManager.GetTranslation("U_Notes_Title_02");
        if (UNotesTitle02Txt != null)
        {
            UNotesTitle02Txt.text = string.Format(title, Application.version);
        }

        if (UIGonggaoLiaojie01Btn != null)
        {
            UIGonggaoLiaojie01Btn.OnClick += ClosePopup;
        }
    }

    private void ClosePopup(CustomButton btn)
    {
        UIManager.Instance?.DestroyUI(this);
    }

    public override void OnCreate()
    {
        base.OnCreate();
        // TODO: Register buttons if more interactions are added later.

        if (DB.GameData != null)
        {
            DB.GameData.CouldPopupBulletinBoard = false;
            DB.GameData.Save();
        }
    }

    public override void OnClose()
    {
        Action onClosed = _onClosed;
        _onClosed = null;

        base.OnClose();

        if (UIGonggaoLiaojie01Btn != null)
        {
            UIGonggaoLiaojie01Btn.OnClick -= ClosePopup;
        }

        onClosed?.Invoke();
    }

    /// <summary>
    /// 创建版本更新公告。
    /// 调用边界：启动弹窗序列可传入关闭回调；普通入口可以省略。
    /// 注意事项：回调只在弹窗真实 OnClose 后触发一次，创建失败时不会触发。
    /// </summary>
    /// <param name="onClosed">弹窗关闭完成后的可选回调。</param>
    public static BulletinBoardPopup Create(Action onClosed = null)
    {
        if (UIManager.Instance == null)
        {
            return null;
        }

        var panel = UIManager.Instance.OpenUI<BulletinBoardPopup>($"{typeof(BulletinBoardPopup).Name}", UILayer.Popup);
        if (panel != null)
            panel._onClosed = onClosed;

        return panel;
    }
}
