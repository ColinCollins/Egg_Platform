using System;
using System.Collections;
using System.Collections.Generic;
using Bear.Logger;
using Game.Scripts.Common;
using Game.Turntable;
using UnityEngine;

/// <summary>
/// Level 51 控制器
/// </summary>
public class Level51Ctrl : MonoBehaviour, IDebuger
{
    [Header("假门 - Button1")]
    // press 状态下假门沿 y 轴缓慢移动到指定位置。离开后，快速返回原位置
    [SerializeField] Level51PushButton button1;
    [SerializeField] private Transform fakeDoor;
    [SerializeField] private Vector3 fakeDoorTargetPos;
    [SerializeField] private float fakeDoorDownDuration = 1f;
    [SerializeField] private float fakeDoorUpDuration = 0.2f;

    [Header("碰撞体设置")]
    [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f);
    [SerializeField] private float colliderZPosition = 0f;
    [SerializeField] private Camera sceneCamera;

    [Header("自动退出设置")]
    [SerializeField] private float autoExitDelay = 2f;


    private Vector3 _fakeDoorOriginPos;
    private float _fakeDoorMoveStartY;
    private float _fakeDoorMoveTargetY;
    private float _fakeDoorMoveDuration;
    private float _fakeDoorMoveElapsed;
    private bool _isFakeDoorMoving;
    private Coroutine _autoExitCoroutine;
    private Collider2D _lastTriggerCollider;
    private bool _isAutoExiting;
    private MelenitasDev.SoundsGood.Sound _findItemsSound;

    private List<GameObject> worldColliders;
    private List<RectTransform> levelBlockRects;
    private Canvas canvas;
    private Camera uiCamera;
    private Camera worldCamera;
    private bool isInitialized = false;

    void OnEnable()
    {
        if (!isInitialized)
        {
           Invoke(nameof(InitializeColliders), 0.01f);
        }
    }

    void OnDisable()
    {
        CancelInvoke(nameof(InitializeColliders));
    }

    void Start()
    {
        if (fakeDoor != null)
            _fakeDoorOriginPos = fakeDoor.position;

        if (button1 != null)
        {
            button1.gameObject.tag = "Player";
            button1.SetEnter(() => MoveFakeDoor(true));
            button1.SetExit(() => MoveFakeDoor(false));

            button1.SetEnter(OnButtonEnter);
            button1.SetExit(OnButtonExit);
        }
    }

    void Update()
    {
        UpdateFakeDoorMovement();
        UpdateColliderPositions();
    }

    private void OnDestroy()
    {
        if (_autoExitCoroutine != null)
        {
            StopCoroutine(_autoExitCoroutine);
            _autoExitCoroutine = null;
        }

        _findItemsSound?.Stop();
        _findItemsSound = null;

        if (button1 != null)
        {
            button1.SetEnter((Action)null);
            button1.SetExit((Action)null);
            button1.SetEnter((Action<Collider2D>)null);
            button1.SetExit((Action<Collider2D>)null);
        }

        if (worldColliders != null)
        {
            foreach (GameObject obj in worldColliders)
            {
                if (obj != null)
                    Destroy(obj);
            }
            worldColliders.Clear();
        }

        Time.fixedDeltaTime = 1f / 60f;
    }

    private void MoveFakeDoor(bool down)
    {
        if (fakeDoor == null)
            return;

        float targetY = down ? fakeDoorTargetPos.y : _fakeDoorOriginPos.y;
        float duration = down ? fakeDoorDownDuration : fakeDoorUpDuration;

        _fakeDoorMoveStartY = fakeDoor.position.y;
        _fakeDoorMoveTargetY = targetY;
        if (Mathf.Abs(_fakeDoorMoveStartY - targetY) <= Mathf.Epsilon || duration <= 0f)
        {
            Vector3 position = fakeDoor.position;
            position.y = targetY;
            fakeDoor.position = position;
            _isFakeDoorMoving = false;
        }
        else
        {
            _fakeDoorMoveDuration = duration;
            _fakeDoorMoveElapsed = 0f;
            _isFakeDoorMoving = true;
        }

        if (down)
        {
            _findItemsSound ??= AudioManager.PlaySound("findItems", loop: true);
        }
        else
        {
            _findItemsSound?.Stop();
            _findItemsSound = null;
        }
    }

    private void UpdateFakeDoorMovement()
    {
        if (!_isFakeDoorMoving || fakeDoor == null)
            return;

        _fakeDoorMoveElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_fakeDoorMoveElapsed / _fakeDoorMoveDuration);
        float easedProgress = 1f - (1f - progress) * (1f - progress);

        Vector3 position = fakeDoor.position;
        float nextY = Mathf.LerpUnclamped(_fakeDoorMoveStartY, _fakeDoorMoveTargetY, easedProgress);
        position.y = nextY;
        fakeDoor.position = position;

