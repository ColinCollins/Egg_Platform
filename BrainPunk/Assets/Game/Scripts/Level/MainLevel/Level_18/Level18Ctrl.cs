using Unity.VisualScripting;
using UnityEngine;

public class Level18Ctrl : MonoBehaviour
{
    [SerializeField] private Transform[] Keys;
    [SerializeField] private StateTriggerDisplayHandle[] Chests;
    [SerializeField] private PickupAnimListener pickup;

    [SerializeField] private StateTriggerOwner actor;
    [SerializeField] private GameObject shiny;

    private int currentIndex = 0;
    private Transform currentKey;
    private StateTriggerDisplayHandle currentChest;
    private FollowTarget follower;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentKey = Keys[currentIndex];
        currentChest = Chests[currentIndex];
        pickup.OnReachEndTarget.AddListener(OnFlyComplete);
    }

    public void NextKey()
    {
        currentIndex++;
        if (currentIndex >= Keys.Length)
        {
            return;
        }

        currentKey = Keys[currentIndex];
        currentChest = Chests[currentIndex];

        pickup.transform.position = currentKey.position;
/*         follower = currentKey.GetComponent<FollowTarget>();
        follower.SetTarget(pickup.transform); */
    }

    /// <summary>
    /// 开始飞行
    /// </summary>
    public void PlayFly()
    {
        pickup.Execute();
    }

    /// <summary>
    /// 飞行结束
    /// </summary>

    public void OnFlyComplete()
    {
        if (!follower)
            follower.SetTarget(actor.transform);
    }


    #region Chest
    /// <summary>
    /// 开宝箱时播放
    /// </summary>
    public void PlayShiny()
    {
        var obj = Instantiate(shiny, transform);
        obj.SetActive(true);
        obj.transform.position = currentChest.transform.position + new Vector3(0, 0.5f, 0);
    }

    #endregion

}
