# DLevel_053 风险评估

## 1. 评估范围

本记录覆盖 DLevel_053 的蜂群生成、追踪、物理碰撞、失败演出、成功倒计时、Prefab/GUID/FileID 和关卡配置链；共享影响包含 `DrawLevelCtrl.cs` 的延迟失败演出状态，以及 `DrawLevelCountdownCtrl.cs` 的失败演出期间冻结。结论基于 2026-08-13 的代码、Prefab YAML 与配置静态审计；Unity PlayMode 和移动端结果均标记为 `uncertain`。

## 2. 已确认行为

- 第一笔有效画线同时启动本关蜂群计划与共享 10 秒成功倒计时。
- 蜂群总数固定为 8，随机批次范围为 1 至 3，只改变批次组合和时间，不改变总量。
- 蜜蜂之间在生成时逐对忽略碰撞；蜜蜂与画线、地面和其它非 Trigger 场景碰撞体保持物理接触。
- 蜜蜂只对 Dynamic Rigidbody2D 施加接触点冲量；其它实体仍会触发蜜蜂反冲。
- 玩家命中通过唯一 Actor、Player Tag 和 Actor Layer 共同确认；特效位置优先使用 `Collision2D` 接触点。
- 失败演出先原子占用 `DrawLevelCtrl`，因此会锁输入和其它结算来源；共享倒计时读取该占用并停止扣时。
- 1.5 秒失败等待使用 unscaled time，结束后走 `CompleteFailurePerformance()`，没有另写重载或失败事件。
- Prefab 本地引用、脚本 GUID 和序列化字段静态闭环，未发现 Missing Script、重复 YAML 定义或悬空本地 FileID。

## 3. 风险矩阵

| 风险 | 等级 | 当前控制 | 仍需验证 |
| --- | --- | --- | --- |
| 玩家受击 1.5 秒内倒计时归零导致同时成功 | 高 | `TryBeginFailurePerformance` 原子占用；Countdown 在占用期间不扣时 | PlayMode 在剩余小于 1.5 秒时命中 |
| 同帧多只蜜蜂重复提交失败 | 高 | 首只命中后 `_isFailing` 与共享占用立即生效；后续不同蜂只播一次表现，同蜂按实例去重 | 多蜂同帧接触 Player |
| Actor 死亡被共享 `CheckFinished` 提前提交失败 | 高 | `DrawLevelCtrl.CheckFinished` 在 `_isPlayingFailure` 时直接返回 | PlayMode 验证实际事件顺序 |
| 高速蜜蜂穿过细线或玩家 | 高 | Continuous Collision、Interpolate、非 Trigger CircleCollider | 不同帧率与移动端 Physics2D 实测 |
| 反冲期间穿透障碍或恢复后卡入 Collider | 中 | 临时 IgnoreCollision；按距离和时限结束后恢复 | 薄线、凹角、连续碰撞和重叠出生 |
| 复合冲量使短线或轻障碍异常飞出 | 高 | 本关实际主冲量 `900`、水平冲量 `400` 均在模板暴露，只对 Dynamic 刚体施力 | 质量约 `12.5~50` 的不同长度画线 |
| 蜂群形成直线或出生重叠 | 中 | 均匀攻击角槽、椭圆出生、最小间距、随机批次与时间片 | 镜头内视觉分布和首批重叠率 |
| 蜂间 IgnoreCollision 配对成本增长 | 低 | 当前总数仅 8，最多 28 对 | 若策划大幅提高总数需重新评估 |
| 真正暂停或结算时蜂体仍运动/动画继续 | 高 | Pause/Resolved 时 `SetSuspended` 关闭 `Rigidbody2D.simulated` 并暂停动画；首击后的失败演出期刻意继续运动 | 暂停、恢复、成功、最终失败重建 |
| Disable/Reset 后异步失败回调污染新尝试 | 高 | attempt version、销毁 Token、OnDisable 取消占用 | 快速 Replay、退出和重置 |
| 运行时无法通过 20053 加载关卡 | 已关闭 | 数据源与运行时 data JSON 均唯一包含 20053 | PlayMode 实际资源加载 |
| D053 不出现在关卡序列 | 已关闭 | 排序 Excel/JSON 已追加 `45 / 20053` | 选关页实际展示与进入 |

## 4. Prefab 与资源风险

