# Level119 重开生命周期功能说明

## 需求与问题

- Reset/Replay 采用主线销毁旧 Level、实例化新 Prefab 的流程，旧 Level119 不应完整复位。
- 胜利 Replay 从 `SUCCESS` 状态创建新实例时，`statePlaying` 初始化为 false；Pause 退出只派发 `GameResumeEvent`，原代码没有把它恢复为 true，导致假提示弹窗请求长期卡在状态门禁。

## 当前行为

- `GameResetEvent` 和 `OnDisable` 只调用 `BeginTeardown()`：关闭弹窗、解绑事件、停止 Sequence/旋转/粒子、关闭 UFO 杆碰撞并锁住旧实例输入，不恢复层数、UFO、Actor、相机或控制初态。
- 新实例仅通过 `OnEnable -> InitializeRuntimeState()` 建立初态。
- `OnGameResume()` 下一帧读取 `PlayCtrl` 的真实状态，再恢复 `statePlaying/gameplayActive`；这样既覆盖 Replay 的 `PAUSE -> PLAYING`，也不依赖 Pause 退出时的事件订阅顺序，玩家再次操作时可以正常弹出假提示。

## 验证与回滚

- `Assembly-CSharp.csproj` 编译：0 error / 120 warning。
- 尚未执行胜利 Replay 后首次左右移动/跳跃弹窗、弹窗关门成功、失败 Reset 和移动端横屏 PlayMode，标记为 `uncertain`。
- 回滚时恢复 Reset 完整复位并移除 Resume 后的延迟状态同步；会恢复 Replay 弹窗不出的缺陷。

## 2026-09-09：RemainingLayersWallText 与语言同步

- 需求：由 Level119Ctrl 控制场景剩余层数文本，并在切换语言后更新。
- 原始基线：当前工作区 Prefab 已移除该文本的 Localize / LocalizationParamsManager；旧脚本仍写 remainingLayers 参数，失败后拼接中文。I2 现有词条实际使用 {0}，且仅比较层数的缓存无法响应语言切换。
- 实现：Level119Ctrl 保留 Inspector 中已有 remainingLayersText 引用，直接读取 S_LevelTips_Des_level_106_1 并格式化 999 - completedLayers（下限 0）。初始化与每层落定复用原刷新入口；OnEnable 订阅 I2 OnLocalizeEvent，语言刷新后使数字缓存失效并重绘，OnDisable 解除订阅。无新增对象查找、全局事件或 Reset 清理。
- 影响范围：仅本关脚本；复用 Assets/Resources/I2Languages.asset 的现有 12 个非空翻译。本次未修改 Prefab、共享脚本、翻译资源、存档、打点、广告或资源加载流程，保留用户已有 Prefab 改动。
- 验证：使用 Unity 6000.0.59f2 自带 Roslyn 和现有 Assembly-CSharp.rsp 完整编译，输出重定向至临时目录，退出码 0；12 个非空模板分别格式化 999、998、0 通过；现有 TMP fileID 绑定核对通过；git diff --check 通过。
- 未验证：PlayMode 初始显示、落层刷新、暂停中切换语言、禁用/重建后的订阅与显示、Android/iOS 横屏字号和字形仍为 uncertain。当前已配置语言无 RTL；未来开放 RTL 语言时需另验排版。
- 回滚：仅撤回本次 Level119Ctrl 文本相关差异；不撤回用户的 Prefab 修改。撤回后旧逻辑仍会因缺少参数组件回退中文。
