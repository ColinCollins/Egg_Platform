# HotReload 模块分析

创建时间：2026-05-21
分析范围：`Assets/Game/Scripts/HotReload`、`Assets/Game/UI/Loading/Scripts/LoadManager.cs`、`Assets/Game/Scripts/Config/ConfigManager.cs`、`Assets/GF/Scripts/Runtime/Settings/GameSetting.cs` 及 GF 侧相关热更实现。

## 1. 本次分析遵循的知识库工作流

来自知识库 `02-领域知识/AI 工作流规则/` 的要点：

- 先文档，后制作：先梳理模块现状，再落文档。
- 小步闭环：本次只做 HotReload 模块现状分析，不顺带改代码。
- 识别关键模块后扩散：从 `LoadManager -> HotReloadCtrl -> 状态节点 -> ConfigManager -> GameSetting/GF 资源层` 往外看。
- 证据记录：所有结论都对应到当前仓库代码或已有风险报告。
- Git 变更驱动风险评估工作流：重点看调用链、状态链、资源链、失败路径，而不是只看单文件。

对这类模块继续分析时，建议固定按下面顺序走：

1. 找入口：谁触发热更，谁等待热更结束。
2. 找状态机：有哪些状态、每个状态的成功/失败转移是什么。
3. 找资源链：包名、版本、CDN、清单、下载器、缓存清理。
4. 找下游依赖：热更完成后谁依赖它产出的资源。
5. 找失败链：失败后是否中断、降级、超时、继续启动。

## 2. 模块定位

`HotReload` 在当前项目里承担的是“启动阶段资源包初始化与补丁下载”的职责，不是代码热重载。

主入口在 [LoadManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/Loading/Scripts/LoadManager.cs:76)：

- 从 `GameSettingData.Setting.defaultPackageName` 读取默认包名。
- 从 `BuiltinPackageElement.playMode` 读取运行模式。
- 调用 `HotReloadCtrl.Instance.Init(packageName, playMode)` 启动流程。
- 阻塞等待条件是 `HotReloadCtrl.Instance.IsFinish && booster.IsReady`。
- 失败或超时后不会中断启动，而是继续进入 `LoadConfigAsync` 和 `OnPostProcess`。

因此它处于游戏主线最靠前的位置，直接影响：

- 配置表加载。
- 资源系统初始化。
- 主场景进入。
- 后续关卡 prefab、字体、UI 资源是否能被正确加载。

## 3. 当前代码结构

### 3.1 控制器

[HotReloadCtrl.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl.cs:11) 只做三件事：

- 保存上下文：`PackageName`、`PackageVersion`、`PlayMode`、`Downloader`。
- 组装 Bear `StateMachine`。
- 暴露两个终态标记：`IsFinish`、`IsFailed`。

它本身不处理业务逻辑，业务都分散在各个状态节点里。

### 3.2 事件

[HotReloadEvents.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadEvents.cs:7) 定义了 UI/外部观察到的事件：

- `PatchStepsChange`
- `InitializeFailed`
- `PackageVersionRequestFailed`
- `PackageManifestUpdateFailed`
- `FoundUpdateFiles`
- `DownloadUpdate`
- `WebFileDownloadFailed`

`LoadManager` 订阅这些事件后只做文案和日志更新，不改变流程控制权。

### 3.3 状态机顺序

状态名定义在 [HotReloadStateName.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadStateName.cs:6)。

实际顺序如下：

| 顺序 | 状态                        | 主要职责                   | 成功后进入                                   | 失败后结果      |
| ---- | --------------------------- | -------------------------- | -------------------------------------------- | --------------- |
| 1    | `INITIALIZE_PACKAGE`      | 创建/初始化 YooAsset 包    | `REQUEST_PACKAGE_VERSION`                  | `SetFailed()` |
| 2    | `REQUEST_PACKAGE_VERSION` | 获取包版本                 | `UPDATE_PACKAGE_MANIFEST`                  | `SetFailed()` |
| 3    | `UPDATE_PACKAGE_MANIFEST` | 更新 manifest              | `CREATE_DOWNLOADER`                        | `SetFailed()` |
| 4    | `CREATE_DOWNLOADER`       | 构建下载器并统计待下载文件 | `DOWNLOAD_PACKAGE_FILES` 或 `START_GAME` | 无显式失败分支  |
| 5    | `DOWNLOAD_PACKAGE_FILES`  | 执行文件下载               | `DOWNLOAD_PACKAGE_OVER`                    | `SetFailed()` |
| 6    | `DOWNLOAD_PACKAGE_OVER`   | 下载结束后的过渡节点       | `CLEAR_CACHE_BUNDLE`                       | 无              |
| 7    | `CLEAR_CACHE_BUNDLE`      | 清理无用缓存               | `START_GAME`                               | 无显式失败分支  |
| 8    | `START_GAME`              | 标记热更完成               | 结束                                         | 无              |

