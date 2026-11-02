using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace Bear.UI
{
    /// <summary>
    /// UI 管理器
    /// 单例模式，管理所有 UI 的打开、关闭、栈管理等
    /// </summary>
    public partial class UIManager : MonoBehaviour
    {
        public Vector2 LayerResolution = new Vector2(1920, 1080);
        public float MaskAlpha = 0.5f;
        public float MaskFadeDuration = 0.2f;
        public CanvasScaler.ScreenMatchMode ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        public float MatchWidthOrHeight = 0.5f;

        private static UIManager _instance;
        public static UIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("UIManager");
                    _instance = go.AddComponent<UIManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Transform _uiRoot;
        private UILayerManager _layerManager;
        private Dictionary<string, BaseUIView> _uiInstances;
        private Dictionary<string, string> _uiKeyToPath; // 存储键到路径的映射，用于卸载
        private Dictionary<UILayer, UIStack> _layerStacks;
        private HashSet<BaseUIView> _closingAndDestroyingViews;
        private UIMask _sharedMask;
        private Dictionary<UILayer, MaskLayerState> _layerMaskStates;
        private bool _initialized = false;

        /// <summary>
        /// 查询 Popup/Top 是否仍有界面或关闭中的遮罩，供首页 OneLink 等待弹窗结束。
        /// 只读取现有 UI 栈，不创建界面、不关闭弹窗；未初始化时按阻塞处理。
        /// </summary>
        public bool HasBlockingPopup
        {
            get
            {
                if (!_initialized || _layerStacks == null)
                    return true;

                if ((_layerStacks.TryGetValue(UILayer.Popup, out UIStack popup) && popup.Count > 0) ||
                    (_layerStacks.TryGetValue(UILayer.Top, out UIStack top) && top.Count > 0))
                    return true;

                return (_closingAndDestroyingViews != null && _closingAndDestroyingViews.Count > 0) ||
                       (_sharedMask != null && _sharedMask.gameObject.activeInHierarchy);
            }
        }

        /// <summary>
        /// 记录各层是否持有遮罩请求；共享遮罩只显示在当前最高有效层，避免跨层重复叠黑。
        /// </summary>
        private struct MaskLayerState
        {
            public bool IsActive;
            public bool IsCloseByMask;
        }

        #region Debug Helpers

        [Conditional("DEBUG_MODE")]
        private static void DebugLog(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        [Conditional("DEBUG_MODE")]
        private static void DebugLogWarning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        [Conditional("DEBUG_MODE")]
        private static void DebugLogError(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        #endregion

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 初始化 UI 系统, 这里可以提取出来，自定义某些 Layer 状态
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            CreateUIRoot();
            _layerManager = new UILayerManager(_uiRoot, LayerResolution, ScreenMatchMode, MatchWidthOrHeight);
            _uiInstances = new Dictionary<string, BaseUIView>();
            _uiKeyToPath = new Dictionary<string, string>();
            _layerStacks = new Dictionary<UILayer, UIStack>();
            _closingAndDestroyingViews = new HashSet<BaseUIView>();
            _layerMaskStates = new Dictionary<UILayer, MaskLayerState>();

            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                _layerStacks[layer] = new UIStack();
                _layerMaskStates[layer] = default;
            }

            // 初始化 Loader
            // InitializeLoaders();

            _initialized = true;
        }

        private void CreateUIRoot()
        {
            GameObject uiRootGo = new GameObject("UIRoot");
            uiRootGo.transform.SetParent(transform, false);
            _uiRoot = uiRootGo.transform;
        }

        /// <summary>
        /// 生成 UI 实例的键（格式：uiTypeName_pathFileName）
        /// </summary>
        /// <param name="uiType">UI 类型</param>
        /// <param name="path">UI 资源路径</param>
        /// <returns>生成的键</returns>
        private string GetUIKey(Type uiType, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return uiType.Name;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            return $"{uiType.Name}_{fileName}";
        }

        /// <summary>
        /// 打开 UI（通过路径加载）
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <param name="path">UI 资源路径</param>
        /// <param name="layer">UI 层级</param>
        /// <param name="isShowMask">是否显示遮罩，默认为 true</param>
        /// <param name="isCloseByMask">点击遮罩是否关闭 UI，默认为 false</param>
        /// <returns>UI 实例</returns>
        public T OpenUI<T>(string path, UILayer layer = UILayer.Normal, bool isShowMask = true, bool isCloseByMask = false) where T : BaseUIView
        {
            DebugLog($"[UIManager] OpenUI (with path) start. Type={typeof(T).Name}, Path={path}, Layer={layer}, ShowMask={isShowMask}, CloseByMask={isCloseByMask}");

            if (!_initialized)
            {
                DebugLog("[UIManager] Not initialized, calling Initialize().");
                Initialize();
            }

            if (string.IsNullOrEmpty(path))
            {
                DebugLogError("[UIManager] OpenUI failed: path is null or empty.");
                return null;
            }

            Type uiType = typeof(T);
            string uiKey = GetUIKey(uiType, path);
            BaseUIView uiView = null;

            if (_uiInstances.TryGetValue(uiKey, out BaseUIView existingUI))
            {
                uiView = existingUI;
                DebugLog($"[UIManager] Reuse existing UI instance. Key={uiKey}");
            }
            else
            {
                // 使用 Loader 加载 UI
                DebugLog($"[UIManager] Loading UI with loaders. Path={path}");
                GameObject uiPrefab = LoadUIWithLoaders(path);
                if (uiPrefab == null)
                {
                    DebugLogError($"[UIManager] LoadUIWithLoaders failed. Path={path}");
                    return null;
                }

                T component = uiPrefab.GetComponent<T>();
                if (component == null)
                {
                    DebugLogError($"[UIManager] Prefab at path '{path}' does not have component of type {uiType.Name}");
                    GameObject.Destroy(uiPrefab);
                    return null;
                }

                uiView = component;
                uiView.SetLayer(layer);
                _uiInstances[uiKey] = uiView;
                _uiKeyToPath[uiKey] = path; // 保存路径映射，用于卸载
                DebugLog($"[UIManager] New UI instance created and registered. Key={uiKey}, Layer={layer}");

                Transform layerRoot = _layerManager.GetLayerRoot(layer);
                if (layerRoot != null)
                {
                    uiPrefab.transform.SetParent(layerRoot, false);
                }

                uiView.OnCreate();
                DebugLog($"[UIManager] OnCreate called. Type={uiType.Name}, Layer={layer}");
            }

            UIStack stack = _layerStacks[layer];
            BaseUIView topUI = stack.Peek();

            // 防护：同一个 UI 已经在当前层级栈顶时，不允许再次打开，避免重复入栈
            if (topUI == uiView)
            {
                DebugLogError($"[UIManager] OpenUI aborted: UI {uiType.Name} is already on top of layer {layer}.");
                return uiView as T;
            }

            if (topUI != null && topUI != uiView)
            {
                DebugLog($"[UIManager] Hiding previous top UI. PrevType={topUI.GetType().Name}, Layer={layer}");
                topUI.OnHide();
            }

            stack.Push(uiView);
            DebugLog($"[UIManager] Pushed UI to layer stack. Type={uiType.Name}, Layer={layer}, StackCount={stack.Count}");
            uiView.OnOpen();
            uiView.OnShow();
            DebugLog($"[UIManager] OnOpen & OnShow completed. Type={uiType.Name}, Layer={layer}");

            if (isShowMask)
            {
                DebugLog($"[UIManager] Request ShowMask. Layer={layer}, CloseByMask={isCloseByMask}");
                ShowMask(layer, isCloseByMask);
            }

            DebugLog($"[UIManager] OpenUI (with path) finished. Type={uiType.Name}, Layer={layer}");
            return uiView as T;
        }

        /// <summary>
        /// 打开 UI（不通过路径，直接创建 GameObject 并添加组件，保持向后兼容）
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <param name="layer">UI 层级</param>
        /// <param name="isShowMask">是否显示遮罩，默认为 true</param>
        /// <param name="isCloseByMask">点击遮罩是否关闭 UI，默认为 false</param>
        /// <returns>UI 实例</returns>
        public T OpenUI<T>(UILayer layer = UILayer.Normal, bool isShowMask = true, bool isCloseByMask = false) where T : BaseUIView
        {
            DebugLog($"[UIManager] OpenUI (no path) start. Type={typeof(T).Name}, Layer={layer}, ShowMask={isShowMask}, CloseByMask={isCloseByMask}");

            if (!_initialized)
            {
                DebugLog("[UIManager] Not initialized, calling Initialize().");
                Initialize();
            }

            Type uiType = typeof(T);
            string uiKey = uiType.Name; // 没有路径时，只使用类型名作为键
            BaseUIView uiView = null;

            if (_uiInstances.TryGetValue(uiKey, out BaseUIView existingUI))
            {
                uiView = existingUI;
                DebugLog($"[UIManager] Reuse existing UI instance (no path). Key={uiKey}");
            }
            else
            {
                DebugLog($"[UIManager] Creating new UI GameObject (no path). Type={uiType.Name}");
                GameObject uiGo = new GameObject(uiType.Name);
                uiView = uiGo.AddComponent<T>();
                uiView.SetLayer(layer);
                _uiInstances[uiKey] = uiView;

                Transform layerRoot = _layerManager.GetLayerRoot(layer);
                if (layerRoot != null)
                {
                    uiGo.transform.SetParent(layerRoot, false);
                }

                uiView.OnCreate();
                DebugLog($"[UIManager] OnCreate called (no path). Type={uiType.Name}, Layer={layer}");
            }

            UIStack stack = _layerStacks[layer];
            BaseUIView topUI = stack.Peek();

            // 防护：同一个 UI 已经在当前层级栈顶时，不允许再次打开，避免重复入栈
            if (topUI == uiView)
            {
                DebugLogError($"[UIManager] OpenUI (no path) aborted: UI {uiType.Name} is already on top of layer {layer}.");
                return uiView as T;
            }

            if (topUI != null && topUI != uiView)
            {
                DebugLog($"[UIManager] Hiding previous top UI (no path). PrevType={topUI.GetType().Name}, Layer={layer}");
                topUI.OnHide();
            }

            stack.Push(uiView);
            DebugLog($"[UIManager] Pushed UI to layer stack (no path). Type={uiType.Name}, Layer={layer}, StackCount={stack.Count}");
            uiView.OnOpen();
            uiView.OnShow();
            DebugLog($"[UIManager] OnOpen & OnShow completed (no path). Type={uiType.Name}, Layer={layer}");

            if (isShowMask)
            {
                DebugLog($"[UIManager] Request ShowMask (no path). Layer={layer}, CloseByMask={isCloseByMask}");
                ShowMask(layer, isCloseByMask);
            }

            DebugLog($"[UIManager] OpenUI (no path) finished. Type={uiType.Name}, Layer={layer}");
            return uiView as T;
        }

        /// <summary>
        /// 关闭 UI
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        public void CloseUI<T>() where T : BaseUIView
        {
            Type uiType = typeof(T);
            // 尝试查找所有匹配的 UI 实例（可能通过不同路径加载）
            List<BaseUIView> matchingViews = new List<BaseUIView>();
            foreach (var kvp in _uiInstances)
            {
                if (kvp.Key.StartsWith(uiType.Name + "_") || kvp.Key == uiType.Name)
                {
                    if (kvp.Value is T)
                    {
                        matchingViews.Add(kvp.Value);
                    }
                }
            }

            // 关闭第一个匹配的 UI（通常只有一个）
            if (matchingViews.Count > 0)
            {
                CloseUI(matchingViews[0]);
            }
        }

        /// <summary>
        /// 关闭指定 UI
        /// </summary>
        /// <param name="uiView">UI 实例</param>
        public void CloseUI(BaseUIView uiView)
        {
            if (uiView == null)
            {
                DebugLogWarning("[UIManager] CloseUI called with null uiView, ignored.");
                return;
            }

            UILayer layer = uiView.Layer;
            DebugLog($"[UIManager] CloseUI start. Type={uiView.GetType().Name}, Layer={layer}");
            UIStack stack = _layerStacks[layer];

            if (stack.Peek() == uiView)
            {
                stack.Pop();
                DebugLog($"[UIManager] UI popped from layer stack. Type={uiView.GetType().Name}, Layer={layer}, StackCount={stack.Count}");
                uiView.OnHide();
                uiView.OnClose();
                DebugLog($"[UIManager] OnHide & OnClose called. Type={uiView.GetType().Name}, Layer={layer}");

                BaseUIView nextUI = stack.Peek();

                if (nextUI != null)
                {
                    DebugLog($"[UIManager] Next UI on stack will be shown. Type={nextUI.GetType().Name}, Layer={layer}");
                    nextUI.OnShow();
                }
                else
                {
                    DebugLog($"[UIManager] No more UI on layer {layer}, hiding mask.");
                    HideMask(layer);
                }
            }
            else
            {
                DebugLogWarning($"[UIManager] CloseUI aborted: UI {uiView.GetType().Name} is not on top of stack for layer {layer}.");
            }

            DebugLog($"[UIManager] CloseUI finished. Type={uiView.GetType().Name}, Layer={layer}");
        }

        /// <summary>
        /// 关闭栈顶 UI
        /// </summary>
        /// <param name="layer">层级</param>
        public void CloseTopUI(UILayer layer = UILayer.Normal)
        {
            UIStack stack = _layerStacks[layer];
            BaseUIView topUI = stack.Pop();
            if (topUI != null)
            {
                topUI.OnHide();
                topUI.OnClose();

                BaseUIView nextUI = stack.Peek();
                if (nextUI != null)
                {
                    nextUI.OnShow();
                }
                else
                {
                    HideMask(layer);
                }
            }
        }

        /// <summary>
        /// 关闭指定层级的所有 UI
        /// </summary>
        /// <param name="layer">层级</param>
        public void CloseAllUI(UILayer layer)
        {
            if (_layerStacks.TryGetValue(layer, out UIStack stack))
            {
                while (stack.Count > 0)
                {
                    BaseUIView uiView = stack.Pop();
                    uiView.OnHide();
                    uiView.OnClose();
                }
                HideMask(layer);
            }
        }

        /// <summary>
        /// 关闭所有 UI
        /// </summary>
        public void CloseAllUI()
        {
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                CloseAllUI(layer);
            }
        }

        /// <summary>
        /// 获取 UI 实例
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <returns>UI 实例</returns>
        public T GetUI<T>() where T : BaseUIView
        {
            Type uiType = typeof(T);
            // 尝试查找所有匹配的 UI 实例（可能通过不同路径加载）
            foreach (var kvp in _uiInstances)
            {
                if (kvp.Key.StartsWith(uiType.Name + "_") || kvp.Key == uiType.Name)
                {
                    if (kvp.Value is T)
                    {
                        return kvp.Value as T;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 获取 UI 实例（通过路径）
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <param name="path">UI 资源路径</param>
        /// <returns>UI 实例</returns>
        public T GetUI<T>(string path) where T : BaseUIView
        {
            Type uiType = typeof(T);
            string uiKey = GetUIKey(uiType, path);
            if (_uiInstances.TryGetValue(uiKey, out BaseUIView uiView))
            {
                return uiView as T;
            }
            return null;
        }

        /// <summary>
        /// 销毁指定 UI 实例
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        public void DestroyUI<T>() where T : BaseUIView
        {
            Type uiType = typeof(T);
            // 尝试查找所有匹配的 UI 实例（可能通过不同路径加载）
            List<BaseUIView> matchingViews = new List<BaseUIView>();
            foreach (var kvp in _uiInstances)
            {
                if (kvp.Key.StartsWith(uiType.Name + "_") || kvp.Key == uiType.Name)
                {
                    if (kvp.Value is T)
                    {
                        matchingViews.Add(kvp.Value);
                    }
                }
            }

            // 销毁第一个匹配的 UI（通常只有一个）
            if (matchingViews.Count > 0)
            {
                DestroyUI(matchingViews[0]);
            }
        }

        /// <summary>
        /// 销毁指定 UI 实例（通过路径）
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <param name="path">UI 资源路径</param>
        public void DestroyUI<T>(string path) where T : BaseUIView
        {
            Type uiType = typeof(T);
            string uiKey = GetUIKey(uiType, path);
            if (_uiInstances.TryGetValue(uiKey, out BaseUIView uiView))
            {
                DestroyUI(uiView);
            }
        }

        /// <summary>
        /// 播放指定 UI 的关闭动画，并在动画全部完成后销毁实例。
        /// 调用边界：栈顶 UI 会保留栈与遮罩直到动画结束；非栈顶实例无需展示关闭过程，将直接销毁。
        /// 重复调用会被忽略，现有 DestroyUI 的立即销毁语义保持不变。
        /// </summary>
        /// <param name="uiView">需要关闭并销毁的 UI 实例。</param>
        public void CloseAndDestroyUI(BaseUIView uiView)
        {
            if (uiView == null)
            {
                DebugLogWarning("[UIManager] CloseAndDestroyUI called with null uiView, ignored.");
                return;
            }

            UILayer layer = uiView.Layer;
            if (!_layerStacks.TryGetValue(layer, out UIStack stack) || stack.Peek() != uiView)
            {
                DebugLogWarning($"[UIManager] CloseAndDestroyUI found a non-top UI. Destroying immediately. Type={uiView.GetType().Name}, Layer={layer}");
                DestroyUI(uiView);
                return;
            }

            if (!_closingAndDestroyingViews.Add(uiView))
            {
                DebugLog($"[UIManager] CloseAndDestroyUI ignored duplicate request. Type={uiView.GetType().Name}, Layer={layer}");
                return;
            }

            DebugLog($"[UIManager] CloseAndDestroyUI waiting for hide animations. Type={uiView.GetType().Name}, Layer={layer}");
            uiView.HideWithCompletion(() => CompleteCloseAndDestroyUI(uiView));
        }

        private void CompleteCloseAndDestroyUI(BaseUIView uiView)
        {
            if (!_closingAndDestroyingViews.Remove(uiView) || uiView == null)
            {
                return;
            }

            DestroyUI(uiView);
        }

        /// <summary>
        /// 销毁指定 UI 实例
        /// </summary>
        /// <param name="uiView">UI 实例</param>
        public void DestroyUI(BaseUIView uiView)
        {
            if (uiView == null)
            {
                return;
            }

            _closingAndDestroyingViews?.Remove(uiView);

            Type uiType = uiView.GetType();

            // 从栈中移除
            UILayer layer = uiView.Layer;
            if (_layerStacks.TryGetValue(layer, out UIStack stack))
            {
                stack.Remove(uiView);
                
                // 如果移除的是栈顶 UI，需要处理下一个 UI
                BaseUIView topUI = stack.Peek();
                if (topUI != null)
                {
                    topUI.OnShow();
                }
                else
                {
                    HideMask(layer);
                }
            }

            // 调用销毁生命周期方法
            uiView.OnDestroyView();

            // 查找并移除对应的键，同时获取路径信息用于卸载
            string keyToRemove = null;
            foreach (var kvp in _uiInstances)
            {
                if (kvp.Value == uiView)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            // 调用 Loader 的 Unload 方法（如果有路径信息）
            if (keyToRemove != null && _uiKeyToPath.TryGetValue(keyToRemove, out string path) && !string.IsNullOrEmpty(path))
            {
                UnloadUIWithLoaders(path);
            }

            // 从字典中移除
            if (keyToRemove != null)
            {
                _uiInstances.Remove(keyToRemove);
                _uiKeyToPath.Remove(keyToRemove);
            }

            // 销毁 GameObject
            if (uiView != null && uiView.gameObject != null)
            {
                GameObject.Destroy(uiView.gameObject);
            }
        }

        /// <summary>
        /// 销毁所有 UI 实例
        /// </summary>
        public void DestroyAllUI()
        {
            // 创建副本列表，避免在遍历时修改字典
            List<BaseUIView> uiViews = new List<BaseUIView>(_uiInstances.Values);

            foreach (BaseUIView uiView in uiViews)
            {
                if (uiView != null)
                {
                    DestroyUI(uiView);
                }
            }

            // 清空所有栈
            foreach (UIStack stack in _layerStacks.Values)
            {
                stack.Clear();
            }

            // 隐藏所有遮罩
            foreach (UILayer layer in System.Enum.GetValues(typeof(UILayer)))
            {
                HideMask(layer);
            }
        }

        /// <summary>
        /// 释放 UIManager 单例实例
        /// </summary>
        public void ReleaseInstance()
        {
            // 销毁所有 UI
            DestroyAllUI();

            // 清理资源
            if (_uiRoot != null)
            {
                GameObject.Destroy(_uiRoot.gameObject);
                _uiRoot = null;
            }

            _layerManager = null;
            _uiInstances?.Clear();
            _uiKeyToPath?.Clear();
            _layerStacks?.Clear();
            _layerMaskStates?.Clear();
            _sharedMask = null;
            _initialized = false;

            // 释放单例引用
            if (_instance == this)
            {
                _instance = null;
            }

            // 销毁 GameObject
            if (gameObject != null)
            {
                GameObject.Destroy(gameObject);
            }
        }

        #region Mask Management

        private void ShowMask(UILayer layer, bool isCloseByMask = false)
        {
            if (!IsMaskSupportedLayer(layer))
            {
                return;
            }

            _layerMaskStates[layer] = new MaskLayerState
            {
                IsActive = true,
                IsCloseByMask = isCloseByMask
            };
            RefreshSharedMask();
        }

        private void HideMask(UILayer layer)
        {
            if (!IsMaskSupportedLayer(layer))
            {
                return;
            }

            _layerMaskStates[layer] = default;
            RefreshSharedMask();
        }

        /// <summary>
        /// Popup 与 Top 共用一张遮罩；Top 优先，关闭后无闪烁地归还给 Popup。
        /// </summary>
        private void RefreshSharedMask()
        {
            if (!TryGetHighestMaskLayer(out UILayer ownerLayer, out MaskLayerState ownerState))
            {
                _sharedMask?.Hide();
                return;
            }

            Transform layerRoot = _layerManager.GetLayerRoot(ownerLayer);
            if (layerRoot == null)
            {
                _sharedMask?.Hide();
                return;
            }

            UIMask mask = GetOrCreateSharedMask(layerRoot);
            if (mask == null)
            {
                return;
            }

            Transform maskTransform = mask.transform;
            if (maskTransform.parent != layerRoot)
            {
                maskTransform.SetParent(layerRoot, false);
            }
            maskTransform.SetAsFirstSibling();

            // 层级切换时复用当前显示进度，不重新触发淡入，避免遮罩交接期间闪烁或叠黑。
            if (!mask.IsVisible)
            {
                mask.Show(Color.black, MaskAlpha);
            }

            if (ownerState.IsCloseByMask)
            {
                mask.SetClickable(true, () => CloseTopUI(ownerLayer));
            }
            else
            {
                mask.SetClickable(true, null);
            }
        }

        private UIMask GetOrCreateSharedMask(Transform layerRoot)
        {
            if (_sharedMask != null)
            {
                return _sharedMask;
            }

            GameObject maskGo = new GameObject("Mask");
            maskGo.transform.SetParent(layerRoot, false);
            RectTransform rectTransform = maskGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            _sharedMask = maskGo.AddComponent<UIMask>();
            _sharedMask.gameObject.SetActive(false);
            return _sharedMask;
        }

        private bool TryGetHighestMaskLayer(out UILayer layer, out MaskLayerState state)
        {
            if (_layerMaskStates.TryGetValue(UILayer.Top, out state) && state.IsActive)
            {
                layer = UILayer.Top;
                return true;
            }

            if (_layerMaskStates.TryGetValue(UILayer.Popup, out state) && state.IsActive)
            {
                layer = UILayer.Popup;
                return true;
            }

            layer = default;
            state = default;
            return false;
        }

        private static bool IsMaskSupportedLayer(UILayer layer)
        {
            return layer == UILayer.Popup || layer == UILayer.Top;
        }

        #endregion
    }
}

