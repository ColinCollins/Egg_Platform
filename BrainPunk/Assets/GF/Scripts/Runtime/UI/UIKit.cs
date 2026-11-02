using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Navigation;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;
using Object = UnityEngine.Object;


namespace GF
{
    public partial class UIKit
    {
        //UI根节点
        private Transform _uiRoot;
        //UI相机
        private Camera _uiCamera;
        //UI CanvasScaler
        private CanvasScaler _canvasScaler;

        //正在显示的UI堆栈
        private List<Type> _uiStack = new List<Type>();
        ///所有的UI字典
        private Dictionary<Type, UIBase> _uis = new Dictionary<Type, UIBase>();
        //正在打开的UI界面
        private HashSet<Type> _uiOpenning = new HashSet<Type>();
        //正在关闭的UI界面
        private HashSet<Type> _uiClosing = new HashSet<Type>();
        
        public Func<bool> IsTablet { set; get; }

        public void Initialize()
        {
            Object.DontDestroyOnLoad(GetUIRoot().parent);
        }

        /// <summary>
        /// 获取UI根节点
        /// </summary>
        /// <returns></returns>
        public Transform GetUIRoot()
        {
            if (_uiRoot == null)
            {
                _uiRoot = GameObject.Find("UIRoot/Root").transform;
            }

            return _uiRoot;
        }

        /// <summary>
        /// 获取UI相机
        /// </summary>
        /// <returns></returns>
        public Camera GetUICamera()
        {
            if (_uiCamera == null)
            {
                _uiCamera = GameObject.Find("UIRoot/UICamera").GetComponent<Camera>();
            }

            return _uiCamera;
        }

        /// <summary>
        /// 获取CanvasScaler
        /// </summary>
        /// <returns></returns>
        public CanvasScaler GetCanvasScaler()
        {
            if (_canvasScaler == null)
            {
                _canvasScaler = GameObject.Find("UIRoot").GetComponent<CanvasScaler>();
            }

            return _canvasScaler;
        }
        
        /// <summary>
        /// 尝试打开已经存在的UI
        /// </summary>
        /// <param name="args"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private (T, bool) TryOpenExistUI<T>(params object[] args) where T : UIBase
        {
            Type uiType = typeof(T);
            if (_uis.TryGetValue(uiType, out UIBase uiBaseOpened))
            {
                LogKit.I("界面已经打开中，执行OnRefresh方法");
                _uiStack.Remove(uiType);
                _uiStack.Add(uiType);
                uiBaseOpened.SetParams(true, args);
                SortUIStack(false);
                return (uiBaseOpened as T, false);
            }

            if (_uiOpenning.Contains(uiType))
            {
                LogKit.I("界面正在打开中，无法重复打开");
                return (null, true);
            }

            return (null, false);
        }
        
        
        /// <summary>
        /// 打开界面后的处理
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="attribute"></param>
        /// <param name="args"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private T OpenAfterLoad<T>(GameObject prefab, UIViewAttribute attribute, params object[] args) where T : UIBase
        {
            if (_uiOpenning.Contains(typeof(T)))
            {
                GameObject obj = GameObject.Instantiate(prefab, GetUIRoot());
                _uiOpenning.Remove(typeof(T));

                UIBase uiBase = obj.GetComponent<UIBase>();
                uiBase.SetUIConfig(attribute);
                _uis[typeof(T)] = uiBase;
                AddCanvas(uiBase);
                

                uiBase.SetParams(false, args);
                // 这里使用 Navigator 来管理界面导航，内部会去调用 Initialize 方法
                Navigator.Instance.NavigateTo(uiBase);
                // uiBase.Initialize();
                _uiStack.Add(typeof(T));

                SortUIStack(false);

                return uiBase as T;
            }else
            {
                LogKit.I("界面打开失败，已经被关闭");
                App.Res.ReleaseAsset(attribute.path);
                return null;
            }
        }