## 4. 各状态节点细节

### 4.1 `INITIALIZE_PACKAGE`

代码： [HotReloadCtrl_InitializePackage.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_InitializePackage.cs:14)

按 `EPlayMode` 分三条初始化路径：

- `EditorSimulateMode`
  - 用 `EditorSimulateModeHelper.SimulateBuild(...)` 生成模拟 manifest。
- `OfflinePlayMode`
  - 用 `OfflinePlayModeParameters` 初始化本地包。
- `HostPlayMode`
  - 用 `HostPlayModeParameters` 初始化远端包。
  - 注入 `BuildinQueryServices`、`DecryptoHTXOR`、`GF.RemoteServices`。
  - 设置断点续传阈值为 `1MB`。

几个关键点：

- 它调用的是 `GF.RemoteServices`，不是文件底部那个内部 `RemoteServices` 类。底部私有类目前没有被使用。
- Host 模式里把 `generation` 写死成 `"0"`，没有使用 `BuiltinPackageElement.generation`。
- 初始化失败时会派发 `InitializeFailed`，然后 `owner.SetFailed()`。

### 4.2 `REQUEST_PACKAGE_VERSION`

代码： [HotReloadCtrl_RequestPackageVersion.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_RequestPackageVersion.cs:13)

- `OfflinePlayMode` / `EditorSimulateMode`：
  - 直接读取 `package.GetPackageVersion()`。
- `HostPlayMode`：
  - 调用 `package.UpdatePackageVersionAsync()`。

失败条件：

- 本地版本为空。
- 远端请求失败。
- 远端返回成功但版本串为空。

失败后都统一 `SetFailed()`。

### 4.3 `UPDATE_PACKAGE_MANIFEST`

代码： [HotReloadCtrl_UpdatePackageManifest.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_UpdatePackageManifest.cs:13)

- 直接调用 `package.UpdatePackageManifestAsync(packageVersion)`。
- 成功后进入下载器创建。
- 失败后派发 `PackageManifestUpdateFailed` 并 `SetFailed()`。

### 4.4 `CREATE_DOWNLOADER`

代码： [HotReloadCtrl_CreateDownloader.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_CreateDownloader.cs:12)

- 使用 `package.CreateResourceDownloader(10, 3)`。
- `TotalDownloadCount == 0` 时不下载，直接进入 `START_GAME`。
- 否则派发 `FoundUpdateFiles`，再进入下载状态。

这里没有做：

- 磁盘空间检查。
- 用户确认下载大小。
- 网络类型判断。

### 4.5 `DOWNLOAD_PACKAGE_FILES`

代码： [HotReloadCtrl_DownloadPackageFiles.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_DownloadPackageFiles.cs:13)

- 绑定下载错误回调和进度回调。
- 调用 `downloader.BeginDownload()`。
- 下载失败时派发 `WebFileDownloadFailed`，然后 `SetFailed()`。
- 下载成功后进入 `DOWNLOAD_PACKAGE_OVER`。

### 4.6 `DOWNLOAD_PACKAGE_OVER`

代码： [HotReloadCtrl_DownloadPackageOver.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_DownloadPackageOver.cs:11)

- 这是纯过渡节点。
- 只负责切到 `CLEAR_CACHE_BUNDLE`。

### 4.7 `CLEAR_CACHE_BUNDLE`

代码： [HotReloadCtrl_ClearCacheBundle.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_ClearCacheBundle.cs:12)

- 调用 `package.ClearUnusedCacheFilesAsync()`。
- 在 `Completed` 回调里直接进入 `START_GAME`。

这里的特点是：

- 没有检查 `operation.Status`。
- 无论清缓存成功还是失败，都会继续启动。

