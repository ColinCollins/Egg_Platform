from pathlib import Path
import re,json,hashlib
t=Path(__file__).parent;r=Path('D:/GitStore/BrinPunk/BrainPunk');d=t/'managed';d.mkdir(exist_ok=True)
def read(p):return (r/p).read_text(encoding='utf-8-sig')
def method(s,n):
 a=re.search(r'^[ \t]*(?:private|public|internal) [^\r\n=;(){}]*\b'+re.escape(n)+r'\(',s,re.M).start();b=s.index('{',s.index(n+'(',a));depth=1;i=b+1
 while depth:depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[a:i]+'\n'
ctrl=read('Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs');common=read('Assets/Game/UI/CommonPopup/Scripts/CommmonPopup.cs');before=(t/'DigPlayCtrl.before.cs').read_text(encoding='utf-8-sig')
unchanged=[]
for n in ['OnResolved','ShowVictoryPanel','OnNext','OnReplay','OnReset','RebuildAsync','Deactivate','BuildLevelAsync','TryEnterLevel','Pause','Resume','ExitToLevelSelection']:
 assert method(before,n)==method(ctrl,n),n;unchanged.append(n)
oldtick=method(before,'Tick').replace('if (!IsActive || _paused','if (_paused')
newtick=method(ctrl,'Tick').replace('        if (!IsActive) return;\n','').replace('        if (Input.GetKeyDown(KeyCode.Escape)) ShowExitPopup();\n','')
assert oldtick==newtick,'original HUD/statistics/tips body changed'
(d/'Probe.cs').write_text('''using System;using UnityEngine;using Bear.UI;
public class DigPlayCtrl {
 public bool IsActive=true,_paused,_transitioning,_resolved;public Runtime _runtime=new();public Hud _hud=new();public Choice _choice=new();private bool IsTbc,_hintGranted,_tipsWarningShown;private int _analyticsReviveCount,_showTipsFailCount=5;private float _analyticsCurrentLevelTimeSeconds,_showTipsTimeLimit=20;public int Ticks,Warnings;
 private bool CanInteract()=>IsActive&&!_paused&&!_transitioning&&_runtime!=null&&!_runtime.IsResultLocked;
 private void TickLevelAnalytics(float delta){Ticks++;_analyticsCurrentLevelTimeSeconds+=delta;}private void DispatchEvent(object e,bool v){Warnings++;}
'''+''.join(method(ctrl,n) for n in ['Tick','ShowExitPopup','CanShowExitPopup'])+'''}
public class CommmonPopup:BaseUIView {
 private Action _onYesCallback,_onNoCallback;public static int Opens;public static string Title,Content;
 public static CommmonPopup Create(string title=null,string content=null,Action onYes=null,Action onNo=null){Opens++;Title=title;Content=content;var p=new CommmonPopup{_onYesCallback=onYes,_onNoCallback=onNo};UIManager.Instance.Popup=p;return p;}
 public void Yes()=>OnYesBtnClick(new());public void No()=>OnNoBtnClick(new());public void Close()=>OnCloseBtnClick(new());
'''+''.join(method(common,n) for n in ['OnClose','OnYesBtnClick','OnNoBtnClick','OnCloseBtnClick'])+'''}
''',encoding='utf-8')
(d/'Stubs.cs').write_text('''using System;
namespace UnityEngine {public enum KeyCode{Escape}public static class Input{public static bool Down;public static int Reads;public static bool GetKeyDown(KeyCode _){Reads++;return Down;}}public static class Time{public static float deltaTime=1;}public static class Application{public static int Quits;public static bool WasClosed;public static void Quit(){Quits++;WasClosed=Bear.UI.UIManager.Instance.Popup?.IsOpen==false;}}}
namespace UnityEditor {public static class EditorApplication{public static bool isPlaying=true;}}
namespace Bear.UI {public class BaseUIView{public bool IsOpen=true;public virtual void OnClose(){IsOpen=false;}}public class UIManager{public static UIManager Instance=new();public CommmonPopup Popup;public T GetUI<T>(string _)=>Popup is T p?p:default;public void CloseUI(CommmonPopup p){p.OnClose();}public void DestroyUI(CommmonPopup p){p.OnClose();Popup=null;}}}
namespace I2.Loc {public static class LocalizationManager{public static string GetTranslation(string key)=>key;}}
public class Runtime{public bool IsRuntimeReady=true,IsResultLocked,HasHint=true;}public class Hud{public bool IsRewardAdRequesting;public int Syncs;public void SetDigInteraction(bool _){Syncs++;}}public class Choice{public bool IsRewardAdPending;}public class CustomButton{}
public class GuideFlowManager{public static GuideFlowManager Instance=new();public bool HasActiveFlow;public int PendingCount;}
public class GamePlayPanelSwitchTipsEvent{}public static class Witness<T>{public static object _=new();}
''',encoding='utf-8')
(d/'Program.cs').write_text('''using System;using UnityEngine;using Bear.UI;
class Program {static int count;static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}static DigPlayCtrl Setup(){UIManager.Instance=new();GuideFlowManager.Instance=new();CommmonPopup.Opens=0;Input.Down=true;Input.Reads=0;Application.Quits=0;UnityEditor.EditorApplication.isPlaying=true;return new();}
static void Main(){
 var c=Setup();Input.Down=false;c.Tick();Check(CommmonPopup.Opens==0&&c.Ticks==1&&c._hud.Syncs==1,"无按键保持原HUD/统计推进");
 c=Setup();c.IsActive=false;c.Tick();Check(CommmonPopup.Opens==0&&Input.Reads==0&&c.Ticks==0,"非活动模式不读按键/不开弹窗");
 c=Setup();c._runtime=null;c.Tick();Check(CommmonPopup.Opens==1&&c.Ticks==0,"选关无Runtime仍可退出确认");
 c=Setup();c._paused=true;c.Tick();Check(CommmonPopup.Opens==1&&c.Ticks==0&&c._paused,"暂停允许确认且不推进统计/不改暂停");
 UIManager.Instance.Popup.No();Check(Application.Quits==0&&c._paused&&!UIManager.Instance.Popup.IsOpen,"取消先关闭、不退出且保留暂停状态");
 c=Setup();c.Tick();Check(CommmonPopup.Opens==1&&CommmonPopup.Title=="U_Quit_Title_01"&&CommmonPopup.Content=="U_Quit_Des_01","正常活动使用默认退出文案与弹窗");c.Tick();Check(CommmonPopup.Opens==1,"重复按键不重复打开");UIManager.Instance.Popup.Close();Check(Application.Quits==0&&UIManager.Instance.Popup==null,"关闭按钮只销毁、不退出");c.Tick();Check(CommmonPopup.Opens==2,"关闭后可重新打开");
 UIManager.Instance.Popup.Yes();Check(Application.Quits==1&&Application.WasClosed,"确认先关闭弹窗再退出应用");
#if UNITY_EDITOR
 Check(!UnityEditor.EditorApplication.isPlaying,"Editor确认同时退出PlayMode");
#else
 Check(UnityEditor.EditorApplication.isPlaying,"非Editor没有编辑器退出逻辑");
#endif
 foreach(var state in new[]{"loading","resolved","resultLocked","choiceAd","inkAd","guide","queuedGuide"}){c=Setup();switch(state){case "loading":c._transitioning=true;break;case "resolved":c._resolved=true;break;case "resultLocked":c._runtime.IsResultLocked=true;break;case "choiceAd":c._choice.IsRewardAdPending=true;break;case "inkAd":c._hud.IsRewardAdRequesting=true;c._paused=true;break;case "guide":GuideFlowManager.Instance.HasActiveFlow=true;break;case "queuedGuide":GuideFlowManager.Instance.PendingCount=1;break;}c.Tick();Check(CommmonPopup.Opens==0,"拒绝插队 "+state);}
 c=Setup();c._transitioning=true;c.Tick();c._transitioning=false;c.Tick();Check(CommmonPopup.Opens==1,"转场完成后按键恢复");
 c=Setup();c._choice.IsRewardAdPending=true;c.Tick();c._choice.IsRewardAdPending=false;c.Tick();Check(CommmonPopup.Opens==1,"广告结果收尾后按键恢复");
 c=Setup();UIManager.Instance.Popup=new(){IsOpen=false};c.Tick();Check(CommmonPopup.Opens==1,"已关闭的缓存弹窗允许重新打开");
 c=Setup();c._runtime.HasHint=false;c.Tick();Check(CommmonPopup.Opens==1,"TBC形态无提示Runtime允许确认");
 Console.WriteLine($"PASS {count} actual-method assertions; input/UI/Guide/Quit platform peripherals simulated.");}}
''',encoding='utf-8')
(d/'Verify.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup></Project>',encoding='utf-8')
compile=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012/compile.ps1').read_text(encoding='utf-8-sig').replace('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012',t.as_posix())
(t/'compile.ps1').write_text(compile.rstrip()+'\n',encoding='utf-8')
protected=['Assets/Game/Scripts/Play/DrawPlayCtrl.cs','Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs','Assets/Game/UI/CommonPopup/Scripts/CommmonPopup.cs','Assets/Game/UI/CommonPopup/Resources/CommmonPopup.prefab']
import subprocess
for path in protected:assert subprocess.check_output(['git','diff','--',path],cwd=r)==b'',path
(t/'static-check.json').write_text(json.dumps({'unchanged_dig_methods':unchanged,'original_tick_body_preserved':True,'protected_files':protected,'source_sha256':hashlib.sha256((r/'Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs').read_bytes()).hexdigest(),'scope':'实际方法抽取；UI/Input/Guide/Application.Quit模拟，不执行真实退出'},ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared actual Tick/popup handlers, verified',len(unchanged),'unchanged Dig methods')
