# DLevel_014 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_014`
- 记录日期：2026-08-03
- 目标：玩家先画线，第一笔有效线完成后篮球自动以一次性冲量投出；玩家依靠真实
  Physics2D 碰撞改变弹道，让篮球从上方向下进入复用的 DLevel013 篮筐。
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_014.prefab`
- 局部控制器：`Assets/Game/Scripts/Level/DrawLevel/Level14/DLevel14Ctrl.cs`
- 参考：DLevel008 的首笔 Impulse 生命周期、DLevel013 的双平面进筐判定。

## 2. 组件结构

```text
DLevel_014
├─ DrawLevelCtrl
├─ DrawLineFeature                    # 50 墨水、多笔、Dynamic 重线
├─ DrawLevelCountdownCtrl             # 首笔后 15 秒，超时失败
├─ DLevel14Ctrl
│  ├─ 首笔一次性 AddForce / AddTorque
│  ├─ 篮筐上/下平面扫掠判定
│  ├─ 进球点彩纸烟花 + 延迟渐隐
│  └─ 篮球进入 FallArea 的本地失败判定
├─ Sp_hx_bbox_01                      # 投球器节点；使用用户手调的 Sp_hx_jstl_01 Sprite 与实体碰撞
├─ Sp_prop_1_02                       # 篮球 Rigidbody2D + CircleCollider2D
├─ Sp_hx_lqk_01                       # D013 同版前景与两侧实体篮沿
├─ Sp_hx_lqk_02                       # D013 同版篮板背景
├─ DrawGame_BasketSuccessFirework     # 成功时运行时实例化
└─ FallArea
```

## 3. 生命周期

1. `Awake` 缓存篮球局部位置、旋转与颜色，强制设为 Kinematic 并清零速度。
2. 点按、取消或无效短线不触发；第一笔有效线完成后才切 Dynamic。
3. 发射顺序固定为：清零线/角速度 → `AddForce(..., Impulse)` →
   `AddTorque(..., Impulse)` → `WakeUp()`。后续不再逐帧写位置或速度。
4. 玩家画线在该回调前已经生成 Collider 并启用 Dynamic，因此篮球与线、投球器、篮沿之间
   全部由 Physics2D 求解。
5. 球心向下依次扫过篮沿上、下平面且 X 在开口内才成功；侧入、下方反弹和只碰框不成功。
6. 成功锁占用后在下平面插值穿越点播放一轮烟花。篮球不定格，继续运动 `0.35s` 后用
   `0.4s` 渐隐，再提交胜利。
7. 篮球中心进入本关 FallArea 时由本控制器先播放 `failed` 音效，等待 `0.2s` 后调用
   `DrawLevelCtrl.TryFailAndReset()`；这是因为篮球没有 `ActorCtrl`，不能依赖通用 FallArea 的角色过滤。
   该本地链没有 Actor 死亡动画，且一旦触发，`0.2s` 等待与通用 FallArea 一样不会被 Pause 冻结。
8. Pause 会关闭已发射篮球的 `simulated`，暂停正在播放的烟花；恢复时保留原速度继续模拟。
   成功等待和渐隐只累计非暂停时长。
9. Attempt Reset 恢复初始位姿、Kinematic、零速度、完整 alpha，并清理烟花和所有本地门禁。

## 4. 当前参数

| 对象 | 参数 | 当前值 |
| --- | --- | --- |
| 发射 | 世界方向 / Impulse / Spin | `(1, 0.75) / 14 / -0.35` |
| 篮球 Rigidbody2D | Mass / Gravity / Detection | `1 / 1 / Continuous` |
| 篮球材质 | Friction / Bounce / Combine | `0.1 / 0.85 / Maximum` |
| 篮球初态 | Body Type | `Kinematic`；第一笔后才为 Dynamic |
| 篮筐 | 开口世界宽度 | 约 `2.141`；与当前 D013 几何一致，只整体上移 `1.15` |
| 进筐门禁 | X 容差 / 最小向下速度 | `0.04 / 0.1` |
| 成功表现 | 延迟 / 渐隐 | `0.35s / 0.4s`，期间不停止物理 |
| 倒计时 | 时长 / 到时结果 | `15s / Failed` |

## 5. 资源与系统边界

- 本关材质：`DLevel014_BasketballBounce.physicsMaterial2D`，只绑定 D014 篮球。
- 共享烟花：`Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_BasketSuccessFirework.prefab`；
  复用常规成功门的 Confetti 参数，不实例化整扇门；根 SortingGroup 使用 SceneLayer Order `220`，
  高于篮球 `150` 与篮筐前景 `151`。
- D014 不继承或调用 DLevel13Ctrl；D013 控制器只按组合需求局部增加得分点烟花。共享 DrawLine、
  DLevel008、Physics2D Layer Matrix 和全局材质不改。
- 不新增 DB/Luban/存档字段、全局事件、UI、analytics、广告/IAP、RemoteConfig 或平台分支。

## 6. 验证与缺口

- Unity Roslyn `Assembly-CSharp` 全量编译：`0 error`，只有工程既有 warnings。
- 静态检查：控制器、篮球、两篮沿、FallArea、材质和共享烟花引用均非空；D014 `107` 个、
  共享烟花 `5` 个 YAML anchor 均唯一且本地 fileID 可解析，Unity PrefabImporter 已重新导入。
- `uncertain`：尚未执行完整 PlayMode，`14` 冲量、`0.85` 弹性、画线解法、擦框判定、烟花
  尺寸/实际遮挡、暂停恢复接触、15 秒可解性、FallArea 节奏、Reset/Replay 和 Android/iOS 横屏均待实测。

## 7. 回滚

- 只回滚手感时优先调整 `launchDirection`、`launchImpulse`、`spinImpulse` 或本关篮球材质，
  不在碰撞回调中强写反射速度。
- 回滚篮筐同步时应成组恢复前/后景 Scale、前景位置及两个 BoxCollider 的 offset/size。
- 完整回滚可移除 DLevel14Ctrl 和本关材质，并恢复旧 DLevel15Ctrl；这也会同时撤销首笔发射、
  进筐结算、烟花和篮球 FallArea 失败。
- 用户手调的投球器位置、旋转、新 Sprite 与 Collider 尺寸不是本功能回滚目标。
