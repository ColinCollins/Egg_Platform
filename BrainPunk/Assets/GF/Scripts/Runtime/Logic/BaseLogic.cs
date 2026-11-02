using GF.Guru;

namespace GF
{
    public abstract class BaseLogic : GuruLogic
    {

        // Start方法执行的第一行调用
        public abstract void OnBeforeStart();

        // Start方法执行的最后一行调用
        public abstract void OnEnter();

        // 打开界面动画完成后调用
        public virtual void OnOpenAnim()
        {
        }

        // 关闭界面动画完成后调用
        public virtual void OnCloseAnim()
        {
        }

        // 已打开的界面刷新
        public abstract void OnRefresh();

        // ui显示时调用
        public virtual void OnEnable()
        {
        }

        // ui隐藏时调用
        public virtual void OnDisable()
        {
        }

        // Update方法执行的最后一行调用
        public virtual void Update()
        {
        }

        // 返回键调用
        public virtual void OnDeviceBack()
        {
        }

        // UIKit中销毁gameObject前调用
        public abstract void OnExit();

        // 销毁gameObject回调
        public virtual void OnDestroy()
        {
            App.Event.RemoveEventTarget(this);
        }
    }
}