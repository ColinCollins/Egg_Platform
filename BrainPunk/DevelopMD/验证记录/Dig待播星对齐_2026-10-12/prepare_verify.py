from pathlib import Path
import re, subprocess, json, hashlib
r=Path('D:/GitStore/BrinPunk/BrainPunk'); t=Path(__file__).parent; d=t/'managed'; d.mkdir(exist_ok=True)
def read(p): return (r/p).read_text(encoding='utf-8-sig')
def method(s,n):
    match=re.search(r'^[ \t]*(?:private|public|internal|protected) [^\r\n=;(){}]*\b'+re.escape(n)+r'\(',s,re.M)
    if match is None: raise ValueError('Method not found: '+n)
    a=match.start()
    b=s.index('{',s.index(n+'(',a)); i=b+1; depth=1
    while depth: depth+=(s[i]=='{')-(s[i]=='}'); i+=1
    return s[a:i]+'\n'
ctrl=read('Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs'); levels=read('Assets/Game/Scripts/Play/Dig/DigGameLevelCtrl.cs'); panel=read('Assets/Game/UI/DigGame/Scripts/DigGame_GameVictoryPanel.cs'); reward=read('Assets/Game/UI/DigGame/Scripts/DigGame_GameVictoryPanel_Reward.cs'); deep=read('Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs'); draw=read('Assets/Game/Scripts/Play/DrawPlayCtrl.cs'); drawpanel=read('Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs'); gen=read('Assets/BearGame/com.bear.savemodule@fa7a4686436c/Runtime/Editor/PartialClassGenerator.cs'); base=read('Assets/BearGame/com.bear.savemodule@fa7a4686436c/Runtime/Data/BaseSaveDataSO.cs')
for p in ['DigGame.cs','DigGame_Partial.cs','DrawGame.cs','DrawGame_Partial.cs']: (d/p).write_text(read('Assets/Game/DB/'+p),encoding='utf-8')
common='using System;using System.IO;using System.Text;using System.Text.RegularExpressions;using System.Collections.Generic;using Config;using Bear.UI;using Bear.SaveModule;using UnityEngine;\n'
probe=common+'''public class DigPlayCtrl {
 private BaseUIView _victory; private DigLevelData _data; private Runtime _runtime=new();private int _levelId=1,_version; public bool Failed;public BaseUIView Victory=>_victory;
 public void Load(string id,int stars){_data=new(){Id=id};_runtime.InkState.StarCount=stars;}public void Win()=>ShowVictoryPanel();public bool Write(string id,int stars)=>TryRecordVictoryStars(id,stars);
 private void HandleFailure(int v,Exception ex){Failed=true;}
'''+''.join(method(ctrl,n) for n in ['ShowVictoryPanel','TryRecordVictoryStars'])+'''}
public class DrawPlayCtrl { private int _drawLevelDisplayId=1;public bool Write(string id,int stars)=>TryRecordVictoryStars(id,stars);
'''+method(draw,'TryRecordVictoryStars')+'''}
public class DigGameLevelCtrl {public const string TbcDataId="40999";private List<DigLevelSort> _levelSorts=new(){new(){Id=1,Path="40001"},new(){Id=2,Path="40002"}}; public bool IsFirstVictory(int id)=>!DB.DigGame.PassedLevels.Contains(id);
'''+method(levels,'RecordVictory')+'''}
public class DigGame_GameVictoryPanel:BaseUIView {
 private int _collectedStars,_newStars;private bool _isConfigured,_isEntranceAnimationComplete=true,_starsStarted,_isPresentationReady;private int PresentationVersion;public Stars victoryStarGroup=new();private Share sharePart;private bool SupportsSharePart=>true;
 public int High=>_collectedStars;public int Added=>_newStars;public bool Ready=>_isPresentationReady;public void Close(){IsOpen=false;PresentationVersion++;}private void RefreshButtonStates(){}
'''+''.join(method(panel,n) for n in ['Configure','CacheAndConsumeVictoryStars','RefreshPresentation','Create'])+'''}
public class DigGame_GameVictoryPanel_Reward:DigGame_GameVictoryPanel {
'''+method(reward,'Create')+'''}
public class DrawGame_GameVictoryPanel {
 private int _collectedStarCount,_waitingAnimStarCount;public int High=>_collectedStarCount;public int Added=>_waitingAnimStarCount;public void Consume(DrawLevelData d)=>CacheAndConsumeVictoryStars(d);
'''+method(drawpanel,'CacheAndConsumeVictoryStars')+'''}
public class GameVictoryPanel_DeepLink:BaseUIView {
 public Stars victoryStarGroup=new();public bool _isStarPresentationComplete,_isEntranceComplete=true,_isNavigationTriggered;public Block clickBlock=new();public CustomButton homeButton=new(),replayButton=new(),nextButton=new();public CanvasGroup nextButtonGroup=new();
'''+''.join(method(deep,n) for n in ['CreateForDig','OpenPanel','ConfigureDigStars','ConsumeDigWaitingStars','ConfigureDrawStars','ConsumeDrawWaitingStars','CompleteStarPresentation','SetStarAnimationBlocked','RefreshButtonStates'])+'''}
public class GeneratorProbe {public static string Preview(string path)=>GeneratePartialClassCodeStatic(path);
'''+''.join(method(gen,n) for n in ['GeneratePartialClassCodeStatic','CheckInheritsFromBaseSaveDataSOStatic','ExtractClassNameStatic','ExtractNamespaceStatic','ExtractPrivateFieldsStatic','ExtractDefaultValueStatic','ExtractTypeStatic','BuildPartialClassCodeStatic','GetDefaultValueForTypeStatic','ToPascalCaseStatic'])+'''private class FieldInfo{public string Type,Name,DefaultValue;}}
namespace Bear.SaveModule { public class BaseSaveDataSO:UnityEngine.ScriptableObject {public virtual void Init(){}
'''+method(base,'ToJson')+method(base,'FromJson')+'''}}
'''
probe=probe.replace('using Config;','using Config;using Newtonsoft.Json;')
(d/'Probe.cs').write_text(probe,encoding='utf-8')
(d/'Stubs.cs').write_text('''using System;using System.Collections.Generic;using Newtonsoft.Json;
namespace UnityEngine {public class ScriptableObject{}public class SerializeField:Attribute{}public static class Mathf{public static int Clamp(int x,int a,int b)=>Math.Min(b,Math.Max(a,x));public static int Max(int a,int b)=>Math.Max(a,b);}public class Block{public bool Active;public void SetActive(bool v){Active=v;}}public class CanvasGroup{public bool interactable,blocksRaycasts;}}
namespace UnityEditor {} namespace Bear.SaveModule {public enum StorageType{PlayerPrefs}}
namespace Config.Level {public enum GameType{Main,Draw,IWanner,Dig}}
namespace Config {public class DigLevelData{public string Id;}public class DrawLevelData{public string Id;}public class DigLevelSort{public int Id;public string Path;}}
namespace Bear.UI {public class BaseUIView{public bool IsOpen=true;public static implicit operator bool(BaseUIView p)=>p!=null;}public enum UILayer{Popup}public class UIManager{public static UIManager Instance=new();public bool FailOpen,MissingStars,Animate=true;public T OpenUI<T>(string name,UILayer layer) where T:BaseUIView,new(){Trace.Calls.Add("panel:"+name);if(FailOpen)return null;var p=new T();if(p is DigGame_GameVictoryPanel normal){normal.victoryStarGroup=MissingStars?null:new(){Animate=Animate};}if(p is GameVictoryPanel_DeepLink deep){deep.victoryStarGroup=MissingStars?null:new(){Animate=Animate};}return p;}}}
public static class Trace{public static List<string> Calls=new();public static List<string> Warnings=new();public static void Log(this object x,string s){}public static void LogError(this object x,string s){Warnings.Add(s);}public static void LogWarning(this object x,string s){Warnings.Add(s);}}
public class Runtime{public Ink InkState=new();}public class Ink{public int StarCount;}
public class CustomButton{public bool Interactable;}public class Share{public void RefreshDigData(Config.DigLevelData d){}}
public class Stars{public int High,Added,Plays;public bool Animate=true;public Action Done;public static implicit operator bool(Stars s)=>s!=null;public bool Refresh(int h,int a,Action done)=>RefreshStars(h,a,done);public bool RefreshStars(int h,int a,Action done){High=h;Added=a;Done=done;Plays++;return Animate&&a>0;}}
public class OneLinkManager{public static OneLinkManager Instance=new();public bool Dig;public bool IsCurrentEntry(Config.Level.GameType t)=>Dig&&t==Config.Level.GameType.Dig;}
public class GameManager{public static GameManager Instance=new();public DigGameLevelCtrl DigGameLevel=new();}
public static class DB{public static Bear.SaveModule.DigGame DigGame=new();public static Bear.SaveModule.DrawGame DrawGame=new();public static bool SaveOk=true;public static Queue<bool> Results=new();public static string DiskDig,DiskDraw;public static bool SaveDigGame(){Trace.Calls.Add("save:dig");bool ok=Results.Count>0?Results.Dequeue():SaveOk;if(ok)DiskDig=DigGame.ToJson();return ok;}public static bool SaveDrawGame(){Trace.Calls.Add("save:draw");bool ok=Results.Count>0?Results.Dequeue():SaveOk;if(ok)DiskDraw=DrawGame.ToJson();return ok;}}
''',encoding='utf-8')
(d/'Program.cs').write_text('''using System;using System.IO;using System.Linq;using System.Collections.Generic;using Bear.SaveModule;using Bear.UI;using Config;using Newtonsoft.Json;
class Program {
 static int tests;static void Check(bool value,string label){if(!value)throw new Exception(label);tests++;Console.WriteLine("PASS "+label);}static void Reset(){DB.DigGame=new();DB.DigGame.Init();DB.DrawGame=new();DB.DrawGame.Init();DB.SaveOk=true;DB.Results.Clear();DB.DiskDig=DB.DiskDraw=null;Trace.Calls.Clear();Trace.Warnings.Clear();UIManager.Instance=new();OneLinkManager.Instance.Dig=false;}
 static string State(object v)=>JsonConvert.SerializeObject(v);static int Value(Dictionary<string,int> d)=>d.TryGetValue("40001",out int n)?n:0;static DigGame ReadDisk(){var s=new DigGame();s.Init();s.FromJson(DB.DiskDig);return s;}
 static void Main(string[] args){
 Reset();Check(DB.DigGame.LevelWaitingAnimStar!=null&&DB.DigGame.LevelWaitingAnimStar.Count==0,"新档初始化待播集合");
 DB.DigGame.FromJson("{\\"LevelCollectedStar\\":{\\"40001\\":1},\\"PassedLevels\\":[1]}");Check(Value(DB.DigGame.LevelCollectedStar)==1&&DB.DigGame.PassedLevels.Contains(1)&&DB.DigGame.LevelWaitingAnimStar.Count==0,"旧档缺字段保留最高星与通关");
 DB.DigGame.FromJson("{\\"LevelWaitingAnimStar\\":null}");Check(DB.DigGame.EnsureValidCollections()&&DB.DigGame.LevelWaitingAnimStar.Count==0&&!DB.DigGame.EnsureValidCollections(),"旧档null修复且幂等");
 DB.DigGame.LevelWaitingAnimStar["40001"]=2;var roundtrip=new DigGame();roundtrip.Init();roundtrip.FromJson(DB.DigGame.ToJson());Check(Value(roundtrip.LevelCollectedStar)==1&&Value(roundtrip.LevelWaitingAnimStar)==2,"实际JSON公开属性往返");
 var cases=new(int? high,int? waiting,int current,bool ok)[]{(null,null,3,true),(1,null,3,true),(1,1,3,true),(3,null,1,true),(3,2,3,true),(0,null,-1,true),(0,-2,5,true),(1,99,3,true),(null,null,3,false),(1,1,3,false),(3,2,1,false),(8,-2,3,true)};
 foreach(var c in cases){Reset();if(c.high.HasValue){DB.DigGame.LevelCollectedStar["40001"]=c.high.Value;DB.DrawGame.LevelCollectedStar["40001"]=c.high.Value;}if(c.waiting.HasValue){DB.DigGame.LevelWaitingAnimStar["40001"]=c.waiting.Value;DB.DrawGame.LevelWaitingAnimStar["40001"]=c.waiting.Value;}DB.SaveOk=c.ok;bool dig=new DigPlayCtrl().Write("40001",c.current);bool draw=new DrawPlayCtrl().Write("40001",c.current);Check(dig==draw&&State(DB.DigGame.LevelCollectedStar)==State(DB.DrawGame.LevelCollectedStar)&&State(DB.DigGame.LevelWaitingAnimStar)==State(DB.DrawGame.LevelWaitingAnimStar),"与Draw实际写入方法比较 "+c);}
 Reset();DB.SaveOk=false;Check(!new DigPlayCtrl().Write("40001",3)&&!DB.DigGame.LevelCollectedStar.ContainsKey("40001")&&!DB.DigGame.LevelWaitingAnimStar.ContainsKey("40001"),"写入失败恢复不存在条目");
 DB.DigGame.LevelCollectedStar["40001"]=1;DB.DigGame.LevelWaitingAnimStar["40001"]=1;Check(!new DigPlayCtrl().Write("40001",3)&&Value(DB.DigGame.LevelCollectedStar)==1&&Value(DB.DigGame.LevelWaitingAnimStar)==1,"写入失败恢复已有最高与待播");
 Reset();DB.DigGame.LevelWaitingAnimStar=null;Check(new DigPlayCtrl().Write("40001",0)&&ReadDisk().LevelWaitingAnimStar.Count==0,"无新增星时补旧集合并保存");
 Reset();Check(!new DigPlayCtrl().Write("40999",3)&&Trace.Calls.Count==0&&!GameManager.Instance.DigGameLevel.RecordVictory(31,new(){Id="40999"}),"TBC不记录星级或通关");
 foreach(bool link in new[]{false,true})foreach(bool repeat in new[]{false,true}){Reset();OneLinkManager.Instance.Dig=link;if(repeat){DB.DigGame.PassedLevels.Add(1);DB.DigGame.LevelCollectedStar["40001"]=1;}var p=new DigPlayCtrl();p.Load("40001",3);p.Win();int high,added;if(link){var dl=(GameVictoryPanel_DeepLink)p.Victory;high=dl.victoryStarGroup.High;added=dl.victoryStarGroup.Added;Check(!dl._isStarPresentationComplete&&dl.clickBlock.Active&&!dl.nextButton.Interactable,"DeepLink等待星动画完成 "+repeat);dl.victoryStarGroup.Done();Check(dl._isStarPresentationComplete&&!dl.clickBlock.Active&&dl.nextButton.Interactable,"DeepLink动画完成开放导航 "+repeat);}else{var normal=(DigGame_GameVictoryPanel)p.Victory;high=normal.High;added=normal.Added;Check(repeat?p.Victory.GetType()==typeof(DigGame_GameVictoryPanel):p.Victory is DigGame_GameVictoryPanel_Reward,"普通首胜奖励与复胜分流 "+repeat);Check(!normal.Ready,"普通等待星动画完成 "+repeat);normal.victoryStarGroup.Done();Check(normal.Ready,"普通动画完成开放表现 "+repeat);}Check(!p.Failed&&high==3&&added==(repeat?2:3)&&Value(DB.DigGame.LevelCollectedStar)==3&&!DB.DigGame.LevelWaitingAnimStar.ContainsKey("40001"),"首胜复胜普通DeepLink消费 "+link+"/"+repeat);Check(Trace.Calls[0]=="save:dig"&&Trace.Calls[1].StartsWith("panel:")&&Trace.Calls[2]=="save:dig"&&Trace.Calls[3]=="save:dig","星保存→面板消费→进度保存 "+link+"/"+repeat);Check(Value(ReadDisk().LevelCollectedStar)==3&&ReadDisk().LevelWaitingAnimStar.Count==0&&ReadDisk().PassedLevels.Contains(1),"消费与通关最终JSON状态 "+link+"/"+repeat);}
 foreach(bool link in new[]{false,true}){Reset();OneLinkManager.Instance.Dig=link;UIManager.Instance.FailOpen=true;var p=new DigPlayCtrl();p.Load("40001",3);p.Win();var disk=ReadDisk();Check(p.Failed&&Value(disk.LevelCollectedStar)==3&&Value(disk.LevelWaitingAnimStar)==3&&!disk.PassedLevels.Contains(1),"面板创建失败前已持久待播 "+link);DB.DigGame=disk;UIManager.Instance.FailOpen=false;p=new();p.Load("40001",1);p.Win();Check(!p.Failed&&!DB.DigGame.LevelWaitingAnimStar.ContainsKey("40001"),"后续面板读取保留待播而非重新计算 "+link);}
 foreach(bool link in new[]{false,true}){Reset();OneLinkManager.Instance.Dig=link;UIManager.Instance.MissingStars=true;var p=new DigPlayCtrl();p.Load("40001",3);p.Win();Check(!p.Failed&&ReadDisk().LevelWaitingAnimStar.Count==0,"缺星组仍消费与Draw相同 "+link);if(link)Check(((GameVictoryPanel_DeepLink)p.Victory)._isStarPresentationComplete,"DeepLink缺星组解除门禁");else Check(((DigGame_GameVictoryPanel)p.Victory).Ready,"普通缺星组解除门禁");}
 foreach(bool link in new[]{false,true}){Reset();new DigPlayCtrl().Write("40001",3);DB.SaveOk=false;if(link){var p=GameVictoryPanel_DeepLink.CreateForDig(new(){Id="40001"});Check(p.victoryStarGroup.Added==3&&!DB.DigGame.LevelWaitingAnimStar.ContainsKey("40001")&&Value(ReadDisk().LevelWaitingAnimStar)==3&&Trace.Warnings.Count==1,"DeepLink消费失败保留磁盘原记录/内存删除");}else{var p=DigGame_GameVictoryPanel.Create(new(){Id="40001"});Check(p.Added==3&&!DB.DigGame.LevelWaitingAnimStar.ContainsKey("40001")&&Value(ReadDisk().LevelWaitingAnimStar)==3&&Trace.Warnings.Count==1,"普通消费失败保留磁盘原记录/内存删除");}}
 Reset();DB.DigGame.LevelCollectedStar["40001"]=2;DB.DigGame.LevelWaitingAnimStar["40001"]=99;DB.DigGame.LevelWaitingAnimStar["40002"]=1;var normalPanel=DigGame_GameVictoryPanel.Create(new(){Id="40001"});Check(normalPanel.High==2&&normalPanel.Added==2&&DB.DigGame.LevelWaitingAnimStar["40002"]==1,"普通待播钳制并保留其它关卡");normalPanel.Configure(new(){Id="40001"});Check(normalPanel.Added==0&&normalPanel.victoryStarGroup.Plays==1,"普通重复配置不重复播放");var reopened=DigGame_GameVictoryPanel.Create(new(){Id="40001"});Check(reopened.Added==0,"普通重新创建无新增星");
 Reset();DB.DigGame.LevelCollectedStar["40001"]=2;DB.DigGame.LevelWaitingAnimStar["40001"]=-1;var negative=GameVictoryPanel_DeepLink.CreateForDig(new(){Id="40001"});Check(negative.victoryStarGroup.Added==0&&negative._isStarPresentationComplete,"DeepLink负待播钳制无动画");
 Reset();var nullPanel=GameVictoryPanel_DeepLink.CreateForDig(null);Check(nullPanel==null&&Trace.Calls.Count==0,"DeepLink空配置不消费");
 Reset();DB.Results.Enqueue(false);var failedWrite=new DigPlayCtrl();failedWrite.Load("40001",3);failedWrite.Win();Check(Value(DB.DigGame.LevelCollectedStar)==0&&Value(ReadDisk().LevelCollectedStar)==0,"后续RecordVictory不重写失败星级");
 Reset();new DigPlayCtrl().Write("40001",3);var originalDisk=ReadDisk();var animation=DigGame_GameVictoryPanel.Create(new(){Id="40001"});animation.Close();animation.victoryStarGroup.Done();Check(!animation.Ready&&ReadDisk().LevelWaitingAnimStar.Count==0,"动画途中关闭不恢复已消费记录/晚回调拒绝");
 Reset();DB.DrawGame.LevelCollectedStar["40001"]=1;DB.DrawGame.LevelWaitingAnimStar["40001"]=1;string other=DB.DrawGame.ToJson();new DigPlayCtrl().Write("40001",3);GameVictoryPanel_DeepLink.CreateForDig(new(){Id="40001"});Check(DB.DrawGame.ToJson()==other&&DB.DiskDraw==null,"Dig全链不修改Draw存档");
 foreach(var c in new(int high,int? pending)[]{(0,null),(0,3),(1,1),(3,2),(2,99),(2,-1),(8,5)})foreach(bool link in new[]{false,true})foreach(bool ok in new[]{false,true}){Reset();DB.SaveOk=ok;DB.DigGame.LevelCollectedStar["40001"]=DB.DrawGame.LevelCollectedStar["40001"]=c.high;if(c.pending.HasValue)DB.DigGame.LevelWaitingAnimStar["40001"]=DB.DrawGame.LevelWaitingAnimStar["40001"]=c.pending.Value;int dh,da,rh,ra;if(link){var digUi=new GameVictoryPanel_DeepLink();digUi.ConfigureDigStars(new(){Id="40001"});var drawUi=new GameVictoryPanel_DeepLink();drawUi.ConfigureDrawStars(new(){Id="40001"});dh=digUi.victoryStarGroup.High;da=digUi.victoryStarGroup.Added;rh=drawUi.victoryStarGroup.High;ra=drawUi.victoryStarGroup.Added;}else{var digUi=DigGame_GameVictoryPanel.Create(new(){Id="40001"});var drawUi=new DrawGame_GameVictoryPanel();drawUi.Consume(new(){Id="40001"});dh=digUi.High;da=digUi.Added;rh=drawUi.High;ra=drawUi.Added;}Check(dh==rh&&da==ra&&State(DB.DigGame.LevelCollectedStar)==State(DB.DrawGame.LevelCollectedStar)&&State(DB.DigGame.LevelWaitingAnimStar)==State(DB.DrawGame.LevelWaitingAnimStar),"与Draw实际消费方法比较 "+c+"/deep="+link+"/save="+ok);}
 string preview=GeneratorProbe.Preview(args[0]);File.WriteAllText(args[1],preview);string actual=File.ReadAllText(args[2]);var property="public Dictionary<string, int> LevelWaitingAnimStar\\n        {\\n            get => levelWaitingAnimStar;\\n            set => levelWaitingAnimStar = value;\\n        }";Check(preview.Replace("\\r\\n","\\n").Contains(property)&&actual.Replace("\\r\\n","\\n").Contains(property)&&preview.Contains("levelWaitingAnimStar = new Dictionary<string, int>();")&&actual.Contains("levelWaitingAnimStar = new Dictionary<string, int>();"),"实际生成器预览与新增属性/初始化一致");
 Console.WriteLine("TOTAL "+tests+" assertions; actual methods/JSON/generator, UI/storage backend simulated");
 }
}
''',encoding='utf-8')
newtonsoft=next((r/'Library/PackageCache/com.unity.nuget.newtonsoft-json@74deb55db2a0/Runtime').glob('Newtonsoft.Json.dll'))
(d/'Verify.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>{newtonsoft}</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
# 仅规范测试快照的行尾空白，不回写生产源码或改变语句／注释。
for fixture in d.glob('*.cs'):
    fixture.write_text('\n'.join(line.rstrip() for line in fixture.read_text(encoding='utf-8').splitlines()).rstrip()+'\n',encoding='utf-8')
unchanged={}
for path,names in {
 'Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs':['OnResolved','OnNext','OnReplay','OnReset','Tick','ShowExitPopup','CanShowExitPopup','TryBeginRewardInk','TryGrantRewardInk','Deactivate'],
 'Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs':['ConfigureMainStars','ConfigureDrawStars','ConfigureIWannerStars','ConsumeDrawWaitingStars','OnHome','OnReplay','OnNextLevel','ResetOpenState','RefreshButtonStates','CompleteStarPresentation'],
 'Assets/Game/UI/DigGame/Scripts/DigGame_GameVictoryPanel.cs':['OnOpen','OnClose','OnNextLevel','OnReplay','PurchaseNoAd','RefreshButtonStates','OnEntranceAnimationComplete']
}.items():
 before=subprocess.check_output(['git','show','HEAD:BrainPunk/'+path],cwd=r).decode('utf-8-sig'); now=read(path)
 for name in names: unchanged[path+':'+name]=method(before,name)==method(now,name)
for path in ['Assets/Game/Scripts/Play/DrawPlayCtrl.cs','Assets/Game/DB/DrawGame.cs','Assets/Game/DB/DrawGame_Partial.cs','Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs','Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs','Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs']:
 before=subprocess.check_output(['git','show','HEAD:BrainPunk/'+path],cwd=r);unchanged[path]=before.decode('utf-8-sig').replace('\r\n','\n')==read(path).replace('\r\n','\n')
assert all(unchanged.values()),[k for k,v in unchanged.items() if not v]
(t/'static-check.json').write_text(json.dumps({'unchanged_methods_and_files':unchanged,'note':'生成器预览由实际静态纯方法执行；不回写整份产物。生产UI与存储后端未运行。'},ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared actual-method fixtures;',len(unchanged),'unchanged checks')
