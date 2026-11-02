using System;
using System.Threading.Tasks;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using Lofelt.NiceVibrations;
using UnityEngine;

public class SuccessAnimCtrl : MonoBehaviour, IEventSender
{
    [SerializeField] private ActorSpineCtrl doorCtrl;
    [SerializeField] private GameObject ribbon;
    [SerializeField] private ParticleSystem rainbon;
    [SerializeField] private ActorDestroyAnimHandle destroyActor;

    [Tooltip("勾选后跳过成功动画末尾的关门音效；默认保留原行为。")]
    [SerializeField] private bool suppressCloseDoorSfx;

    public async void Play(Action callback)
    {
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        if (!doorCtrl.IsPlayingAnimation("door_open"))
        {
            AudioManager.PlaySound("openDoor");
            doorCtrl.PlayAnimation("door_open", false);
        }

        ribbon.SetActive(true);

        await UniTask.WaitForSeconds(0.4f, cancellationToken: this.destroyCancellationToken);
        AudioManager.PlaySound("enterDoor");
        // await UniTask.WaitForSeconds(0.4f, cancellationToken: this.destroyCancellationToken);

        if (destroyActor)
            destroyActor.PlayDestroy();

        await UniTask.WaitForSeconds(1.2f, cancellationToken: this.destroyCancellationToken);

        ribbon.SetActive(false);

        await UniTask.WaitForSeconds(0.2f, cancellationToken: this.destroyCancellationToken);
        doorCtrl.PlayAnimation("dose_close", false);
        await UniTask.WaitForSeconds(.25f, cancellationToken: this.destroyCancellationToken);
        if (!suppressCloseDoorSfx)
            AudioManager.PlaySound("closeDoor");
        if (DB.GameSetting.VibrationOn)
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
        // await UniTask.WaitForSeconds(.37f, cancellationToken: this.destroyCancellationToken);

        rainbon.Play();

        await UniTask.WaitForSeconds(.8f, cancellationToken: this.destroyCancellationToken);

        callback?.Invoke();
    }
}
