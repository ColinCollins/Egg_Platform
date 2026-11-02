# DLevel_011 脚本结构记录

## 1. 基本信息

- 关卡：DLevel_011
- 记录日期：2026-08-03
- 交付状态：脚本、Prefab 绑定和静态编译已完成；PlayMode 与移动端验收待执行
- 参考基线：`PressButtonBase`、`TurntableButton`、`WaterSplashCtrl`、`Level66ActorCtrl`
- 选择理由：本关按钮原本已使用 `TurntableButton : PressButtonBase`；现有 WaterSplash 状态机和
  `WaterSplashCtrl` 已定义 `long` / `stay_long` 的长水柱进入、保持与收起语义
- 主要脚本：`DLevel011Ctrl.cs`、`TagPressButton.cs`、`DLevel11ActorCtrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_011.prefab`

## 2. 脚本与组件树

```text
DLevel_011
├─ DrawLevelCtrl                         # 共享 Draw 关卡生命周期
├─ DrawLineFeature                      # 共享画线与提示对象控制
├─ DLevel001Ctrl                        # 既有首笔后 Actor 移动逻辑
├─ DLevel011Ctrl                        # 本关按钮与水花状态桥接
├─ Actor
│  └─ DLevel11ActorCtrl                 # 本关 Draw 层最大可行走坡度限制
└─ Level11
   ├─ Button
   │  └─ Trigger
   │     └─ TagPressButton              # 按 Tag 过滤并维护按钮占用状态
   └─ WaterSplash
      └─ Animator                       # WaterSplash.controller
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DLevel011Ctrl` | 本关控制器 | 订阅按钮按下/释放，驱动 `long` 与 `stay_long` 参数，禁用时解除回调并关闭保持态 | `MonoBehaviour`、`IDebuger` | 不处理物理接触、画线、胜负、存档或资源加载 |
| `TagPressButton` | 本关按钮组件 | 只接受配置 Tag；多 Collider 占用时等最后一个离开才释放 | `PressButtonBase` | 不知道 WaterSplash、关卡结果或 UI |
| `DLevel11ActorCtrl` | 本关 Actor 子类 | 从 Rigidbody2D 接触点读取 Draw 层支撑法线；超过配置角度且输入方向向上坡时仅让本帧实际速度为 0 | `ActorCtrl` | 不修改共享重力、画线碰撞或其它关卡 Actor |
| `PressButtonBase` | 共享基类 | 缓动按钮 Transform/Collider、音效和 Action 回调 | `MonoBehaviour`、`IDebuger` | 不支持可配置 Tag；本次未修改 |
| `Animator` | 既有资源组件 | 执行 `water_2`、保持水花和 `water_2_hide` 状态 | Unity Animator | 不直接判断按钮碰撞 |

## 4. 生命周期与关键调用

1. PlayMode 启用 `DLevel011Ctrl` 时校验按钮和 Animator 显式引用，并通过 `SetEnter/SetExit` 绑定回调。
2. 指定 Tag 的第一个 Collider 进入 `TagPressButton` Trigger，按钮调用 `SetPressed(true)`，执行基类
   下压缓动和音效，并通知 `DLevel011Ctrl`。
3. `DLevel011Ctrl` 先设置 `stay_long=true`，再触发 `long`，沿既有 Controller 进入
   `water_2 -> water_2_splash_idle`。
4. 同一目标的额外 Collider 进入只记录占用，不重复触发按钮和水花。
5. 最后一个已记录 Collider 离开后，按钮释放并回调控制器；`stay_long=false` 令状态机进入
   `water_2_hide` 后退出。
6. 按钮单独禁用时清空 Collider 集合、即时复位并通知释放；关卡禁用时控制器解除回调、清除 Trigger
   并关闭 `stay_long`，避免 Replay/退出残留。
7. `DLevel001Ctrl` 在首笔完成后仍按既有路径设置 Actor 水平输入并调用 `OnUpdate()`。
8. `DLevel11ActorCtrl.OnUpdate()` 先读取当前 Rigidbody2D 的 Draw 层接触点，把接触法线与世界向上夹角
   作为坡度；仅当坡度大于 50° 且当前输入朝上坡方向时，临时把本帧 `MoveSpeed` 设为 0。
