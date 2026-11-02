# DLevel_011 陡坡移动限制功能与风险评估

## 1. 需求与背景

- 需求：DLevel_011 的 Actor 不应沿玩家画出的高倾角线段持续向上攀爬。
- 原表现：首笔完成后，`DLevel001Ctrl` 持续调用 `SetMoveInput(1f)` 与 `ActorCtrl.OnUpdate()`；共享
  `ActorCtrl.HandleMovement()` 每帧直接写入水平速度，落地检测只判断 Ground Mask 是否命中，不读取
  接触法线或坡度，因此 Physics2D 会在斜面接触约束下把持续水平驱动转换为沿斜面移动。
- 设计边界：按项目单关 Actor 规则新增 `DLevel11ActorCtrl : ActorCtrl`，不修改共享 `ActorCtrl`。

## 2. 影响文件与资源

| 文件/资源 | 变化 |
| --- | --- |
| `Level11/DLevel11ActorCtrl.cs` | 新增本关 Actor 子类和 Draw 层坡度检测；超限时保留行走动画但冻结水平驱动 |
| `DLevel_011.prefab` | Actor 实例从共享 `ActorCtrl` 组件替换为本关子类，最大坡度配置为 50°，检测层为 Draw |
| `DLevel_011脚本结构记录.md` | 补充结构、生命周期、序列化和回滚边界 |
| DrawGame 中央演进记录、已有功能记录 | 记录可复用事实和暂不上提共享的决策 |

未修改共享 Actor prefab、`ActorCtrl.cs`、画线 prefab、Physics2D Layer Matrix、物理材质、DB/Luban、
存档、UI、全局事件、analytics、SDK 或移动端平台配置。

## 3. 当前实现与调用流

1. `DLevel001Ctrl` 仍按既有逻辑给 Actor 写入移动输入并调用虚方法 `OnUpdate()`。
2. `DLevel11ActorCtrl` 使用 Actor 现有 `Rigidbody2D.GetContacts()` 获取最多 8 个 Draw 层非 Trigger 接触点。
3. 接触法线与 `Vector2.up` 的夹角作为坡度；只接受朝向 Actor 的上方支撑法线，排除天花板和纯侧墙。
4. 当坡度大于 50°，且 X 输入与坡面上升方向一致时，临时将 `MoveSpeed` 设为 0，不清除 X 输入。
5. 调用 `base.OnUpdate()`；共享动画仍把角色视为移动态并继续播放行走动画，但 Rigidbody2D 水平驱动为 0。
6. 基类调用结束后用 `finally` 恢复原 MoveSpeed，避免异常或后续帧污染通用速度。

检测数组和 `ContactFilter2D` 均复用，不在逐帧路径创建集合或产生可见托管分配。向下坡方向不拦截，
角色仍可在重力或反向移动下离开陡坡。

## 4. 事实

- `DLevel001Ctrl` 的 Actor 字段已绑定新子类组件；子类继承 `ActorCtrl`，原字段类型兼容。
- 本关原 Actor 序列化值已迁移：`moveSpeed=3`、`WallCheckDistance=0`、`whatIsWall=0`、
  `whatIsGround=16712`，并保留 Spine 与 PushCollider 引用。
- `slopeCheckLayers=16384`，只包含 Layer 14 Draw；最大可行走坡度为 50°。
- Unity 6000.0.59f2 Roslyn 全量 Assembly-CSharp 编译为 0 error；Unity PrefabImporter 已成功导入
  `DLevel_011.prefab`，日志中没有本关反序列化或 Missing Script 错误。

## 5. 直接结论

- 该限制仅作用于 DLevel_011 的 Actor，不会改变其它关卡的坡面、墙体或共享移动行为。
- 超过阈值时只把本帧上坡水平驱动速度置 0；移动输入不变，所以行走动画继续播放。实现不会清零 Y 速度，
  不会禁用刚体，也不会把角色设为 Kinematic。
- 普通 Ground/Stuff 接触不参与本限制；只有 Draw 层线段能触发角度判断。
- 该方案不依赖额外 Collider、Raycast、全局摩擦或 PhysicsMaterial2D 参数。

## 6. 假设与待验证项

- `uncertain`：正式 PlayMode 中 50° 是否符合目标手感；必要时只调整 prefab 的
  `maxWalkableSlopeAngle`，建议先在 45° 到 55° 范围试玩。
- `uncertain`：接触数据来自已完成的物理步，Actor 首次撞上陡线时可能先移动一个物理步再被限制。
- `uncertain`：折线尖角同时产生多个法线时采用“任一超限且为上坡即阻止”的策略，是否会在临界处显得偏严格。
- `uncertain`：低帧率、暂停恢复、失败/Reset/Replay、Android/iOS 横屏设备上的 Physics2D 接触顺序。

## 7. 风险项

| 风险 | 等级 | 影响 | 缓解/观察点 |
| --- | --- | --- | --- |
| 阈值过小 | 中 | 合理解法也无法上坡 | Inspector 局部调高，不改代码 |
| 阈值过大 | 中 | 仍可攀爬接近竖直线 | Inspector 局部调低，并验证关卡可解性 |
| 首次接触有一个物理步延迟 | 低 | 高速撞线时可能短暂抬升 | PlayMode 验证；若可见再评估前向 Cast，不提前扩展 |
| 多接触点法线抖动 | 中 | 临界坡面输入可能断续 | 先验证真实线段；证据不足时不增加滞回或缓存状态 |
| Prefab 后续误换回共享组件 | 中 | 坡度限制完全失效 | 结构记录明确 Actor 子类和两个序列化字段 |

## 8. 验证证据

- Unity Roslyn Assembly-CSharp 全量编译：0 error；输出仅含项目既有 warning。
- Unity Editor 自动导入：`DLevel_011.prefab` PrefabImporter 成功，无 Missing Script 或反序列化错误。
- 静态序列化核对：根控制器和 ActorSkinBinder 都指向新子类；原共享 ActorCtrl 已列入 prefab
  `m_RemovedComponents`，新组件列入 `m_AddedComponents`。
- 静态性能核对：固定长度接触数组、缓存 ContactFilter，无 LINQ、Instantiate、资源加载或全局查找。
- 未执行 Unity PlayMode、设备构建或 Android/iOS 真机测试。

## 9. 回滚路径

1. 在 `DLevel_011.prefab` 的 Actor 实例移除 `DLevel11ActorCtrl` added component，并取消对共享
   `ActorCtrl` 的 removed component。
2. 将根 `DLevel001Ctrl.actor` 与 ActorSkinBinder 的引用恢复到共享 ActorCtrl 实例。
3. 恢复本关原有 ActorCtrl override（moveSpeed、墙距和 LayerMask），删除
   `DLevel11ActorCtrl.cs` 与 meta。

回滚无需修改共享 Actor prefab、其它 Draw 关卡、配置、存档或资源。
