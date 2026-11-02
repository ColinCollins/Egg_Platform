# TurntableItemChoiceCtrl 功能说明

## 需求与背景

- 2026-07-08 用户要求完善 `TurntableItemChoiceCtrl` 脚本功能。
- 脚本注释要求从两个角度范围 `[minA,maxA]`、`[minB,maxB]` 中随机选择一组停止范围，旋转期间动画不要改变。
- 该脚本位于 `Assets/Game/Scripts/Level/LevelItem/Level_33/`，属于 Level_33 转盘奖励逻辑的关卡专用变体。

## 受影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/LevelItem/Level_33/TurntableItemChoiceCtrl.cs`。
- 新增文档：本文件与 `TurntableItemChoiceCtrl风险评估.md`。
- 关联现有系统：`TurntableButton.SetEnter(Play)`、DOTween `Transform.DORotate`、`AudioManager.PlaySound(...)`、`ThrowItemHandle.SpawnItem()`、转盘区域进入/退出回调。
- 未修改 prefab、DB/save、配置表、资源加载路径、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

## 原始行为基线

- `Start()` 直接把 `button.SetEnter(Play)` 绑定到按钮进入回调。
- `Play()` 触发后播放 `turnntableStart` 循环音效，并执行 `turntable.DORotate(new Vector3(0, 0, 360 * 20f), 3f, RotateMode.FastBeyond360).SetEase(Ease.InOutCirc)`。
- 旋转完成后等待 0.1 秒，播放 `turntableGetReward`，再按 `isKey`、`isOther` 和默认分支决定显示 key、显示 `otherStuff` 或调用 `throwHandle.SpawnItem()`。
- 静态 prefab 证据显示 `Assets/Game/Prefabs/Level/Level_033.prefab` 当前仍引用 `TurntableItemCtrl`，未发现 `TurntableItemChoiceCtrl` 或其 guid 引用；因此本次只完善脚本功能，未让现有 prefab 自动切换到新脚本。

## 当前行为

- 新增 `stopAngleRangeA` 与 `stopAngleRangeB` 两个序列化角度范围，单位为转盘 Z 轴角度。
- 每次 `Play()` 开始时，脚本会等概率选择 A/B 其中一组范围，并在该范围内随机一个停止角度。
- 停止角度会追加到原有 `360f * 20f` 后，旋转时长仍为 3 秒，`RotateMode.FastBeyond360` 和 `Ease.InOutCirc` 保持不变。
- 范围端点会自动按 min/max 归一；负角度或大于 360 的角度会归一到 `[0,360)`，例如 `-10` 会按 `350` 处理。
- 两组范围默认均为 `0,0`，未配置时仍会停在旧逻辑的 0 度附近。
- 增加空引用保护：`button`、`turntable`、`keyStartPoint`、`throwHandle` 缺失时不会直接抛出空引用。
- 增加 `_turntableTween` 记录与 `StopTurntableSound()`，对象销毁时会 Kill 旋转 tween 并停止循环音效。
- `ReadyToThrow()` 的 0.1 秒等待后会检查 Unity 对象是否已销毁，避免销毁后继续发奖励或生成物体。
- 新增公开 API `IsStopRangeA`，外部可在 `Play()` 后读取本次旋转选择的停止区间：`true` 表示 A 区间，`false` 表示 B 区间。

## 运行流程

1. 玩家触发 `TurntableButton` 进入回调。
2. `Play()` 检查当前是否正在播放、是否已经取得 key、以及 `turntable` 是否存在。
3. 随机选择停止角度范围并生成目标角度。
4. 维持原来的 DOTween 旋转表现和音效。
5. 旋转完成后进入奖励判定，沿用 key、otherStuff、默认掉落三条既有分支。
6. 如外部需要读取停止区间，可在 `Play()` 后通过 `IsStopRangeA` 判断本次选择的是 A 还是 B。

## 验证记录

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly`：通过，0 个错误，0 个警告。
- 静态扫描 `OnValidate|Reset|ExecuteAlways|ExecuteInEditMode|DB.|Save|RemoteConfig|Instance|Instantiate|Update(`：目标脚本无命中。
- PowerShell 行尾空格检查：目标脚本无行尾空格。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_33/TurntableItemChoiceCtrl.cs`：返回成功；注意该脚本当前仍是未跟踪文件，Git diff 不展示正文，因此以上行尾空格检查作为补充证据。
- 2026-07-08 追加 API 后复验：`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，0 个错误，0 个警告。

## 未验证与剩余风险

- 未在 Unity PlayMode 中实测转盘最终角度、key/other/drop 三条奖励路径、销毁中断场景。
- 未做 Android/iOS 横屏触控 smoke。
- 未验证具体关卡 prefab 挂载与角度区间是否匹配实际 TriggerArea。
- 当前 `Level_033.prefab` 静态证据仍指向 `TurntableItemCtrl`；如果希望新逻辑生效，需要后续在目标 prefab/场景中挂载 `TurntableItemChoiceCtrl` 并配置两组角度范围。
- 新增 API 尚未被现有关卡逻辑调用；外部调用方如果需要“真正停稳后”再读取，应在旋转完成后的逻辑中访问 `IsStopRangeA`。

## 回滚路径

- 仅脚本回滚：移除 `stopAngleRangeA`、`stopAngleRangeB`、随机角度计算、tween/sound 销毁保护相关改动，恢复 `DORotate(new Vector3(0, 0, 360 * 20f), 3f, RotateMode.FastBeyond360)`。
- 若后续 prefab 已切换到新脚本，可重新挂回 `TurntableItemCtrl` 并移除新脚本绑定。
