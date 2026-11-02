using System.Collections;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level080 问号砖块奖励监听：砖块保持可反复顶击，仅在指定次数顶出一颗明信片星星。
/// </summary>
public sealed class Level80QuestionBlockReward : BaseItemEventHandle
{
    /// <summary>
    /// 用途：作为本砖块一次性顶出的明信片星星模板，实例化后仍走 Postcard_Star 原有收集与存档链路。
    /// 调用边界：仅在本关问号砖块第 rewardHitCount 次触发时读取。
    /// 注意事项：模板必须处于未激活状态，且应在预制体中配置正确的星星序号。
    /// </summary>
    [SerializeField] private GameObject starTemplate;

    /// <summary>
    /// 用途：定义本砖块顶出星星所需的触发次数。
    /// 调用边界：仅用于本关运行时计数，不写入存档。
    /// 注意事项：小于 1 时按 1 次处理，避免奖励永远无法出现。
    /// </summary>
    [SerializeField] private int rewardHitCount = 5;

    private int hitCount;
    private bool rewardSpawned;

    public override void Execute()
    {
        hitCount++;
        if (!rewardSpawned && hitCount >= Mathf.Max(1, rewardHitCount))
        {
            rewardSpawned = true;
            SpawnStar();
        }

        IsDone = true;
    }

    private void SpawnStar()
    {
        if (starTemplate == null)
        {
            Debug.LogWarning("[Level80QuestionBlockReward] Star template is missing.");
            return;
        }

        GameObject star = Instantiate(starTemplate, transform.parent);
        star.transform.position = transform.position;
        star.SetActive(true);
        MoveFloatHandle floatHandle = star.GetComponent<MoveFloatHandle>();
        if (floatHandle != null)
            floatHandle.enabled = false;

        StartCoroutine(PlayJumpOut(star.transform, floatHandle));
    }

    private IEnumerator PlayJumpOut(Transform star, MoveFloatHandle floatHandle)
    {
        const float duration = 0.45f;
        const float peakHeight = 2f;
        const float landingOffsetY = -3f;

        Vector3 start = star.position;
        Vector3 end = start + Vector3.up * landingOffsetY;
        float elapsed = 0f;

        while (elapsed < duration && star != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(start, end, progress);
            position.y += 4f * progress * (1f - progress) * peakHeight;
            star.position = position;
            yield return null;
        }

        if (star != null)
            star.position = end;

        if (floatHandle != null)
            floatHandle.enabled = true;
    }
}
