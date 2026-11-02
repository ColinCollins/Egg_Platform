# GamePlayPanel PostcardStar 飞星反馈记录

## 需求背景

2026-07-06，关卡内获得明信片星星后，UI 星星需要飞到 HUD 左侧星星图标处，而不是飞到整个星星道具块中心；到达时需要再播放一次可见特效，飞星最终尺寸需要与 HUD 小星图标一致，并在短暂停留后渐隐消失。同时，HUD 星星道具块的弹性反馈需要更夸张。

## 影响范围

- `Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
  - 调整 UI 飞星目标、目标尺寸、到达脉冲、淡出和 HUD 弹性反馈。
- `Assets/Game/UI/Global/Scripts/PropBlock.cs`
  - 新增 `iconTarget` 序列化引用和 `TryGetIconTarget(...)`，供飞行动画对齐到道具图标节点。
- `Assets/Game/UI/Global/Prefabs/PropBlock.prefab`
  - 将 `iconTarget` 绑定到内部图标 `UI_L_shop_icon_huobi_01` 的 RectTransform。
- `Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`
  - 绑定到达特效 prefab，并把 `starBlockScaleMultiplier` 调整为 `1.3`。
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`
  - 同步特殊面板变体的到达特效参数和 HUD 弹性参数。

本次未修改 `PostcardGetStarEvent`、`PostcardManager`、背包发奖、存档、Luban、RemoteConfig、资源加载、广告、IAP、analytics 或平台 SDK。

## 原有行为基线

- `GamePlayPanel.PostcardStar` 收到 `PostcardGetStarEvent` 后，在 Canvas 下实例化 `postcardStar`，使用二次贝塞尔曲线飞向 `starBlock.transform.position`。
- 飞行过程中 UI 星星会缩放到 `Vector3.zero`，到达后直接销毁，因此终点不会保留 HUD 小星图标尺寸。
- HUD 星星道具块的反馈是放大到 `starBlockScaleMultiplier` 后直接回到原始尺寸，默认 prefab 倍率为 `1.12`。
- `PropBlock` 只对外提供整体道具块，没有暴露内部图标 RectTransform。

## 当前行为

- `PropBlock` 现在可通过 `TryGetIconTarget(...)` 暴露内部图标节点；飞星目标优先使用该图标中心，未绑定时回退到整个 `PropBlock`。
- `GamePlayPanel.PostcardStar` 会用目标图标的世界角点换算 Canvas 本地尺寸，并将飞星在飞行末端缩放到与 HUD 小星图标一致的尺寸。
- UI 星星仍然使用 `postcardStarArcHeight` 和 `EvaluateQuadraticBezier(...)` 播放曲线飞行。
- 飞星抵达后会额外实例化一次 `postcardStarArriveEffect`，在目标图标处放大并淡出；未绑定时复用 `postcardStar`。
- 原飞星抵达后会做一次轻微弹性放大、回到目标图标尺寸、短暂停留，再通过运行时添加的 `CanvasGroup` 渐隐并销毁。
- HUD 星星道具块的反馈改为放大、轻微回缩、归位三段式；prefab 倍率从 `1.12` 提升到 `1.3`。

## 数据、事件与 UI 流

1. `Postcard_Star` 收集成功后继续派发 `PostcardGetStarEvent`，事件参数仍为场景星星世界坐标。
2. `GamePlayPanel.PostcardStar` 将场景世界坐标投影到 Canvas 本地坐标，作为 UI 飞星起点。
3. 面板先显示 `starBlock`，再解析 `PropBlock.iconTarget` 作为 HUD 终点和尺寸来源。
4. UI 飞星沿二次贝塞尔曲线移动到图标中心，并在末端缩放到图标尺寸。
5. 到达后播放一次到达脉冲，飞星本体短暂停留并渐隐。
6. 飞行动画结束后沿用原流程刷新 `PropBlock.UpdateData()`，播放 HUD 弹性反馈并延迟隐藏。

## 验证记录

- `git diff --check -- Assets/Game/UI/Global/Scripts/PropBlock.cs Assets/Game/UI/Global/Prefabs/PropBlock.prefab Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`：通过，仅有当前仓库的 LF/CRLF 提示。
- 静态扫描 `OnValidate|Reset|ExecuteAlways|ExecuteInEditMode|DB.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement`：对本次两个脚本无命中。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未完成，当前环境没有 .NET SDK，返回 `No .NET SDKs were found`。
- 尚未运行 Unity Editor PlayMode、默认面板与 `GamePlayPanel_025_70` 实机/模拟器视觉验证。

## 剩余风险

- 到达脉冲当前复用 UI 星星 prefab，实际视觉强度、层级遮挡和 Animator 叠加效果需要在 Unity PlayMode 中确认。
- 目标尺寸按 RectTransform 世界角点换算，若后续 HUD 图标存在非等比缩放或旋转，飞星会取宽高较小比例保持等比缩放。
- `PropBlock.iconTarget` 依赖 prefab 绑定；若其他未覆盖的 `PropBlock` 变体不继承该绑定，会回退到整个道具块中心。

## 回滚路径

