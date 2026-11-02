using SuperScrollView;
using UnityEngine;

namespace GF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LoopListView2))]
    public class LoopList: MonoBehaviour
    {
        public LoopListView2 loopListView;
        private System.Func<LoopListView2, int, LoopListViewItem2> _onGetItemByIndex;
        public void Init(int itemTotalCount,
            System.Func<LoopListView2, int, LoopListViewItem2> onGetItemByIndex)
        {
            if (loopListView == null)
            {
                loopListView = GetComponent<LoopListView2>();
            }
            _onGetItemByIndex = onGetItemByIndex;
            loopListView.InitListView(itemTotalCount, OnGetItemByIndex);
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopList, int index)
        {
            if (index < 0 || index >= loopListView.ItemTotalCount)
            {
                return null;
            }
            return _onGetItemByIndex(loopList, index);
        }


        public void AddPrefab(ItemPrefabConfData itemPrefabData)
        {
            if (itemPrefabData == null)
            {
                return;
            }
            loopListView.ItemPrefabDataList.Add(itemPrefabData);
        }
        
        public void GoToIndex(int index, float offset = 0)
        {
            if (index < 0 || index >= loopListView.ItemTotalCount)
            {
                return;
            }

            loopListView.MovePanelToItemIndex(index, offset);
        }

        /// <summary>
        /// 设置item数量
        /// </summary>
        /// <param name="count">数量</param>
        /// <param name="resetPos">是否定位到起始位置</param>
        public void SetItemCount(int count, bool resetPos)
        {
            if (count == loopListView.ItemTotalCount)
            {
                return;
            }
            loopListView.SetListItemCount(count, resetPos);
            loopListView.RefreshAllShownItem();
        }
    }
}