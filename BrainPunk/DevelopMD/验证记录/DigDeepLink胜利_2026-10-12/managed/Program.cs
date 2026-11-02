using System;using Config;using Config.Level;using Game;using Bear.UI;
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
