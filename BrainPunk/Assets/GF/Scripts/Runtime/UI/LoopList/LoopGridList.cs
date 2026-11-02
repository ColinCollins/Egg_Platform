using SuperScrollView;
using UnityEngine;

namespace GF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LoopGridView))]
    public class LoopGridList: MonoBehaviour
    {
        public LoopGridView loopGridView;
        private System.Func<LoopGridView, int, int, int, LoopGridViewItem> _onGetItemByIndex;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="itemTotalCount"></param>
        /// <param name="onGetItemByIndex"></param>
        public void Init(int itemTotalCount,
            System.Func<LoopGridView, int, int, int, LoopGridViewItem> onGetItemByIndex)
        {
            if (loopGridView == null)
            {
                loopGridView = GetComponent<LoopGridView>();
            }
            _onGetItemByIndex = onGetItemByIndex;
            loopGridView.InitGridView(itemTotalCount, OnGetItemByIndex);
        }

        /// <summary>
        /// Item的回调
        /// </summary>
        /// <param name="loopList"></param>
        /// <param name="index"></param>
        /// <param name="row"></param>
        /// <param name="column"></param>
        /// <returns></returns>
        private LoopGridViewItem OnGetItemByIndex(LoopGridView loopList, int index,int row,int column)
        {
            if (index < 0 || index >= loopGridView.ItemTotalCount)
            {
                return null;
            }
            return _onGetItemByIndex(loopList, index, row, column);
        }

        /// <summary>
        /// 添加Prefab
        /// </summary>
        /// <param name="itemPrefabData"></param>
        public void AddPrefab(GridViewItemPrefabConfData itemPrefabData)
        {
            if (itemPrefabData == null)
            {
                return;
            }
            loopGridView.ItemPrefabDataList.Add(itemPrefabData);
        }

        /// <summary>
        /// 滚动到指定的位置
        /// </summary>
        /// <param name="index"></param>
        /// <param name="offset"></param>
        public void GoToIndex(int index, float offset = 0)
        {
            if (index < 0 || index >= loopGridView.ItemTotalCount)
            {
                return;
            }

            loopGridView.MovePanelToItemByIndex(index, offset);
        }

        /// <summary>
        /// 设置item数量
        /// </summary>
        /// <param name="count">数量</param>
        /// <param name="resetPos">是否定位到起始位置</param>
        public void SetItemCount(int count, bool resetPos)
        {
            if (count == loopGridView.ItemTotalCount)
            {
                return;
            }
            loopGridView.SetListItemCount(count, resetPos);
            loopGridView.RefreshAllShownItem();
        }
    }
}