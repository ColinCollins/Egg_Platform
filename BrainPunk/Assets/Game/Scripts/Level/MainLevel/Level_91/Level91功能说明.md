# Level091 敲门失败与暂停页门铃功能说明

更新日期：2026-08-05

## 需求与背景

Level091 是 Level004 的进阶关卡，但不修改 Level004。原关卡要求玩家在限定时间内连续敲右侧门，达到次数后直接开门；Level091 将这条操作改成失败陷阱：

- 连续敲门后锁定玩家和 HUD，先开门，黑箱角色再从门内出现、生气并冲撞主角。
- 主角被撞后沿左上方向持续旋转飞出，撞击参数和特效参考 Level10046（工程文件为 Level046）。
- 黑箱命中主角后不再停在接触点，会保持冲刺动画和方向继续跑出相机视口。
- 正确解法是打开暂停页，点击第一个音效按钮。Level091 中该按钮只充当实体门铃，不再切换音效；点击后暂停页保持打开，并在本页播放由左向右传播的 Ding Dong 动画，再打开出口门。
- 出口门已经打开后仍允许再次点击该按钮播放 Ding Dong 反馈，但不会重复执行开门完成逻辑。
- 失败演出期间禁止再次敲门、打开暂停页或重复触发门铃。

Level091 玩法状态仍落在本关私有脚本、Prefab 和资源引用内；没有修改 `Level_004.prefab`、`KnockKnock.cs`、Level046、共享 `GameSettingPopup` 逻辑、共享 `ActorCtrl` 或共享 `GamePlayPanel`。两项明确的共享资源登记是 `VolumeSettingContent.prefab` 的音效图标 Sprite 替换，以及 `l91Doorbell` 在 SoundsGood Collection/生成表中的音频 tag，详见下文独立记录。

## 影响范围

### 本关实现

- `Assets/Game/Prefabs/Level/Level_091.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_091.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91ActorCtrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91GamePlayPanel.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_91/Level91BoxBadActor.prefab`
- 上述新增脚本、Prefab 及目录对应的 `.meta`
- `Assets/Game/ArtSrc/Sprites/L91/Sp_l91_dd_01.png`：Ding Dong 放射线
- `Assets/Game/ArtSrc/Sprites/L91/Sp_l91_dd_02.png`：Ding Dong 文字
- `Assets/Resources/I2Languages.asset`：补充成功结算文案 `S_congraktTips_Des_level_91`
- 本目录的功能说明、风险评估
- `DevelopMD/已有功能设计与使用记录.md` 中的 Level091 索引小节

### 同轮共享设置音效图标

- `Assets/Game/UI/SettingPopup/Sprites/UI_setting_icon_voise2_01.png` 及其 `.meta`
- `Assets/Game/UI/Global/Prefabs/VolumeSettingContent.prefab`：只替换一行音效图标 Sprite 引用
- 旧图 `Assets/Game/UI/SettingPopup/Sprites/UI_setting_icon_voise_01.png` 保留，供局部回滚

### 当前工作区已有配置

- `Assets/Game/Configs/tbleveldata.json` 已有 `10091 -> Level_091`
- `Assets/Game/Configs/tblevelsort.json` 已有排序项 `id=89, path=10091`

本次文档更新没有修改这两份配置，正式选关加载仍需 Unity 中验证。

### 仅参考或复用，未修改

- `Assets/Game/Prefabs/Level/Level_004.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_4/KnockKnock.cs`
- `Assets/Game/Prefabs/Level/Level_046.prefab`
- `Assets/Game/Scripts/Level/Actor/Level46EnemyCtrl.cs`
- 共享 `Actor.prefab`、`ActorCtrl`、`BaseLevelCtrl`
- 共享 `GameSettingPopup`、`GameSettingPopup2` 和 `GamePlayPanel` 的脚本、Prefab 结构与逻辑；`VolumeSettingContent.prefab` 除音效图标的一行 Sprite 引用外未修改
- `Knock_Explosion` 粒子 Prefab

## 原有行为基线

Level004 的 `KnockKnock` 会在 `TimeLimit` 内统计对 `Target` 的点击。`Level_004.prefab` 当前序列化为 `RequiredCount=3`、`TimeLimit=1.5`；每次有效敲门播放 `knockDoor` 和局部粒子，达到次数后直接启用出口 Collider、播放 `door_open` 和 `openDoor`。

共享暂停页的 `Sfx_toggle` 原本用于切换全局音效状态。Level091 不修改共享弹窗或 Toggle Prefab，而是在 `GamePlayPanel_091` 打开的当前弹窗实例上临时改绑该按钮。

