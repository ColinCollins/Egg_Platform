using System.Collections.Generic;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Level.Actor;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Controls AI spawning, transport dispatch, and finished-level exit cleanup.
///
/// version 2.1.0
/// Level64 生成的 AI helper 会先清理从玩家 Actor 克隆过来的 Skin 部件，再保留 v2.0.0 的生成顺序皮肤。
/// </summary>
public class Level64AICtrl : MonoBehaviour, IDebuger
{
    private const string DoorIdleAnimName = "door_idle";
    private const string FirstHelperSkinName = "box_rich";
    private const string SecondHelperSkinName = "box_bad";
    private const string CarriedBlockCloneName = "BlockItem(Clone)";

    [SerializeField] private Level64Ctrl levelCtrl;
    [SerializeField] private ActorSpineCtrl startDoor;
    [SerializeField] private Level64ActorCtrl actor;
    [SerializeField] private int maxAICount = 2;
    [SerializeField] private Transform door;
    [SerializeField] private float exitDoorDistance = 0.5f;
    [SerializeField] private float exitScaleDuration = 0.2f;

    private readonly List<Level64ActorCtrl> _tools = new();
    private readonly HashSet<Level64ActorCtrl> _exitingTools = new();
    private bool canOpen = true;

    private void Update()
    {
        if (_tools.Count <= 0)
            return;

        bool isFinished = levelCtrl != null && levelCtrl.IsFinished;
        for (int i = _tools.Count - 1; i >= 0; i--)
        {
            Level64ActorCtrl tool = _tools[i];
            if (tool == null)
            {
                RemoveToolAt(i);
                continue;
            }

            if (isFinished)
            {
                HandleFinishedTool(tool);
                continue;
            }

            DispatchTransportTool(tool);
        }
    }

    private void OnDestroy()
    {
        for (int i = _tools.Count - 1; i >= 0; i--)
        {
            Level64ActorCtrl tool = _tools[i];
            if (tool == null)
                continue;

            levelCtrl?.CleanupActorCarry(tool);
            tool.transform.DOKill();
            Destroy(tool.gameObject);
        }

        _tools.Clear();
        _exitingTools.Clear();
    }

    public void ClickStartDoor()
    {
        if (!canOpen)
            return;

        if (!TrySpawnAI())
            return;

        canOpen = false;
        PlayOpenDoorAnim();
    }

    private async void PlayOpenDoorAnim()
    {
        if (startDoor == null)
        {
            canOpen = true;
            return;
        }

        AudioManager.PlaySound("openDoor");
        startDoor.PlayAnimation("door_open", false);

        await UniTask.WaitForSeconds(1f);

        startDoor.PlayAnimation("dose_close", false);

        await UniTask.WaitForSeconds(0.15f);
        AudioManager.PlaySound("closeDoor");

        await UniTask.WaitForSeconds(.15f);

        startDoor.PlayAnimation(DoorIdleAnimName, true);

        canOpen = true;
    }

    public bool TrySpawnAI()
    {
        CleanupNullTools();
        if (actor == null || startDoor == null || _tools.Count >= maxAICount)
            return false;

        int spawnOrder = _tools.Count;
        Level64ActorCtrl createdTool = CreateToolActor();
        if (createdTool == null)
            return false;

        RemoveGeneratedAIInheritedSkins(createdTool);
        AssignSpawnOrderSkin(createdTool, spawnOrder);
        _tools.Add(createdTool);
        createdTool.SetTransportState(Level64ActorCtrl.TransportState.ToOutput);
        return true;
    }

    public int GetAliveAICount()
    {
        CleanupNullTools();
        return _tools.Count;
    }

    /// <summary>
    /// Door trigger API. Bind this from a Collider2D trigger if you want AI to enter door on contact.
    /// </summary>
    public void OnAIEnterDoor(Collider2D collider)
    {
        if (collider == null)
            return;

        Level64ActorCtrl tool = collider.GetComponentInParent<Level64ActorCtrl>();
        if (tool == null || !_tools.Contains(tool))
            return;

        TryBeginToolExit(tool);
    }

