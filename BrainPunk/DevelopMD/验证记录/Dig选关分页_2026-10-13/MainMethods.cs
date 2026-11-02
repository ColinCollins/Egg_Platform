using System;using Bear.Logger;using Bear.UI;public partial class MainPanel{private int GetInitialPageIndex(int maxIndex)
    {
        var levelCtrl = PlayCtrl.Instance?.Level;
        if (levelCtrl?.CurrentLevelData != null)
        {
            int currentSortIndex = sortDatas.FindIndex(data => data.Id == levelCtrl.CurrentLevel);
            if (currentSortIndex >= 0)
            {
                return Math.Clamp(currentSortIndex / MaxCount, 0, maxIndex);
            }
        }

        return Math.Clamp(DB.GameData.ChioceLevelPanelPageIndex, 0, maxIndex);
    }
private void RefreshPageIndex()
    {
        int maxIndex = sortDatas.Count / MaxCount;
        if (sortDatas.Count % MaxCount == 0)
            maxIndex -= 1;

        pageIndex = GetInitialPageIndex(maxIndex);
    }
private void OnQuickTurnPageChanged(int targetPageIndex)
    {
        if (_isPageAnimating)
            return;

        int maxPageIndex = sortDatas.Count <= 0 ? 0 : (sortDatas.Count - 1) / MaxCount;
        int newPageIndex = Math.Clamp(targetPageIndex, 0, maxPageIndex);
        if (newPageIndex == pageIndex)
            return;

        pageIndex = newPageIndex;
        RefreshBtn();
        RefreshItems();
    }
public void EnterLevel(LevelSort sort)
    {
        this.Log("--------------------- enter level");

        this.DispatchEvent(Witness<Game.Events.EnterLevelEvent>._, sort);
        WaitToClose().Forget();

        DB.GameData.ChioceLevelPanelPageIndex = pageIndex;
        DB.GameData.Save();
    }
public override void OnClose()
    {
        base.OnClose();
        EventsUtils.ResetEvents(ref _subscriber);
        UnsubscribeGameCenterAuthState();
    }}