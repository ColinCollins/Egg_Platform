# HotReload 测试代码实现

创建时间：2026-05-21  
版本：v1.0  
关联文档：[HotReload测试流程设计.md](HotReload测试流程设计.md)、[HotReload模块分析.md](HotReload模块分析.md)

## 1. 概述

本文档基于 `HotReload测试流程设计.md` 给出可直接落地的代码实现方案。目标是在**最小侵入现有代码**的前提下，补齐环境隔离、测试记录、GM 可视化和验证矩阵这四块能力。

### 1.1 代码位置规划

| 文件 | 路径 | 职责 |
|---|---|---|
| `HotReloadEnvironmentManager.cs` | `Assets/Game/Scripts/HotReload/Test/` | 运行时环境配置管理 |
| `HotReloadTestRecorder.cs` | `Assets/Game/Scripts/HotReload/Test/` | 热更节点测试记录 |
| `HotReloadCacheIsolation.cs` | `Assets/Game/Scripts/HotReload/Test/` | 缓存按环境分桶 |
| `HotReloadTestValidator.cs` | `Assets/Game/Scripts/HotReload/Test/` | 验证矩阵与断言 |
| `HotReloadDebugPanel.cs` | `Assets/Game/Scripts/HotReload/Test/` | GM/Debug 面板扩展 |
| `HotReloadTestBootstrap.cs` | `Assets/Game/Scripts/HotReload/Test/` | 测试入口与生命周期 |
| `HotReloadBuildInjector.cs` | `Assets/Editor/HotReload/` | 构建时注入测试配置 |
| `HotReloadTestMenu.cs` | `Assets/Editor/HotReload/` | Editor 菜单快捷操作 |

### 1.2 环境枚举定义

沿用流程设计中的 4 个环境名：

```csharp
public enum HotReloadEnvironment
{
    LocalSim,           // 本地模拟：编辑器自测
    StagingHotfix,      // 测试热更：QA / 开发联调
    ReleaseRehearsal,   // 预发布演练：正式候选验证
    ProdOnline,         // 正式线上：真实用户环境
}
```

---

## 2. 环境管理器 (HotReloadEnvironmentManager)

### 2.1 设计目标

- 运行时切换 CDN、RemoteConfig、缓存桶，不依赖手改 `GameSetting.asset`。
- 构建时通过编译符号或配置文件注入默认环境。
- 所有环境切换都走统一入口，避免散落配置。

### 2.2 代码实现

