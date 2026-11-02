using Guru.SDK.Framework.Utils.Controller;
using Guru.SDK.Framework.Utils.Navigation;

namespace GF.Guru
{
    public abstract class LifecycleElement : GuruElement, INavigable
    {
        public abstract RoutePath RoutePath { get; internal set; }

        public abstract LifecycleController Controller { get; }


        public virtual void OnNavigationCreate()
        {
        }

        public virtual void OnNavigationDestroy()
        {
        }

        public virtual void OnNavigationTransitionIn()
        {
        }

        public virtual void OnNavigationTransitionOut()
        {
        }
    }
}