        /// <summary>
        /// 异步打开界面
        /// </summary>
        /// <param name="baseLogic"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public async UniTask<T> OpenAsync<T>(params object[] args) where T : UIBase
        {
            Type uiType = typeof(T);
            (T ret, bool opening) = TryOpenExistUI<T>(args);
            if (ret != null)
            {
                return ret;
            }
            if (opening)
            {
                LogKit.E("界面正在打开中，无法重复打开");
                return null;
            }

            // 获取UI配置
            UIViewAttribute attribute = Attribute.GetCustomAttribute(uiType, typeof(UIViewAttribute)) as UIViewAttribute;

            if (attribute == null)
            {
                LogKit.I("找不到UI配置，无法打开");
                return null;
            }

            // 加入打开中列表
            _uiOpenning.Add(uiType);
            GameObject prefab = await App.Res.LoadAssetAsync<GameObject>(attribute.path, attribute.path);
            
            ret = OpenAfterLoad<T>(prefab, attribute, args);

            return ret;
        }

        /// <summary>
        /// 同步打开界面
        /// </summary>
        /// <param name="baseLogic"></param>
        /// <param name="args"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T OpenSync<T>(params object[] args) where T : UIBase
        {
            Type uiType = typeof(T);
            (T ret, bool opening) = TryOpenExistUI<T>(args);
            if (ret != null)
            {
                return ret;
            }
            if (opening)
            {
                LogKit.E("界面正在打开中，无法重复打开");
                return null;
            }

            // 获取UI配置
            UIViewAttribute attribute = Attribute.GetCustomAttribute(uiType, typeof(UIViewAttribute)) as UIViewAttribute;

            if (attribute == null)
            {
                LogKit.I("找不到UI配置，无法打开");
                return null;
            }

            // 加入打开中列表
            _uiOpenning.Add(uiType);
            GameObject prefab = App.Res.LoadAsset<GameObject>(attribute.path, attribute.path);
            
            ret = OpenAfterLoad<T>(prefab, attribute, args);

            return ret;
        }

        /// <summary>
        /// 获取UI
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetUI<T>() where T : UIBase
        {
            if (_uis.TryGetValue(typeof(T), out UIBase uiBase))
            {
                return uiBase as T;
            }

            return null;
        }

        /// <summary>
        /// 关闭所有UI
        /// </summary>
        public void CloseAll()
        {
            foreach (KeyValuePair<Type, UIBase> item in _uis)
            {
                UIBase uiBase = item.Value;

                //执行界面退出逻辑
                uiBase.OnExit();
                GameObject.Destroy(uiBase.gameObject);
                App.Res.ReleaseAsset(uiBase.Path);
            }

            _uiStack.Clear();
            _uis.Clear();
            _uiOpenning.Clear();
            _uiClosing.Clear();
        }

        /// <summary>
        /// 通过UIID关闭界面
        /// </summary>
        /// <param name="uiid"></param>
        public void Close<T>(Action callback = null) where T : UIBase
        {
            Type uiType = typeof(T);
            if (_uiClosing.Contains(uiType))
            {
                LogKit.E($"{uiType} 正在关闭中...");
                return;
            }
            if (_uis.TryGetValue(uiType, out UIBase uiBase))
            {
                //如果不是集成UIPopup的界面，直接销毁
                UIAnimationBase uiAnimation = uiBase.GetComponent<UIAnimationBase>();
                if (uiAnimation != null)
                {
                    _uiClosing.Add(uiType);
                    uiAnimation.PlayExitAnimation(delegate
                    {
                        DestroyUI<T>();
                        callback?.Invoke();
                    });
                }else
                {
                    DestroyUI<T>();
                    callback?.Invoke();
                }
            }
        }
        
