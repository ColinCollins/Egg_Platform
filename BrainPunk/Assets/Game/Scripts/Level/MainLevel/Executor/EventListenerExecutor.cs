using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

namespace Game.ItemEvent
{
    /// <summary>
    /// 事件监听触发执行器
    /// </summary>
    public class EventListenerExecutor : BaseItemExecutor, IDebuger
    {
        private EventSubscriber _subscriber;

        public EventSubscriber Subscriber => _subscriber;
        
        [SerializeField] private int Id;

        void Awake()
        {
            EventsUtils.ResetEvents(ref _subscriber);
            _subscriber.Subscribe<OnTiggerItemEvent>(OnEventExecute);
        }


        public void OnEventExecute(OnTiggerItemEvent evt)
        {
            if (evt.EventId != Id)
                return;

            this.Log("Accept Event Id: " + evt.EventId);
            Execute();
        }
    }
}
