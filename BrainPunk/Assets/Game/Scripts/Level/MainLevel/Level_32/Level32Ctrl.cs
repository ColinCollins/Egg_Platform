using System.Collections.Generic;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

public class Level32Ctrl : MonoBehaviour, IDebuger
{
    [SerializeField] private SpringJoint2D springJoint;
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;

    [SerializeField] private ParticleSystem knock;

    [Header("碰撞体设置")]
    [SerializeField] private Vector2 colliderSize = new Vector2(1f, 1f);
    [SerializeField] private float colliderZPosition = 0f;
    [SerializeField] private Camera sceneCamera;

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
            Invoke(nameof(InitializeColliders), 0.1f);
        }
    }

    void OnDisable()
    {
        CancelInvoke(nameof(InitializeColliders));
    }

    void Update()
    {
        UpdateColliderPositions();
    }

    public void StopSpringJump()
    {
        if (!levelCtrl.ActorCtrlable)
            WaitingToStopSpringJump().Forget();
    }

    private async UniTaskVoid WaitingToStopSpringJump()
    {
        knock.transform.position = actor.transform.position;
        knock.Play();
        AudioManager.PlaySound("trap");

        await UniTask.WaitForSeconds(0.5f);
        levelCtrl.SetCtrlable(true);
        actor.StopKnock();
        actor.transform.DOKill();
        actor.transform.DORotate(Vector3.zero, 0.2f);
        actor.GetComponent<Rigidbody2D>().gravityScale = 4.5f;
        Debug.Log("---------- Stop Spring Jump");
    }

    public void TriggerSpring(Collider2D collider)
    {
        // this.Log("-------------- ???");
        PlaySpring();
    }

    private async void PlaySpring()
    {
        AudioManager.PlaySound("boardTrigger");
        var startY = springJoint.transform.localPosition.y;
        springJoint.transform.DOLocalMoveY(startY - 0.2f, 0.1f);

        await UniTask.WaitForSeconds(.2f);

        levelCtrl.SetCtrlable(false);
        springJoint.enabled = true;
        AudioManager.PlaySound("spring");
        actor.Knockback(new Vector2(0.08f, 0.6f), 70f);
        actor.GetComponent<Rigidbody2D>().gravityScale = 1.5f;

        await UniTask.WaitForSeconds(.6f);

        if (!levelCtrl.ActorCtrlable)
        {
            actor.transform.localEulerAngles = Vector3.zero;
            actor.transform.DORotate(new Vector3(0, 0, 360f), 3f, RotateMode.FastBeyond360);
        }
    }

    #region Collision
    private void InitializeColliders()
    {
        if (isInitialized)
            return;

        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
        {
            Debug.LogWarning("[Level32Ctrl] PlayCtrl or CurrentGamePlayPanel is null, will retry later");
            Invoke(nameof(InitializeColliders), 0.1f);
            return;
        }

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
            Debug.LogWarning("[Level32Ctrl] Canvas not found");
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
            Debug.LogWarning("[Level32Ctrl] No levelBlock found");
            return;
        }

        for (int i = 0; i < levelBlockRects.Count; i++)
        {
            CreateColliderForLevelBlock(levelBlockRects[i], i);
        }

        isInitialized = true;

        Time.fixedDeltaTime = 1f / 120f;
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
        if (canvas == null || worldCamera == null)
            return Vector3.zero;

        Vector2 screenPoint;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            screenPoint = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        else if (canvas.renderMode == RenderMode.ScreenSpaceCamera && uiCamera != null)
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.position);
        else if (canvas.renderMode == RenderMode.WorldSpace)
            return new Vector3(rect.position.x, rect.position.y, colliderZPosition);
        else
            screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera != null ? uiCamera : worldCamera, rect.position);

        float zDistance = Mathf.Abs(worldCamera.transform.position.z - colliderZPosition);
        Vector3 worldPos = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, zDistance));
        worldPos.z = colliderZPosition;
        return worldPos;
    }
    #endregion
    void OnDestroy()
    {
        if (worldColliders != null)
        {
            foreach (GameObject obj in worldColliders)
            {
                if (obj != null)
                    Destroy(obj);
            }
            worldColliders.Clear();
        }

        Time.fixedDeltaTime = 1f / 50f;
    }
}
