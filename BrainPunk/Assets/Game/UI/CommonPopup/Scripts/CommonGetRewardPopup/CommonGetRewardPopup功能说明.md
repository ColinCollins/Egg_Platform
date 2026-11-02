# CommonGetRewardPopup 功能说明

## 2026-09-14 ClickBlock 停用清理

- 需求：只对本通用领奖弹窗处理停用时的全屏 ClickBlock 与关闭时序。
- 影响文件：`CommonGetRewardPopup.cs`；Prefab、奖励配置、Bag、广告事件与资源加载未改。
- 原行为：关闭金币动画期间 `_isClosing` 与 ClickBlock 会开启；若弹窗在动画结束前被其他 UI 隐藏，异步结束时因 `isActiveAndEnabled` 为 false 不销毁，返回后仍处于关闭锁。
- 新行为：`OnDisable` 关闭本地 ClickBlock；`OnEnable` 根据 `_isClosing` 恢复未完成的关闭锁。金币动画若在隐藏期间结束，保留关闭状态并标记待关闭；弹窗再次显示后延后一帧销毁。隐藏时不直接销毁非栈顶 UI，避免共享 UIManager 重播当前栈顶的 `OnShow`。重新 `Create` 时递增关闭版本并清除旧待关闭标记，使旧异步关闭不会销毁新内容。
- 数据与事件流：奖励已发放和回调仍按原路径执行；隐藏期间结束的关闭回调在弹窗重新显示并实际销毁时触发，不要求玩家再次点击，也不重播关闭音效和金币动画。
- 静态场景复核：正常可见关闭仍立即走 `DestroyUI`；隐藏中完成关闭只登记待关闭，恢复可见后重检 `IsOpen/IsVisible/isActiveAndEnabled` 再销毁；外部先销毁时 `OnClose` 清标记，延迟任务不再重复销毁；新 `Create` 的版本号使旧关闭任务失效。快速隐藏再显示时，即使 GameObject 尚未真的停用，`OnShow` 也会检查待关闭标记。
- 验证：静态核对 `ClosePopupAsync`、`BaseUIView.OnHide/OnShow`、`CommonUIRewardAnimCtrl.OnDisable` 与 `UIManager.DestroyUI`；`git diff --check` 通过。用当前 `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp` 和 Unity Roslyn 在隔离临时目录完整编译最新运行时源码，结果 `EXIT=0 / ERRORS=0`。普通 dotnet build 缺少 Unity `Temp/bin/Debug` 元数据 DLL，旧 `1300b0aP.dag` 响应文件因迁移前路径报 CS2001，均未用作本次编译结论。PlayMode、快速覆盖往返及 Android/iOS 触控仍为 `uncertain`。
- 回滚：撤销本次 `OnDisable` 清理、隐藏期间的待关闭标记与关闭版本门禁，恢复原关闭时序；隐藏后可能再次出现无法点击。

## 2026-07-03 奖励展示与皮肤奖励接入

### 需求背景

