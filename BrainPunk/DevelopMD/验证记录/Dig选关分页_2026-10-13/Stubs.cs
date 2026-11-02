using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Threading.Tasks;using Bear.UI;using Bear.SaveModule;using Bear.Logger;using Config;using UnityEngine;
namespace UnityEngine {
 public class GameObject {public bool Active=true;public void SetActive(bool v){Active=v;}}
 public class RectTransform{}
 public class SerializeField:Attribute{} public class MinAttribute:Attribute{public MinAttribute(int n){}}
 public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);}
 public static class Application{public static bool isPlaying=true;}
}
namespace UnityEngine.Serialization{public class FormerlySerializedAsAttribute:Attribute{public FormerlySerializedAsAttribute(string s){}}}
namespace Bear.Logger{public interface IDebuger{} public static class Logs{
 public static void Log(this IDebuger v,string s){}public static void LogError(this IDebuger v,string s){Trace.Errors.Add(s);}public static void LogWarning(this IDebuger v,string s){Trace.Warnings.Add(s);}
}}
namespace Bear.UI{
 public class BaseUIView {public bool IsOpen;public virtual void OnCreate(){}public virtual void OnOpen(){IsOpen=true;}public virtual void OnClose(){IsOpen=false;}protected T Instantiate<T>(T a,RectTransform p)where T:new()=>new T();}
 public enum UILayer{Normal,Popup}
 public class UIManager{public static UIManager Instance=new();public T OpenUI<T>(string n,UILayer l)where T:new()=>new T();}
}
namespace Config{public class DigLevelSort{public int Id;public string Path;}public class DigLevelData{public string Id;public string Path;}}
public class LevelSort{public int Id;public string Path;}public class LevelData{public string Id;}
namespace Bear.SaveModule{
 public class DigGame{public int CurrentLevel=1,MaxLevel;public int ChoiceLevelPanelPageIndex;public List<int> UnlockLevels=new(),PassedLevels=new();public bool EnsureValidCollections()=>false;}
 public class MainSave{public int CurrentLevel=1,ChioceLevelPanelPageIndex;public void Save(){DB.MainWrites++;DB.MainDiskPage=ChioceLevelPanelPageIndex;}}
}
public static class DB{public static DigGame DigGame=new();public static MainSave GameData=new();public static int DigWrites,MainWrites,DigDiskPage,MainDiskPage;public static bool SaveOk=true;public static bool SaveDigGame(){DigWrites++;if(SaveOk)DigDiskPage=DigGame.ChoiceLevelPanelPageIndex;return SaveOk;}}
public static class Trace{public static List<string> Calls=new(),Errors=new(),Warnings=new();}
public class CustomButton{public GameObject gameObject=new();public event Action<CustomButton> OnClick;public void Click()=>OnClick?.Invoke(this);}
public class RowFirstLayoutGroup{public void RebuildLayout(){}}
public class DigGame_LevelChoiceItem{public DigLevelSort Sort;public void Init(DigGame__ChoiceLevelPanel p){}public void SetData(DigLevelSort s,DigLevelData d){Sort=s;}public void SetCanvasGroupAlpha(float a,float t=0){}}
public class LevelChoicePanel_QuickTurn{public int Page;public bool Enabled=true;public void Initialize(int n,int per,int page,Func<int> get,Action<int> changed,Action<int> selected){Page=page;}public void SetCurrentPage(int p){Page=p;}public void SetInteractable(bool v){Enabled=v;}public void SetCanvasGroupAlpha(float a,float t=0){}}
namespace Guru.SDK.Framework.Utils.Log{public static class Log{public static void I(string s){}}}
public class DigGameLevelCtrl{public bool IsReady=true;public int ConfigGeneration=1;public List<DigLevelSort> LevelSorts=new();public Dictionary<string,DigLevelData> Data=new();public DigLevelData GetLevelDataById(string id)=>Data.GetValueOrDefault(id);
 public bool IsCurrentSelection(int gen,DigLevelSort s,DigLevelData d)=>s!=null&&d!=null&&gen==ConfigGeneration&&LevelSorts.Any(v=>v.Id==s.Id&&v.Path==s.Path)&&ReferenceEquals(GetLevelDataById(d.Id),d);
 public bool TryGetWaitingLevelUnlockPair(out DigLevelSort a,out DigLevelSort b){a=null;b=null;return false;}}