- 主 Prefab 的 `drawLevelCtrl`、`actor`、`hive`、`knockEffect` 均为非空绑定；Bee 模板和运行根由 Hive 持有。
- Bee Prefab 绑定两帧 Sprite、Rigidbody2D、CircleCollider2D 和 SpriteRenderer；Hive 绑定内部 SpawnPoint、inactive BeeTemplate 和运行根。
- 蜂巢无 Collider 是职责设计，不是资源缺失；它负责视觉、出生点、生成计划和运行实例管理。
- 主 Prefab 的 Hive 实例仍有指向旧 Hive 源 fileID 的 Collider/Rigidbody 历史 Override；它们不影响当前 Bee 参数继承，但应在 Unity Inspector 后续清理。
- 外部 GUID 静态扫描中出现的 Unity 2D Square 与 URP Sprite Lit/Unlit 默认材质来自 Package 或内置资源，不是缺失资产。
- `Assets/Game/Prefabs/Level/Level_053.prefab` 属于常规主线关卡，与 DrawGame 的 `DLevel_053.prefab` 不是重复资源。

## 5. 配置、数据与事件边界

- `LevelData.xlsx` 的 `draw_level` 已有 `20053 / DLevel_053 / 蜂群围攻`，墨水上限 15，评级阈值 0/2/5。
- `LevelSort.xlsx.drawLevelSort` 已追加下一顺序项 `45 / 20053 / 蜂群围攻`，并完成 Luban 生成。
- `tbdrawleveldata.json` 共 46 项，唯一一项为 `20053 / DLevel_053`；
  `tbdrawlevelsort.json` 共 45 项，唯一一项为 `45 / 20053`。配置阻断已关闭。
- 本关不写 DB，不新增全局事件。成功、失败、HUD 与重建均沿用 DrawGame 共享链路。

## 6. 验证证据

- YAML 检查：主/Bee/Hive 分别有 `120/6/9` 个定义，无重复定义、无悬空本地引用。
- GUID 检查：三个 Prefab、三个脚本及引用资源均可解析；未发现旧 `DLevel53_Bee`、`DLevel53_Hive` 或旧 GUID 引用。
- 参数检查：Hive 为 8 只、3 秒、批次 1 至 3；主 Prefab 为失败 1.5 秒、成功 10 秒。D053 实际 Bee 值为 Scale `0.825`、渐显 `0.2s`、主冲量 `900`、水平冲量 `400`、随机角 `±8°`；画线质量按长度约 `12.5~50`。
- 代码检查：失败占用、Countdown 暂停、接触点特效、unscaled 延迟、Reset/Disable 清理和反冲碰撞恢复路径均已静态核对。
- `git diff --check`：相关三个 C# 与三个 Prefab 通过。
- 本轮最终 D053 三脚本隔离编译为 `0 error / 21 warning`；warning 均为同名类型 `CS0436`。当前全工程编译被无关的 `Level117Ctrl.cs` 缺失 `sweat*` 字段阻断。
- 配置验证：Luban 执行成功；两份运行时 JSON 解析成功且 `20053` 各唯一一次。
- `uncertain`：未完成 Unity PlayMode、Android 或 iOS 横屏设备验证；当前没有可宣称的实际帧率、碰撞稳定性、视觉手感或移动端验收结论。

## 2026-08-13 蜂群分散与可调模板变更风险

### 变更基线

- 原出生半径 `0.25` 小于同批蜜蜂的有效可视/碰撞占用，且全部 Bee 每帧直追同一个 Actor 中心，导致出生和撞线位置都集中。
- 历史基线为 `impactImpulse=40 / strokeMass=200`，冲量量级不足以形成明显推动；当前实际值已改为主冲量 `900`、水平冲量 `400`、线质量约 `12.5~50`。
- 原生成配置位于 `DLevel53Ctrl`，蜂窝只提供随机位置；场景里没有可直接调整本关 Bee Override 的模板实例。

### 新风险与控制

