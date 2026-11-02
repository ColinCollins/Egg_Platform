# Level 105 不要操作功能说明

## 当前有效结论

Level 105 是关卡本地隔离的“不要操作”自动流程：规则从关卡出现第一帧生效，玩家除暂停、重来、提示三个系统入口外的移动、跳跃、HUD 按钮或普通屏幕按下都会判负；不操作时主角自动完成开场、陷坑 5+1 跳、假金币、假胜利和真门通关。

本说明依据当前 `feature/1.11.0_common@5c65e25e4` 的代码与 Prefab 整理。当前分支没有本关测试、运行日志或截图；假弹窗“只展示、不发奖、不写业务数据”目前只有静态代码证据，必须动态验证。

## 组成与职责

- `Domain/Level105DontOperateRule.cs`：零 Unity 依赖的阶段、计时、5+1 跳计数、挂起、白名单和胜负模型。
- `Level105Ctrl.cs`：输入判定、自动移动、陷坑物理、暂停恢复、真假门、标准胜负与清理。
- `Level105EntranceCtrl.cs`：首次进入时可取消、可挂起的起始门出场；失败重开可跳过首次演出。
- `Level105FakePopupCtrl.cs`：按资源名打开关卡专属 UI Variant，等待真实入场完成，拦截假按钮并在反馈结束后收口失败。
- `Level105FakeRewardPopupView.cs`：展示 100 金币外观，阻断业务按钮。
- `Level105FakeVictoryPopupView.cs`：复用真实胜利面板外观和入场表现，只刷新只读展示并阻断业务按钮。

## 自动流程

1. `Awake` 即建立输入判负窗口；首次进关由起始门播放出场，重开路径直接进入就绪。
2. 出场完成后原地等待 5 秒，然后自动向右跑到陷坑。
3. 主角落到坑底后执行 5 次原地完整起落；第 5 次落地立即执行独立第 6 次向右脱坑跳。
4. 脱坑后跑到世界 `X=1`，空场景等待 2 秒，再打开假 100 金币弹窗；真实入场完成后完整展示 5 秒并销毁。
5. 主角继续跑到世界 `X=4.1`，立即打开假胜利面板；入场完成后完整展示 5 秒并销毁。
6. 假胜利完全移除后才启用第二扇真门触发器。主角自动跑进真门，`BaseLevelCtrl.IsActorEnterDoor` 成立后 Domain 登记 Victory，后续沿标准成功链结算。

## 输入判负与白名单

- 玩家移动/跳跃事件统一进入 `NotifyPlayerOperateInput()`。
- 普通鼠标按下或 Touch Began 会用 EventSystem 射线判断是否命中系统白名单；未命中暂停、重来、提示就判负。
- GameplayPanel 中非白名单 `CustomButton` 设置 `IsBlockClick=true`，同时订阅 `OnClickDown` 触发本关失败；销毁时恢复原值并解绑。
- 假弹窗按钮同样阻断业务 `OnPointerClick`，保留按下/抬起反馈；按下同帧锁定 Failure，约 0.2 秒反馈结束后关闭 UI 并播放死亡。
- 真门已经成立的同帧优先提交 Victory，随后输入不再判负。

## 暂停与生命周期

- 暂停、提示及其子面板会本地挂起 Domain、计时、开场流程和 Actor 物理，不修改全局 `Time.timeScale`。
- 恢复事件延后一帧，并持续等待可见 `UIMask` 消失，避免关闭系统 UI 的同一次点击被当作违规输入。
- `GameResetEvent`、SUCCESS/FAILED、`OnDisable` 和 `OnDestroy` 会停止自动移动、关闭真门、取消开场、销毁假弹窗、恢复 Actor 物理并解绑按钮/事件。

## UI、数据与资源流

- 资源目录：`Assets/Game/Prefabs/Level/Level_105/Resources/`。
- `Level105FakePopupCtrl` 通过 UIManager 打开：
  - `Level105_CommonGetRewardPopup`
  - `Level105_GameVictoryPanel`
  - `Level105_GameVictoryPanel_Reward`
- 假胜利普通/奖励版只读检查当前关卡是否已在 `DB.GameData.PassedLevels`。
- 展示层还只读使用 `HasSharedLevel`、分享金币 RemoteConfig、NoAds 购买/道具状态和 Postcard 星星数量来还原真实胜利面板外观。
- 当前 Level 105 脚本中未发现直接发奖、保存、广告、分享、切下一关或 analytics 调用；继承组件和 Prefab 既有回调是否被 `IsBlockClick` 完整阻断仍需 PlayMode 证明。

## Prefab 与配置

- 关卡资源：`Assets/Game/Prefabs/Level/Level_105.prefab`。
- Domain 程序集：`Game.Level105.Domain.asmdef`，零 Unity 引用。
- 当前 `tbleveldata.json` 与 `tblevelsort.json` 没有 Level 105 条目。是否只用于 GM/调试暂存为 `uncertain`。

## 验证与回滚

- 已验证：脚本、Prefab、三份 UI Variant 资源存在；静态检查未发现本关直接业务写入；输入、暂停、UI 和清理调用链已复核。
- 未验证：Unity 编译、Prefab 引用、完整自动正解、所有违规输入、系统白名单、假 UI 无副作用、重开/退出和移动端表现。
- 回滚：选择性移除 `Level_105` 脚本目录、`Level_105.prefab` 及其 `Resources` 子目录和本地文档。当前没有 Level 105 配置或数据迁移。
