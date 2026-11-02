using System;
using System.Reflection;
using Config;
using Config.Level;
using Game;
using Game.ConfigModule;
using Newtonsoft.Json.Linq;
using SimpleJSON;

public static class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
 static JObject Open(string mode,string target=null)=>new JObject { ["deep_link_value"]="open_level",["deep_link_sub1"]=mode,["deep_link_sub2"]=target };
 static void Config(int count){ConfigManager.Instance.Tables=new();for(int i=1;i<=count;i++) {string id="40"+i.ToString("000");var data=new DigLevelData(new JSONObject{["id"]=id,["path"]="DigLevel_"+i.ToString("000"),["levelType"]=3,["lockType"]=0});ConfigManager.Instance.Tables.TbDigLevelData.Data.Add(id,data);ConfigManager.Instance.Tables.TbDigLevelSort.DataList.Add(new DigLevelSort(new JSONObject{["id"]=i,["path"]=id}));} }
 static DigGameLevelCtrl Init(){var ctrl=new DigGameLevelCtrl();Check(ctrl.Init(out _),"配置初始化");GameManager.Instance.DigGameLevel=ctrl;return ctrl;}
 public static void Main(){
   foreach(string mode in new[]{"main","draw","iwanner","dig"," DIG "}) {Check(OneLinkRequest.TryParse(Open(mode),out var r,out _),"解析玩法 "+mode);if(mode.Trim().ToLowerInvariant()=="dig")Check(r.GameType==GameType.Dig,"dig 类型映射");}
   Check(OneLinkRequest.TryParse(new JObject{["click_event"]=Open("dig","DigLevel_001")},out var nested,out _)&&nested.LevelPath=="DigLevel_001","iOS 对象 click_event");
   Check(OneLinkRequest.TryParse(new JObject{["click_event"]=Open("dig","DigLevel_999").ToString()},out var str,out _)&&str.GameType==GameType.Dig,"iOS 字符串 click_event");
   Check(!OneLinkRequest.TryParse(Open("other"),out _,out _),"非法玩法拒绝");
   var invalid=Open("dig");invalid["deep_link_sub2"]=new JObject();Check(!OneLinkRequest.TryParse(invalid,out _,out _),"非法目标类型拒绝");
   Check(OneLinkRequest.TryParse(new JObject{["deep_link_value"]="getCoin",["deep_link_sub1"]="x",["deep_link_sub2"]="5"},out var reward,out _)&&reward.CoinCount==5,"原金币协议保留");
   var map=typeof(OneLinkManager).GetMethod("TryMapSrnCampaignTarget",BindingFlags.NonPublic|BindingFlags.Static);
   foreach(string source in new[]{"Facebook Ads","googleadwords_int"}) {object[] args={new JObject{["media_source"]=source,["campaign"]="test_DL-OL-dig-001"},null};Check((bool)map.Invoke(null,args)&&((JObject)args[1])["deep_link_sub2"].ToString()=="DigLevel_001","SRN dig 前缀 "+source);}
   Config(1);DB.DigGame=new();DB.DigGame.Init();var ctrl=Init();
   Check(ctrl.Levels.Count==1&&ctrl.TryGetLevel(999,out var tbc)&&tbc.Path=="DigLevel_999","TBC 独立于普通总关数");
   Check(ctrl.TryGetNext(1,out int next)&&next==999&&!ctrl.TryGetNext(999,out _),"末关下一关为 TBC");
   Check(ctrl.ResolveLowestUnpassed()==1,"无进度回退首未通关");
   Check(ctrl.RecordVictory(1,3),"首胜记录");Check(DB.DigGame.CurrentLevel==999&&DB.DigGame.UnlockLevels.Contains(999),"首胜解锁末关");
   Check(DB.DigGame.LevelCollectedStar["40001"]==3,"最高星写独立 Dig 档");
   Check(!ctrl.RecordVictory(1,1)&&DB.DigGame.LevelCollectedStar["40001"]==3,"重复低星不降最高星");
   int count=DB.DigGame.PassedLevels.Count;Check(!ctrl.RecordVictory(999,3)&&DB.DigGame.PassedLevels.Count==count&&!DB.DigGame.LevelCollectedStar.ContainsKey("40999"),"TBC 不记通关或星级");
   Check(ctrl.ResolveLowestUnpassed()==999,"全通回退 TBC");
   Check(DigRouteProbe.TryEnterDig("DigLevel_001",out _)&&GameManager.Instance.Entered==1&&GameManager.Instance.UnlockAllowed,"指定目标走统一入口并允许仅目标解锁");
   Check(DigRouteProbe.TryEnterDig("diglevel_999",out _)&&GameManager.Instance.Entered==999,"TBC 目标不区分大小写");
   Check(DigRouteProbe.TryEnterDig("missing",out _)&&GameManager.Instance.Entered==999,"无效目标全通回退 TBC");
   DB.DigGame.PassedLevels.Clear();Check(DigRouteProbe.TryEnterDig(null,out _)&&GameManager.Instance.Entered==1,"空目标回退首未通关");
   DB.DigGame.PassedLevels.Add(1);DB.DigGame.CurrentLevel=999;Config(2);ctrl=Init();
   Check(DB.DigGame.CurrentLevel==2&&DB.DigGame.UnlockLevels.Contains(2),"新增内容从旧 TBC 续入新关");
   Check(ctrl.ResolveLowestUnpassed()==2,"按同一排序选择首未通关");
   DB.DigGame.LevelCollectedStar=null;Check(DB.DigGame.EnsureValidCollections()&&DB.DigGame.LevelCollectedStar.Count==0&&DB.DigGame.PassedLevels.Contains(1),"旧档补星级集合保留进度");
   GameManager.Instance.DigGameLevel=null;Check(!DigRouteProbe.TryEnterDig("DigLevel_001",out _),"配置缺失拒绝链接");
   Console.WriteLine($"PASS {checks} managed assertions; real parser/SRN/Dig config/save code, service and Unity wrappers stubbed.");
 }
}
