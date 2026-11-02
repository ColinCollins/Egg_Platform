using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Game.Events;
using UnityEngine;

public class GamePlayPanel_DragButtons : MonoBehaviour
{
    [SerializeField] private List<CustomButton> Btns;

    private List<UIDrag> uidrags;
    private List<Vector3> originsPos;

    private EventSubscriber _subscriber;

    void Start()
    {
        if (originsPos == null)
        {
            originsPos = new List<Vector3>();
            uidrags = new List<UIDrag>();
        }

        // 为每个按钮添加或获取 UIDrag 组件
        for (int i = 0; i < Btns.Count; i++)
        {
            if (Btns[i] != null)
            {
                // 获取或添加 UIDrag 组件
                UIDrag uidrag = Btns[i].GetComponent<UIDrag>();
                if (uidrag == null)
                {
                    uidrag = Btns[i].gameObject.AddComponent<UIDrag>();
                }

                uidrags.Add(uidrag);
                originsPos.Add(Btns[i].transform.position);
                
                // 设置原始位置
                uidrag.SetOriginalPosition(Btns[i].transform.position);

                // 设置拖拽委托，处理 CustomButton.IsBlockClick
                CustomButton btn = Btns[i]; // 闭包变量
                uidrag.OnDragBegin += () => OnDragBeginHandle(btn);
                uidrag.OnDragEnd += () => OnDragEndHandle(btn);
            }
        }

        AddListener();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnGameReset(GameResetEvent @event)
    {
        ResetUI();
    }

    public void ResetUI()
    {
        for (int i = 0; i < originsPos.Count && i < uidrags.Count; i++)
        {
            if (uidrags[i] != null)
            {
                uidrags[i].ResetPosition();
            }
        }
    }

    void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }

    /// <summary>
    /// 获取按钮列表（供外部访问）
    /// </summary>
    public List<CustomButton> GetButtons()
    {
        return Btns;
    }

    /// <summary>
    /// 拖拽开始时的处理
    /// </summary>
    private void OnDragBeginHandle(CustomButton btn)
    {
        if (btn != null)
        {
            btn.IsBlockClick = true;
        }
    }

    /// <summary>
    /// 拖拽结束时的处理
    /// </summary>
    private void OnDragEndHandle(CustomButton btn)
    {
        if (btn != null)
        {
            btn.IsBlockClick = false;
        }
    }
}