Level046 的敌人撞击流程会等待 `0.1s`，在敌人位置播放 `Knock_Explosion` 和 `trap`，再以 `(-0.5, 0.5)`、力度 `50` 击飞主角，并以 `0.5s` 一圈持续旋转。Level091 复用这组局部参数和同一粒子资源，不修改 Level046。

成功结算配置原已引用 `S_congraktTips_Des_level_91`，但本地化资源中缺少同名 Term。修复前即使关卡成功链能够完成，胜利页也无法从 `I2Languages` 取得 Level091 的独立提示文案。该问题不属于公共胜利页逻辑，修复没有修改公共胜利页。

## Level091 状态与输入边界

`Level91Ctrl` 使用以下本关状态：

```text
Ready
├─ 暂停页门铃 -> DoorbellRinging -> DoorOpen
└─ 连续敲门   -> AngrySequence -> Charging -> Failed

DoorOpen
└─ 暂停页门铃反馈 -> DoorOpen
```

- 只有 `Ready` 能累计敲门或取得首次开门流程所有权；`Ready` 和 `DoorOpen` 都允许播放门铃反馈。
- 连续敲门窗口为 `3` 次、`1.5s`；超时或收到暂停事件时清空计数。
- 门铃、敌人演出、冲撞和失败期都会停止 Actor 输入，通过 `BaseLevelCtrl.SetCtrlable(false)`、`ActorCtrl.StopMoving()` 和 `GamePlayPanelSwitchBlockEvent` 锁定玩法输入。
- `Level91GamePlayPanel.SetSpecialInputEnabled(false)` 同时拦截暂停按钮和当前门铃；门铃 Toggle 保持 `interactable=true`，失败期通过禁用 Toggle 组件阻止输入，避免显示成 Disabled 置灰状态。
- 失败演出开始后出口 Trigger 保持关闭；只有门铃成功流程会启用该 Trigger。
- 关卡禁用、退出或重开时取消异步任务和 Tween，黑箱恢复预放姿态并隐藏，HUD 输入阻塞也会解除。

### 暂停状态空引用修复

早期实现从 `Update()` 调用 `PlayCtrl.CheckState()`。退出或初始化边界中可能出现 `PlayCtrl.Instance` 尚存在、内部状态机已经为空的窗口，从而产生空引用。

当前 `Level91Ctrl` 不再逐帧调用 `PlayCtrl.CheckState()`：

- `OnEnable` 初始化本关 `gameplayPaused=false`。
- 订阅 `GamePauseEvent` 和 `GameResumeEvent`，只更新本地暂停标记。
- `Update()` 只检查 `gameplayPaused`；暂停时清空敲门计数，不读取 `PlayCtrl` 内部状态机。
- 演出锁定态恢复游戏时，会立即及下一帧再次锁定 Actor，避免共享 Resume 流程短暂恢复移动。
- 清理时解除事件订阅并复位本地标记。

## 正确解法：暂停页实体门铃

### 绑定范围

1. `Level_091.prefab` 指定专用 HUD 名 `GamePlayPanel_091`。
2. 点击 HUD 暂停按钮后，专用面板最多等待三个 `LastPostLateUpdate` 帧查找已打开的 `GameSettingPopup`。
3. 只在该弹窗实例内查找名为 `Sfx_toggle` 的 Toggle，移除其当前运行时监听并改绑为 `OnDoorbellToggleChanged`。
4. Toggle 每次点击后立即 `SetIsOnWithoutNotify(false)`，因此本关不会切换或持久化音效状态。
5. 门铃流程本身不调用 `CloseUI(GameSettingPopup)`，暂停页在 Ding Dong 动画与开门时保持打开。仅当整个 `GamePlayPanel_091` 关闭时，清理逻辑才关闭仍存活的设置弹窗。

### 实体门铃动画

门铃动画使用非缩放时间 `SetUpdate(true)`，所以暂停页打开、游戏时间暂停时仍能播放。