9. 随后继续调用 `ActorCtrl.OnUpdate()`；输入保持为移动态，因此行走动画继续播放，但 Rigidbody2D 的
   水平驱动速度为 0。基类调用后立即恢复原 MoveSpeed，重力、下落和向下坡移动不受影响。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 当前绑定 | 缺失时行为 |
| --- | --- | --- | --- |
| DLevel_011 根 | `DLevel011Ctrl.tagPressButton` | Button/Trigger 的组件 `{fileID: 3946344186666221765}` | Bear Logger 报错，不注册按钮回调 |
| DLevel_011 根 | `DLevel011Ctrl.waterSplashAnimator` | WaterSplash Animator `{fileID: 2509047369608542369}` | Bear Logger 报错，不注册按钮回调 |
| Button/Trigger | `TagPressButton.targetTag` | `tools` | 与画线模板的实际 Tag 一致；Draw 仍只是物理层名 |
| Actor | `DLevel11ActorCtrl.maxWalkableSlopeAngle` | `50` | 值越小限制越严格；范围 0° 到 89° |
| Actor | `DLevel11ActorCtrl.slopeCheckLayers` | Draw 层（bit 14） | 只限制画线，不改变 Ground、Stuff 或机关表面的通用行为 |
| TagPressButton 基类字段 | button / collider | 按钮视觉 Transform / Button 实体 Collider | 缺失项不移动对应视觉或 Collider |
| WaterSplash Animator | Controller | `WaterSplash.controller` | 必须包含 `long` Trigger、`stay_long` Bool 和 water_2 状态链 |

Prefab 原 `TurntableButton` 组件原位替换为 `TagPressButton`，保留同一组件 fileID、按钮 Transform、
Collider、`offsetY=0.2` 和 `tweenDuration=0.1`，没有修改共享按钮或动画资源。

## 6. 数据、UI、资源与事件流

- DB/存档/Luban：无读取、写入或字段变化。
- UI：不修改 `GamePlayPanel` 或全局弹窗。
- 资源：复用 Prefab 已绑定的 WaterSplash Animator Controller；无运行时加载或实例化。
- 事件：只用 `PressButtonBase` 的局部 Action 回调；无 `PlayEvents`、Bear 全局事件或跨关订阅。
- Analytics/SDK：无。
- 性能：碰撞入口只维护少量 Collider 引用；水花播放由 Animator 驱动，无新增 Update/扫描/GC 热点。

## 7. 相对参考基线的差异与结构复盘

| 差异 | 原因 | 影响 | 结论 |
| --- | --- | --- | --- |
| `TagPressButton` 将 Player 硬编码改为序列化 Tag | 本关需求明确要求指定 Tag 可触发 | 当前配置为 `tools`，与画线模板 Tag 一致 | 本关局部复用候选，不修改共享基类 |
| 用 `DLevel11ActorCtrl` 替换本关 Actor 上的共享组件实例 | 最大坡度只属于本关玩法，不能污染已验收关卡 | 继续继承通用 Actor，陡坡时保留移动输入和动画，只冻结本帧水平驱动 | 符合 Actor 单关能力放入子类的项目规则 |
| 复用 Rigidbody2D 接触点而非追加射线或全局物理参数 | 当前问题由既有碰撞接触与强制水平速度共同造成 | 固定 8 个接触点数组，无逐帧分配；不修改 Physics2D Matrix | 保持局部最小实现 |
| 用 HashSet 维护多 Collider | Actor 可能有多个 Collider，单一 Exit 不应提前释放 | 避免按钮和水花闪断 | 局部稳定性改进 |
| 水花桥接留在 `DLevel011Ctrl` | Tag 按钮不应依赖某个具体 Animator | 按钮可复用，关卡表现不污染共享类 | 保持局部职责分离 |
| Prefab 使用显式引用 | 避免层级搜索误绑定其它 Animator | 缺失时显式报错并停止注册 | 本关必要约束 |