- 通用奖励弹窗需要根据奖励内容展示 Coin、Star 和 Skin。
- Skin 奖励不能使用固定占位 sprite，需要根据 `rewards[GameProps.Skin]` 中的皮肤 id 查找 `Config.Skin`，再读取该皮肤配置的 `TextureResources` 对应真实图标。
- 广告倍数 `multiple` 只影响 Coin / Star，Skin 只发放一次，不参与倍数计算。
- 原脚本内已有注释保持不改动。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - 处理弹窗数据过滤、按钮绑定、普通奖励发放、广告额外奖励发放、Skin 图标异步加载。
- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonRewardItem.cs`
  - 继续作为奖励 item 的 sprite/count 刷新组件，本次未改动。
- `Assets/Game/UI/CommonPopup/Resources/CommonGetRewardPopup.prefab`
  - 移除已不再使用的 `skinSprite` 序列化残留，保留 `coinSprite` 和 `starSprite`。
- 依赖但未修改的系统：
  - `Game.ConfigModule.ConfigManager` / `TbSkin.GetOrDefault(skinId)`：解析具体皮肤配置。
  - `SkinUtils.ResolveIconLocation(Config.Skin)` / `SkinUtils.LoadIconAsync(...)`：解析并加载真实皮肤图标。
  - `PlayCtrl.Instance.Bag.AddTool(...)`：发放 Coin / Star。
  - `PlayCtrl.Instance.Bag.GetNewSkin(...)`：发放 Skin。
  - `RewardAdHelper.TryToShowRewardAd(...)`：广告领取入口。

### 原行为基线

- 弹窗打开后立即发放普通奖励。
- 点击广告按钮成功后，按 `multiple - 1` 发放额外奖励。
- 奖励 item 通过 `GameProps` 映射到本地序列化 sprite。
- 当前工作区已有实现曾把 `GameProps.Skin` 放入通用奖励过滤路径，但没有可用的 `skinSprite` 字段，且无法按具体皮肤 id 读取真实图标。

### 新行为

- `_rewards` 只保存 Coin / Star，这两类奖励继续参与普通领取和广告倍数追加。
- `Create(Dictionary<GameProps, object> rewards, int multiple = 1)` 支持从 rewards 字典同时解析 Coin、Star 和 Skin：
  - `GameProps.Skin` 的 value 用于查找 `ConfigManager.Instance.Tables.TbSkin` 中的具体 `Config.Skin`。
  - 弹窗为该 Skin 创建一个奖励 item，并通过 `SkinUtils` 异步加载 `TextureResources` 对应图标。
  - 普通领取时调用 `Bag.GetNewSkin(_skinReward.Id)` 发放皮肤。
  - 广告领取时只调用 Coin / Star 的额外发放逻辑，不重复发放 Skin。
- Skin 仍由通用 rewards 字典传入；弹窗内部缓存为 `_skinReward` 后只走 Skin 图标和授予流程，不进入 Coin / Star 的数量发放和广告倍数发放路径。
- 当仅有 Skin 奖励且没有 Coin / Star 时，广告按钮不会显示，因为没有可被倍数追加的数量奖励。

### 数据、资源与 UI 流程

1. 调用方打开弹窗，传入包含 Coin / Star 数量和可选 `GameProps.Skin -> skin.Id` 的奖励字典。
2. `SetData(...)` 过滤 Coin / Star 到 `_rewards`，并通过 `GameProps.Skin` 的 value 查找 `_skinReward`。
3. `RefreshRewardItems()` 为 Coin / Star 创建数量 item；若存在 `_skinReward`，额外创建 Skin item。
4. Skin item 先显示数量 `x1`，随后异步解析并加载 `Skin.TextureResources` 对应 sprite。
5. 异步加载完成前如果弹窗关闭、重刷或 item 被销毁，版本号保护会阻止旧结果回写。
6. 普通奖励发放时：
   - Coin / Star 调用 `Bag.AddTool(..., RewardType.Other)`。
   - Skin 调用 `Bag.GetNewSkin(_skinReward.Id)`。
7. 广告成功后：
   - Coin / Star 调用 `Bag.AddTool(..., RewardType.Ad)` 发放 `multiple - 1` 份额外奖励。
   - Skin 不再发放，也不乘以 `multiple`。

### 验证记录

- 已运行：
  - `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`
- 结果：
  - 0 个错误。
  - 0 个警告。

### 未覆盖风险

- 尚未在 Unity PlayMode 中实际打开弹窗验证 Skin 图标加载、关闭时异步回写保护和广告按钮显隐。
- 尚未在 Android/iOS 横屏真机上验证 UI 布局、触控和资源包内 Skin 图标可加载性。
- 如果 `GameProps.Skin` 的 value 不是 `Config.Skin` 或有效 skin id 字符串，弹窗不会展示或发放 Skin。

### 回滚路径

- 回滚 `CommonGetRewardPopup.cs` 中 `GameProps.Skin` 字典解析、`_skinReward` 内部缓存、Skin item 创建、Skin 图标加载和 `GrantSkinReward()` 调用。
- 如需恢复固定图标方案，可重新启用 prefab 和脚本中的 `skinSprite` 字段，但这会失去按具体 `skin.Id` 展示真实 texture 的能力。
- prefab 回滚时重新补回 `skinSprite` 序列化字段即可恢复旧序列化结构。

## 2026-07-03 奖励字典 object 入参兼容

### 需求背景

- GMPanel 的 `GetRewardTest1` 需要在同一个奖励字典里放入 Coin、Star 数量和 Skin id，用于验证通用奖励弹窗的混合奖励链路。
- `CommonGetRewardPopup` 的公开 `Create(...)` 当前接收 `Dictionary<GameProps, object>`，内部需要与公开 API 对齐，避免 `Dictionary<GameProps, int>` 与 `Dictionary<GameProps, object>` 类型不一致导致编译失败。

### 新行为

- `SetData(...)` 现在接收 `Dictionary<GameProps, object>`。
- Coin / Star 的 value 会通过 `TryGetRewardCount(...)` 转为数量，支持 `int`、安全范围内的 `long`、`float`、`double` 和可解析为整数的字符串。
- Skin 的 value 会通过 `ResolveSkinReward(...)` 解析：
  - value 为 `Config.Skin` 时直接使用该配置。
  - 其他 value 使用 `ToString()` 作为 `skinId` 查 `TbSkin`。
- Skin 从 `rewards[GameProps.Skin]` 解析，外部不需要额外传 Skin 参数；解析后的 Skin 仍只发放一次，不参与广告倍数。

### 验证记录

- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`。
- 结果为 0 warning / 0 error。

### 未验证风险

- 尚未在 Unity PlayMode 中实际用 `Dictionary<GameProps, object>` 打开弹窗验证 object value 的所有兼容类型。
- GMPanel 当前覆盖 `int` 数量和 `GameProps.Skin -> string skinId` 组合路径。

