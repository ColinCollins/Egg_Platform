using System;
using System.Linq;
using GF.Guru;
using GF.Scripts.Runtime.Guru;
using Guru.SDK.Framework.Utils.Controller;
using Guru.SDK.Framework.Utils.Log;
using Guru.SDK.Framework.Utils.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace GF
{
    public abstract class UIBase : LifecycleElement
    {
        [HideInInspector]
        public string Path;

        public override RoutePath RoutePath { get; internal set; }

        public override LifecycleController Controller => _baseLogic.Controller;
        
        [HideInInspector]
        public UILayer Layer;
        [HideInInspector]
        public bool HideWhenRemove;

        protected Transform _root;

        //缓存关键参数 同时可用作异步判空操作
        protected Transform _transform;
        protected GameObject _gameObject;

        protected object[] _args;

        protected BaseLogic _baseLogic;
        

        /// <summary>
        ///  判空接口
        /// </summary>
        public bool IsNull() => null == _gameObject;

        /// <summary>
        /// 根节点
        /// </summary>
        public Transform Root { get => _root; }

        /// <summary>
        /// ui是否是激活状态
        /// </summary>
        /// <returns></returns>
        public bool IsActive() => null != _gameObject && _gameObject.activeInHierarchy;

        protected override void OnInitialized()
        {
            Log.D("OnInitialized");
            _root = transform.Find("root");
            // 由于在设置Logic的时候，Awake已经触发，则无法在View中的Awake回调中触发，所以Awake在此处进行主动触发
            _baseLogic = CreateLogic(_args) ?? new LifecycleLogic();
            // 这里使用 Setup 内部会调用 Initialize 方法
            _baseLogic?.Setup();
 
            // _baseLogic?.Initialize();

            ScriptGenerator();
        }

        public abstract BaseLogic CreateLogic(params object[] args);

        /// <summary>
        /// Start 生命周期
        /// </summary>
        public virtual void Start()
        {
            SetScreenEvent();
            OnBeforeStart();
            SafeAreaResize();
            AddEvent();
            OnEnter();
        }

        public virtual void Awake()
        {
            _gameObject = gameObject;
            _transform = transform;
        }
        
        /// <summary>
        /// 分辨率适配
        /// </summary>
        public void SafeAreaResize()
        {
            if (_root != null)
            {
                SafeAreaSizer safeAreaSizer = _root.gameObject.GetComponent<SafeAreaSizer>();
                if (safeAreaSizer == null)
                {
                    _root.gameObject.AddComponent<SafeAreaSizer>();
                }
            }
        }

        /// <summary>
        /// 设置界面配置
        /// </summary>
        /// <param name="uiViewAttribute"></param>
        public void SetUIConfig(UIViewAttribute uiViewAttribute)
        {
            Path = uiViewAttribute.path;
            // TODO: 这里需要跟进，是否是有效路径
            RoutePath = new RoutePath(Path);
            Layer = uiViewAttribute.uiLayer;
            HideWhenRemove = uiViewAttribute.hideWhenRemove;
        }

        /// <summary>
        /// 标签
        /// </summary>
        /// <returns></returns>
        public string GetTag()
        {
            return $"{Path}";
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        public virtual void AddEvent() {}
        
        /// <summary>
        /// 脚本生成器
        /// </summary>
        protected virtual void ScriptGenerator()
        {
            
        }

        /// <summary>
        /// 销毁界面
        /// </summary>
        public virtual void OnDestroy()
        {
            _baseLogic?.OnDestroy();
            _baseLogic = null;
            _transform = null;
            _gameObject = null;
        }

        /// <summary>
        /// 进入界面
        /// </summary>
        public virtual void OnBeforeStart()
        {
            _baseLogic?.OnBeforeStart();
        }

        /// <summary>
        /// 进入界面
        /// </summary>
        public virtual void OnEnter()
        {
            _baseLogic?.OnEnter();
            //页面被打开时发送
            App.Event.DispatchEvent(Define.Event.OnUIViewShowAtTop, GetType().Name);
        }

        /// <summary>
        /// ui界面显示
        /// </summary>
        private void OnEnable()
        {
            _baseLogic?.OnEnable();
        }
        
        /// <summary>
        /// ui界面隐藏
        /// </summary>
        private void OnDisable()
        {
            _baseLogic?.OnDisable();
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update()
        {
            _baseLogic?.Update();
        }

        /// <summary>
        /// 刷新界面
        /// </summary>
        public virtual void OnRefresh()
        {
            SetScreenEvent();
            _baseLogic?.OnRefresh();
            //页面被重新唤醒时发送
            App.Event.DispatchEvent(Define.Event.OnUIViewShowAtTop, GetType().Name);
        }
        
        /// <summary>
        /// 返回按钮监听事件（只有最上层才会调用）
        /// </summary>
        public virtual void OnDeviceBack()
        {
            _baseLogic?.OnDeviceBack();
        }

        public virtual void SetScreenEvent()
        {
            //设置firebase screen name
            string uiName = gameObject.name;
            if (uiName.Contains("(Clone)"))
            {
                uiName = uiName.Replace("(Clone)", "");
            }
        }

        /// <summary>
        /// 更新参数
        /// </summary>
        /// <param name="isRefresh"></param>
        /// <param name="args"></param>
        public void SetParams(bool isRefresh, params object[] args)
        {
            if (args != null && args.Length > 0)
            {
                _args = args;
            }

            string argTag = "更新 界面参数";
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    argTag += " {" + args[i] + "}";
                }
            }
            
            LogKit.I(argTag);

            if (isRefresh)
            {
                OnRefresh();
            }
        }

        /// <summary>
        /// 退出界面
        /// </summary>
        public virtual void OnExit()
        {
            App.Event.RemoveEventTarget(this);
            _baseLogic?.OnExit();
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }


        #region FindChildComponent

        public Transform FindChild(string path)
        {
            return transform.Find(path);
        }

        public Transform FindChild(Transform trans, string path)
        {
            return trans.Find(path);
        }

        public T FindChildComponent<T>(string path) where T : Component
        {
            return transform.Find(path).GetComponent<T>();
        }

        public T FindChildComponent<T>(Transform trans, string path) where T : Component
        {
            return trans.Find(path).GetComponent<T>();
        }

        #endregion
        
        
        public override void OnNavigationCreate()
        {
            Log.D("OnNavigationCreate");
            Initialize();
        }
        
    }
}