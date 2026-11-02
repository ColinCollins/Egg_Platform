using Bear.EventSystem;
using Game.Events;
using UnityEngine;

public class Level42UICtrl : MonoBehaviour, IEventSender
{
    [SerializeField] private CustomButton pauseBtn;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (pauseBtn != null)
            pauseBtn.OnClick += OnClickPause;   
    }

    // 触发事件
    private void OnClickPause(CustomButton btn)
    {
        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
    }

}
