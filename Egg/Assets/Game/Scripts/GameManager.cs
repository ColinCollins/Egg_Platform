using System.Collections;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using UnityEngine;

public class GameManager : MonoBehaviour, IDebuger
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

        StartPanel.Create();
        // DB.GameData.CurrentLevel = 2;
        // DB.GameData.Save();
        this.Log(DB.GameData.CurrentLevel.ToString());
    }

    private void InitUIManager()
    {
        UIManager.Instance.Initialize();
        ResourcesUILoader newLoader = new ResourcesUILoader("UI/");
        UIManager.Instance.RegisterLoader(newLoader, 5);
    }
}