public partial class GameManager{public EntryManager ControllerManager=new();private FocusProxy _digPlayCtrl=new();
 public static GameManager Instance=new();public DigGameLevelCtrl DigGameLevel=new();public DigGame__ChoiceLevelPanel Panel;
 public bool ReturnToStartPanel(out string error){error=null;Trace.Calls.Add("home");Panel?.OnClose();return true;}

 public bool TryEnterDigAtHighestPlayableLevel(out string error){if(!TryEnterDig(out error))return false;Panel.GetType().GetMethod("ShowHighestPlayableLevelPage",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(Panel,null);return true;}
 public bool TryExitDigLevelSelection(out string error)=>ReturnToStartPanel(out error);
 public bool TryEnterDigLevel(DigLevelSort s,DigLevelData d,out string error){error=null;Trace.Calls.Add("enter:"+s.Id);return true;}
}
public class PlayCtrl{public static PlayCtrl Instance=new();public LevelCtrl Level=new();}
public partial class LevelCtrl:IDebuger{public List<LevelSort> levelsort=new();public Dictionary<string,LevelData> leveldatas=new();public LevelData GetLevelDataById(string p)=>leveldatas.GetValueOrDefault(p);}
public class Witness<T>{public static readonly Witness<T> _=new();}
namespace Game.Events{public class EnterLevelEvent{}}
public static class EventStubs{public static void DispatchEvent<T>(this IDebuger v,Witness<T> w,LevelSort s){DB.GameData.CurrentLevel=s.Id;}public static void Forget(this Task t){}}
public static class EventsUtils{public static void ResetEvents(ref object v){v=null;}}
public static class ConfigManager{public static class RemoteConfig{public static bool Open=true;public static bool GetDigGameIsOpen()=>Open;}}
public partial class MainPanel:BaseUIView,IDebuger{
 public List<LevelSort> sortDatas=new();public int MaxCount=10;public int pageIndex;private bool _isPageAnimating;private object _subscriber;private LevelChoicePanel_QuickTurn quickTurnComp=new();
 private void RefreshBtn(){}private void RefreshItems(){}private void UnsubscribeGameCenterAuthState(){}private Task WaitToClose()=>Task.CompletedTask;
 public void Open(){RefreshPageIndex();}public void Drag(int p)=>OnQuickTurnPageChanged(p);
}
public partial class HomeHarness:IDebuger{public void Click()=>Enter(new CustomButton());}
public partial class ExitHarness:IDebuger{
 public bool IsActive=true;private bool _transitioning,_instance,_hud;private object _runtime;private int _levelId=21;
 private DigGame__ChoiceLevelPanel _choice=>GameManager.Instance.Panel;
 public ExitHarness(bool opening,bool running){_transitioning=opening;_runtime=running?new object():null;}
}
public class EntryManager{public bool TryEnter(PlayEntryRequest request,out string error){error=null;Trace.Calls.Add("dig");GameManager.Instance.Panel?.OnOpen();return true;}}
public class FocusProxy{public void FocusHighestPlayableLevel(){var p=GameManager.Instance.Panel;p.GetType().GetMethod("ShowHighestPlayableLevelPage",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(p,null);}}
public enum PlayModeType{Dig}
public class PlayEntryRequest{public PlayEntryRequest(PlayModeType type){}}
namespace Game.ConfigModule{public static class ConfigManager{public static class RemoteConfig{public static bool GetDigGameIsOpen()=>global::ConfigManager.RemoteConfig.Open;}}}