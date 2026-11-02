# Level120 功能与风险记录

## 需求与边界

- 红色机关弹簧首次完成“压下、回弹”后才打开上层隐藏入口；白色底座始终停留在初始位置，不参与压缩、顶出或隐藏。机关之后仍可重复踩踏，但只进行小幅压缩和小幅弹起。角色从入口落到下层，进入左侧门通关。
- 右侧门保留为物理误导路线，不触发成功。
- 蓝色弹簧区域的平台表面使用一条连续横向尖刺，不在弹簧位置留空；按钮压到最低点仍位于尖刺上方。
- 弹簧长度、按钮高度、尖刺位置和尺寸全部固化在 `Level_120.prefab`，不由运行时代码生成或改写静态场景布局。
- 相机在角色进入下层前完全锁定开局 Y，只保留水平安全区跟随；角色向下进入隐藏入口的平台厚度时立即切换为上下、左右双轴持续跟随。

## 受影响文件

- `Assets/Game/Prefabs/Level/Level_120.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_120/Level120Ctrl.cs`
- 本目录对应 `.meta` 文件
- `DevelopMD/已有功能设计与使用记录.md`

## 当前实现

1. `Level120Ctrl` 仅绑定本关已有的 1 个红色弹簧和 9 个蓝色弹簧。所有弹簧运行时压缩统一修改弹簧 Transform 的 `localScale.y`，并补偿位置保持底端固定；不再修改 SpriteRenderer 的 Sliced `size`。可动按钮、触发区与弹簧顶部同步下压，水平移动仍由共享 Actor 控制。红色机关中无 Collider 的白色 `Sp_button_1_02` 底座不参与压缩或顶出，也不再在首次机关完成时隐藏，始终停留并显示在初始位置。
2. 9 个蓝色弹簧在 Prefab 中使用 Sliced Sprite 设置为长短交替。弹力依据 Prefab 中的实际高度在 `18.5～34` 之间插值，使物理最高点约为上一版的 50%；最后一根弹簧没有使用最高弹力，按当前 `moveSpeed=5 / gravityScale=4.5` 的理论滞空距离仍小于右侧高台间距。
3. 红色弹簧首次使用独立弹力 `26`。Prefab 保持默认收起状态并显示白色按钮底座；触发时先压缩并弹起角色，回位后于 `0.22` 秒内向上顶出约 `1.9` 世界单位至演示位置并保持，随后打开隐藏入口。白色底座全程留在初始位置，只有红色按钮、触发区和弹簧移动。顶出后的触发区同步上移；即使 Actor 先从侧面进入触发区，控制器也会在其主 Collider 真正落到按钮顶面时逐帧补判触发，不再要求玩家跳出触发区后重新进入，也不允许稳定站在按钮上。Actor 上升越过按钮 `0.35` 后即可重新上弦，下降落回按钮时重复弹动；后续使用弹力 `16.5`，以 `0.18` 秒从顶出位置向下压缩 `1.65` 世界单位，即按钮最低点停在初始位置上方约 `0.25`，发射 Actor 后再以 `0.24` 秒回位，不重复机关顶出或开入口。Reset/Replay 会恢复默认收起状态并保持白色底座显示。
4. 尖刺使用一条宽 `66.5` 的 Sliced Sprite 连续覆盖弹簧区地面。触发后统一调用本关已有 `TrapEventHandlle.PlayKnock`，沿用项目原陷阱死亡、击飞、音效和失败事件。
5. 9 根蓝色弹簧保持顶端与按钮位置不变，Sliced 底端延伸到平台下沿；弹簧排序低于平台和尖刺，因此伸入部分由平台前景遮住，不会在平台下方露出。
6. 上层地面从开局起就是三段有效碰撞，不再关闭整块 Collider 后用三块外置 Collider 重建：`Ground_mid` 自带 Collider 在 Level120 实例内缩短为左段，世界 X=`-14.682582～-5.02`；中段 `HiddenEntranceBlocker` 覆盖 X=`-5.02～-2.22`、宽 `2.8`；右段 `Ground_mid_RightCollision` 覆盖 X=`-2.22～-1.712970`。只有中段绑定运行时开关，左右段始终有效。机关完成后同时关闭中段和红弹簧 `Sp_l94_dm_01` 上覆盖洞口的实体 Collider。洞口只扩大物理落口；褐色后层与白色前层分别恢复原始 Sliced 本地宽度 `2.11 / 2.24`，避免把不透明的竖向墙面素材横向拉宽后在场景中形成浅色竖带。
7. 右侧门的所有 `OnTrigger2DHandle` 与 Trigger Collider 都在本关初始化时关闭，并且不再承担本关成功动画，只保留外观误导。Level120 根 `BaseLevelCtrl.successAnim` 显式绑定左侧下层 `door_0 (1)` 实例的 `SuccessAnimCtrl`；进入左下门后，标准成功动画会在该门原位吞入 Actor 并结算，不会调用最右侧误导门的动画或把 Actor 移到其 `endTarget`。
8. 本关停用实例上的通用 `CameraFollowArea`，改由 `Level120Ctrl` 执行分段跟随，并继续经过 `CameraFollowRange` 限位。`downwardFollowUnlocked=false` 时目标 Y 恒等于相机开局 Y，X 轴只在角色越出上层水平安全区时跟随；隐藏入口已开、Actor 正在下降且主 Collider 同时进入中央洞口宽度与上层平台厚度时，当帧切换为下层双轴持续跟随，此后 X/Y 目标都直接取 Actor 位置加配置偏移，不再沿用上层 X 安全区。原 Actor 根 Y=`-6.5` 判断继续作为异常漏判兜底。锁 Y 阶段每次弹簧发射前，会根据相机上沿、`0.35` 世界单位安全边距、当前皮肤所有启用 Renderer 的最高点及 Rigidbody2D 实际重力计算安全初速度上限；红/蓝弹簧的设计速度只会被向下限制，防止角色顶部弹出画面。
9. `Ground_mid` 保持共享 Prefab 原本的 `Game / Order 0`，不提升整块地面排序。红弹簧通道自带褐色后层 `Sp_l94_dm_02 / Order 10` 与白色前层 `Sp_l94_dm_01 / Order 14`；控制器只在 Actor 正在下降且主 Collider 开始进入平台厚度时，把 Actor 根 `SortingGroup` 从 `Game / 200` 临时设为 `Game / 12`，形成 `褐色 10 < Actor 12 < 白色 14 < CommonBg 纸纹 20`。恢复条件使用 Actor 全部启用 Renderer 与主 Collider 的最高点，只有整个角色视觉都低于平台底边才恢复原排序，避免角色底部已离开但头顶仍与洞口边缘重叠时提前穿帮。白色前层此前为 Order 201，越过纸纹层后显得偏白；恢复为 Level94 同素材使用的 Order 14 后，纸纹继续覆盖它，洞口与左右地面保持同一叠色。Level120 中唯一的真实 `SpriteMask` 属于停用的备用弹簧充能表现 `L73/ChargeFillMask`，与该色差无关。

