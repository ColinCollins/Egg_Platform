
using MelenitasDev.SoundsGood;
public class ClickAudio : ButtonClickTrigger
{
    public string AudioName = "button";
    private Sound sound;

    public override void OnButtonDown(bool hasAnim)
    {

    }

    public override void OnButtonUp(bool hasAnim)
    {
        if (!DB.GameSetting.SfxOn)
            return;

        if (sound == null)
            sound = new Sound(AudioName);

        if (sound.Playing)
            return;

        sound.Play();
    }
}
