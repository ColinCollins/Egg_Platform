using System.Collections;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Game.Common;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>, IDebuger
{
    // Start is called before the first frame update
    private void Awake()
    {
        Application.targetFrameRate = 60;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        PlayCtrl.Instance.Init();
    }
}
