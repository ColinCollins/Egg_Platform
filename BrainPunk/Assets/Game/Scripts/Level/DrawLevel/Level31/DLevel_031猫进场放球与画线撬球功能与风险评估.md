# DLevel_031 猫进场放球与画线撬球功能与风险评估

## 1. 需求结论

本关采用以下明确行为：

1. 猫夹球装置从左侧进场。
2. 猫到位后自动开夹放球，不等待玩家先画一笔。
3. 球落稳且猫退场后才允许画线。
4. 玩家画出包住球的杯口和向右上方延伸的长尾，依靠真实物理把球撬进高处门。

实现不增加脚本推力、不锁定解法线、不把球瞬移到成功门，也不复制第二套猫夹球控制器。

## 2. 基线问题

修改前 DLevel031 根节点挂载 `DLevel27Ctrl : DLevel2Ctrl`。该基类的契约是猫先入场持球，第一笔有效线
完成后才开夹释放，和“进场放球”不一致。若在保留它的同时再增加 `DLevel10Ctrl`，`DrawLevelCtrl` 会把
两个 Intro Handler 都收集并串行调用，导致同一套 Rig 和球被重复复位、挂接和释放。

复制来的球只有 `mass=0.5`，而画线满长度质量为 80；球过轻时，小幅接触也可能产生过强速度。画线碰撞
半径 `0.05` 也只有视觉半宽的一半，不利于稳定围住球。

## 3. 当前方案

### 3.1 入场复用

- 以 `DLevel10Ctrl` 替换根节点原控制器，使用 `FromLeft`。
- 显式绑定本关 `DrawAlienClipBallRigView` 与根释放父节点。
- 复用“闭夹飞入 -> 开夹放球 -> 原路退场 -> 落地双弹 -> 开放画线”的现有完整序列。
- Retry 复用共享最终态，不重复播放整段 Intro。

### 3.2 真实撬升物理

- 球：`mass=1 / gravityScale=1.8 / linearDamping=0.04 / Interpolate / Continuous`。
- 线：`strokeMass=100 / colliderRadius=0.1 / gravityScale=2.5 / angularDamping=0.2 / Interpolate`。
- 线使用本关独立 `DLevel31Grip`，摩擦 1、弹性 0、Maximum Combine；球不使用高弹材质。
- 线质量继续按长度缩放，极短线最低为配置质量的 25%；没有 AddForce、AddTorque 或 Joint2D。

当前参数是首轮可玩基线，不是最终手感承诺。推荐在正式 PlayMode 中一次只调整一个维度：

- 撬升不足：先把线 `gravityScale` 从 2.5 调到 3.0，再考虑把 `strokeMass` 从 100 调到 120。
- 球飞得过猛：先把 `strokeMass` 降到 80，或把球质量从 1 小幅提高到 1.5～2。
- 杯口抓不住：当前线摩擦已提高到 1；优先检查杯口线形与接触半径，不增加脚本吸附。
- 球下坠过快：把球重力从 1.8 降到 1.5，不建议用高弹材质补偿。

## 4. 影响范围

| 范围 | 结果 |
|---|---|
| DLevel031 Prefab | 修改根控制器、显式 Rig/渐隐引用、球参数、线模板物理与独立高摩擦材质 override |
| `DLevel31Grip.physicsMaterial2D` | 本关画线专用 friction 1、bounce 0 材质 |
| `DLevel10Ctrl` | 只删除错误的 FromTop 专属诊断并更正注释/Inspector 标题 |
| 共享猫夹球 Prefab | 未修改 |
| 共享画线 Prefab | 未修改；所有物理值为 DLevel031 nested instance override |
| Physics2D Layer Matrix | 未修改 |
| Actor/成功门/FallArea | 未修改共享资源和回调 |
| UI、存档、analytics、SDK、平台分支 | 未修改 |
| Luban/JSON/Excel | 未修改，DLevel031 正式配置入口仍待确定 |

## 5. 生命周期与状态风险

### 5.1 Intro 超时

球若因碰撞配置异常未在 8 秒内稳定落地，`DLevel10Ctrl` 会记录错误并结束 Intro 门禁，避免永久不能画线。
这种兜底只保证可操作，不保证球处于理想落点。

### 5.2 Reset/Replay 落点

共享 Retry 只向下接受本关根下的 Static、非 Trigger Collider。当前长地面满足条件；若以后把初始落点改成
Dynamic 托盘或机关，必须增加本关明确落座逻辑，不能继续假设共享 Cast 会命中。

### 5.3 暂停

现有 Draw 暂停链没有统一把 `Time.timeScale` 置 0，DOTween 和 Physics2D 可能在暂停画面中继续推进。
这是共享流程既有缺口，本次没有扩大为全局暂停改造；正式试玩必须覆盖 Intro 中暂停与放球后暂停。

### 5.4 结果与渐隐

