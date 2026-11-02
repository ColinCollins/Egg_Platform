# Level089 功能说明

更新时间：2026-07-17

## 需求与背景

Level089 的四个场景按钮需要按 `3-2-4-1` 的顺序踩下，使石柱 `stone` 逐段缓慢下落。正确输入期间石柱子物体抖动并震屏，错误输入需要回弹按钮并复位石柱；第一次就按错时只播放石柱快速抖动。所有机关演出期间都禁止玩家移动。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_089.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_89/Level89Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_89/Level89SequenceButton.cs`
- 复用已有 `ActorCtrl.StopMoving()`、`ActorCtrl.ResumeMoving()`、`PressButtonBase` 和 DOTween。

没有修改共享角色控制、`BaseLevelCtrl`、存档、配置、资源地址、全局事件、分析、广告、IAP、RemoteConfig 或平台 SDK。

## 原有行为基线

`Level_089.prefab` 已有四个从左至右排布的按钮、各自的 `Trigger`、按钮视觉与 `BoxCollider2D`，原先均使用 `Level47YellowButton`，只能按进入/离开触发器进行普通下压和回弹。根节点已有 `BaseLevelCtrl` 与角色实例，场景根节点已有 `stone` 及 8 个子物体。

## 实现后行为

- 四个按钮从左至右编号为 `1`、`2`、`3`、`4`，只有依次踩 `3`、`2`、`4`、`1` 才是正确解法。
- 正确按下的按钮锁定在下压状态；石柱以 `1.2` 秒向下移动总距离 `8.0` 的四分之一，同时 `stone` 的所有子物体做本地位置抖动，镜头做位置抖动，角色调用 `StopMoving()`；动画完成才调用 `ResumeMoving()`。
- 错按时，当前按钮先按 `0.18` 秒完成下压，再以 `0.18` 秒回弹。已经有正确按钮锁定时，石柱在回弹开始后以 `0.35` 秒返回初始位置，石柱子物体同步抖动，过程锁定角色。
- 第一次按键就按错：先让已按下的按钮完成 `0.18` 秒下压和 `0.18` 秒回弹，石柱不移动；回弹结束后才锁定角色并让石柱子物体快速抖动 `0.32` 秒。
- 每个按钮进入触发器后设置“必须先离开”标记；在回弹后，玩家仍停在该 Trigger 内不会再次触发，只有先离开再进入才可按下。
- 关卡禁用时终止本关创建的石柱与镜头 Tween，并在需要时恢复角色移动，避免关卡卸载后遗留禁动状态。

## 数据与流程

玩家进入 Trigger -> `Level89SequenceButton` 下压并通知 `Level89Ctrl` -> 比较当前编号与 `3-2-4-1` -> 正确：锁定按钮并下移石柱 -> 错误：回弹全部按钮并按已完成步骤决定石柱复位，首步错误在回弹结束后才仅抖动石柱 -> 演出完成 -> 恢复玩家移动。

本关不读写存档，不加载新资源，也不发送全局事件。

## 验证证据

- 静态检查：根节点新增 `Level89Ctrl` 的 GUID 引用 1 个；4 个 Trigger 全部改为 `Level89SequenceButton`，旧 `Level47YellowButton` GUID 引用为 0。
- 静态检查：四个按钮保留既有 `button`、`collider`、`offsetY: 0.2` 绑定，`tweenDuration` 调整为 `0.18`；编号从左至右为 `1-4`。
- 静态检查：`stone` 是关卡根节点直接子物体，包含 8 个子物体；控制器仅在运行时 `Awake` 查找其引用，无 `OnValidate`、`Reset`、编辑器回调、DB 或保存访问。
- `git diff --check` 通过。
- 当前机器没有 .NET SDK，且未发现可调用的 Unity 编辑器命令行；无法在本轮执行 Unity 编译或 PlayMode。

## 未验证项

- Unity 重新导入后确认 `Level89Ctrl` 与四个 `Level89SequenceButton` 没有 Missing Script。
- PlayMode 验证正确顺序、各步骤下落距离、按钮保持下压、第二步及之后输入错误的快速复位、第一步错误的纯抖动、离开再进入触发器的再触发限制，以及重开/退出后的角色可移动性。
- Android/iOS 横屏验证演出期间触控禁动、镜头震动强度、石柱通过后的路径与 60 fps 表现。

## 2026-07-17 编译修复

- `Level89Ctrl.JoinStoneShake(...)` 原先调用不存在于当前 DOTween 版本的 `Transform.DOShakeLocalPosition(...)`，导致 `CS1061`。
- 已改为项目当前可用的 `Transform.DOShakePosition(...)`，持续时间、强度、振动次数、随机度、淡出和谐波参数保持不变；石柱抖动仍加入原有 Sequence，未改动关卡流程或共享代码。
- 仍需 Unity 重新导入后确认该编译错误消失，并在 PlayMode 验收石柱抖动方向和强度。

## 回滚路径

从 `Level_089.prefab` 根节点移除 `Level89Ctrl`，将四个 Trigger 的脚本恢复为 `Level47YellowButton`，并删除 `Level_89` 目录中的本关脚本与文档，即可回到原有独立按钮下压/回弹表现。

## 2026-07-22 石柱与错误音效接入

### 需求与影响范围

原有顺序机关的石柱下降和错误分支均没有专用声音。本次为每次真实石柱下降新增 `l89StonePillarDrop`，错误输入复用 Level068 密码错误链已使用的 `passwordError`。影响 `Level89Ctrl.cs`、`Assets/Game/ArtSrc/Audio/Sfx/l89`、SoundsGood Collection 与生成表；未修改共享密码面板、`AudioManager`、关卡 prefab、存档、配置或事件。

### 新行为与资源流

- 用户提供的 `石头机关.ogg` 原样复制为 `Assets/Game/ArtSrc/Audio/Sfx/l89/l89StonePillarDrop.ogg`，保留 44.1 kHz Ogg/Vorbis 立体声，不再次转码。
- 每次正确踩中 `3-2-4-1` 的下一项后，`PlayCorrectStep()` 在石柱下降 Tween 创建前播放一次 `l89StonePillarDrop`，四个正确步骤各播一次；错误复位向上移动不播放该声音。
- 所有错误输入在统一错误分支入口播放一次既有 `passwordError`，随后才进入首次错误抖动或已有进度复位，避免两个失败方法分别播放造成双声。
- `isBusy` 和按钮离开后重新武装的既有逻辑继续限制动画期间重复触发。

### 验证、缺口与回滚

- `passwordError` 已核对来源于共享 `PasswordPopup.CheckPassword()`，Level068 的密码面板沿用该分支；本次没有复制或修改其音频资源。
- 新 tag 的 Collection GUID、生成项、脚本调用和目标文件哈希已静态核对；`Assembly-CSharp` 与 `SoundsGood.Application` 独立编译均为 0 个错误。
- 当前 Unity 编辑器已成功导入 `l89StonePillarDrop.ogg` 和更新后的 Sound Collection，并于 15:22 重建两个目标程序集；最近刷新日志相关错误为 0。
- PlayMode 仍需确认四次下降的音画同步、错误输入一次提示、1.715 秒机关声在相邻 1.2 秒步骤间是否需要调节，以及 Android/iOS 真机音量。
- 回滚时移除 `Level89Ctrl` 的两个播放调用，删除 `l89StonePillarDrop` 的登记和 L89 音频资源；保留共享 `passwordError` 原资源与其他关卡调用。
