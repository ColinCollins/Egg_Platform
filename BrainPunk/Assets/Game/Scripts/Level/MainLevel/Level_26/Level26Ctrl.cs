using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level.Actor;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;

public class Level26Ctrl : MonoBehaviour
{
    private const string DoorIdleAnimName = "door_idle";
    private const string DefaultToolSkinName = "box_original";

    [SerializeField] private ActorSpineCtrl startDoor;

    // [SerializeField] private MMRotationShaker startDoorShaker;
    [SerializeField] private ActorCtrl actor;

    [SerializeField] private GameObject enemy;

    [SerializeField] private ParticleSystem knock;

    [SerializeField] private float doorShakeTimerInterval = 5f;

    // [SerializeField] private MMF_Player doorShake;

    protected List<ActorCtrl> tools;
    private ActorCtrl tool;
    private bool canOpen = false;

    void Awake()
    {
        canOpen = true;

        tools = new List<ActorCtrl>();
    }

    void Start()
    {
        RunDoorShakeTimerAsync().Forget();
    }

    private async UniTaskVoid RunDoorShakeTimerAsync()
    {
        /* var cts = this.GetCancellationTokenOnDestroy();
        while (!cts.IsCancellationRequested)
        {
            await UniTask.WaitForSeconds(doorShakeTimerInterval, cancellationToken: cts);

            if (startDoor == null || startDoorShaker == null)
                return;

            if (!startDoor.IsPlayingAnimation(DoorIdleAnimName))
                continue;

            startDoorShaker.StartShaking();
        } */
    }

    public void ClickStartDoor()
    {
        if (!canOpen)
            return;

        canOpen = false;
        PlayOpenDoorAnim();

        var tool = Instantiate(actor, actor.transform.parent);
        tool.SwitchDash(true);
        tool.SetMoveInput(1);
        var pos = tool.transform.position;
        pos.x = startDoor.transform.position.x;
        tool.transform.position = pos;
        tool.transform.localScale = Vector3.one * 0.7f;
        tool.transform.localEulerAngles = Vector3.zero;
        tool.gameObject.tag = "tools";
        tool.gameObject.layer = LayerMask.NameToLayer("TriggerItem");

        RemoveGeneratedToolSkins(tool);

        tools.Add(tool);
    }

    void Update()
    {
        if (tools != null && tools.Count > 0)
        {
            for (int i = 0; i < tools.Count; i++)
            {
                tool = tools[i];
                if (tool == null)
                {
                    tools.Remove(tool);
                    i--;
                    continue;
                }
                tool.OnUpdate();
            }
        }
    }

    private async void PlayOpenDoorAnim()
    {
        AudioManager.PlaySound("openDoor");
        startDoor.PlayAnimation("door_open", false);

        await UniTask.WaitForSeconds(1f);

        startDoor.PlayAnimation("dose_close", false);

        await UniTask.WaitForSeconds(0.15f);
        AudioManager.PlaySound("closeDoor");

        await UniTask.WaitForSeconds(.15f);

        startDoor.PlayAnimation("door_idle", true);

        canOpen = true;
    }

    #region 生成 Tool 皮肤清理

    private static void RemoveGeneratedToolSkins(ActorCtrl tool)
    {
        if (tool == null)
            return;

        DisableGeneratedToolSkinBinding(tool);
        ClearGeneratedToolSkinPartMounts(tool.transform);
        ResetGeneratedToolFullBodySkin(tool);
    }

    private static void DisableGeneratedToolSkinBinding(ActorCtrl tool)
    {
        var bindings = tool.GetComponentsInChildren<ActorSkinBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i] != null)
                bindings[i].enabled = false;
        }
    }

    private static void ResetGeneratedToolFullBodySkin(ActorCtrl tool)
    {
        if (tool.SpineCtrl != null)
            tool.SpineCtrl.SetSkin(DefaultToolSkinName);
    }

    private static void ClearGeneratedToolSkinPartMounts(Transform root)
    {
        ClearSkinPartMountChildren(root, "back");
        ClearSkinPartMountChildren(root, "face");
        ClearSkinPartMountChildren(root, "body");
    }

    private static void ClearSkinPartMountChildren(Transform root, string mountName)
    {
        var mount = FindSkinPartMount(root, mountName);
        if (mount == null)
            return;

        for (int i = mount.childCount - 1; i >= 0; i--)
        {
            Destroy(mount.GetChild(i).gameObject);
        }
    }

    private static Transform FindSkinPartMount(Transform root, string mountName)
    {
        if (root == null || string.IsNullOrEmpty(mountName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == mountName &&
                (child.GetComponent<BoneFollowerAutoBinder>() != null || child.GetComponent<BoneFollower>() != null))
            {
                return child;
            }

            Transform result = FindSkinPartMount(child, mountName);
            if (result != null)
                return result;
        }

        return null;
    }

    #endregion

    public void TryToDestroyEnemy(Transform target)
    {
        DestroyEnemy(target);
    }

    public async Task DestroyEnemy(Transform target)
    {
        knock.transform.position = target.transform.position;
        knock.Play();

        AudioManager.PlaySound("trap");

        enemy.GetComponent<Level26EnemyCtrl>().HideLifeBar();
        enemy.GetComponent<Collider2D>().enabled = false;
        enemy.transform.DOLocalMove(new Vector3(9, 10f, 0), 1f);
        enemy.transform.DORotate(new Vector3(0, 0, 360f), 0.5f, RotateMode.FastBeyond360).SetLoops(-1);

        await UniTask.WaitForSeconds(1.2f);

        Destroy(enemy);
    }

    void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.transform.DOKill();
        }
    }
}
