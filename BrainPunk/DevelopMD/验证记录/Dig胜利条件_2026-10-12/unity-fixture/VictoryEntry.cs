using System;using System.IO;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Game.Dig;
public static class VictoryEntry {
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var host=new GameObject("VictoryVerification");host.AddComponent<DigVictoryVerification>();EditorSceneManager.SaveScene(host.scene,"Assets/Probe.unity");EditorApplication.isPlaying=true;}
 public static void ValidateLegacy(){
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var go=new GameObject("Legacy");go.AddComponent<DigLevelCtrl>();const string path="Assets/ProbeAssets/LegacyLevel.prefab";PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
  string content=File.ReadAllText(path);content=content.Replace("  victoryCondition: 0\n","").Replace("  victoryDelay: 1\n","");File.WriteAllText(path,content);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);var level=prefab.GetComponent<DigLevelCtrl>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  if((float)typeof(DigLevelCtrl).GetField("victoryDelay",flags).GetValue(level)!=1f||(DigVictoryCondition)typeof(DigLevelCtrl).GetField("victoryCondition",flags).GetValue(level)!=DigVictoryCondition.ConsumeAllGerms)throw new Exception("Legacy defaults failed");
  if(content.Contains("victoryCondition:")||content.Contains("victoryDelay:"))throw new Exception("Legacy fixture still has new fields");
  File.WriteAllText("C:/Users/admin/AppData/Local/Temp/brainpunk-dig-victory-conditions-20261012/legacy-defaults.txt","PASS: actual PrefabImporter for fixture with missing victoryCondition/victoryDelay uses ConsumeAllGerms/1s; edit mode does not initialize runtime.");EditorApplication.Exit(0);
 }
}
