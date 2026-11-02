# Postcard_ProgressItem 功能说明

## 需求背景

2026-07-06 需求：完善 `Postcard_ProgressItem` 的编辑器绑定、刷新状态和解锁动画，用于明信片页中单个进度 item 的消耗显示、可解锁红点、遮罩解锁表现和本地引用补齐。

同日补充：用户确认解锁触发时 `blockCost` 可以立即隐藏，不需要等待 `coverImg.fillAmount` 遮罩动画结束；触发后仍用 `clickBtn.Interactable = false` 阻止重复点击。

2026-07-08 补充：`coverImg` 改为按 `UI_mingxinpian_img_<末位为1的数字>_01` 检索与绑定，绑定时默认把 `coverImg.gameObject.active` 设为 false；解锁动画不再走 `coverImg.fillAmount` 遮罩，而是在解锁被接受后激活 `coverImg` 并调用同物体上的 `Postcard_TurnEffect.ClosePage` 表现，完成后 `coverImg` 稳定保持 active=true。

2026-07-09 补充：检查用户新增的解锁本体缩放反馈后，将延迟缩放重新纳入 `_unlockTween` 生命周期管理，避免刷新、关闭或销毁时残留未停止的 DOTween。

2026-07-13 补充：可解锁状态的 `redDot` 按参考参数增加呼吸缩放表现，倍率为 `1.1`、单程时长为 `0.8` 秒、缓动为 `Ease.InOutSine`，并使用无限 `LoopType.Yoyo` 循环。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs`
- 相关脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs`
- 相关 prefab：`Assets/Game/UI/Postcard/Resources/Postcard_ocean.prefab` 等明信片页资源 prefab、`Assets/Game/UI/Postcard/Prefabs/PostcardPopup_GuidePart.prefab`、`Assets/Game/UI/Postcard/Sprites/Pictures/ocean/明信片 拼图.layers.prefab`
- 相关 UI 节点：`clickArea`、`Postcard_CostBlock` / `PostcardCostBlock` 内 TMP 文本、`UI_common_img_gantan_01*` 提示点、进度 item 直接子级明信片图片
- 相关 shader：`Assets/Game/ArtSrc/Shader/UI/UIPageCurl.shader`
- 本次未修改配置表、DB/save、资源加载路径、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件。

## 原有行为基线

- `Postcard_ProgressItem.AutoBind()` 已存在 Odin `[Button("AutoBind")]` 入口，早期只补齐 `clickBtn`、`text`、`coverImg`、`redDot`。
- 用户补充了 `Refresh(bool isUnlock, int unlockCount, int currentStar)`、`blockCost`、`unlockAnimDuration`、`PlayUnlockAnim()` 入口和说明，但 `Refresh` 尚未应用 `isUnlock`，`PlayUnlockAnim()` 仍为空。
- 明信片页 prefab 文本证据显示进度 item 局部层级包含明信片图片、`UI_common_img_gantan_01*`、嵌套 `Postcard_CostBlock` / `PostcardCostBlock` 和 `clickArea`。

## 当前行为

- `Refresh(isUnlock, unlockCount, currentStar)` 是运行时状态同步入口：
  - 停止旧解锁 tween，缓存 `_isUnlock` 和 `_unlockCount`。
  - `text` 显示本进度需要的星星数量。
  - 已解锁时 `coverImg.gameObject.active = true`，隐藏 `blockCost` 和 `redDot`。
  - 未解锁时 `coverImg.gameObject.active = false`，显示 `blockCost`，并在 `currentStar >= unlockCount` 时显示 `redDot`。
  - `redDot` 显示后从原始局部缩放呼吸到 `1.1` 倍，使用 `0.8` 秒 `Ease.InOutSine` 单程与无限 Yoyo；隐藏、解锁、组件禁用或销毁时停止 tween 并恢复原始缩放，重新启用且红点仍显示时恢复动画。
- `PlayUnlockAnim()` 只负责本地 UI 表现：
  - 先停止旧 tween 并把 `_isUnlock` 标记为 true。
  - 立即设置 `clickBtn.Interactable = false` 阻止重复点击，并隐藏 `redDot` 与 `blockCost`。
  - 先激活 `coverImg`，再调用 `Postcard_TurnEffect.TryClosePage(...)` 播放 `ClosePage` 翻页表现。
  - `Postcard_TurnEffect` 缺失、禁用、运行时 shader 缺失、对象未激活或播放启动失败时安全降级为直接完成，避免上层解锁流程卡住。
  - `unlockAnimDuration` 仅保留为 item 本体轻微缩放反馈的时长参考，不再驱动 `coverImg.fillAmount`；缩放反馈延迟 0.6 秒播放，并由 `_unlockTween` 统一停止。