- 将 `GamePlayPanel.PostcardStar.cs` 的飞行缩放恢复为到达 `Vector3.zero` 后销毁，移除到达脉冲、渐隐和图标尺寸换算逻辑。
- 删除 `PropBlock.iconTarget` 与 `TryGetIconTarget(...)`，并移除 `PropBlock.prefab` 中的 `iconTarget` 绑定。
- 将两个 `GamePlayPanel` prefab 的到达特效参数移除，并把 `starBlockScaleMultiplier` 恢复为 `1.12`。

## 2026-07-06 补充：到达特效、+1 飘字和数字滚动

### 需求背景

用户反馈星星飞到 HUD 后没有明显播放到达特效，并希望数字后方出现 `+1` 浮动 tips、HUD 数字在到达后有滚动效果，同时 HUD 整体弹性再夸张一些。

### 本次调整

- `GamePlayPanel.PostcardStar` 新增 `postcardStarArriveEffects: List<GameObject>`，到达后会按列表逐个实例化特效；保留旧 `postcardStarArriveEffect` 单引用作为兼容入口。
- 到达特效实例化后会主动播放子节点 `ParticleSystem`，并对 `Animator` 执行 `Rebind()` / `Update(0f)`，避免 UI/粒子特效因为刚实例化而停在旧状态。
- 默认 `GamePlayPanel.prefab` 在到达时同时播放 `Postcard_UIStar` 兜底脉冲和 `Postcard_UIStarCollection` 到达特效；`GamePlayPanel_025_70.prefab` 也序列化了同一个列表入口。
- `PropBlock` 新增 `PlayRewardFeedback(int fallbackDelta)`，由明信片飞星到达后调用；它会优先使用最近一次 `UpdatePropEvent` 的 old/new 计数重放数字滚动，并在数字右侧生成 `+N` 飘字。
- `PropBlock` 的飘字通过复制 `CountTxt` 生成，不新增 prefab；运行时设置颜色、位置、缩放、淡入上浮淡出，并在完成或销毁时清理。
- HUD 星星块的 `starBlockScaleDuration` 调整为 `0.65`，`starBlockScaleMultiplier` 调整为 `1.55`，三段式放大/回缩/归位反馈更明显。

### 验证记录

- `git diff --check` 对本次脚本、prefab 和文档通过，仅有当前仓库 LF/CRLF 提示。
- 静态扫描 `OnValidate|Reset|ExecuteAlways|ExecuteInEditMode|DB.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement`：对本次两个脚本无命中。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未完成，当前环境没有 .NET SDK，返回 `No .NET SDKs were found`。
- 尚未运行 Unity PlayMode 验证特效层级、`+1` 飘字位置、数字滚动节奏和 HUD 弹性强度。

### 剩余风险

- `PropBlock` 仍会响应背包的即时 `UpdatePropEvent`，飞星到达后会再用最近 old/new 重放一次滚动，用于保证到达反馈可见；如果后续希望完全延迟数字变化，需要单独拆分星星道具块的自动更新策略。
- 默认到达特效当前依赖 `Postcard_UIStar` 和 `Postcard_UIStarCollection` 的视觉资源，最终强度需要 Unity PlayMode 中按实际 HUD 层级确认。

### 回滚路径

- 将 `RefreshPulseAndHideStarBlock()` 恢复为调用 `starBlock.UpdateData()`，移除 `PropBlock.PlayRewardFeedback(...)` 及飘字相关字段和方法。
- 清空 `postcardStarArriveEffects` 列表，仅保留或移除旧单引用 `postcardStarArriveEffect`。
- 将两个 `GamePlayPanel` prefab 的 `starBlockScaleDuration` / `starBlockScaleMultiplier` 恢复为 `0.5` / `1.3`，或进一步恢复为旧版 `0.5` / `1.12`。

## 2026-07-06 补充：进场隐藏、到达同步和场景特效播放

### 需求背景

用户反馈进入场景时不应先显示星星货币栏；飞星到达位置与 HUD 弹性反馈需要几乎同时发生，且当前 HUD 弹性过大、整体动画过慢；到达位置配置的两个特效仍未明显播放。

### 本次调整

