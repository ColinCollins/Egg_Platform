// 隔离 Unity 验证替身：只替代共享事件依赖，不替代 Dig 物理、大小、地形或 Tween。
using System;
#region 隔离依赖替身（禁止放入正式工程 Assets）
namespace Bear.EventSystem { public sealed class EventSubscriber { public void Subscribe<T>(Action<T> callback) {} } }
namespace Game.Events { public sealed class SwitchGameStateEvent { public string NewState; } }
namespace Game.Play { public static class GamePlayStateName { public const string PLAYING="Playing"; } }
public static class EventsUtils { public static void ResetEvents(ref Bear.EventSystem.EventSubscriber value) { value=new Bear.EventSystem.EventSubscriber(); } }
#endregion
