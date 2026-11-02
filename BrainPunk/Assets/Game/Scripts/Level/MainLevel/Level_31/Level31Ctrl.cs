using DG.Tweening;
using Game.Common;
using TMPro;
using UnityEngine;

public class Level31Ctrl : MonoBehaviour
{
    [SerializeField] private GameObject guard;
    [SerializeField] private Collider2D door;
    [SerializeField] private TextMeshProUGUI score;

    [SerializeField] private int defaultScore;

    // [SerializeField] private Vector2Int RandomScore;

    [Header("Score Fly Text")]
    [SerializeField] private ScoreFlyText scoreFlyTextPrefab;
    [SerializeField] private Transform flyTextParent; // Canvas 或 UI 根节点
    [SerializeField] private float flyDistance = 100f;
    [SerializeField] private float flyDuration = 1f;

    private int lastScore;
    private int currentScore;
    private bool poolRegistered = false;

    void Start()
    {
        currentScore = defaultScore;
        lastScore = defaultScore;

        score.gameObject.SetActive(true);
        score.text = defaultScore.ToString();

        // RegisterScoreFlyTextPool();
    }

    /// <summary>
    /// 注册分数飞字对象池
    /// </summary>
/*     private void RegisterScoreFlyTextPool()
    {
        if (scoreFlyTextPrefab == null)
        {
            Debug.LogWarning("[Level31Ctrl] ScoreFlyText prefab is not assigned!");
            return;
        }

        if (ObjectPoolManager.Instance != null && !poolRegistered)
        {
            ObjectPoolManager.Instance.RegisterPool(
                () => Instantiate(scoreFlyTextPrefab),
                initialSize: 3,
                maxSize: 10
            );
            poolRegistered = true;
        }

        // 如果没有指定父节点，尝试从 score 的父节点获取 Canvas
        if (flyTextParent == null && score != null)
        {
            Canvas canvas = score.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                flyTextParent = canvas.transform;
            }
            else
            {
                flyTextParent = transform; // 回退到当前 transform
            }
        }
    }
 */
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void CutdownScore(int count)
    {
        int scoreReduction = count;//Random.Range(RandomScore.x, RandomScore.y);
        currentScore -= scoreReduction;
        UpdateScore(scoreReduction);
    }

    public void CutdownAllScore()
    {
        int scoreReduction = defaultScore; // 记录减少的分数（当前剩余的所有分数）
        currentScore = 0;
        UpdateScore(scoreReduction);
    }

    private void UpdateScore(int scoreReduction = 0)
    {
        score.DOCounter(lastScore, currentScore, 0.5f).OnComplete(OnScoreUpdateComplete);

        // 创建飞字效果
        if (scoreReduction > 0)
        {
            CreateScoreFlyText(scoreReduction);
        }

        lastScore = currentScore;
    }

    private void OnScoreUpdateComplete()
    {
        if (currentScore <= 0)
        {
            currentScore = 0;
            door.enabled = true;
            score.transform.parent.gameObject.SetActive(false);

            guard.transform.Rotate(0, 180f, 0);
            guard.transform.DOMoveX(20, 4.5f).OnComplete(() =>
            {
                Destroy(guard.gameObject);
            });
            
            var spineCtrl = guard.GetComponentInChildren<ActorSpineCtrl>();
            spineCtrl.PlayAnimation("walk", true);
        }
    }

    /// <summary>
    /// 创建分数飞字效果
    /// </summary>
    private void CreateScoreFlyText(int scoreValue)
    {
        if (scoreFlyTextPrefab == null || ObjectPoolManager.Instance == null)
        {
            return;
        }

        ScoreFlyText flyText = ObjectPoolManager.Instance.Get<ScoreFlyText>();
        if (flyText == null)
        {
            Debug.LogWarning("[Level31Ctrl] Failed to get ScoreFlyText from pool!");
            return;
        }

        // 使用 score UI 的位置作为起始位置
        Vector3 startPosition = score.transform.position;

        flyText.Setup(scoreValue, startPosition, flyTextParent, flyDistance, flyDuration);
    }

    void OnDestroy()
    {
        if (guard)
        {
            guard.transform.DOKill();
        }

        if (score)
        {
            score.DOKill();
        }

        // 清理对象池（可选，如果需要在关卡结束时清理）
        if (poolRegistered && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ClearPool<ScoreFlyText>();
        }
    }
}