## 已执行验证

- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 完整编译：`exit 0 / 0 error`；输出仅包含工程既存警告。
- Prefab 静态校验：788 个对象定义、788 个唯一 fileID、所有本地精确引用均可解析、0 个重复或缺失引用；`BaseLevelCtrl.successAnim` 指向左下门实例的 `SuccessAnimCtrl`，该组件与左下门 Transform 同属 PrefabInstance `2313217612899337903`，不再指向最右门实例 `5221672132798406027`。`Ground_mid` 自带 Collider 与中、右三段边界继续保持连续；洞口褐色/白色 Sliced 层保持原本的本地宽度 `2.11 / 2.24`。
- 渲染静态校验：红弹簧通道褐色后层、Actor 穿洞临时层、白色前层及纸纹层依次为 `Game / 10 / 12 / 14 / 20`；Level94 同素材白色前层同样使用 Order 14。控制器存在“入口已开 + Actor 下落 + Collider 进入平台厚度”三条件切层、Actor 全部启用 Renderer 与 Collider 的最高点越过平台底边后恢复，以及 Reset/Disable 恢复路径。没有修改 `Ground_mid`、`CommonBg`、共享 `Mask` 的材质、颜色或排序。
- 红色机关默认态静态校验：按钮本地 Y=`-1.123`、弹簧本地 Y=`-7.12`、白色底座激活；白色底座不在按钮移动集合中，首次机关序列也已无 `SetActive(false)` 隐藏路径，Reset/Replay 仍显式保持其激活。
- 尖刺静态校验：只有 `SpikeStrip_00` 激活并连续覆盖整个平台，旧分段对象全部关闭；Level120 控制器脚本 GUID 唯一。
- 相机与出屏保护静态校验：未解锁分支直接使用 `cameraInitialPosition.y`，不再含 `Mathf.Max(initialY, actorY)` 的向上跟随；中央洞口水平范围与上层平台厚度同时重叠时立即设置 `downwardFollowUnlocked=true`，`Y=-6.5` 路径保留为兜底。解锁后 `followX` 恒为 true，目标 X/Y 均来自 Actor；Prefab 的 CameraFollowRange 允许相机在下层范围内移动。弹簧发射统一经过 `sqrt(2 * |gravity| * availableRise)` 上限，并把 Actor Collider 与全部启用 Renderer 顶边纳入可用高度计算。

