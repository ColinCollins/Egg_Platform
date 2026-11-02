# DrawGame_GameVictoryPanel 分享与 Spine 调整记录

## 需求背景

2026-08-05，普通 `DrawGame_GameVictoryPanel` prefab 完成新版界面调整并加入评价/分享区域，需要补齐该区域的运行时控制。后续 Spine 资源改为带界面前缀的命名：普通界面主 Spine 使用 `2_appear01 -> 2_idle`，`_Reward` 界面主 Spine 使用 `1_appear01 -> 1_idle`。实现节奏参考主线 `GameVictoryPanel_Reward`，但 Draw 分享数据必须与主线关卡隔离。

## 影响范围

- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`
- `Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_Share.cs`
- `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel.prefab`
- `Assets/Game/DB/DrawGame.cs`
- `Assets/Game/DB/DrawGame_Partial.cs`
- `DevelopMD/已有功能设计与使用记录.md`

未修改主线胜利面板 prefab、Draw 奖励版 prefab、Luban 配置、RemoteConfig、全局事件、广告、IAP 或资源加载路径。

## 原有行为基线

- 普通 Draw 胜利面板只控制下一关、重玩、星级和打开特效，Spine 固定播放 `in` 后进入 `idle`。
- 用户调整后的普通 Draw prefab 已包含 `GameVictoryPanel_Share`、like/dislike/share 按钮和分享奖励文本，但根面板没有 share 序列化引用，也没有刷新、延迟显示或前后台恢复调用。
- `GameVictoryPanel_Share.RefreshData()` 只读取主线 `PlayCtrl` 与 `DB.GameData.HasSharedLevel`；直接用于 Draw 会把关卡路径和一次性奖励状态记到主线。
- Draw 奖励版曾通过 `SupportsSharePart=false` 主动关闭 share 区域；prefab 实际已包含完整 `GameVictoryPanel_Share` 和金币飞行动画引用，因此缺失表现来自脚本开关，不是资源缺失。

## 新行为

- 普通 Draw 胜利面板通过 prefab 显式绑定 `sharePart`。打开时保持按钮隐藏，2 秒后调用 `Show()`；被其它 UI 覆盖后重新显示时会恢复 share 按钮组。
- `Configure(DrawLevelData)` 通过 `RefreshDrawData(...)` 注入 Draw 的 `Id` 和 `Path`。like/dislike 打点继续使用既有 `LevelVictoryReaction`，其 `level_name` 使用 Draw 配置 `Path`。
- 分享按钮继续复用现有 Android/iOS 商店链接分享与 RemoteConfig 金币数。一次性奖励使用 `DrawLevelData.Id` 写入 `DB.DrawGame.SharedLevelIds`，不读写主线 `GameData.HasSharedLevel`。
- `GameVictoryPanel_Share.Show()` 统一按当前关卡的持久化状态筛选按钮：主线使用既有 `GameData.HasReactionLevel`，Draw 使用独立 `DrawGame.HasReactionLevel`；每关首次点赞/点踩后记录 `1/0` 并保存，再次进入同一关不显示 like/dislike，进入尚未评价的其它关卡仍显示。已领取当前关卡分享奖励时不再显示 shareBtn。两部分都完成后直接停用 shareGroup。share 点击会先锁定交互，再按既有收回 tween 隐藏按钮，最后一个可见按钮收回后同样停用 shareGroup，避免系统分享返回后重新出现。
- 普通新版 Spine 播放 `appear01`，完成回调继续切换到循环 `idle01`。Draw `_Reward` 版独立覆写为 `appear -> idle`。
- Draw 奖励版移除 `SupportsSharePart=false` 覆写，继承基类默认开启状态。打开时同样注入当前 `DrawLevelData.Id/Path`，2 秒后展示 share 区域；广告奖励选择和按钮组仍走原有独立流程。
- Draw 奖励版 `BtnGroup` 不再从面板打开时独立延迟 2 秒播放，改为在 Spine 入场完成回调中立即执行 `0.35s` 的位移加渐显；基类先启动下一关按钮渐显和派生表现，再安排等待星星序列。星星仍延迟 `1.7s` 后开始，因此不会遮掉或等待 `BtnGroup` 渐显完成。

## 数据、资源、UI 与事件流

1. `DrawPlayCtrl` 创建面板并调用 `Configure(DrawLevelData)`。
2. 面板把 `Id/Path` 注入 `GameVictoryPanel_Share`，同时按既有流程消费 Draw 等待播放的星数。
3. 打开 2 秒后 share 区域按当前关卡状态显示；首次 like/dislike 选择先把 `1/0` 写入对应玩法存档并保存，再发送既有胜利评价打点。主线以 `levelPath` 为键写入 `DB.GameData.HasReactionLevel`，Draw 以 `DrawLevelData.Id` 为键写入 `DB.DrawGame.HasReactionLevel`。
4. share 点击调用现有平台分享；首次点击增加 RemoteConfig 指定金币，并把当前 `DrawLevelData.Id` 写入 `DB.DrawGame.SharedLevelIds` 后调用 `DB.SaveDrawGame()`。
5. 旧 Draw 档缺少 `sharedLevelIds` 或 `hasReactionLevel` 时由 `EnsureValidCollections()` 补为空集合；主线旧档缺少 `hasReactionLevel` 时由评价组件在首次写入前补为空字典。

普通 Draw prefab 中 `GameVictoryPanel_Share.rewardAnimCtrl` 当前静态绑定为 `null`，因此金币会入账，但不会播放该组件的金币飞行动画；此项未擅自绑定新的通用奖励动画资源。

## 验证证据

- CodeGraph 核对了 Draw 胜利面板、主线 `_Reward` share 控制、`GameVictoryPanel_Share`、`DrawLevelData`、`DB.DrawGame` 和 `LevelVictoryReaction` 调用边界。
- 静态解析新 `Box.json`，确认动画列表包含 `appear`、`appear01`、`idle`、`idle01`。
- 静态解析两个 Draw 胜利 prefab 的 `spineCtrl -> skeletonGraphic -> skeletonDataAsset` 引用，普通版与 `_Reward` 版均指向 `ui_drawGameVictoryPanel_001/Box_SkeletonData.asset`（GUID `73d37372f8ba4ad49ab38af65ceba823`），与包含四个目标动画的 `Box.json` 一致。
- 静态核对普通 Draw prefab：根面板 `sharePart` 指向用户新增的 `GameVictoryPanel_Share` 组件，组件的 like/dislike/share 与文本引用均非空。
- 静态核对 Draw 奖励 prefab：根面板 `sharePart`、组件的 like/dislike/share、分享文本与 `rewardAnimCtrl` 引用均非空；本次无需修改 prefab。
- 奖励版启用 share 后使用独立中间目录进行 `Assembly-CSharp` 定向构建，结果为 `0 error / 101 warning`；目标脚本和中文文档的 `git diff --check` 通过。
- `BtnGroup` 与星星节奏调整后再次使用独立中间目录定向构建，结果为 `0 error / 101 warning`；静态调用顺序确认派生入场完成钩子先于 `RefreshVictoryStarGroup()`，奖励版不再保留 `Invoke(nameof(ShowBtnGroup), 2f)`。
- share 初次调整后的 `Assembly-CSharp.csproj` 定向构建通过：`0 error / 102 warning`。
- 2026-08-05 最终按界面映射调整为普通版 `appear01 -> idle01`、奖励版 `appear -> idle`；使用独立中间目录并复用 Unity 已生成依赖进行 `Assembly-CSharp` 定向构建，结果为 `0 error / 101 warning`。完整依赖构建重试曾因其它进程占用 `Temp/obj/Unity.Services.Core.Registration.dll` 失败，该失败属于文件锁，不是脚本编译错误。
- 每关一次评价补充修改使用同一独立中间目录进行 `Assembly-CSharp` 定向构建，结果为 `0 error / 101 warning`；相关脚本和中文文档的 `git diff --check` 通过。
- 脚本与文档执行 `git diff --check`；普通 Draw prefab 的用户已有大范围 YAML 改动包含多处行尾空格，因此 prefab 整体检查仍会报告这些既有格式项，本次未批量改写美术 YAML。

## 剩余风险与未验证缺口

- `uncertain`：尚未在 Unity PlayMode 验证普通版 `appear01 -> idle01`、奖励版 `appear -> idle`、share 延迟进场、覆盖/恢复、like/dislike 单次交互和下一关/重玩竞态。
- `uncertain`：尚未在 Unity PlayMode 验证奖励版 share 区域与奖励选择/广告按钮组同时显示时的视觉间距、触控区域和进场动画叠加效果。
- `uncertain`：尚未在 Unity PlayMode 验证 `appear -> BtnGroup 0.35s 渐显 -> 星星 1.7s 延迟播放` 的实际视觉节奏，以及快速 Hide/Show 时按钮组重新进场是否符合预期。
- `uncertain`：尚未在 Unity PlayMode 验证“仅分享、仅评价、两者均完成”三种 shareGroup 恢复状态，以及系统分享返回时 shareBtn 不重现。
- `uncertain`：尚未在 Unity PlayMode 验证主线和 Draw 各自“同关不再显示、换关仍显示”以及重启应用后的每关评价状态。
- `uncertain`：静态资源和 prefab 引用已包含目标动画，但尚未在 Unity 导入完成后的实际 AnimationState 中验证两套动画均能播放并正确收到完成回调。
- `uncertain`：尚未验证首次分享得币、同一 Draw 关卡重复分享不重复得币、旧档集合补全和保存失败场景。
- `uncertain`：Android/iOS 横屏下的系统分享面板、切回 Unity 后输入恢复和真机打点尚未验证。
- 普通 Draw prefab 未绑定 `rewardAnimCtrl`，分享得币没有飞行动画；若产品要求该表现，需要由 UI 资源明确提供并绑定起点/终点组件后补验。

## 回滚路径

- 从普通 Draw prefab 根组件移除 `sharePart` 绑定，并删除 `DrawGame_GameVictoryPanel` 的 share 生命周期控制，可恢复为无 share 控制的旧行为。
- 删除 `RefreshDrawData`、`DB.DrawGame.SharedLevelIds` 及其初始化/集合修复，可撤销 Draw 独立分享奖励记录；回滚前应评估已经写入的新存档字段。
- 删除 Draw `HasReactionLevel` 并恢复评价组件的面板内存状态，可撤销 Draw 每关一次评价；主线既有 `GameData.HasReactionLevel` 字段无需删除。回滚后评价按钮会再次在每个新面板实例中出现。
- 将普通面板的入场/待机动画名恢复为旧值，并移除奖励版 `appear/idle` 覆写，可恢复旧 Spine 播放链。
- 在 `DrawGame_GameVictoryPanel_Reward` 恢复 `SupportsSharePart=false` 覆写，可单独关闭奖励版 share 区域而不影响普通版。
- 将 `ShowBtnGroup()` 恢复为 `OnOpenExtra()` 中的 2 秒延迟调用，并恢复基类派生钩子与星星刷新顺序，可回滚本次节奏调整。

## 2026-08-05：Spine `1_` / `2_` 动画名同步

### 需求与基线

- 最新 `Box.json` 动画名已改为 `1_appear`、`1_appear01`、`1_idle`、`2_appear`、`2_appear01`、`2_idle`。
- 修改前运行时仍播放普通版 `appear01 -> idle01`、奖励版 `appear -> idle`，与新资源名不一致。
- 当前两个 prefab 均包含背景 Spine 和主 Spine；背景 Spine 由 prefab 分别以 `2_appear` / `1_appear` 起播，根面板的 `spineCtrl` 继续控制主 Spine 的入场与待机切换。

### 影响文件与新行为

- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`：普通版主 Spine 改为 `2_appear01 -> 2_idle`。
- `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`：奖励版覆写改为 `1_appear01 -> 1_idle`。
- `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel.prefab`与 `DrawGame_GameVictoryPanel_Reward.prefab`：核对当前用户资源改动中的 `2_appear/2_idle` 与 `1_appear/1_idle` 前缀映射；本次不覆盖其它布局、层级和美术改动。
- 动画完成回调、按钮组进场、Draw 星星刷新、分享、存档、奖励、事件、analytics 和平台 SDK 流程未改变。