| 风险 | 等级 | 当前控制 | 仍需验证 |
| --- | --- | --- | --- |
| 分散偏角过大导致蜜蜂绕开玩家 | 高 | `1.2 -> 0.45` 距离内平滑收束，近端完全直扑 | 不同角色位置、画线形状和移动状态 |
| 蜂群仍在同一点撞线 | 中 | 8 个均匀角度槽、槽内随机、每轮洗牌、椭圆出生与同批最小间距 | 多次重开记录撞击位置覆盖 |
| 本关实际冲量 `900 + 水平 400` 使短线或轻障碍飞出过快 | 高 | 只作用于 Dynamic Rigidbody2D；两个参数均在场景模板明确暴露 | 长短线、不同墨水长度和接触点手感 |
| inactive 模板意外参与物理 | 高 | Prefab 根 `m_IsActive=0`；Hive 启用时若发现模板激活会自动关闭 | Unity 导入后层级状态、Prefab Override |
| 运行克隆保持 inactive 而不飞 | 高 | `Instantiate -> Initialize -> SetActive(true)`，Initialize 主动解析引用 | PlayMode 首批生成和动画启动 |
| 清理误删模板 | 高 | 只清显式 `_spawnedBees` 列表；模板从不登记 | Reset、失败重开、Replay、退出 |
| Reset 时残留碰撞或反冲 IgnoreCollision | 高 | 运行蜂先 SetActive(false)，Bee.OnDisable 恢复当前障碍碰撞，再 Destroy | 反冲当帧 Reset 与快速退出 |
| 蜂间碰撞恢复或互相施力 | 中 | 新蜂先初始化并启用，再在下一物理步前与已登记 active 蜂逐对 IgnoreCollision；当前 8 只最多 28 对 | 多批生成、销毁和暂停恢复 |
| Hive/Ctrl 两个 Reset Handler 顺序不同 | 中 | 两者只重置各自所有状态；Ctrl 不再清蜂，Hive 不取消失败异步 | 清线 Reset 与失败演出中的 Reset |

### Prefab 与参数证据

- `DLevel53Hive.prefab` 包含 inactive `BeeTemplate_AdjustHere` nested prefab，Hive `beeTemplate` 直接引用其 stripped `DLevel53Bee` 组件。
- Hive 生成参数全部序列化在蜂窝：`8` 只、`3` 秒、批次 `1~3`、出生椭圆 `(0.6,0.3)`、最小间距 `0.4`、攻击偏角 `42` 度、抖动 `0.25`。
- Bee 源 Prefab 序列化 `impactImpulse=350`，D053 场景模板 Override 为 `900`；收束距离 `1.2`、直扑距离 `0.45`。
- 主 `DLevel_053.prefab` 的 Ctrl 只保留结算引用和 `failureDelay=1.5`，旧 `beePrefab`、`beeRoot`、总数、窗口及批次字段均已移除。

### 验证证据与未验证项

- Unity 6000.0.59f2 当前工程的 Bee 编译日志在中间失败记录后完成两次 `Assembly-CSharp` 编译，均为 `ExitCode: 0`；最终 DLL 生成时间晚于三个 D053 脚本。日志中仍可检索到的 `DLevel53Ctrl.cs(403)` / `CS7036` 属于迁移过程旧记录，当前文件只有 278 行，不是最终编译结果。
- Roslyn 隔离编译三个最终 D053 脚本同样为 `0 error`。工程级 MSBuild 仍因当前生成项目缺少大量 `Temp/bin/Debug/*.dll` 元数据而失败，但不改变 Unity 实际脚本编译已通过的证据。
- Prefab YAML 独立复审通过：Hive/Bee/主关卡均无重复 fileID、悬空本地引用或 Missing Script，nested template 的 source、父子关系、inactive Override 和 Hive 引用闭合。
- `git diff --check` 通过；静态字段扫描确认实际 Prefab 冲量不再是 `40`，Ctrl 不再持有生成字段。
- 代码复审发现并修复了 inactive Collider 上建立 `Physics2D.IgnoreCollision` 不可靠的问题；最终顺序为 inactive 初始化、启用、同帧建立忽略关系、登记运行列表。
- `uncertain`：未执行 Unity PlayMode、Android/iOS 横屏、真实物理手感、8 蜂移动端帧率或失败/成功边界测试。

### 本轮回滚路径

1. 优先通过 `BeeTemplate_AdjustHere` 将 `Impact Impulse` 调回 `40`，可只回滚推力手感。
2. 将 Hive `Max Attack Angle` 调为 `0` 可临时关闭路线分散而不改代码；完整回滚再移除槽位和 Bee 收束逻辑。
3. 若回滚职责迁移，恢复 Ctrl 原生成字段/列表与 Hive 的 `GetSpawnPosition()`，并移除 Hive 内嵌 BeeTemplate。
4. 本轮不改变 DB、Luban、全局事件、SDK 或共享 Physics2D Layer，因此这些系统无需迁移。

## 2026-08-13 渐显、复合冲量与连续失败演出风险

### 当前配置证据

- Bee 源 Prefab：Scale `0.825`、`fadeInDuration=0.2`、`impactImpulse=350`、`horizontalImpactImpulse=400`、随机偏角 `±8°`。
- `DLevel_053.prefab` 对内嵌 BeeTemplate 覆盖 `impactImpulse=900`；因此本关运行时主冲量不是 `350`。画线基准质量为 `strokeMass=50`，最低长度质量比例为 `0.25`。
- 首只蜜蜂命中后占用一次失败结果并调用一次 `Actor.Die()`；失败等待期间 Hive 不暂停生成，运行蜂不因 `Actor.IsDied` 停止追踪。后续不同蜜蜂只补播一次接触点表现。