## 未验证与风险

- Unity PlayMode 中的 Transform 缩放压缩手感、红弹簧后续 `16.5 / 1.65 / 0.18 / 0.24` 弹动、白色底座常驻观感、侧向进入后落到按钮顶面的自动补触发、最低点相对初始位置约 `+0.25` 的实际观感、固定 Y 镜头下不同皮肤的顶部留白、动态限速后的前八段可达性与最后一跳不可达、按钮最低点与尖刺的垂直间距、2.8 单位宽物理入口与原宽视觉墙面的组合落点、下层双轴持续跟随节奏与边界、左下门成功动画及最右门完全无响应、`10/12/14/20` 穿洞逐像素遮挡与叠色仍为 `uncertain`，需要完整实走。
- 理论跳距基于当前 Actor `moveSpeed=5`、Rigidbody2D `gravityScale=4.5` 和工程重力 `-9.81`；锁 Y 阶段的垂直初速度会随当前 Renderer 高度动态收紧，若后续修改共享 Actor 物理参数、皮肤高度或相机正交尺寸，需要重新验收各弹簧弹力与横向落点。
- 不同分辨率、移动端持续按键、重开/Replay 以及低帧率下的触发边缘仍为 `uncertain`。
- 蓝色弹簧虽然按物理最高点减半，但滞空时间也会同步降低；前八段的实际落点容差仍需 PlayMode 实走确认。

## 回滚

- 删除 Level120 根节点的 `Level120Ctrl` 组件及本目录脚本，即可撤销本关运行时逻辑。
- 恢复 `Level_120.prefab` 中 9 个蓝色弹簧原始 Sliced 高度、按钮/触发器位置与尖刺宽度，移除 `HiddenEntranceBlocker / Ground_mid_RightCollision`，并恢复 `Ground_mid` 自带 Collider 的原 offset/size，即可撤销静态场景改动；移除 Actor 穿洞临时 SortingGroup 切换即可撤销遮挡修正。若仅回滚门修正，将 `BaseLevelCtrl.successAnim` 重新指向原最右门组件并删除左下门的 stripped `SuccessAnimCtrl` 引用即可。
- 未修改共享 Actor、门、相机、弹簧或尖刺 Prefab 源资源。
