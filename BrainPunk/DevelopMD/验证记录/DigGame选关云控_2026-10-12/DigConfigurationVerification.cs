using System;
using System.IO;
using System.Reflection;
using Game.Dig;
using UnityEngine;
using UnityEditor;

// 本轮原生检查只覆盖 ConfigureInk 的调用边界，完整地图与 UI 留给生产 PlayMode。
public sealed class DigConfigurationVerification : MonoBehaviour
{
    private void Start()
    {
        try
        {
            var level = gameObject.AddComponent<DigLevelCtrl>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var capacity = typeof(DigLevelCtrl).GetField("maxInkLength", flags);
            var stages = typeof(DigLevelCtrl).GetField("inkStarStage", flags);
            level.ConfigureInk(6, new Vector3(1, 3, 5));
            if ((float)capacity.GetValue(level) != 6 || (Vector3)stages.GetValue(level) != new Vector3(1, 3, 5)) throw new Exception("注入配置");
            level.ConfigureInk(9, new Vector3(0, 2, 4));
            if ((float)capacity.GetValue(level) != 9) throw new Exception("初始化前可替换配置");
            typeof(DigLevelCtrl).GetField("<IsRuntimeReady>k__BackingField", flags).SetValue(level, true);
            level.ConfigureInk(99, new Vector3(0, 0, 0));
            if ((float)capacity.GetValue(level) != 9 || (Vector3)stages.GetValue(level) != new Vector3(0, 2, 4)) throw new Exception("已初始化不可替换");
            File.WriteAllText("C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012/native-config-results.txt", "PASS 3: 当前生产 ConfigureInk 的配置注入、初始化前替换、初始化后拒绝。未验证生产地图/完整 HUD。");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText("C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012/native-config-results.txt", "FAIL " + ex);
            EditorApplication.Exit(1);
        }
    }
}