## 2026-07-03 ParamBase 解析抽离到配置工具层

### 需求背景

- `CommonGetRewardPopup` 已需要兼容 Luban `Dictionary<GameProps, Config.ParamBase>` 奖励入参。
- 弹窗内部不应继续持有 `ParamBase` 派生类型拆包和 Value 反射读取逻辑，因此按要求抽离到 `Assets/Game/Scripts/Config/BaseParamUtils.cs`。

### 新行为

- `CommonGetRewardPopup.ResolveSkinReward(...)` 不再直接识别 `Config.ParamBase`，改为调用 `BaseParamUtils.TryGetString(...)` 取得 skin id。
- `CommonGetRewardPopup.TryGetRewardCount(...)` 不再处理 `int`、`long`、`float`、`double`、`string` 和 `ParamBase` 的分支，统一调用 `BaseParamUtils.TryGetInt(...)`。
- 弹窗仍保留 Skin 查表、Coin / Star 发放、Skin 发放、广告倍数和 UI 刷新职责；配置参数拆包职责已移到配置工具层。

### 验证记录

- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`。
- 因当前本地 `Assembly-CSharp.csproj` 是被忽略的 Unity 生成文件，新脚本首次未进入编译列表；临时加入 `Assets/Game/Scripts/Config/BaseParamUtils.cs` 后复跑通过，结果为 0 error / 97 warnings。

### 未验证风险

- 尚未在 Unity Editor 重新生成 csproj 后确认 `BaseParamUtils.cs` 自动进入工程。
- 尚未在 PlayMode 中用真实 `PostcardConfig.Rewards` 打开弹窗验证 `IntParam` / `StringParam` 解析与奖励发放。
- Android/iOS 横屏弹窗表现、广告成功回调和 Skin 图标资源加载仍待补验。

## 2026-07-03 Coin 奖励关闭动画与 PropBlock 刷新

### 需求背景

- `CommonGetRewardPopup` 在存在 Coin 奖励时，关闭界面前需要先播放通用 Coin 飞行动画。
- 飞行动画完成后需要刷新弹窗内的 `PropBlock`，让 Coin 数量显示与已经写入的背包数据对齐，然后再关闭弹窗。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - 关闭入口改为异步关闭链路，普通关闭按钮、领取按钮和广告成功关闭共用同一逻辑。
  - 记录 Coin 奖励 item，作为 `rewardAnimCtrl.PlayFrom(...)` 的起点。
- `Assets/Game/UI/CommonPopup/Resources/CommonGetRewardPopup.prefab`
  - 当前 prefab 已绑定 `PropBlock prop` 和 `CommonUIRewardAnimCtrl rewardAnimCtrl`；本次未改 prefab。
- 依赖但未修改的系统：
  - `CommonUIRewardAnimCtrl.PlayFrom(Sprite, Transform, int)`：播放飞行动画。
  - `PropBlock.UpdateData()`：从 `PlayCtrl.Instance.Bag` 读取最新数量并刷新文本。

### 原行为基线

- 弹窗点击普通领取、关闭按钮或广告成功后会直接 `DestroyUI(this)`。
- Coin 奖励在打开弹窗时已经通过 `Bag.AddTool(...)` 发放，广告成功后额外 Coin 也会先写入背包；关闭前没有专门等待飞行动画或手动刷新 `PropBlock`。

### 新行为

- `ClosePopup()` 现在转入 `ClosePopupAsync()`。
- 如果 `_rewards` 中存在 `GameProps.Coin`：
  - 先把按钮置为不可交互，避免关闭动画期间重复点击。
  - 使用 Coin 奖励 item 的 transform 作为起点调用 `rewardAnimCtrl.PlayFrom(coinSprite, startPoint)`。
  - 动画尝试完成后在 `finally` 中调用 `prop.UpdateData()`，即使动画组件缺失或播放失败，也会刷新 `PropBlock`。
  - 刷新完成后关闭弹窗。
- 如果没有 Coin 奖励，仍按原链路直接关闭，不播放飞行动画，也不强制刷新 `PropBlock`。

### 验证记录

- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`。
- 结果为 0 warning / 0 error。

### 未验证风险

- 尚未在 Unity PlayMode 中实际验证 Coin-only、Coin+Star+Skin、Skin-only 三种关闭路径。
- 尚未验证 `rewardAnimCtrl` 起点到 `PropBlock` 终点的实际飞行动画位置与视觉表现。
- 尚未在 Android/iOS 横屏真机验证关闭期间按钮禁用、动画播放和 `PropBlock` 数字刷新。

### 回滚路径

- 回滚 `CommonGetRewardPopup.cs` 中 `_coinRewardItem`、`_isClosing`、`ClosePopupAsync()`、`PlayCoinRewardAnimIfNeededAsync()` 和 `GetCoinRewardAnimStartPoint()` 相关逻辑。
- 将 `ClosePopup()` 恢复为直接 `UIManager.Instance?.DestroyUI(this)`，即可恢复旧关闭时序。