- `PlayUnlockAnim(Action onComplete, bool forceCompleteOnInterrupt = false)` 供 `Postcard_PageItem` 在解锁表现完成后解除 `PostcardPopup` 动画交互锁；回调只表示本地 UI 表现结束，不代表发奖或额外存档完成。仅已保存且非最后一块的普通解锁可传 true，使 TurnEffect 因禁用/销毁中断时强制完成回调；默认 false 保持取消语义。
- `OnDisable()` / `OnDestroy()` 会清理解锁 tween；当前清理先使用普通 `Kill()` 停止 tween，再把本体显式恢复到 `Awake()` 缓存的初始局部缩放。若关闭时已进入解锁态，`OnDisable()` 会把 `coverImg` 收敛为 active=true 并隐藏 `blockCost`，避免下次显示残留消耗块。
- 点击 `AutoBind` 时，工具在当前 item 局部层级中查找并写入：
  - `clickBtn`：名为 `clickArea` 的子物体上的 `CustomButton`
  - `text`：当前 item 子级中的第一个 `TMP_Text`，用于嵌套 `Postcard_CostBlock` 的数量文本
  - `blockCost`：优先查找 `PostcardCostBlock` / `Postcard_CostBlock`，失败时回退为 `text` 的父节点
  - `redDot`：名称以 `UI_common_img_gantan_01` 开头的子物体
  - `coverImg`：当前 item 直接子节点中的 `Image`，排除 `clickArea` 和 `redDot` 后，优先取 GameObject 名称或 sprite 名称符合 `UI_mingxinpian_img_<末位为1的数字>_01` 的对象；找不到时才回退到第一个直接 `Image`
  - `turnEffect`：优先取 `coverImg` 同物体上的 `Postcard_TurnEffect`
  - `coverImg.gameObject`：绑定时默认设为 inactive，并记录 Undo / prefab override
- 只在发现实际字段变化时调用 `Undo.RecordObject`、`EditorUtility.SetDirty`，并在 prefab instance 上记录 property override。
- `UnityEditor` API 与辅助查找方法均位于 `#if UNITY_EDITOR` 内，不进入移动端运行时构建。
- `index` 暂不自动绑定。当前证据只显示节点名为 `组13/组14/组15` 且空间位置不等同于简单 sibling 顺序，解锁顺序仍需人工确认。

## 数据与资源流

- `Refresh` 和 `PlayUnlockAnim` 只修改当前 item 的 `TMP_Text.text`、`GameObject.SetActive`、`Image.fillAmount` 兼容值和本地 `Postcard_TurnEffect` 表现。
- `PlayUnlockAnim` 在解锁触发时立即隐藏 `blockCost`，翻页表现完成回调只负责通知调用方；本体缩放反馈是当前 item 的局部 DOTween，不新增每帧轮询或全局事件。
- `redDot` 呼吸反馈是绑定红点 Transform 上的局部 DOTween，只在 PlayMode 且红点处于 active hierarchy 时创建；不修改 prefab 参数，不创建 GameObject，也不接入全局红点管理器。
- AutoBind 只读取当前 GameObject 的 Transform 层级和已存在组件，只写入当前 `Postcard_ProgressItem` 的本地序列化字段。
- `coverImg` 自动绑定只做名称和 sprite 名称的本地字符串判断，不读取资源路径、不加载图片资源，也不扫描其它页面。
- 本组件不创建 GameObject，不实例化 prefab，不读取配置，不访问 `DB` / save，不触发资源加载、红点系统、广告、IAP、analytics 或平台 SDK。

## 验证记录

