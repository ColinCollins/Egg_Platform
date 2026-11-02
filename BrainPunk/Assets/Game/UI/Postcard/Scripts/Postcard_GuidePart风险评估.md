# Postcard_GuidePart 风险评估

## 原始行为基线

- `Postcard_GuidePart` 只有注释和序列化字段，不参与运行时。
- `PostcardPopup` 已有 `guidePart` 字段，但主 prefab 原先未绑定该字段。
- `Postcard_PageItem` 是明信片 item 解锁的唯一运行时入口，包含 star 校验、`DB.SavePostcard()`、失败回滚、扣星、动画、整页完成与奖励流程回调。
- `NormalGuidePopup` 已使用 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding` 作为引导状态，不需要新增存档字段。

## 影响文件与资源

- `Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs`
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`
- `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
- `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab`
- `Assets/Game/UI/Postcard/Prefabs/PostcardPopup_GuidePart.prefab`

## 已使用的既有系统

- `Bear.Logger.IDebuger` 和 `this.LogWarning(...)`：用于缺失绑定、无效状态和保存失败提示。
- `DB.Postcard` / `DB.SavePostcard()`：复用已有 guide state 与明信片进度存档。
- `Postcard_PageItem`：保持解锁校验、扣星、保存和动画权威，并向 guide 层提供只读的 cost/currentStar/isUnlock 显示状态。
- `CustomButton.OnClick`：局部绑定/解绑 guide item 点击，不新增全局事件。

## 风险项

### P1-1 pending guide state 保存失败后可能再次触发

- 风险：`PostcardPopup` 启动或跳过 guide 后会把 `IsGuiding=false` 并保存；如果 `DB.SavePostcard()` 失败，当前运行时内存已经变为 false，但重启后仍可能从旧存档读到 true。
- 当前处理：保存失败记录 warning，不阻塞正常 popup 使用；缺失 guidePart 也会消费 pending state，避免软锁。
- 回滚条件：发现 guide 在重启后反复出现且无法继续正常使用。
- 待验证：模拟 `DB.SavePostcard()` 失败后关闭重开与重启读取。

### P1-2 guide item 与真实 page item 下标不一致会解锁错误目标

- 风险：当前实现按 guide prefab 序列化 `items` 列表下标映射到真实页面运行时 item 列表下标；真实页面列表会按 `Postcard_ProgressItem.Index` 排序。
- 当前处理：每次推进都会校验 guide item、guide button 和真实 page item 都存在；缺失时跳过无效目标。`Postcard_ems.prefab` 的真实 item index 已修正为 `0..4`，与当前 guide prefab 的序列化列表顺序一致。
- 静态证据：`Postcard_ems.prefab` 修复前 5 个真实 `Postcard_ProgressItem.index` 均为 `0`，而配置 `Postcard_ems.unlockProgress` 为 `0..4`；修复后真实 item index 为 `0,1,2,3,4`。
- 回滚条件：PlayMode 发现点击 guide 目标后真实页面解锁的图片与指示目标不一致。
- 待验证：Unity Inspector / PlayMode 对照 guide item 顺序、真实页面 item 顺序和视觉位置；其它主题页仍需单独确认。

### P1-3 duplicate click 或延迟回调可能推进多次

- 风险：active guide item 被快速点击，或 unlock 动画完成回调在 guide 已停止后返回，可能导致重复扣星、重复保存或旧 guide 推进。
- 当前处理：`_isUnlocking` 会在 accepted unlock 期间禁用 guide click；guide 现在等待真实页面稳定回调和 guide 层 item 动画都结束后再推进；`OnGuidedUnlockStable(...)` 与 guide 动画回调都通过当前 unlock index 做状态保护，`OnDisable()` 清空完成回调。
- 回滚条件：快速点击导致扣星两次、存档写入两次或 guide index 跳过。
- 待验证：PlayMode 快速连点 active target、guide item 动画尚未结束时关闭或切页、页面稳定回调早于 guide 动画完成时的推进状态。

### P1-6 guide 遮罩层不播放自身动画导致玩家看不到解锁表现

- 风险：真实 `Postcard_PageItem` 在遮罩层下方播放解锁动画时，玩家看到的是 guide 层复制出来的 item；如果 guide 层 item 不同步播放动画，会表现为点击后没有 progressItem 动画。
- 当前处理：真实页面接受 guided unlock 后，guide 层对应 `Postcard_ProgressItem` 同步调用 `PlayUnlockAnim(...)`；guide 同时等待页面稳定与 guide 可视动画完成后再移动 finger。
- 回滚条件：guide 层动画遮挡真实页面表现、动画时长与页面表现严重不一致，或导致 guide 推进明显变慢。
- 待验证：PlayMode 实际点击第一项和中间项，确认 guide item 遮罩动画、finger 移动和真实页面存档进度一致。

### P1-7 guide item cost 与真实页面配置不一致

- 风险：guide 层持有的是复制出来的 `Postcard_ProgressItem`，如果不从真实 `Postcard_PageItem` 读取 `unlockProgress` 和当前星星数，cost 文本、红点和遮罩状态会与真实页面不一致。
- 当前处理：`Postcard_PageItem.TryGetGuideProgressViewStateByListIndex(...)` 只读返回真实目标 `isUnlock`、`unlockCount` 和 `currentStar`；`Postcard_GuidePart` 在启动、目标查询和解锁失败降级时用该状态刷新 guide item。
- 回滚条件：新增只读 API 导致 guide 层显示与真实页面仍不一致，或暴露 API 被其它模块误用。
- 待验证：PlayMode 对照每个 guide item cost 文本、红点状态、星星不足提示和真实页面 `Postcard_PageItem.Refresh()` 的显示一致性。

### P1-4 真实页面普通点击锁未释放会造成 popup 卡住

- 风险：Guide 开始后调用 `SetGuideInteractionLocked(true)`；若停止、关闭、切页或销毁时未释放，普通 item 点击会被长期拦截。
- 当前处理：`StopGuide()`、`OnDisable()`、`OnDestroy()` 和 `StopGuideRuntime()` 均释放 `_runtimePageItem.SetGuideInteractionLocked(false)`。
- 回滚条件：guide 关闭后普通明信片 item 不能点击。
- 待验证：guide 完成、手动关闭 popup、切到 unlock 占位页、重新打开 popup。

### P1-5 page item 解锁 parity 可能被 guide 入口破坏

- 风险：如果 guide 入口复制解锁逻辑，可能绕过 star 校验、save rollback、整页完成或奖励流程。
- 当前处理：`TryUnlockGuidedItemByListIndex(...)` 只解析目标并调用原 `TryUnlockItem(item, onStable)`；原普通点击入口继续委托同一私有方法。
- 回滚条件：guide 解锁的 star 花费、存档、动画或整页完成流程与普通点击不同。
- 待验证：足星、星星不足、保存失败、已解锁、最后一个 item 完成页面。

### P2-1 finger 坐标转换在不同 Canvas / 横屏设备上可能偏移

- 风险：finger 使用 active guide item 中心的 world position 转换到 finger parent 本地坐标；Canvas 缩放、父节点 pivot 或不同横屏适配可能导致视觉偏移。
- 当前处理：只在 guide start 和 active target 改变时刷新；缺失 finger 或 target 时隐藏 finger，不阻塞 popup。
- 回滚条件：finger 明显偏离目标中心或遮挡错误目标。
- 待验证：Unity PlayMode 多分辨率、Android/iOS 横屏设备。

### P2-4 fingerPs 循环粒子未绑定或层级不一致

- 风险：`fingerPs` 依赖 prefab/Inspector 绑定或 finger 子节点存在 `ParticleSystem`；当前静态搜索未在 `PostcardPopup_GuidePart.prefab` 或 `PostcardPopup.prefab` 中看到 `fingerPs` 绑定项，若 finger 子节点也没有粒子则不会出现表现。若粒子不是 finger 子节点，Canvas 层级、缩放和渲染排序也可能与 finger 不一致。
- 当前处理：`Postcard_GuidePart` 只在 finger 有有效目标且组件处于 PlayMode 时启动协程，先延迟 0.5 秒，再每 1 秒清空并重播一次 `fingerPs`；隐藏 finger、目标无效、停止 guide、禁用或销毁时停止并清空。未手动绑定时会尝试从 finger 子节点查找 `ParticleSystem`，仍未找到则 no-op，不影响 guide 解锁流程。
- 回滚条件：粒子循环导致点击提示遮挡错误目标、频繁重播影响视觉节奏，或关闭 guide 后粒子残留。
- 待验证：Unity Inspector 绑定、PlayMode 确认延迟 0.5 秒且间隔 1 秒循环、关闭/切页/完成后粒子停止、Android/iOS 横屏层级和位置。

### P2-2 prefab 绑定缺失会让引导无法出现

- 风险：主 `PostcardPopup.prefab` 或 guide prefab 丢失绑定时，pending guide state 会被消费但玩家看不到局部引导。
- 当前处理：主 prefab 已静态绑定嵌套 guidePart；运行时仍保留 `GetComponentInChildren<Postcard_GuidePart>(true)` 兜底。缺失时记录 warning 并消费 pending state，保护正常 popup。
- 回滚条件：绑定导致 prefab 无法导入或 popup 层级异常。
- 待验证：Unity Inspector 检查嵌套 prefab instance、PlayMode 打开 popup。

### P2-3 Unity Editor 运行验证仍缺失

- 风险：`dotnet build` 能覆盖 C# 编译，但不能证明 Unity Inspector 绑定、prefab instance override、Canvas 粒子渲染层级和 PlayMode 时序正确。
- 当前证据：2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 已通过，结果为 99 warnings / 0 errors。
- 回滚条件：Unity Editor 打开 prefab 或 PlayMode 触发 `Postcard_GuidePart` 时出现脚本异常、粒子不停止或引导无法继续。
- 待验证：Unity Editor PlayMode、prefab Inspector 绑定、Android/iOS 横屏 smoke。

## 验证证据

- `requirements.md` checklist：16/16 PASS。
- `check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`：因当前分支 `codex/postcard-guide-part` 不符合 SpecKit 默认 `NNN-name` 规则失败；`-PathsOnly` 可正确解析 `specs/035-postcard-guide-part`。
- `git diff --check`：脚本与 prefab 静态检查通过，仅有 LF/CRLF 提示。
- 静态敏感扫描：未新增 editor lifecycle、per-frame polling、global event、RemoteConfig、Ads、IAP、Analytics、PlayerPrefs。
- 原注释保护：`Postcard_GuidePart` 顶部原注释块在当前 diff 中未被改写。
- prefab 静态证据：`PostcardPopup.prefab` 的 `guidePart` 指向嵌套 `PostcardPopup_GuidePart` stripped component；guide prefab 有 5 个 items 和 finger。
- 2026-07-08 静态证据：`Postcard_ems.prefab` 修复前真实 item index 全为 `0`，`tbpostcardconfig.json` 对应配置为 `0..4`；修复后 `Postcard_ems.prefab` 真实 item index 为 `0..4`。
- 2026-07-08 静态代码检查：`Postcard_GuidePart` accepted unlock 后会调用 guide item `PlayUnlockAnim(...)`，并通过 `_isPageUnlockStable` / `_isGuideUnlockVisualStable` 双条件推进。
- 2026-07-08 静态代码检查：`Postcard_GuidePart` 修复前没有读取真实页面 cost，只在跳过已解锁项时调用 `guideItem.Refresh(true, 0, 0)`；现已改为通过 `Postcard_PageItem.TryGetGuideProgressViewStateByListIndex(...)` 同步 cost/currentStar/isUnlock。
- 2026-07-08 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有 Git LF/CRLF 提示。
- 2026-07-08 静态敏感扫描 `Postcard_GuidePart.cs` 和 `Postcard_PageItem.cs` 未命中编辑器生命周期、每帧轮询、全局事件、RemoteConfig、IAP、Ads、Analytics 或 PlayerPrefs 关键路径。
- 2026-07-08 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误。
- 2026-07-09 静态敏感扫描 `Postcard_GuidePart.cs` 未命中 `OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics`、`PlayerPrefs`、`DB.`、`SavePostcard`、`Instantiate` 或 `Resources.Load`。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 99 warnings / 0 errors。
- 2026-07-09 静态 prefab 搜索显示 `fingerPs` 只在脚本中命中，`PostcardPopup_GuidePart.prefab` 与 `PostcardPopup.prefab` 暂未看到该字段绑定；粒子实际表现待 Inspector 绑定后验证。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git LF/CRLF 提示。
- 2026-07-09 delay/interval 调整后重新执行敏感扫描、`dotnet build` 和 `git diff --check`；结果仍为敏感扫描无命中、编译 99 warnings / 0 errors、diff check 仅 LF/CRLF 提示。

## 未验证缺口

- Unity PlayMode 全场景矩阵未执行。
- Android/iOS 横屏 smoke 未执行。
- 保存失败、缺失绑定、finger 缺失、目标数量不匹配等降级路径未做运行时复现。
- 整页完成时 guide completion、Chest 奖励弹窗和自动切页组合流程未做运行时验证。
- guide 层可视动画与真实页面动画并行时的遮挡关系、时长一致性和输入锁释放仍待 PlayMode 验证。
- guide 层 cost 文本、红点状态和星星不足路径尚未与真实页面逐项 PlayMode 对照。
- `fingerPs` 的 prefab 引用绑定、延迟 0.5 秒且间隔 1 秒循环播放、停止清理、非 finger 子节点同步位置和移动端横屏渲染层级尚未运行时验证。

## 回滚路径

- 代码回滚：移除 `PostcardPopup` 的 guide start/consume region，移除 `Postcard_PageItem` 的 guide public API 和 click lock，恢复 `Postcard_GuidePart` 到字段-only 状态。
- 资源回滚：从 `PostcardPopup.prefab` 移除嵌套 `PostcardPopup_GuidePart` 或清空 `guidePart` 绑定。
- 数据回滚：无需迁移，因未新增存档字段或配置。

## 2026-07-13：GuideFinger 职责迁移风险补充

### 原始行为基线与变更边界

- 原始实现由 `Postcard_GuidePart` 同时承担引导目标推进和 finger/particle 表现；点击粒子在目标有效后延迟 0.5 秒首次播放，并每 1 秒清空后重播。
- 当前只迁移 finger 目标定位、显隐、粒子位置同步、循环与生命周期清理；guide item 映射、按钮锁、解锁、扣星、存档和完成回调保持原链路。
- 影响文件为 `Postcard_GuidePart.cs`、新增 `GuideFinger.cs`、`PostcardPopup_GuidePart.prefab` 和 `PostcardPopup.prefab`；未新增全局事件、配置、资源地址或存档字段。

### 风险项

- **P1：prefab 跨对象绑定失效。** `Postcard_GuidePart` 位于嵌套 guide prefab，`GuideFinger` 位于主 popup；主 prefab 使用 nested prefab override 绑定两者。静态 fileID 存在，但仍需 Unity Inspector 导入和 PlayMode 验证 override 是否稳定。
- **P1：粒子循环未按预期停止。** 当前在 GuideFinger 的 `Hide`、`OnDisable`、`OnDestroy` 统一停止协程并以 `StopEmittingAndClear` 清空；仍需快速关闭、切页、完成和 popup 销毁场景验证无残留粒子。
- **P2：目标切换节奏。** 目标切换会立即移动 finger，但已有循环不会重新计算 0.5 秒初始延迟，下一次粒子可能在 0~1 秒内出现；这是对迁移前“已有协程不重启”行为的保留，若视觉要求每个目标都固定延迟 0.5 秒需另行确认。
- **P2：移动端位置与层级。** 世界中心转 finger 父节点局部坐标的算法保持不变；Android/iOS 横屏、不同安全区和 Canvas 粒子排序仍未验证。
- **P2：缺失引用降级。** GuidePart 缺少显式引用时只在所属 `PostcardPopup` 内查找 GuideFinger；finger 或粒子缺失时隐藏或跳过粒子，不阻断解锁流程。warning 表现未新增，缺失绑定仍可能静默降低视觉质量。

### 验证证据与未验证缺口

- CodeGraph 直接表现链：`RefreshFingerPosition` → `Create` → `ShowAt` → `StartParticleLoop` → `PlayParticleLoop`；无自动化测试覆盖。
- C# 项目构建通过：98 warnings / 0 errors。
- 静态敏感扫描无 editor lifecycle、per-frame polling、global event、DB/save、Resources.Load 或 Instantiate 命中；GuidePart 无旧粒子循环残留。
- 脚本与 guide prefab 的 `git diff --check` 通过，仅有 LF/CRLF 提示；主 popup prefab 本身包含任务开始前的其他未提交编辑，按工作区归属保留。
- Unity PlayMode、Inspector prefab override、0.5 秒/1 秒视觉节奏、快速切页/关闭/销毁以及 Android/iOS 横屏 smoke 均未执行。

### 回滚路径与待确认项

- 回滚代码：把 finger RectTransform、ParticleSystem、协程和定位方法恢复到 `Postcard_GuidePart`，移除 `GuideFinger` 公共 API 调用。
- 回滚资源：恢复 GuidePart 的旧 GuideFinger 字段绑定，并停止从 Resources 创建独立 Finger。
- 无 DB 或配置迁移。
- 待确认：目标切换后是否要求重新等待完整 0.5 秒；当前按原行为保留连续循环。

## 2026-07-13：独立 GuideFinger 点击生命周期风险

### 原始行为基线与变更边界

- 变更前独立 `Create(target, root, callback)` 只实例化 prefab 并保存 callback，没有完整的 GuidePart 解锁串联。
- 当前 GuidePart 已使用独立实例的 callback 进入原解锁链；扣星、存档和 ProgressItem 动画仍由既有 PageItem/ProgressItem 负责。
- 影响文件为 `GuideFinger.cs` 与 `Postcard_GuideFinger.prefab`，未增加全局事件或新数据字段。

### 风险项

- **P1：ClickArea 绑定缺失会导致实例无法关闭。** 当前 prefab 已静态增加 `CustomButton` 并绑定 `clickArea`；代码仍保留子层级查找作为降级，但运行时导入和触摸命中仍需 PlayMode 验证。
- **P1：callback 异常影响调用栈。** 点击入口使用 `finally` 保证根 GameObject 被销毁，但不吞掉 callback 异常；Unity Console 仍会记录该异常，调用方应自行保证回调安全。
- **P2：帧末销毁窗口。** `Destroy(gameObject)` 在帧末生效；当前在执行 callback 前已清空字段并解除监听，避免同一实例重复消费回调，仍需快速连点验证。
- **P2：全屏 ClickArea 输入遮挡。** 独立 prefab 的透明 Image 会接收射线，符合点击任意区域关闭的当前用途；若后续要求只点击 target，需要调整 ClickArea 尺寸而不是改 callback 语义。
- **P2：跨 Canvas 定位。** `ShowAt` 使用目标世界中心转换到 finger 父节点局部坐标；不同 Canvas render mode、缩放与安全区仍需多分辨率验证。

### 验证证据、未验证项与回滚

- C# 构建通过：98 warnings / 0 errors；静态敏感扫描无编辑器生命周期、每帧轮询、DB/save、全局事件或平台链路。
- prefab 静态证据确认透明 `Image.raycastTarget=1`、`CustomButton._interactable=1`，以及 `GuideFinger.clickArea` 的 fileID 绑定一致。
- Unity PlayMode、callback 正常/异常、快速连点、资源缺失、Android/iOS 横屏触摸和 Canvas 定位未验证。
- 回滚代码和 prefab 的点击绑定即可恢复为仅表现组件；无数据迁移或资源地址迁移。

## 2026-07-13：Finger 驱动原 ProgressItem 解锁的风险补充

### 行为边界与调用链

- GuidePart 不再订阅 guide 复制 item 的 `CustomButton.OnClick`，而是为当前目标调用 `GuideFinger.Create(target, guidePartRoot, OnGuideFingerClick)`。

## 2026-07-15：GuideFinger 类型重命名风险补充

### 原有基线与改动

- 原脚本文件和 MonoBehaviour 类型均为 `Postcard_GuideFinger`；prefab 通过脚本 meta GUID `c30e1794b95c6b541b0f9ea5dc6ead87` 绑定组件。
- 当前文件和类型统一为 `GuideFinger`，所有 C# 调用点同步更新；现有 prefab 文件名与 Resources 加载键保持 `Postcard_GuideFinger`。

### 风险与证据

- **P0：脚本组件丢失。** 已通过保留原 `.meta` GUID，并静态核对 prefab `m_Script` GUID 与新 `GuideFinger.cs.meta` 一致规避；Unity Inspector 实际显示仍待打开 prefab 验证。
- **P1：旧类型残留导致编译失败。** C# 静态搜索已无 `Postcard_GuideFinger` 类型声明或调用；Unity 工程文件已引用 `GuideFinger.cs`，并成功重新生成 `Assembly-CSharp.dll/.pdb`。
- **P1：资源路径误改。** `Resources.Load<GuideFinger>("Postcard_GuideFinger")` 保持原加载键，本次没有重命名 prefab 或资源目录。
- 命令行构建受 `Temp/bin/Debug` 中 191 个缺失元数据 DLL 阻断；不涉及运行时行为、存档、配置、全局事件、资源数量或移动端性能变化，PlayMode 与 Android/iOS 横屏仍未验证。

### 回滚

- 将 `GuideFinger.cs/.meta` 和类型/调用点恢复为 `Postcard_GuideFinger`，继续使用相同 GUID；prefab、数据和资源路径无需回滚。
- Finger callback 只负责把当前 index 交给 `Postcard_PageItem.TryUnlockGuidedItemByListIndex(...)`；真实 `TryUnlockItem(...)` 仍是星星、存档、扣除和真实 ProgressItem 动画的唯一权威。
- guide 复制 item 的 `PlayUnlockAnim(...)` 保留为遮罩层可见表现，不参与数据写入；页面与 guide 动画双稳定后才推进。

### 风险项

- **P1：Finger 创建失败会失去点击入口。** Guide item 按钮现在保持禁用；Resources prefab 缺失或创建失败时只记录 warning，引导会停在当前目标。需要在 PlayMode 验证资源可加载。
- **P1：拒绝解锁后的 Finger 重建。** 星星不足、保存失败或校验拒绝时必须重新创建当前目标 Finger；静态代码已覆盖该路径，运行时仍需逐项验证。
- **P1：双动画推进时序。** 页面稳定回调和 guide 复制 item 动画必须都完成才推进；Finger 改为帧末销毁不应改变该门闩，但快速关闭和最后一项完成仍需验证。
- **P2：实例化/销毁开销。** 每个目标至少一次 `Resources.Load/Instantiate/Destroy`，失败重试会增加次数；没有每帧循环，但 Android/iOS 低端设备的 GC 和 UI Canvas rebuild 尚未测量。
- **P2：全屏 ClickArea 语义。** 当前 Finger prefab 的 ClickArea 覆盖全屏，任意位置点击都会尝试当前目标解锁；若产品要求必须点目标区域，需要调整 prefab raycast 范围。

### 验证缺口与回滚

- C# 构建通过：98 warnings / 0 errors；CodeGraph 已确认 Finger callback 到 PageItem 原解锁入口及 guide `PlayUnlockAnim` 的调用链。
- Unity PlayMode、星星不足、存档失败、快速连点、切页/关闭、最后目标完成、资源缺失以及 Android/iOS Profiler 均未验证。
- 回滚时恢复 active guide item 的按钮订阅，恢复旧 GuideFinger 表现引用并移除运行时 `Create/Close`；数据结构无需迁移。

## 2026-07-13：Stage Two 后置显示风险

- **事实：** GuidePart 不再依赖 Popup 初次 `OnOpen()` 启动；当前正常胜利引导由 Stage Two 关闭回调显式启动。
- **P1：旧 pending 状态。** Popup 为兼容其它入口仍会自动读取 `IsGuiding`；历史异常存档可能提前显示 GuidePart，需中断恢复验证。
- **P1：回调目标失效。** Stage Two 期间关闭 PostcardPopup 时，回调会检测对象并停止；不写新 pending 状态，但首次引导的后续重试尚未验证。
- **P2：创建失败降级。** Stage Two 无法创建时 GuidePart 直接显示，避免无回调卡住；该路径的 UI 层级和 warning 尚未故障注入。
- 编译通过，98 warnings / 0 errors；Unity PlayMode、Stage Two 正常/异常关闭、旧存档、Android/iOS 横屏触摸仍待验证。回滚只需恢复外部启动时序，无 GuidePart 资源或存档迁移。
