using System;
using System.Collections.Generic;
using System.Data;
using Bear.EventSystem;
using Bear.UI;
using Config;
using Game.ConfigModule;
using UnityEngine;
using UnityEngine.UI;

public partial class ChoiceLevelPanel : BaseUIView, IEventSender
{
    [SerializeField] private int MaxCount;
    [SerializeField] private RectTransform Content;
    [SerializeField] private LevelChoiceItem itemPrefab;

    private List<LevelData> datas;
    private int pageIndex = 0;
    private List<LevelChoiceItem> _itemCache;

    public override void OnOpen()
    {
        base.OnOpen();
        datas = ConfigManager.Instance.Tables.TbLevelData.DataList;

        InitItems();
        InitButtons();
    }

    private void InitItems()
    {
        if (_itemCache == null)
            _itemCache = new List<LevelChoiceItem>();

        if (_itemCache.Count <= MaxCount)
        {
            for (int i = _itemCache.Count; i < MaxCount; i++)
            {
                var item = Instantiate(itemPrefab, Content);
                item.Init(this);
                _itemCache.Add(item);
            }
        }

        pageIndex = 0;
        RefreshItems();
    }

    private void InitButtons()
    {
        LastBtn.OnClick += LastPage;
        NextBtn.OnClick += NextPage;

        RefreshBtn();
    }

    private void LastPage(CustomButton btn)
    {
        pageIndex = Math.Max(pageIndex - 1, 0);

        RefreshBtn();
        RefreshItems();
    }
    private void NextPage(CustomButton btn)
    {
        int dt = datas.Count % MaxCount > 0 ? 1 : 0;
        pageIndex = Math.Min(pageIndex + 1, datas.Count / MaxCount + dt);
        RefreshBtn();
        RefreshItems();
    }

    private void RefreshBtn()
    {
        LastBtn.gameObject.SetActive(pageIndex <= 0);
        NextBtn.gameObject.SetActive(pageIndex * MaxCount >= datas.Count);
    }

    private void RefreshItems()
    {
        if (_itemCache.Count <= 0)
            return;

        int index = -1;
        LevelData data = null;
        for (int i = 0; i < _itemCache.Count; i++)
        {
            index = pageIndex * MaxCount + i;
            _itemCache[i].SetData(index >= datas.Count ? null : datas[index]);
        }
    }

    public void EnterLevel(LevelData data)
    {
        this.DispatchEvent(Witness<Game.Events.EnterLevelEvent>._, data);
    }

    public static ChoiceLevelPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<ChoiceLevelPanel>($"{typeof(ChoiceLevelPanel).Name}", UILayer.Normal);

        return panel;
    }
}
