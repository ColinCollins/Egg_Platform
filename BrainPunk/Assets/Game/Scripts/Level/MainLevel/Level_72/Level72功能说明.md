# Level72 plane 拖动旋转功能说明

## 需求与背景

- 玩家用手在 `plane` 的碰撞区域开始拖动时，整个 `RotateGroup` 跟随手势旋转。
- 旋转能力必须限制在 Level 72，不修改共享输入、角色、关卡基类或其他关卡脚本。

## 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_72/Level72Ctrl.cs`：新增本关专用输入与旋转控制器。
- `Assets/Game/Prefabs/Level/Level_072.prefab`：增挂 `Level72Ctrl`，让 Actor 保持在关卡根节点，并补齐地刺死亡处理器、5 个触发事件和既有击中特效引用。
- `Assets/Game/Scripts/Level/LevelItem/Level_72/Level72功能说明.md`、`Level72风险评估.md`：本地功能与风险记录。
- 只读取现有 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent` 和 `GameResetEvent`；未修改事件定义或派发方。

## 原有行为基线

- 当前 prefab 已由策划创建 `RotateGroup`，并把本关世界对象重挂到该节点下；相机与根控制组件仍在组外。
- `plane` 是 `RotateGroup` 的子节点，下面有 3 个 `Ground_long` 和 2 个 `Ground_short`，但原先没有手势旋转脚本。
- 上述 5 个 Ground 根节点各有一个启用中的实体 `BoxCollider2D`，原先只参与地形物理。
- 首版绑定曾把 Actor 放入 `RotateGroup`，导致角色随场景旋转；现已恢复为关卡根节点的直接子对象。
- 5 个 `Caltrop_long` 原有 Trigger Collider 均有效，但新增的 `OnTrigger2DHandle.onEnter` 目标为空，因此角色落到刺上不会进入死亡流程。

## 新行为

- `Awake()` 缓存 `plane` 子层级当前 5 个 `Collider2D`；它们的并集就是开始旋转的输入区域，没有新增一块猜测尺寸的隐形碰撞盒。
- 按下时先排除 UI，再用每个碰撞盒的 `OverlapPoint` 判断是否位于 plane 区域；区域外按下不会开始旋转。
- 一次拖动只认领最先有效的 `fingerId`，第二根手指不能接管；Editor 无触摸时使用鼠标路径。
- 开始后持续跟踪 owner 到松开或取消，不要求指针继续停留在原碰撞盒内，避免场景旋转后输入区移走导致中断。
- 每帧计算指针绕 `RotateGroup.position` 的屏幕角度增量，并用 `Mathf.DeltaAngle` 旋转 `RotateGroup`；支持顺逆时针、跨越正负 180 度和连续多圈。
- 旋转中心 12 像素内暂停角度计算，离开中心后重新建立基线，避免中心附近方向抖动。
- 暂停、非 Playing 状态、失焦、切后台、组件禁用、触摸丢失时立即结束拖动；失败或手动重置时先恢复 prefab 初始角度，随后仍由原框架重建整关。
- Actor 保持在 Level 72 根节点，不随 `RotateGroup` 旋转；地形、门和陷阱仍随旋转组运动。
- 5 个地刺统一调用复用的 `TrapEventHandlle.PlayKnock`：定位并播放 `Knock_Explosion`、调用 `ActorCtrl.Die()`、施加力度 10 的世界向上击退、播放陷阱音效，并在既有延迟后派发失败事件。默认地刺 handler 仍保持移除，避免双触发。

## 配置与数据边界

- Prefab 绑定：`rotateGroup=RotateGroup`、`plane=plane`、`inputCamera=GameCamera`。
- 默认手感：`sensitivity=1`、`invertRotation=false`、`centerDeadZonePixels=12`。
- 不修改 `BaseLevelCtrl`、`SpriteDragRotate`、`DragableItem`、`ActorCtrl`、Actor prefab 或共享 `TrapEventHandlle` 源码；只调整 Level 72 实例层级与事件绑定。
- 死亡表现复用既有 `Knock_Explosion` prefab，未新增或修改共享资源文件和资源地址。
- 不新增 DB/save、Luban、PlayerPrefs、RemoteConfig、资源地址、UI prefab、analytics、广告、IAP 或平台 SDK 流程。

## 验证记录

- Prefab 静态审计确认脚本 GUID、组件 fileID 及三条引用闭合且唯一；原暂存和未暂存的策划改动均保留。
- 静态审计确认 `plane` 当前恰好缓存 5 个启用中的 Ground `BoxCollider2D`，没有修改它们的尺寸、Trigger 状态或物理层。
- Prefab 的 186 个 YAML header fileID 全部唯一；Actor instance 与根 children 双向一致，`RotateGroup` 中不含 Actor，起始门等其他实例仍保持原旋转组归属。
- 5/5 个地刺事件均指向根级 `TrapEventHandlle.PlayKnock`；handler 的 Actor 和 ParticleSystem 引用均闭合，5 个 Trigger Collider 保留，默认 handler 仍被移除。
- 输入代码复核覆盖 UI 排除、单指 owner、触摸丢失、暂停/恢复、中心死区和跨 180 度增量。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/.rsp2` 并追加 `Level72Ctrl.cs` 后编译通过，退出码为 0、0 errors；临时程序集可检出 `Level72Ctrl`。
- 已跟踪差异执行 `git diff --check` 通过，仅有当前工作区 LF/CRLF 转换提示；尚未跟踪的 `Level_72` 目录 6 个新文件已另行扫描，未发现尾随空格。Unity Editor 正式导入/程序集重建、Inspector 和 PlayMode 仍待执行。

## 手动验收清单

- 只在 5 段 plane 地形碰撞盒内按下才能旋转；段间空白、其他场景物体和 UI 上按下不触发。
- 顺/逆时针拖动方向自然，跨正负 180 度及连续绕圈无跳变；松手后立即停止。
- 第二根手指不接管；Cancel、切后台、暂停和失焦后旧手指不残留。
- `RotateGroup` 下地形、门和陷阱整体同步旋转；Actor、相机与 HUD 不旋转。
- Actor 站立、行走、跳跃时保持自身朝向；旋转地形扫过或承托 Actor 时无明显抖动、穿透或门/失败区误触发。
- 逐个踩中 5 个地刺都能触发死亡、粒子、音效、击退和失败流程；同时接触多根刺时只结算一次，失败重开后仍可再次触发。
- 死亡、手动重置、退出重进后恢复 prefab 初始角度。
- Android/iOS 横屏验证触控、帧稳定性和后台恢复。

## 剩余风险与回滚

- Unity PlayMode、Inspector 和 Android/iOS 尚未执行；真实物理、地刺死亡链与触摸手感仍需按清单验收。
- `plane` 以后新增的任意子 `Collider2D` 也会被缓存为输入区域，层级调整后需要重新核对范围。
- 完整回滚需从 `Level_072.prefab` 移除 `Level72Ctrl`、根级 `TrapEventHandlle` 和 `Knock_Explosion` 实例，成套还原 5 个地刺 UnityEvent 与 Actor 层级，再删除 `Level_72` 目录及本地文档；无需修改共享代码、配置或资源 prefab。
