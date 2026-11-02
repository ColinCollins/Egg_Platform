using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class CameraStackSetup : MonoBehaviour
{
    private Camera sceneCamera; // 主场景相机
    private Camera uiCamera;    // UI 相机

    void Start()
    {
        sceneCamera = GetComponent<Camera>();
        
    }
}