- 按钮按压：`0.07s` 缩至 `0.86`，再以 `0.1s` 回弹至 `1.08`，最后 `0.08s` 回到原缩放。
- 放射线：从目标左侧 `58px`、透明度 `0`、缩放 `0.72` 开始；在 `0.04s` 时启动，向右移动、放大并渐显。
- Ding Dong 文字：从目标左侧 `96px`、透明度 `0`、缩放 `0.82` 开始；在 `0.16s` 时错峰启动，向右移动、放大并渐显。
- 保持约 `0.3s` 后，放射线与文字继续分别向右移动 `24px / 30px` 并渐隐，形成从左向右传播的视觉。
- 动态根挂在当前 `GameSettingPopup` 的 `RectTransform` 下，使用 `buttonRect.TransformPoint(buttonRect.rect.center)` 取得 `Sfx_toggle` 的实际视觉中心作为基础锚点，局部 Z 归零，根尺寸为 `430 x 180`。
- 用户此前确认的是 `dingDongOffsetFromSfx={110,-55}` 时的旧根位置，随后又要求整组继续向右移动。`GamePlayPanel_091.prefab` 最终使用 `dingDongOffsetFromSfx={130,-55}`；策划仍可直接在 Inspector 中调整 X/Y，不修改共享暂停页。
- 共享 `VolumeSettingContent.prefab` 的静态横向坐标为 `Sfx_toggle=-208`、`Music_toggle=0`。根位置确认后，图片大小和文字相对位置继续使用 091 私有参数调整。
- `dingDongVisualScale=1.5` 只乘到 Rays 与 Text 的 `sizeDelta`，不缩放 `430 x 180` 动态根，不改变根锚点或偏移。
- `dingDongTextLocalOffset={-55,0}` 只叠加到文字局部目标；基础 TextTarget X 为 `160`，当前有效 X 为 `105`。该字段可在 Inspector 继续微调，不影响 Rays。
- 放射线相对动态根的目标位置为 `(-55, 55)`；文字当前有效目标为 `(105, 55)`。两张 Image 均不拦截射线，结束时销毁，不改共享暂停页 Prefab。

两张图片均缺失时，流程等待 `0.1s` 后继续；只有首次取得开门所有权的门铃会调用完成开门，避免资源缺失造成软锁。动画异常时同样只对首次门铃尝试完成开门。

### 开门结果

- 门铃开始时锁定 Actor 和 HUD，防止重复输入。
- Ding Dong 动画完成后播放出口门 `door_open` 和 `openDoor`，启用成功 Trigger。
- Actor 控制和 HUD 阻塞恢复；暂停页仍由玩家自行关闭。
- 门进入 `DoorOpen` 后，`CanPlayDoorbellFeedback` 仍返回 true。后续点击会完整播放 Ding Dong，但 `TryBeginDoorbell()` 返回 false，并以 `shouldOpenDoor=false` 跳过 `CompleteDoorbell()`。
- 即使完成回调被重复抵达，`CompleteDoorbell()` 也只接受 `DoorbellRinging`，因此首次调用后不会再次改变关卡状态。
- 玩家在动画过程中主动关闭暂停页时，Tween 被销毁后代码会尝试完成已经按下的门铃；该边界仍需 PlayMode 验证。

当前实体门铃在每次允许播放完整 Ding Dong 反馈的点击开始时播放 `l91Doorbell`；首次点击和门已打开后的重复点击都会响，动画中连点、失败态或引用无效时不会播放。

### 共享设置音效图标替换

- 用户提供的源文件为 `\\192.168.1.5\DW-ArtSource\BrainPunk_001\UI\切图\设置 暂停\UI_setting_icon_voise2_01.png`。
- 项目资产为 `Assets/Game/UI/SettingPopup/Sprites/UI_setting_icon_voise2_01.png`，Meta GUID 为 `f240a8b03ae84ada8c57dc035e3084a7`，SHA256 为 `44D71B25D3779C54A1A9AA44A9C04A44B2ADCF7CEFAA7E5A780D993FACA0D512`。
- 新旧音效图均为 `106 x 104`；新 Meta 与旧图的 TextureImporter 语义一致。
- `VolumeSettingContent.prefab` 只把一行 Sprite GUID 从旧图 `7476e53ae4e2015459ee7ec10a992060` 替换为新图 `f240a8b03ae84ada8c57dc035e3084a7`，没有调整 RectTransform、Toggle、事件或其它 Image。
- `GameSettingPopup` 与 `GameSettingPopup2` 都继承该共享内容且没有对应 Sprite override，因此新图会覆盖游戏内暂停设置和游戏外设置入口。旧图及 Meta 保留，可把该一行引用切回旧 GUID 回滚。
- 本轮没有操作 Unity GUI 或执行 PlayMode；两个 Popup 的图标显示、Toggle 开关态和不同分辨率仍需视觉验证。

## 错误解法：连续敲门失败演出

失败时间线以 `Level_091.prefab` 当前序列化参数为准：