结构复盘决定：当前只有 DLevel_011 使用可配置 Tag 的 `PressButtonBase` 子类，不修改共享
`PressButtonBase`，也不将 WaterSplash 逻辑上提。后续若多个关卡出现相同 Tag 按钮且完成回归，
再评估迁移到共享目录。

## 8. 验证证据与缺口

- 静态编译：Unity 自动导入后，`Assembly-CSharp.rsp` 已包含两个本关脚本；使用 Unity 6000.0.59f2
  Roslyn 执行全量编译，结果 `0 error / 104 warning`，warning 均为项目既有项。
- 静态扫描：本关新增代码没有 `OnValidate`、`Reset`、`ExecuteAlways`、DB、存档、全局事件或直接
  `Debug.Log`；运行时入口有 `Application.isPlaying` 外层门禁。
- Prefab：根控制器绑定 TagPressButton 与 WaterSplash Animator；按钮组件保留原有基类字段并配置
  `targetTag=tools`；Actor 的共享 `ActorCtrl` 实例组件已替换为 `DLevel11ActorCtrl`，并保留本关
  `moveSpeed=3`、地面/墙体 Mask、Spine 和 PushCollider 引用；脚本 GUID 均有对应 meta。
- Unity 导入：Editor.log 记录 DLevel_011 PrefabImporter 成功完成，没有本关脚本编译错误。
- `git diff --check`：目标脚本和 Prefab 通过。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 陡坡 PlayMode：未执行；50° 边界、折线尖角、多接触点和不同帧率下的实际手感仍需验证。
- `uncertain`：按钮快速进出时水花过渡手感、多 Collider 实际组合、暂停/失败/Replay、完整关卡可解性、
  Android/iOS 横屏 60 fps 与低帧率触发顺序。

## 9. 风险与回滚

- 主要风险：Animator 参数或 Controller 被后续美术修改会令水花不响应；目标 Tag 配错会令按钮无法触发；
  尚未通过 PlayMode 观察快速释放时 `water_2_hide` 的视觉衔接；坡度检测基于上一物理步已生成的接触，
  首次接触陡线可能存在最多一个物理步的限制延迟。
- 回滚路径：从根节点移除 `DLevel011Ctrl` 组件，把 Trigger 组件脚本 GUID 恢复为原
  `TurntableButton` GUID `458e7f1a77dc400448fde8718ca1ad2b`，将 `DLevel011Ctrl.cs` 恢复为空壳基线，
  并删除 `TagPressButton.cs` 及其 meta。
  共享 `PressButtonBase`、WaterSplash Controller、Draw 系统、配置和存档均无需回滚。

坡度功能单独回滚时：把 Actor prefab 实例上的 `DLevel11ActorCtrl` 恢复为共享 `ActorCtrl`，恢复根
`DLevel001Ctrl.actor` 与 ActorSkinBinder 的旧组件引用，并删除 `DLevel11ActorCtrl.cs` 及 meta；不需要
修改共享 Actor prefab、Physics2D Matrix 或其它 Draw 关卡。

## 10. 既有多 TipsObject 记录边界

本关此前存在独立的多 TipsObject 工作区改动与文档。本次没有修改其控制代码、对象引用或展示流程；
相关最终结构存在并发工作区变更，仍以对应专项文档和最终 Unity Prefab 导入结果为准，不把它并入
按钮/水花行为结论。

## 11. 2026-08-03 陡坡限制补充

- 原行为基线：首笔后 `DLevel001Ctrl` 每帧写入正向移动输入；共享 `ActorCtrl.HandleMovement()` 将
  Rigidbody2D 的 X 速度持续写为 `moveSpeed`，而落地判断只检查向下 BoxCast 是否命中，不限制坡角。
- 当前行为：本关 Actor 使用 `DLevel11ActorCtrl`。Draw 层支撑面超过 50° 时只阻止朝上坡方向的水平驱动；
  移动输入仍保留，所以共享行走动画继续播放，同时保留重力、Y 速度和下坡移动。
- 局部边界：未修改共享 `ActorCtrl`、Actor prefab、画线生成器、碰撞矩阵、物理材质或其它关卡。
- 详细事实、风险、验证缺口与回滚见 `DLevel_011陡坡移动限制功能与风险评估.md`。
