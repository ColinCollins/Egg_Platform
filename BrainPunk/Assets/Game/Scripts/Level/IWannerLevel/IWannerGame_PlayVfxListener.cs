using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 粒子特效播放监听器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，在指定父节点下创建粒子并移动到目标位置。
/// </summary>
public sealed class IWannerGame_PlayVfxListener : BaseItemEventHandle, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定粒子实例的父节点。
    /// 调用边界：仅在 Execute 创建当前粒子实例时使用。
    /// 注意事项：必须配置；关卡清理 root 时会一并清理仍存活的粒子实例。
    /// </summary>
    [Tooltip("粒子实例的父节点")]
    [SerializeField] private Transform root;

    /// <summary>
    /// 用途：指定粒子实例生成后的世界坐标。
    /// 调用边界：每次 Execute 时读取一次 target.position。
    /// 注意事项：只复制位置，不复制 target 的旋转和缩放。
    /// </summary>
    [Tooltip("粒子实例的目标世界位置")]
    [SerializeField] private Transform target;

    /// <summary>
    /// 用途：配置需要实例化并激活的粒子预制体。
    /// 调用边界：每次 Execute 创建一个新实例；粒子生命周期由预制体或 root 所属关卡负责。
    /// 注意事项：是否显式播放由 autoPlay 控制。
    /// </summary>
    [Tooltip("需要实例化的粒子预制体")]
    [SerializeField] private GameObject particle;

    /// <summary>
    /// 用途：控制实例激活后是否显式播放全部子级 ParticleSystem。
    /// 调用边界：仅控制当前组件本次创建的粒子实例。
    /// 注意事项：默认 false，与共享 PlayVfxListener 保持一致；关闭时由粒子预制体自身的 Play On Awake 决定播放。
    /// </summary>
    [Tooltip("激活实例后，是否显式播放全部子级 ParticleSystem")]
    [SerializeField] private bool autoPlay = false;

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：以 root 为父节点创建粒子，并把实例放到 target 的世界位置。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的关卡本地逻辑调用。
    /// 注意事项：root、target 或 particle 缺失时会记录警告并立即完成，避免顺序执行器卡住。
    /// </summary>
    public override void Execute()
    {
        IsRunning = true;
        IsDone = false;

        if (root == null || target == null || particle == null)
        {
            this.LogWarning("[IWannerGame_PlayVfxListener] Root, target, or particle is missing.");
            CompleteExecution();
            return;
        }

        GameObject particleInstance = Instantiate(particle, root);
        particleInstance.transform.position = target.position;
        particleInstance.SetActive(true);

        if (autoPlay)
        {
            ParticleSystem[] particleSystems =
                particleInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
                particleSystems[i].Play(false);
        }

        _ = WaitForDone();
    }

    #endregion

    #region Internal

    private async Task WaitForDone()
    {
        await UniTask.WaitForSeconds(0.1f);
        CompleteExecution();
    }

    private void CompleteExecution()
    {
        IsRunning = false;
        IsDone = true;
    }

    #endregion
}
