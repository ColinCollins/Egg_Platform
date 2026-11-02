using UnityEngine;
using Bear.UI;

namespace Bear.UI.Example
{
    /// <summary>
    /// UI Loader 使用示例
    /// 展示如何注册和使用不同的 UI 加载器
    /// </summary>
    public class UILoaderExample : MonoBehaviour
    {
        private void Start()
        {
            // 示例1：注册单个 Loader（默认优先级 0）
            ResourcesUILoader resourcesLoader = new ResourcesUILoader("UI/");
            UIManager.Instance.RegisterLoader(resourcesLoader);
            
            // 示例2：注册多个 Loader，按优先级顺序尝试加载
            // 优先级数字越小，优先级越高
            ResourcesUILoader primaryLoader = new ResourcesUILoader("UI/Primary/");
            UIManager.Instance.RegisterLoader(primaryLoader, 0); // 最高优先级

            ResourcesUILoader fallbackLoader = new ResourcesUILoader("UI/Fallback/");
            UIManager.Instance.RegisterLoader(fallbackLoader, 10); // 较低优先级，作为备用

            // 现在加载 UI 时，会先尝试 primaryLoader，如果失败则尝试 fallbackLoader
            // var uiView = UIManager.Instance.OpenUI<SampleUIView>("SampleUIView", UILayer.Normal);

            // 示例3：自定义 Loader
            // 可以继承 IUILoader 接口实现自己的加载逻辑
            // 例如：AddressablesLoader、AssetBundleLoader 等
        }

        /// <summary>
        /// 示例：使用路径加载 UI
        /// </summary>
        private void ExampleLoadUIWithPath()
        {
            // 使用已注册的 Loader 加载 UI
            // 路径相对于 Loader 的基础路径
            var uiView = UIManager.Instance.OpenUI<SampleUIView>("SampleUIView", UILayer.Normal);
            
            if (uiView != null)
            {
                Debug.Log("UI loaded successfully");
            }
            else
            {
                Debug.LogError("Failed to load UI");
            }
        }

        /// <summary>
        /// 示例：切换不同的 Loader
        /// </summary>
        private void ExampleSwitchLoader()
        {
            // 注册新的 Loader（会更新已存在的相同实例的优先级）
            ResourcesUILoader newLoader = new ResourcesUILoader("Prefabs/UI/");
            UIManager.Instance.RegisterLoader(newLoader, 5);
            
            Debug.Log("Loader registered with path: Prefabs/UI/");
        }

        /// <summary>
        /// 示例：取消注册 Loader
        /// </summary>
        private void ExampleUnregisterLoader()
        {
            ResourcesUILoader loader = new ResourcesUILoader("UI/");
            UIManager.Instance.RegisterLoader(loader);
            
            // 取消注册
            UIManager.Instance.UnregisterLoader(loader);
            
            Debug.Log("Loader unregistered");
        }
    }
}

