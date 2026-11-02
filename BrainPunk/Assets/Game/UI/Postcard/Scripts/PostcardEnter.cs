using System.Linq;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 明信片界面活动入口
/// CustomButton 点击打开 PostcardPopup。
/// OnEnable 的时候，检查一下 RemoteConfig 配置，IsOpenPostcard 是否 = true。若为 false，则不显示 gameObject
/// 
/// </summary>
public class PostcardEnter : MonoBehaviour
{
    private CustomButton button;

    private bool isForceShow = false;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!Application.isPlaying)
            return;

        if (isForceShow)
        {
            isForceShow = false;
            return;
        }

        if (!ConfigManager.RemoteConfig.IsOpenPostcard() || IsLockLevel() || !DB.Postcard.HasGuide)
        {
            gameObject.SetActive(false);
            return;
        }

        button ??= GetComponentInChildren<CustomButton>(true);
        if (button != null)
        {
            button.OnClick -= OpenPostcardPopup;
            button.OnClick += OpenPostcardPopup;
        }

        gameObject.SetActive(true);
        RedDotManager.Refresh(RedDotKey.Postcard);
    }

    public void ForceShow()
    {
        isForceShow = true;

        button ??= GetComponentInChildren<CustomButton>(true);
        if (button != null)
        {
            button.OnClick -= OpenPostcardPopup;
            button.OnClick += OpenPostcardPopup;
        }

        gameObject.SetActive(true);
        RedDotManager.Refresh(RedDotKey.Postcard);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
    }

    private void OnDisable()
    {
        if (button != null)
            button.OnClick -= OpenPostcardPopup;
    }

    private bool HasGuide()
    {
        return DB.Postcard.HasGuide;
    }

    private bool IsLockLevel()
    {
        int unlockLevel = ConfigManager.RemoteConfig.GetPostcardGuideLevel();
        var passedLevels = DB.GameData?.PassedLevels;
        var maxLevelId = passedLevels != null && passedLevels.Count > 0
            ? passedLevels.Max()
            : 0;

        return maxLevelId < unlockLevel;
    }

    private void OpenPostcardPopup(CustomButton _)
    {
        PostcardPopup.Create();
    }
}