```csharp
using System;
using UnityEngine;

namespace Game.HotReload.Test
{
    /// <summary>
    /// 热更环境管理器
    /// 职责：统一管理 CDN、RemoteConfig、缓存桶、版本标识的环境隔离。
    /// </summary>
    public class HotReloadEnvironmentManager : MonoBehaviour
    {
        private const string PREF_KEY_ENV = "HotReload_Env";
        private const string PREF_KEY_BUILD_ID = "HotReload_BuildId";

        public static HotReloadEnvironmentManager Instance { get; private set; }

        [SerializeField] private HotReloadEnvironment _defaultEnvironment = HotReloadEnvironment.StagingHotfix;
        [SerializeField] private bool _allowRuntimeSwitch = true; // 仅测试包开启

        /// <summary>当前环境</summary>
        public HotReloadEnvironment CurrentEnv { get; private set; }

        /// <summary>当前 BuildId（用于标识候选产物）</summary>
        public string CurrentBuildId { get; private set; }

        /// <summary>当前环境对应的 CDN 根地址</summary>
        public string CurrentCdnRoot => GetCdnRoot(CurrentEnv);

        /// <summary>当前缓存桶后缀（用于隔离本地缓存）</summary>
        public string CurrentCacheBucket => $"{CurrentEnv}_{CurrentBuildId}";

        #region 环境配置表（可改成从 ScriptableObject 或 JSON 读取）

        [System.Serializable]
        public class EnvConfig
        {
            public HotReloadEnvironment env;
            public string cdnUrl;
            public string cdnFallbackUrl;
            public string remoteConfigKey; // 如果 RemoteConfig 也分环境
            public string displayName;
        }

        [SerializeField] private EnvConfig[] _envConfigs = new EnvConfig[]
        {
            new EnvConfig
            {
                env = HotReloadEnvironment.LocalSim,
                cdnUrl = "",
                cdnFallbackUrl = "",
                remoteConfigKey = "local",
                displayName = "本地模拟"
            },
            new EnvConfig
            {
                env = HotReloadEnvironment.StagingHotfix,
                cdnUrl = "https://your-cdn.com/hotfix/staging",
                cdnFallbackUrl = "",
                remoteConfigKey = "staging",
                displayName = "测试热更"
            },
            new EnvConfig
            {
                env = HotReloadEnvironment.ReleaseRehearsal,
                cdnUrl = "https://your-cdn.com/hotfix/rehearsal",
                cdnFallbackUrl = "",
                remoteConfigKey = "rehearsal",
                displayName = "预发布演练"
            },
            new EnvConfig
            {
                env = HotReloadEnvironment.ProdOnline,
                cdnUrl = "https://your-cdn.com/hotfix/prod/current",
                cdnFallbackUrl = "",
                remoteConfigKey = "prod",
                displayName = "正式线上"
            }
        };

        #endregion

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 优先从本地存储恢复（调试用），否则用默认值
            int savedEnv = PlayerPrefs.GetInt(PREF_KEY_ENV, (int)_defaultEnvironment);
            CurrentEnv = (HotReloadEnvironment)savedEnv;
            CurrentBuildId = PlayerPrefs.GetString(PREF_KEY_BUILD_ID, "unknown");

            ApplyToGameSetting();
        }

        /// <summary>
        /// 切换环境（运行时调试用，正式上线包建议关闭 _allowRuntimeSwitch）
        /// </summary>
        public void SwitchEnvironment(HotReloadEnvironment env, string buildId = null)
        {
            if (!_allowRuntimeSwitch && Application.isPlaying)
            {
                Debug.LogWarning($"[HotReloadEnv] Runtime switch disabled. Current={CurrentEnv}");
                return;
            }

            var prevEnv = CurrentEnv;
            CurrentEnv = env;
            if (!string.IsNullOrEmpty(buildId))
                CurrentBuildId = buildId;

            PlayerPrefs.SetInt(PREF_KEY_ENV, (int)CurrentEnv);
            PlayerPrefs.SetString(PREF_KEY_BUILD_ID, CurrentBuildId);
            PlayerPrefs.Save();

            ApplyToGameSetting();
            Debug.Log($"[HotReloadEnv] Switched {prevEnv} -> {CurrentEnv}, BuildId={CurrentBuildId}, CDN={CurrentCdnRoot}");
        }

        /// <summary>
        /// 把当前环境的 CDN 等配置写入 GameSettingData（内存态）
        /// </summary>
        private void ApplyToGameSetting()
        {
            var config = GetConfig(CurrentEnv);
            if (config == null) return;

            // 直接修改 GameSettingData.Setting 的内存值，不碰 asset 文件
            // 注意：GameSetting.SetCdnUrl 存在写本地存储顺序问题，这里绕开它直接设字段
            var setting = GF.GameSettingData.Setting;
            if (setting != null)
            {
                // 修正后的顺序：先改内存字段，再持久化
                setting.cdnUrl = config.cdnUrl;
                setting.cdnUrlFallback = config.cdnFallbackUrl;
                App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN, config.cdnUrl);
                App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN_FALLBACK, config.cdnFallbackUrl);
            }
        }

        public EnvConfig GetConfig(HotReloadEnvironment env)
        {
            foreach (var c in _envConfigs)
            {
                if (c.env == env) return c;
            }
            return null;
        }

        public string GetCdnRoot(HotReloadEnvironment env)
        {
            var config = GetConfig(env);
            return config?.cdnUrl ?? string.Empty;
        }
    }
}
```

### 2.3 关键说明

- **为什么不直接用 `GameSetting.SetCdnUrl()`？**  
  当前 `SetCdnUrl` 的写入顺序是：先写本地存储（存的却是旧值 `cdnUrl`），再改内存字段。这会导致持久化值和内存值不一致。环境管理器绕过这个接口，自己保证顺序。

- **编译符号注入**  
  构建时可以通过 `BuildInjector` 把 `_defaultEnvironment` 和 `_allowRuntimeSwitch` 注入成目标值。详见第 7 节。

---

## 3. 测试记录器 (HotReloadTestRecorder)

### 3.1 设计目标

- 覆盖流程设计中要求记录的 8 个节点 + 6 项上下文信息。
- 不阻塞主流程，仅追加记录。
- 支持导出为 JSON 或截图附件，方便 QA 提交 bug。

