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
        InitUIManager();
        // Test
        DBManager.Instance.Initialize();

        PlayCtrl.Instance.Init();
        // this.Log(DB.GameData.CurrentLevel.ToString());
    }

    private void InitUIManager()
    {
        UIManager.Instance.Initialize();
        ResourcesUILoader newLoader = new ResourcesUILoader("UI/");
        UIManager.Instance.RegisterLoader(newLoader, 5);
    }
}