### 风险与控制

| 风险 | 等级 | 当前控制 | 仍需验证 |
| --- | --- | --- | --- |
| 极短线被水平冲量推得过快 | 高 | 额外水平冲量单独暴露，可不改主冲量降到 `250~350` | 短/中/长线与不同接触点 |
| 主冲量和水平冲量叠加方向不符合示意 | 中 | 主冲量仅围绕入射方向随机 `±8°`，水平符号跟随入射或目标方向 | 蜜蜂从左右、上下斜向撞线 |
| 渐显期间暂停后 Alpha 错进 | 低 | 渐显只在蜂群模拟未暂停时由 Update 推进 | 生成当帧打开设置面板 |
| 首击后全蜂停止导致失败演出僵硬 | 高 | `_isFailing` 不再暂停 Hive/Bee，移除 `Actor.IsDied` 停飞条件 | 1.5 秒窗口内生成、追踪和碰撞 |
| 后续蜜蜂重复死亡或重复失败 | 高 | `_isFailing` 分支不再调用 `Die()`/失败 API；首击共享占用原子化 | 多蜂同帧和连续 CollisionStay |
| 同一蜜蜂持续接触重复特效 | 中 | `_playerHitBees` 按 Bee 去重 | Bee 销毁/Reset 后集合清理 |
| 多蜂同帧特效位置互相覆盖 | 低 | 当前明确复用单个 ParticleSystem | 是否需要改成粒子池 |

### 验证与回滚

- D053 三脚本 Unity Roslyn 隔离编译 `0 error / 21 warning`，warning 均为同名类型 `CS0436`；Prefab 静态引用和 `git diff --check` 通过。
- 全工程 Unity 编译目前被与本关无关的 `Level117Ctrl.cs` 缺失 `sweat*` 字段错误阻断，因此不把全工程编译写成通过。
- `uncertain`：PlayMode、Android/iOS 横屏、低帧率连续碰撞和最终力度手感尚未验证。
- 回滚可分别进行：Scale 恢复 `0.75`；渐显时长设 `0`；水平冲量和随机角设 `0`；主关卡 `impactImpulse` Override 从 `900` 恢复到旧值；若只回滚失败表现，再恢复首击暂停蜂群和 Actor 死亡停飞门禁。

## 2026-08-13 蜜蜂二维朝向修复风险

### 基线与变更范围

- 旧行为只按角色左右位置设置 `flipX`，斜飞时图片始终水平；两帧源图实际头朝左，但 Prefab 旧配置声明头朝右，因此还存在左右基准错误。
- 当前只在 `DLevel53Bee` 内缓存玩家主 Collider、计算身体中心瞄准方向，并组合水平翻面与刚体 `SetRotation`；Bee Prefab 的 `spriteFacesRight` 改为 `false`。不修改 Actor、共享 Physics2D Layer、生成器、碰撞冲量、倒计时或失败状态机。

### 风险与控制

| 风险 | 等级 | 当前控制 | 仍需验证 |
| --- | --- | --- | --- |
| 向左飞时整张蜜蜂倒立 | 高 | 先水平翻面，再从当前头部水平轴计算 `SignedAngle`，旋转范围折叠在 `±90°` | 左上、左下路线的真实画面 |
| 玩家正上/正下方连续翻面 | 中 | 水平分量在 `±0.02` 内保留上一左右状态 | 快速跨过玩家垂直线 |
| 朝向追随随机航线而非玩家 | 中 | 朝向独立使用 Bee 到玩家主 Collider 中心的方向，不使用带攻击偏角和摆动的飞行方向 | 大偏角远距离飞入 |
| 反冲时头部指向后退方向 | 中 | `UpdateRecoil()` 继续调用角色朝向刷新，反冲速度不参与视觉朝向 | 连续碰线与冷却窗口 |
| 旋转改变碰撞手感 | 低 | Bee 使用中心圆形 Collider；刚体继续 Freeze Rotation，脚本只显式设置表现角 | Unity PlayMode 高速碰撞 |
| 玩家主 Collider 缺失或失效 | 低 | 初始化优先根非 Trigger Collider，再选同刚体最大有效子 Collider；运行时失效回退 Transform | Actor Variant 与死亡演出 |

### 验证、缺口与回滚