        /// <summary>
        /// 关闭最上层的UI
        /// </summary>
        public void CloseTop()
        {
            if (_uiStack.Count > 0)
            {
                for (int i = _uiStack.Count - 1; i >= 0; i--)
                {
                    Type uiType = _uiStack[i];
                    if(_uis.TryGetValue(uiType, out UIBase uiBase))
                    {
                        if (uiBase.gameObject.activeSelf == true)
                        {
                            uiBase.OnDeviceBack();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取最上层ui
        /// </summary>
        /// <returns></returns>
        public UIBase GetTopUI()
        {
            if (_uiStack.Count > 0)
            {
                for (int i = _uiStack.Count - 1; i >= 0; i--)
                {
                    Type uiType = _uiStack[i];
                    if(_uis.TryGetValue(uiType, out UIBase uiBase))
                    {
                        if (uiBase.IsActive() == true)
                        {
                            return uiBase;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 销毁UI
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private void DestroyUI<T>() where T : UIBase
        {
            Type uiType = typeof(T);
            UIBase uiBase = _uis[uiType];
            
            if (uiBase.HideWhenRemove)
            {
                uiBase.Hide();
            }else
            {
                //执行界面退出逻辑
                uiBase.OnExit();
                if (!uiBase.IsNull())
                {
                    GameObject.Destroy(uiBase.gameObject);
                }
                App.Res.ReleaseAsset(uiBase.Path);
                //从UI字典中移除
                _uis.Remove(uiType);
            }
            
            _uiClosing.Remove(uiType);
            //从UI堆栈中移除
            _uiStack.Remove(uiType);

            //重新排序UI堆栈
            SortUIStack(true);
        }

        /// <summary>
        /// 重新排序UI堆栈
        /// </summary>
        private void SortUIStack(bool isClose)
        {
            bool flag = false;
            //从栈顶开始遍历
            for (int i = _uiStack.Count - 1; i >= 0; i--)
            {
                Type currUiId = _uiStack[i];
                if (!flag)
                {
                    //如果存在多个界面，则根据Layer Name判断是否隐藏
                    if (i > 0)
                    {
                        Type nextUiId = _uiStack[i - 1];

                        UIBase currUIBase= _uis[currUiId];
                        UIBase nextUIBase = _uis[nextUiId];

                        if (currUIBase.Layer >= nextUIBase.Layer)
                        {
                            if (_uis.TryGetValue(currUiId, out UIBase currUI))
                            {
                                //关闭界面时，才刷新
                                if (isClose)
                                {
                                    currUI.OnRefresh();
                                }
                                currUI.gameObject.SetActive(true);
                            }

                            //当相同Layer，则跳过后续判断，通过else直接隐藏
                            if (currUIBase.Layer == nextUIBase.Layer)
                            {
                                flag = true;
                            }
                        }else
                        {
                            flag = true;
                        }
                    }
                    else
                    {
                        if (_uis.TryGetValue(currUiId, out UIBase currUI))
                        {
                            //关闭界面时，才刷新
                            if (isClose)
                            {
                                currUI.OnRefresh();    
                            }
                            currUI.gameObject.SetActive(true);
                        }
                    }
                }else
                {
                    if (_uis.TryGetValue(currUiId, out UIBase currUI))
                    {
                        if (currUI.Layer != UILayer.TopLayer)
                        {
                            // currUI.gameObject.SetActive(false);
                            currUI.Hide();
                        }
                    }

                    //跨Layer时，需要重新判断
                    if (i > 0)
                    {
                        Type nextUiId = _uiStack[i - 1];
                        UIBase currUIBase= _uis[currUiId];
                        UIBase nextUIBase = _uis[nextUiId];

                        if (currUIBase.Layer != nextUIBase.Layer)
                        {
                            flag = false;
                        }
                    }
                }
            }

            int[] orders = {0, 0, 0, 0, 0, 0};
            int orderSpan = 200;
            //遍历UI堆栈，重新排序
            for (int i = 0; i < _uiStack.Count; i++)
            {
                Type currUiId = _uiStack[i];
                if (_uis.TryGetValue(currUiId, out UIBase currUI))
                {
                    switch (currUI.Layer)
                    {
                        case UILayer.SceneLayer:
                            SetCanvasOrder(currUI, orders[0]);
                            orders[0] += orderSpan;
                            break;
                        case UILayer.BaseLayer:
                            SetCanvasOrder(currUI, orders[1]);
                            orders[1] += orderSpan;
                            break;
                        case UILayer.MiddleLayer:
                            SetCanvasOrder(currUI, orders[2]);
                            orders[2] += orderSpan;
                            break;
                        case UILayer.PopupLayer:
                            SetCanvasOrder(currUI, orders[3]);
                            orders[3] += orderSpan;
                            break;
                        case UILayer.TopLayer:
                            SetCanvasOrder(currUI, orders[4]);
                            orders[4] += orderSpan;
                            break;
                        case UILayer.NotifyLayer:
                            SetCanvasOrder(currUI, orders[5]);
                            orders[5] += orderSpan;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 获取SortingLayerName
        /// </summary>
        /// <param name="uiLayer"></param>
        /// <returns></returns>
        public string GetSortingLayerName(UILayer uiLayer)
        {
            return uiLayer.ToString();
        }

        /// <summary>
        /// 添加Canvas
        /// </summary>
        /// <param name="uiBase"></param>
        /// <returns></returns>
        private Canvas AddCanvas(UIBase uiBase)
        {
            GameObject go = uiBase.gameObject;

            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = go.AddComponent<Canvas>();
            }

            canvas.overrideSorting = true;
            string name = GetSortingLayerName(uiBase.Layer);
            canvas.sortingLayerName = name;

            GraphicRaycaster graphicRayCaster = go.GetComponent<GraphicRaycaster>();
            if (graphicRayCaster == null)
            {
                graphicRayCaster = go.AddComponent<GraphicRaycaster>();
            }

            graphicRayCaster.enabled = true;

            return canvas;
        }

        /// <summary>
        /// 设置Canvas的排序Order
        /// </summary>
        /// <param name="uiBase"></param>
        /// <param name="order"></param>
        private void SetCanvasOrder(UIBase uiBase, int order)
        {
            Canvas canvas = AddCanvas(uiBase);
            canvas.sortingOrder = order;
            
                     
            Debug.Log($"<color=orange> Add Canvas for [{canvas.gameObject.name}]  -> {order} </color>");

            UpdateChildrenSortingOrder(uiBase);
        }
        
        /// <summary>
        /// 刷新子节点的Canvas层级
        /// </summary>
        /// <param name="uiBase"></param>
        private void UpdateChildrenSortingOrder(UIBase uiBase)
        {
            Canvas[] canvases = uiBase.gameObject.GetComponentsInChildren<Canvas>(true);
   

            //Canvas需要设置SortingOrder
            foreach (Canvas aCanvas in canvases)
            {
                UpdateSortingOrder(aCanvas.gameObject, uiBase);
            }

            //ParticleSystemRenderer需要设置SortingOrder
            ParticleSystemRenderer[] particleSystemRenderers = uiBase.gameObject.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (ParticleSystemRenderer aParticleSystemRenderer in particleSystemRenderers)
            {
                UpdateSortingOrder(aParticleSystemRenderer.gameObject, uiBase);
            }
        }

        /// <summary>
        /// 更新指定对象的SortingOrder
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="uiBase"></param>
        private void UpdateSortingOrder(GameObject gameObject, UIBase uiBase)
        {
            SortingOrderHelper sortingOrderHelper = gameObject.GetComponent<SortingOrderHelper>();
            if (sortingOrderHelper == null)
            {
                sortingOrderHelper = gameObject.AddComponent<SortingOrderHelper>();
                sortingOrderHelper.Initialize();
            }

            sortingOrderHelper.SetSortingOrder(uiBase);
        }

        /// <summary>
        /// 判断该层级的ui是否显示
        /// </summary>
        /// <param name="uiLayer"></param>
        /// <returns></returns>
        public bool IsShowUIByLayer(UILayer uiLayer)
        {
            foreach (var uiBase in _uis.Values)
            {
                if (uiBase.Layer == uiLayer && uiBase.IsActive())
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断这些层级的是否存在显示的ui
        /// </summary>
        /// <param name="layers"></param>
        /// <returns></returns>
        public bool IsShowUIByLayers(List<UILayer> layers)
        {
            foreach (var uiBase in _uis.Values)
            {
                if (layers.Contains(uiBase.Layer) && uiBase.IsActive())
                {
                    return true;
                }
            }

            return false;
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) // 返回键
            {
                //Code
                LogKit.I("返回键被点击");
                CloseTop();
            }
        }
    }
}