    private void DispatchTransportTool(Level64ActorCtrl tool)
    {
        if (tool == null || levelCtrl == null || _exitingTools.Contains(tool))
            return;

        tool.SetMoveInput(tool.IsTransportToOutput() ? 1f : -1f);
        tool.OnUpdate();

        if (levelCtrl.IsActorBusy(tool))
            return;

        if (tool.IsTransportToOutput())
        {
            if (levelCtrl.IsActorInOutputArea(tool))
                levelCtrl.TryPickupOutputForActor(tool);
            return;
        }

        if (tool.IsTransportToInput() && levelCtrl.IsActorInInputArea(tool))
            levelCtrl.TryDropInputForActor(tool);
    }

    private Level64ActorCtrl CreateToolActor()
    {
        Level64ActorCtrl createdTool = Instantiate(actor, actor.transform.parent);
        createdTool.SwitchDash(true);
        createdTool.SetTransportState(Level64ActorCtrl.TransportState.ToOutput);
        createdTool.SetMoveInput(1f);

        Vector3 position = createdTool.transform.position;
        position.x = startDoor.transform.position.x;
        createdTool.transform.position = position;
        // createdTool.transform.localScale = Vector3.one * 0.7f;
        createdTool.transform.localEulerAngles = Vector3.zero;
        createdTool.gameObject.layer = LayerMask.NameToLayer("TriggerItem");
        return createdTool;
    }

    private void RemoveGeneratedAIInheritedSkins(Level64ActorCtrl tool)
    {
        if (tool == null)
            return;

        DisableGeneratedAISkinBinding(tool);
        ClearGeneratedAISkinPartMounts(tool.transform);
        ClearGeneratedAICarriedBlockClone(tool.transform);
    }

    private static void DisableGeneratedAISkinBinding(Level64ActorCtrl tool)
    {
        var bindings = tool.GetComponentsInChildren<ActorSkinBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i] != null)
                bindings[i].enabled = false;
        }
    }

    private void AssignSpawnOrderSkin(Level64ActorCtrl tool, int spawnOrder)
    {
        if (tool == null)
            return;

        string skinName = spawnOrder switch
        {
            0 => FirstHelperSkinName,
            1 => SecondHelperSkinName,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(skinName))
            return;

        if (tool.SpineCtrl == null)
        {
            this.LogWarning($"[Level64AICtrl] Helper {spawnOrder + 1} missing SpineCtrl; skin {skinName} was not applied.");
            return;
        }

        if (!tool.SpineCtrl.SetSkin(skinName))
            this.LogWarning($"[Level64AICtrl] Failed to apply helper skin: {skinName}");
    }

    private static void ClearGeneratedAISkinPartMounts(Transform root)
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

    private static void ClearGeneratedAICarriedBlockClone(Transform root)
    {
        var block = root != null ? root.Find(CarriedBlockCloneName) : null;
        if (block != null)
            Destroy(block.gameObject);
    }

    private void HandleFinishedTool(Level64ActorCtrl tool)
    {
        if (tool == null || _exitingTools.Contains(tool))
            return;

        tool.SwitchDash(true);
        tool.SetMoveInput(1f);
        tool.OnUpdate();

        if (door == null || tool.transform.position.x < door.position.x - exitDoorDistance)
            return;

        TryBeginToolExit(tool);
    }

    private bool TryBeginToolExit(Level64ActorCtrl tool)
    {
        if (tool == null || _exitingTools.Contains(tool) || !_tools.Contains(tool))
            return false;

        PlayToolExit(tool);
        return true;
    }

    private void PlayToolExit(Level64ActorCtrl tool)
    {
        _exitingTools.Add(tool);
        levelCtrl?.CleanupActorCarry(tool);
        tool.SetMoveInput(0f);
        tool.transform.DOKill();
        tool.transform.DOScale(Vector3.zero, exitScaleDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                _exitingTools.Remove(tool);
                _tools.Remove(tool);
                if (tool != null)
                    Destroy(tool.gameObject);
            });
    }

    private void CleanupNullTools()
    {
        for (int i = _tools.Count - 1; i >= 0; i--)
        {
            if (_tools[i] != null)
                continue;

            RemoveToolAt(i);
        }
    }

    private void RemoveToolAt(int index)
    {
        Level64ActorCtrl tool = _tools[index];
        if (tool != null)
            _exitingTools.Remove(tool);

        _tools.RemoveAt(index);
    }
}
