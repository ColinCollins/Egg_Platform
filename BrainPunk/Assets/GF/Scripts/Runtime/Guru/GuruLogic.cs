#nullable enable
using Guru.SDK.Framework.Utils.Controller;

namespace GF
{
    public abstract class GuruLogic
    {
        // 初始化
        public abstract void Initialize();

        public LifecycleController? Controller { get; private set; }

        protected virtual void SetupController()
        {
            Controller = new LifecycleController();
        }

        internal void Setup()
        {
            SetupController();
            Initialize();
        }

        internal void CleanUp()
        {
            
        }
    }
}