- 2026-07-08 静态映射确认 `PostcardPopup_GuidePart.prefab`、6 个 Resources 明信片页 prefab 和 `Sprites/Pictures/ocean/明信片 拼图.layers.prefab` 中的 `coverImg` 均指向 `*_1_01`，且这些目标 GameObject 当前 `m_IsActive: 0`。
- 2026-07-08 重新评估当前 `Postcard_TurnEffect.cs` 后，相关 prefab 已移除旧版 `pageCurlMaterial` 序列化残留；静态计数确认上述 prefab 中 `Postcard_ProgressItem` 与 `Postcard_TurnEffect` 数量一致，且不再命中旧错误脚本 GUID `4ead0353ac43aa743ab637684837defb`。
- 2026-07-08 静态关键字扫描确认 touched scripts 未新增 `Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB.`、`Save`、`RemoteConfig`、`Instantiate`、`PlayEvents` 链路；`Postcard_TurnEffect.OnValidate` 仅在 PlayMode 刷新私有材质参数。
- 本次按最新确认的 UI 表现目标调整为解锁触发时立即隐藏 `blockCost`，不再等待 `coverImg.fillAmount` 动画完成。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem风险评估.md.meta DevelopMD/已有功能设计与使用记录.md Assets/Game/UI/Postcard/Prefabs/PostcardPopup_GuidePart.prefab Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab Assets/Game/UI/Postcard/Resources/Postcard_food.prefab Assets/Game/UI/Postcard/Resources/Postcard_forest.prefab Assets/Game/UI/Postcard/Resources/Postcard_museum.prefab Assets/Game/UI/Postcard/Resources/Postcard_ocean.prefab Assets/Game/UI/Postcard/Resources/Postcard_universe.prefab Assets/Game/UI/Postcard/Sprites/Pictures/ocean/明信片 拼图.layers.prefab` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 warnings / 0 errors。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-09 静态敏感扫描 `Postcard_ProgressItem.cs` 未命中 `OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics`、`PlayerPrefs`、`DB.`、`SavePostcard`、`Instantiate`。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-13 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs` 通过，仅有工作区 LF/CRLF 转换提示；敏感关键字扫描未命中编辑器生命周期、每帧轮询、全局事件、DB/save、RemoteConfig、PlayerPrefs 或实例化链路。
- 2026-07-13 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，98 warnings / 0 errors。

## 剩余风险

- 尚未在 Unity Editor Inspector 中实际点击 `AutoBind` 验证进度 item 的写入结果、coverImg 默认 inactive、Undo 和 prefab override 记录。
- 尚未在 Unity PlayMode 中验证 `Refresh` 的可解锁红点、已解锁 coverImg 显示和 `PlayUnlockAnim` 翻页表现。
- 尚未在 Unity PlayMode / Android / iOS 横屏中实际验证点击解锁后立即隐藏 `blockCost`、激活 `coverImg` 与 `ClosePage` 翻页的视觉表现。
- 尚未在 Unity PlayMode / Android / iOS 横屏中实际确认 `redDot` 的 `1.1` 倍、`0.8` 秒 InOutSine 无限 Yoyo 呼吸节奏，以及弹窗反复开关、切页和解锁时的缩放复位观感。
- Resources prefab 的 YAML 更新为静态受控修改；仍需 Unity 打开 prefab 确认 `Postcard_TurnEffect` 组件、Image 引用和 shader 运行时可用性无 Missing / override 异常。
- 如果后续 prefab 更换红点或消耗块命名，或让 `coverImg` 不再是直接子节点 Image，当前绑定规则需要同步调整。
- `index` 仍依赖人工配置；若后续需要自动计算解锁顺序，应先明确排序依据并补验证。

## 回滚方式

- 回退 `Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs` 中 `Refresh` / `PlayUnlockAnim` 的实现、`turnEffect` 字段、coverImg active 控制、`blockCost` AutoBind 和编辑器辅助方法，并回退相关 prefab 中 `coverImg` / `turnEffect` / `m_IsActive` 修改，即可恢复到旧遮罩动画状态。
- 若只回退本次红点表现，移除 `_redDotTween`、基础缩放缓存、`PlayRedDotTween()` / `StopRedDotTween()` 及其在刷新和生命周期入口中的调用，即可恢复为仅控制 `redDot` 显隐。
- 删除本说明文档及对应 `.meta` 可移除本次文档记录。

## 2026-07-14：redDot 增加 PostcardEnter 同节奏旋转抖动

### 需求与影响范围

- 需求：参考 `PostcardEnter` 感叹号上的 `ShakeRotateHandle` 表现，为 `Postcard_ProgressItem.redDot` 增加相同节奏的旋转抖动。
- 本次仅修改 `Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs`；不修改明信片 prefab、配置、DB/save、资源路径、全局事件、分析打点或平台 SDK。

### 原有行为与当前行为

