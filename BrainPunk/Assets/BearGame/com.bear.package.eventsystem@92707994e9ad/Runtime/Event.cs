using System;
using System.Diagnostics;

namespace Bear.EventSystem
{
    public interface IEvent
    {
        void Release();
    }

    /// <summary>
    /// 事件基类
    /// </summary>
    public abstract class Event : IEvent
    {
        public int Timestamp { get; private set; }

        protected void Initialize()
        {
            Timestamp = Environment.TickCount;
        }

        public void Release()
        {
        }

        protected T Return<T>(T arg)
        {
            return arg;
        }
    }

    /// <summary>
    /// 无参事件
    /// </summary>
    public interface IEventBase : IEvent
    {
        void Initialize();
    }

    public interface IReadableEvent : IEvent
    {
    }

    public abstract class EventBase : Event, IEventBase, IReadableEvent
    {
        void IEventBase.Initialize() => base.Initialize();
    }

    /// <summary>
    /// 1参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    public interface IEventBase<in T1> : IEvent
    {
        void Initialize(T1 param1);
    }

    public interface IReadableEvent<out T1> : IEvent
    {
        T1 Field1 { get; }
    }

    public abstract class EventBase<T1> : Event, IEventBase<T1>, IReadableEvent<T1>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;
        T1 IReadableEvent<T1>.Field1 => Field1;