- `GamePlayPanel.OnOpen()` 不再调用开局展示逻辑，改为 `HidePostcardStarBlockAtLevelStart()`，打开面板时强制隐藏 `starBlock`，避免进场闪一下货币栏。
- 飞星到达目标点的回调中立即调用 `PlayPostcardStarArrivalFeedback()`，同步触发 `+1`、数字滚动、HUD 弹性和隐藏计时；飞星后续弹性/淡出结束后不再重复触发 HUD 弹性。
- `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 的 HUD 弹性参数从 `0.65 / 1.55` 调整为 `0.32 / 1.25`，减小幅度并加快节奏。
- `postcardStarArriveEffectDuration` 调整为 `0.45`，给到达特效更长可见时间。
- 到达特效现在先判断 prefab 根节点是否是 UI `RectTransform`：
  - UI 特效继续实例化到 Canvas 下，并在 HUD 图标本地坐标播放。
  - 非 UI/场景粒子特效不再挂到 Canvas 下，而是把 HUD 目标点转换到关卡世界坐标后实例化播放，避免场景粒子在 Overlay Canvas 下不可见。

### 验证记录

- `git diff --check` 已通过，仅有当前仓库 LF/CRLF 提示。
- 脚本风险关键字扫描无命中。
- 尚未运行 Unity PlayMode，需在编辑器内确认：进场星星栏不闪、到达特效可见、HUD 弹性幅度和节奏符合预期。

### 回滚路径

- `GamePlayPanel.OnOpen()` 恢复调用 `ShowPostcardStarBlockAtLevelStart()`。
- 将 HUD 到达反馈从飞星到达回调移回 `PlayPostcardStarFlightAsync()` 完整等待后。
- 将场景粒子特效恢复为全部实例化到 Canvas 下，或仅保留 UI 特效列表。

## 2026-07-06 补充：星星绑定粒子传递到 HUD 到达点

> 历史版本说明：本节记录 2026-07-06 的尝试方案。2026-07-07 已移除 `ArrivalEffectPrefabs` 事件传递链，当前行为以后文“移除 `arrivalEffectPrefabs` 到达特效传递链”为准。

### 需求背景

用户再次反馈飞星到达 HUD 后仍没有播放星星上绑定的两个特效；同时希望飞行继续保持明显曲线且不要太快，HUD 弹性形变速度也不要过快。

### 本次调整

- `PostcardGetStarEvent` 从 `EventBase<Vector3>` 扩展为 `EventBase<Vector3, IReadOnlyList<ParticleSystem>>`，保留 `StarWorldPosition`，新增 `ArrivalEffectPrefabs`。
- `Postcard_Star` 派发事件时会把当前星星自身的 `collectionEffects` 一起传给 UI；如果列表为空，则使用旧 `expolsion` 作为兼容兜底。
- `GamePlayPanel.PostcardStar` 收到事件后，飞星到达 HUD 图标时优先播放事件带来的 `ArrivalEffectPrefabs`，即星星上真正绑定的两个粒子特效；只有事件没有带特效时才使用面板 prefab 的 UI 兜底特效。
- 飞行参数调整：默认面板 `postcardStarFlyDuration=0.9`、`postcardStarArcHeight=180`；`GamePlayPanel_025_70` 为 `0.95` / `180`，让路径更慢且曲线更明显。
- HUD 弹性参数调整为 `starBlockScaleDuration=0.45`、`starBlockScaleMultiplier=1.25`，比上一版慢一些、幅度保持收敛。

### 验证记录

- `rg` 确认 `PostcardGetStarEvent` 仅由 `GamePlayPanel` 和 `PostcardManager` 监听；`PostcardManager` 仍只读取 `StarWorldPosition`。
- `git diff --check` 对 `PlayEvents.cs`、`Postcard_Star.cs`、`GamePlayPanel.PostcardStar.cs` 和相关 prefab 通过，仅有当前仓库 LF/CRLF 提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未完成，当前环境没有 .NET SDK，返回 `No .NET SDKs were found`。
- 尚未运行 Unity PlayMode 验证到达点两个粒子、曲线速度和 HUD 弹性手感。

### 剩余风险

- `ArrivalEffectPrefabs` 传递的是场景星星 prefab 上的粒子 prefab 引用；若个别关卡星星实例未同步 `collectionEffects`，到达点会回退到面板 UI 兜底特效。
- 事件签名扩展需要 Unity 编译确认事件系统泛型派发无误；当前环境缺少 .NET SDK，尚未完成编译验证。

### 回滚路径

- 将 `PostcardGetStarEvent` 恢复为单参数事件，并把 `Postcard_Star` 派发参数恢复为仅传 `ResolveCollectionWorldPosition()`。
- 删除 `GamePlayPanel.PostcardStar` 中接收和播放 `ArrivalEffectPrefabs` 的逻辑，恢复只使用 `postcardStarArriveEffects` / `postcardStarArriveEffect`。
## 2026-07-06 补充：到达粒子层级、+1 颜色与飞星消失节奏

> 历史版本说明：本节中提到的 `ArrivalEffectPrefabs` 播放路径已在 2026-07-07 移除，当前到达特效仅来自 `GamePlayPanel` prefab 本地配置。

### 需求背景
用户反馈星星飞到 HUD 后两个到达特效已经出现但层级不对，`+1` 浮字颜色需要与 HUD 星星数字一致，同时飞星到目标后的本体消失可以更快。

### 本次调整
- `GamePlayPanel.PostcardStar` 播放 `ArrivalEffectPrefabs` 时不再把粒子转换到关卡场景世界坐标，而是实例化为 HUD Canvas 父节点下的子物体，使用目标图标的 Canvas 本地坐标作为粒子局部位置。
- 新增 `postcardStarArriveParticleSortingOrderOffset=80`，到达粒子实例化后会把所有子级 `ParticleSystemRenderer` 的 `sortingLayerID` 对齐当前 Canvas，并把 `sortingOrder` 提升到 Canvas 排序之上，避免被 HUD 元素压住。
- 非 UI 的到达 `GameObject` 特效也统一挂到 Canvas 父节点并执行同样的粒子排序提升；UI `RectTransform` 特效仍走原有 Canvas 内锚点播放和淡出逻辑。
- `PropBlock.PlayFloatingTip(...)` 的 `+N` 浮字颜色改为继承 `CountTxt.color`，并移除 prefab 中不再使用的独立黄色 `floatingTipColor` 序列化值。
- `GamePlayPanel.prefab` 与 `GamePlayPanel_025_70.prefab` 的飞星到达后淡出从 `0.06 / 0.18` 调整为 `0.02 / 0.1`，让飞星本体在对齐 HUD 图标后更快淡出。

### 验证记录
- 已执行静态 diff 检查，确认 `floatingTipColor` 不再存在，两个面板 prefab 均写入 `postcardStarArriveParticleSortingOrderOffset: 80`。
- 待补 Unity PlayMode 验证：两个绑定粒子是否盖在 HUD 星星栏上层、`+1` 颜色是否与数字一致、飞星淡出速度是否符合手感。

### 回滚路径
- 将 `PlayPostcardStarArriveParticleEffect(...)` 和非 UI 到达特效恢复为世界坐标实例化，移除 `BoostPostcardStarParticleSorting(...)` 和 `postcardStarArriveParticleSortingOrderOffset`。
- 将 `PropBlock.PlayFloatingTip(...)` 恢复为独立颜色字段，并把 `floatingTipColor` 写回 `PropBlock.prefab`。
- 将两个 `GamePlayPanel` prefab 的 `postcardStarArriveFadeDelay` / `postcardStarArriveFadeDuration` 恢复为 `0.06` / `0.18`。
## 2026-07-06 补充：飞星尾段淡出与到达粒子可见性修正

### 需求背景
用户反馈飞星到达目标后仍继续做本体放大缩小，导致 HUD 弹性反馈播放时星星本体还停在目标点附近，视觉上对不齐；同时上一次调整后，原本到达点播放的绑定粒子特效又不显示。

### 本次调整
- `GamePlayPanel.PostcardStar` 的飞星本体不再在到达后执行放大/回缩动画；飞行尾段会提前淡出，抵达回调触发时强制 alpha 为 0。
- 两个 `GamePlayPanel` prefab 的飞星本体淡出参数调整为 `postcardStarArriveFadeDelay=0`、`postcardStarArriveFadeDuration=0.08`，让本体在飞行最后 0.08 秒快速消失。
- HUD 数字滚动、`+1` 浮字和 HUD 整体弹性仍在飞星抵达回调触发，因此现在是“飞星已消失后再播放 HUD 放大缩小”。
- 星星绑定的到达粒子不再作为 Canvas 子物体实例化，避免继承 Canvas 缩放导致粒子过小或不可见；现在会取 HUD 目标点的 Canvas 世界坐标播放，同时继续提升 `ParticleSystemRenderer` 的 sorting layer/order 到 HUD 上层。
- 非 UI 的到达 `GameObject` 特效同样使用 HUD 目标点世界坐标播放；UI `RectTransform` 到达特效仍挂在 Canvas 下按锚点播放。

### 验证记录
- 已静态确认 `postcardStarArrivePopScaleMultiplier` 不再存在，飞星本体到达后不再追加 scale tween。
- 待补 Unity PlayMode 验证：飞星尾段消失是否自然、HUD 弹性是否在本体不可见后触发、两个绑定粒子是否重新可见且层级正确。

### 回滚路径
- 将飞星序列恢复为到达后执行本体 `DOScale` 放大/回缩和抵达后淡出。
- 将粒子到达特效恢复为 Canvas 子物体实例化，或恢复到更早的场景世界坐标转换逻辑。

## 2026-07-07 补充：移除 `arrivalEffectPrefabs` 到达特效传递链

### 需求背景

用户要求整理 `GamePlayPanel.PostcardStar` 中 `arrivalEffectPrefabs` 部分的使用。合并后的代码把明信片星星收集事件恢复为只传场景星星世界坐标，场景星星自身的粒子特效只在场景内播放，不再作为 HUD 到达特效参数传给 `GamePlayPanel`。

### 本次调整

- `PostcardGetStarEvent` 恢复为 `EventBase<Vector3>`，仅保留 `StarWorldPosition`，不再暴露 `ArrivalEffectPrefabs`。
- `Postcard_Star.Collect()` 派发事件时只传 `ResolveCollectionWorldPosition()`；`collectionEffects` / `expolsion` 仍只用于 `PlayCollectionEffects()` 在场景星星原位置播放收集特效。
- `GamePlayPanel.PostcardStar` 的 `OnGetStar(...)` 只读取 `evt.StarWorldPosition`，`PlayPostcardStarAsync(...)`、`PlayPostcardStarFlightAsync(...)` 和 `PlayPostcardStarArriveEffect(...)` 不再接收或转发 `arrivalEffectPrefabs`。
- 删除只服务该参数链的 `PlayPostcardStarArriveParticleEffect(...)` 和 `ResolveParticleLifetime(...)`。
- 到达特效回退顺序收敛为面板本地配置；后续已进一步精简为只播放 `postcardStarArriveEffects` 列表，未配置时复用 `postcardStar`。
- 本次未修改 `PostcardManager`、prefab 绑定、存档字段、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 数据、资源、事件与 UI 流

1. `Postcard_Star` 收集成功且存档/发奖成功后，派发 `PostcardGetStarEvent(StarWorldPosition)`。
2. `Postcard_Star.PlayCollectionEffects()` 在场景星星原位置实例化 `collectionEffects`，没有列表配置时使用隐藏兼容字段 `expolsion`。
3. `GamePlayPanel.PostcardStar` 将 `StarWorldPosition` 换算为 Canvas 本地起点，实例化 `postcardStar` 并飞向 HUD 图标。
4. 飞星抵达时只使用当前 `GamePlayPanel` prefab 上的到达特效配置，不再尝试播放场景星星实例传来的粒子 prefab。
5. HUD 数字滚动、`+1` 浮字、星星块弹性反馈和隐藏计时仍沿用 `PlayPostcardStarArrivalFeedback()` / `RefreshPulseAndHideStarBlock()`。

### 验证记录

- `git diff -- Assets/Game/Scripts/Play/PlayEvents.cs Assets/Game/Scripts/Postcard/Postcard_Star.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs` 静态确认：事件签名改为单参数，`Postcard_Star` 派发只传世界坐标，`GamePlayPanel.PostcardStar` 不再读取 `evt.ArrivalEffectPrefabs`。
- `rg` 静态确认 `GamePlayPanel.PostcardStar.cs` 中不再存在 `arrivalEffectPrefabs`、`ArrivalEffectPrefabs`、`PlayPostcardStarArriveParticleEffect`、`ResolveParticleLifetime` 或 `IReadOnlyList<ParticleSystem>` 残留。
- `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB\.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：通过，仅有当前工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 warning / 0 error。

