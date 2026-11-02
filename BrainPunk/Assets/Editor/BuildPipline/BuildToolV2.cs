#nullable enable
using System;
using System.IO;
using Bear.Logger;
using Guru.Editor;
using UnityEditor;
using UnityEngine;

public static class BuildToolV2
{
    #region 构建代理

    // TODO: 每个项目的定制化构建需要实现此代理
    private static IAppBuildDelegate? AppBuildDelegate => BPGameCustomBuildDelegate.Instance;

    #endregion

    #region 构建参数

    public enum BuildEnv
    {
        Debug,
        Release,
    }

    private class BuildParam
    {
        public AppBuilderType BuildType;
        public BuildEnv BuildEnv;
        public bool IsBuildAAB;
        public string BundleVersion;

        public override string ToString()
        {
            return
                $"{nameof(BuildType)}: {BuildType}, {nameof(BuildEnv)}: {BuildEnv}, {nameof(BundleVersion)}: {BundleVersion}, {nameof(IsBuildAAB)}: {IsBuildAAB}";
        }
    }

    #endregion

    #region 编辑器菜单

    [MenuItem("Tools/自动化打包/平台切换/Android", false, 1)]
    public static void ChangePlatform2Android()
    {
        BuildSwitchPlatform(BuildTarget.Android);
    }

    [MenuItem("Tools/自动化打包/平台切换/IOS", false, 2)]
    public static void ChangePlatform2IOS()
    {
        BuildSwitchPlatform(BuildTarget.iOS);
    }


    #endregion

    #region Jenkins打包

    private static string[] ParseJenkinsBuildSetting(string[] commandLineArgs)
    {
        for (int i = 0; i < commandLineArgs.Length; i++)
        {
            string commandLineArg = commandLineArgs[i];
            if (commandLineArg.StartsWith("-params"))
            {
                return commandLineArg.Split('-');
            }
        }

        return null;
    }

    public static void JenkinsBuildAndroid()
    {
        string outputDir = Path.GetFullPath($"{Application.dataPath}/../BuildOutput/Android");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        var param = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
        BuildParam buildParam = new BuildParam
        {
            BundleVersion = param[2],
            BuildType = AppBuilderType.Jenkins,
            BuildEnv = param[3].ToUpper() == "DEBUG" ? BuildEnv.Debug : BuildEnv.Release,
            IsBuildAAB = false
        };
        Debug.Log(buildParam.ToString());
        BuildAndroid(buildParam);
    }

    public static void JenkinsBuildAndroidRelease()
    {
        string outputDir = Path.GetFullPath($"{Application.dataPath}/../BuildOutput/Android");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        var param = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
        BuildParam buildParam = new BuildParam
        {
            BundleVersion = param[2],
            BuildType = AppBuilderType.Jenkins,
            BuildEnv = BuildEnv.Release,
            IsBuildAAB = true
        };
        Debug.Log(buildParam.ToString());



        BuildAndroid(buildParam);
    }

    public static void JenkinsBuildIOSRelease()
    {
        var param = ParseJenkinsBuildSetting(Environment.GetCommandLineArgs());
        BuildParam buildParam = new BuildParam
        {
            BundleVersion = param[2],
            BuildType = AppBuilderType.Jenkins,
            BuildEnv = BuildEnv.Release,
            IsBuildAAB = true,
        };
        BuildIOS(buildParam);
    }

    #endregion

    #region Android打包

    [MenuItem("Tools/自动化打包/Android/Debug", false, 10)]
    public static void BuildAndroidDebug()
    {
        BuildAndroid(new BuildParam()
        {
            BundleVersion = PlayerSettings.bundleVersion,
            BuildType = AppBuilderType.Editor,
            BuildEnv = BuildEnv.Debug,
            IsBuildAAB = false,
        });
    }


    [MenuItem("Tools/自动化打包/Android/Release", false, 1)]
    public static void BuildAndroidRelease()
    {
        BuildAndroid(new BuildParam()
        {
            BundleVersion = PlayerSettings.bundleVersion,
            BuildType = AppBuilderType.Editor,
            BuildEnv = BuildEnv.Release,
            IsBuildAAB = false,
        });
    }


    [MenuItem("Tools/自动化打包/Android/Release发布包", false, 2)]
    public static void BuildAndroidReleaseAAB()
    {
        BuildAndroid(new BuildParam()
        {
            BundleVersion = PlayerSettings.bundleVersion,
            BuildType = AppBuilderType.Editor,
            BuildEnv = BuildEnv.Release,
            IsBuildAAB = true,
        });
    }

