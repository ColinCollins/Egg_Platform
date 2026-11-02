# DLevel_999 音效功能与风险评估

## 1. 需求与原行为基线

- 需求：进入 Draw 的 `DLevel_999` 时播放主线 TBC 关相同的 `TBC` 音效。
- 原行为：主线 `PlayCtrl.RefreshGamePanel()` 在 `Level.IsLastLevel` 时调用 `AudioManager.PlaySound("TBC")`；Draw 的 HUD 创建链没有对应调用，仅在进入 Drawing 时保证 `musicInGame` 背景音乐。

## 2. 影响文件、资源与系统

- 修改：`Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- 记录：本文件、`DLevel_999脚本结构记录.md`、Draw 中央演进记录及 `DevelopMD/已有功能设计与使用记录.md`
- 复用资源：`Assets/SoundsGood/Data/Collections/SoundCollection.asset` 的 `TBC` 标签及既有 AudioClip
- 未修改：`DLevel_999.prefab`、Luban 表、DB/存档、UI prefab、事件、Analytics、广告、IAP、RemoteConfig、平台 SDK

## 3. 修改后行为

- `TryCreateGamePlayPanel()` 创建 Draw HUD 并设置关卡 ID 后调用 `IsTbcLevel()`。
- 判定直接读取 `TryEnterLevel()` 已标准化并保存的实际资源路径，只接受
  `Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab`（忽略大小写）。
- 命中后调用既有 `AudioManager.PlaySound("TBC")`；SFX 关闭时由 AudioManager 直接跳过。
- Reset/Replay 重建 HUD 时会再次触发，保持与主线 `RefreshGamePanel()` 的调用边界一致。
- `musicInGame` 背景音乐、Draw 输入、Intro、结算和退出流程不变。

## 4. 风险项

1. Reset/Replay 会再次播放 TBC，可能产生频繁重播听感；当前与主线行为一致，仍需 PlayMode 验证。
2. `TBC` 为一次性 SFX，会与 `musicInGame` 同时存在；实际混音响度未在设备验证。
3. Android/iOS 音频焦点、静音切换与低端设备解码表现未验证，标记为 `uncertain`。
4. Editor 日志仍存在既有 Analytics 查询对运行时追加 `sortId:45` 的缺失警告；当前音效门禁已与该查询解耦，Analytics 修复不在本需求范围。

## 5. 验证证据

- `tbdrawleveldata.json` 静态确认 `20999` 的 `path` 为 `DLevel_999`。
- `SoundCollection.asset` 静态确认 `TBC` 标签绑定一个 AudioClip。
- Editor 日志直接确认失败根因：上一版通过原始 `TbDrawLevelSort` 查询运行时追加项时输出
  `DrawLevelData is missing. sortId:45`，导致门禁未命中。
- 修复后代码静态确认门禁使用实际标准化资源路径，位于 HUD 创建成功之后，且只触发既有 `AudioManager.PlaySound()`。
- 编译：`dotnet build Assembly-CSharp.csproj --no-restore -m:1 --nologo` 成功，`0 error / 101 个项目既有 warning`。
- `uncertain`：Unity PlayMode、Reset/Replay、SFX 关闭、Android/iOS 横屏设备尚未执行。

## 6. 回滚路径

- 从 `DrawPlayCtrl` 删除 `TbcLevelResourcePath`、`IsTbcLevel()` 及 HUD 创建后的 TBC 播放分支。
- 不需要回滚音频资源、prefab、配置或迁移玩家数据。

## 7. 待确认项

- 当前没有需求要求 TBC 只在首次进入播放；实现对齐主线，在每次 HUD 重建时播放。若后续只允许单次播放，需要新增局部会话门禁并补 Reset/Replay 产品规则。