## 2026-07-03 Skin 图标 NativeSize 限框显示

### 需求背景

- Skin 奖励图标使用真实皮肤 texture，不能简单按 Coin/Star 的固定 icon 尺寸直接替换。
- Skin 图标刷新时需要先执行 `Image.SetNativeSize()`，保留原图宽高比例，再确保最终尺寸不会超过奖励 item 的 icon 框。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonRewardItem.cs`
  - 新增 `RefreshSkin(Sprite sprite, int count)`，只给 Skin 奖励图标使用。
  - 在单次刷新内读取 icon 当前框尺寸，并在 `SetNativeSize()` 后按该框等比限制。
- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - Skin item 初始化和异步加载完成后的 sprite 回写改为调用 `RefreshSkin(...)`。
- `Assets/Game/UI/CommonPopup/Prefabs/CommonRewardItem.prefab`
  - 当前 icon 初始 RectTransform 为 `112 x 112`，本次未修改 prefab。

### 原行为基线

- `CommonRewardItem.Refresh(Sprite, int)` 直接替换 `icon.sprite` 和数量文本。
- Coin / Star 使用该固定显示路径；Skin 图标加载完成后也曾走同一 `Refresh(...)`，不会按真实 texture 做 native size。

### 新行为

- Coin / Star 仍使用 `Refresh(Sprite, int)`，不改变原固定尺寸表现。
- Skin 使用 `RefreshSkin(Sprite, int)`：
  - 先刷新数量文本。
  - 读取 icon 当前框尺寸。
  - sprite 为空时只清空图标，不做 NativeSize 缩放。
  - sprite 有效时先 `SetNativeSize()`，再按 icon 初始框宽高计算等比缩放，限制在框内。

### 验证记录

- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`。
- 结果为 0 error / 1115 warnings；warnings 属于当前工程输出，本次未逐项审计。

### 未验证风险

- 尚未在 Unity PlayMode 中用真实 Skin texture 验证不同宽高比皮肤图标的最终显示尺寸。
- 尚未在 Android/iOS 横屏真机验证皮肤奖励 item 文本、图标和边框无重叠。

### 回滚路径

- 回滚 `CommonRewardItem.RefreshSkin(...)` 及其限框辅助方法。
- 将 `CommonGetRewardPopup` 的 Skin item 初始化和图标回写恢复为 `Refresh(...)`，即可回到固定尺寸显示路径。

## 2026-07-06 关闭回调接入

### 需求背景

- 调用方需要在 `CommonGetRewardPopup` 关闭时执行后续逻辑，且不能绕过现有普通领取、关闭按钮、广告成功后关闭和 Coin 飞行动画链路。
- 回调只用于通知弹窗关闭，不应参与奖励发放、广告结果判定、存档、资源加载或平台 SDK 流程。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - 新增实例级 `_closeCallback`。
  - 三个公开 `Create(...)` 入口增加可选 `Action callback = null` 参数。
  - `OnClose()` 中清理按钮和异步图标状态后调用关闭回调。
- 本次未修改 `CommonGetRewardPopup.prefab`、奖励 item prefab、Luban 配置、DB/save 字段、广告 placement、analytics、IAP 或平台 SDK。

### 原行为基线

- 弹窗通过 `Create(...)` 打开后立即发放普通奖励。
- 普通按钮、关闭按钮和广告成功回调最终都会进入 `ClosePopupAsync()`，存在 Coin 奖励时先播放 Coin 飞行动画并刷新 `PropBlock`，再调用 `UIManager.Instance?.DestroyUI(this)`。
- `OnClose()` 原本只负责解绑按钮、递增 Skin 图标加载版本、清空 Skin item 引用和重置广告请求状态。

### 新行为

- `Create(Dictionary<GameProps, object> rewards, int multiple = 1, Action callback = null)` 支持传入关闭回调。
- `Create(Dictionary<GameProps, Config.ParamBase> rewards, int multiple = 1, Action callback = null)` 支持配置奖励入口传入关闭回调。
- `Create(Config.PostcardConfig postcardConfig, Action callback = null)` 支持明信片配置入口传入关闭回调；配置为空导致弹窗未打开时不会调用回调。
- 回调保存在当前弹窗实例，`OnClose()` 调用 `InvokeCloseCallback()` 时会先取出并置空，再执行 `callback?.Invoke()`，确保同一实例关闭生命周期内最多触发一次。

### 流程边界

1. 调用方打开弹窗并可选传入 `callback`。
2. 弹窗正常展示、发放普通奖励、等待用户普通关闭或广告成功关闭。
3. 关闭路径仍先执行原有 Coin 飞行动画和 `PropBlock` 刷新。
4. `UIManager.DestroyUI(this)` 触发 `OnClose()` 后，弹窗完成按钮解绑和异步状态清理，再调用关闭回调。
5. 回调异常未在弹窗内部吞掉，调用方需要保证回调逻辑自身安全。

### 验证记录

