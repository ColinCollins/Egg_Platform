using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DG.Tweening;
using Game.Dig;
using UnityEngine;
using UnityEditor;

// 隔离测试使用真实生产关卡／Actor／Germ／目标脚本、Unity物理与DOTween；地形和UI边界替身。
public sealed class DigVictoryVerification : MonoBehaviour
{
    const string Output="C:/Users/admin/AppData/Local/Temp/brainpunk-dig-victory-conditions-20261012/";
    GameObject live;DigLevelCtrl level;DigActorCtrl actor;DigGermCtrl[] germs;
    readonly List<DigLevelResult> results=new();readonly List<string> checks=new();
    static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    static void Call(object o,string name,params object[] values)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,values);
    void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(name);Debug.Log("VICTORY CHECK "+checks.Count+": "+name);}
    GameObject Node(string name,Transform parent=null){var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);return go;}
    DigActorCtrl Actor(Transform parent,Vector2 position,int index=0){var go=Node("Actor",parent);go.transform.position=position;var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=0;var circle=go.AddComponent<CircleCollider2D>();var a=go.AddComponent<DigActorCtrl>();Set(a,"body",Node("Body",go.transform).transform);Set(a,"physicsBody",rb);Set(a,"circle",circle);Set(a,"index",index);return a;}
    void Fresh(DigVictoryCondition mode=DigVictoryCondition.ConsumeAllGerms,int count=3,float delay=1f,bool nullGerms=false){
        if(live){live.SetActive(false);Destroy(live);}live=Node("Level");level=live.AddComponent<DigLevelCtrl>();actor=Actor(live.transform,new Vector2(-5,0));
        var camera=Node("Camera",live.transform).AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        Set(level,"inputCamera",camera);Set(level,"actor",actor);Set(level,"terrain",live.AddComponent<DigTerrainCtrl>());Set(level,"victoryCondition",mode);Set(level,"victoryDelay",delay);
        germs=new DigGermCtrl[count];for(int i=0;i<count;i++){var go=Node("Germ"+i,live.transform);go.transform.position=new Vector2(20+i,0);var circle=go.AddComponent<CircleCollider2D>();circle.isTrigger=true;var g=go.AddComponent<DigGermCtrl>();Set(g,"body",Node("Body",go.transform).transform);Set(g,"circle",circle);Set(g,"currentSize",5);Set(g,"growthSize",5);germs[i]=g;}
        Set(level,"germs",nullGerms?null:germs);results.Clear();level.Resolved+=r=>results.Add(r);level.InitializeRuntime();level.SetPaused(false);level.SetInputEnabled(true);
    }
    DigTargetTrigger Target(int index=0,bool trigger=true){var go=Node("Target",live.transform);var collider=go.AddComponent<BoxCollider2D>();collider.size=Vector2.one;collider.isTrigger=trigger;var target=go.AddComponent<DigTargetTrigger>();Set(target,"level",level);Set(target,"triggerCollider",collider);Set(target,"index",index);return target;}
    void MoveToTarget(){actor.GetComponent<Rigidbody2D>().position=Vector2.zero;Physics2D.SyncTransforms();Physics2D.Simulate(.02f);}
    IEnumerator Advance(float duration){float until=Time.time+duration;while(Time.time<until)yield return null;}
    IEnumerator Start(){
        Physics2D.simulationMode=SimulationMode2D.Script;Application.targetFrameRate=120;QualitySettings.vSyncCount=0;
        var run=Run();while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception ex){Debug.LogException(ex);File.WriteAllText(Output+"failed.txt",ex.ToString());EditorApplication.Exit(1);yield break;}yield return next;}
        if(live)live.SetActive(false);
        File.WriteAllLines(Output+"playmode-checks.txt",checks);File.WriteAllText(Output+"verified.txt","PASS "+checks.Count+" assertions; real Unity 6000 physics/Trigger/Tween, actual production sources. Terrain/UI/native storage/platform services are not covered.");EditorApplication.Exit(0);
    }
    IEnumerator Run(){
        var defaults=Node("Defaults");var defaultsLevel=defaults.AddComponent<DigLevelCtrl>();Check(Get<float>(defaultsLevel,"victoryDelay")==1f&&Get<DigVictoryCondition>(defaultsLevel,"victoryCondition")==DigVictoryCondition.ConsumeAllGerms,"Unity新组件的真实默认配置");Destroy(defaults);
        Fresh();Check(Get<DigVictoryCondition>(level,"victoryCondition")==DigVictoryCondition.ConsumeAllGerms&&Get<float>(level,"victoryDelay")==1f,"001吞噬默认及一秒延迟");
        Check(actor.Index==0,"Actor默认index与大小隔离");Check(!level.TrySucceed()&&!level.TryReachTarget(actor,0),"吞噬未完成与目标模式隔离");
        var actorAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProbeAssets/DigActor.prefab");Check(actorAsset&&actorAsset.GetComponent<DigActorCtrl>().Index==0,"实际Actor源Prefab导入新增index");
        Fresh(DigVictoryCondition.ReachTarget,0,1f,true);Check(level.IsRuntimeReady&&germs.Length==0&&!level.TrySucceed(),"到达模式允许null／零病菌且不能用吞噬判胜");
        var target=Target(7);MoveToTarget();Check(!level.IsResultLocked&&results.Count==0,"真实Trigger不同index不结束");
        Set(target,"index",0);Physics2D.Simulate(.02f);Check(level.IsResultLocked&&results.Count==0,"真实Stay相同index立即锁定但未提交");
        Check(!actor.GetComponent<Rigidbody2D>().simulated&&!level.TryDigSegment(Vector2.zero,Vector2.one,true),"等待胜利期间冻结主角并关闭挖掘");
        Check(!level.TryReachTarget(actor,0)&&!level.TryCompleteByHint(),"等待期间重复目标及金币跳关不重入");
        yield return Advance(.45f);Check(results.Count==0,"默认一秒不会在半秒提前提交");
        yield return Advance(.7f);Check(results.Count==1&&results[0]==DigLevelResult.Succeeded,"到达模式默认一秒后唯一成功");
        yield return Advance(.1f);Check(results.Count==1,"连续Update不重复提交成功");
        Fresh(DigVictoryCondition.ReachTarget,0,.2f);target=Target();MoveToTarget();yield return Advance(.07f);Check(results.Count==0,"自定义延迟尚未到点");yield return Advance(.2f);Check(results.Count==1,"自定义延迟到点成功");
        Fresh(DigVictoryCondition.ReachTarget,0,0f);Target();MoveToTarget();Check(results.Count==1&&level.IsResultLocked,"零延迟即时成功但仍锁结果");
        Fresh(DigVictoryCondition.ReachTarget,0,.3f);target=Target();actor.Grow(5);MoveToTarget();var tween=Get<Tween>(actor,"_animation");Check(tween!=null&&!tween.IsPlaying(),"达成目标同时暂停未完成成长动画");
        yield return Advance(.08f);float left=Get<float>(level,"_victoryTimeRemaining");level.SetPaused(true);yield return Advance(.35f);Check(results.Count==0&&Get<float>(level,"_victoryTimeRemaining")==left,"手动暂停不推进延迟");
        level.SetPaused(false);Check(!actor.GetComponent<Rigidbody2D>().simulated&&!tween.IsPlaying(),"恢复等待不解冻物理或成长Tween");yield return Advance(.4f);Check(results.Count==1,"暂停恢复后继续剩余延迟");level.SetPaused(false);Check(!tween.IsPlaying(),"成功提交后Resume也不重开成长动画");
        Fresh(DigVictoryCondition.ReachTarget,0,.3f);Target();MoveToTarget();yield return Advance(.06f);left=Get<float>(level,"_victoryTimeRemaining");Call(level,"OnApplicationPause",true);yield return new WaitForSecondsRealtime(.35f);Check(results.Count==0&&Get<float>(level,"_victoryTimeRemaining")==left,"系统后台不推进延迟");Call(level,"OnApplicationPause",false);yield return Advance(.4f);Check(results.Count==1,"后台恢复后成功一次");
        Fresh(DigVictoryCondition.ReachTarget,0,.2f);Target();MoveToTarget();int oldSuccess=0;level.Resolved+=_=>oldSuccess++;var old=level;live.SetActive(false);Call(old,"TickVictoryDelay");yield return Advance(.3f);Check(oldSuccess==0&&!Get<bool>(old,"_victoryStarted"),"禁用取消等待且不补发旧结果");Fresh(DigVictoryCondition.ReachTarget,0,.2f);Target();MoveToTarget();yield return Advance(.3f);Check(oldSuccess==0&&results.Count==1,"重建新实例与旧等待隔离");
        Fresh(DigVictoryCondition.ReachTarget,0,.2f);target=Target();var foreign=Actor(live.transform,Vector2.zero,0);foreign.Initialize(level);foreign.SetFrozen(false);Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Check(!level.IsResultLocked&&!level.TryReachTarget(foreign,0),"相同index但不是本关绑定Actor不能触发");foreign.gameObject.SetActive(false);
        var unknown=Node("OtherBody",live.transform);var unknownBody=unknown.AddComponent<Rigidbody2D>();unknownBody.gravityScale=0;unknown.AddComponent<CircleCollider2D>();Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Check(!level.IsResultLocked,"无Actor组件的刚体不触发");unknown.SetActive(false);
        target.enabled=false;MoveToTarget();Check(!level.IsResultLocked,"禁用目标脚本收到物理回调也不判胜");target.enabled=true;Physics2D.Simulate(.02f);Check(level.IsResultLocked,"启用后Stay重新核验目标");
        Fresh(DigVictoryCondition.ReachTarget,0,0f);target=Target();actor.GetComponent<CircleCollider2D>().enabled=false;var child=Node("ChildCollider",actor.transform).AddComponent<BoxCollider2D>();Check(child.attachedRigidbody==actor.GetComponent<Rigidbody2D>(),"子Collider物理附着到Actor刚体");MoveToTarget();Check(results.Count==1,"真实子Collider通过attachedRigidbody识别Actor");
        Fresh(DigVictoryCondition.ReachTarget,0,.1f);target=Target(0,false);actor.GetComponent<CircleCollider2D>().isTrigger=true;MoveToTarget();Check(!level.IsResultLocked,"目标Collider未勾Trigger不得胜利");
        Fresh(DigVictoryCondition.ReachTarget,0,.1f);target=Target();Set(target,"level",null);MoveToTarget();Check(!level.IsResultLocked,"未绑定所属关卡不得胜利");
        Fresh(DigVictoryCondition.ReachTarget,0,.1f);target=Target();var otherCollider=Node("OtherCollider",live.transform).AddComponent<BoxCollider2D>();otherCollider.isTrigger=true;Set(target,"triggerCollider",otherCollider);MoveToTarget();Check(!level.IsResultLocked,"目标Collider必须绑定脚本同节点");
        Fresh(DigVictoryCondition.ReachTarget,0,0f);Set(actor,"index",-2);Target(-2);MoveToTarget();Check(results.Count==1,"index仅作匹配不擅自限制编号范围");
        Fresh(DigVictoryCondition.ConsumeAllGerms,1,.2f);Target();MoveToTarget();Check(!level.IsResultLocked,"吞噬模式碰到同index目标不提前结束");
        actor.GetComponent<Rigidbody2D>().position=new Vector2(-5,0);level.QueueContact(germs[0]);Call(level,"FixedUpdate");Check(actor.CurrentSize==15&&results.Count==0&&!level.IsResultLocked&&!level.TrySucceed(),"最后病菌认领后仍等待吸入动画");
        yield return Advance(.34f);Check(level.IsResultLocked&&results.Count==0&&Get<bool>(level,"_victoryStarted"),"全部动画结束后吞噬进入同一延迟入口");yield return Advance(.3f);Check(results.Count==1&&results[0]==DigLevelResult.Succeeded,"吞噬模式统一延迟成功");
        Fresh(DigVictoryCondition.ConsumeAllGerms,3,.1f);level.QueueContact(germs[0]);Call(level,"FixedUpdate");yield return Advance(.4f);Check(!level.IsResultLocked&&results.Count==0&&actor.CurrentSize==15,"未吃完全部病菌不能启动胜利等待");
        level.QueueContact(germs[1]);Call(level,"FixedUpdate");yield return Advance(.35f);Check(!level.IsResultLocked&&results.Count==0,"倒数第二只吞噬后不成功");level.QueueContact(germs[2]);Call(level,"FixedUpdate");yield return Advance(.5f);Check(results.Count==1&&actor.CurrentSize==25,"多病菌全部吞噬后成功且成长保持");
        Fresh(DigVictoryCondition.ReachTarget,1,.1f);level.QueueContact(germs[0]);Call(level,"FixedUpdate");yield return Advance(.45f);Check(!level.IsResultLocked&&results.Count==0,"到达模式吃完病菌不提前胜利");Target();MoveToTarget();yield return Advance(.2f);Check(results.Count==1,"到达模式有病菌时仍只由目标胜利");
        Fresh(DigVictoryCondition.ReachTarget,1,.1f);level.QueueContact(germs[0]);Call(level,"FixedUpdate");var eating=Get<Tween>(germs[0],"_animation");Target();MoveToTarget();Check(!eating.IsPlaying()&&level.IsResultLocked,"到达时未完成吞噬动画也冻结");yield return Advance(.2f);Call(level,"OnConsumed",Get<int>(level,"_generation"));Check(results.Count==1,"到达后旧吞噬回调不重复胜利");
        Fresh(DigVictoryCondition.ReachTarget,1,.1f);Set(germs[0],"currentSize",20);germs[0].transform.position=Vector2.zero;Target();MoveToTarget();Call(level,"FixedUpdate");Check(level.IsResultLocked&&Get<bool>(level,"_victoryStarted")&&!germs[0].IsClaimed&&actor.CurrentSize==10,"同物理步目标达成先锁结果并清未处理接触");yield return Advance(.2f);Check(results.Count==1&&results[0]==DigLevelResult.Succeeded,"同物理步目标优先结果不随接触回调顺序改变");
        Fresh(DigVictoryCondition.ConsumeAllGerms,2,.1f);Set(germs[1],"currentSize",20);level.QueueContact(germs[0]);level.QueueContact(germs[1]);Call(level,"FixedUpdate");Check(level.IsResultLocked&&!Get<bool>(level,"_victoryStarted")&&actor.CurrentSize==10&&!germs[0].IsClaimed,"同批次大者优先失败不吞小者");yield return Advance(.6f);Check(results.Count==1&&results[0]==DigLevelResult.Failed,"失败仍按死亡动画提交没有胜利延迟");
        Fresh(DigVictoryCondition.ReachTarget,1,.1f);Set(germs[0],"currentSize",20);level.QueueContact(germs[0]);Call(level,"FixedUpdate");Check(!level.TryReachTarget(actor,0),"失败已锁后目标不可覆盖");yield return Advance(.6f);Check(results.Count==1&&results[0]==DigLevelResult.Failed,"到达模式死亡链仍有效");
        foreach(var mode in new[]{DigVictoryCondition.ConsumeAllGerms,DigVictoryCondition.ReachTarget}){Fresh(mode,1,.2f);Check(level.TryCompleteByHint()&&results.Count==0&&level.IsResultLocked,"金币跳关也进入统一等待 "+mode);yield return Advance(.3f);Check(results.Count==1,"金币跳关统一延迟提交 "+mode);}
        Fresh(DigVictoryCondition.ReachTarget,0,.2f);Target();level.SetPaused(true);Check(!level.TryReachTarget(actor,0),"暂停时到达请求拒绝");level.SetPaused(false);Call(level,"OnApplicationPause",true);Check(!level.TryReachTarget(actor,0),"后台时到达请求拒绝");Call(level,"OnApplicationPause",false);
        // 另建未初始化控制器核验配置错误，不修改生产配置。
        foreach(float bad in new[]{-1f,float.NaN,float.PositiveInfinity}){var invalidGo=Node("Invalid",live.transform);var invalid=invalidGo.AddComponent<DigLevelCtrl>();Set(invalid,"inputCamera",Get<Camera>(level,"inputCamera"));Set(invalid,"actor",actor);Set(invalid,"terrain",Get<DigTerrainCtrl>(level,"terrain"));Set(invalid,"victoryCondition",DigVictoryCondition.ReachTarget);Set(invalid,"victoryDelay",bad);bool rejected=false;try{invalid.InitializeRuntime();}catch(InvalidOperationException){rejected=true;}Check(rejected&&!invalid.IsRuntimeReady,"拒绝非法延迟避免永久锁住 "+bad);}
    }
}
