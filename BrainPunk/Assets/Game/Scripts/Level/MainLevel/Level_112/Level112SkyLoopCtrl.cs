using UnityEngine;

/// <summary>
/// Level112 无限升空天空对象池：只把落到镜头下方的既有云与星星循环回收到镜头上方。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level112SkyLoopCtrl : MonoBehaviour
{
    [SerializeField] private Transform[] skyElements;
    [SerializeField] private Transform oneShotMoon;
    [SerializeField, Min(1f)] private float loopHeight = 48f;
    [SerializeField, Min(0f)] private float recycleBelowViewport = 3f;

    private Camera targetCamera;
    private Vector3[] initialLocalPositions;

    private void Awake()
    {
        targetCamera = Camera.main;
        CacheInitialPositions();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null || skyElements == null)
            return;

        float cameraHalfHeight = targetCamera.orthographic
            ? targetCamera.orthographicSize
            : 5f;
        float recycleY = targetCamera.transform.position.y - cameraHalfHeight - recycleBelowViewport;
        float step = Mathf.Max(1f, loopHeight);

        foreach (Transform element in skyElements)
        {
            if (element == null || IsOneShotMoon(element) || element.position.y >= recycleY)
                continue;

            Vector3 position = element.position;
            int stepCount = Mathf.FloorToInt((recycleY - position.y) / step) + 1;
            position.y += stepCount * step;
            element.position = position;
        }
    }

    private bool IsOneShotMoon(Transform element)
    {
        // 名称兜底保证旧 Prefab 或热重载未写入 oneShotMoon 引用时，月亮仍不会被循环回收。
        return element == oneShotMoon || element.name == "SkyMoon";
    }

    private void OnDisable()
    {
        if (skyElements == null || initialLocalPositions == null)
            return;

        int count = Mathf.Min(skyElements.Length, initialLocalPositions.Length);
        for (int index = 0; index < count; index++)
        {
            if (skyElements[index] != null)
                skyElements[index].localPosition = initialLocalPositions[index];
        }
    }

    private void CacheInitialPositions()
    {
        if (skyElements == null)
            return;

        initialLocalPositions = new Vector3[skyElements.Length];
        for (int index = 0; index < skyElements.Length; index++)
        {
            if (skyElements[index] != null)
                initialLocalPositions[index] = skyElements[index].localPosition;
        }
    }
}
