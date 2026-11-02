using UnityEngine;


/// <summary>
/// 用于状态切换
/// </summary>
public class StateTriggerOwner : MonoBehaviour
{
    public int CurrentState
    {
        get; private set;
    }

    void Start()
    {
        CurrentState = 0;
    }

    public void SetState(int state)
    {
        CurrentState = state;
    }
}
