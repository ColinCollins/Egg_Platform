# IWannerGame_GravityCtrl 功能与风险记录

日期：2026-08-24

## 需求与原始基线

- 需求：把原 `ILevelGravityCtrl` 与独立 AreaEffector2D Jump 开关能力统一为
  `IWannerGame_GravityCtrl`，由 `AreaEffector2D` 提供区域重力，并提供 Force Angle 与开关 API。
- Jump 反转必须同时更新 Actor 根节点 `localScale.y`、Ground 检测方向和最大坠落速度方向；Actor 未完成
  “离开旧承载面并落到新承载面”的周期前不得再次反转。
- 原实现直接反转 `Rigidbody2D.gravityScale`；独立 `IWannarGame_SwitchAreaEffect2DCtrl` 只切换
  `AreaEffector2D.enabled`，两者同时使用会形成两套重力来源且没有统一 Ground 方向。

## 影响文件、资源与系统

- `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_GravityCtrl.cs`
- `Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs`
- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel058Ctrl.cs`
- `Assets/Game/Prefabs/IWannerGameItems/IWannerGame_AreaEffect2D.prefab`（只读取现有配置，本次未修改）
- `Assets/Game/Prefabs/WannerLevel/ILevel_057.prefab`、`ILevel_059.prefab`（只做静态引用检查，本次未修改）
- `Assets/Game/Prefabs/WannerLevel/ILevel_058.prefab`（为 058 局部控制器序列化 Force Magnitude）
- 不涉及 DB/Save、配置、资源加载、UI、打点、广告、平台 SDK 或新增全局事件。

## 修改后行为与事件流

`PlayerJumpEvent` → `IWannerGame_GravityCtrl.OnPlayerJump()` → 检查 AreaEffector2D 已开启、Actor 存活且已落地
→ `forceAngle += 180` → Actor `localScale.y *= -1` → 同步世界重力/Ground 检测方向
→ 等待 Actor 先离开旧承载面、再接触新承载面后开放下一次反转。

- `AreaEffector2D.forceAngle` 是本控制器同步 Ground 检测方向的来源；`useGlobalAngle=false` 时会叠加 Effector Transform 旋转，
  `forceMagnitude<0` 时实际方向反向。
- 控制器不读取或修改 Actor `Rigidbody2D.gravityScale`；Actor 原有 Unity 重力与 AreaEffector2D 区域力会按 Physics2D 规则叠加。
- `SetForceAngle(float)` 把角度归一化到 0～360；Effector 开启时立即同步 Actor Ground 方向，关闭时只保存角度。
- `SetForceMagnitude(float)` 直接写入绑定 Effector 的 Force Magnitude，保留 Unity 对负值反转施力方向的原始语义；
  不修改角度、开关或 Actor 自身重力。Effector 已开启时会按当前角度与新力值重新同步 Ground 方向。
- `SetGravityEnabled(bool)` 与 `ToggleGravityEnabled()` 只切换绑定 Effector，不修改 Actor 自身 `gravityScale`；
  Ground 检测保留最后方向，重新开启时按当前 Force Angle 同步。
- 控制器现在序列化保存 `forceAngle` 与 `isGravityEnabled`；`OnEnable()` 找到目标后立即把两项写入
  `AreaEffector2D.forceAngle/enabled`。Jump 与公开 API 同时更新控制器状态和 Effector，不再只依赖目标组件原值。
- `Actor_IWanner.SetGroundCheckDirection(Vector2)` 保存世界方向；共享 Ground BoxCast 与 Gizmo 继续读取
  `GroundCheckDirection`。最大坠落速度改为限制刚体沿该方向的速度分量，不再只判断 Y 或 gravityScale。
- 第 058 关 API 先调用 `SetGravityEnabled(false)` 进入等待 Jump 阶段；有效 Jump 的同步事件回调全部结束后，
  058 通过 `SetForceMagnitude`、`SetForceAngle(0)` 与 `SetGravityEnabled(true)` 开启水平区域力。
- 原控制器脚本 `.meta` GUID `d10426f3a4849554b934796731c4141d` 随新文件保留，现有 prefab 组件引用不会因改名丢失。
- 无 prefab 引用的 `IWannarGame_SwitchAreaEffect2DCtrl` 及其独立记录已删除，避免同一 Jump 重复控制。

## Prefab 静态证据

- `ILevel_057.prefab`、`ILevel_058.prefab`、`ILevel_059.prefab` 当前均引用保留的控制器 GUID。
- `ILevel_059.prefab` 子层级存在 `AreaEffector2D`，控制器已显式绑定其 fileID，并保存
  `forceAngle=0`、`isGravityEnabled=true`；目标静态 Force Magnitude 为 `10`。
- `ILevel_058.prefab` 已实例化 `IWannerGame_AreaEffect2D.prefab` 并把 stripped AreaEffector2D 绑定到控制器；
  Force Magnitude override 为 150，`ILevel058Ctrl.forceMagnitude` 当前也配置为 150。
- `ILevel_057.prefab` 当前 YAML 中未找到可解析的 AreaEffector2D 或区域重力 prefab 引用；
  控制器会记录 Bear Warning，运行表现标记为 `uncertain`，需在 Unity 中补齐目标后验证。
- `ILevel_056.prefab` 引用了 `IWannerGame_AreaEffect2D.prefab`，但当前未引用重力控制器 GUID；是否需要挂载由关卡配置决定。

## 验证

- CodeGraph 已确认旧控制器只有 `ILevel058Ctrl` 一个代码类型引用；该引用已同步为新类型。
- 静态确认控制器只使用现有 `PlayerJumpEvent` 与 Bear `EventSubscriber`，并在 `OnDisable()` 释放订阅。
- 静态确认新公开 API 与序列化字段均带中文用途、调用边界和注意事项注释。
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` 通过：
  `0 error / 106 warning`；warning 为工程既有诊断，目标脚本没有新增编译错误。