### 3.2 代码实现

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.HotReload.Test
{
    /// <summary>
    /// 热更测试记录器
    /// 职责：记录热更全链路节点、耗时、结果、上下文。
    /// </summary>
    public class HotReloadTestRecorder : MonoBehaviour
    {
        public static HotReloadTestRecorder Instance { get; private set; }

        [System.Serializable]
        public class StepRecord
        {
            public string stepName;
            public string state; // Enter / Exit / Failed
            public float timestamp;
            public float durationFromStart;
            public string detail;
        }

        [System.Serializable]
        public class TestReport
        {
            public string buildId;
            public string environment;
            public string appVersion;
            public string packageName;
            public string packageVersion;
            public string playMode;
            public string cdnUrl;
            public bool remoteConfigSuccess;
            public bool cacheHit;
            public List<StepRecord> steps = new List<StepRecord>();
            public bool isSuccess;
            public string failReason;
            public float totalDuration;
        }

        private TestReport _report;
        private float _startTime;
        private bool _isRecording;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void StartRecord(string buildId, string environment)
        {
            _report = new TestReport
            {
                buildId = buildId,
                environment = environment,
                appVersion = Application.version,
                startTime = Time.time
            };
            _startTime = Time.time;
            _isRecording = true;
        }

        public void RecordStep(string stepName, string state, string detail = null)
        {
            if (!_isRecording) return;
            _report.steps.Add(new StepRecord
            {
                stepName = stepName,
                state = state,
                timestamp = Time.time,
                durationFromStart = Time.time - _startTime,
                detail = detail
            });
        }

        public void SetContext(
            string packageName,
            string packageVersion,
            string playMode,
            string cdnUrl,
            bool remoteConfigSuccess,
            bool cacheHit)
        {
            if (!_isRecording) return;
            _report.packageName = packageName;
            _report.packageVersion = packageVersion;
            _report.playMode = playMode;
            _report.cdnUrl = cdnUrl;
            _report.remoteConfigSuccess = remoteConfigSuccess;
            _report.cacheHit = cacheHit;
        }

        public void FinishRecord(bool success, string failReason = null)
        {
            if (!_isRecording) return;
            _isRecording = false;
            _report.isSuccess = success;
            _report.failReason = failReason;
            _report.totalDuration = Time.time - _startTime;
        }

        public string ExportJson()
        {
            return JsonUtility.ToJson(_report, true);
        }

        public TestReport GetReport() => _report;

        public void SaveToPlayerPrefs(string key = "HotReload_LastTestReport")
        {
            if (_report == null) return;
            PlayerPrefs.SetString(key, ExportJson());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 快速打印到控制台，方便测试时直接复制
        /// </summary>
        public void DumpToConsole()
        {
            if (_report == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("========== HotReload Test Report ==========");
            sb.AppendLine($"BuildId: {_report.buildId}");
            sb.AppendLine($"Environment: {_report.environment}");
            sb.AppendLine($"AppVersion: {_report.appVersion}");
            sb.AppendLine($"Package: {_report.packageName} / {_report.packageVersion}");
            sb.AppendLine($"PlayMode: {_report.playMode}");
            sb.AppendLine($"CDN: {_report.cdnUrl}");
            sb.AppendLine($"RemoteConfigOK: {_report.remoteConfigSuccess}, CacheHit: {_report.cacheHit}");
            sb.AppendLine($"Result: {(_report.isSuccess ? "SUCCESS" : "FAILED")}, Reason: {_report.failReason}");
            sb.AppendLine($"TotalDuration: {_report.totalDuration:F2}s");
            sb.AppendLine("--- Steps ---");
            foreach (var s in _report.steps)
            {
                sb.AppendLine($"[{s.durationFromStart:F2}s] {s.stepName} : {s.state} {s.detail}");
            }
            sb.AppendLine("===========================================");
            Debug.Log(sb.ToString());
        }
    }
}
```

### 3.3 与 LoadManager 的集成点

在 `LoadManager.HotReload()` 中，于初始化前后插入记录代码（最小侵入）：

```csharp
// 在 HotReload() 方法开头追加：
HotReloadTestRecorder.Instance?.StartRecord(
    HotReloadEnvironmentManager.Instance?.CurrentBuildId ?? "unknown",
    HotReloadEnvironmentManager.Instance?.CurrentEnv.ToString() ?? "unknown"
);

// 在 Init 之后追加上下文记录：
HotReloadTestRecorder.Instance?.SetContext(
    packageName,
    HotReloadCtrl.Instance.PackageVersion,
    packageElement.playMode.ToString(),
    GameSettingData.Setting.GetCdnUrl(),
    remoteConfigSuccess: false, // 在 LoadConfigAsync 后更新
    cacheHit: false             // 在热更结束后更新
);

// 在 while 循环结束后追加：
bool success = HotReloadCtrl.Instance.IsFinish && !HotReloadCtrl.Instance.IsFailed;
HotReloadTestRecorder.Instance?.FinishRecord(
    success,
    HotReloadCtrl.Instance.IsFailed ? "HotReload Failed" :
    (Time.time - hotReloadStartTime > HOT_RELOAD_TIMEOUT ? "Timeout" : null)
);
HotReloadTestRecorder.Instance?.DumpToConsole();
```

---

## 4. 缓存隔离 (HotReloadCacheIsolation)

### 4.1 设计目标

YooAsset 的默认缓存路径在 `Application.persistentDataPath` 下。如果不隔离，同一安装包切换环境后，旧缓存可能干扰新环境的测试结果。

### 4.2 代码实现

```csharp
using System.IO;
using UnityEngine;

namespace Game.HotReload.Test
{
    /// <summary>
    /// 热更缓存隔离器
    /// 职责：按环境+BuildId 分桶，避免缓存污染。
    /// </summary>
    public static class HotReloadCacheIsolation
    {
        /// <summary>
        /// 获取当前环境对应的缓存根目录
        /// </summary>
        public static string GetCacheRoot(HotReloadEnvironment env, string buildId)
        {
            string basePath = Application.persistentDataPath;
            string bucket = string.IsNullOrEmpty(buildId) ? env.ToString() : $"{env}_{buildId}";
            return Path.Combine(basePath, "YooAssetCache", bucket);
        }

        /// <summary>
        /// 清理指定环境的全部缓存
        /// </summary>
        public static void ClearCache(HotReloadEnvironment env, string buildId)
        {
            string path = GetCacheRoot(env, buildId);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                Debug.Log($"[HotReloadCache] Cleared: {path}");
            }
            else
            {
                Debug.Log($"[HotReloadCache] No cache to clear: {path}");
            }
        }

        /// <summary>
        /// 清理所有环境的缓存（慎用）
        /// </summary>
        public static void ClearAllCache()
        {
            string root = Path.Combine(Application.persistentDataPath, "YooAssetCache");
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
                Debug.Log($"[HotReloadCache] Cleared ALL cache under: {root}");
            }
        }

        /// <summary>
        /// 计算当前缓存目录的占用大小（MB）
        /// </summary>
        public static float GetCacheSizeMB(HotReloadEnvironment env, string buildId)
        {
            string path = GetCacheRoot(env, buildId);
            if (!Directory.Exists(path)) return 0f;

            long totalBytes = 0;
            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                totalBytes += file.Length;
            }
            return totalBytes / (1024f * 1024f);
        }
    }
}
```

### 4.3 与 YooAsset 的集成

当前 `HotReloadCtrl_InitializePackage` 在 `HostPlayMode` 下使用 `FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices)`，默认缓存路径由 YooAsset 内部决定。

要接入自定义缓存路径，需要修改 `HostPlayModeParameters` 的构建方式：

```csharp
// 在 HotReloadCtrl_InitializePackage.cs 的 HostPlayMode 分支中：
if (playMode == EPlayMode.HostPlayMode)
{
    var initParameters = new HostPlayModeParameters();
    initParameters.BuildinQueryServices = new BuildinQueryServices();
    initParameters.DecryptionServices = new DecryptoHTXOR();

    string generation = "0";
    // TODO：接入 BuiltinPackageElement.generation（变更清单第 1 项）
    initParameters.RemoteServices = new GF.RemoteServices(
        GameSettingData.Setting.GetCdnUrl(),
        GameSettingData.Setting.GetCdnUrlFallback(),
        generation,
        package.PackageName);

    initParameters.BreakpointResumeFileSize = 1024 * 1024;

    // ===== 缓存隔离注入点 =====
    var envMgr = HotReloadEnvironmentManager.Instance;
    if (envMgr != null)
    {
        string cacheRoot = HotReloadCacheIsolation.GetCacheRoot(envMgr.CurrentEnv, envMgr.CurrentBuildId);
        var cacheFileSystemParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(
            initParameters.RemoteServices,
            cacheRoot); // YooAsset 支持自定义缓存根目录
        initParameters.CacheFileSystemParameters = cacheFileSystemParams;
    }
    // ==========================

    initializationOperation = package.InitializeAsync(initParameters);
}
```

> 注：如果当前 YooAsset 版本的 `CreateDefaultCacheFileSystemParameters` 不支持自定义路径，可降级为在测试前调用 `HotReloadCacheIsolation.ClearCache(...)` 手动清理。

---

## 5. GM 面板 — HotReload Page (`GMHotReloadPageController`)

### 5.1 设计目标

- 利用现有 `GMPanel` 的 `PageView` 体系，新增一个独立 Page 展示 HotReload 信息。
- 风格与 `GMDailyScheduleTestController` 保持一致（`debugText` + 动态生成 `CustomButton`）。
- 支持运行时一键切换环境、清缓存、导出报告、重启场景。
- **不依赖** `HotReloadEnvironmentManager` 等尚未落地的模块，直接基于现有 `HotReloadCtrl`、`GameSettingData`、`YooAssets` 读取状态。

### 5.2 Prefab 结构要求

在 `GMPanel.prefab` 的 `PageView/Content` 下新增一个 Page 节点，结构如下：

```
GMPanel
└── Pages (PageView)
    └── Content
        ├── ... (现有 pages)
        └── HotReloadPage  <-- 新增
            ├── debugText      (TextMeshProUGUI, 全页信息展示)
            ├── buttonContainer (Transform, 按钮挂载点)
            └── buttonTemplate  (CustomButton, 仅一个模板实例，代码会动态复制)