### 4.8 `START_GAME`

代码： [HotReloadCtrl_StartGame.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/HotReload/HotReloadCtrl_Nodes/HotReloadCtrl_StartGame.cs:11)

- 只做 `owner.SetFinish()`。
- `LoadManager` 看到 `IsFinish` 后才会跳出等待。

## 5. 启动链与下游依赖

HotReload 不是孤立模块，它被嵌在启动队列里：

1. `EnterGame`
2. `HotReload`
3. `LoadConfigAsync`
4. `OnPostProcess`
5. `LoadSceneAsync("Main")`

关键调用点见 [LoadManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/Loading/Scripts/LoadManager.cs:39)。

最关键的下游依赖是配置表加载：

- [ConfigManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/Scripts/Config/ConfigManager.cs:208) 会从当前 YooAsset 包中同步加载 `Assets/Game/Configs/{file}.json`。
- 它先做 `package.CheckLocationValid(streamingPath)`，再 `package.LoadAssetSync<TextAsset>(streamingPath)`。

也就是说，HotReload 的结果直接决定：

- 配置 json 是否存在。
- 资源包版本和配置表版本是否匹配。
- 后续 UI / prefab / level 资源能否按配置正确加载。

## 6. 配置入口与当前仓库状态

配置定义在 [GameSetting.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/GF/Scripts/Runtime/Settings/GameSetting.cs:57)：

- `buildPipeline`
- `builtinPackageList`
- `defaultPackageName`
- `cdnUrl`
- `cdnUrlFallback`

当前仓库中的 [GameSetting.asset](/D:/GitStore/BrinPunk/BrainPunk/Assets/Resources/GameSetting.asset:1) 显示：

- `defaultPackageName = GamePackage`
- `builtinPackageList[0].packageName = GamePackage`
- `builtinPackageList[0].playMode = 0`
- `cdnUrl = gs://brainpunk-52a55.firebasestorage.app`
- `cdnUrlFallback = 空`

`EPlayMode` 枚举定义见 `Assets/GF/Third/YooAsset/Runtime/InitializeParameters.cs`：

- `0 = EditorSimulateMode`
- `1 = OfflinePlayMode`
- `2 = HostPlayMode`

这意味着从当前提交状态看，默认包配置还是 `EditorSimulateMode`。这在 Editor 下正常，但如果出包前没有额外构建流程覆盖它，运行时会走模拟分支而不是正式热更分支。这里需要结合实际出包流程再确认。

## 7. 与 GF 层已有热更实现的关系

当前项目里实际上存在至少两套“资源热更流程”：

- 游戏启动用的 `Game.HotReload.HotReloadCtrl`
- GF 层的 `ProcedureHotUpdate`
- GF 层的 `PackageDownloadOperation`
- GF 层的 `ResKit.InitHostPackage / InitOfflinePackage / InitSimulatePackage`

相关文件：

- [ProcedureHotUpdate.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/GF/Scripts/Runtime/Procedure/ProcedureHotUpdate.cs:1)
- [PackageDownloadOperation.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/GF/Scripts/Runtime/Res/Download/PackageDownloadOperation.cs:1)
- [ResKit.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/GF/Scripts/Runtime/Res/ResKit.cs:1)

这几套逻辑的核心步骤高度相似：

- 初始化包
- 更新版本
- 更新 manifest
- 创建下载器
- 下载
- 清理缓存

但它们并不完全一致：

- `HotReloadCtrl` 使用 Bear 状态机和事件。
- `ProcedureHotUpdate` 用 UniTask 串流程。
- `PackageDownloadOperation` 用自定义 operation 状态机。
- GF 侧实现支持 `generation` 参与远端 `.version` 请求，`HotReloadCtrl` 当前没有真正接入这套参数。

这说明当前项目存在“同一职责多套实现并存”的架构现状，后续维护要先确认哪套才是主线。

## 8. 当前模块的主要问题与风险

### 8.1 风险一：失败后继续启动，错误会后移

现状：

- `LoadManager` 在 `IsFailed` 或超时后仍继续 `LoadConfigAsync` 和 `OnPostProcess`。
- 代码位置见 [LoadManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/Loading/Scripts/LoadManager.cs:98)。

影响：