- 定向静态扫描与 `git diff --check` 通过；目标脚本没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、
  `ExecuteInEditMode`、DB/Save、实例化或 Unity `Debug.Log` 路径。
- Unity PlayMode 与 Android/iOS 真机验证尚未执行。

## 剩余风险与未验证缺口

- `uncertain`：尚未在 Unity PlayMode 验证 059 的区域覆盖、Collider Mask、Force Magnitude、Drag、实际 Force Angle、
  连续 Jump、碰顶/落地、死亡、Reset 与关卡退出。
- `uncertain`：057/058 缺少可解析 AreaEffector2D，未补齐 prefab 绑定前不会产生新区域重力。
- 共享基础跳跃仍写入正 Y 速度；任意角度重力下的跳跃冲量没有在本次需求中方向化，90/270 以外角度的实际手感待验证。
- 单向平台补充探测和移动平台速度合成仍主要按上下重力设计；任意水平/斜向 Force Angle 下只有共享 Ground BoxCast
  与最大坠落速度方向已同步，其余承载表现为 `uncertain`。
- `AreaEffector2D` 只在 Collider 区域内施力；Actor 离开区域后继续保留并使用自身原有 `gravityScale`。
- `uncertain`：当 Actor 自身重力方向与 AreaEffector2D Force Angle 不一致时，实际合力方向可能不同于 Ground 检测方向；
  调用方需要按关卡参数保证两者组合符合预期。
- Android/iOS 横屏触控、Physics2D 稳定性与 60 fps 表现尚未验证。

## 回滚路径

- 将保留 GUID 的脚本恢复为 `ILevelGravityCtrl.cs` 与原类名，并恢复直接反转 `gravityScale` 的实现，可回退旧重力链。
- 删除 `Actor_IWanner.SetGroundCheckDirection(Vector2)`，把最大坠落速度恢复为按 `gravityScale` 判断 Y 上下限，可撤销方向化限速。
- 将 `ILevel058Ctrl` 恢复为直接禁用旧组件并自行把 `gravityScale` 设为 0，可回退 058 阶段切换。
- 如需恢复独立开关，再恢复 `IWannarGame_SwitchAreaEffect2DCtrl` 脚本与原 meta；当前静态证据没有 prefab 引用需要迁移。