1. `Ready` 中在 `1.5s` 内敲门 `3` 次；每次播放 `knockDoor` 和敲门粒子。
2. 第三次命中后立刻关闭出口 Trigger、锁定 Actor 和 HUD，播放出口门 `door_open`。
3. 等待 `doorOpenLeadDelay=0.5s`。
4. 激活场景中预放的 `Level91_BoxBad`。
5. 黑箱使用 `box_bad` 皮肤和 `walk`，在 `enemyRevealDuration=0.45s` 内渐显并向玩家方向走出 `enemyWalkOutDistance=1.8`。
6. 走出完成后立即循环播放 `idle`，在 idle 状态中等待 `preAngryDelay=0.5s`。
7. 播放一次 `angry`，等待动画完整结束。
8. 切换循环 `walk01`，以 `Ease.InQuad` 冲向锁定后的玩家 X；速度参数为 `12`，持续时间限制在 `0.2s` 至 `2.5s`。
9. 到达玩家 X 后触发命中回调、停止玩家移动并缓存命中位置；黑箱自身不停止。
10. 黑箱保持 `walk01` 和原冲刺方向，以同一 `enemyChargeSpeed=12` 继续移动到 `Camera.main` 视口外；`runOffViewportPadding=0.15` 表示额外跑出 `15%` 视口宽度，相机不可用或换算结果无效时沿原方向追加 `16` 个世界单位。
    - 当前 `Level_091.prefab` 使用固定主相机，中心 `X=0`、正交尺寸 `5.9`；玩家命中 X 约为 `-7.49`。按 Prefab 标注的 `1920x1080 / 2340x1080` 横屏范围静态换算，黑箱到达视口外目标分别约需 `0.51s / 0.76s`，均短于命中后 `impactDelay + failureDelay = 1.1s` 的失败事件时限。
11. 命中回调等待 `impactDelay=0.1s` 后，在缓存的接触位置播放与 Level046 相同资源的 `Knock_Explosion` 和 `trap`。
12. 调用 `ActorCtrl.Knockback((-0.5, 0.5), 50)`，并以 `0.5s` 一圈持续旋转；这里不提前调用 `Die()`，也不在本关脚本中追加击飞后的停止。
13. 等待 `failureDelay=1s` 后派发 `OnTriggerTrapEvent` 作为失败兜底；此前 PlayMode 测试场景已完成击飞和失败链，但 `2026-07-29` 新增的黑箱持续冲出行为尚未做 PlayMode，最终出画构图仍待验证。

## 黑箱预放、影子与复位

- `Level91BoxBadActor.prefab` 是共享 Actor 的本关变体，初始皮肤为 `box_bad`，增加 `Level91ActorCtrl`，并停用其物理模拟和普通移动输入。
- 该 Prefab 保留共享 Actor 的 `Shadow` 节点为 active。
- `Level_091.prefab` 将黑箱作为场景实例预放，实例初始 inactive；最终初始位置和旋转以策划当前在 Inspector 中保存的结果为准，本关不在运行时 `Instantiate`。
- 策划在 Unity 调整并保存关卡后，当前 YAML 中 `Level91Ctrl.boxBadActor` 和 `impactVfx` 都会保存为 `{fileID: 0}`，Inspector 表现为 None；场景中的黑箱实例和 `Knock_Explosion_Impact` 粒子仍然存在。
- `Level91Ctrl.Awake -> ResolveReferences()` 不依赖这两个序列化引用：用 `GetComponentInChildren<Level91ActorCtrl>(true)` 找到 inactive 黑箱，再遍历包含 inactive 的子级 `ParticleSystem`，按精确对象名 `Knock_Explosion_Impact` 找到撞击粒子。
- `Awake` 缓存黑箱预放位置和旋转、将 Spine Alpha 置零并保持隐藏；开门等待结束后才激活。
- 初始化时再次禁用 Rigidbody2D 和所有子 Collider，演出位置完全由 DOTween 控制。
- 退出、重开、Replay 或对象禁用时取消走出、冲撞及持续冲出视口的 Sequence，恢复预放姿态、Alpha 和 inactive 状态。
- `boxBadActor`、玩家 Actor 或出口门 Spine 仍无法解析，黑箱初始化失败，或演出抛出异常时，控制器会记录错误、隐藏黑箱、恢复 `Ready`、Actor 与 HUD，不再走旧的“直接击飞”兜底。
- `impactVfx` 最终仍无法按名解析时只会缺少撞击粒子；在黑箱演出已经正常到达命中点的情况下，击飞与失败流程仍继续。

黑箱位置可由策划直接在 `Level_091.prefab` Inspector 中调整。影子与 Spine 渐显的同步观感、Z 排序和地面贴合仍需视觉验收。

## 数据、资源与系统流

