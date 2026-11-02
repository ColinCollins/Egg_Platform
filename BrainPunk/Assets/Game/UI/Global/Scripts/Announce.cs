using Bear.UI;
using Config;
using I2.Loc;
using UnityEngine;

public partial class Announce : BaseUIView
{
    /// <summary>
    /// 进入 ignore 的 levelPath 不会展示 announce, 后期有需要在修改到配置中
    /// </summary>
    public static readonly string[] IgnoreLevels = new string[]
    {
        "Level_053"
    };

    [SerializeField] private Animator anim;
    [Header("自动关闭时间（秒）")]
    [SerializeField] private float duration = 2f;

    private float remainingTime;
    private bool isTiming;

    public override void OnOpen()
    {
        base.OnOpen();
        StartAutoCloseTimer();
    }

    private void SetLevel(int level)
    {
        LevelTxt.text = string.Format(LocalizationManager.GetTranslation("U_Annouce_des_01"), level);
    }

    private void OnDisable()
    {
        StopAutoCloseTimer();
    }

    private void Update()
    {
        if (!isTiming)
        {
            return;
        }

        // Keep realtime behavior like WaitForSecondsRealtime.
        remainingTime -= Time.unscaledDeltaTime;
        if (remainingTime <= 0f)
        {
            isTiming = false;
            UIManager.Instance.CloseUI(this);
        }
    }

    private void StartAutoCloseTimer()
    {
        if (duration <= 0f)
        {
            return;
        }

        StopAutoCloseTimer();
        remainingTime = duration;
        isTiming = true;
    }

    private void StopAutoCloseTimer()
    {
        isTiming = false;
        remainingTime = 0f;
    }

    private static bool ShouldIgnore(LevelData data)
    {
        if (data == null)
            return false;

        for (int i = 0; i < IgnoreLevels.Length; i++)
        {
            if (IgnoreLevels[i].Equals(data.Path))
            {
                return true;
            }
        }

        return false;
    }

    public static Announce Create(int level, LevelData data, float duration = -1f)
    {
        if (ShouldIgnore(data))
        {
            return null;
        }

        var panel = UIManager.Instance.OpenUI<Announce>(nameof(Announce), UILayer.Top, false);
        if (duration > 0f)
        {
            panel.duration = duration;
        }

        panel.SetLevel(level);
        // panel.anim.Play("");

        return panel;
    }

    public static void CloseStraightly()
    {
        UIManager.Instance.CloseUI<Announce>();
    }
}