- 热更失败不会立刻阻断启动。
- 如果内置包不完整、配置版本不匹配、远端资源缺失，真正报错点会后移到配置加载、UI 打开或关卡资源加载阶段。

这个风险和已有风险报告结论一致。

### 8.2 风险二：超时常量与日志文案不一致

现状：

- 超时常量是 `30f`。
- 但日志写的是 `HotReload timeout (60s)`。

位置：

- [LoadManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/Loading/Scripts/LoadManager.cs:97)
- [LoadManager.cs](/D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/Loading/Scripts/LoadManager.cs:103)

影响：

- 排查线上日志时会误判真实等待时长。

### 8.3 风险三：Host 模式没有真正用上 `generation`

现状：

- `GameSetting.BuiltinPackageElement` 里有 `generation`、`yooVersion` 字段。
- `GF.RemoteServices` 也支持把 `generation` 拼到 `.version` 请求上。
- 但 `HotReloadCtrl_InitializePackage` 里把 `generation` 固定成 `"0"`。

影响：

- 热更链路和 GF 资源层已有的版本控制设计没有完全对齐。
- 如果服务端按 `generation` 控制版本，当前实现可能无法拿到预期版本。

### 8.4 风险四：当前默认资源配置是 `EditorSimulateMode`

现状：

- 仓库里的 `Assets/Resources/GameSetting.asset` 把 `GamePackage.playMode` 配成了 `0`。

影响：

- 这在编辑器开发阶段合理。
- 但如果构建流程没有在出包前改写资产，就可能导致运行包走错模式。

说明：

- 这里只能确认“仓库当前资产状态”，不能单凭这一点断言正式包一定有问题。

### 8.5 风险五：清缓存结果不参与流程判定

现状：

- `CLEAR_CACHE_BUNDLE` 只监听 `Completed`，不校验结果状态。

影响：

- 旧缓存未清理时可能留下包版本脏状态。
- 这类问题通常不会立刻表现为启动失败，而会变成偶发资源错配。

### 8.6 风险六：失败 UI 文案粒度过粗

现状：

- `LoadManager` 的四类失败事件几乎都显示成 `HotReload Initialize Failed!`。

影响：

- 用户提示和研发日志都不够精确。
- 一线排查时需要再回日志看真实失败节点。

### 8.7 风险七：`booster.IsReady` 与热更共用一个等待门

现状：

- `while ((!HotReloadCtrl.Instance.IsFinish || !booster.IsReady) && !HotReloadCtrl.Instance.IsFailed)`

影响：

- 热更和 `booster` 就绪被绑在同一个阻塞循环里。
- 一旦超时 `break`，后续流程会同时绕过这两个门，问题来源不容易区分。

## 9. 建议的文档化结论

如果后续要继续治理这个模块，优先级建议如下：

1. 先统一“哪一套热更流程是主线”，避免 `HotReloadCtrl` 和 GF 侧逻辑长期并存。
2. 明确失败策略：失败后是强制退出、重试、离线降级，还是继续启动。
3. 把 `generation / yooVersion / playMode / CDN` 这一组版本参数接成一条完整链路。
4. 给 `LoadManager` 补清晰失败态，区分初始化失败、版本失败、manifest 失败、下载失败、超时失败。
5. 验证当前出包流程是否会在正式构建前改写 `GameSetting.asset` 的 `playMode`。

## 10. 本次分析的证据来源

- 代码：
  - `Assets/Game/Scripts/HotReload/*`
  - `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
  - `Assets/Game/Scripts/Config/ConfigManager.cs`
  - `Assets/GF/Scripts/Runtime/Settings/GameSetting.cs`
  - `Assets/GF/Scripts/Runtime/Res/*`
- 现有文档：
  - `风险评估报告/核心模块功能基线.md`
  - `风险评估报告/2026-04-30-1919-Git变更驱动每日风险评估报告.md`
- 知识库：
  - `AI 工作流规则.md`
  - `Git变更驱动风险评估工作流.md`

## 11. 一句话总结

当前 `HotReload` 模块本质上是“启动前资源包补丁状态机”，主流程已经具备基础容错，但它的失败处理是“继续启动型”的，且与 GF 侧已有热更实现存在重复和参数不一致问题，所以短期内最大的风险不是热更完全跑不起来，而是“热更失败后主流程继续前进，错误在更后面的资源/配置阶段爆出来”。