| 类型 | Level091 行为 |
| --- | --- |
| UI | 仅在 `GamePlayPanel_091` 中临时改绑当前 `GameSettingPopup/Sfx_toggle`；运行时以按钮视觉中心为基础锚点并叠加本关 `dingDongOffsetFromSfx`，再分别应用图片视觉倍率和文字局部偏移，创建并销毁两张非交互 Image，不修改共享弹窗 |
| 事件 | 订阅 `GamePauseEvent`、`GameResumeEvent`；使用既有 `GamePlayPanelSwitchBlockEvent` 和 `OnTriggerTrapEvent` |
| 输入 | 世界敲门同时支持 Touch Began 和鼠标，过滤 UI；暂停页门铃由当前 Toggle 回调触发 |
| 动画 | 出口门 Spine：`door_open`；黑箱 Spine：`walk -> idle -> angry -> walk01`，命中后保持 `walk01` 继续冲出视口；UI 和位移使用 DOTween |
| 物理 | 黑箱关闭 Rigidbody2D 模拟和 Collider；主角击飞继续走既有 `ActorCtrl.Knockback` |
| 音频 | 复用 `knockDoor`、`openDoor`、`trap`，并通过 SoundsGood tag `l91Doorbell` 播放本关门铃 Ogg；统一遵守全局 SFX 开关 |
| 资源 | 黑箱场景预放；黑箱与撞击粒子在序列化字段为 None 时由 inactive 子级类型/精确名称解析；Ding Dong 使用 L91 两张序列化 Sprite；无运行时资源加载 |
| 本地化 | `tbleveldata.json` 继续引用 `S_congraktTips_Des_level_91`；`I2Languages.asset` 新增且仅新增该 Term，76 个逻辑语言槽位逐项复用 Level004 对应文案，简体中文为“你好，有人在家吗？”；公共胜利页不变 |
| 存档 | 不读写 DB、Save、PlayerPrefs，不新增存档字段或迁移 |
| 配置 | 工作区已有 `10091 / Level_091` 和排序 `id=89`；本实现未增加运行时配置读取 |
| 商业/平台 | 不新增 RemoteConfig、analytics、广告、IAP、网络或平台 SDK |

## 已完成验证