### 剩余风险

- 尚未运行 Unity PlayMode，需确认默认 `GamePlayPanel` 和 `GamePlayPanel_025_70` 的到达特效仍由 prefab 本地配置正常播放。
- 尚未运行 Android/iOS 横屏 smoke，移动端 HUD 特效层级、触摸输入和帧率表现仍待补验。
- `Postcard_Star` 当前仍保留未被事件派发使用的 `ResolveArrivalEffectPrefabs()`，静态上不影响运行路径；后续若继续清理 `Postcard_Star`，可作为单独代码整理项处理。
- 如果后续仍希望把场景星星实例绑定的粒子带到 HUD 到达点，需要重新设计事件契约和收集脚本派发参数，而不是只在面板内恢复旧参数。

### 回滚路径

- 若只回滚 `GamePlayPanel` 表现，可在 `GamePlayPanel.PostcardStar` 中恢复 `arrivalEffectPrefabs` 参数链、`PlayPostcardStarArriveParticleEffect(...)` 和粒子生命周期清理逻辑。
- 若需要完整恢复场景星星绑定粒子传递，还需同步恢复 `PostcardGetStarEvent` 的多参数签名和 `Postcard_Star` 的派发参数；否则面板侧旧参数没有事件来源。

## 2026-07-07 补充：到达特效配置入口合并