### 验证、风险与回滚

- 已从 `Box.json` 静态解析并确认六个目标动画名存在；已核对两个 prefab 的背景/主 Spine 起播名和根面板 `spineCtrl` 绑定。
- `git diff --check` 已通过本次脚本与中文记录检查。`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 在 64 秒限制内未返回错误输出，但也未完成，因此本次编译结果标记为 `uncertain`，需由 Unity 完成重新导入/编译后复核 Console。
- `uncertain`：仍需 Unity PlayMode 确认背景 `appear` 与主 Spine `appear01 -> idle` 同时播放时的实际画面、完成回调和按钮/星星节奏，并补充 Android/iOS 横屏验证。
- 回滚时恢复两个脚本的旧动画名即可；不涉及 DB schema、配置表、资源地址或存档迁移。

## 2026-08-06：Draw 胜利面板接入免广告购买

### 需求、原行为与影响范围

- 需求：`DrawGame_GameVictoryPanel` 的 `noAd_btn` 应与主线胜利面板一样可发起免广告购买，
  且 `DrawGame_GameVictoryPanel_Reward` 复用同一套基类逻辑。
- 原行为：普通 Draw prefab 已包含 `UICommon_btn` 免广告按钮实例，但根脚本没有序列化字段和
  点击回调；Reward 脚本虽然持有按钮引用，却在 `OnOpenExtra()` 中主动隐藏，因此两个 Draw
  胜利面板都不能从该入口购买免广告。
- 影响代码与资源：
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel.cs`
  - `Assets/Game/UI/GameVictory/Scripts/DrawGame_GameVictoryPanel_Reward.cs`
  - `Assets/Game/UI/DrawGame/VictoryPanel/Resources/DrawGame_GameVictoryPanel.prefab`
