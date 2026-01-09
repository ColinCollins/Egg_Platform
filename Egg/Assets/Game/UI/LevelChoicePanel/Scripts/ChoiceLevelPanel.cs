using System.Collections.Generic;
using Bear.UI;
using Config;
using Game.ConfigModule;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.UI;

public partial class ChoiceLevelPanel : BaseUIView
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
                _itemCache.Add(item);
            }
        }

        pageIndex = 0;
    }

    private void LastPage(CustomButton btn)
    {
        RefreshBtn();
    }
    private void NextPage(CustomButton btn)
    {
        RefreshBtn();
    }

    private void RefreshBtn()
    {
        LastBtn.gameObject.SetActive(pageIndex <= 0);
        NextBtn.gameObject.SetActive(pageIndex * MaxCount >= datas.Count);
    }
    public static ChoiceLevelPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<ChoiceLevelPanel>($"{typeof(ChoiceLevelPanel).Name}", UILayer.Normal);

        return panel;
    }
}