- 已运行 `git diff --check -- Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`，结果通过；仅提示该文件后续会按 Git 设置进行 LF/CRLF 转换。
- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`。
- 编译结果为 0 error / 97 warnings；warnings 属于当前工程既有输出范围，本次未逐项审计。

### 未验证风险

- 尚未在 Unity PlayMode 中实际验证普通关闭、关闭按钮、广告成功关闭和外部销毁 UI 四类路径的回调触发次数。
- 尚未在 Android/iOS 横屏真机验证关闭回调中继续打开其他 UI 时的输入和层级表现。
- 若调用方回调内部抛出异常，当前弹窗不捕获该异常，后续需要按调用方场景单独处理。

### 回滚路径

- 回滚 `CommonGetRewardPopup.cs` 中 `_closeCallback`、`SetCloseCallback(...)`、`InvokeCloseCallback(...)`、`OnClose()` 回调调用和三个 `Create(...)` 的 `callback` 参数。
- 回滚后弹窗恢复为关闭时不通知调用方的旧行为，奖励发放和关闭动画链路不受额外回调影响。

## 2026-07-14 Coin 动画结束后的数字展示停留

### 需求背景

- Coin 奖励在弹窗打开时已经写入背包，但领取关闭时仍需先完成飞行动画，再刷新 `PropBlock` 数字，并短暂停留后关闭，保证玩家能看到数字变化。
- `CommonUIRewardAnimCtrl` 默认使用 unscaled time；关闭链路中的停留也需要保持相同时间语义，避免游戏暂停或 `Time.timeScale == 0` 时弹窗无法结束关闭。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - 完善 `PlayCoinRewardAnimIfNeededAsync()` 的 Coin 数量校验、销毁取消和动画后停留逻辑。
- `Assets/Game/UI/CommonPopup/Resources/CommonGetRewardPopup.prefab`
  - 当前工作区将内嵌 `PropBlock.IsAutoUpdate` 设为 `false`，避免 Coin 发放事件在飞行动画前抢先刷新数字；`PropBlock.OnEnable()` 仍负责绑定弹窗打开时的初始数量。
- 依赖但未修改：`CommonUIRewardAnimCtrl.PlayFrom(...)`、`PropBlock.UpdateData()`、背包奖励发放和既有关闭回调链路。

### 原行为基线

- `_rewards` 包含 Coin 时，关闭流程等待飞行动画并在 `finally` 中刷新 `PropBlock`；没有 Coin 时直接关闭。
- 当前工作区初始实现会在刷新后等待 1 秒，但等待使用 scaled time，且弹窗在等待前被外部销毁时仍会继续启动该等待。

### 新行为与 UI 流程

1. `PlayCoinRewardAnimIfNeededAsync()` 使用 `TryGetValue(...)` 确认 Coin 奖励数量大于 0，否则不进入动画和停留流程。
2. 保存当前弹窗的 `destroyCancellationToken`，然后沿用 Coin item transform 作为起点尝试播放飞行动画。
3. 无论动画正常完成、组件缺失或播放过程抛出异常，只要弹窗仍处于 active 状态且 `prop` 有效，就在 `finally` 中调用 `prop.UpdateData()`。
4. 数字刷新后使用 unscaled time 停留 1 秒；弹窗销毁会取消等待，`SuppressCancellationThrow()` 将该正常生命周期取消收束在方法内部。
5. 停留完成后仍由 `ClosePopupAsync()` 的 `finally` 销毁 UI，`OnClose()` 继续作为关闭回调边界。

本次不改变 Coin 发放数量与时机、广告倍数、Skin/Star 奖励、配置、存档、资源地址、全局事件、分析打点、IAP 或平台 SDK 流程。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`
  - 结果：0 error / 98 warnings。
- `git diff --check -- Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs Assets/Game/UI/CommonPopup/Resources/CommonGetRewardPopup.prefab`
  - 结果：通过；仅提示工作区 LF 后续会按 Git 设置转换为 CRLF。
- 静态扫描确认目标方法没有新增编辑器生命周期、全局事件或每帧轮询。

### 未验证风险

- 尚未在 Unity PlayMode 实测 Coin-only、Coin+其他奖励、无 Coin、缺少 `rewardAnimCtrl` 和外部销毁弹窗路径。
- 尚未目视确认 `PropBlock` 数字滚动完成与额外 1 秒停留的最终节奏。
- 尚未在 Android/iOS 横屏真机验证暂停态、触控锁定和关闭回调衔接。

### 回滚路径

- 回滚 `PlayCoinRewardAnimIfNeededAsync()` 中正数校验、`destroyCancellationToken`、unscaled 1 秒等待和 `SuppressCancellationThrow()`，即可恢复修改前的关闭时序。
- 如需恢复 Coin 发放事件即时刷新数字，将 `CommonGetRewardPopup.prefab` 内嵌 `PropBlock.IsAutoUpdate` 恢复为 `true`；该回滚会取消“飞行动画结束后再显示新数量”的视觉时序。

