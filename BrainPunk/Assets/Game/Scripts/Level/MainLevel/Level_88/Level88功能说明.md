# Level088 功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：按黄色关卡范围清理可证明的冗余代码，要求玩家可见行为、资源、事件、重置与结算流程不变。
- 影响文件：仅 `Level88Ctrl.cs`。`SetFirstBurstCount()` 的唯一调用参数来自当前对象的 `ParticleSystem` 组件枚举，因此移除方法内重复的空引用防御；Burst 数量写入、粒子资源和播放时机均未改变。
- 前后差异：原逻辑在进入该方法前已取得有效组件；精简后仍按原顺序检查 `burstCount` 并写回首个 Burst。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过；未修改 Prefab、配置、存档、打点和共享脚本。
- 回滚：恢复 `SetFirstBurstCount()` 开头的空引用判断即可，不需要回滚其它 Level088 内容。

更新时间：2026-08-10

## 需求背景

Level088 复制常规关卡 Level004 的地形、角色、敲门区域和进门吸入成功链。区别是门不会在少量敲击后直接打开，而是永久累计敲击次数，在第 15、20、30 次依次进入三段破损，完全破碎后才能进入。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_088.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_88/Level88Ctrl.cs`
- `Assets/Game/Scripts/Level/SuccessAnimCtrl.cs`
- `Assets/Game/ArtSrc/Sprites/L88/Sp_l88_01.png` 到 `Sp_l88_04.png`
- 复用 `Assets/Game/ArtSrc/Particles/Prefabs/Knock_Explosion.prefab`
- 复用 `Assets/Game/ArtSrc/Particles/Prefabs/DoorBroken_075.prefab`

没有修改 Level004、Level075、共享 `KnockKnock`、`BaseLevelCtrl`、Actor、`door_0` 或两个复用粒子 Prefab。共享 `SuccessAnimCtrl` 仅增加默认关闭的 `suppressCloseDoorSfx` 序列化开关；只有 Level088 的 `door_0` Prefab 实例将其覆盖为开启，其他关卡继续播放原关门音效。

## 原有行为基线

- Level004 的 `KnockKnock` 要求在 `1.5s` 内敲击 3 次，超时会清零，达标后启用出口 Collider 并播放旧门 Spine 开门动画。
- Level088 已复制完整 `BaseLevelCtrl`、Actor、`door_0`、成功 Collider、吸入动画、`knockArea` 和普通敲门粒子结构。
- 策划已在 `door_0` 下放置 `Sp_l88_01`，并将旧门显示节点禁用；L88 目录提供完整门到破洞的四张阶段贴图。

## 当前行为

- 敲击只在实际 `PLAYING` 状态生效，从 UI 开始的触摸或鼠标点击会被忽略。
- 点击必须命中原 Level004 的独立 `knockArea` Collider；计数不设时间限制，也不会因停顿清零。
- 每次有效敲击都会把 Level004 已有的 `Knock_Explosion` 移到本次点击的世界坐标，清空上一轮残留后重新播放；第 15、20、30 次也包含该反馈。
- 第 1 到 14 次门外观不变，并播放普通 `knockDoor` 敲门音效。
- 第 15 次切换 `Sp_l88_02`，播放 4 个主碎片。
- 第 20 次切换 `Sp_l88_03`，播放 5 个主碎片。
- 第 30 次切换 `Sp_l88_04`，播放 18 个主碎片，禁用敲门区域并启用原 `door_0` 成功 Trigger。
- 三段破损实例都会关闭名为 `Clouds` 的子粒子，只保留门碎片；修改只作用于 Level088 运行时实例。
- 三次破损使用 `blockByDoor` 音效；普通敲击仍使用 `knockDoor`。
- Actor 进入已启用的 Trigger 后继续执行原 `BaseLevelCtrl -> SuccessAnimCtrl -> ActorDestroyAnimHandle` 吸入过关链，但 Level088 跳过该成功动画末尾的 `closeDoor` 音效。

## 资源与数据流

- 输入流：Touch / Editor Mouse -> UI 排除 -> `knockArea.OverlapPoint` -> 永久累计计数。
- 敲门反馈流：有效点击屏幕坐标 -> 主相机转换世界坐标 -> 移动并重播 `Knock_Explosion` 根粒子及其子粒子。
- 视觉流：四张 L88 Sprite 在同一个 SpriteRenderer 上切换，不运行时加载或实例化门贴图。
- 粒子流：仅三个破损阈值实例化 `DoorBroken_075`；激活前覆盖根碎片首个 Burst 并禁用 `Clouds` 子节点，播放结束由 Prefab 的 StopAction 自动销毁。
- 成功流：第 30 次启用既有成功 Collider -> Actor 进入 -> 原成功动画与关卡完成回调；Level088 的 Prefab override 让共享成功脚本仅在本实例跳过 `closeDoor`。
- 本关不读写 DB、存档、配置、analytics、广告、IAP、RemoteConfig 或平台 SDK。

## 当前参数

| 项目 | 当前值 |
| --- | --- |
| 三段破损阈值 | `15 / 20 / 30` |
| 主碎片数量 | `4 / 5 / 18` |
| 烟尘数量 | `0 / 0 / 0`（`Clouds` 子系统禁用） |
| 敲门反馈 | 每次有效敲击重播 `Knock_Explosion` |
| 成功末尾关门音效 | 仅 Level088 跳过 |
| 普通敲击计数超时 | 无 |

## 验证与缺口

- 将破损阈值调整为 `15 / 20 / 30` 后，使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 再次复编，结果为 `0 error`，`Level88Ctrl.cs` 无诊断，临时编译产物已删除。
- 本轮修改后，Unity 6000.0.59f2 于 2026-08-10 13:46 重新生成 `Assembly-CSharp.dll`；`Temp/pipeline_recompile_status.json` 记录 `completed`、`failed:false`、`errors:[]`，脚本编译通过。
- 实现初版使用 Unity 6000.0.59f2 的 `Assembly-CSharp.rsp` 并显式追加新脚本执行完整 Roslyn 编译，结果为 `0 error`；输出仅有工程既有 warning，没有 Level088 诊断。
- 本轮另用 Unity 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 定向复编；响应文件包含 `Level88Ctrl.cs` 与 `SuccessAnimCtrl.cs`，结果为 `0 error`，两份目标脚本均无诊断，临时 DLL 已删除。
- 最新 Prefab YAML 静态扫描为 139 个对象定义、266 条本地非零引用、0 重复、0 缺失。
- 脚本 GUID、四张门图 GUID、`Knock_Explosion`、`DoorBroken_075`、`knockArea`、门阶段 Renderer、成功 Collider、Level088 的关门音效 override、OnTrigger 回调和吸入动画引用已静态核对。
- 静态扫描确认 Level088 不再保留烟尘数量字段，运行时代码只禁用实例中的 `Clouds`，没有修改共享 `DoorBroken_075` Prefab。
- 全关卡 Prefab 扫描只发现 Level088 写入 `suppressCloseDoorSfx` override；共享字段默认值为 false，因此其他关卡的序列化行为保持原样。
- 当前生成配置 `tbleveldata.json` / `tblevelsort.json` 没有 `10088 / Level_088`；本次未擅自选择排序或文案配置，正常选关入口仍待策划配置。
- 当前 Tips 仍沿用复制关卡的 `S_LevelTips_Des_level_04`，尚无 Level088 独立提示、答案或通关文案。
- 尚未执行 Unity PlayMode 完整敲击 30 次、吸入通关、重开和移动端真机验证。

## 回滚路径

- 将 Level088 根组件恢复为复制时的共享 `KnockKnock`，或移除 `Level88Ctrl` 并恢复原字段。
- 如需单独撤回本轮反馈，解除 `knockVfx` 和 `suppressCloseDoorSfx` 的 Level088 Prefab 接线，并恢复旧碎片数量；删除共享脚本中的默认关闭字段及其条件判断即可完全恢复原成功音效实现。
- 保留策划提供的 L88 门素材与 Level088 地形；Level004、Level075 和共享粒子资源无需回滚。
