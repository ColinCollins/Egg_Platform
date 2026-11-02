from pathlib import Path
import re, json, xml.sax.saxutils as xml
r=Path('D:/GitStore/BrinPunk/BrainPunk');t=Path(__file__).parent;d=t/'managed';d.mkdir(exist_ok=True)
def read(p):return (r/p).read_text(encoding='utf-8-sig')
def method(s,name):
    a=re.search(r'^[ \t]*(?:private|public|internal|protected) [^\r\n]*\b'+re.escape(name)+r'\(',s,re.M).start()
    b=s.index('{',s.index(name+'(',a));depth=1;i=b+1
    # 当前抽取的方法没有在注释中出现单独的不成对大括号。
    while depth:
        if s[i]=='{':depth+=1
        if s[i]=='}':depth-=1
        i+=1
    return s[a:i]+'\n'
rc=read('Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs')
(d/'CommitProbe.cs').write_text('''using System;using System.Collections.Generic;using UnityEngine;using Game.ConfigModule;using GuruLog=Guru.SDK.Framework.Utils.Log.Log;
public static class CommitProbe {
 private static string pendingDrawConfig,pendingDigConfig;
 private static int pendingDrawCallbackSequence,pendingDigCallbackSequence,latestDrawCallbackSequence,latestDigCallbackSequence;
 private static bool isCacheReady=true;
 private const string DrawGameConfigKey="drawgame_config",DigGameConfigKey="diggame_config";
 private static Dictionary<string,string> latestValidConfigs=new();
 public static bool Commit(Dictionary<string,string> entries,bool callback=true)=>PersistResolvedConfigs(entries,callback);
 public static bool HasPendingDig=>pendingDigConfig!=null;
'''+method(rc,'PersistResolvedConfigs')+method(rc,'TryApplyPendingDigConfig')+'}',encoding='utf-8')
# 跨玩法累计的实际方法，SDK 发出事件和存档 IO 在 harness 中捕获。
up=read('Assets/Game/Scripts/SDK/Services/Events/GameSDKService.UserProperty.cs')
(d/'CombinedProbe.cs').write_text('public partial class GameSDKService {\n'+method(up,'GetLevelPassedCount')+method(up,'GetCombinedLevelPassedCount')+'}',encoding='utf-8')
(d/'Stubs.cs').write_text('''using System;using System.Collections.Generic;using Config;
namespace UnityEngine {
 public sealed class SerializeField:Attribute {}
 public static class Application {public static bool isPlaying=true;}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int v,int a,int b)=>Math.Min(b,Math.Max(a,v));}
 public static class Debug {public static void LogWarning(string s){} public static void LogError(string s){} }
}
namespace Bear.Logger {public interface IDebuger {} public static class L {public static void Log(this IDebuger o,string s){} public static void LogWarning(this IDebuger o,string s){} public static void LogError(this IDebuger o,string s){} } }
namespace Bear.SaveModule {public enum StorageType{PlayerPrefs}public abstract class BaseSaveDataSO{public abstract void Init();}}
namespace Luban {public abstract class BeanBase{public abstract int GetTypeId();}public class SerializationException:Exception {}}
namespace Config {public class GlobalTable {public List<Config.LubanConfig.GlobalConst> DataList=new();}public class Tables {public TbDigLevelData TbDigLevelData;public TbDigLevelSort TbDigLevelSort;public GlobalTable TbGlobalConst=new();}}
namespace Game.ConfigModule {public partial class ConfigManager {
 public static ConfigManager Instance=new();public bool IsInitialized=true;public Tables Tables=new();
 public static partial class RemoteConfig {
  public const string DrawGameConfigKey="drawgame_config",EnabledFieldKey="enabled",LevelSortKey="level_sort";
  public static Dictionary<string,string> Raw=>DB.GameData.RemoteConfigCache;public static bool FailSave;
 }
}}
public static class DB {
 public class OtherSave {public Dictionary<string,int> LevelSuccessCount=new();public Dictionary<string,string> RemoteConfigCache=new();public bool Save()=>!Game.ConfigModule.ConfigManager.RemoteConfig.FailSave;}
 public static OtherSave GameData=new(),DrawGame=new(),IWannerGame=new();
 public static Bear.SaveModule.DigGame DigGame=new();public static int Saves;
 public static bool SaveDigGame(){Saves++;return true;}
}
public class GameManager {
 public static GameManager Instance=new();public static bool IsInitialized=true;
 public DigGameLevelCtrl DigGameLevel;public DrawLevelStub DrawGameLevel;
 public bool IsDigRemoteConfigRefreshBlocked,IsDrawRemoteConfigRefreshBlocked;
 public string DigRemoteConfigRefreshState=>"test";public string DrawRemoteConfigRefreshState=>"test";
 public int RefreshCount;public void RefreshDigRemoteConfigViews(){RefreshCount++;}public void RefreshDrawRemoteConfigViews(){}
}
public class DrawLevelStub {public bool IsReady;public int ConfigGeneration;public List<Config.DrawLevelSort> LevelSorts=new();public bool TryPrepareRemoteConfig(string j,out List<Config.DrawLevelSort> c,out string e){c=null;e=null;return false;}public void ApplyRemoteConfig(List<Config.DrawLevelSort> c){} }
namespace Config {public class DrawLevelSort {}}
namespace Guru.SDK.Framework.Utils.Log {public static class Log {public static void I(string s){}public static void W(string s){}public static void E(string s){} }}
public partial class GameSDKService {
 public const int LevelStartTypeNewGame=0,LevelStartTypeReplay=1,LevelEndTypeFailed=1,LevelEndTypeSuccess=0,LevelEndTypeExit=2;
 private const string ScreenGameView="game";
 public static GameSDKService Instance=new();public List<(string name,Dictionary<string,object> data)> Events=new();public int BPlay;public string LastPath;
 private void RecordLastStartedLevelPath(string p){LastPath=p;}private void SetScreen(string s){}
 private string GetLevelStartType(int t)=>t==1?"replay":"new_game";
 private string GetLevelResult(int t)=>t==0?"success":t==2?"exit":"fail";
 private void LogEvent(string name,Dictionary<string,object> data){Events.Add((name,data));}
 public void UpdateBPlay(){BPlay=GetCombinedLevelPassedCount();}
 private void LevelEndSuccessEvent(int n,int id){if(n<=50)Events.Add(($"level_end_success_{n}",new(){{"level",id}}));}
}
public sealed partial class DigPlayCtrl : Bear.Logger.IDebuger {
 private DigLevelData _data;private int _levelId;private object _runtime=new();private bool _resolved;
 public void Enter(int id,DigLevelData data){_levelId=id;_data=data;StartNewLevelAnalytics();}
 public void TickProbe(float seconds){TickLevelAnalytics(seconds);}
 public void Retry(){RecordLevelRetryAnalytics();ReportLevelEndAnalytics(GameSDKService.LevelEndTypeFailed);StartResetAnalytics();}
 public void Win(){RecordLevelSuccessAnalytics();ReportLevelEndAnalytics(GameSDKService.LevelEndTypeSuccess);ResetCompletedLevelAnalyticsCounters();}
 public void Replay(){StartReplayAnalytics();}
 public void Exit(){ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);ResetLevelAnalyticsContext();}
}
''',encoding='utf-8')
getters=read('Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs')
constants='\n'.join(re.findall(r'^\s*public const string DigGame\w+ = .*?;',getters,re.M))
methods=['TrySetRawBatch','TryGetRaw','TryGetValue<T>','TryGetGlobalConst','TryGetRemoteLevelRangeValue','TryResolveLevelRangeValue']+re.findall(r'public static \w+ (GetDigGame\w+)\(',getters)
(d/'GettersProbe.cs').write_text('using System;using System.Collections.Generic;using UnityEngine;using Config.LubanConfig;namespace Game.ConfigModule { public partial class ConfigManager {public static partial class RemoteConfig {private static GlobalConst globalConst;'+constants+'\n'+''.join(method(getters,n) for n in methods)+'}}}',encoding='utf-8')
files=['Assets/Game/Scripts/Play/Dig/DigGameLevelCtrl.cs','Assets/Game/Scripts/Play/Dig/DigPlayCtrl.Analytics.cs','Assets/Game/Scripts/SDK/Services/Events/GameSDKService.Dig.cs','Assets/Game/Scripts/Common/JsonUtils.cs','Assets/Game/DB/DigGame.cs','Assets/Game/DB/DigGame_Partial.cs','Assets/Configs/Code/DigLevelData.cs','Assets/Configs/Code/DigLevelSort.cs','Assets/Configs/Code/TbDigLevelData.cs','Assets/Configs/Code/TbDigLevelSort.cs','Assets/Configs/Code/LubanConfig/GlobalConst.cs','Assets/Configs/Code/Level/GameType.cs','Assets/Configs/Code/Level/LevelLockType.cs','Library/PackageCache/com.code-philosophy.luban@d870def77ce1/Runtime/SimpleJSON/SimpleJSON.cs','Library/PackageCache/com.code-philosophy.luban@d870def77ce1/Runtime/StringUtil.cs']
project='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'
project+=''.join('<Compile Include="'+xml.escape(str(r/f))+'" />' for f in files)
project+='<Reference Include="Newtonsoft.Json"><HintPath>'+str(r/'Library/PackageCache/com.unity.nuget.newtonsoft-json@74deb55db2a0/Runtime/Newtonsoft.Json.dll')+'</HintPath></Reference></ItemGroup></Project>'
(d/'Managed.csproj').write_text(project,encoding='utf-8')
print('Verification harness uses actual Dig data/sort/save/analytics and extracted actual RemoteConfig commit methods; SDK/Unity/IO stubbed.')
