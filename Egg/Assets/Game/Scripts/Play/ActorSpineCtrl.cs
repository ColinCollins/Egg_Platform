using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

public class ActorSpineCtrl : MonoBehaviour
{
    public enum AnimState
    {
        Die,
        Jump,
        Run,
        Win,
        Walk1,
        Walk2
    }

    [FoldoutGroup("Refs")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;

    [FoldoutGroup("Config")]
    [SerializeField] private AnimState defaultState = AnimState.Walk1;

    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string dieAnim = "die";
    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string jumpAnim = "jump";
    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string runAnim = "run";
    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string winAnim = "win";
    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string walk1Anim = "walk_1";
    [FoldoutGroup("Config/Anim Names")]
    [SerializeField] private string walk2Anim = "walk_2";

    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool dieLoop = false;
    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool jumpLoop = false;
    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool runLoop = true;
    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool winLoop = false;
    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool walk1Loop = true;
    [FoldoutGroup("Config/Loop")]
    [SerializeField] private bool walk2Loop = true;

    [FoldoutGroup("Test")]
    [SerializeField] private AnimState testState = AnimState.Walk1;

    private AnimState currentState;

    void Awake()
    {
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }

        SetState(defaultState, true);
    }

    public AnimState CurrentState => currentState;

    [FoldoutGroup("Test")]
    [Button("Test Switch")]
    private void TestSwitch()
    {
        SetState(testState, true);
    }

    public void SetState(AnimState state, bool force = false)
    {
        if (!force && currentState == state)
            return;

        if (skeletonAnimation == null || skeletonAnimation.AnimationState == null)
            return;

        string animName = GetAnimName(state);
        if (string.IsNullOrEmpty(animName))
            return;

        bool loop = GetLoop(state);
        skeletonAnimation.AnimationState.SetAnimation(0, animName, loop);
        currentState = state;
    }

    private string GetAnimName(AnimState state)
    {
        switch (state)
        {
            case AnimState.Die:
                return dieAnim;
            case AnimState.Jump:
                return jumpAnim;
            case AnimState.Run:
                return runAnim;
            case AnimState.Win:
                return winAnim;
            case AnimState.Walk1:
                return walk1Anim;
            case AnimState.Walk2:
                return walk2Anim;
            default:
                return string.Empty;
        }
    }

    private bool GetLoop(AnimState state)
    {
        switch (state)
        {
            case AnimState.Die:
                return dieLoop;
            case AnimState.Jump:
                return jumpLoop;
            case AnimState.Run:
                return runLoop;
            case AnimState.Win:
                return winLoop;
            case AnimState.Walk1:
                return walk1Loop;
            case AnimState.Walk2:
                return walk2Loop;
            default:
                return true;
        }
    }
}
