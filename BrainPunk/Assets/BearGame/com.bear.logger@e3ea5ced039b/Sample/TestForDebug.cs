using System.Collections;
using System.Collections.Generic;
using Bear.Logger;
using UnityEngine;

public class TestForDebug : MonoBehaviour, IDebuger
{
    // Start is called before the first frame update
    void Start()
    {
        this.Log("This is a log debug");
        
        this.LogWarning("This is a warning debug");
        
        this.LogError("This is a error debug");
        
        this.LogColor("This is a color debug", BearLogger.Color.green);
        
        BearLogger.Log("This is a log debug", this);
        
        BearLogger.LogWarning("This is a warning debug", this);
        
        BearLogger.LogError("This is a error debug", this);
        
        BearLogger.LogColor("This is a color debug", BearLogger.Color.green, this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