## 2026-07-17 领奖关闭动画期间启用 ClickBlock

### 需求、原行为与影响范围

- 需求：`CommonGetRewardPopup` 接受领取/关闭操作后，从领奖关闭动画开始到弹窗真正关闭期间禁止任何额外点击；明信片完整引导中的领奖步骤也必须遵守该输入锁。
- 原行为：`_isClosing` 会把普通领取、广告和关闭按钮设为不可交互，但没有覆盖整个奖励弹窗的射线遮罩；领奖动画期间仍可能点击到内嵌 `PropBlock` 或其它可接收射线的内容。
- 影响文件：`CommonGetRewardPopup.cs`、`CommonGetRewardPopup.prefab` 和本说明文档。没有修改奖励发放数量/时机、广告结果、背包、DB/save、配置、资源地址、全局事件、analytics、IAP 或平台 SDK。

### 新行为与 UI 流程

1. Prefab 在普通内容之后增加全屏 `ClickBlock.prefab` 实例，默认 inactive，并绑定到 `CommonGetRewardPopup.clickBlock`。
2. 普通领取、关闭按钮和广告成功最终都进入 `ClosePopup()`；方法先拒绝重复请求，再同步设置 `_isClosing=true`、开启 `ClickBlock` 并刷新按钮状态。
3. 随后才播放既有领取音效、Coin 飞行动画、`PropBlock` 数字刷新和 unscaled 1 秒停留；遮罩在整个异步阶段持续开启。
4. `DestroyUI(this)` 触发 `OnClose()` 后关闭遮罩并执行既有关闭回调。`OnOpen()` 与 `SetData(...)` 也会重置遮罩，防止缓存实例继承上次状态。
5. 明信片领奖 GuideFinger 动态创建在 Popup 最后子节点，首次点击回调后会解除订阅并销毁；它能发起第一次领取，但不会在遮罩开启后提供第二次点击入口。

### 验证、风险与回滚

- 静态核对确认 `_isClosing` 在任何异步等待前同步设置，`ClosePopupAsync()` 不再承担延迟锁定；普通领取、关闭按钮和广告成功共用同一入口。
- Prefab 静态证据确认 ClickBlock 全拉伸、可接收射线、默认关闭、位于普通内容之后且序列化绑定有效。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过：0 errors / 99 warnings；warnings 为当前工程输出，本次未逐项审计。
- 尚未在 Unity PlayMode 验证 Coin/无 Coin、普通领取、关闭按钮、广告成功、明信片 GuideFinger、快速连点及外部销毁；Android/iOS 横屏和多指输入也未验证。
- 回滚时删除 `SetClickBlocked(...)` 和 `ClosePopup()` 中的遮罩同步，并从 prefab 移除 ClickBlock 实例/绑定；按钮的 `_isClosing` 防重复逻辑和奖励流程可继续保留。

## 2026-07-23 普通奖励发放开关

### 需求背景

- 外部业务可能已经完成普通奖励入包，此时仍需要复用 `CommonGetRewardPopup` 展示奖励内容和关闭流程。
- 原接口打开弹窗后会在 `SetData(...)` 末尾无条件执行 `GrantNormalReward()`；外部先发奖再打开弹窗会让 Coin、Star 或 Skin 重复领取。
- 本次需要保留既有调用方默认行为，同时提供纯展示模式，让奖励真实获取逻辑可以由外部业务负责。

### 影响范围

- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - 三个公开 `Create(...)` 入口新增可选参数 `grantNormalReward`。
  - `SetData(...)` 根据该参数决定是否调用 `GrantNormalReward()`。
- 本次未修改现有调用方、prefab、奖励配置、DB/save 字段、资源地址、全局事件、分析打点、IAP 或平台 SDK。
- 现有 Coin 关闭动画、`PropBlock` 刷新、Skin 图标加载、关闭回调和广告成功后的额外奖励流程保持不变。

### 原行为基线

1. 任一 `Create(...)` 入口打开弹窗并解析奖励数据。
2. `SetData(...)` 刷新奖励 item 和按钮后，无条件执行 `GrantNormalReward()`。
3. 普通奖励中的 Coin / Star 通过 `Bag.AddTool(..., RewardType.Other)` 入包，Skin 通过 `Bag.GetNewSkin(...)` 获取。
4. 广告成功后由 `GrantAdReward()` 按 `multiple - 1` 发放额外 Coin / Star。

### 新行为与调用边界

1. 三个 `Create(...)` 入口末尾新增 `bool grantNormalReward = true`，因此所有既有调用方不传参数时仍保持打开即发放普通奖励。
2. 外部已经完成普通奖励入包时，使用命名参数 `grantNormalReward: false`：

   ```csharp
   CommonGetRewardPopup.Create(
       rewards,
       multiple,
       callback,
       grantNormalReward: false);
   ```