> 历史版本说明：本节记录“列表 + 旧单字段兼容”的过渡方案。后续已按用户要求移除 `postcardStarArriveEffect` 单字段，当前行为以后文“移除 `postcardStarArriveEffect` 单字段”为准。

### 需求背景

`GamePlayPanel.PostcardStar` 同时保留 `postcardStarArriveEffects` 列表和旧版兼容字段 `postcardStarArriveEffect`。旧代码在 `PlayPostcardStarArriveEffect(...)` 中分别处理列表和单字段，逻辑重复；并且 `GamePlayPanel_025_70.prefab` 当前静态绑定显示两者引用同一个 prefab，旧路径会重复播放同一个到达特效。

### 本次调整

- 保留 `postcardStarArriveEffect` 序列化字段作为旧 prefab 兼容入口，不删除字段、不修改 prefab。
- 新增统一播放入口 `PlayConfiguredPostcardStarArriveEffects(...)`，列表字段和兼容单字段都通过 `TryPlayPostcardStarArriveEffectPrefab(...)` 播放。
- 新增 `ContainsPostcardStarArriveEffect(...)`，当兼容单字段已经存在于 `postcardStarArriveEffects` 列表中时跳过，避免同一个 prefab 被重复实例化。
- `postcardStar` 仍作为没有任何配置特效时的最终兜底。

### 数据、资源、事件与 UI 流

- 事件、存档、奖励、配置、资源加载路径不变；只整理 HUD 到达特效的本地播放分支。
- 默认 `GamePlayPanel.prefab` 当前仍会播放列表特效和单字段兼容脉冲，因为两者静态引用不同。
- `GamePlayPanel_025_70.prefab` 当前列表和单字段静态引用相同，合并后只播放一次该到达特效。

### 验证记录