```

将 `GMHotReloadPageController.cs` 挂载到 `HotReloadPage` 节点上，拖拽绑定以上三个引用即可。

### 5.3 代码实现

已实现到：

```
Assets/Game/UI/GM/Scripts/GMHotReloadPageController.cs
```

关键特征：

| 特征 | 说明 |
|---|---|
| **自包含** | 不依赖 `HotReloadEnvironmentManager` 等未落地模块，直接使用 `PlayerPrefs` + `GameSettingData` |
| **实时刷新** | `Update` 每 0.5s 刷新一次 `debugText`，展示环境、CDN、PlayMode、PackageVersion、缓存大小、RemoteConfig 状态 |
| **动态按钮** | 复用 `GMDailyScheduleTestController` 的按钮生成模式，提供 6 个操作：切 Staging / 切 Rehearsal / 切 Prod（带确认保护）/ 清缓存 / 复制报告 / 重启场景 |
| **RichText** | 红绿蓝黄颜色区分状态（`#00C853` 通过、`#FF3B30` 失败、`#FFB300` 警告、`#00B0FF` 信息） |
| **CDN 写入修正** | 切换环境时使用正确的写入顺序（先改内存字段 `setting.cdnUrl`，再 `LocalStorage.SetData`），绕过 `GameSetting.SetCdnUrl()` 的 bug |

