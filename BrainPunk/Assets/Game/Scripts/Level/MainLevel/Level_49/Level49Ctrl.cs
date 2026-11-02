using System;
using Bear.UI;
using UnityEngine;

public class Level49Ctrl : MonoBehaviour
{
    [SerializeField] private Collider2D passwordTrigger;
    private GamePanel_UseBtnCommonFeature commonFeature;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private PasswordPopup2 popup;
    void Start()
    {
        commonFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
        commonFeature.Use_btn.OnClick += ShowPassword2;
        commonFeature.Use_btn.gameObject.SetActive(false);
    }

    private void ShowPassword2(CustomButton btn)
    {
        popup = PasswordPopup2.Create();
    }

    public void CloseUseBtn()
    {
        commonFeature.Use_btn.gameObject.SetActive(false);
        passwordTrigger.enabled = false;
    }

    void OnDestroy()
    {
        
    }
}
