using System;using System.Collections.Generic;
namespace UnityEngine {public static class Mathf {public static int Clamp(int x,int a,int b)=>Math.Min(b,Math.Max(a,x));public static int Max(int a,int b)=>Math.Max(a,b);}public class Block {public bool Active;public void SetActive(bool b){Active=b;}}public class CanvasGroup {public bool interactable,blocksRaycasts;}}
namespace Config.Level {public enum GameType{Main,Draw,IWanner,Dig}}
namespace Config {public class DigLevelData{public string Id,Path;}public class DigLevelSort{public int Id;public string Path;}}
namespace Bear.UI {public class BaseUIView {public int High,Added;public static implicit operator bool(BaseUIView p)=>p!=null;}public class UIManager {public static UIManager Instance=new();public bool FailOpen;public bool MissingStars;public bool Animate=true;public T OpenUI<T>(string name,UILayer layer)where T:BaseUIView,new(){Trace.Calls.Add("panel");if(FailOpen)return null;var p=new T();if(p is GameVictoryPanel_DeepLink dl){if(MissingStars)dl.victoryStarGroup=null;else dl.victoryStarGroup.Animate=Animate;}return p;}}public enum UILayer{Popup}}
public static class Trace {public static List<string> Calls=new();public static void Log(this object x,string s){}public static void LogError(this object x,string s){}}
public class DigGame_GameVictoryPanel:Bear.UI.BaseUIView {public static DigGame_GameVictoryPanel Create(Config.DigLevelData _){Trace.Calls.Add("panel");return new();}public void ConfigureStars(int high,int added){High=high;Added=added;}}
public class DigGame_GameVictoryPanel_Reward:DigGame_GameVictoryPanel {public static new DigGame_GameVictoryPanel_Reward Create(Config.DigLevelData _){Trace.Calls.Add("panel");return new();}}
public class Stars {public int High,Added;public bool Animate;public Action Done;public bool RefreshStars(int high,int added,Action done){High=high;Added=added;Done=done;return Animate&&added>0;}}
public class Runtime {public Ink InkState=new();}public class Ink {public int StarCount;}public class GameReplayEvent{}public class EnterNextLevelEvent{}public class CustomButton{public bool Interactable;}
public class Save {public HashSet<int> PassedLevels=new();public List<int> UnlockLevels=new();public Dictionary<string,int> LevelCollectedStar=new();public int MaxLevel,CurrentLevel;public bool IsEnterByOneLink;public void EnsureValidCollections(){}}
public static class DB {public static Save DigGame=new(),GameData=new(),DrawGame=new();public static bool SaveDigGame(){Trace.Calls.Add("save");return true;}}
public class GameManager {public static GameManager Instance=new();public DigGameLevelCtrl DigGameLevel=new();}
public static class GuruLog {public static void I(string s){}}
