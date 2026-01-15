using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class CameraStackSetup : MonoBehaviour
{
    private Camera sceneCamera; // 主场景相机
    private Camera uiCamera;    // UI 相机

    void Start()
    {
        sceneCamera= GetComponent<Camera>();
        uiCamera = Camera.main;
        if (sceneCamera != null && uiCamera != null)
        {
            // 获取或添加附加组件
            var universalData = sceneCamera.GetUniversalAdditionalCameraData();
            // 确保它不在堆栈中，然后添加
            if (!universalData.cameraStack.Contains(uiCamera))
            {
                universalData.cameraStack.Add(uiCamera);
            }
        }
    }
}