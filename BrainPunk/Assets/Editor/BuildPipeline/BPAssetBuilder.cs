using System;
using System.Diagnostics;
using System.IO;
using GF;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace BrainPunk.Editor.BuildPipeline
{
    /// <summary>
    /// BrainPunk 资源构建封装器
    /// 职责：统一 BBP/SBP 构建入口，管理版本号、缓存、产物输出。
    /// </summary>
    public static class BPAssetBuilder
    {
        public static BuildResult Build(BuildTarget buildTarget, BPAssetBuildConfig config, bool forceCleanCache = false)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (forceCleanCache && config.UseScriptableBuildPipeline)
            {
                BPSBPCacheCleaner.ClearBuildCache();
                BPSBPCacheCleaner.ClearPipelineOutput();
            }

            string packageVersion = ResolvePackageVersion(config);
            UnityEngine.Debug.Log($"[BPAssetBuilder] Start building. Pipeline={(config.UseScriptableBuildPipeline ? "SBP" : "BBP")}, " +
                                  $"Mode={config.BuildMode}, Version={packageVersion}, Target={buildTarget}");

            Stopwatch sw = Stopwatch.StartNew();
            BuildResult result = config.UseScriptableBuildPipeline
                ? BuildWithSBP(buildTarget, packageVersion, config)
                : BuildWithBBP(buildTarget, packageVersion, config);
            sw.Stop();

            if (result != null && result.Success)
            {
                UnityEngine.Debug.Log($"[BPAssetBuilder] Build success in {sw.Elapsed.TotalSeconds:F1}s. Output: {result.OutputPackageDirectory}");
            }
            else
            {
                string failedTask = result?.FailedTask ?? "Unknown";
                string errorInfo = result?.ErrorInfo ?? "Unknown";
                UnityEngine.Debug.LogError($"[BPAssetBuilder] Build failed after {sw.Elapsed.TotalSeconds:F1}s. Task: {failedTask}, Error: {errorInfo}");
            }

            return result;
        }

        #region SBP Build

        private static BuildResult BuildWithSBP(BuildTarget buildTarget, string packageVersion, BPAssetBuildConfig config)
        {
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
                BuildTarget = buildTarget,
                BuildMode = EBuildMode.IncrementalBuild,
                PackageName = config.PackageName,
                PackageVersion = packageVersion,
                CompressOption = config.CompressOption,
                FileNameStyle = config.FileNameStyle,
                BuildinFileCopyOption = config.BuildinFileCopyOption,
                EnableSharePackRule = config.EnableSharePackRule,
                VerifyBuildingResult = config.VerifyBuildingResult,
                WriteLinkXML = config.SbpWriteLinkXML,
                CacheServerHost = string.IsNullOrEmpty(config.SbpCacheServerHost) ? null : config.SbpCacheServerHost,
                CacheServerPort = config.SbpCacheServerPort,
            };

            if (config.EnableEncryption)
                buildParameters.EncryptionServices = new EncryptoHTXOR();

            var pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(buildParameters, enableLog: true);
        }

        #endregion

        #region BBP Build (Fallback)

        private static BuildResult BuildWithBBP(BuildTarget buildTarget, string packageVersion, BPAssetBuildConfig config)
        {
            var buildParameters = new BuiltinBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
                BuildTarget = buildTarget,
                BuildMode = config.BuildMode,
                PackageName = config.PackageName,
                PackageVersion = packageVersion,
                CompressOption = config.CompressOption,
                FileNameStyle = config.FileNameStyle,
                BuildinFileCopyOption = config.BuildinFileCopyOption,
                EnableSharePackRule = config.EnableSharePackRule,
                VerifyBuildingResult = config.VerifyBuildingResult,
            };

            if (config.EnableEncryption)
                buildParameters.EncryptionServices = new EncryptoHTXOR();

            var pipeline = new BuiltinBuildPipeline();
            return pipeline.Run(buildParameters, enableLog: true);
        }

        #endregion

        #region Version Resolution

        public static string ResolvePackageVersion(BPAssetBuildConfig config)
        {
            switch (config.VersionStrategy)
            {
                case BPAssetBuildConfig.EVersionStrategy.TimeStamp:
                    return GetTimeStamp().ToString();

                case BPAssetBuildConfig.EVersionStrategy.GitCommitHash:
                    return GetGitCommitHash() ?? GetTimeStamp().ToString();

                case BPAssetBuildConfig.EVersionStrategy.Custom:
                    return config.CustomVersion;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static int GetTimeStamp()
        {
            int tickToMillisecond = 10000;
            long stamp197011 = 62135596800000;
            return (int)(DateTime.UtcNow.Ticks / tickToMillisecond - stamp197011);
        }

        private static string GetGitCommitHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = Directory.GetParent(Application.dataPath)?.FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                string output = process?.StandardOutput.ReadToEnd()?.Trim();
                process?.WaitForExit(5000);
                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
