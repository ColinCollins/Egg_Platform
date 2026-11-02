# DLevel_025 柱门平衡物理功能与风险评估

## 1. 最终需求口径

- 门和石柱在玩家画线前不能自行运动，以保留编辑器摆好的平衡初态。
- 玩家完成任意一笔有效画线后，门和石柱必须立即解锁为 Dynamic；不要求线碰到门或石柱。
- 解锁只开启真实 Physics2D，不由脚本 AddForce/AddTorque，也不持续改写速度。
- 成功门 Trigger 的完整区域不允许落笔或穿线；玩家仍可通过该 Trigger 触发成功。

此前将“画了线就唤醒”误当作缺陷并改成接触后才释放，与用户最终确认的玩法相反；该接触门禁及运行时 Relay 已全部移除。

## 2. 开场锁定与画线释放

### 2.1 锁定阶段

- 门和石柱 Prefab、Awake 与 Attempt Reset 均保持 `Kinematic + simulated`。
- 归位先短暂 `simulated=false`，清速度和恢复局部位姿后再恢复 Kinematic 模拟，避免 Reset 中间态参与求解。
- 控制器不覆盖 mass、damping、gravityScale、材质、插值或连续检测；Rigidbody2D Inspector 是唯一参数来源。

### 2.2 有效笔画完成

- `DLevel25Ctrl` 实现既有 `IDrawStrokeCompletedHandler`。
- `OnDrawStrokeCompleted()` 对任意有效笔画直接调用 `ReleaseMechanism()`，不检查笔数、位置、长度以外的附加条件或 Collision。
- 门和石柱同步切 Dynamic、清除锁定阶段残速并 WakeUp，随后完全交给 Physics2D。
- `_isReleased` 使同一次 Attempt 只释放一次，避免第二笔再次清空正在运动的速度。
- Reset 后 `_isReleased=false`，两体归位并恢复 Kinematic，下次有效笔画可再次解锁。

## 3. Trigger 禁画与玩家隔离

- DrawLineFeature 的起笔与续画查询使用 `Physics2D.defaultPhysicsScene + ContactFilter2D`，显式设置 `useTriggers=true` 和原非 Draw LayerMask。
- 门成功 Trigger 保持 Layer 3、size `(2.5446355,3.045598)` 与原成功回调，因此完整 Trigger footprint 会阻止落笔与 CircleCast 穿线。
- 门的非 Trigger 实体碰撞由旧矩形改为 12 点 Polygon；成功 Trigger 尺寸不变，两者职责继续分离。
- 门实体 Polygon 仅排除 Actor bit 2048，玩家不被门实体推挡；成功 Trigger 的排除仍为 0，所以继续触发进门成功。
- Draw Layer 14 仍被阻挡 Mask 排除，明确设计成可绘制的 Draw Trigger 保持可画。

## 4. 参数与几何

- 门 mass 100、柱 mass 50 是当前 Inspector 值；控制器不覆盖。
- 画线最大质量约 20。质量不会改变重力加速度，但会影响碰撞传力；当前质量仍需按 PlayMode 手感确认。
- 旧门实体 Box 为 `(2.3,2.55)`、offset `(0,-0.104367256)`；门框中下段视觉明显收窄，旧盒每侧约多出 `0.1～0.22` 世界单位的透明碰撞区。
- 新门实体 Polygon 共 12 点，局部 bounds 为 `x[-1.18,1.18]`、`y[-1.37,1.17]`，顺时针且无自交；门实体底约 y=2.004。
- 当前柱使用 BoxCollider2D，size `(1.0939388,5.4596634)`、offset `(0,0.002503872)`，世界顶约 y=1.962336；两者非穿透缝约 0.041664。
- 门/柱实体 Collider 使用 friction 0.8、bounce 0 的 `DrawStrokeStable`。

## 5. 风险矩阵

| 风险 | 影响 | 当前控制 | 待验证 |
| --- | --- | --- | --- |
| 开场自行倒塌 | 玩家还没画就散架 | Prefab/Awake 为 Kinematic | 入场演出、等待 10 秒 |
| 画线后没有解锁 | 玩法无法开始 | 任意有效 StrokeCompleted 直接释放 | 远处短线/长线/多笔 |
| 解锁瞬间凭空弹开 | 未碰线也突然散架 | 非穿透几何、零残速、低弹高摩擦材质 | 首个 Dynamic 物理步 |
| 第二笔清空运动速度 | 机关动作突兀 | `_isReleased` 单次锁存 | 多笔连续绘制 |
| Trigger 仍漏画 | 成功门内部出现线 | 显式 `useTriggers=true` | 门中心、边缘、快速穿线 |
| 共享 Trigger 查询改变其它关 | 非 Draw 逻辑 Trigger 开始阻挡 | 项目原配置本就为 true；Draw Layer Trigger 仍被排除 | Draw 关冒烟回归 |
| 实体轮廓仍偏离视觉 | 视觉未接触却提前碰撞 | 12 点 Polygon 贴合门框中下段，不改成功 Trigger | 右下斜边、左右中段、门倒地后接触 |
| 当前质量不可解 | 画线难以推倒机关 | Inspector 单一参数来源，可局部调整 | 不同线长/角度 |
| 玩家被实体门挡住 | 无法进门 | 门实体 Polygon 仅排除 Actor | 各倾角进门 |

## 6. 验证与回滚

- Unity 6000.0.59f2 先前全量 Roslyn 编译 `0 error`；本次仅调整 Prefab 的 Collider 序列化类型，不修改 C# 运行时控制流程。
- Prefab 129 个 anchor 唯一、129 个唯一/254 处本地引用完整；12 点 Polygon 顺时针无自交，材质、Actor 排除位与原成功 Trigger 均保留。
- Editor.log 最近 6000 行未发现 D025、反序列化、无效序列化或编译错误。
- 静态确认画线完成直达唯一释放入口、同次 Attempt 单次锁存、两体 Kinematic 初态/Reset、旧接触门禁移除，以及 Trigger 显式查询保留。
- 完整 PlayMode 的画线即解锁、解锁后稳定性、实体轮廓贴合、Trigger 禁画、斜线可解性、Reset/Replay 和 Android/iOS 真机仍为 `uncertain`。
- 回滚画线释放会使门柱始终锁住，不能作为可玩方案；回滚 Trigger 确定化会让禁画重新依赖全局查询状态。
- 仅回滚本次实体轮廓时，把门实体 fileID `900000000000025002` 恢复为 `BoxCollider2D`，size `(2.3,2.55)`、offset `(0,-0.104367256)`；无需改脚本字段或成功 Trigger。