### 5.4 使用方式

1. 美术/策划在 `GMPanel.prefab` 的 `PageView/Content` 下复制一个现有 page（如 DailySchedule page），改名为 `HotReloadPage`。
2. 删掉 page 中原有的 DailySchedule 专用内容，保留 `debugText` + `buttonContainer` + `buttonTemplate`。
3. 把 `GMHotReloadPageController.cs` 拖到 `HotReloadPage` 上，绑定三个引用。
4. 运行游戏，打开 GM 面板，滑动/点击导航到 HotReload Page 即可看到实时信息。

---

## 6. 验证矩阵自动化 (HotReloadTestValidator)

### 6.1 设计目标

把流程设计中的 7 个验证场景变成可代码化断言的集合，供自动化测试或 QA 人工对照使用。

### 6.2 代码实现

```csharp
using System;
using System.Collections.Generic;

namespace Game.HotReload.Test
{
    /// <summary>
    /// 热更验证矩阵
    /// 职责：定义每个测试场景的通过标准，支持手动和半自动验证。
    /// </summary>
    public static class HotReloadTestValidator
    {
        public enum TestScenario
        {
            FreshInstallColdStart,      // 新装首次启动
            OldVersionOverrideInstall,  // 旧版本覆盖安装
            OldCacheRestart,            // 已有旧缓存再启动
            NetworkInterrupt,           // 热更中断网
            PostUpdateRestart,          // 热更完成后二次启动
            RemoteConfigEmptyOrTimeout, // RemoteConfig 为空 / 超时
            ManifestUpToDate,           // 资源已更新但不需下载
        }

        [System.Serializable]
        public class ValidationResult
        {
            public TestScenario scenario;
            public bool passed;
            public string reason;
            public List<string> checks = new List<string>();
        }

        /// <summary>
        /// 对一份测试报告执行全量验证
        /// </summary>
        public static List<ValidationResult> ValidateReport(HotReloadTestRecorder.TestReport report)
        {
            var results = new List<ValidationResult>();

            // 通用断言：必须有 INITIALIZE_PACKAGE -> START_GAME 的完整链路
            results.Add(ValidateCompleteChain(report));

            // 通用断言：HostPlayMode 下必须能拿到 packageVersion
            results.Add(ValidatePackageVersionAcquired(report));

            // 通用断言：CDN 不能是空或默认值
            results.Add(ValidateCdnConfigured(report));

            // 场景化断言（根据报告中的上下文信息推断）
            results.Add(ValidateCacheBehavior(report));
            results.Add(ValidateDownloadBehavior(report));
            results.Add(ValidateFailGraceful(report));

            return results;
        }

        private static ValidationResult ValidateCompleteChain(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.FreshInstallColdStart };
            string[] requiredSteps = new[]
            {
                HotReloadStateName.INITIALIZE_PACKAGE,
                HotReloadStateName.REQUEST_PACKAGE_VERSION,
                HotReloadStateName.UPDATE_PACKAGE_MANIFEST,
                HotReloadStateName.CREATE_DOWNLOADER,
                HotReloadStateName.START_GAME
            };

            foreach (var step in requiredSteps)
            {
                bool found = report.steps.Exists(s => s.stepName == step && s.state == "Enter");
                r.checks.Add($"{step} Enter = {found}");
                if (!found)
                {
                    r.passed = false;
                    r.reason = $"Missing step: {step}";
                    return r;
                }
            }
            r.passed = true;
            return r;
        }

        private static ValidationResult ValidatePackageVersionAcquired(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.FreshInstallColdStart };
            bool hasVersion = !string.IsNullOrEmpty(report.packageVersion);
            r.checks.Add($"PackageVersion acquired = {hasVersion}");
            r.passed = hasVersion;
            if (!hasVersion) r.reason = "PackageVersion is empty";
            return r;
        }

        private static ValidationResult ValidateCdnConfigured(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.FreshInstallColdStart };
            bool valid = !string.IsNullOrEmpty(report.cdnUrl) && !report.cdnUrl.Contains("127.0.0.1");
            r.checks.Add($"CDN valid = {valid} ({report.cdnUrl})");
            r.passed = valid;
            if (!valid) r.reason = "CDN not configured or using default localhost";
            return r;
        }

        private static ValidationResult ValidateCacheBehavior(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.OldCacheRestart };
            // 如果 cacheHit=true，则应该没有 DOWNLOAD_PACKAGE_FILES 或下载量极小
            bool hasDownload = report.steps.Exists(s => s.stepName == HotReloadStateName.DOWNLOAD_PACKAGE_FILES);
            if (report.cacheHit && hasDownload)
            {
                r.passed = false;
                r.reason = "Cache hit but still downloaded files";
            }
            else
            {
                r.passed = true;
            }
            r.checks.Add($"CacheHit={report.cacheHit}, HasDownload={hasDownload}");
            return r;
        }

        private static ValidationResult ValidateDownloadBehavior(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.FreshInstallColdStart };
            bool hasDownloader = report.steps.Exists(s => s.stepName == HotReloadStateName.CREATE_DOWNLOADER);
            bool hasDownload = report.steps.Exists(s => s.stepName == HotReloadStateName.DOWNLOAD_PACKAGE_FILES);
            // CREATE_DOWNLOADER 后要么进 DOWNLOAD，要么直接 START_GAME（无更新）
            r.checks.Add($"HasDownloader={hasDownloader}, HasDownload={hasDownload}");
            r.passed = true; // 两种分支都是合法路径
            return r;
        }

        private static ValidationResult ValidateFailGraceful(HotReloadTestRecorder.TestReport report)
        {
            var r = new ValidationResult { scenario = TestScenario.NetworkInterrupt };
            bool hasFail = !string.IsNullOrEmpty(report.failReason);
            if (hasFail)
            {
                // 失败后仍然报告了完整步骤链路，说明 LoadManager 没有卡死
                bool hasStartGame = report.steps.Exists(s => s.stepName == HotReloadStateName.START_GAME);
                r.checks.Add($"Failed but reached START_GAME = {hasStartGame}");
                r.passed = hasStartGame; // 当前设计是失败继续，所以走到 START_GAME 才算符合预期
                if (!hasStartGame) r.reason = "Failed and blocked before START_GAME";
            }
            else
            {
                r.passed = true;
                r.checks.Add("No failure recorded");
            }
            return r;
        }

        /// <summary>
        /// 生成可读的验证报告文本
        /// </summary>
        public static string FormatResults(List<ValidationResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========== HotReload Validation Matrix ==========");
            int passed = 0, failed = 0;
            foreach (var r in results)
            {
                sb.AppendLine($"[{ (r.passed ? "PASS" : "FAIL") }] {r.scenario}");
                if (!r.passed) sb.AppendLine($"       Reason: {r.reason}");
                foreach (var c in r.checks) sb.AppendLine($"       - {c}");
                if (r.passed) passed++; else failed++;
            }
            sb.AppendLine($"Total: {passed} passed, {failed} failed.");
            sb.AppendLine("=================================================");
            return sb.ToString();
        }
    }
}
```