- 静态检查确认 `PlayPostcardStarArriveEffect(...)` 只调用统一入口 `PlayConfiguredPostcardStarArriveEffects(...)`，列表和单字段不再各自维护播放分支。
- `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB\.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `rg` 静态确认 `GamePlayPanel.PostcardStar.cs` 中不再存在 `arrivalEffectPrefabs`、`ArrivalEffectPrefabs`、`PlayPostcardStarArriveParticleEffect`、`ResolveParticleLifetime` 或 `IReadOnlyList<ParticleSystem>` 残留。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel_PostcardStarFlyFeedback.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 97 warnings。

### 剩余风险

- 尚未运行 Unity PlayMode，需确认默认面板的两个不同到达特效仍按预期同时播放，`GamePlayPanel_025_70` 不再重复播放同一特效后视觉强度是否符合预期。
- Android/iOS 横屏 smoke 仍待补验。

### 回滚路径

- 将 `PlayPostcardStarArriveEffect(...)` 恢复为分别遍历 `postcardStarArriveEffects` 和单独判断 `postcardStarArriveEffect` 的旧分支；如果确需保留重复播放，可移除 `ContainsPostcardStarArriveEffect(...)` 的跳过逻辑。

## 2026-07-07 补充：移除 `postcardStarArriveEffect` 单字段

### 需求背景

用户明确要求移除一个 `postcardStarArriveEffect`，保留 `postcardStarArriveEffects` 列表并确保仍有到达表现可播放。此前过渡方案保留了单字段作为兼容入口，但当前需求优先精简脚本和 prefab 序列化数据。

### 本次调整

- `GamePlayPanel.PostcardStar` 删除 `postcardStarArriveEffect` 序列化字段。
- 删除单字段去重逻辑 `ContainsPostcardStarArriveEffect(...)` 和额外 `TryPlayPostcardStarArriveEffectPrefab(...)` 包装。
- `PlayConfiguredPostcardStarArriveEffects(...)` 现在只遍历 `postcardStarArriveEffects` 列表，跳过空项并播放有效 prefab。
- 当 `postcardStarArriveEffects` 为空或全为空项时，仍回退播放 `postcardStar`，保证 HUD 到达点有兜底表现。
- 从 `GamePlayPanel.prefab` 和 `GamePlayPanel_025_70.prefab` 中移除已删除字段的 `postcardStarArriveEffect` 序列化项；保留两个 prefab 的 `postcardStarArriveEffects` 列表绑定。

### 数据、资源、事件与 UI 流

- 事件、存档、奖励、配置、资源加载路径不变；仅精简 `GamePlayPanel` HUD 到达特效本地配置入口。
- 默认 `GamePlayPanel.prefab` 当前已把旧单字段脉冲和原列表特效都迁移到 `postcardStarArriveEffects`，因此仍播放两个本地到达特效。
- `GamePlayPanel_025_70.prefab` 当前通过 `postcardStarArriveEffects` 播放同一个到达特效；不再保留重复字段。

### 验证记录

- `rg` 静态确认 `GamePlayPanel.PostcardStar.cs`、`GamePlayPanel.prefab` 和 `GamePlayPanel_025_70.prefab` 中不再存在 `postcardStarArriveEffect` 单字段、`ContainsPostcardStarArriveEffect` 或 `TryPlayPostcardStarArriveEffectPrefab` 残留。
- 静态检查确认默认 `GamePlayPanel.prefab` 的 `postcardStarArriveEffects` 列表有 2 项，`GamePlayPanel_025_70.prefab` 的 `postcardStarArriveEffects` 列表有 1 项。
- `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB\.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab Assets/Game/UI/GamePanel/Scripts/GamePlayPanel_PostcardStarFlyFeedback.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 97 warnings。

### 剩余风险

- 尚未运行 Unity PlayMode，需确认默认面板迁移到列表后的两个到达特效仍按预期同时播放。
- 尚未运行 Android/iOS 横屏 smoke。

### 回滚路径

- 如需恢复旧单字段兼容，重新添加 `postcardStarArriveEffect` 序列化字段，并在 `PlayConfiguredPostcardStarArriveEffects(...)` 中追加该字段播放逻辑；同时把 prefab 中的单字段绑定写回。

## 2026-07-07 补充：清理到达特效无效状态

### 需求背景

用户要求继续整理 `GamePlayPanel.PostcardStar` 代码，去除当前列表化到达特效逻辑后遗留的无效内容。本次目标是只删除已有列表路径可覆盖的旧状态和未调用私有方法，不调整飞星手感、事件契约、存档或 prefab 配置。

### 本次调整

- 删除旧单实例到达特效时代遗留的 `postcardStarArriveEffectSequence` 和 `currentPostcardStarArriveEffect`。
- `StopPostcardStarArriveEffect()` 现在只通过 `postcardStarArriveEffectSequences` 和 `currentPostcardStarArriveEffects` 统一停止 tween 与销毁实例，UI 特效和非 UI 特效共用同一清理路径。
- 删除未被调用的私有方法 `TryGetStarBlockCanvasLocalPoint(...)`。
- 保留 public `TryGetStarBlockWorldPosition(...)`，避免因代码整理删除仍可能被外部脚本或 prefab 事件引用的公开 API。
- 本次未修改 `PostcardGetStarEvent`、`Postcard_Star`、`PostcardManager`、prefab、DB/save、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 数据、资源、事件与 UI 流

- 明信片星星收集事件仍只传 `StarWorldPosition`。
- HUD 到达特效仍只读取 `postcardStarArriveEffects` 列表；列表为空或全为空时继续使用 `postcardStar` 兜底。
- 到达特效的播放数量、位置、缩放、淡出时长和 HUD 数字/弹性反馈触发时机不变。

### 验证记录

