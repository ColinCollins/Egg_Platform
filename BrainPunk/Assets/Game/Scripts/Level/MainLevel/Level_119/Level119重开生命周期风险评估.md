# Level119 重开生命周期风险评估

## 原始基线

- Reset 在旧实例上清空层物件、恢复 UFO/Actor/相机、清除成功状态并重新开放控制。
- Replay 新实例从 `SUCCESS` 创建后保持 `statePlaying=false`，弹窗触发请求可以产生但无法打开。

## 风险控制

- teardown 只做停止、取消、解绑和输入锁，不改旧实例作者初态，避免黑幕窗口视觉跳变与控制复活。
- Resume 状态同步延后一帧并重新读取 `PlayCtrl.CheckState(PLAYING)`；Reset teardown 会解绑事件并停止待执行协程，因此不会让旧实例重新激活，也不依赖事件订阅顺序。
- 未修改弹窗资源、I2 多语言、层数文本、成功门、存档、配置、广告和统计。

## 验证缺口与回滚

- C# 编译通过，0 error / 120 warning。
- `GameResumeEvent` 由 `PlayCtrl_Pause.OnExit()` 在状态机切换当前节点之前派发；本关下一帧读取真实状态，Pause 若退出到非 PLAYING 也不会误开玩法。
- PlayMode 与 Android/iOS 尚未验证，结论标记 `uncertain`。
- 回滚为恢复旧 Reset 复位和移除 Resume 状态同步，不涉及 Prefab 或共享状态机。

## 2026-09-09：剩余层数多语言风险补充

- 基线与范围：当前 Prefab 已无 I2 文本/参数组件，旧 Ctrl 会写中文；本次仅修改 Level119Ctrl 文本刷新和 I2 回调订阅，沿用已有 TMP 绑定及词条。
- 既有系统：初始化、层物件落定时调用 UpdateRemainingLayersText；I2 OnLocalizeEvent 在语言刷新后通知，OnDisable 解绑以避免静态事件持有旧实例。
- 风险边界：显示依赖已有 I2 语言源就绪及词条含 {0}；没有增加自动生成词条、中文覆盖或运行时对象查找。当前 12 个非空翻译已校验，未来翻译修改需保留占位符。
- 验证：Unity Roslyn 全工程编译退出码 0；12 个模板 × 3 个边界层数格式化通过，Inspector 引用静态确认。
- 未验证缺口：真实语言切换回调、重复进关、数字递减、移动端布局与字形仍为 uncertain；静态格式化不代表运行时或视觉验收。
- 回滚：撤回本次 Ctrl 文本刷新与语言订阅差异，保留原 Prefab 和重开生命周期逻辑。