---

## 7. Editor 构建注入工具 (HotReloadBuildInjector)

### 7.1 设计目标

- 构建 APK/IPA 时自动把 `GameSetting.asset` 的 `playMode` 改成 `HostPlayMode`。
- 构建时注入当前 `buildId` 和环境标识。
- 提供 Editor 菜单一键切环境、清缓存、模拟热更。

### 7.2 代码实现

```csharp
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.HotReload.Test.Editor
{
    /// <summary>
    /// 热更测试构建注入器
    /// 职责：构建前自动修正 playMode、注入 buildId。
    /// </summary>
    public class HotReloadBuildInjector : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            // 自动把默认包的 playMode 改成 HostPlayMode（仅当当前 asset 是 EditorSimulateMode 时）
            var setting = GF.GameSettingData.Setting;
            if (setting == null)
            {
                Debug.LogError("[HotReloadBuildInjector] GameSetting asset not found!");
                return;
            }

            var packageElement = setting.GetDefaultPackageElement(setting.defaultPackageName);
            if (packageElement != null && packageElement.playMode == YooAsset.EPlayMode.EditorSimulateMode)
            {
                Debug.LogWarning("[HotReloadBuildInjector] Auto-switching playMode from EditorSimulateMode to HostPlayMode for build.");
                packageElement.playMode = YooAsset.EPlayMode.HostPlayMode;
                EditorUtility.SetDirty(setting);
                AssetDatabase.SaveAssets();
            }

            // 生成 buildId
            string buildId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{report.summary.guid.ToString().Substring(0, 8)}";
            PlayerPrefs.SetString("HotReload_BuildId", buildId);
            Debug.Log($"[HotReloadBuildInjector] BuildId injected: {buildId}");
        }

        [MenuItem("HotReload/Test/Clear All Cache")]
        private static void MenuClearAllCache()
        {
            HotReloadCacheIsolation.ClearAllCache();
        }

        [MenuItem("HotReload/Test/Switch to Staging")]
        private static void MenuSwitchStaging()
        {
            var go = new GameObject("HotReloadEnvMgr_Temp");
            var mgr = go.AddComponent<HotReloadEnvironmentManager>();
            mgr.SwitchEnvironment(HotReloadEnvironment.StagingHotfix, buildId: "editor-test");
            GameObject.DestroyImmediate(go);
        }

        [MenuItem("HotReload/Test/Print Last Test Report")]
        private static void MenuPrintReport()
        {
            string json = PlayerPrefs.GetString("HotReload_LastTestReport", "null");
            Debug.Log($"[HotReloadBuildInjector] Last Report:\n{json}");
        }
    }
}
#endif
```

