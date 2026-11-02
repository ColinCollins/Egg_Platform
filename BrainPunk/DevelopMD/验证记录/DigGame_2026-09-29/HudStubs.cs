using System;
using Bear.UI;
using UnityEngine;
// 只用于隔离边界检查；BaseUIView、子类、UniTask、DOTween 使用真实实现。
#region HUD 隔离依赖（禁止用于生产）
public class GamePlayPanel : BaseUIView, Bear.EventSystem.IEventSender
{
 public static int ParentCalls; public int Level;
 public override void OnCreate(){ParentCalls++;}
 public override void OnOpen(){ParentCalls++;base.OnOpen();}
 public override void OnClose(){ParentCalls++;base.OnClose();}
 public void SetData(int value){Level=value;}
 public static GamePlayPanel Create(string name="")=>null;
 private void Update(){ParentCalls++;}
 private void OnDestroy(){ParentCalls++;}
}
namespace Bear.EventSystem {
 public interface IEventSender{}
 public abstract class EventBase<T>{protected T Field1;}
 public class Witness<T>{public static Witness<T> _=new Witness<T>();}
 public static class DispatchTest {
  public static int Count; public static Type EventType;
  public static void DispatchEvent<T>(this IEventSender sender,Witness<T> witness,GameResetType value){Count++;EventType=typeof(T);}
 }
}
public enum GameResetType {Manually,Failed}
namespace Bear.UI {public sealed class UIManager {public static UIManager Instance=new UIManager(); public T OpenUI<T>(string path,UILayer layer) where T:BaseUIView=>null;}}
public sealed class GameManager {public static GameManager Instance=new GameManager();public Manager ControllerManager=new Manager();public sealed class Manager {public Game.Play.IPlayPauseHost ActivePauseHost;}}
public static class GameSettingPopup {public static int OpenCount;public static void Create(bool playing,Game.Play.IPlayPauseHost host){OpenCount++;}}
#endregion