- 源码静态确认 `Level91Ctrl.Update()` 不再调用 `PlayCtrl.CheckState()`，暂停只由本地 `gameplayPaused` 和 `GamePauseEvent / GameResumeEvent` 驱动。
- `Level_091.prefab` 已绑定 `Level91Ctrl`、Actor、敲门目标、出口 Trigger/Spine、敲门粒子和两张 Ding Dong Sprite；当前 `boxBadActor` 与 `impactVfx` 字段均为 `{fileID: 0}`。
- 当前 Prefab 参数已回读：`3 / 1.5s / 0.5s / 1.8 / 0.45s / 0.5s / 12 / 0.2~2.5s / (-0.5,0.5) / 0.1s / 50 / 0.5s / 1s`。
- `Level_091.prefab` 中没有旧 `KnockKnock` 脚本 GUID；Level004 的直接开门逻辑没有继续挂在 091。
- 黑箱实例初始 inactive，源码在 `Awake` 缓存 Unity 最终解析出的预放姿态，并使用包含 inactive 的组件查询自动解析，且没有 `Instantiate`；文档不固定策划可继续调整的坐标。
- `Level91BoxBadActor.prefab` 的 `box_bad` 皮肤、禁用物理和 active Shadow override 已静态确认。
- 对 `Assets/Game/ArtSrc/Spines/sp_Box_001/Box.json` 的结构化解析确认 `idle / walk / angry / walk01` 四个动画和 `box_bad` 皮肤均存在；`Box_SkeletonData.asset` 的 `defaultMix=0.2`。
- 场景中名为 `Knock_Explosion_Impact` 的粒子实例使用与 `Level_046.prefab` 相同的 `Knock_Explosion` GUID；源码按该名字解析，击飞方向、力度、等待和旋转参数与 `Level46EnemyCtrl` 静态一致。
- 关键引用缺失、黑箱初始化失败和演出异常均已静态确认会调用 `CancelBrokenAngrySequence()`，不会直接击飞玩家。
- `CanPlayDoorbellFeedback`、`shouldOpenDoor` 与 `CompleteDoorbell` 状态检查已静态确认：门打开后可重复播放 Ding Dong，但仅首次门铃完成开门。
- Ding Dong 根节点已静态确认挂到当前 `GameSettingPopup`，以 `Sfx_toggle` 的 `RectTransform` 视觉中心为基础锚点；用户先确认旧值 `{110,-55}` 的位置，随后要求整体右移，`GamePlayPanel_091.prefab` 最终为 `dingDongOffsetFromSfx={130,-55}`。
- `GamePlayPanel_091.prefab` 已挂 `Level91GamePlayPanel`，`Level_091.prefab` 的 `gamePlayPanelName` 为 `GamePlayPanel_091`。
- YAML 本地引用扫描：`Level_091.prefab` 为 `137` 个定义、`0` 重复、`0` 缺失内部引用；`GamePlayPanel_091.prefab` 为 `34 / 0 / 0`；`Level91BoxBadActor.prefab` 为 `4 / 0 / 0`。
- `tbleveldata.json` 和 `tblevelsort.json` 已静态找到 `10091 / Level_091 / id=89`。
- 本地化静态脚本确认 `S_congraktTips_Des_level_91` 在 `I2Languages.asset` 中唯一，输出为 `termCount=1 level04Slots=76 level91Slots=76 zhCNExpected=True`；Level004 与 Level091 的 76 个逻辑语言槽位逐项比较为 `76/76` 一致、无差异。
- `git diff -- Assets/Resources/I2Languages.asset` 显示只新增该 Term 段，共 `83` 行；原始文本块因 YAML 空白格式不同不能直接判等，但解析后的逻辑条目完全一致。资产中的 Unity 空标量行保留既有尾随空格，本次未为格式原因改写资产。
- Unity 已成功导入新增本地化 Term。修复前的完整成功链已通过 PlayMode；修复后再次尝试进入结算时被项目既有 `Loading/SDK waiting` 阻挡，因此这次导入成功不等同于胜利页文案动态显示已验证。
- 最新一次使用 Unity 6000.0.59f2 自带 dotnet 与 csc、复用当前 Bee `Assembly-CSharp` 响应文件的独立 Roslyn 编译仍为 exit code `0`、`0 error`。
- 两份新增文档的尾随空白扫描通过；共享索引的限定路径 `git diff --check` 通过。全工作区 `git diff --check` 仍被本任务外 `Assets/Game/ArtSrc/Font/en SDF.asset` 的既有 trailing whitespace 拦截。
- `2026-07-29` 跟进修改后，独立 Roslyn 编译再次得到 exit code `0`；`Level_091.prefab / GamePlayPanel_091.prefab / Level91BoxBadActor.prefab` 的 YAML 定义数仍为 `137 / 34 / 4`，重复 fileID 与缺失本地引用均为 `0`。
- `dingDongOffsetFromSfx`、`dingDongVisualScale` 与 `dingDongTextLocalOffset` 三个 UI 序列化字段在脚本和 `GamePlayPanel_091.prefab` 中各唯一，当前值分别为 `{130,-55}`、`1.5`、`{-55,0}`；`runOffViewportPadding=0.15` 仍唯一。
- 静态顺序断言确认门铃先保留根锚点与偏移，再只对 Rays/Text `sizeDelta` 应用 `dingDongVisualScale`，最后把 `dingDongTextLocalOffset` 叠加到文字目标；黑箱走出完成后先调用 `PlayAnimation(idle, true, false)`，再进入既有 `preAngryDelay=0.5s` 和 `angry`。
- 固定主相机、玩家命中 X、两种 Prefab 标注横屏比例和失败时限的静态换算表明，黑箱预计在约 `0.51s / 0.76s` 内到达视口外，早于 `1.1s` 失败事件；该结果只验证参数关系，不替代动画连续性和完整出画的 PlayMode 观察。
- `2026-07-29` 最后一次刷新后检查 `Editor.log`，`error CS / NullReferenceException / MissingReferenceException / Level91 Exception` 计数均为 `0`。该检查不是 PlayMode，也不能证明 UI 尺寸、文字位置、idle 混合或黑箱出画表现正确。
- 共享图标替换后，`VolumeSettingContent.prefab / GamePlayPanel_091.prefab` 分别为 `42 / 34` 个 YAML 定义，重复 fileID 与缺失本地引用均为 `0`；新 GUID 在全部 `Assets` 中共出现 `2` 处，旧 GUID 只保留在旧图 `.meta` 的 `1` 处。独立 Roslyn 编译仍为 exit code `0`。
- Unity PlayMode 已完成本关核心回归：
  - 正常流程可从暂停页首次点击门铃、播放 Ding Dong、打开出口门并进入成功链。
  - 连续敲门可进入开门、黑箱走出、angry、冲撞、10046 参数击飞和失败链。
  - 重置后黑箱、门铃状态、输入锁定和关卡流程均能恢复，可重新执行关卡。
  - 门已经打开后再次点击仍会播放完整 Ding Dong 反馈，但不会重复执行开门或 `CompleteDoorbell()` 的首次生效逻辑。
  - 上述正常、失败、重置和重复门铃路径中未观察到 Level091 异常或空引用。