    [MenuItem("Tools/自动化打包/Android/Release(DeepClean)", false, 3)]
    public static void BuildAndroidReleaseDeepClean()
    {
        Debug.Log("[BuildToolV2] DeepClean Release build requested. SBP cache will be cleared.");
        BPGameCustomBuildDelegate.ForceDeepCleanForNextBuild = true;
        BuildAndroidRelease();
    }

    [MenuItem("Tools/自动化打包/Android/Release发布包(DeepClean)", false, 4)]
    public static void BuildAndroidReleaseAABDeepClean()
    {
        Debug.Log("[BuildToolV2] DeepClean Release AAB build requested. SBP cache will be cleared.");
        BPGameCustomBuildDelegate.ForceDeepCleanForNextBuild = true;
        BuildAndroidReleaseAAB();
    }

    /// <summary>
    /// 打 Android 包
    /// </summary>
    /// <param name="buildParam"></param>
    private static void BuildAndroid(BuildParam buildParam)
    {
        Debug.Log("========Guru Build Start========");
        var param = AppBuildParam.Build(
            targetName: AppBuildParam.TargetNameAndroid,
            isRelease: buildParam.BuildEnv == BuildEnv.Release,
            builderType: buildParam.BuildType,
            version: buildParam.BundleVersion,
            androidTargetVersion: 36,
            buildShowLog: buildParam.BuildEnv == BuildEnv.Release,
            buildSymbols: buildParam.BuildEnv == BuildEnv.Release,
            buildAAB: buildParam.IsBuildAAB,
            useMinify: true,
            debugWithMono: false
        // strippingLevel: ManagedStrippingLevel.Low
        );

        if (buildParam.BuildEnv == BuildEnv.Release)
            BearLogger.LogClose();
        else
            BearLogger.LogOpen();

        var builder = new AppBuilder(AppBuildDelegate);
        builder.BuildAndroid(param);
        Debug.Log("========Guru Build End========");
    }

    #endregion

    #region IOS打包

    [MenuItem("Tools/自动化打包/IOS/Xcode Debug", false, 21)]
    public static void BuildIOSDebug()
    {
        BuildIOS(new BuildParam()
        {
            BundleVersion = PlayerSettings.bundleVersion,
            BuildType = AppBuilderType.Editor,
            BuildEnv = BuildEnv.Debug,
            IsBuildAAB = false,
        });
    }

    [MenuItem("Tools/自动化打包/IOS/Xcode Release", false, 20)]
    public static void BuildIOSReleaseAppStore()
    {
        BuildIOS(new BuildParam()
        {
            BundleVersion = PlayerSettings.bundleVersion,
            BuildType = AppBuilderType.Editor,
            BuildEnv = BuildEnv.Release,
            IsBuildAAB = true,
        });
    }

    [MenuItem("Tools/自动化打包/IOS/Xcode Release(DeepClean)", false, 22)]
    public static void BuildIOSReleaseDeepClean()
    {
        Debug.Log("[BuildToolV2] DeepClean iOS Release build requested. SBP cache will be cleared.");
        BPGameCustomBuildDelegate.ForceDeepCleanForNextBuild = true;
        BuildIOSReleaseAppStore();
    }

    private static void BuildIOS(BuildParam buildParam)
    {
        Debug.Log("========Guru Build Start========");
        var param = AppBuildParam.Build(
            targetName: AppBuildParam.TargetNameIOS,
            isRelease: buildParam.BuildEnv == BuildEnv.Release,
            builderType: buildParam.BuildType,
            version: buildParam.BundleVersion,
            buildShowLog: buildParam.BuildEnv == BuildEnv.Release,
            buildSymbols: buildParam.BuildEnv == BuildEnv.Release,
            buildAAB: buildParam.IsBuildAAB,
            useMinify: true,
            debugWithMono: false,
            iOSTargetVersion: "15.0"
        );

        var builder = new AppBuilder(AppBuildDelegate);

        if (buildParam.BuildEnv == BuildEnv.Release)
            BearLogger.LogClose();

        AppBuildDelegate.OnBeforeBuildStart(BuildTarget.iOS);
        builder.BuildIOS(param);
        Debug.Log("========Guru Build End========");
    }

    #endregion

    #region 通用方法

    /// <summary>
    /// 平台切换
    /// </summary>
    /// <param name="targetPlatform"></param>
    private static void BuildSwitchPlatform(BuildTarget targetPlatform)
    {
        if (EditorUserBuildSettings.activeBuildTarget != targetPlatform)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(targetPlatform);
            AssetDatabase.Refresh();
        }
    }

    #endregion
}