---

## 8. 对现有代码的最小侵入修改清单

要把这套测试代码跑起来，需要在现有代码中做以下**最小修改**：

### 8.1 LoadManager.cs（3 处插入）

```csharp
// 第 1 处：HotReload() 开头，事件订阅之后
HotReloadTestRecorder.Instance?.StartRecord(
    HotReloadEnvironmentManager.Instance?.CurrentBuildId ?? "unknown",
    HotReloadEnvironmentManager.Instance?.CurrentEnv.ToString() ?? "unknown"
);

// 第 2 处：HotReloadCtrl.Instance.Init 之后
HotReloadTestRecorder.Instance?.RecordStep(
    HotReloadStateName.INITIALIZE_PACKAGE, "Enter",
    $"playMode={packageElement.playMode}, package={packageName}"
);

// 第 3 处：while 循环结束后、yield return 1f 之前
bool hotReloadSuccess = HotReloadCtrl.Instance.IsFinish && !HotReloadCtrl.Instance.IsFailed;
string failReason = HotReloadCtrl.Instance.IsFailed ? "HotReload Failed" :
    (Time.time - hotReloadStartTime > HOT_RELOAD_TIMEOUT ? "Timeout" : null);
HotReloadTestRecorder.Instance?.FinishRecord(hotReloadSuccess, failReason);
HotReloadTestRecorder.Instance?.SaveToPlayerPrefs();
HotReloadTestRecorder.Instance?.DumpToConsole();
```

