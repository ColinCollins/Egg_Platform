using Bear.EventSystem;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using I2.Loc;
using UnityEngine;

public class Level37_Ctrl : MonoBehaviour
{
    private GamePanel_UseBtnCommonFeature commonFeature;

    private PicVerificationStep1Popup step1Popup;

    [SerializeField] private ActorCtrl actorCtrl;
    [SerializeField] private BoxCollider2D doorCollider;
    [SerializeField] private ActorSpineCtrl doorSpineCtrl;

    [SerializeField] private SitableIStuffCtrl sitableChair;

    private bool isFinished = false;

    void Start()
    {
        commonFeature = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>();
        commonFeature.Use_btn.OnClick += ShowvVerification;
    }

    private void ShowvVerification(CustomButton btn)
    {   
        if (isFinished)
            return;

        StartVerificationFlow().Forget();
    }

    private async UniTask StartVerificationFlow()
    {
        await PlaySitAnim();

        step1Popup = PicVerificationStep1Popup.Create();
        if (step1Popup)
        {
            step1Popup.OnVerificationEnd += OnVerificationEnd;
            step1Popup.OnVerificationSuccess += OnVerificationSuccess;
        }
    }

    private async UniTask PlaySitAnim()
    {
        actorCtrl.PlaySit(sitableChair.transform, new Vector3(0.3f, .45f, 0f));
        
        await UniTask.WaitForSeconds(.8f, true);

        sitableChair.SitIn();

        await UniTask.WaitForSeconds(.5f, true);
    }

    private void OnVerificationEnd(bool isSuc)
    {
        actorCtrl.StopSit();
        sitableChair.SitOut();
    }

    private async void OnVerificationSuccess()
    {
        isFinished = true;
        actorCtrl.StopSit();
        doorCollider.enabled = true;
        doorSpineCtrl.PlayAnimation("door_open", false);
        AudioManager.PlaySound("openDoor");

        SystemTips.Show(LocalizationManager.GetTranslation("S_LevelTips_Des_level_37_09"));

        commonFeature.StopListener();
    }

    void OnDestroy()
    {
        if (commonFeature != null && commonFeature.Use_btn != null)
            commonFeature.Use_btn.OnClick -= ShowvVerification;
    }
}
