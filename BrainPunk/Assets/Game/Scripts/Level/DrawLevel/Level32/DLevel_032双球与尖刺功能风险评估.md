# DLevel_032 双球与尖刺功能风险评估

## 1. 修改范围

- 使用共享 `DrawTwoBallReleaseCtrl`，替代删猫后无有效 Rig 引用的 `DLevel6Ctrl`。
- `DLevel32SpikeTrapHandle` 从“实例化完整 Knock Prefab”改为“复用 D003 同结构的现成粒子实例”。
- D032 Prefab 保留现有双球、10 个尖刺、双球成功组件和场景布局；不添加猫或夹子。
- D006 临时 `playEntranceAnimation` 特例已撤回；正式爱心贴图共享改动保留。

## 2. 风险与控制

| 风险 | 等级 | 当前控制 |
| --- | --- | --- |
| 删猫后首笔无法释放球 | 高 | D032 独立控制器直接绑定两颗本地 Rigidbody2D，不依赖 Rig |
| 重置后球位置、镜像或碰撞盒偏移错误 | 高 | 缓存并恢复父级、局部位姿、缩放、Collider offset 与 payload 位姿 |
| 右球负缩放参与动态物理 | 高 | 释放时将镜像转移到 payload，Rigidbody2D 根改为正缩放 |
| 完整 Knock Prefab 带出敲门子表现 | 高 | 与 D003 一样移除 source localID `5393762992232218095` |
| 命中反复创建粒子和残留对象 | 中 | 单实例 Stop/Clear/移动/Play；Reset 与 Disable 均清空 |
| 右球 `tools` Tag 漏判 | 高 | 本关运行时清空尖刺 Tag 过滤并从 attached Rigidbody 子级解析 Actor |
| 其它 TriggerItem 被误绑定 | 中 | 当前 D032 的该组件计数为 10，均来自 Caltrop；扩展场景时需改显式列表 |
| 成功与失败同帧竞争 | 高 | 两条链继续通过 DrawLevelCtrl 原子结果门禁；真实顺序待 PlayMode |
| 手写 Prefab 本地引用断链 | 高 | 303 anchors 唯一，0 duplicate，0 missing local reference |
| 移动端粒子/纹理方向异常 | 中 | Android/iOS 未验证，状态为 `uncertain` |

## 3. 复用边界

- 复用 D003 的粒子资源、子对象裁剪规则和“移动现成实例后播放”的方式，不复用主线失败事件。
- 复用 `DrawTwoBallReunionSuccessHandle` 的双球成功表现和正式爱心资源，不复制结算逻辑。
- 首笔双球释放只属于无猫的 D032，因此使用局部控制器，不继续扩展 D006 猫夹控制器。
- 没有修改共享 `ActorCtrl`、Physics2D Layer Matrix、DB、存档、Luban、RemoteConfig、analytics
  或平台 SDK。

## 4. 验证证据

- Unity 6000.0.59f2 自带 Roslyn 完整编译命令返回 `exit 0`；输出只有工程既有 warning。
- 静态 Prefab 回读确认：无 `Alien` 名称、无 DLevel6 GUID、一个 DLevel32 GUID、两颗球绑定正确、
  两球 `Kinematic/mass=10`、10 个尖刺 Trigger、一个 Knock nested instance。
- D032 不再出现 `knockPrefab` 或直接外部 ParticleSystem PPtr；D003/D032 的 removed child localID 一致。
- `git diff --check` 的本轮目标文件通过；仓库其它用户改动中仍有 D029/D031 既有尾随空白，未擅自修改。
- 最新 PrefabImporter、完整 PlayMode、连续 Reset/Replay、暂停恢复、低帧率以及 Android/iOS 横屏
  尚未完成，结论为 `uncertain`。

## 5. 回滚路径

- 仅回滚 D032 首笔释放：移除 `DrawTwoBallReleaseCtrl` 和 Prefab 组件绑定；不要恢复猫或 DLevel6Ctrl。
- 仅回滚粒子播放方式：恢复尖刺控制器字段和 Prefab 绑定；若恢复完整模板实例化，会重新引入敲门
  子表现，需同时保留 D003 的 removed child override。
- 仅回滚爱心贴图：删除共享 Resources 加载分支和 PNG；双球碰撞、冻结和成功提交不受影响。

## 6. 2026-08-05 爱心尺寸与发射率风险补充

- 影响文件限定为共享 `DrawTwoBallReunionSuccessHandle.cs` 与 D006/D032/D033/D042/D043 五个关卡
  Prefab；D032 使用 `heartSizeMultiplier=1.5`、`heartEmissionRate=4`，原值分别为无额外倍率 `1`
  与 `5`。Bubble 原随机尺寸 `0.2~0.4` 因此约变为 `0.3~0.6`。
- 运行时只修改新克隆实例的 `main.startSize`；正式贴图、Bubble 源 Prefab、尖刺 Knock 粒子、碰撞与
  失败逻辑不变。较大的屏幕覆盖由降低连续发射率平衡，但这不是移动端性能验证结论。
- Unity 6000.0.59f2 自带 Roslyn 完整编译为 `ExitCode=0 / Errors=0 / Warnings=104`，目标脚本无诊断；
  共享脚本消费者恰为五关且每关成功组件、rate `4`、size `1.5` 各 1 个。D032 Prefab 检查为
  `Definitions/LocalReferences/duplicate/missing=259/539/0/0`。Unity Editor.log 已记录五个目标 PrefabImporter
  成功导入并完成脚本重编译；D032 PlayMode 的爱心层级与密度、尖刺/成功竞争、Pause/Reset/Replay 和
  Android/iOS 过绘仍为 `uncertain`。
- 回滚本次视觉参数时只恢复倍率 `1` 与发射率 `5`，并在移除字段时同步清理五个 Prefab 的序列化项；
  不执行本文件旧有的“删除正式贴图”回滚，也不恢复完整 Knock 模板或 DLevel6Ctrl。
