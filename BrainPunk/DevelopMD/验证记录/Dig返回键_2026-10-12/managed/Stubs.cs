using System;
namespace UnityEngine {public enum KeyCode{Escape}public static class Input{public static bool Down;public static int Reads;public static bool GetKeyDown(KeyCode _){Reads++;return Down;}}public static class Time{public static float deltaTime=1;}public static class Application{public static int Quits;public static bool WasClosed;public static void Quit(){Quits++;WasClosed=Bear.UI.UIManager.Instance.Popup?.IsOpen==false;}}}
namespace UnityEditor {public static class EditorApplication{public static bool isPlaying=true;}}
namespace Bear.UI {public class BaseUIView{public bool IsOpen=true;public virtual void OnClose(){IsOpen=false;}}public class UIManager{public static UIManager Instance=new();public CommmonPopup Popup;public T GetUI<T>(string _)=>Popup is T p?p:default;public void CloseUI(CommmonPopup p){p.OnClose();}public void DestroyUI(CommmonPopup p){p.OnClose();Popup=null;}}}
namespace I2.Loc {public static class LocalizationManager{public static string GetTranslation(string key)=>key;}}
public class Runtime{public bool IsRuntimeReady=true,IsResultLocked,HasHint=true;}public class Hud{public bool IsRewardAdRequesting;public int Syncs;public void SetDigInteraction(bool _){Syncs++;}}public class Choice{public bool IsRewardAdPending;}public class CustomButton{}
public class GuideFlowManager{public static GuideFlowManager Instance=new();public bool HasActiveFlow;public int PendingCount;}
public class GamePlayPanelSwitchTipsEvent{}public static class Witness<T>{public static object _=new();}