- 不修改主线 `GameVictoryPanel`、Reward prefab 布局、Draw 插屏后自动免广告弹窗参数、
  RemoteConfig、存档结构、商品配置或全局事件。

### 修改后行为与数据、UI、平台流

1. Draw 基类统一绑定 `NoAdBtn.OnClick`，普通版和 Reward 版都使用
   `ProductName.NoAds` 查询商品并调用既有 `PurchaseManager.Purchase`。
2. 发起购买前发送既有 `IAP_CLK`，并在购买等待期间禁用免广告、下一关和重玩按钮；Reward
   版的奖励广告忙状态仍通过 `HasExtraBusyState()` 与购买入口互斥。
3. 回调成功时走 `PurchaseManager.OnPurchaseSuccess`，失败时走
   `OnPurchaseFailed`；随后重新检查 SDK 已拥有状态与 Bag 中 `GameProps.NoAds` 权益。
4. 已拥有免广告权益时隐藏按钮；未拥有时显示。Reward 子类不再声明或隐藏独立字段，继续
   使用原 Reward prefab 已有的 `NoAdBtn` 绑定。
5. 普通 prefab 根组件新增 `NoAdBtn` 绑定，指向其 `UICommon_btn` 实例上的既有
   `CustomButton`；未新增资源地址、实例化或每帧轮询。

