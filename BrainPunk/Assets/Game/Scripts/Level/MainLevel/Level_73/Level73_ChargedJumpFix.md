# Level073 蓄力跳修复记录

更新时间：2026-07-17

## 背景

Level073 的长按输入会先走共享 `jump_btn.OnClickDown`，因此角色会先普通跳再进入蓄力；`jump_ready_3` 的实际调用又被损坏的注释吞掉；`L73` 重新挂到 Actor 后使用了错误的本地坐标，导致进度条跑到角色左下方的屏幕外。

## 影响范围

- `Assets/Game/Scripts/Level/LevelItem/Level_73/Level73Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_073.prefab`
- 本目录的说明文档

没有修改共享 `GamePlayPanel`、`CustomButton`、`ActorCtrl`、过场、HUD prefab、配置或存档代码。

## 修复后的行为

- Level073 运行时保存 `Root/jump_btn` 的原始状态后，将其 `Interactable` 和组件 `enabled` 都关闭；本关继续通过同一 `RectTransform` 的触摸/鼠标轮询处理输入。这样长按不会再由共享 `OnClickDown` 抢先触发普通跳。
- 未进入蓄力前松手时，仍由本关调用 `ActorCtrl.TriggerJump()` 完成短按跳。
- 按住满 `1.5` 秒后才进入蓄力，停止横向移动，显示 `L73`，并在本关独占的 Spine 轨道 1 循环 `jump_ready_3`。轨道 1 会以 `0.15` 秒从空动画淡入，松手、取消或失焦时再以同样时长淡出，避免蓄力动作硬切；这不会被共享 `ActorCtrl` 每帧写入轨道 0 的 idle 动画重置。
- 普通短按仍只走既有跳跃，不增加前冲。未满蓄力松手时，按蓄力进度的二次曲线同时应用 `18` 至 `30` 的竖直速度和 `2` 至 `24` 的面朝方向前冲速度；本关在角色落地前保持移动锁定，避免 `ActorCtrl` 覆盖前冲速度或接受玩家移动。蓄力条从空到满时间调整为 `4.5` 秒，前段视觉和力度增长均更慢。
- 满蓄力松手时，本关会暂停 Actor 的 `Rigidbody2D` 模拟，以 `DOJump` 精确飞到场景 `EndPoint`，保留 Actor 原有 Z 轴层级。飞行期间关闭本关控制、不会触发沿途物理碰撞或陷阱；到点后调用既有 `BaseLevelCtrl.SuccessStrightly()`，不依赖终点旁门的碰撞触发。
- 本关镜头复用 `10032`（`Level_032`）使用的共享 `CameraFollowArea` 脚本，并将其 `actor` 绑定为 Level073 的 Actor 根 Transform。073 保持 X/Y 均可跟随，因为 `10032` 的 X 锁定配置不适用于本关横向终点路径。
- 填充条每帧独立更新，即使 Spine 引用暂时不可用也会从下向上显示进度。
- `L73` 保持为 Actor 根节点的子物体；其当前相对位置继续以 `Level_073.prefab` 的序列化值为准，便于直接在 Actor 层级中调整。
- 三层渲染顺序保持：`Sp_l73_jd_02` 背景为 0，`Sp_l73_jd_01` 填充为 1，`Sp_l73_jd_03` 边框为 2。

## 验证

- 静态确认 `CustomButton.OnPointerDown` 不检查 `Interactable`，因此禁用组件是阻断共享按下跳跃的必要本关修复。
- 静态确认 `L73` 在 Actor 嵌套 prefab 的 `m_AddedGameObjects` 中已登记，且父级和三层 SpriteRenderer 引用有效。
- 静态确认 `EndPoint` 是纯 Transform，Actor 到点后不能可靠触发门碰撞，因此满蓄力完成时显式复用 `SuccessStrightly()` 成功链。
- 静态确认满蓄力飞行将 Actor 刚体 `simulated` 设为 `false`，并在飞行被中断时恢复原值；目标 Z 轴保留 Actor 当前值，不使用 `EndPoint` 的编辑器 Z 值。
- 已执行局部 YAML 引用检查；Unity PlayMode 与移动端触摸验收仍需在编辑器中完成。

## 待验收与回滚

