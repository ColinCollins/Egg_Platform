# Level073 功能说明

> 最新的蓄力跳修复记录见 `Level73_ChargedJumpFix.md`。当前关卡 HUD 变体为 `GamePlayPanel_001`，不再采用本文历史记录中的 `GamePlayPanel_003`。

更新时间：2026-07-17

## 需求与背景

Level073 需要主角镜头跟随，以及保留原有点按跳跃的同时加入长按蓄力跳跃。跳跃按钮按住 3 秒后显示场景内 `L73` 蓄力条，角色播放 `jump_ready_3`，松手时按照填充比例获得更高的竖直跳跃速度。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_073.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_73/Level73Ctrl.cs`
- 已有 `CameraFollowArea`、`GamePlayPanel_003`、`ActorCtrl`、`ActorSpineCtrl`。

没有修改共享角色跳跃、共享游戏面板、存档、配置、全局事件、埋点、广告、RemoteConfig 或平台 SDK。

## 原有行为基线

`GamePlayPanel_003` 的跳跃按钮在按下时派发既有 `PlayerJumpEvent`，由 `BaseLevelCtrl` 调用 `ActorCtrl.TriggerJump()`。Level073 已配置 `jump_ready_1` 和 `jump_ready_3` 动画，并在场景中放置但默认隐藏 `L73`：`Sp_l73_jd_02` 为底图、`Sp_l73_jd_03` 为边框、`Sp_l73_jd_01` 为渐变填充图。

## 实现后行为

- 在 Level073 根节点复用 `CameraFollowArea`，目标绑定主角与本关 GameCamera，X/Y 均允许跟随、无偏移；极小跟随区域使镜头随主角位置移动。
- 短按仍走原有按下即跳路径，不拦截 `PlayerJumpEvent`。
- 按住跳跃按钮满 3 秒后，`L73` 显示并开始在 3 秒内从下往上填满；`Level73Ctrl` 在 LateUpdate 保持播放 `jump_ready_3`，避免被普通角色状态动画覆盖。
- 松手时以填充比例在 `18` 至 `30` 之间计算跳跃竖直速度，并仅在该速度高于当前上升速度时覆盖 Y 速度；未进入蓄力的短按不追加跳跃。
- 进度条填充图原始中心 Pivot 保持不变，控制器同时缩小其 Y 缩放并下移中心，视觉底边固定，因此表现为自下而上填充。

## 数据与流程

跳跃按钮 Down -> 原有跳跃事件 -> `Level73Ctrl` 开始计时 -> 3 秒后显示 `L73` 和 `jump_ready_3` -> 3 秒内逐步填充 -> Up -> 根据比例提高主角 Rigidbody2D 的 Y 速度 -> 既有 `ActorCtrl` 空中动画与碰撞流程继续执行。

## 验证证据

- 静态检查：`L73` 三个 SpriteRenderer 与源图已核对，填充图 `Sp_l73_jd_01` 的 Pivot 为中心，脚本使用底边补偿计算。
- 静态检查：Level073 的 BaseLevelCtrl 已绑定主角，GameCamera 与 GamePlayPanel_003 均存在；跳跃按钮既有路径为 `Root/jump_btn` 的 OnClickDown。
- 静态检查：新控制器仅监听本关跳跃按钮，未订阅或新增全局事件；无 `OnValidate`、`Reset`、编辑器运行时链路或存档访问。
- Unity 导入：Level073 prefab 的 168 个 YAML fileID 无重复；新增脚本的 GUID、蓄力条、主角与 GameCamera 引用均已静态核对。Unity 的 `Assembly-CSharp.dll` 已在新脚本写入后重新生成，最近 Editor.log 未出现 Level073 或 C# 错误。

## 未验证项

- Unity 导入后检查 `Level73Ctrl`、`CameraFollowArea` 和两项根节点绑定无 Missing Script。
- PlayMode 验证短按保持原跳跃、长按 3 秒后动画与进度条出现、不同蓄力比例松手的高度差、松手/禁用/重置后的 UI 复位。
- Android/iOS 横屏验证按住、移出按钮后松手、暂停与失败重开期间没有误加速，且镜头跟随无抖动或越界。

## 回滚路径

从 Level073 根节点移除 `Level73Ctrl` 和 `CameraFollowArea` 两个组件，删除 `Level_73` 本地脚本与文档即可恢复到原有按下即跳和静态镜头行为。

## 2026-07-17 第二轮修正

- 共享 `GamePlayPanel_003` 的 `jump_btn.OnClickDown` 会立即派发 `PlayerJumpEvent`，旧的 Down/Up 监听无法满足“长按不先跳”。本关启动后保存并关闭该按钮的 `Interactable`，这会关闭其 Graphic 射线和 `Collider2D`；关卡改为只在原按钮 `RectTransform` 的屏幕范围内处理触摸与编辑器鼠标。
- 短按在松手时调用既有 `ActorCtrl.TriggerJump()`；长按满 3 秒前不跳，开始蓄力后调用 `StopMoving()`，在 `LateUpdate` 保持 `jump_ready_3` 循环。蓄力松手按 18 至 30 的蓄力速度起跳，取消、失焦、触点丢失、禁用和销毁均恢复移动但不跳。
- `L73` 已移动至 `{x: -10.3, y: -1.1}`。渲染顺序改为底图 `Sp_l73_jd_02=0`、填充 `Sp_l73_jd_01=1`、边框 `Sp_l73_jd_03=2`，填充图不再被底图遮挡。
- `L73` 现为 Actor 根节点的直接子物体，本地坐标为 `{x: -10.673412, y: -2.9056954}`，由原场景坐标减去 Actor 初始坐标换算而来，以保持当前画面位置。后续可直接在 Actor 层级中调整相对位置；该节点仍不含拖拽或碰撞组件。
- `Level_073.prefab` 未引用 `DragableItem` 的 GUID，且 `L73` 根节点及子节点只有 Transform/SpriteRenderer、没有 Collider2D；本轮没有为蓄力条新增拖拽或碰撞组件。

静态检查已确认没有新增全局事件、DB/save、配置或共享 `GamePlayPanel`/`ActorCtrl` 改动。Unity PlayMode 和 Android/iOS 横屏仍需验收按钮命中、短按、恰好 3 秒、半满/满蓄力、暂停/失焦、死亡/重开和跨关卡按钮恢复。回滚本轮时恢复本关脚本与 `L73` 位置/排序配置即可。