### 8.2 HotReloadCtrl_InitializePackage.cs（1 处修改 + 1 处接入）

```csharp
// 修改：HostPlayMode 下 generation 从 "0" 改为读取配置
string generation = packageElement.generation; // 替代原来的 string generation = "0";
if (string.IsNullOrEmpty(generation)) generation = "0";

// 追加：缓存隔离（如果 YooAsset API 支持自定义 cacheRoot）
var envMgr = HotReloadEnvironmentManager.Instance;
if (envMgr != null && initParameters.CacheFileSystemParameters != null)
{
    // 通过 YooAsset 提供的 API 注入自定义缓存根（视版本而定）
}
```

### 8.3 HotReloadCtrl 各状态节点（8 处记录）

在每个状态节点的 `OnEnter` 和 `OnExit` 中追加记录：

```csharp
// 示例：HotReloadCtrl_RequestPackageVersion.cs
public override void OnEnter()
{
    HotReloadTestRecorder.Instance?.RecordStep(HotReloadStateName.REQUEST_PACKAGE_VERSION, "Enter");
    // ... 原有逻辑
}
public override void OnExit()
{
    HotReloadTestRecorder.Instance?.RecordStep(HotReloadStateName.REQUEST_PACKAGE_VERSION, "Exit");
    // ... 原有逻辑
}
```

其他 7 个节点同理。

### 8.4 GameSetting.cs（1 处修正）

```csharp
// 修正 SetCdnUrl 的写入顺序 bug
public void SetCdnUrl(string cdn)
{
    // 原代码：先写本地存储旧值，再改内存 → 错误
    // App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN, cdnUrl);
    // cdnUrl = cdn;

    // 修正后：先改内存，再持久化新值
    cdnUrl = cdn;
    App.LocalStorage.SetData(Define.LocalStorage.STORAGE_CDN, cdn);
}
```

---

## 9. 落地顺序建议

| 阶段 | 动作 | 预期产出 |
|---|---|---|
| **P0** | 创建 `Assets/Game/Scripts/HotReload/Test/` 目录，落地 `HotReloadEnvironmentManager`、`HotReloadTestRecorder`、`HotReloadCacheIsolation` | 运行时环境可切换、可记录 |
| **P0** | 在 `LoadManager` 插入 3 处记录代码 | 每次启动自动产出测试报告 |
| **P1** | 落地 `HotReloadDebugPanel`，挂到 GM 面板 | QA 可手动切环境、清缓存、看报告 |
| **P1** | 落地 `HotReloadBuildInjector`，修正 `GameSetting.SetCdnUrl()` | 构建时自动注入 HostPlayMode |
| **P2** | 给 8 个状态节点补全 Enter/Exit 记录 | 报告粒度细化到每个状态 |
| **P2** | 接入 `generation`、接入自定义缓存根 | 与 GF 层对齐版本控制 |
| **P3** | 落地 `HotReloadTestValidator`，接入 CI 或自动化测试框架 | 验证矩阵半自动化 |

---

## 10. 风险与兜底

| 风险 | 兜底方案 |
|---|---|
| `HotReloadEnvironmentManager` 在 `LoadManager.Awake` 之后才初始化 | 把 `EnvMgr` 做成 `MonoSingleton` 或提前到 `EnterGame` 之前初始化 |
| YooAsset 版本不支持自定义 CacheFileSystemParameters 路径 | 降级为测试前手动 `ClearCache`，不阻塞流程 |
| `GF.RemoteServices` 的 generation 接入后影响正式包 | 加编译符号 `#if HOTRELOAD_TEST` 隔离测试代码，或只在 `Staging/Rehearsal` 环境启用 |
| 测试面板误操作切到 Prod | `ProdOnline` 切换加二次确认弹窗；正式包关闭 `_allowRuntimeSwitch` |

---

## 修改记录

- 2026-05-21 v1.0：基于 `HotReload测试流程设计.md` 输出可落地的代码实现，包含环境管理、测试记录、缓存隔离、GM 面板、构建注入和最小侵入修改清单。
