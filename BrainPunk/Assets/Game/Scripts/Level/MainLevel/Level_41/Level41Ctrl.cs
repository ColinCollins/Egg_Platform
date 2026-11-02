using UnityEngine;

public class Level41Ctrl : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Time.fixedDeltaTime = 1f / 120f;
    }

    // Update is called once per frame
    void OnDestroy()
    {
        Time.fixedDeltaTime = 1f / 60f;
    }
}
