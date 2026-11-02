# 插屏广告关卡区间配置功能与风险记录

## 1. 需求与原有行为

- 需求日期：2026-08-18。
- `Luban/Configs/Datas/Const/Global.xlsx` 新增主线、DrawGame、IWannerGame 三组关卡区间、胜利插屏间隔和失败/重置门槛配置。
- 原实现分别从 `level_config`、`drawgame_config`、`iwannergame_config` 读取单个固定的间隔数和失败门槛；同一玩法所有关卡使用同一值。
- 原有免广告、插屏 CD、激励视频后 CD、最小关卡、成功计数、失败/重置计数、IWanner 小关通过计数和 placement 分流保持不变。

## 2. 配置定义

| 玩法 | 区间上界 | 区间胜利间隔 | 区间失败/重置门槛 |
| --- | --- | --- | --- |
| 主线 | `6\|26\|56\|76\|999` | `6\|5\|4\|3\|2` | `-1\|30\|30\|30\|30` |
| DrawGame | `6\|999` | `6\|5` | `30\|30` |
| IWannerGame | `1\|999` | `6\|5` | `30\|30` |

- `LevelRange` 按严格递增的包含上界解释，选择第一个“大于等于当前关卡”的上界所对应的同索引值。
- 例如主线第 7～26 关使用胜利间隔 `5`、失败门槛 `30`；第 77～999 关使用间隔 `2`、失败门槛 `30`。
- 当前关卡超过最后上界时沿用最后一档，避免后续新增关卡因缺少档位而关闭插屏。
- 区间上界和值数组必须非空、等长且上界严格递增；不满足时拒绝整组区间配置并进入兼容回退。
- 字段拼写以源表为权威，包含既有的 `interstital`、`interstitital`、`mianGame`、`iwannarGame` 拼写，不在运行时代码中自行改名。

## 3. 新行为与兼容顺序

- `InterstitialAdPolicyBase.CanShowInterstitial(currentLevel, placement)` 每次判断时把当前玩法关卡序号传给间隔和失败门槛读取入口。
- 当前关卡来源保持现有定义：主线读取当前主线关卡序号，DrawGame 读取画线关卡显示序号，IWannerGame 读取当前 Group 的一基顺序。
- 新客户端按以下顺序读取每个间隔或失败门槛：
  1. 当前玩法 RemoteConfig 的新区间数组。
  2. 当前玩法 RemoteConfig 的旧固定字段。
  3. 本地 `GlobalConst` 的新区间数组。
  4. 本地 `GlobalConst` 的旧固定字段。
- 原有固定字段没有从 Global 表、Luban 产物、Guru 默认载荷、`LubanGenerator` 白名单或运行时常量/API 中删除。旧客户端继续读取固定字段；未下发新区间字段的旧 RemoteConfig 也继续控制新客户端。
- `-1` 仍通过既有 `failCountThreshold > 0` 判断表示关闭失败触发；不改变失败或 Reset 的计数时机。

## 4. 数据与调用流

```text
Global.xlsx
  -> Luban 生成 GlobalConst.cs / lubanconfig_tbglobalconst.json
  -> LubanGenerator 按玩法白名单写入 Guru 三个默认载荷
  -> GuruAllSpecs.G.cs
  -> RemoteConfigCache（运行态）
  -> ConfigManager.RemoteConfig 按当前关卡解析区间
  -> Main / Draw / IWanner InterstitialAdPolicy
  -> CanShowInterstitial 的胜利间隔或失败门槛判断
```

- 未新增存档字段、资源地址、全局事件或广告 placement。
- 失败/Reset 仍只调用现有 `OnPlayerFailedOrRetried()`；成功仍调用现有 `OnLevelSucceeded()`；插屏成功回调后的计数重置规则不变。
- GM 插屏调试数据显示当前玩法、当前关卡实际解析后的间隔；补充 IWanner 独立 DB 数据显示。

## 5. 影响文件与系统

- 源配置：`Luban/Configs/Datas/Const/Global.xlsx`。
- Luban 产物：`Assets/Configs/Code/LubanConfig/GlobalConst.cs`、`Assets/Game/Configs/lubanconfig_tbglobalconst.json`。
- Guru 默认配置：`Assets/Guru/guru_spec.yaml`、`Assets/Guru/Gen/GuruAllSpecs.G.cs`。
- 编辑器工具：`Assets/Game/Editor/LubanGenerator.cs`。
- 运行时配置与策略：`Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`、`Assets/Game/Scripts/Ads/InterstitialAdPolicy.cs`。
- 调试显示：`Assets/Game/UI/GM/Scripts/GMPanel.cs`、`Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`。
- 不改变玩家存档结构、广告 SDK 调用、UI prefab、关卡 prefab、打点事件顺序或资源加载流程。

## 6. 验证证据

- 使用 `@oai/artifact-tool` 回读 `Global.xlsx`，确认九个新增字段和值与本记录一致，旧固定字段仍存在。
- 执行 Luban client/json/cs-simple-json 生成成功，实际覆盖 `GlobalConst.cs` 和 `lubanconfig_tbglobalconst.json`。
- 静态一致性脚本确认九个新区间字段在 Global JSON、`LubanGenerator`、生成 C#、运行时常量和三个 Guru 默认载荷中名称和值一致。
- 兼容检查确认三个 Guru 默认载荷中的六个旧间隔/失败字段仍存在。
- 主线边界抽查结果：`6=6/-1`、`7=5/30`、`26=5/30`、`27=4/30`、`56=4/30`、`57=3/30`、`76=3/30`、`77=2/30`、`999=2/30`、`1000=2/30`，格式为“胜利间隔/失败门槛”。
- `Assembly-CSharp.csproj` 定向构建通过：`0 warning / 0 error`。
- `Assembly-CSharp-Editor-firstpass.csproj` 定向构建通过：`0 warning / 0 error`。
- `Assembly-CSharp-Editor.csproj` 定向构建通过：`0 error / 4 warning`；warning 均来自既有 `Assets/Editor/BuildPipline/BuildToolV2.cs`。
- 本任务目标文件的 `git diff --check` 通过；仅报告仓库既有 CRLF 转换提示，没有空白错误。

## 7. 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode 使用真实 RemoteConfig 分别验证三玩法各边界关卡、失败/Reset 累计、成功计数和插屏成功后的计数重置。
- `uncertain`：尚未验证真实广告 SDK、原生覆盖层回调、Android/iOS 横屏和热更新配置下发。
- `uncertain`：本次未在 Unity Editor 实际点击三个 Guru 同步菜单；YAML 与生成 C# 已静态同步，但菜单确认框、AssetDatabase 导入和 Guru 官方生成日志仍需人工验证。
- 非法远端区间会安全回退旧固定值，但当前没有额外上报配置错误日志；需要结合 RemoteConfig 发布校验避免静默使用旧值。

## 8. 回滚路径

1. 恢复 `ConfigManager.RemoteConfig` 的固定值读取方法和 `InterstitialAdPolicy` 的无关卡参数调用。
2. 从 `LubanGenerator` 三个玩法白名单及 Guru 三个默认载荷移除九个新区间字段，保留所有旧固定字段。
3. 从 `Global.xlsx` 移除九个新区间行后重新执行 Luban，禁止手工长期维护生成 C# 或 JSON。
4. 恢复 GM 调试数据显示固定间隔；无需迁移或清理玩家存档，因为本次没有新增存档字段。
