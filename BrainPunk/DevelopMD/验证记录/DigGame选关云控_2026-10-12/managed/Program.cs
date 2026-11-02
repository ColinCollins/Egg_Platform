using System;using System.Collections.Generic;using System.IO;using System.Linq;using Config;using Game.ConfigModule;using SimpleJSON;
public static class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
 static string Repo="D:/GitStore/BrinPunk/BrainPunk/";
 static string Remote(params int[] ids)=>"{\"enabled\":true,\"level_sort\":["+string.Join(",",ids)+"]}";
 static Dictionary<string,string> Entry(string json)=>new(){{"diggame_config",json}};
 static (string name,Dictionary<string,object> data) Last=>GameSDKService.Instance.Events.Last();
 static int I(Dictionary<string,object> d,string k)=>Convert.ToInt32(d[k]);
 static long L(Dictionary<string,object> d,string k)=>Convert.ToInt64(d[k]);
 public static void Main(){
  ConfigManager.Instance.Tables.TbDigLevelData=new TbDigLevelData(JSON.Parse(File.ReadAllText(Repo+"Assets/Game/Configs/tbdigleveldata.json")));
  ConfigManager.Instance.Tables.TbDigLevelSort=new TbDigLevelSort(JSON.Parse(File.ReadAllText(Repo+"Assets/Game/Configs/tbdiglevelsort.json")));
  ConfigManager.Instance.Tables.TbGlobalConst.DataList.Add(new Config.LubanConfig.GlobalConst(JSON.Parse(File.ReadAllText(Repo+"Assets/Game/Configs/lubanconfig_tbglobalconst.json"))[0]));
  Check(ConfigManager.RemoteConfig.GetDigGameIsOpen()&&ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==60&&ConfigManager.RemoteConfig.GetDigGameGameTipsPopupPassLevelCoinCost()==40,"独立 Global 默认开关/价格");
  Check(ConfigManager.RemoteConfig.GetDigGameInterstitialShowIntervalCount(4)==4&&ConfigManager.RemoteConfig.GetDigGameInterstitialShowIntervalCount(5)==2&&ConfigManager.RemoteConfig.GetDigGameInterstitialShowFailedCount(31)==6,"本地区间含上界且分档");
  ConfigManager.RemoteConfig.Raw["drawgame_config"]="{\"digGame_gameTipsPopupCoinCost\":999}";
  Check(ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==60,"Dig 不读取 Draw key");
  ConfigManager.RemoteConfig.Raw["diggame_config"]="{\"digGameIsOpen\":false,\"digGame_gameTipsPopupCoinCost\":7,\"digGameShowTipsFailCount\":9}";
  Check(!ConfigManager.RemoteConfig.GetDigGameIsOpen()&&ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==7&&ConfigManager.RemoteConfig.GetDigGameShowTipsFailCount()==9&&ConfigManager.RemoteConfig.GetDigGameGameTipsPopupPassLevelCoinCost()==40,"远端优先与单字段本地兜底");
  ConfigManager.RemoteConfig.Raw["diggame_config"]="{\"digGame_gameTipsPopupCoinCost\":\"invalid\",\"digGameShowTipsTimeLimit\":\"bad\"}";
  Check(ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==60&&ConfigManager.RemoteConfig.GetDigGameShowTipsTimeLimit()==20,"类型解析失败本地兜底");
  ConfigManager.RemoteConfig.Raw["diggame_config"]="{\"digGame_gameTipsPopupCoinCost\":0,\"digGame_interstitialCD\":-1,\"digGame_dailyNOAdPopupAutoShowCount\":0}";
  Check(ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==0&&ConfigManager.RemoteConfig.GetDigGameInterstitialCD()==-1&&ConfigManager.RemoteConfig.GetDigGameDailyNOAdPopupAutoShowCount()==0,"沿用 Draw 非正数语义：免费价格/无冷却/关闭自动弹窗");
  ConfigManager.RemoteConfig.Raw["diggame_config"]="{\"digGame_interstitalAdLevelRange\":[4],\"digGame_interstitalAdRangeLevelCount\":[2,3],\"digGame_interstitialShowIntervalCount\":8}";
  Check(ConfigManager.RemoteConfig.GetDigGameInterstitialShowIntervalCount(5)==8,"非法区间优先回退远端单值");
  ConfigManager.RemoteConfig.Raw["diggame_config"]="broken";Check(ConfigManager.RemoteConfig.GetDigGameGameTipsPopupCoinCost()==60,"无效 JSON 本地兜底");
  ConfigManager.RemoteConfig.Raw.Remove("diggame_config");
  DB.DigGame.Init();var ctrl=new DigGameLevelCtrl();GameManager.Instance.DigGameLevel=ctrl;
  Check(ctrl.Init(out _),"实际 Luban 表初始化");Check(ctrl.LevelSorts.Count==31,"30 普通关和末尾 TBC");
  Check(ctrl.LevelSorts.Select(x=>x.Id).SequenceEqual(Enumerable.Range(1,31)),"排序连续且 TBC=31");
  var data=ctrl.GetLevelDataById("40001");var oldSort=ctrl.GetLevelSortById(1);int oldGeneration=ctrl.ConfigGeneration;
  Check(data.MaxInkLength==20&&data.InkStarStage1==0&&data.InkStarStage2==4&&data.InkStarStage3==10,"真实生成墨水 schema");
  Check(ctrl.GetLevelDataById("40999").Path=="DigLevel_999","TBC 真实表资源");
  Check(ctrl.TryGetWaitingLevelUnlockPair(out var before,out var tbc)&&before.Id==30&&tbc.Id==31,"末尾普通关解锁 TBC 条件");
  Check(ctrl.IsCurrentSelection(oldGeneration,oldSort,data),"绑定身份有效");
  foreach(var bad in new[]{"{}broken","{\"enabled\":1}","{\"enabled\":true}",Remote(),Remote(40001,40001),Remote(49999),Remote(40999),"{\"enabled\":true,\"level_sort\":[true]}"})
   Check(!ctrl.TryPrepareRemoteConfig(bad,out _,out _),"拒绝无效远端候选 "+bad);
  Check(ctrl.TryPrepareRemoteConfig(Remote(40030,40001,40999),out var c,out _)&&c.Count==3&&c[2].Path=="40999","远端重排/TBC 末尾只追加一次");
  Check(ctrl.TryPrepareRemoteConfig("{\"enabled\":false,\"level_sort\":null}",out c,out _)&&c.Count==31,"关闭远端排序回退本地");
  Check(ctrl.RecordVictory(1,data,3),"首胜保存独立进度");Check(DB.DigGame.UnlockLevels.Contains(2)&&DB.DigGame.CurrentLevel==2,"顺序解锁下一关");
  Check(!ctrl.RecordVictory(1,data,1)&&DB.DigGame.LevelCollectedStar[data.Id]==3,"重复胜利最高星不降低");
  Check(!ctrl.RecordVictory(31,ctrl.GetLevelDataById("40999"),3)&&!DB.DigGame.PassedLevels.Contains(31),"TBC 不写普通成功或星级");
  Check(ctrl.ResolveLowestUnpassed()==2,"OneLink 首未通过关回退");
  DB.DigGame.LevelSuccessCount=null;DB.DigGame.LevelPlayDuration=null;Check(DB.DigGame.EnsureValidCollections()&&DB.DigGame.LevelSuccessCount.Count==0&&DB.DigGame.PassedLevels.Contains(1),"旧档补统计集合保留进度");
  GameManager.Instance.IsDigRemoteConfigRefreshBlocked=true;
  var updates=Entry(Remote(40002,40001));updates["drawgame_config"]="draw-unrelated";
  Check(!CommitProbe.Commit(updates)&&CommitProbe.HasPendingDig,"广告/暂停/转场延迟 Dig 候选");
  Check(!ConfigManager.RemoteConfig.Raw.ContainsKey("diggame_config")&&ConfigManager.RemoteConfig.Raw["drawgame_config"]=="draw-unrelated","Dig 延迟不阻断其它 key 保存");
  CommitProbe.TryApplyPendingDigConfig(GameManager.Instance);Check(ctrl.ConfigGeneration==1,"门禁未解除不提交");
  GameManager.Instance.IsDigRemoteConfigRefreshBlocked=false;CommitProbe.TryApplyPendingDigConfig(GameManager.Instance);
  Check(ctrl.ConfigGeneration==2&&ctrl.LevelSorts[0].Path=="40002"&&GameManager.Instance.RefreshCount==1,"安全后缓存与排序同轮提交并刷新");
  Check(!ctrl.IsCurrentSelection(oldGeneration,oldSort,data),"旧广告/选关身份被拒绝");
  int generation=ctrl.ConfigGeneration;var raw=ConfigManager.RemoteConfig.Raw["diggame_config"];
  Check(!CommitProbe.Commit(Entry(Remote(49999)))&&ctrl.ConfigGeneration==generation&&ConfigManager.RemoteConfig.Raw["diggame_config"]==raw,"未知 ID 保留旧缓存和序列");
  ConfigManager.RemoteConfig.FailSave=true;Check(!CommitProbe.Commit(Entry(Remote(40030)))&&ctrl.ConfigGeneration==generation&&ConfigManager.RemoteConfig.Raw["diggame_config"]==raw,"落盘失败不切换列表");ConfigManager.RemoteConfig.FailSave=false;
  Check(CommitProbe.Commit(Entry("{\"enabled\":true,\"level_sort\":[40002,40001],\"digGameIsOpen\":false}"))&&ctrl.ConfigGeneration==generation,"同序列参数更新不增加版本");
  GameManager.Instance.IsDigRemoteConfigRefreshBlocked=true;CommitProbe.Commit(Entry(Remote(40030)));
  Check(CommitProbe.HasPendingDig,"最新候选待提交");CommitProbe.Commit(Entry(ConfigManager.RemoteConfig.Raw["diggame_config"]));
  Check(!CommitProbe.HasPendingDig,"新回调等于当前缓存可撤销旧候选");GameManager.Instance.IsDigRemoteConfigRefreshBlocked=false;
  DB.DigGame.Init();DB.GameData.LevelSuccessCount["main"]=2;DB.DrawGame.LevelSuccessCount["draw"]=3;DB.IWannerGame.LevelSuccessCount["iwanner"]=4;
  var play=new DigPlayCtrl();var sdk=GameSDKService.Instance;play.Enter(1,data);
  Check(Last.name=="level_start"&&I(Last.data,"level")==10&&I(Last.data,"play_count")==1&&(string)Last.data["item_category"]=="dig"&&sdk.LastPath=="DigLevel_001","四玩法开始和货币上下文");
  play.TickProbe(1.25f);play.Retry();var fail=sdk.Events[^2];
  Check(fail.name=="level_end"&&(string)fail.data["result"]=="fail"&&I(fail.data,"revive")==1&&L(fail.data,"duration")==1250,"Reset 先 revive 再失败并记录时长");
  Check(Last.name=="level_start"&&I(Last.data,"play_count")==2&&I(Last.data,"level")==10,"Reset 清本次时长再 start");
  play.TickProbe(2.5f);play.Retry();fail=sdk.Events[^2];Check(I(fail.data,"revive")==2&&L(fail.data,"total_duration")==3750,"同关失败累计 revive 与时长");
  // 云控更新后 analytics 仍使用原始数据。
  CommitProbe.Commit(Entry(Remote(40030,40002)));play.TickProbe(3);play.Win();var win=sdk.Events[^2];
  Check(win.name=="level_end"&&I(win.data,"level")==10&&(string)win.data["level_name"]=="DigLevel_001"&&I(win.data,"revive")==2&&L(win.data,"duration")==3000&&L(win.data,"total_duration")==6750,"成功前累计且身份不被重排改写");
  Check(Last.name=="level_end_success_10"&&I(Last.data,"level")==40001&&sdk.BPlay==10&&DB.DigGame.LevelSuccessCount["40001"]==1,"派生事件真实 ID 与 b_play 四玩法累计");
  DB.DigGame.PassedLevels.Add(1);play.Replay();Check(I(Last.data,"play_count")==4&&(string)Last.data["start_type"]=="replay","Replay 增进入次数/类型");
  play.TickProbe(.5f);play.Exit();Check(I(Last.data,"revive")==0&&L(Last.data,"duration")==500&&I(Last.data,"level")==11&&(string)Last.data["result"]=="exit","Replay 清 revive/时长，退出成对上报");
  int eventCount=sdk.Events.Count;play.Exit();Check(sdk.Events.Count==eventCount,"重复退出不重报");
  play.Enter(31,ctrl.GetLevelDataById("40999"));play.TickProbe(1);play.Retry();play.Exit();Check((string)Last.data["level_name"]=="DigLevel_999"&&!DB.DigGame.LevelSuccessCount.ContainsKey("40999"),"TBC 开始/Reset/退出无普通成功");
  Console.WriteLine($"PASS {checks} assertions; actual config/save/analytics and RemoteConfig commit bodies; Unity/SDK/cache backend simulated.");
 }
}
