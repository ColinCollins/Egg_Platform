# IWannerGame RemoteConfig 同步工具功能记录

## 需求与背景

- IWannerGame 的 GlobalConst 已移除 `iWannerGame_ShowTipsTimeLimit`。
- 插屏最低门槛字段由 `iWannerGame_interstitialShowMinLevel` 改为
  `iWannerGame_interstitialShowMinGroup`，业务含义改为配置顺序中的 Group 序号。
- 本次只同步 Editor 生成工具和运行时 RemoteConfig 读取 API，不接入插屏展示流程。

## 影响范围

- `Assets/Game/Editor/LubanGenerator.cs`
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
- 输入数据：`Assets/Game/Configs/lubanconfig_tbglobalconst.json`
- 后续生成目标：`Assets/Guru/guru_spec.yaml`

## 修改前后行为

- 修改前，生成器仍要求已删除的提示时间字段，并读取旧的最小关卡字段；运行时 API 也会访问
  已不存在的 GlobalConst 属性，重新生成配置后会导致编译失败。
- 修改后，生成器只同步当前 GlobalConst 中存在的八个 IWannerGame 字段；更新 YAML 时会明确清理
  两个已废弃字段，同时继续保留其它未知扩展字段。
- 运行时移除提示时间读取 API，并提供
  `GetIWannerGameInterstitialShowMinGroup()`，优先读取 `iwannergame_config`，缺失时回退当前
  GlobalConst 的 `IWannerGameInterstitialShowMinGroup`。

## 数据流

```text
Luban Global.xlsx
  -> lubanconfig_tbglobalconst.json / GlobalConst.cs
  -> LubanGenerator 的 IWanner 字段白名单
  -> guru_spec.yaml 的 iwannergame_config
  -> RemoteConfigService 拉取
  -> ConfigManager.RemoteConfig 运行时读取
```

## 验证记录

- `git diff --check`：目标脚本无空白错误。
- `Assembly-CSharp-Editor.csproj`：编译成功，`0 error / 4 warning`，warning 为工程既有 Editor 警告。
- `Assembly-CSharp.csproj`：编译被当前工作区
  `IWannerPlayCtrl.cs` 中缺失的 `TryOpenLevelSelection` 阻断；本次两个目标脚本未产生新增编译错误。
- 未在 Unity Editor 内实际点击 `Update IWannerGameConfig To Guru YAML`，因此 YAML 写入结果仍需手工验证。

## 剩余风险与回滚

- 当前 `lubanconfig_tbglobalconst.json` 中 `iWannerGame_interstitialShowMinGroup` 的值为 `6`；
  本次按用户要求不修改配置数据，最终线上值需由配置负责人确认。
- `guru_spec.yaml` 仍保留旧字段，需在 Unity Editor 中执行更新菜单后确认旧字段被清理、新字段被写入。
- 回滚时恢复 `LubanGenerator` 的旧字段白名单和 `ConfigManager.RemoteConfig` 的旧常量/读取 API；
  若 GlobalConst 已保持新结构，回滚会重新产生字段不匹配，必须同时回滚 GlobalConst 数据与生成代码。

## 2026-08-13：新增小关通过次数插屏字段

- `GlobalConstIWannerGameConfigKeys` 已补入 `iWannerGame_interstitialShowPassedLevelCount`，同步菜单会把该字段作为必需字段参与预览、合并与 Guru 官方代码生成；源 JSON 缺失时整体停止，不做部分写入。
- `ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowPassedLevelCount()` 优先读取 `iwannergame_config`，缺失时回退 GlobalConst，全部不可用时返回 `-1`；负值由运行时策略视为关闭。
- 当前 `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 已按 `lubanconfig_tbglobalconst.json` 同步为九个现行字段，并移除两个历史字段；当前新字段默认值为 `-1`。
- 运行时与 Editor 定向构建均通过；尚未在 Unity Editor 实际点击同步菜单，因此确认弹窗、`AssetDatabase.ImportAsset` 和 Guru 菜单日志仍为 `uncertain`。
- 回滚时从白名单、运行时 getter 和 Guru 默认载荷移除新字段；若 GlobalConst 仍保留该列，生成产物可以继续包含属性，但运行时不再消费。
