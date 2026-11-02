using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using MoreMountains.Feedbacks;
using UnityEngine;

public class Level29Ctrl : MonoBehaviour
{
    [SerializeField] private Transform ceiling;

    [SerializeField] private Transform door;

    [SerializeField] private ParticleSystem slam;
    [SerializeField] private MMF_Player shake;

    [SerializeField] private DragableItem dragItem;

    void Start()
    {
        ceiling.DOMoveY(-3.4f, 2.5f).SetDelay(2f).SetUpdate(true).SetEase(Ease.Linear);

        Invoke(nameof(OpenDragDoor), 1.5f);
    }

    private void OpenDragDoor()
    {
        dragItem.enabled = true;
    }

    public void KillMove(Collider2D collider)
    {
        ceiling.DOKill();

        PlayVfx().Forget();
    }

    private async UniTaskVoid PlayVfx()
    {
        door.GetComponent<BoxCollider2D>().enabled = false;
        door.GetComponent<DragableItem>().enabled = false;

        await UniTask.WaitForSeconds(0.1f);
        slam.Play();

        shake.PlayFeedbacks();
        AudioManager.PlaySound("blockByDoor");
    }

    void OnDestroy()
    {
        if (ceiling)
        {
            ceiling.DOKill();
        }
    }
}