- PlayMode 需验证：短按松手跳、连续长按超过 `1.5` 秒不预跳、`jump_ready_3` 循环、半满蓄力的前冲距离、满蓄力到 `EndPoint` 后成功、飞行期间左右/跳跃输入无效，以及重开后跳跃按钮恢复。
- 如需回滚，只还原 `Level73Ctrl.cs` 的本关输入、前冲与终点飞行逻辑，以及 `Level_073.prefab` 的本关参数/引用；不要修改共享输入、HUD 或过场代码。

## 2026-07-17 第三轮表现调校

- `Sp_l73_jd_01` 不再按蓄力比例缩放。`L73/ChargeFillMask` 使用同一张填充图作为 `SpriteMask`，只在排序层 2、Order 1 裁切填充层；填充图本身始终保持原始位置、原始比例。遮罩从底部向上缩放和移动，因此低蓄力时先露出素材底部颜色，满蓄力时恢复完整渐变，不会把渐变图压扁。
- 满蓄力飞向 `EndPoint` 的本关参数改为 `2s`、跳跃弧高 `5.5`。起终点约有 32 个世界单位，平均横移速度相对原 `1.2s` 降低约 40%，同时增加可见高度；仍保留既有 `DOJump`、Rigidbody2D 暂停和直达成功流程。
- 本轮只修改 `Level73Ctrl.cs`、`Level_073.prefab` 与本目录文档。没有修改共享角色、HUD、镜头、加载、配置或其他关卡。

### 本轮静态验证与待验收

- `Level_073.prefab` 的 `ChargeFillMask`、`chargeMask`、填充 Renderer 和 `L73` 子节点引用均已核对；173 个本地 YAML fileID 无重复，所有本地 fileID 引用均有定义。
- 已确认填充、遮罩和边框的排序依次为 1、0、2，遮罩自定义范围仅覆盖填充层 1；底图与边框不使用 MaskInteraction。
- Unity 已在本轮脚本写入后重新编译并复制 `Assembly-CSharp.dll`；最新编译段包含 `Csc`、IL 后处理和程序集复制，未发现 `Level73Ctrl` 或 `error CS` 条目。
- 仍需在 Unity PlayMode 验收 0%、半满、满蓄力时的渐变显示，以及满蓄力横向速度、抛物线高度、到达 EndPoint 后的成功过场；Android/iOS 横屏触控与 60 fps 也未实际验收。

## 2026-07-17 第四轮可见性与过渡修复

- `ChargeFillMask` 不再使用 Actor `SortingGroup` 内脆弱的自定义范围；遮罩保持在填充图之前，关闭 Custom Range，并将 Alpha Cutoff 从 `1` 调低到 `0.01`。本关只有 `Sp_l73_jd_01` 使用 `VisibleInsideMask`，因此不会裁切角色、底图或边框。填充图仍保持原始比例，遮罩继续从底部向上展开。
- `L73` 现在在长按进入蓄力时以 `0.12s` 淡入，松手、取消或失焦时以相同时长淡出，淡出完成才关闭根节点。淡入淡出只修改本关三张 `SpriteRenderer` 的 Alpha，不调用 Actor、Spine 或共享 `SpriteRendererGroupAlpha`。
- Actor 的入场透明度组件会把 `L73` 一并收集，启动阶段可能临时把它们置为 0；本关淡入固定使用 `0 -> 1`，不缓存该临时 Alpha，避免蓄力条在角色入场完成后仍被淡成不可见。

### 本轮验证状态

- 静态确认填充源图与遮罩源图均有 8872 个完全不透明像素；`0.01` Cutoff 能保留边缘抗锯齿，同时不会把遮罩裁空。
- 当前 Unity 正在 Play Mode，脚本程序集尚未重新加载，因此需要停止并重新进入 073 后才能看到本轮遮罩范围与淡入淡出修复；旧运行实例的画面不代表本轮代码结果。
- 重新进入后应验收：长按满 1.5 秒时底图/填充/边框短暂淡入，填充从底部正常增长，松手时三层一起淡出，且短按、满蓄力终点飞行和重开保持原行为。

## 2026-07-17 第五轮终点飞行节奏修正

- 满蓄力飞向 `EndPoint` 的 `DOJump` 横向时间插值从 `Ease.OutQuad` 改为 `Ease.Linear`。总时长仍为 `2s`、弧高仍为 `5.5`，因此平均横向速度保持不变；差异是接近落点时不再因缓出曲线突然减速，竖直跳跃弧线仍由 `DOJump` 保留。
- 只影响 Level073 满蓄力的自动飞行。普通短按、未满蓄力的物理前冲、输入锁定、成功调用与其它关卡均未改变。
