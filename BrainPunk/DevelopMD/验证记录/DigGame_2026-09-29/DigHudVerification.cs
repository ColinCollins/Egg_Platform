using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;

/// <summary>隔离 HUD 回归；不得作为完整主工程 UI/Prefab 或平台测试。</summary>
public sealed class DigHudVerification : MonoBehaviour
{
 #region 检查驱动
 int checks;DigGame_GamePlayPanel hud;CustomButton reset,pause;GameObject block;Image mask;
 sealed class Host:Game.Play.IPlayPauseHost {public int Pauses;public void Pause(){Pauses++;}public void Resume(){}public void ExitToLevelSelection(){}}
 void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;Debug.Log("DIG HUD CHECK: "+name);}
 void Bind(string field,object value)=>typeof(DigGame_GamePlayPanel).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(hud,value);
 IEnumerator Start()
 {
  var flow=Run();while(true){object next;try {if(!flow.MoveNext())break;next=flow.Current;}catch(Exception e){Debug.LogException(e);File.WriteAllText("../hud-results.txt",e.ToString());UnityEditor.EditorApplication.Exit(1);yield break;}yield return next;}
  File.WriteAllText("../hud-results.txt",$"PASS {checks} HUD assertions. Real Dig child/BaseUIView/UniTask/DOTween; parent gameplay, buttons, event dispatcher, settings popup and pause host use stubs. Production prefab only statically checked.");UnityEditor.EditorApplication.Exit(0);
 }
 IEnumerator Run()
 {
  var root=new GameObject("HUD",typeof(RectTransform));hud=root.AddComponent<DigGame_GamePlayPanel>();
  var r=new GameObject("reset");r.transform.SetParent(root.transform);reset=r.AddComponent<CustomButton>();
  var p=new GameObject("pause");p.transform.SetParent(root.transform);pause=p.AddComponent<CustomButton>();
  block=new GameObject("block");block.transform.SetParent(root.transform);
  var m=new GameObject("mask",typeof(RectTransform));m.transform.SetParent(root.transform);mask=m.AddComponent<Image>();
  Bind("digResetButton",reset);Bind("digPauseButton",pause);Bind("digBlackMask",mask);Bind("digClickBlock",block);
  hud.OnCreate();hud.OnOpen();hud.OnShow();yield return null;
  Check(hud.IsOpen&&hud.IsVisible,"继承 BaseUIView 显示契约");Check(GamePlayPanel.ParentCalls==0,"不调用主线父类生命周期或 Update");
  Check(block.activeSelf&&!reset.Interactable&&!pause.Interactable,"打开先锁 HUD");
  bool allowed=true;hud.ConfigureForDig(7,()=>allowed);Check(hud.Level==7,"沿用关卡号 API");
  hud.SetDigInteraction(true);Check(!block.activeSelf&&reset.Interactable&&pause.Interactable,"开放按钮与遮罩同步");
  allowed=false;reset.TestClick();pause.TestClick();Check(DispatchTest.Count==0&&GameSettingPopup.OpenCount==0,"实时门禁拒绝死亡当帧点击");
  allowed=true;reset.TestClick();reset.TestClick();Check(DispatchTest.Count==1&&DispatchTest.EventType==typeof(Game.Events.DigGameResetEvent),"Reset 仅派发一次 Dig 事件");
  Check(block.activeSelf&&!reset.Interactable,"Reset 同步锁定");
  var host=new Host();GameManager.Instance.ControllerManager.ActivePauseHost=host;hud.SetDigInteraction(true);pause.TestClick();pause.TestClick();
  Check(host.Pauses==1&&GameSettingPopup.OpenCount==1,"Pause 使用注入宿主且拒绝重入");
  hud.SetDigBlackMask(true);Check(mask.gameObject.activeSelf&&mask.color.a==1,"立即遮黑");
  var reveal=hud.FadeDigMaskAsync(false);yield return new WaitForSeconds(.25f);reveal.GetAwaiter().GetResult();Check(!mask.gameObject.activeSelf,"揭幕完成隐藏黑幕");
  var cover=hud.FadeDigMaskAsync(true);yield return new WaitForSeconds(.25f);cover.GetAwaiter().GetResult();Check(mask.gameObject.activeSelf&&Mathf.Abs(mask.color.a-1)<.001f,"遮黑完成保持不透明");
  var old=hud.FadeDigMaskAsync(false);hud.OnHide();hud.OnClose();Check(!hud.IsOpen&&!hud.IsVisible,"关闭状态完整");
  hud.OnOpen();hud.OnShow();hud.SetDigBlackMask(true);yield return new WaitForSeconds(.25f);
  bool cancelled=false;try{old.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled&&mask.gameObject.activeSelf&&mask.color.a==1,"旧等待取消不隐藏重开黑幕");
  hud.SetDigInteraction(true);reset.TestClick();Check(DispatchTest.Count==1,"重开未配置时旧门禁已清除");
  hud.ConfigureForDig(1,()=>true);hud.SetDigInteraction(true);reset.TestClick();Check(DispatchTest.Count==2,"重开后不重复绑定按钮");
  hud.OnClose();Check(GamePlayPanel.ParentCalls==0,"关闭不执行主线清理");
  hud.OnOpen();hud.OnShow();var pending=hud.FadeDigMaskAsync(false);Destroy(root);yield return new WaitForSeconds(.25f);
  cancelled=false;try{pending.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
  Check(cancelled&&GamePlayPanel.ParentCalls==0,"直接销毁取消旧任务且隔离父类销毁");
 }
 #endregion
}