- PlayMode 同时证明当前序列化字段为 None 时，运行时能够解析 inactive 黑箱与 `Knock_Explosion_Impact`；字段为 None 的保存现象仍以当前 Prefab YAML 为静态依据。

## 未验证项

- Unity 正式 `Assembly-CSharp.dll` 产物时间与构建日志归档；本次 PlayMode 已能运行当前脚本，但未单独记录正式程序集文件时间。
- 暂停页音效按钮在其它 UI 皮肤或分辨率下是否始终保持正常视觉、不置灰；失败期禁用 Toggle 组件的跨皮肤表现。
- 用户确认的是旧根偏移 `{110,-55}`，随后要求整组右移到最终 `{130,-55}`；`dingDongVisualScale=1.5` 的最终视觉大小、有效 TextTarget X=`105` 的文字位置，以及其它宽高比下的传播效果仍需用户验收，TextTarget 可能继续微调。本轮没有操作 Unity GUI 或执行 PlayMode。
- `GameSettingPopup` 与 `GameSettingPopup2` 中新音效图标的实际显示、Toggle 开关态、布局和不同分辨率表现。
- 主动关闭暂停页、快速重复点击、动画中退出/重开、Sprite 缺失或弹窗查找延迟时不会卡在 `DoorbellRinging`。
- 跨关卡进入下一关后，全局音效状态和共享 Sfx Toggle 监听是否保持原行为。
- 策划再次移动并保存黑箱后，两个 Inspector 字段继续为 None 时自动解析仍稳定，且不会误选玩家 Actor、敲门粒子或其它同类组件。
- 注入黑箱/玩家/门 Spine 缺失、`SetSkin(box_bad)` 失败或演出异常时，玩家不会被直接击飞，Actor 与 HUD 能恢复操作；撞击粒子单独缺失时失败链仍能结束。
- 黑箱 Inspector 位置、Shadow、Spine Alpha、Z 排序、`walk -> idle` 的 `defaultMix=0.2` 混合、idle 停留 `0.5s` 后进入 angry 的节奏、命中后 `0.1s` 延迟、持续冲出视口及击飞构图。
- 暂停/恢复、Replay、退出、成功/失败同帧竞争、主动关闭弹窗和应用切后台；本次只完成正常、失败、重置和重复门铃核心路径。
- `10091` 正式选关加载和关卡排序；修复后胜利页实际显示 `S_congraktTips_Des_level_91` 仍需在 `Loading/SDK waiting` 阻塞解除后重新完成一次结算复验。
- Android/iOS 横屏触摸、多指、不同宽高比和代表性设备性能。

## 回滚

整体撤回 Level091 时，应从确认过的本轮前基线恢复 `Level_091.prefab` 与 `GamePlayPanel_091.prefab`，移除 `Level_91` 私有脚本、黑箱变体和本地文档，并删除共享记录中的 Level091 小节。若同时撤回正式入口，再由配置所有者确认后移除 `10091` 配置项。

只回滚门铃 UI 时，可恢复 091 使用普通 HUD，并移除 `Level91GamePlayPanel` 与 Ding Dong Sprite 绑定；不要修改共享 `GameSettingPopup`、`VolumeSettingContent` 或全局音效逻辑。

只回滚失败演出时，可恢复 091 对原 `KnockKnock` 的关卡内使用并移除预放黑箱和撞击粒子；不要修改或回滚 `Level_004.prefab`、`KnockKnock.cs`、Level046、共享 Actor 或其他关卡。

只回滚 `2026-07-29` 跟进时，可从 `GamePlayPanel_091.prefab` 和 `Level91GamePlayPanel` 移除 `dingDongOffsetFromSfx`，并从 `Level91BoxBadActor.prefab` 和 `Level91ActorCtrl` 移除 `runOffViewportPadding` 与命中后的持续冲出步骤；前者会恢复用户已指出的偏高位置，后者会恢复黑箱停在接触点的旧问题。

只回滚最新的门铃参数微调时，把 `dingDongOffsetFromSfx` 从最终 `{130,-55}` 恢复为此前确认过的 `{110,-55}`，把 `dingDongVisualScale` 从 `1.5` 恢复为 `1.2`；保留 `dingDongTextLocalOffset={-55,0}` 和 idle 流程。

