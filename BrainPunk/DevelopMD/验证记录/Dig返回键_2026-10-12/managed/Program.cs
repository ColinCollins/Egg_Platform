using System;using UnityEngine;using Bear.UI;
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
