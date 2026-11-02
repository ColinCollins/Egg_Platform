using Lofelt.NiceVibrations;

public class ClickVibration : ButtonClickTrigger
{
    [UnityEngine.SerializeField]
    private HapticPatterns.PresetType presetType = HapticPatterns.PresetType.Selection;

    public override void OnButtonDown(bool hasAnim)
    {
        if (!DB.GameSetting.VibrationOn)
            return;

        HapticPatterns.PlayPreset(presetType);
    }

    public override void OnButtonUp(bool hasAnim)
    {

    }
}
