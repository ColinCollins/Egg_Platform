# PostcardEnter 活动入口功能说明

## 需求背景

`PostcardEnter` 脚本注释要求作为明信片界面活动入口：`CustomButton` 点击打开 `PostcardPopup`；`OnEnable` 时检查 `RemoteConfig` 配置 `IsOpenPostcard` 是否为 `true`，若为 `false` 则不显示 `gameObject`。本次按注释补齐运行时实现，并保留原注释文本不改。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/PostcardEnter.cs`
- 入口 prefab：`Assets/Game/UI/Postcard/Prefabs/PostcardEnter.prefab`
- 打开界面：`PostcardPopup.Create()`
- 配置读取：`ConfigManager.RemoteConfig.IsOpenPostcard()`
- 未修改 `PostcardPopup`、`PostcardManager`、`Postcard_Star`、存档字段、Luban 配置、RemoteConfig 字段、资源加载、analytics、广告、IAP 或平台 SDK。

## 原有行为基线

- `PostcardEnter.cs` 只有类注释和空的 Unity 模板方法，没有按钮绑定或活动开关判断。
- `PostcardEnter.prefab` 根节点绑定 `PostcardEnter`，子节点 `clickArea` 绑定 `CustomButton`。
- `ConfigManager.RemoteConfig.IsOpenPostcard()` 已存在，优先读取 `level_config.isOpenPostcard`，否则回退到 `GlobalConst.IsOpenPostcard`。
- `PostcardPopup.Create()` 已存在，使用 `UIManager.Instance.OpenUI<PostcardPopup>(..., UILayer.Normal)` 打开界面。

## 当前行为

- `OnEnable()` 仅在 `Application.isPlaying` 时执行运行时逻辑，避免编辑器态 prefab 预览读取 DB/RemoteConfig 缓存链路。
- 若 `ConfigManager.RemoteConfig.IsOpenPostcard()` 返回 `false`，入口根物体立即 `SetActive(false)`。
- 若活动开启，脚本通过 `GetComponentInChildren<CustomButton>(true)` 解析入口按钮，并绑定点击事件。
- 点击入口按钮后调用 `PostcardPopup.Create()` 打开明信片弹窗。
- `OnDisable()` 会解除按钮事件绑定，避免对象反复启用后重复订阅。

## 数据、资源、UI 流

1. `PostcardEnter` 随 prefab 激活触发 `OnEnable()`。
2. 脚本读取 `ConfigManager.RemoteConfig.IsOpenPostcard()`。
3. 配置关闭时隐藏入口；配置开启时解析 `CustomButton` 并绑定点击。
4. 玩家点击入口按钮。
5. `PostcardPopup.Create()` 打开 `PostcardPopup` UI。

## 验证记录

- 静态检查确认 `PostcardEnter.cs` 的原类注释文本未被改写，原 Unity 模板注释也已保留。
- 静态 prefab 证据：`PostcardEnter.prefab` 根节点绑定 `PostcardEnter`，子节点 `clickArea` 绑定 `CustomButton`。
- 静态链路证据：`ConfigManager.RemoteConfig.IsOpenPostcard()`、`PostcardPopup.Create()` 与 `CustomButton.OnClick` 均在项目内存在。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardEnter.cs Assets/Game/UI/Postcard/Scripts/PostcardEnter功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardEnter风险评估.md Assets/Game/UI/Postcard/Scripts/PostcardEnter功能说明.md.meta Assets/Game/UI/Postcard/Scripts/PostcardEnter风险评估.md.meta DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，结果为 0 error / 97 warnings。

## 剩余风险

- 尚未运行 Unity PlayMode 验证活动开启时入口可见并能打开 `PostcardPopup`。
- 尚未运行 Unity PlayMode 验证活动关闭时入口隐藏后不影响所在 UI 布局。
- 尚未在 Android/iOS 横屏设备验证入口点击区域、弹窗打开层级和触摸表现。
- 若后续 `PostcardEnter.prefab` 的 `CustomButton` 不再位于子节点，当前自动解析仍可能失效，需要同步调整脚本或显式序列化绑定。

## 回滚方式

- 回退 `PostcardEnter.cs` 中新增的 `using Game.ConfigModule`、按钮缓存、`OnEnable()`、`OnDisable()` 和打开弹窗逻辑。
- 删除本说明文档及对应 `.meta`。

## 2026-07-13 空通关列表异常修复

### 需求背景与影响范围

- 玩家关闭 `BeginerPopup` 后，Bear UI 会重新显示下层 UI，`PostcardEnter.OnEnable()` 随入口重新激活而执行。
- 当明信片活动开启且 `DB.GameData.PassedLevels` 尚无元素时，原 `IsLockLevel()` 直接调用 `Max()`，抛出 `InvalidOperationException: Sequence contains no elements`。
- 本次仅修改 `PostcardEnter.cs` 的关卡门槛判断；未修改 `BeginerPopup`、Bear UI、DB 字段、存档写入、RemoteConfig、prefab、资源、事件、analytics 或平台 SDK。

### 原有行为与修复后行为

- 原行为：`PassedLevels` 非空时取最大已通关关卡与 `PostcardGuideLevel` 比较；列表为 null 或空时无法完成判断并中断 UI 显示链。
- 修复后：列表非空时保持原 `Max()` 比较；列表为 null 或空时使用最高通关关卡 `0` 继续比较。
- `PostcardGuideLevel > 0` 时，无通关记录的入口仍保持锁定；门槛为 `0` 时入口可按原默认配置语义开放。
- 修复没有新增公开 API、序列化字段、日志、运行时实例化或每帧逻辑。

### 数据与 UI 流

1. `PostcardEnter.OnEnable()` 仍先读取 `IsOpenPostcard()`；活动关闭时短路，不执行关卡门槛判断。
2. 活动开启时读取既有 `DB.GameData.PassedLevels` 和 `GetPostcardGuideLevel()`，不修改或保存数据。
3. 空通关记录按 `0` 比较门槛，锁定时仍由现有逻辑隐藏入口；未锁定时继续绑定按钮并刷新红点。

### 验证记录

- CodeGraph 确认 `IsLockLevel()` 只有 `PostcardEnter.OnEnable()` 一个调用点，本次修改局限于入口本地判断。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardEnter.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 使用空 `PassedLevels` 存档复现关闭 `BeginerPopup` 的完整 UI 栈流程；Android/iOS 横屏也未验证。
- 若产品规则要求“门槛为 0 时，无通关记录仍必须锁定”，需要把空列表分支改为直接返回锁定；当前实现按原数值比较的默认基线处理。
- 回滚时恢复 `DB.GameData.PassedLevels.Max()` 的原实现即可；该回滚会重新暴露空列表异常。

## 2026-08-25 StartPanel 每次打开主动刷新入口

### 需求背景与影响范围

- `StartPanel` 每次 `OnOpen()` 时主动刷新 `PostcardEnter`，确保首页复用打开后按当前活动、关卡门槛和引导状态更新入口显隐。
- 修改文件为 `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`；复用 `PostcardEnter.Refresh()`，未修改 prefab、RemoteConfig 字段、DB 结构、资源地址、全局事件、analytics 或平台 SDK。

### 原有行为与当前行为

- 原有行为：入口仅依赖自身 `OnEnable()` 刷新；若入口已因条件不满足执行 `SetActive(false)`，再次打开仍复用的 `StartPanel` 时，该 inactive 节点不会自行触发 `OnEnable()`，状态可能停留在上次判断结果。
- 当前行为：`StartPanel.OnOpen()` 通过 `GetComponentInChildren<PostcardEnter>(true)` 找到包含 inactive 状态的入口并缓存引用，每次打开调用 `Refresh()`。
- `Refresh()` 继续统一判断 `IsOpenPostcard()`、关卡解锁门槛和 `DB.Postcard.HasGuide`，并负责按钮绑定、入口显隐与红点刷新；没有新增第二套状态规则。

### 数据、UI 与生命周期流

1. `StartPanel.OnOpen()` 刷新 DrawGame 入口后解析或复用 `PostcardEnter` 缓存。
2. 调用 `PostcardEnter.Refresh()` 只读现有 RemoteConfig 与 DB 状态。
3. 条件不满足时入口保持隐藏；条件满足时入口重新激活、绑定按钮并刷新红点。
4. 首次创建时可能同时经过 `PostcardEnter.OnEnable()` 与 `StartPanel.OnOpen()`；按钮绑定前已有解除操作，不会累积重复回调。

### 验证、剩余风险与回滚

- 静态确认 `StartPanel.prefab` 中存在名为 `PostcardEnter` 的嵌套 prefab 实例，自动生成字段未提供该引用，因此运行时查询使用 `includeInactive = true`。
- 静态确认没有新增 `Update()`、实例化、存档写入或全局事件。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 106 warning。
- 目标文件 `git diff --check`：通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：尚未执行 Unity PlayMode 的开关 true/false、解锁门槛跨越、引导完成前后及首页反复打开验证；Android/iOS 横屏仍待 smoke。
- 回滚时删除 `StartPanel` 中的 `postcardEnter` 缓存字段及 `OnOpen()` 刷新调用，不需要迁移数据或回退 prefab。
