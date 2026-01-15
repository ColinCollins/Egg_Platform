using System.Collections.Generic;
using Bear.Logger;
using UnityEngine;

public class CutManager : MonoBehaviour, IDebuger
{
    [SerializeField] private List<Level5Anchor> anchors = new List<Level5Anchor>();
    [SerializeField] private Transform dragObject;
    [SerializeField] private Transform doorObject;
    [SerializeField] private float radius = 0.5f;
    
    private Camera mainCamera;
    private bool isDragging = false;
    private Vector2 lastDragPosition;

    void Awake()
    {
        // mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>();
        }
    }

    void Update()
    {
        HandleDragInput();
        
        if (isDragging && dragObject != null)
        {
            CheckLineIntersection();
        }
    }

    private void HandleDragInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        // 移动端触摸检测
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchPosition = touch.position;

            if (touch.phase == TouchPhase.Began)
            {
                isDragging = true;
                UpdateDragPosition(touchPosition);
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                UpdateDragPosition(touchPosition);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
            }
        }
#else
        // PC端鼠标检测
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            UpdateDragPosition(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDragPosition(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
#endif
    }

    private void UpdateDragPosition(Vector2 screenPosition)
    {
        if (mainCamera == null || dragObject == null)
            return;

        // 对于正交相机，需要考虑 orthographicSize 的变化
        if (mainCamera.orthographic)
        {
            // 正交相机：直接使用 ScreenToWorldPoint，深度使用相机的世界空间 z 位置
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - dragObject.position.z);
            Vector3 screenPosWithZ = new Vector3(screenPosition.x, screenPosition.y, zDistance);
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosWithZ);
            worldPosition.z = dragObject.position.z;
            dragObject.position = worldPosition;
        }
        else
        {
            // 透视相机：使用标准的 ScreenToWorldPoint
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - dragObject.position.z);
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
            worldPosition.z = dragObject.position.z;
            dragObject.position = worldPosition;
        }
    }

    private void CheckLineIntersection()
    {
        if (dragObject == null)
            return;

        Vector2 dragPos = dragObject.position;

        foreach (var anchor in anchors)
        {
            if (anchor == null)
                continue;
                
            if (anchor.IsCut)
                continue;

            Level5TrackLine trackLine = anchor.TrackLine;
            if (trackLine == null)
            {
                Debug.LogWarning($"CutManager: Anchor {anchor.name} has no TrackLine");
                continue;
            }

            Transform startPoint = trackLine.GetStartPoint();
            Transform endPoint = trackLine.GetEndPoint();

            if (startPoint == null || endPoint == null)
            {
                Debug.LogWarning($"CutManager: TrackLine {trackLine.name} has null StartPoint or EndPoint");
                continue;
            }

            Vector2 lineStart = startPoint.position;
            Vector2 lineEnd = endPoint.position;

            // 计算点到线段的距离
            float distance = PointToLineSegmentDistance(dragPos, lineStart, lineEnd);
            this.Log($"Distance: {distance}");
            if (distance <= radius)
            {
                Debug.Log($"CutManager: Line intersection detected! Distance: {distance}, Radius: {radius}");
                CutLine(anchor, dragPos);
                break;
            }
        }
    }

    private float PointToLineSegmentDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float lineLength = line.magnitude;
        
        if (lineLength < 0.001f)
            return Vector2.Distance(point, lineStart);

        Vector2 lineNormalized = line / lineLength;
        Vector2 pointToStart = point - lineStart;
        
        float projection = Vector2.Dot(pointToStart, lineNormalized);
        projection = Mathf.Clamp(projection, 0f, lineLength);
        
        Vector2 closestPoint = lineStart + lineNormalized * projection;
        return Vector2.Distance(point, closestPoint);
    }

    private void CutLine(Level5Anchor anchor, Vector2 cutPosition)
    {
        if (anchor == null || anchor.IsCut)
            return;

        anchor.SetCut(true);

        // 移除 springJoint（如果存在）
        SpringJoint2D springJoint = anchor.SpringJoint;
        if (springJoint != null)
        {
            Destroy(springJoint);
        }

        // 获取或添加门和锚点的 Rigidbody2D
        Rigidbody2D doorRb = doorObject != null ? doorObject.GetComponent<Rigidbody2D>() : null;
        if (doorRb == null && doorObject != null)
        {
            doorRb = doorObject.gameObject.AddComponent<Rigidbody2D>();
            doorRb.bodyType = RigidbodyType2D.Dynamic;
            doorRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            doorRb.mass = 0.01f; // 设置为最小质量
            doorRb.linearDamping = .8f; // 线性衰减设置为10
        }
        else if (doorRb != null)
        {
            doorRb.bodyType = RigidbodyType2D.Dynamic;
            doorRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        Rigidbody2D anchorRb = anchor.GetComponent<Rigidbody2D>();
        if (anchorRb == null)
        {
            anchorRb = anchor.gameObject.AddComponent<Rigidbody2D>();
            // Anchor 的 Rigidbody2D 状态保持原样，不修改
        }

        if (doorRb == null || anchorRb == null)
        {
            Debug.LogWarning("CutManager: Failed to get or create Rigidbody2D components");
            return;
        }

        // 创建两个新的 Rigidbody2D 空物体
        GameObject rb1Obj = new GameObject("CutPoint1");
        GameObject rb2Obj = new GameObject("CutPoint2");
        
        rb1Obj.transform.position = cutPosition;
        rb2Obj.transform.position = cutPosition;

        Rigidbody2D rb1 = rb1Obj.AddComponent<Rigidbody2D>();
        Rigidbody2D rb2 = rb2Obj.AddComponent<Rigidbody2D>();
        rb1.bodyType = RigidbodyType2D.Dynamic;
        rb1.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb1.mass = 0.01f; // 设置为最小质量
        rb1.linearDamping = .8f; // 线性衰减设置为10
        rb2.bodyType = RigidbodyType2D.Dynamic;
        rb2.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb2.mass = 0.01f; // 设置为最小质量
        rb2.linearDamping = .8f; // 线性衰减设置为10

        // 给门和锚点分别绑定 DistanceJoint2D
        DistanceJoint2D doorJoint = doorObject.gameObject.AddComponent<DistanceJoint2D>();
        DistanceJoint2D anchorJoint = anchor.gameObject.AddComponent<DistanceJoint2D>();

        doorJoint.connectedBody = rb1;
        doorJoint.autoConfigureDistance = false;
        doorJoint.distance = Vector2.Distance((Vector2)doorRb.transform.position, cutPosition);
        doorJoint.enableCollision = false;

        anchorJoint.connectedBody = rb2;
        anchorJoint.autoConfigureDistance = false;
        anchorJoint.distance = Vector2.Distance((Vector2)anchorRb.transform.position, cutPosition);
        anchorJoint.enableCollision = false;

        // 获取原始 line 的 StartPoint 和 EndPoint
        Level5TrackLine originalLine = anchor.TrackLine;
        Transform originalStart = originalLine.GetStartPoint();
        Transform originalEnd = originalLine.GetEndPoint();

        // Copy 一个 line，EndPosition 为这两个新建物体
        GameObject newLineObj = new GameObject("CutLine");
        newLineObj.transform.SetParent(anchor.transform.parent);
        
        LineRenderer newLineRenderer = newLineObj.AddComponent<LineRenderer>();
        Level5TrackLine newLine = newLineObj.AddComponent<Level5TrackLine>();
        
        // 复制原始 LineRenderer 的设置
        LineRenderer originalRenderer = originalLine.GetComponent<LineRenderer>();
        if (originalRenderer != null)
        {
            newLineRenderer.material = originalRenderer.material;
            newLineRenderer.startWidth = originalRenderer.startWidth;
            newLineRenderer.endWidth = originalRenderer.endWidth;
            newLineRenderer.startColor = originalRenderer.startColor;
            newLineRenderer.endColor = originalRenderer.endColor;
            newLineRenderer.sortingLayerID = originalRenderer.sortingLayerID;
            newLineRenderer.sortingOrder = originalRenderer.sortingOrder;
        }

        // 设置新 line 的 StartPoint 为门，EndPoint 为新建物体 rb1
        newLine.SetStartPoint(doorObject);
        newLine.SetEndPoint(rb1Obj.transform);

        // 修改原始 line 的 EndPoint 为另一个新建物体 rb2
        originalLine.SetEndPoint(rb2Obj.transform);
        
        Debug.Log($"CutManager: Line cut successfully at position {cutPosition}");
    }
}
