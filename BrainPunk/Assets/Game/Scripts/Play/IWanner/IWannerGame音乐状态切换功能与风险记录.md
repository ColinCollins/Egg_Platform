# IWannerGame 音乐状态切换功能与风险记录

## 需求与背景

- 需求：IWanner 进入可操作的 Game play 状态时播放 `musicInGame`，玩法结束时播放 `musicOutGame`。
- 修改前 IWanner 的进入、组结算和退出生命周期没有主动切换音乐；主线 `PlayCtrl_Playing`、`PlayCtrl_Start`、`PlayCtrl_Failed` 以及 Draw 玩法已有同名音乐标签的切换基线。

## 影响文件与系统

- 修改 `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`。
- 复用 `Game.Scripts.Common.AudioManager` 与现有 SoundsGood 标签 `musicInGame`、`musicOutGame`。
- 不修改音频资源、SoundsGood 配置、UI/prefab、存档、Luban/RemoteConfig、全局事件、分析打点、广告、支付或平台 SDK。

## 修改后行为与调用流

1. `OpenCurrentLevelAsync()` 完成关卡加载、黑幕渐隐并恢复 Level/HUD 输入后，若当前不是 `musicInGame`，则播放 `musicInGame`。
2. Reset、组内下一小关及 Replay 都复用上述入口；若同一 `musicInGame` 正在播放，不会重新起播。
3. `HandleGroupCompletion()` 锁定末关输入并进入整组结算时，切换为 `musicOutGame`，沿用主线退出状态的 8 秒淡入参数。
4. 活动 IWanner 控制器执行 `Deactivate()` 时再次保证 `musicOutGame`；已在整组结算切换过时不会重播。非活动控制器在 Shutdown 中重复 Deactivate 不触发音乐切换。
5. 从 Victory Replay 返回关卡时，只有新关加载完成并恢复可操作后才重新切回 `musicInGame`。

## 原有行为基线与风险控制

- 音乐切换放在玩法控制器现有生命周期边界，没有新增状态机、逐帧轮询或全局事件。
- 失败后等待 Reset 仍属于当前小关运行期，保持 `musicInGame`；只有整组完成或退出玩法才播放 `musicOutGame`。
- `AudioManager.PlayMusic()` 继续遵守现有 MusicOn 设置；本次不改变静音、暂停、音量或 SFX 行为。
- 标签相等检查防止 Reset、下一小关、重复结算或退出链重复重启同一音乐。

## 验证记录

- 静态核对 IWanner 可操作入口为 `OpenCurrentLevelAsync()` 恢复 Level/HUD 输入后的收口位置，整组结束入口为 `HandleGroupCompletion()`，玩法退出入口为 `Deactivate()`。
- 静态核对主线对 `musicOutGame` 使用 8 秒淡入、对 `musicInGame` 使用默认淡入；IWanner 与该参数基线一致。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo --verbosity:minimal`：`0 error / 109 warning`；warning 为工程既有范围，未发现本次音乐修改引入的编译错误。
- `git diff --check` 对本次跟踪文件通过；新增 Markdown 与 meta 另行完成行尾空白检查。
- `uncertain`：尚未在 Unity PlayMode 实听首次进入、Reset、组内推进、末关结算、Victory Replay、暂停菜单退出及音乐开关组合。
- `uncertain`：尚未在 Android/iOS 真机验证原生覆盖、应用前后台切换及设备音频焦点恢复。

## 回滚路径

- 从 `IWannerPlayCtrl` 删除音乐标签常量、`PlayInGameMusic()`、`PlayOutGameMusic()` 以及 `OpenCurrentLevelAsync()`、`HandleGroupCompletion()`、`Deactivate()` 中的调用即可。
- 回滚不涉及音频资源、SoundsGood 配置、存档、prefab、事件或玩家数据迁移。