成功门通过球内 Actor 身份回认复合球。球壳渐隐组现已显式绑定，避免 Actor 已淡出而蓝色外壳残留。
失败边界与成功 Trigger 同帧竞争仍由 `DrawLevelCtrl` 结果锁处理。

## 6. 物理风险

| 风险 | 等级 | 观察点/处理 |
|---|---|---|
| 长线完成瞬间与球/地面重叠，产生过大分离冲量 | 高 | 检查常见杯口画法；必要时调质量/重力，不做瞬移 |
| 高门附近 Top FallArea 净空较紧 | 高 | 验证擦门、越门和高抛；只有实际误判再上移 Top |
| 最大摩擦长线在地面卡死，尾部不能形成转矩 | 中 | 对照夹持改善与尾部转动；若明显卡死再局部回调到 0.9 |
| 多笔补画互相穿过 | 中 | 当前明确关闭笔间碰撞，避免多笔互卡；补笔只能与球/场景作用 |
| 球角速度导致外壳视觉快速旋转 | 中 | 当前角阻尼继承 0.5；实测异常再做 D031 局部 override |
| 球从左侧极远处逃出 | 低 | 长地面覆盖至约 x=-27.4；确认可逃出后再补 Left FallArea |
| 场景残留 `Circle (4)` 静态圆碰撞影响某些线形 | 中 | 它没有 Joint 且 `trackLine` 为空；PlayMode 判断是否为本关设计对象 |

## 7. 验证清单

### 已完成

- [x] 根节点只有一个猫夹球 Intro Handler。
- [x] DLevel10Ctrl、Rig、释放父节点和球壳渐隐组引用显式存在。
- [x] 球与画线参数从 Prefab YAML 回读一致。
- [x] 147 个 YAML anchor 唯一，147 个唯一非零本地引用无缺失。
- [x] Unity PrefabImporter 成功导入 DLevel031，导入后日志段无目标错误。
- [x] DLevel31Grip 经 NativeFormatImporter 导入，Prefab 中新 GUID 唯一且可解析。
- [x] Unity Bee/Roslyn 完整 Assembly-CSharp 编译为 `0 error`。
- [x] tracked 文档和 C# `git diff --check` 无空白错误，仅有仓库换行提示。

### 待 PlayMode/真机

- [ ] 首次进入从左侧飞入，球到位后自动释放，画线在整个 Intro 结束前不可用。
- [ ] 球准确落在左侧长地面，双弹结束后仍为 Dynamic。
- [ ] 参考图的单根杯口长尾可以夹住球并撬到门高。
- [ ] 短线、满墨长线、多笔补画不会产生不可控爆速或长期卡死。
- [ ] 成功时内部 Actor 与蓝色球壳同步渐隐。
- [ ] 擦门、高抛和越门时成功/Top FallArea 判定顺序符合预期。
- [ ] 20 秒失败、手动 Reset、失败 Retry、成功 Replay 都恢复正确最终态。
- [ ] Intro 中暂停、物理运行中暂停、低帧率和 Android/iOS 横屏触控表现可接受。
- [ ] 正式配置表加入关卡后，50 墨水玩法和评级口径一致。

## 8. 回滚边界

- 回滚自动放球：把根控制器恢复为原 `DLevel27Ctrl` 字段组，并移除 DLevel10Ctrl Rig stripped 引用；行为会
  回到第一笔后放球，不能同时保留两个控制器。
- 回滚物理手感：只需恢复 DLevel031 的球 Rigidbody2D 和画线模板 nested override，以及
  `DrawLineFeature.strokeMass/colliderRadius`。仅回滚本次摩擦时，把画线材质引用恢复为
  `DrawStrokeStable` 并删除未被引用的 `DLevel31Grip` 及其 meta；共享 Prefab 不需要迁移。
- 回滚成功渐隐：清空 `successExtraRendererGroup` 会重新产生球壳可能残留的问题，不建议单独回滚。
- 完整移除本关玩法不需要清理 DB、存档或 SDK；当前未写入相关配置。

## 9. 2026-08-04 夹球摩擦跟进

- 基线：DLevel031 画线原复用 `DrawStrokeStable(friction=0.8)`，用户担心杯口接触仍会让球滑脱。
- 变更：新增本关独立 `DLevel31Grip(friction=1,bounce=0,Maximum)`，只替换 DLevel031 画线模板实例的
  Rigidbody2D 材质。球材质保持不变，避免球与地面也获得最大摩擦而削弱撬升。
- 影响：不改共享材质、共享画线、质量、重力、碰撞半径、事件、成功/失败、配置、存档或平台链。
- 待验证：不同杯口曲率下的滑脱率、长尾是否被地面卡死、球离杯时机和进门高度仍为 `uncertain`。
- 静态验证：147 个 Prefab anchor/147 个唯一非零本地引用无重复缺失；材质与 Prefab 均由 Unity 成功导入，
  导入后日志无 C#、断言、YAML 或缺失引用错误。
