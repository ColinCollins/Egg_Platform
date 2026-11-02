using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Dig;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 隔离 PlayMode 检查：复制的生产 Dig 脚本未替换算法；共享事件系统仅为编译替身。
public sealed class DigVerification : MonoBehaviour
{
    int checks;
    GameObject prefab,live;
    DigLevelCtrl level;
    DigActorCtrl actor;
    DigGermCtrl[] germs;
    readonly List<DigLevelResult> results=new List<DigLevelResult>();
    static T Field<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
    // 验证实际世界格距，不以 Grid.cellSize 的内部实现方式代替细分效果。
    static float WorldCellWidth(Tilemap map)=>Vector3.Distance(map.GetCellCenterWorld(Vector3Int.zero),map.GetCellCenterWorld(Vector3Int.right));
    void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Debug.Log("DIG CHECK: "+name);}
    void Batch()=>typeof(DigLevelCtrl).GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(level,null);
    void Fresh()
    {
        if(live){live.SetActive(false);Destroy(live);}
        live=Instantiate(prefab);level=live.GetComponent<DigLevelCtrl>();actor=Field<DigActorCtrl>(level,"actor");germs=Field<DigGermCtrl[]>(level,"germs");results.Clear();
        level.Resolved+=r=>results.Add(r);level.InitializeRuntime();level.SetPaused(false);level.SetInputEnabled(true);
    }
    void Contact(params int[] ids){foreach(int id in ids)level.QueueContact(germs[id]);Batch();}
    IEnumerator Start()
    {
        Physics2D.simulationMode=SimulationMode2D.Script;
#if UNITY_EDITOR
        prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/DigLevel/DigLevel_001.prefab");
#endif
        IEnumerator tests=Run();
        while(true)
        {
            object next;
            try{if(!tests.MoveNext())break;next=tests.Current;}
            catch(Exception ex){Debug.LogException(ex);File.WriteAllText("../playmode-failed.txt",ex.ToString());Quit(1);yield break;}
            yield return next;
        }
        File.WriteAllText("../playmode-results.txt",$"PASS {checks} assertions; real Unity PlayMode, physics simulation, Tween, Tilemap and Prefab. Shared event system stubbed; production UI/mobile not covered.");
        Quit(0);
    }
    IEnumerator Run()
    {
        Check(DigSizeRules.CanConsumeBatch(10,new[]{5}),"小于");
        Check(DigSizeRules.CanConsumeBatch(10,new[]{10}),"等于");
        Check(!DigSizeRules.CanConsumeBatch(10,new[]{12}),"大于");
        Check(!DigSizeRules.CanConsumeBatch(10,new[]{5,12})&&!DigSizeRules.CanConsumeBatch(10,new[]{12,5}),"同一步顺序无关失败优先");
        Fresh();Check(actor.CurrentSize==10&&germs.Length==3,"初始配置");
        var terrain=Field<DigTerrainCtrl>(level,"terrain");var map=Field<Tilemap>(terrain,"collisionMap");int initialTiles=map.GetUsedTilesCount();
        Check(!level.TrySucceed(),"未吃完拒绝提前胜利");
        Check(Mathf.Abs(WorldCellWidth(map)-.2f/Field<int>(terrain,"collisionSubdivisions"))<.0001f,"按当前 Prefab 细分参数生成格子");
        Check(Mathf.Abs(terrain.StrokeStep-.075f)<.0001f,"笔画间距 0.075");
        Check(map.HasTile(map.WorldToCell(new Vector2(3.95f,3.95f)))&&!map.HasTile(map.WorldToCell(new Vector2(4.05f,4.05f))),"细分保持原 8x8 世界边界");
        Check(map.HasTile(map.WorldToCell(new Vector2(2.1f,2.1f))),"初始化沙土碰撞");
        terrain.Stamp(new Vector2(2.1f,2.1f));terrain.FlushCollider();Check(!map.HasTile(map.WorldToCell(new Vector2(2.1f,2.1f))),"挖掘删除碰撞格");
        Check(!map.HasTile(map.WorldToCell(new Vector2(2.76f,2.1f)))&&map.HasTile(map.WorldToCell(new Vector2(2.88f,2.1f))),"细轮廓接近可见半径且不超挖");
        Set(terrain,"collisionSubdivisions",2);terrain.Initialize();Check(Mathf.Abs(WorldCellWidth(map)-.1f)<.0001f,"精度参数 2 生效");
        Set(terrain,"collisionSubdivisions",1);terrain.Initialize();Check(Mathf.Abs(WorldCellWidth(map)-.2f)<.0001f,"精度参数 1 恢复原精度");
        Set(terrain,"collisionSubdivisions",4);terrain.Initialize();Check(Mathf.Abs(WorldCellWidth(map)-.05f)<.0001f,"重复初始化不累计缩小地形");
        Set(terrain,"collisionSubdivisions",8);terrain.Initialize();Check(Mathf.Abs(WorldCellWidth(map)-.025f)<.0001f,"精度参数 8 对应 0.025");
        Contact(0,0);Check(actor.CurrentSize==15&&germs[0].IsClaimed,"重复碰撞只增长一次");
        Contact(0);Check(actor.CurrentSize==15,"已吞噬拒绝重复");
        yield return new WaitForSeconds(.35f);
        Check(Mathf.Abs(actor.GetComponent<CircleCollider2D>().radius-.35f*Mathf.Sqrt(1.5f))<.001f,"成长圆碰撞半径映射");
        Check(results.Count==0,"未吃完不胜利");
        Contact(1);yield return new WaitForSeconds(.35f);Check(actor.CurrentSize==20,"第二次成长 20");
        Contact(2);Check(actor.CurrentSize==30&&results.Count==0&&!level.TrySucceed(),"最后吞噬等待动画");
        yield return new WaitForSeconds(.35f);Check(results.Count==1&&results[0]==DigLevelResult.Succeeded,"全部动画完成唯一胜利");
        Check(!level.TrySucceed()&&results.Count==1,"完成后不重复派发胜利");
        level.SetPaused(true);level.SetPaused(false);Check(!actor.GetComponent<Rigidbody2D>().simulated,"胜利后恢复不启动物理");
        Fresh();Contact(0,1);Check(actor.CurrentSize==10&&level.IsResultLocked&&!germs[0].IsClaimed,"混合接触不先吃小病菌");
        Check(results.Count==0,"死亡先表现再提交");
        yield return new WaitForSeconds(.56f);Check(results.Count==1&&results[0]==DigLevelResult.Failed,"失败唯一提交");
        level.SetPaused(false);Check(!actor.GetComponent<Rigidbody2D>().simulated,"死亡不被 Resume 复活");
        Fresh();Contact(1,0);Check(actor.CurrentSize==10&&level.IsResultLocked,"混合接触反序仍失败");
        Fresh();Set(germs[1],"currentSize",8);Contact(0,1);Check(actor.CurrentSize==20&&germs[0].IsClaimed&&germs[1].IsClaimed,"同一步全部可吃累计成长");
        Fresh();Contact(0);yield return new WaitForSeconds(.05f);Contact(2);
        yield return new WaitForSeconds(.6f);Check(actor.CurrentSize==15&&results.Count==1&&results[0]==DigLevelResult.Failed,"吞噬中死亡取消旧回调");
        Fresh();Set(germs[0],"currentSize",10);Contact(0);Check(actor.CurrentSize==15,"相等实际关卡可吞噬");
        Fresh();actor.GetComponent<Rigidbody2D>().linearVelocity=new Vector2(1,-2);level.SetPaused(true);
        Check(!actor.GetComponent<Rigidbody2D>().simulated,"暂停关闭物理");level.SetPaused(false);
        Check(actor.GetComponent<Rigidbody2D>().linearVelocity==new Vector2(1,-2),"恢复保持原速度");
        Contact(0);level.SetPaused(true);yield return new WaitForSeconds(.4f);Check(germs[0].gameObject.activeSelf,"暂停冻结吸入");
        level.SetPaused(false);yield return new WaitForSeconds(.35f);Check(!germs[0].gameObject.activeSelf,"恢复继续吸入");
        Fresh();Contact(0);Contact(1);Contact(2);var oldResults=results.Count;Fresh();yield return new WaitForSeconds(.4f);
        Check(results.Count==0&&actor.CurrentSize==10,"重建隔离旧回调并恢复大小");
        Check(germs[0].CurrentSize==5&&germs[1].CurrentSize==12&&germs[2].CurrentSize==18,"重建恢复病菌值");
        Check(Field<Tilemap>(Field<DigTerrainCtrl>(level,"terrain"),"collisionMap").HasTile(new Vector3Int(10,10,0)),"重建恢复地形");
        // 真实物理回调：主角进入病菌触发器，再批量提交。
        actor.transform.position=germs[0].transform.position;Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Batch();
        Check(actor.CurrentSize==15,"真实 Physics2D 触发器进入结算");
        Fresh();terrain=Field<DigTerrainCtrl>(level,"terrain");
        for(float y=3.5f;y>=-3.4f;y-=.1f)terrain.Stamp(new Vector2(-1.4f,y));terrain.FlushCollider();
        for(int i=0;i<200 && results.Count==0;i++) {Physics2D.Simulate(.02f);Batch();yield return null;}
        yield return new WaitForSeconds(.4f);
        Check(actor.CurrentSize==30&&results.Count==1&&results[0]==DigLevelResult.Succeeded,"实际挖通路线后依靠重力顺序吞噬通关");
        Fresh();Contact(0);Contact(1);Contact(2);
        typeof(DigLevelCtrl).GetMethod("BeginFailure",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(level,null);
        yield return new WaitForSeconds(.6f);
        Check(results.Count==1&&results[0]==DigLevelResult.Failed,"最后吞噬待完成时注入失败，旧吞噬不提交胜利");
        Fresh();Contact(0);
        var applicationPause=typeof(DigLevelCtrl).GetMethod("OnApplicationPause",BindingFlags.NonPublic|BindingFlags.Instance);
        applicationPause.Invoke(level,new object[]{true});yield return new WaitForSeconds(.4f);
        Check(germs[0].gameObject.activeSelf&&!actor.GetComponent<Rigidbody2D>().simulated,"切后台冻结物理和吞噬动画");
        applicationPause.Invoke(level,new object[]{false});yield return new WaitForSeconds(.35f);
        Check(!germs[0].gameObject.activeSelf&&actor.GetComponent<Rigidbody2D>().simulated,"回前台继续局部动画与物理");
        Fresh();Contact(2);applicationPause.Invoke(level,new object[]{true});yield return new WaitForSeconds(.6f);
        Check(results.Count==0,"死亡表现切后台不提前结算");
        applicationPause.Invoke(level,new object[]{false});yield return new WaitForSeconds(.56f);
        Check(results.Count==1&&results[0]==DigLevelResult.Failed&&!actor.GetComponent<Rigidbody2D>().simulated,"死亡回前台只完成失败，不复活");
        Fresh();Set(germs[0],"growthSize",0);Contact(0);
        Check(actor.CurrentSize==10&&germs[0].IsClaimed,"零成长仍可吞噬且不改变大小");
        Fresh();level.SetPaused(true);var cam=Field<Camera>(level,"inputCamera");cam.aspect=16f/9f;
        var rt=new RenderTexture(1280,720,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("../DigLevel_001-preview.png",image.EncodeToPNG());
        cam.targetTexture=null;RenderTexture.active=null;rt.Release();Destroy(rt);Destroy(image);
        yield return null;
    }
    static void Quit(int code)
    {
#if UNITY_EDITOR
        EditorApplication.Exit(code);
#endif
    }
}