        if (progress >= 1f)
        {
            position.y = _fakeDoorMoveTargetY;
            fakeDoor.position = position;
            _isFakeDoorMoving = false;
        }
    }

    private void OnButtonEnter(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            return;

        _lastTriggerCollider = other;

        if (_autoExitCoroutine != null)
            StopCoroutine(_autoExitCoroutine);
        _autoExitCoroutine = StartCoroutine(AutoExitAfterDelay());
    }

    private void OnButtonExit(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            return;

        if (_autoExitCoroutine != null)
        {
            StopCoroutine(_autoExitCoroutine);
            _autoExitCoroutine = null;
        }

        if (!_isAutoExiting)
            return;

        if (other != null && other.CompareTag("tools"))
        {
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.AddForce(new Vector2(.5f, 1.5f).normalized * 200f, ForceMode2D.Impulse);
            }
        }
    }

    private IEnumerator AutoExitAfterDelay()
    {
        yield return new WaitForSeconds(autoExitDelay);
        _autoExitCoroutine = null;
        _isAutoExiting = true;
        button1?.ManuallyExit(_lastTriggerCollider);
        _isAutoExiting = false;
        _lastTriggerCollider = null;
    }

    #region Collision
    private void InitializeColliders()
    {
        if (isInitialized)
            return;

        worldColliders = new List<GameObject>();
        levelBlockRects = new List<RectTransform>();

        if (sceneCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
                worldCamera = FindFirstObjectByType<Camera>();
        }
        else
        {
            worldCamera = sceneCamera;
        }

        canvas = PlayCtrl.Instance.CurrentGamePlayPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[Level51Ctrl] Canvas not found");
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            uiCamera = canvas.worldCamera;
        else if (canvas.renderMode == RenderMode.WorldSpace)
            uiCamera = canvas.worldCamera;
        else
            uiCamera = null;

        Transform panelRoot = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        FindLevelBlocks(panelRoot);

        if (levelBlockRects.Count == 0)
        {
            Debug.LogWarning("[Level51Ctrl] No levelBlock found");
            return;
        }

        for (int i = 0; i < levelBlockRects.Count; i++)
        {
            CreateColliderForLevelBlock(levelBlockRects[i], i);
        }

        isInitialized = true;

        Time.fixedDeltaTime = 1f / 120f;

        // 延迟一帧等 Canvas 布局完成后再对齐 button1
        Invoke(nameof(AlignButton1ToLevelBlock), 0.01f);
    }

    private void FindLevelBlocks(Transform parent)
    {
        if (parent.name == "levelBlock")
        {
            RectTransform rect = parent as RectTransform;
            if (rect != null)
                levelBlockRects.Add(rect);
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            FindLevelBlocks(parent.GetChild(i));
        }
    }

    private void CreateColliderForLevelBlock(RectTransform levelBlockRect, int index)
    {
        GameObject colliderObj = new GameObject($"LevelBlockCollider_{index}_{levelBlockRect.name}");
        colliderObj.transform.SetParent(transform);
        colliderObj.layer = LayerMask.NameToLayer("TriggerItem");

        BoxCollider2D boxCollider = colliderObj.AddComponent<BoxCollider2D>();
        boxCollider.size = colliderSize;
        colliderObj.tag = "Player";
        colliderObj.transform.position = new Vector3(0, 0, colliderZPosition);

        worldColliders.Add(colliderObj);
    }

    private void UpdateColliderPositions()
    {
        if (canvas == null || worldCamera == null)
            return;
        if (worldColliders == null || levelBlockRects == null || levelBlockRects.Count != worldColliders.Count)
            return;

        for (int i = 0; i < levelBlockRects.Count && i < worldColliders.Count; i++)
        {
            if (levelBlockRects[i] != null && worldColliders[i] != null)
            {
                Vector3 worldPosition = ConvertUIToWorldPosition(levelBlockRects[i]);
                worldColliders[i].transform.position = worldPosition;
            }
        }
    }

    private Vector3 ConvertUIToWorldPosition(RectTransform rect)
    {
        return ConvertUIToWorldPosition(rect, colliderZPosition);
    }

    private Vector3 ConvertUIToWorldPosition(RectTransform rect, float targetWorldZ)
    {
        if (canvas == null || worldCamera == null)
            return Vector3.zero;

        // WorldSpace Canvas: 直接用 rect 的世界坐标，仅替换 z
        if (canvas.renderMode == RenderMode.WorldSpace)
            return new Vector3(rect.position.x, rect.position.y, targetWorldZ);

        // 先获取屏幕坐标
        Vector2 screenPoint;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.position);
        else
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera != null ? uiCamera : worldCamera, rect.position);

        // 计算从相机到目标 z 平面的距离（带符号，沿相机朝向方向）
        float zDistance;
        float camZ = worldCamera.transform.position.z;
        float camForwardZ = worldCamera.transform.forward.z;

        if (Mathf.Abs(camForwardZ) < 0.001f)
        {
            zDistance = Mathf.Abs(camZ - targetWorldZ);
        }
        else
        {
            zDistance = (targetWorldZ - camZ) / camForwardZ;
            if (zDistance <= 0)
            {
                Debug.LogWarning($"[Level51Ctrl] zDistance <= 0 (camZ={camZ}, forwardZ={camForwardZ}, targetZ={targetWorldZ}), fallback to abs diff");
                zDistance = Mathf.Abs(camZ - targetWorldZ);
            }
        }

        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
        worldPos.z = targetWorldZ;
        return worldPos;
    }

    private void AlignButton1ToLevelBlock()
    {
        if (button1 == null || levelBlockRects == null || levelBlockRects.Count == 0)
            return;

        RectTransform targetBlock = levelBlockRects[0];
        if (targetBlock == null)
            return;

        Transform targetTransform = button1.transform.parent != null ? button1.transform.parent : button1.transform;
        float targetZ = targetTransform.position.z;
        Vector3 targetWorldPos = ConvertUIToWorldPosition(targetBlock, targetZ);

        Vector3 pos = targetTransform.position;
        pos.x = targetWorldPos.x;
        targetTransform.position = pos;
    }
    #endregion
}
