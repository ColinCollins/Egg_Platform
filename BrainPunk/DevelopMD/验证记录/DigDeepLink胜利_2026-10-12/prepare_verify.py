from pathlib import Path
import re, json, hashlib
t=Path(__file__).parent;r=Path('D:/GitStore/BrinPunk/BrainPunk');d=t/'managed';d.mkdir(exist_ok=True)
def read(p):return (r/p).read_text(encoding='utf-8-sig')
def method(s,name):
    a=re.search(r'^[ \t]*(?:private|public|internal) [^\r\n=;(){}]*\b'+re.escape(name)+r'\(',s,re.M).start()
    sig=s.index(name+'(',a);b=s.index('{',sig);i=b+1;depth=1
    while depth:
        depth+=(s[i]=='{')-(s[i]=='}');i+=1
    return s[a:i]+'\n'
ctrl=read('Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs');panel=read('Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs');levels=read('Assets/Game/Scripts/Play/Dig/DigGameLevelCtrl.cs');link=read('Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.cs')
(d/'Probe.cs').write_text('''using System;using System.Collections.Generic;using Config;using Config.Level;using Game;using Bear.UI;using UnityEngine;
public class DigPlayCtrl {
 private BaseUIView _victory;private DigLevelData _data;private Runtime _runtime=new();private int _levelId,_version;private bool _resolved=true,_transitioning,_rewardInkGranted=true,_hintGranted=true;public bool IsActive=true;
 public BaseUIView Victory=>_victory;public int Current=>_levelId;public int Starts,Replays,Transitions,Exits;public bool KeepInk=>_rewardInkGranted;public bool KeepHint=>_hintGranted;
 public void Load(int id,int stars){_levelId=id;_data=GameManager.Instance.DigGameLevel.GetLevelDataById(GameManager.Instance.DigGameLevel.GetLevelSortById(id)?.Path);_runtime.InkState.StarCount=stars;}
 public void Win()=>ShowVictoryPanel();public void Next()=>OnNext(new());public void Replay()=>OnReplay(new());
 private void StartNewLevelAnalytics(){Starts++;}private void StartReplayAnalytics(){Replays++;}private void BeginTransition(){_transitioning=true;Transitions++;}private void BeginRebuild(bool _){_transitioning=true;Transitions++;}
 private void ExitToLevelSelection(){Exits++;OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);}
 private void HandleFailure(int version,Exception ex){ExitToLevelSelection();}
'''+''.join(method(ctrl,n) for n in ['ShowVictoryPanel','OnNext','OnReplay'])+'''}
public class GameVictoryPanel_DeepLink:BaseUIView {
 public Stars victoryStarGroup=new();public bool _isStarPresentationComplete,_isEntranceComplete=true,_isNavigationTriggered;public Block clickBlock=new();public CustomButton homeButton=new(),replayButton=new(),nextButton=new();public CanvasGroup nextButtonGroup=new();
 public bool TryNavigate()=>TryBeginNavigation();
'''+''.join(method(panel,n) for n in ['CreateForDig','OpenPanel','CompleteStarPresentation','SetStarAnimationBlocked','TryBeginNavigation','RefreshButtonStates'])+'''}
public class DigGameLevelCtrl {
 public const string TbcDataId="40999";public List<DigLevelSort> _levelSorts=new();public List<DigLevelSort> LevelSorts=>_levelSorts;public Dictionary<string,DigLevelData> Data=new();
 public DigLevelData GetLevelDataById(string id)=>id!=null&&Data.TryGetValue(id,out var data)?data:null;
 public bool IsFirstVictory(int id)=>!DB.DigGame.PassedLevels.Contains(id);
'''+''.join(method(levels,n) for n in ['ResolveLowestUnpassed','GetLevelSortById','RecordVictory'])+'''}
namespace Game {
 public class OneLinkManager {
 public static OneLinkManager Instance=new();public Config.Level.GameType? _entryGameType;public bool _entryBypassesHistory;
 public void Set(Config.Level.GameType type,bool confirmed=true){_entryGameType=type;DB.GameData.IsEnterByOneLink=confirmed;}
 public bool IsCurrentEntry(Config.Level.GameType gameType)=>_entryGameType==gameType&&DB.GameData?.IsEnterByOneLink==true;
'''+method(link,'EndEntry')+'''}
}
''',encoding='utf-8')
(d/'Stubs.cs').write_text('''using System;using System.Collections.Generic;
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
''',encoding='utf-8')
(d/'Program.cs').write_text('''using System;using Config;using Config.Level;using Game;using Bear.UI;
class Program {
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static DigPlayCtrl Setup(bool deep=false,int current=5,int high=2){DB.DigGame=new();DB.GameData=new();DB.DrawGame=new();UIManager.Instance=new();OneLinkManager.Instance=new();GameManager.Instance=new();Trace.Calls.Clear();var levels=GameManager.Instance.DigGameLevel;
 for(int i=1;i<=6;i++){var id=(40000+i).ToString();levels._levelSorts.Add(new(){Id=i,Path=id});levels.Data[id]=new(){Id=id,Path=$"DigLevel_{i:000}"};}levels._levelSorts.Add(new(){Id=7,Path="40999"});levels.Data["40999"]=new(){Id="40999",Path="DigLevel_999"};
 if(deep)OneLinkManager.Instance.Set(GameType.Dig);var c=new DigPlayCtrl();c.Load(current,high);return c;}
 static void Main(){
 var c=Setup();c.Win();Check(c.Victory is DigGame_GameVictoryPanel_Reward,"普通首胜仍奖励面板");
 c=Setup();DB.DigGame.PassedLevels.Add(5);c.Win();Check(c.Victory.GetType()==typeof(DigGame_GameVictoryPanel),"普通复胜仍普通面板");
 c=Setup(true);c.Win();Check(c.Victory is GameVictoryPanel_DeepLink,"DeepLink首胜专属面板");var p=(GameVictoryPanel_DeepLink)c.Victory;
 Check(p.victoryStarGroup.High==2&&p.victoryStarGroup.Added==2,"Dig实际最高/新增星注入");Check(p.clickBlock.Active&&!p.TryNavigate(),"新增星动画拦截输入");p.victoryStarGroup.Done();Check(!p.clickBlock.Active&&p.TryNavigate()&&!p.TryNavigate(),"星动画回调开放且导航去重");
 Check(DB.DigGame.PassedLevels.Contains(5)&&DB.DigGame.LevelCollectedStar["40005"]==2&&Trace.Calls.IndexOf("panel")<Trace.Calls.IndexOf("save"),"创建面板后记录Dig进度与独立星");Check(DB.DrawGame.LevelCollectedStar.Count==0,"不消费Draw星数据");
 c=Setup(true,5,1);DB.DigGame.PassedLevels.Add(5);DB.DigGame.LevelCollectedStar["40005"]=3;c.Win();p=(GameVictoryPanel_DeepLink)c.Victory;Check(p.victoryStarGroup.High==3&&p.victoryStarGroup.Added==0,"DeepLink复胜不降最高星、不重播旧星");Check(p.TryNavigate(),"无新增星不阻断导航");
 c=Setup(true);c.Win();c.Replay();Check(c.Replays==1&&c.Transitions==1&&OneLinkManager.Instance.IsCurrentEntry(GameType.Dig)&&c.KeepHint&&c.KeepInk,"Replay保留来源/提示/加墨资格");
 c=Setup(true);OneLinkManager.Instance.Set(GameType.Dig,false);c.Win();Check(c.Victory is DigGame_GameVictoryPanel_Reward,"尚未确认加载不命中DeepLink");
 c=Setup();OneLinkManager.Instance.Set(GameType.Draw);c.Win();Check(c.Victory is DigGame_GameVictoryPanel_Reward&&OneLinkManager.Instance.IsCurrentEntry(GameType.Draw),"其它玩法来源不污染Dig分支");
 c=Setup(true);DB.DigGame.PassedLevels.Add(1);DB.DigGame.PassedLevels.Add(3);c.Next();Check(c.Current==2&&c.Starts==1&&c.Transitions==1&&!OneLinkManager.Instance.IsCurrentEntry(GameType.Dig)&&!c.KeepHint&&!c.KeepInk,"DeepLink Next最低未通过、启动一次并清来源/资格");c.Next();Check(c.Starts==1,"转场中连续Next不重复启动");
 c=Setup(true);for(int i=1;i<=6;i++)DB.DigGame.PassedLevels.Add(i);c.Next();Check(c.Current==7&&c.Transitions==1,"普通全通专属Next进入TBC");
 c=Setup();var levels=GameManager.Instance.DigGameLevel;var item=levels._levelSorts[4];levels._levelSorts.RemoveAt(4);levels._levelSorts.Insert(0,item);c.Next();Check(c.Current==1,"普通Next重排仍按当前实际内容定位");
 c=Setup();levels=GameManager.Instance.DigGameLevel;levels._levelSorts.RemoveAt(4);c.Next();Check(c.Exits==1&&c.Transitions==0,"普通当前内容被移除回选关");
 c=Setup(true);GameManager.Instance.DigGameLevel.Data.Remove("40001");c.Next();Check(c.Exits==1&&!OneLinkManager.Instance.IsCurrentEntry(GameType.Dig)&&c.Starts==0,"专属Next目标数据丢失结束来源并降级");
 c=Setup(true);UIManager.Instance.MissingStars=true;c.Win();p=(GameVictoryPanel_DeepLink)c.Victory;Check(p.TryNavigate()&&DB.DigGame.PassedLevels.Contains(5),"星组缺失不阻断进度与导航");
 c=Setup(true);UIManager.Instance.FailOpen=true;c.Win();Check(c.Exits==1&&!DB.DigGame.PassedLevels.Contains(5)&&!OneLinkManager.Instance.IsCurrentEntry(GameType.Dig),"专属工厂失败沿失败路径、没有伪造通关");
 UIManager.Instance=null;Check(GameVictoryPanel_DeepLink.CreateForDig(3,1)==null,"UIManager缺失工厂返回null");
 Console.WriteLine($"PASS {checks} actual-method assertions; UI/SDK/resources/analytics lifecycle peripherals simulated.");
 }
}
''',encoding='utf-8')
(d/'Verify.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
# Current workspace compile outputs only TEMP, using the actual Bee references.
compile=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012/compile.ps1').read_text(encoding='utf-8-sig').replace('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012',t.as_posix())
(t/'compile.ps1').write_text(compile,encoding='utf-8')
before=(t/'DigPlayCtrl.before.cs').read_text(encoding='utf-8-sig');panel_before=(t/'DeepLinkPanel.before.cs').read_text(encoding='utf-8-sig')
unchanged=[]
for name in ['OnResolved','OnReplay','OnReset','RebuildAsync','Deactivate','BuildLevelAsync','TryEnterLevel']:
 assert method(before,name)==method(ctrl,name),name;unchanged.append(name)
for name in ['CreateForIWanner','ConfigureDrawStars','ConfigureMainStars','ConfigureIWannerStars','OnHome','OnReplay','OnNextLevel']:
 assert method(panel_before,name)==method(panel,name),name;unchanged.append(name)
report={'unchanged_methods':unchanged,'prefab_changed':False,'source_hashes':{f:hashlib.sha256((r/f).read_bytes()).hexdigest() for f in ['Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs','Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs']},'scope':'实际方法抽取，外围模拟；不替代生产UI/Spine/原生广告/真实深链'}
(t/'static-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared actual methods and verified',len(unchanged),'unchanged lifecycle/other-mode methods')