- `rg -n "\bcurrentPostcardStarArriveEffect\b|\bpostcardStarArriveEffectSequence\b|\bTryGetStarBlockCanvasLocalPoint\b" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB\.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs Assets/Game/UI/GamePanel/Scripts/GamePlayPanel_PostcardStarFlyFeedback.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 383 warnings。

### 剩余风险

- 尚未运行 Unity PlayMode，需确认默认面板和 `GamePlayPanel_025_70` 的到达特效视觉保持与清理前一致。
- 尚未运行 Android/iOS 横屏 smoke。

### 回滚路径

- 如清理后发现旧逻辑依赖问题，可恢复 `postcardStarArriveEffectSequence`、`currentPostcardStarArriveEffect` 和 `TryGetStarBlockCanvasLocalPoint(...)`，并把 `StopPostcardStarArriveEffect()` 恢复为同时清理单实例缓存与列表缓存。

## 2026-07-09 补充：连续收集飞星独立播放

### 需求背景

用户要求每次触发明信片星星 collect 表现时，每个 star 的飞行效果互相独立；后一次收集允许重新实例化新的 UI 飞星，不应把前一次仍在飞行或到达点播放的表现打断。

### 本次调整

- `GamePlayPanel.PostcardStar` 不再在 `PlayPostcardStarAsync(...)` 开始时调用 `StopPostcardStarFlight()`，连续收到 `PostcardGetStarEvent` 时会各自实例化新的 `postcardStar`。
- 原单实例状态 `postcardStarSequence` / `currentPostcardStar` 改为 `postcardStarSequences` / `currentPostcardStars` 列表，面板关闭或销毁时统一停止所有飞星 tween 并销毁残留实例。
- 到达特效入口 `PlayPostcardStarArriveEffect(...)` 不再先调用 `StopPostcardStarArriveEffect()`，因此多个 star 的到达特效可以并行完成。
- `postcardStarCleanupVersion` 只在统一清理时递增，用于阻止关闭过程中的异步回调重新刷新 HUD；新飞星开始不会递增该版本，也不会取消旧飞星。
- 每个飞星自己的到达回调都会触发 `RefreshPulseAndHideStarBlock()`；HUD 星星块仍是共享对象，后到达的反馈会重置共享 HUD 的隐藏计时和缩放反馈。

### 数据、资源、事件与 UI 流

- 事件签名仍是 `PostcardGetStarEvent(StarWorldPosition)`；本次不修改 `Postcard_Star`、`PostcardManager`、背包发奖、DB/save、配置或 prefab 绑定。
- 每次事件到达后，面板将对应场景世界坐标投影到 Canvas 本地坐标，创建一个独立 UI 飞星并加入活动列表。
- 飞星完成或目标解析失败时会从活动列表移除并销毁；面板关闭、销毁或统一停止时会清理所有活动飞星和到达特效。

### 验证记录

- `rg` 静态确认旧单实例字段 `postcardStarSequence`、`currentPostcardStar`、`hasPlayedPostcardStarArrivalFeedback`、`PlayPostcardStarArrivalFeedback` 已无残留。
- `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|DB\.|SavePostcard|RemoteConfig|PlayerPrefs|YooAsset|Analytics|IAP|Advertisement" Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：无命中。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`：通过，仅有当前工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

### 剩余风险

- 尚未运行 Unity PlayMode 验证连续快速收集 2 个以上 star 时，多个飞星、到达特效和共享 HUD 弹性反馈的实际叠加观感。
- 尚未在 Android/iOS 横屏设备验证连续实例化飞星的帧率和触控稳定性；当前实现只在 collect 事件触发时创建短生命周期实例，不新增空闲期循环。
- HUD 数字滚动、`+N` 浮字和星星块缩放仍由共享 `PropBlock` 承担，快速连续到达时后一次反馈会打断前一次共享 HUD 缩放，但不会打断飞星本体和到达特效。

### 回滚路径

- 在 `PlayPostcardStarAsync(...)` 开头恢复调用 `StopPostcardStarFlight()`，并将活动列表恢复为单个 `postcardStarSequence` / `currentPostcardStar` 状态。
- 在 `PlayPostcardStarArriveEffect(...)` 中恢复进入前调用 `StopPostcardStarArriveEffect()`，使每次到达只保留最新一组到达特效。

## 2026-07-13 补充：starBlock 开局保持隐藏

### 需求背景

用户要求 `GamePlayPanel` 的 `starBlock` 在关卡开局时以 `alpha=0` 初始化，不显示星星栏；只有实际获得明信片 star 时才显示。

### 影响文件与系统

