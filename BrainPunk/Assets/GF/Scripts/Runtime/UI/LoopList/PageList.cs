using System;
using System.Collections.Generic;
using SuperScrollView;
using UnityEngine;

namespace GF
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LoopListView2))]
    public class PageList : MonoBehaviour
    {
        class DotElement
        {
            public GameObject dotElemRoot;
            public GameObject dotNormal;
            public GameObject dotSelect;
        }

        public Action<LoopListView2,LoopListViewItem2> onSnapFinish;
        
        [SerializeField] private bool _hasDot;
        [SerializeField] private bool _isClickEvent;
        private System.Func<LoopListView2, int, LoopListViewItem2> _onGetItemByIndex;

        public LoopListView2 loopListView;
        public RectTransform dotsRoot;
        public RectTransform dotTemplate;
        private int _pageCount = 0;
        private List<DotElement> _dotElemList = new();

        public void Init(int pageCount, Func<LoopListView2, int, LoopListViewItem2> onGetItemByIndex,float snapSpeed = 10, float snapFinishThreshold = 0.1f)
        {
            _pageCount = pageCount;
            if (_hasDot)
            {
                InitAllDots();
            }

            if (loopListView == null)
            {
                loopListView = GetComponent<LoopListView2>();
            }

            _onGetItemByIndex = onGetItemByIndex;
            LoopListViewInitParam initParam = LoopListViewInitParam.CopyDefaultInitParam();
            initParam.mSnapVecThreshold = 99999;        //滚动速度小于该值时，触发自动吸附
            initParam.mSnapFinishThreshold = snapFinishThreshold;      //自动吸附完成的阈值
            initParam.mSmoothDumpRate = 10 / snapSpeed;        //吸附速度衰减率
            loopListView.mOnEndDragAction = OnEndDrag;
            loopListView.mOnSnapNearestChanged = OnSnapNearestChanged;
            loopListView.mOnSnapItemFinished = OnSnapFinish;
            loopListView.InitListView(_pageCount, OnGetItemByIndex, initParam);
        }

        LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pageCount)
            {
                return null;
            }

            return _onGetItemByIndex(listView, pageIndex);
        }

        public void GoToIndex(int index, float offset = 0)
        {
            if (index < 0 || index >= loopListView.ItemTotalCount)
            {
                return;
            }

            loopListView.MovePanelToItemIndex(index, offset);
        }

        private void OnSnapNearestChanged(LoopListView2 listView, LoopListViewItem2 item)
        {
            if (_hasDot)
            {
                UpdateAllDots();
            }
        }

        private void OnSnapFinish(LoopListView2 arg1, LoopListViewItem2 arg2)
        {
            onSnapFinish?.Invoke(arg1, arg2);
        }

        void OnEndDrag()
        {
            float vec = loopListView.ScrollRect.velocity.x;
            int curNearestItemIndex = loopListView.CurSnapNearestItemIndex;
            LoopListViewItem2 item = loopListView.GetShownItemByItemIndex(curNearestItemIndex);
            if (item == null)
            {
                loopListView.ClearSnapData();
                return;
            }

            if (Mathf.Abs(vec) < 50f)
            {
                loopListView.SetSnapTargetItemIndex(curNearestItemIndex);
                return;
            }

            Vector3 pos = loopListView.GetItemCornerPosInViewPort(item, ItemCornerEnum.LeftTop);
            if (pos.x > 0)
            {
                if (vec > 0)
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex - 1);
                }
                else
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex);
                }
            }
            else if (pos.x < 0)
            {
                if (vec > 0)
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex);
                }
                else
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex + 1);
                }
            }
            else
            {
                if (vec > 0)
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex - 1);
                }
                else
                {
                    loopListView.SetSnapTargetItemIndex(curNearestItemIndex + 1);
                }
            }
        }

        #region Dot

        public void ResetDots()
        {
            if (!_hasDot)
            {
                return;
            }
            if (_pageCount == _dotElemList.Count)
            {
                return;
            }

            if (_pageCount > _dotElemList.Count)
            {
                int addCount = _pageCount - _dotElemList.Count;
                AppendDots(addCount);
            }
            else
            {
                int removeCount = _dotElemList.Count - _pageCount;
                RemoveDots(removeCount);
            }

            int curNearestItemIndex = loopListView.CurSnapNearestItemIndex;
            RefreshAllDots(curNearestItemIndex);
        }

        void InitAllDots()
        {
            dotTemplate.gameObject.SetActive(false);
            CreateDots(_pageCount);
        }

        void CreateDots(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CreateOneDot(dotsRoot, dotTemplate);
            }
        }

        void CreateOneDot(RectTransform rectParent, RectTransform rectTemplate)
        {
            int dotIndex = _dotElemList.Count;
            GameObject obj = GameObject.Instantiate(rectTemplate.gameObject, rectParent);
            obj.gameObject.name = "dot" + dotIndex;
            obj.gameObject.SetActive(true);
            RectTransform rectTrans = obj.GetComponent<RectTransform>();
            rectTrans.localScale = Vector3.one;
            rectTrans.localEulerAngles = Vector3.zero;
            rectTrans.anchoredPosition3D = Vector3.zero;
            rectTrans.SetAsLastSibling();

            DotElement elem = new DotElement();
            elem.dotElemRoot = obj;
            elem.dotNormal = obj.transform.Find("DotNormal").gameObject;
            elem.dotSelect = obj.transform.Find("DotSelect").gameObject;
            _dotElemList.Add(elem);
            if (_hasDot && _isClickEvent)
            {
                ClickEventListener listener = ClickEventListener.Get(elem.dotElemRoot);
                listener.SetClickEventHandler(delegate(GameObject tmpObj) { OnDotClicked(dotIndex); });
            }
        }

        void OnDotClicked(int index)
        {
            int curNearestItemIndex = loopListView.CurSnapNearestItemIndex;
            if (curNearestItemIndex < 0 || curNearestItemIndex >= _pageCount)
            {
                return;
            }

            if (index == curNearestItemIndex)
            {
                return;
            }

            loopListView.SetSnapTargetItemIndex(index);
        }

        void UpdateAllDots()
        {
            int curNearestItemIndex = loopListView.CurSnapNearestItemIndex;
            if (curNearestItemIndex < 0 || curNearestItemIndex >= _pageCount)
            {
                return;
            }

            int count = _dotElemList.Count;
            if (curNearestItemIndex >= count)
            {
                return;
            }

            RefreshAllDots(curNearestItemIndex);
        }

        void RefreshAllDots(int selectedIndex)
        {
            for (int i = 0; i < _dotElemList.Count; ++i)
            {
                DotElement elem = _dotElemList[i];
                if (i != selectedIndex)
                {
                    elem.dotNormal.SetActive(true);
                    elem.dotSelect.SetActive(false);
                }
                else
                {
                    elem.dotNormal.SetActive(false);
                    elem.dotSelect.SetActive(true);
                }
            }
        }

        void AppendDots(int count)
        {
            CreateDots(count);
        }

        void RemoveDots(int count)
        {
            while (count > 0)
            {
                int removeIndex = _dotElemList.Count - 1;
                DotElement elem = _dotElemList[removeIndex];
                _dotElemList.RemoveAt(removeIndex);
                GameObject.Destroy(elem.dotElemRoot);
                count--;
            }
        }

        #endregion
    }
}