3. `grantNormalReward` 为 `false` 时，弹窗仍解析并展示 Coin、Star、Skin，仍保留普通关闭、Coin 飞行动画、`PropBlock` 刷新和关闭回调，但不调用 `GrantNormalReward()`，因此不会再次写入普通数量奖励或 Skin。
4. 该开关只控制 normal 奖励。广告成功后的 `multiple - 1` 额外 Coin / Star 仍由弹窗发放；调用方若要求完全只展示，应传入不启用广告倍数的展示数据，或另行定义广告奖励归属。
5. 外部发奖的成功性、幂等、存档和失败回滚由调用方负责；弹窗不会校验外部是否已经发奖。

### 验证记录

- CodeGraph 重新读取修改后的 `SetData(...)` 和三个 `Create(...)`，确认 `GrantNormalReward()` 只剩一个受 `grantNormalReward` 条件保护的调用点。
- 已运行：
  - `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`
- 结果：编译通过，0 个错误、0 个警告。
- 首次执行同一编译命令时使用了过短的工具超时，命令被中断且没有形成编译结论；随后完整重跑成功。

### 未验证风险

- 尚未在 Unity PlayMode 实测 `grantNormalReward: false` 下 Coin、Star、Skin 均只展示且不重复入包。
- 尚未在 Unity PlayMode 验证纯展示模式关闭后的 Coin 飞行动画与 `PropBlock` 数字保持外部发奖后的正确值。
- 尚未在 Android/iOS 横屏真机验证触控、布局和广告按钮流程。
- `DailyChallengePopup` 已接入 `grantNormalReward: false`；该业务在 Popup 打开时由 `DailyChallengeManager` 先完成 normal 发奖和领奖标记，动画完成后再打开本弹窗展示。

### 回滚路径

- 删除三个 `Create(...)` 的 `grantNormalReward` 参数和向 `SetData(...)` 的参数传递。
- 将 `SetData(...)` 恢复为无条件调用 `GrantNormalReward()`，即可恢复弹窗打开即发放普通奖励的旧行为。

## 2026-07-23 数量奖励批量发放下沉 SimpleBag

### 需求、影响与新行为

- 原 `CommonGetRewardPopup.GrantRewards(...)` 内联遍历 `_rewards`，计算 `基础数量 * 倍率` 后逐项调用 `Bag.AddTool(...)`；其他奖励业务无法复用该批量逻辑。
- `SimpleBag` 新增公开 `GrantRewards(...)`，只接收已经解析为 `int` 的数量奖励、`RewardType` 和倍率，并沿用 `AddTool(...)` 的保存、事件与打点行为。
- `CommonGetRewardPopup` 的 normal 数量奖励和广告额外数量奖励均直接调用 `SimpleBag.GrantRewards(...)`，原私有批量循环已删除。
- DailyChallengeManager 也复用该 API 发放 Popup 打开时的 normal Coin/Star；Skin 继续由 `SimpleBag.GetNewSkin(...)` 处理。
- 本次不把 UI 的 `object` / `ParamBase` 解析、Sprite、广告请求或 Skin 查表职责下沉到 SimpleBag。

### 验证、风险与回滚

- CodeGraph 静态复核确认 `SimpleBag.GrantRewards(...) -> AddTool(...)`，CommonGetRewardPopup 与 DailyChallengeManager 均使用该入口。
- C# 工程编译通过：0 个错误、99 个项目既有警告。
- 尚未在 Unity PlayMode 与 Android/iOS 横屏真机验证 normal、广告倍率和 DailyChallenge 前置发奖的实际数量增量。
- 回滚时恢复 CommonGetRewardPopup 原私有 `GrantRewards(...)` 循环，并把 DailyChallengeManager 的数量奖励恢复为逐项 `AddTool(...)`；删除 SimpleBag 新 API 即可，不涉及存档迁移。

## 2026-07-23 Postcard 页面完成接入纯展示模式

- Postcard 正常路径在完成存档成功、扣星之后、启动最后一块解锁动画之前，由 `PostcardPopup` 入口解析并发放 normal Coin、Star、Skin；页面完成处理器在首个异步等待前复用该入口兜底。完成表现结束后调用 `CommonGetRewardPopup.Create(..., grantNormalReward: false)`，避免再次领取。
- 若 Postcard 前置发放因 Bag/GameData 未就绪或配置无法处理而失败，则传 `grantNormalReward: true` 保留弹窗默认发奖降级；因此本弹窗仍只根据调用方明确传入的开关决定 normal 奖励归属。
- 广告成功后的额外 Coin/Star 仍由本弹窗处理，Skin 不参与倍率。Postcard 的持久化幂等、完成存档与奖励存档一致性不属于本弹窗职责。
- C# 工程构建通过：0 errors / 99 warnings；Unity PlayMode、移动端横屏、动画中断和弹窗创建失败仍待验证。具体边界见 `Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md` 与 `PostcardPopup风险评估.md`。

## 2026-07-23 前置发奖模式的 Coin 显示基线

### 需求背景与原行为