- D053 三脚本隔离编译为 `0 error / 21 warning`，全部 warning 为预期 `CS0436`；三个相关 Prefab 的 fileID、本地引用和 Missing Script 静态检查通过。
- `uncertain`：未执行前台 PlayMode、Android/iOS 横屏和低帧率插值验证，不能仅凭静态数学检查宣称最终视觉手感已验收。
- 回滚只需恢复 Bee 的旧 `RefreshFacing()` 和 `spriteFacesRight: 1`，删除 `_targetCollider` / `_facesRight` 及辅助解析方法；不需要迁移数据、资源、存档、事件或 SDK 配置。

## 7. PlayMode 与移动端验收清单

1. 多次重开记录 8 只蜜蜂的批次组合和时间，确认总数固定且不是同时出现。
2. 分别让蜜蜂撞击动态画线、动态障碍、静态地面和 Kinematic 障碍，确认冲量与反冲方向正确。
3. 验证蜜蜂之间互相穿过且不会改变方向、施力或触发失败。
4. 从角色不同身体位置命中，确认 Knock 出现在真实接触点且只结算一次。
5. 在成功倒计时剩余小于 1.5 秒时触发命中，确认失败演出期间数值冻结且不会成功。
6. 验证暂停/恢复、清线 Reset、失败重开、成功、Replay 和快速退出均无残蜂、残粒子或旧异步回调。
7. Android/iOS 横屏至少各做一次 8 蜂压力测试，观察高速穿透、帧率、触控输入锁和粒子层级。

## 8. 回滚路径

1. 删除 DLevel_053 的三个局部脚本及 Bee/Hive Prefab，并从主 Prefab 移除局部控制器和引用。
2. 若完整回滚本玩法，同时还原 `DrawLevelCtrl` 新增的失败演出占用/完成/取消 API、输入与结算门禁，以及 `DrawLevelCountdownCtrl` 在失败演出期间冻结的增量；若其它关卡届时已开始复用这些接口，应先确认调用方再移除。
3. 从 LevelData、LevelSort 及其生成 JSON 中移除 20053，再按既有生成流程同步。
4. 若仅回滚物理手感，优先还原 Bee Prefab 的 Inspector 参数，不改共享 Physics2D Layer 矩阵，也不回滚与物理手感无关的延迟失败公共支持。
5. 无存档、DB、分析、广告或 SDK 数据需要迁移。

## 2026-08-18 D053-D061 肿胀动画变更风险

### 风险与控制

| 风险 | 等级 | 当前控制 | 仍需验证 |
| --- | --- | --- | --- |
| `swell` 被 `ActorCtrl.Die()` 的 `die` 同帧覆盖 | 高 | 先调用 `Die()` 锁定状态和移动，再播放 `swell`；`IsDied` 阻止共享自动动画继续切换 | PlayMode 首次命中实际轨道顺序 |
| 旧骨骼缺少动画导致 Spine 抛错或失败链卡住 | 高 | 九关局部切换到含 `swell` 的骨骼；播放前 `FindAnimation`；失败异步先启动，缺失时保留默认 `die` | Prefab 实际导入和运行时 SkeletonData |
| 新骨骼与旧 Atlas 材质不匹配，出现紫块或错图 | 高 | 按 Level083 先例同时覆盖两个 SkeletonPartsRenderer 材质；骨骼、材质 GUID 均存在 | 各 Skin、分离渲染层级和裁剪效果 |
| 后续蜜蜂反复从头播放 `swell` | 中 | 肿胀调用只在首次失败分支；后续分支只播放 Knock 和音效 | 多蜂同帧与连续接触 |
| 新骨骼破坏原 idle、cover_ears01 或特殊关卡动画 | 高 | 静态对比确认旧 64 个动画和 7 个 Skin 均完整保留，新资源为旧资源超集 | D053-D061 开场，尤其 D058/D059/D061 入场、开夹和释放 |
| 修改共享 Actor 影响其它关卡 | 已关闭 | 只修改九个关卡的 nested Actor Override，共享 `Actor.prefab` 未改 | Unity Inspector 确认 Override 归属 |

### 验证证据、缺口与回滚

- 当前完整 `Assembly-CSharp` Roslyn 编译为 `0 error / 107 warning`，目标脚本无诊断；九个 Prefab 各有正确的 `1+2` 资源 Override，均无重复 fileID 或悬空本地引用，`git diff --check` 通过。
- `uncertain`：未执行 Unity PlayMode、移动端、各 Skin 与重开后的动画恢复验证，不能仅凭静态资源超集宣称最终视觉已验收。
- 回滚时移除九个 Actor 的 SkeletonData/两条材质 Override，并删除 `DLevel53Ctrl` 的 swell 覆盖与排队逻辑即可；不需要修改配置、存档、事件或蜂群物理参数。
