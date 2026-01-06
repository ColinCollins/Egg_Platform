using System.Collections;
using System.Collections.Generic;
using Bear.UI;
using UnityEngine;

public class GameManager : MonoBehaviour
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
    }

    private void InitUIManager()
    {
        UIManager.Instance.Initialize();
        ResourcesUILoader newLoader = new ResourcesUILoader("UI/");
        UIManager.Instance.RegisterLoader(newLoader, 5);
    }
}