### 验证、剩余风险与回滚

- CodeGraph 核对了主线 `PurchaseNoAd -> PurchaseManager.Purchase ->
  OnPurchaseNoAdCallback -> RefreshButtonStates` 流程及 Draw 基类/Reward 继承边界。
- 静态 YAML 核对普通与 Reward prefab 的 `NoAdBtn` 均非空，且都对应
  `UICommon_btn.prefab` 中的 `CustomButton` 组件。
- 本次脚本、prefab 与中文文档执行 `git diff --check`；定向执行
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 通过，结果为
  `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 真机验证购买成功、失败、取消、切后台、
  WaitingPopup 覆盖期间输入锁、回调后按钮隐藏，以及 Reward 奖励广告与免广告购买的互斥表现。
- 回滚时删除 Draw 基类的免广告字段、购买状态和回调，移除普通 prefab 的 `NoAdBtn` 绑定，
  并在 Reward 子类恢复打开时隐藏按钮的旧逻辑；不涉及存档或配置迁移。

## 2026-09-24：OneLink 进入后的重玩限制

- 三种玩法的 OneLink 本轮来源由 OneLinkManager / GameData 管理，历史按玩法一次持久化。主线和 Draw 的普通/Reward 面板在 OnOpen 缓存来源，RefreshButtonStates 隐藏 Inspector 已绑定的 Replay 按钮，Replay 处理同时拦截；普通进关时恢复，同关 Reset 保留限制。未改 Prefab、Spine、广告/购买/奖励逻辑及统计事件。
- 本次主线修改 `GameVictoryPanel.cs`，Draw 修改 `DrawGame_GameVictoryPanel.cs`；Reward 共用基类。Editor 中直接提取三种面板实际按钮方法的模拟检查 18 项通过，并检查六个 Prefab 引用与 Reward 刷新继承；未在 Unity 场景中验证动画和原生广告后的可见性，标记为 `uncertain`。
- 需求、完整数据流、存档兼容、加载边界、验证文件及回滚见 [OneLink 功能与测试记录](../../Scripts/SDK/Services/Core/OneLink功能与测试记录.md) 的 2026-09-24 每玩法一次章节。回滚本次按钮来源读取、隐藏和点击判断即可恢复原重玩界面，不删除玩法历史。