只回滚整个尺寸、文字和 idle 跟进时，应保留当前最终根偏移 `dingDongOffsetFromSfx={130,-55}`，仅移除 `dingDongVisualScale`、`dingDongTextLocalOffset` 及其 Prefab 值，并恢复原始 Rays/Text `sizeDelta` 和 TextTarget X=`160`；黑箱只移除走出完成后的 `idle` 调用，恢复直接等待 `preAngryDelay`，不要回滚持续冲出视口功能。

只回滚共享音效图标时，将 `VolumeSettingContent.prefab` 唯一一行 Sprite GUID 从新图 `f240a8b03ae84ada8c57dc035e3084a7` 切回旧图 `7476e53ae4e2015459ee7ec10a992060`；旧 PNG 与 Meta 已保留，不需要修改两个 Popup 或 Toggle 逻辑。

只回滚成功结算本地化补项时，仅从 `Assets/Resources/I2Languages.asset` 删除 `S_congraktTips_Des_level_91` 这一条 Term；不要修改公共胜利页，也不要改动 `tbleveldata.json` 中既有的 Level091 引用。

## 2026-08-05：实体门铃音频与失败重开排查

### 门铃音频资源与播放时机

- 用户源文件 `C:/Users/Crow/Desktop/音乐音效/menling.ogg` 原样复制为 `Assets/Game/ArtSrc/Audio/Sfx/l91/l91Doorbell.ogg`，没有转码。两者 SHA256 均为 `A4291BCB13CB36A23A98EB90CCA42861BC90CC407FA586B714D9880338833A80`。
- 音频为 Ogg/Vorbis、44.1kHz、立体声、`1.085488s`、42657 bytes；AudioImporter 与 089 的短音效语义一致：Decompress On Load、保持采样率、Vorbis、quality 1、预加载、后台加载、不强制单声道。
- 新 AudioClip Meta GUID 为 `d2f9955a06194b899bca8cb1e608c456`。`SoundCollection.asset` 以唯一 tag `l91Doorbell` 指向该 GUID，`SFX_Generated.cs` 同步生成同名项。
- `Level91GamePlayPanel.OnDoorbellToggleChanged(...)` 只有在 `CanPlayDoorbellFeedback` 通过且当前没有门铃动画时才播放声音，然后沿用既有 `TryBeginDoorbell()` 与 Ding Dong Sequence。门已打开后的重复反馈会再次响，但不会再次开门；失败演出和动画中连点不响。
- 声音使用现有 `AudioManager.PlaySound`，因此尊重进入本关前保存的全局 SFX 状态。091 内的按钮不再负责开关音效，但玩家此前已经静音时门铃也会静音；该产品边界尚未运行态验收，没有绕过共享音效设置。
- 本次音频不增加 Prefab `AudioClip` 字段，不修改共享 Toggle；`Sfx_toggle` 本体没有 `ClickAudio`，不会叠加通用按钮音。

### 被击飞后无法结束的排查结论

- 091 冲撞链仍在 `failureDelay` 后派发一次 `OnTriggerTrapEvent`，`BaseLevelCtrl.TryToRestart()` 也会继续派发 `GameResetEvent`。用户日志中的阻断来自已销毁 Level083 的陈旧监听，不是 091 缺少失败派发。
- 修复限定在 `Level83GameplayCtrl`：生命周期先解绑事件，再访问 Unity 对象；谱面格改用 Unity 假空安全判断。共享 `EventDispatcher` 与 091 失败时序均未修改，详见 Level083 本地实现/风险记录的 `2026-08-05` 节。

### 验证、缺口与回滚

- 音频源/目标哈希一致；ffprobe 参数、Meta 语义、GUID 唯一性、Collection/Generated tag 唯一性与播放调用顺序均通过静态断言。
- `SoundsGood.Application` 与 `Assembly-CSharp` 使用 Unity 生成响应文件独立 Roslyn 编译，均为 `0 error`；后者仅有工程既有 warning。
- 未运行 Unity PlayMode 听感、最快重复点击尾音、此前静音后进入 091、083→091 失败重开或 Android/iOS 真机验证。
- 只回滚门铃音频时，删除 `DoorbellSfxTag` 和一次播放调用，移除 `l91Doorbell` 的 Collection/Generated 项及 `Audio/Sfx/l91` 资源与 Meta；保留门铃视觉、UI 图标和其它 091 逻辑。
- 只回滚重开修复时，仅恢复 `Level83GameplayCtrl` 的清理顺序与谱面格空判断；不要修改 091、BaseLevelCtrl 或共享 EventDispatcher。
