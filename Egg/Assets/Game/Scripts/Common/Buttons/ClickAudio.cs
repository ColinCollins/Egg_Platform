using System.Collections;
using System.Collections.Generic;
using MelenitasDev.SoundsGood;
using UnityEngine;

public class ClickAudio : ButtonClickTrigger
{
    public string AudioName;
    private Sound sound;
    
    public override void OnButtonDown(bool hasAnim)
    {
        
    }

    public override void OnButtonUp(bool hasAnim)
    {
        if (sound == null)
            sound = new Sound(AudioName);
        
        if (sound.Playing)
            return;
        
        sound.Play();
    }
}
