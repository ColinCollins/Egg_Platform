using System;
using System.Collections.Generic;
namespace UnityEngine {
 public sealed class SerializeField:Attribute {}
 public static class Mathf { public static int Clamp(int v,int a,int b)=>Math.Min(b,Math.Max(a,v));public static int Max(int a,int b)=>Math.Max(a,b); }
}
namespace Bear.Logger { public interface IDebuger {} public static class L { public static void LogError(this IDebuger o,string s){} public static void LogWarning(this IDebuger o,string s){} } }
namespace Bear.SaveModule { public enum StorageType { PlayerPrefs } public abstract class BaseSaveDataSO {public abstract void Init();} }
namespace Luban { public abstract class BeanBase {public abstract int GetTypeId();} public class SerializationException:Exception {} }
namespace Config {
 public sealed class Tables { public DigDataTable TbDigLevelData = new();public DigSortTable TbDigLevelSort = new(); }
 public sealed class DigSortTable {public List<DigLevelSort> DataList=new();}
 public sealed class DigDataTable {public Dictionary<string,DigLevelData> Data=new(); public DigLevelData GetOrDefault(string id)=>Data.TryGetValue(id,out var v)?v:null;}
}
namespace Game.ConfigModule { public sealed class ConfigManager { public static ConfigManager Instance = new(); public Config.Tables Tables=new(); } }
public static class DB { public static Bear.SaveModule.DigGame DigGame=new();public static int Saves;public static bool SaveDigGame(){Saves++;return true;} }
public sealed class GameManager {public static GameManager Instance=new();public DigGameLevelCtrl DigGameLevel;public int Entered;public bool UnlockAllowed; public bool TryEnterDigLevel(int? id,out string error,bool allowLockedEntry=false){Entered=id??0;UnlockAllowed=allowLockedEntry;error=null;return true;} }
namespace Guru.SDK.Framework.Utils.Log {public static class Log {public static void I(string s){} } }