        void IEventBase<T1>.Initialize(T1 param1)
        {
            base.Initialize();
            _field1 = param1;
        }
    }

    /// <summary>
    /// 2参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    public interface IEventBase<in T1, in T2> : IEvent
    {
        void Initialize(T1 param1, T2 param2);
    }

    public interface IReadableEvent<out T1, out T2> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
    }

    public abstract class EventBase<T1, T2> : Event, IEventBase<T1, T2>, IReadableEvent<T1, T2>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        T1 IReadableEvent<T1, T2>.Field1 => Field1;
        T2 IReadableEvent<T1, T2>.Field2 => Field2;

        void IEventBase<T1, T2>.Initialize(T1 param1, T2 param2)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
        }
    }

    /// <summary>
    /// 3参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    public interface IEventBase<in T1, in T2, in T3> : IEvent
    {
        void Initialize(T1 param1, T2 param2, T3 param3);
    }

    public interface IReadableEvent<out T1, out T2, out T3> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
        T3 Field3 { get; }
    }

    public abstract class EventBase<T1, T2, T3> : Event, IEventBase<T1, T2, T3>, IReadableEvent<T1, T2, T3>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        protected T3 Field3 => Return(_field3);
        T3 _field3;

        T1 IReadableEvent<T1, T2, T3>.Field1 => Field1;
        T2 IReadableEvent<T1, T2, T3>.Field2 => Field2;
        T3 IReadableEvent<T1, T2, T3>.Field3 => Field3;

        void IEventBase<T1, T2, T3>.Initialize(T1 param1, T2 param2, T3 param3)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
            _field3 = param3;
        }
    }

    /// <summary>
    /// 4参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <typeparam name="T4"></typeparam>
    public interface IEventBase<in T1, in T2, in T3, in T4> : IEvent
    {
        void Initialize(T1 param1, T2 param2, T3 param3, T4 param4);
    }

    public interface IReadableEvent<out T1, out T2, out T3, out T4> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
        T3 Field3 { get; }
        T4 Field4 { get; }
    }

    public abstract class EventBase<T1, T2, T3, T4> : Event, IEventBase<T1, T2, T3, T4>, IReadableEvent<T1, T2, T3, T4>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        protected T3 Field3 => Return(_field3);
        T3 _field3;

        protected T4 Field4 => Return(_field4);
        T4 _field4;

        T1 IReadableEvent<T1, T2, T3, T4>.Field1 => Field1;
        T2 IReadableEvent<T1, T2, T3, T4>.Field2 => Field2;
        T3 IReadableEvent<T1, T2, T3, T4>.Field3 => Field3;
        T4 IReadableEvent<T1, T2, T3, T4>.Field4 => Field4;

        void IEventBase<T1, T2, T3, T4>.Initialize(T1 param1, T2 param2, T3 param3, T4 param4)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
            _field3 = param3;
            _field4 = param4;
        }
    }

    /// <summary>
    /// 5参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <typeparam name="T4"></typeparam>
    /// <typeparam name="T5"></typeparam>
    public interface IEventBase<in T1, in T2, in T3, in T4, in T5> : IEvent
    {
        void Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5);
    }

    public interface IReadableEvent<out T1, out T2, out T3, out T4, out T5> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
        T3 Field3 { get; }
        T4 Field4 { get; }
        T5 Field5 { get; }
    }

    public abstract class EventBase<T1, T2, T3, T4, T5> :
        Event, IEventBase<T1, T2, T3, T4, T5>, IReadableEvent<T1, T2, T3, T4, T5>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        protected T3 Field3 => Return(_field3);
        T3 _field3;

        protected T4 Field4 => Return(_field4);
        T4 _field4;

        protected T5 Field5 => Return(_field5);
        T5 _field5;

        T1 IReadableEvent<T1, T2, T3, T4, T5>.Field1 => Field1;
        T2 IReadableEvent<T1, T2, T3, T4, T5>.Field2 => Field2;
        T3 IReadableEvent<T1, T2, T3, T4, T5>.Field3 => Field3;
        T4 IReadableEvent<T1, T2, T3, T4, T5>.Field4 => Field4;
        T5 IReadableEvent<T1, T2, T3, T4, T5>.Field5 => Field5;

        void IEventBase<T1, T2, T3, T4, T5>.Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
            _field3 = param3;
            _field4 = param4;
            _field5 = param5;
        }
    }

    /// <summary>
    /// 6参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <typeparam name="T4"></typeparam>
    /// <typeparam name="T5"></typeparam>
    /// <typeparam name="T6"></typeparam>
    public interface IEventBase<in T1, in T2, in T3, in T4, in T5, in T6> : IEvent
    {
        void Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6);
    }

    public interface IReadableEvent<out T1, out T2, out T3, out T4, out T5, out T6> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
        T3 Field3 { get; }
        T4 Field4 { get; }
        T5 Field5 { get; }
        T6 Field6 { get; }
    }

    public abstract class EventBase<T1, T2, T3, T4, T5, T6> :
        Event, IEventBase<T1, T2, T3, T4, T5, T6>, IReadableEvent<T1, T2, T3, T4, T5, T6>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        protected T3 Field3 => Return(_field3);
        T3 _field3;

        protected T4 Field4 => Return(_field4);
        T4 _field4;

        protected T5 Field5 => Return(_field5);
        T5 _field5;

        protected T6 Field6 => Return(_field6);
        T6 _field6;

        T1 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field1 => Field1;
        T2 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field2 => Field2;
        T3 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field3 => Field3;
        T4 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field4 => Field4;
        T5 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field5 => Field5;
        T6 IReadableEvent<T1, T2, T3, T4, T5, T6>.Field6 => Field6;

        void IEventBase<T1, T2, T3, T4, T5, T6>.Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
            _field3 = param3;
            _field4 = param4;
            _field5 = param5;
            _field6 = param6;
        }
    }

    /// <summary>
    /// 7参事件
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <typeparam name="T2"></typeparam>
    /// <typeparam name="T3"></typeparam>
    /// <typeparam name="T4"></typeparam>
    /// <typeparam name="T5"></typeparam>
    /// <typeparam name="T6"></typeparam>
    /// <typeparam name="T7"></typeparam>
    public interface IEventBase<in T1, in T2, in T3, in T4, in T5, in T6, in T7> : IEvent
    {
        void Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7);
    }

    public interface IReadableEvent<out T1, out T2, out T3, out T4, out T5, out T6, out T7> : IEvent
    {
        T1 Field1 { get; }
        T2 Field2 { get; }
        T3 Field3 { get; }
        T4 Field4 { get; }
        T5 Field5 { get; }
        T6 Field6 { get; }
        T7 Field7 { get; }
    }

    public abstract class EventBase<T1, T2, T3, T4, T5, T6, T7> :
        Event, IEventBase<T1, T2, T3, T4, T5, T6, T7>, IReadableEvent<T1, T2, T3, T4, T5, T6, T7>
    {
        protected T1 Field1 => Return(_field1);
        T1 _field1;

        protected T2 Field2 => Return(_field2);
        T2 _field2;

        protected T3 Field3 => Return(_field3);
        T3 _field3;

        protected T4 Field4 => Return(_field4);
        T4 _field4;

        protected T5 Field5 => Return(_field5);
        T5 _field5;

        protected T6 Field6 => Return(_field6);
        T6 _field6;

        protected T7 Field7 => Return(_field7);
        T7 _field7;

        T1 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field1 => Field1;
        T2 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field2 => Field2;
        T3 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field3 => Field3;
        T4 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field4 => Field4;
        T5 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field5 => Field5;
        T6 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field6 => Field6;
        T7 IReadableEvent<T1, T2, T3, T4, T5, T6, T7>.Field7 => Field7;

        void IEventBase<T1, T2, T3, T4, T5, T6, T7>.Initialize(T1 param1, T2 param2, T3 param3, T4 param4, T5 param5, T6 param6, T7 param7)
        {
            base.Initialize();
            _field1 = param1;
            _field2 = param2;
            _field3 = param3;
            _field4 = param4;
            _field5 = param5;
            _field6 = param6;
            _field7 = param7;
        }
    }

    /// <summary>
    /// 用于泛型推导
    /// </summary>
    public static class Witness<T>
        where T : new()
    {
        public static readonly T _ = default;
    }
}