- 原有行为：可解锁时 `redDot` 只播放 `1.1` 倍、`0.8` 秒单程、`Ease.InOutSine` 的无限呼吸缩放。
- 当前行为：保留原呼吸缩放，并追加与 `PostcardEnter.prefab` 一致的旋转参数：单次 `0.3` 秒、Z 轴最大 `18` 度、`vibrato = 5`、`randomness = 10`、衰减开启、`Ease.Linear`；每轮连续摇晃两次，结束后等待 `0.7` 秒并无限循环。
- 旋转序列使用 unscaled time，保持与 `PostcardEnter` 的 `ignoreTimeScale = true` 一致。首次播放时缓存红点基础本地欧拉角；刷新隐藏、解锁、组件禁用或销毁时 Kill 缩放与旋转 Tween，并恢复基础缩放和角度。

### UI 流程、验证与回滚

- `Refresh(...)` 仍是显隐入口；只有未解锁且当前星星数量达到消耗时才显示红点并启动两种 Tween。解锁接受后沿用现有流程先停止并隐藏红点，再播放翻页表现。
- 已执行目标脚本 `git diff --check` 和敏感项静态扫描；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 个错误、98 个警告。
- 尚未在 Unity PlayMode、Android 或 iOS 横屏中目视确认呼吸缩放与旋转双摇的叠加观感、暂停态节奏、切页/关闭/解锁时的角度复位和持续循环开销。
- 回滚方式：移除 `_redDotShakeTween`、基础角度缓存、`PlayRedDotTween()` 中的 `DOShakeRotation` Sequence，以及 `StopRedDotTween()` 中对应的 Kill/角度复位，即可恢复为仅呼吸缩放。

## 2026-09-11 非最终解锁中断回调收口

- `Postcard_PageItem` 在普通非最终块调用 `PlayUnlockAnim` 时传入 `forceCompleteOnInterrupt: true`；最后一块与 `Postcard_GuidePart` 仍使用默认 false。
- 本组件只把该状态转交给同节点 `Postcard_TurnEffect.TryClosePage`，不改变扣星、`DB.SavePostcard()`、奖励、引导、资源加载或全局事件。
- 当普通块翻页表现因组件禁用或销毁中断时，TurnEffect 强制执行本组件既有完成回调，进而释放 `PostcardPopup.IsPagePlayingAnim` 与 ClickBlock；表现中断后 coverImg 仍按既有逻辑稳定显示已解锁状态。
- 已做静态调用点检查；Unity PlayMode 的禁用/销毁故障注入、快速关闭重开和 Android/iOS 横屏触摸仍待验证。回滚时删除此处的 true 传参与 TurnEffect 的对应参数即可。

## 2026-09-20 解锁缩放回弹稳定收口

### 需求与原有行为

- 用户实测 `Postcard_ProgressItem` 解锁缩放最终回弹存在偏差，运行时可观察到相关组节点的局部缩放没有稳定回到 `1`。
- 原实现把 item 根节点直接 `DOScale(Vector3.one * 1.1f)` 并依赖两次 Yoyo 与 `Kill(true)` 回到起点，没有独立缓存或显式恢复初始缩放。
- `Postcard_CostBlock/clickArea` 的 `ClickScaleAnim` 实际只缩放子级点击节点；解锁接受后 `blockCost` 会立即隐藏，可能在该子级 0.1 秒抬起回弹完成前禁用它。

### 当前行为与边界

- `Awake()` 缓存当前 item 根节点的初始 `localScale`。解锁脉冲以该缩放的 `1.1` 倍为目标，正常完成、`Refresh()`、组件禁用或销毁时都显式恢复缓存值，不再依赖 DOTween 中断完成语义。
- 隐藏 `blockCost` 前先把已绑定 `clickBtn` 的局部缩放恢复为 `Vector3.one`，与当前 `Postcard_CostBlock.prefab` 中 `ClickScaleAnim.scaleTarget = null` 的常态约定一致，避免隐藏层级保留按下态缩放。
- 未修改共享 `ClickScaleAnim`、`CustomButton`、明信片 prefab、翻页/整页完成动画、配置、DB/save、奖励、事件、资源或平台逻辑。页面完成动画仍可把父级 `Root` 缩放到 `0.75`；该视觉缩放不改变 item 自身恢复后的局部缩放。

### 验证、风险与回滚

- Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` Roslyn 编译退出码为 0；目标差异格式检查及缩放收口静态断言通过。
- 尚未在 Unity PlayMode、Android 或 iOS 横屏中目视确认普通解锁、最后一块解锁、快速关闭/切页和完成动画衔接，运行时观感仍为 `uncertain`。
- 回滚时移除 `_unlockBaseScale` 缓存、完成/停止时的显式恢复以及隐藏消耗块前的 `clickBtn` 缩放复位，并恢复 `Vector3.one * 1.1f` 与 `Kill(true)` 即可；不涉及 prefab 或存档迁移。