- `Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
- `Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`
- 相关 UI 流仅包含 `GamePlayPanel.OnOpen()`、`PostcardGetStarEvent` 和既有飞星/HUD 反馈。

本次未修改 `PostcardGetStarEvent` 定义、发奖、背包、DB/save、配置、资源加载、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 原有行为基线

- `GamePlayPanel.OnOpen()` 已调用 `HidePostcardStarBlockAtLevelStart()`，但默认 `GamePlayPanel.prefab` 中 starBlock 的 `CanvasGroup.m_Alpha` 仍为 `1`，在面板打开回调完成前存在首帧显示风险。
- `TryResolveStarBlockRefs()` 只获取现有 `CanvasGroup`；特殊面板变体没有该组件时，开局隐藏入口无法修改 alpha。
- 脚本和两个面板 prefab 仍保留不再使用的 `starBlockInitialHoldDuration`，脚本中也保留无调用者的开局展示方法。

### 当前行为与 UI 流

- 默认 `GamePlayPanel.prefab` 的 starBlock `CanvasGroup.m_Alpha` 初始化为 `0`，资产加载后即保持透明。
- `GamePlayPanel.OnOpen()` 继续从运行时外层调用 `HidePostcardStarBlockAtLevelStart()`；若当前 starBlock 缺少 `CanvasGroup`，运行时会补齐组件并立即设置为隐藏，不在编辑器生命周期创建组件。
- 已删除开局展示方法、通用开局停留方法及 `starBlockInitialHoldDuration` 序列化项，避免后续误恢复开局显示路径。
- 当前脚本中设置 `SetStarBlockVisibleState(true)` 的两个入口只位于 `OnGetStar(PostcardGetStarEvent)` 启动的收集流程：飞星开始时淡入，飞星到达时刷新反馈并重新计算隐藏计时。
- 获星后的飞行、数字刷新、`+N`、到达特效、弹性反馈和延迟淡出保持原逻辑；关闭面板仍统一停止并隐藏。

### 验证记录

- `git diff --check` 对本次脚本和两个面板 prefab 通过，仅有仓库现有 LF/CRLF 转换提示。
- 静态扫描确认 `ShowPostcardStarBlockAtLevelStart`、`ShowStarBlockForHold`、`starBlockInitialHoldDuration` 已不再存在于代码和 prefab；旧名称只保留在本文件的历史回滚说明中。
- 静态扫描确认 `SetStarBlockVisibleState(true)` 仅保留在获星事件的开始/到达反馈链路；`GamePlayPanel.OnOpen()` 仍只调用隐藏入口。
- 风险关键字扫描没有发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、配置、资源或平台链路；命中的 `SetUpdate(true)` 与 `Animator.Update(0f)` 均为既有动画代码。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未进入有效脚本编译：`Temp/bin/Debug` 缺少 191 个 Unity/Package 元数据程序集。

### 剩余风险与未验证项

- 尚未运行 Unity PlayMode，需要验证默认面板和 `GamePlayPanel_025_70` 开局均无星星栏闪现，获得 star 后能够正常淡入、刷新并延迟隐藏。
- 尚未执行 Android/iOS 横屏 smoke；移动端首帧 UI 合成与获星动画表现仍未验证。
- 默认 `GamePlayPanel.prefab` 在任务开始前已有与本需求无关的未提交修改，本次只调整 starBlock 的 alpha 和移除废弃序列化项，未处理其他既有差异。

### 回滚路径

- 将默认 `GamePlayPanel.prefab` 的 starBlock `CanvasGroup.m_Alpha` 恢复为 `1`。
- 恢复 `starBlockInitialHoldDuration`、`ShowPostcardStarBlockAtLevelStart()` 和 `ShowStarBlockForHold(...)`，并把两个 prefab 的序列化值写回。
- 删除 `TryResolveStarBlockRefs()` 中运行时补齐 `CanvasGroup` 的分支，即可恢复本次修改前的开局可见性处理。

## 2026-07-22 补充：UIRespawnExplosion 到达特效生命周期修复

### 需求背景

- 默认 `GamePlayPanel` 收集明信片星星后会实例化 `UIRespawnExplosion`，DOTween Safe Mode 持续报告目标 `RectTransform`、`CanvasGroup` 已销毁，并报告完成回调访问已销毁对象。
- `Editor.log` 将回调异常定位到 `GamePlayPanel.PostcardStar.cs` 的到达特效完成回调。

### 影响文件与系统

- `Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
- 明信片星星抵达 HUD 图标后的本地 UI 到达特效生命周期。
- 不修改共享 `UIRespawnExplosion.prefab`、其他 prefab 使用方、明信片发奖、事件、DB/save、配置、资源路径、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 原有行为基线与根因

- `UIRespawnExplosion` 根粒子系统序列化为 `duration=1s`、`stopAction=Destroy`，且根发射模块关闭；它会在外层到达特效 Sequence 完成前销毁整个实例。
- `GamePlayPanel` 同时对实例根 `RectTransform` 和运行时 `CanvasGroup` 播放 `1.1s` 缩放、淡出，并在完成回调销毁实例。
- 根粒子约 `1s` 提前销毁后，最后约 `0.1s` 的 Tween 仍访问目标；完成回调还在判空前读取 `effectRect.gameObject`，因此一次触发可产生两个目标丢失错误和一个 callback 错误。

### 修复后行为

- 只在 `GamePlayPanel` 创建出的 UI 到达特效实例上，把根粒子的 `ParticleSystemStopAction.Destroy` 改为 `None`；共享 prefab 资产及其他使用方保持不变。
- UI 到达特效继续按原配置播放 `1.1s` 缩放与淡出，完成后由既有 Sequence 统一销毁实例。
- Sequence 使用 `SetLink(effectInstance, KillOnDestroy)`；若实例仍被其他路径提前销毁，Tween 会随实例终止。
- 完成和终止回调统一使用已捕获的 `effectInstance` 清理活动实例列表，不再通过可能已销毁的 `effectRect` 读取 `gameObject`。

### 验证记录

- 静态确认修改只位于 UI 到达特效分支；非 UI 到达特效和共享 prefab 未修改。
- `git diff --check -- Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 风险关键字扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、资源或平台链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 warning / 0 error。

### 剩余风险与未验证项

- 尚未运行 Unity PlayMode 连续收集验证；需要清空 Console 后连续收集多个明信片星星，确认 `UIRespawnExplosion(Clone)` 不再产生 DOTween Safe Mode 错误，并确认 1.1 秒视觉完整。
- 失败、重开、胜利、退出关卡期间的到达特效中断清理仍需 PlayMode 覆盖。
- Android/iOS 横屏真机表现和帧率尚未验证；本次不新增循环或额外实例，仅修改既有实例的生命周期归属。

### 回滚路径

- 删除运行时根粒子 `stopAction` 覆盖、Sequence `SetLink` 和终止回调中的实例列表清理，并把完成回调恢复为通过 `effectRect.gameObject` 清理，即可恢复修改前行为。
