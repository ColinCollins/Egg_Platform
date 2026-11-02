using System.Collections;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level080 运行时配置：复用 Level6 的玩法面板，但将密码弹窗替换为本关专用变体。
/// </summary>
public sealed class Level80Ctrl : MonoBehaviour
{
    private const string PasswordPopupName = "PasswordPopup_80_5";

    private IEnumerator Start()
    {
        while (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            yield return null;

        Level6GamePanelFeature panelFeature = PlayCtrl.Instance.CurrentGamePlayPanel
            .GetComponent<Level6GamePanelFeature>();
        if (panelFeature == null)
        {
            Debug.LogWarning("[Level80Ctrl] Level6GamePanelFeature is missing from GamePlayPanel_001.");
            yield break;
        }

        panelFeature.ConfigurePasswordPopup(PasswordPopupName);
    }
}
