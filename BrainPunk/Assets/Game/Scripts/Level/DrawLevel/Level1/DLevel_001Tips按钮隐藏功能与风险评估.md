# DLevel_001 Tips 按钮隐藏功能与风险评估

## 1. 需求与原行为

- 需求：进入 `DLevel_001` 时隐藏 Draw HUD 的 Tips 按钮。
- 原行为：`DrawGame_GamePlayPanel.prefab` 的 `tips_btn` 默认激活，所有 Draw 关卡创建 HUD 后都展示同一按钮；warning 控制也会在所有关卡检查时间或累计重试阈值。
- 当前配置直接证据为 `TbDrawLevelSort id=1 -> DrawLevelData id=20001 -> path=DLevel_001`。实现不依赖可被远端排序调整的显示 ID，而使用解析后的关卡资源路径识别 `DLevel_001.prefab`。

## 2. 影响文件与系统

- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`：保存当前关卡是否展示 Tips，创建 HUD 时传入显示状态，并让 DLevel_001 跳过 warning 阈值派发。
- `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel.cs`：新增内部 `SetTipsButtonVisible(bool)`，只切换已绑定 `TipsBtn` 的 GameObject。
- 不修改 DLevel_001 prefab、关卡脚本、Draw HUD prefab、配置、存档、资源地址、全局事件类型、analytics、广告、IAP 或平台 SDK。

## 3. 修改后行为与边界

```text
TryEnterLevel
  -> ResolveLevelResourcePath(levelPath)
  -> 资源路径等于 Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab
     -> 当前关 Tips 可见状态=false
     -> 创建/重建 HUD 时隐藏 TipsBtn（warning 子节点随父节点不可见）
     -> UpdateTipsWarning 跳过阈值计时与事件派发
  -> 其它 Draw 资源路径
     -> 当前关 Tips 可见状态=true
     -> 保留原按钮、warning 和提示弹窗行为
```

- DLevel_001 的 Reset/失败重试和 Victory Replay 会重建 HUD，但控制器保留当前资源身份，因此按钮继续隐藏。
- 进入其它 Draw 关卡时会重新计算并显式恢复 Tips 按钮，不会把首关状态带到后续关卡。
- 本次只移除首关的提示入口；画线输入、Guide 落笔隐藏、Actor 行走、胜负、墨水、倒计时和打点生命周期不变。

## 4. 风险、验证与未验证缺口

- 资源识别使用 `StringComparison.OrdinalIgnoreCase` 比较标准化完整 prefab 路径，避免以显示编号判断造成排序变更误伤。
- `TipsBtn` 缺失时内部 API 安全跳过；当前 Draw HUD generated 字段和 prefab 绑定已存在。
- 目标代码和中文文档的差异检查通过；`Assembly-CSharp.csproj` 构建结果为 `0 error / 101 个工程既有 warning`，warning 未指向本次修改文件。
- `uncertain`：尚未在 Unity PlayMode 验证 DLevel_001 首次进入、Reset、失败重试、Victory Replay 均隐藏，以及从首关切换到其它 Draw 关卡后按钮恢复。
- `uncertain`：Android/iOS 横屏布局与触控尚未验证；本次没有新增平台分支。

## 5. 回滚路径

- 删除 `DrawPlayCtrl` 的 `TipsHiddenLevelResourcePath`、`_isTipsButtonVisible`、资源路径判断、HUD 显隐调用与 warning 跳过条件。
- 删除 `DrawGame_GamePlayPanel.SetTipsButtonVisible(bool)`。
- 无需迁移 prefab、配置、存档或资源；回滚后 DLevel_001 恢复与其它 Draw 关卡相同的 Tips 入口。