- `grantNormalReward:false` 表示 normal 奖励已经由外部入包。弹窗实例激活时，内部 Coin `PropBlock` 会先读取真实余额，因此修改前展开弹窗就直接显示发奖后的数量，后续飞 Coin 动画没有正确的旧值起点。
- DailyChallenge 可能在打开结算 Popup 时一次性前置发放多个阶段。只减当前阶段奖励仍会提前显示后续阶段金额，因此需要允许调用方声明“本次动画后仍需隐藏”的 Coin。

### 新行为与接口边界

- 三个 `Create(...)` 入口新增可选参数 `remainingHiddenCoinAfterReward = 0`，只在 `grantNormalReward:false` 时生效。
- 展开时的显示值为：

  ```text
  真实 Coin - 当前弹窗 normal Coin - remainingHiddenCoinAfterReward
  ```

- 当前飞 Coin 动画结束后的显示目标为：

  ```text
  真实 Coin - remainingHiddenCoinAfterReward
  ```

- Postcard 只有当前一份前置奖励，沿用默认值 `0`，因此展开时减去本次奖励，飞币后滚动到真实余额。
- DailyChallenge 把后续尚未展示阶段的基础 Coin 传入该参数；第一阶段不会提前显示第二阶段金额，广告额外 Coin 会在当前阶段动画结束后进入显示目标。
- 默认 `grantNormalReward:true` 的旧调用保持原行为：展开时保留发奖前显示，弹窗内部入包，飞币后读取真实余额。
- `PropBlock.UpdateData(int)` 仅提供指定 UI 目标值的滚动显示，不读取或修改 Bag；无参 `UpdateData()` 仍用于刷新真实余额。
- 余额减法使用 `long` 计算并把负值钳制为 `0`，不修改奖励数量、存档、事件、广告发放或 Skin 流程。

### 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 个错误、99 个项目既有警告。
- 目标代码 `git diff --check` 通过；静态复核覆盖默认发奖、Postcard 单阶段、DailyChallenge 双阶段和广告额外 Coin 的计算链。
- 尚未在 Unity PlayMode 录屏确认弹窗展开首帧、数字滚动终点、无限 Coin、余额小于配置奖励、双阶段分别看广告，以及 Android/iOS 横屏真机表现。
- 回滚时删除 `remainingHiddenCoinAfterReward`、前置余额计算和 `PropBlock.UpdateData(int)`，恢复关闭动画后统一调用无参 `UpdateData()` 即可；不涉及数据迁移。

## 2026-09-09 Shop 有序奖励列表与多 Skin 展示

### 需求与基线

- ChestGiftPopup 购买成功后需要使用 CommonGetRewardPopup 展示已发放的 Shop 奖励。
- `Shop.Rewards` 当前为 `List<ShopReward>`，同一礼包包含重复 Skin 类型；原 CommonGetRewardPopup 只接收 Dictionary 或 PostcardConfig，并只缓存、展示和发放第一件 Skin。
- ChestGiftController 在打开通用弹窗前已经完成奖励入包，因此本流程必须使用 `grantNormalReward=false`。

### 新行为与边界

- 新增 `Create(List<ShopReward>, multiple, callback, grantNormalReward, remainingHiddenCoinAfterReward)` 公开入口，按原列表逐项传入解析流程，不把 Shop 奖励压成 Dictionary。
- Coin/Star 重复数量在 UI 内合计；每一项有效 Skin 都保留为独立奖励项并分别异步加载真实图标。
- 默认 `grantNormalReward=true` 的其它入口现在也能处理多个 Skin 并逐件调用 `GetNewSkin`；原单 Skin、Coin 和 Star 调用行为保持。
- ChestGift 使用 `grantNormalReward=false` 与 `multiple=1`：通用弹窗只展示已入包的 Coin/Skin，广告按钮隐藏，不重复发奖。
- 关闭动画仍只在存在正数 Coin 时调用 CommonUIRewardAnimCtrl；Skin 与其它非 Coin 奖励不飞。
- 多 Skin 图标加载共用一次版本号，关闭、刷新或 item 销毁后旧异步结果不会回写到新弹窗。

### 验证、剩余风险与回滚

- 缓存依赖独立编译结果为 `0 error / 2010 warning`；定向 diff 空白检查通过。
- 静态核对当前五档 ChestGift 为一项 Coin 加两至三项 Skin，新入口能够保留全部 Skin 条目。
- Unity Hot Reload 无法完整应用本次新增重载和多 Skin 字段结构；需要退出 Play Mode 并完成 Unity 全量脚本重编译后验证。
- `uncertain`：尚未在 Unity PlayMode 验证四个奖励项的布局、皮肤图片加载顺序、预发奖 Coin 数字隐藏/恢复以及通用弹窗外部销毁；Android/iOS 横屏未验证。
- 回滚新 `List<ShopReward>` Create 重载及 `_skinRewards` 多项处理即可恢复单 Skin 行为；ChestGift 调用方需同时恢复原购买成功收口，避免调用不存在的重载。
