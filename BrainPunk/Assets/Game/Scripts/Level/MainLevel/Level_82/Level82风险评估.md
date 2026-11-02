# Level082 风险评估

更新时间：2026-07-20

## 原有行为基线

Level012 的 HUD 拖拽会配合场景映射生成碰撞体；公共 StartDoor 不承担可拖物理门职责；基础失败流程在掉入 FailArea 后仍有短暂延迟才重置关卡。Level082 需要改变这些本关表现，但不能影响上述共享实现和其他关卡。

## 受影响资源与系统

- Level082 关卡预制体、专用 HUD、两个运行时控制脚本和一个 Editor 生成器。
- L82 的 13 张石头图片、13 个石头 prefab 和共用物理材质。
- 复用但未修改：`DragableItem`、`GamePlayPanel_DragButtons`、`StartDoor`、`BaseLevelCtrl`、事件总线和 DOTween 镜头反馈。

## 风险与处理

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 修改共享代码导致 Level007/012 或其他关卡回归 | 所有行为放在 Level082 专用脚本、关卡 prefab 和 HUD 变体中；共享资源未修改 | 范围静态已检查 |
| 半透明只影响根图片、子图标仍不透明 | 三个按钮根节点使用 `CanvasGroup(alpha=0.5)`，让整棵 UI 子树统一透明 | Unity 已导入，画面待 PlayMode |
| 拖拽集合误包含 levelBlock | `Btns` 仅保留 reset、pause、tips，并移除 levelBlock 的本地 `UIDrag` | 静态已检查 |
| 去掉 UI 场景碰撞时破坏按钮点击/拖拽 | 不绑定 `Level12SceneItemsCtrl`，但保留 Graphic Raycast、CustomButton 与 `UIDrag` | 静态已检查，触摸待验证 |
| 新实体门碰到角色或石头造成意外推挤 | 门的实体碰撞局部过滤 Actor、Stuff 与 Layer13 石头；原 StartDoor Trigger 单独保留 | Prefab 静态已检查 |
| 拖门开场或失败期间仍可操作 | 同时检查 ActorCtrlable、播放状态、尝试切换、死亡、进门和本关 `IsAttemptBlocked`；失败事件同步通知门锁定 | 代码静态已检查，PlayMode 待验证 |
| 掉入 FailArea 后延迟窗口仍能继续落石 | 本关同步监听 `OnTriggerFailAreaEvent`，先置结束标记，再停止协程、震屏和门交互 | 代码静态已检查，PlayMode 待验证 |
| 多次碰撞重复触发同一次落地 | 仅在 `isWaitingForLanding` 时处理，并在首次 Ground 支撑接触后立即清除标记 | 代码静态已检查 |
| 门侧撞墙也被当成落地 | 除 Ground 层外还要求接触法线 `normal.y > 0.25` | 代码静态已检查 |
| 门被释放到沟壑后掉出可点击区域 | 低于世界 Y=-9、整扇门离开画面后回到最近一次 Ground 安全落地点，不触发震屏或落石 | 代码静态已检查，PlayMode 待验证 |
| 石头互相穿透或弹飞 | 使用 Continuous Rigidbody2D、简化 PolygonCollider2D、较高摩擦和零弹性材质 | 资源静态已检查，手感待验证 |
| 落石物理数量持续增长导致移动端掉帧 | 48 作为动态刚体软上限；达到后优先把已休眠或接近静止的旧石头转成 Static 桥面，低于 Y=-12 的对象在下一批前销毁 | 性能待实测 |
| 第 5/6 次边界因协程时序算错 | 有效高处落地被接受时立即同步递增次数，并把本批完整/小石模式传入协程；前 5 次为完整 4/2/2，第 6 次起为 2 小石 | 代码静态已检查，连续操作待验证 |
| 死亡或重置后仍沿用上一局次数 | Trap、FailArea、GameReset 会立即清零并停止未完成协程；新实例 `OnEnable` 再次清零 | 代码静态已检查，PlayMode 待验证 |
| Layer13 导致角色物理上站住但程序仍判空中 | 只在 Level082 Actor 实例覆盖 `whatIsGround=8520` 与 `whatIsStuff=8448`，不修改公共 Actor | 参考 Level052 的相同局部方案，PlayMode 待验证 |
| 角色站上石头仍显示 Shadow | 13 个石头使用 `WithoutShadowGround`，Level082 Actor 实例覆盖 `ShadowController.ignoreLayer=8192` | Prefab 静态已检查，画面待验证 |
| 为控制性能而吞掉前 5 次完整落石 | 软上限不终止 SpawnGroup；前 5 次高处落地仍生成完整 4/2/2，无可冻结对象时允许短时超出动态预算 | 代码静态已检查，长时间操作待验证 |
| 冻结石头时支撑关系尚未完全稳定，形成悬空桥面 | 只选择休眠或线速度/角速度均接近零的最早石头；仍需观察复杂堆叠下的视觉合理性 | PlayMode 待验证 |
| 随机落点导致无法形成稳定桥面 | 前 5 次大/中/小按 4/2/2 分组，之后每次 2 小石补缝，落点集中在屏幕中央视口宽度 0.34 | PlayMode 待验证 |
| 重新运行 Editor 生成器改变已调好的碰撞轮廓或层 | 菜单明确为 Regenerate，脚本只在非 PlayMode 执行，并固定把新石头设为 `WithoutShadowGround`；生成后仍需重新检查碰撞与引用 | 人工流程约束 |
| 共享角色或阴影逻辑被单关需求污染 | 仅修改 Level082 prefab 的 Actor 实例 override；公共 `Actor.prefab` 内容与 Git index 一致（忽略工作树行尾差异），`ActorCtrl.cs` 与 `ShadowController.cs` 未改 | 静态已检查 |

## 验证证据

- 2026-07-20 通过当前 Unity 6000.0.59f2 编辑器刷新并编译；`Assembly-CSharp.dll` 与 `Assembly-CSharp-Editor.dll` 均于 10:51:31 更新，本次刷新域重载完成，最新 8000 行 Editor.log 中 `error CS` 与 Level082 相关 error/exception 均为 0。
- 本次 PrefabImporter 已重新导入 `Level_082.prefab` 和 13 个石头 prefab，日志没有对应资源解析错误。
- 最终门回收修正后，Unity 6000.0.59f2 于 22:06:56 重新生成运行时程序集；未变化的 Editor 生成器程序集最近一次于 21:58:09 生成。随后完成域重载，最新 3500 行日志中的 `error CS` 为 0。
- Unity 日志记录修改后的 `GamePlayPanel_082.prefab`、`Level_082.prefab`、13 个石头和物理材质均由对应 Importer 成功导入，没有命中 082 资源解析错误。
- 静态 YAML 审计确认 HUD 有 3 个拖拽项、3 个 `UIDrag`、3 个 `CanvasGroup`；Level082、HUD 与 13 个石头 prefab 的新增 fileID 无重复，本地引用闭合。
- 13 个石头已静态确认使用 Layer13、`Untagged`、Dynamic/Continuous Rigidbody2D、单路径 PolygonCollider2D 和共同的零弹性材质。
- `Level_082.prefab` 已静态确认序列化 `fullStoneWaveLimit=5`、`smallOnlyStoneCount=2`、Actor 局部掩码 `8520 / 8448 / 8192`，以及入口门实体碰撞排除掩码 `10496`。
- `Level_082.prefab`、`GamePlayPanel_082.prefab` 与 13 个石头 prefab 共 15 个 YAML 文件均无重复 fileID、无缺失本地引用；13 个石头外部引用及 Actor、StartDoor 的 source fileID 均能解析。
- `tbleveldata.json` 已包含 `10082 -> Level_082`，`tblevelsort.json` 已包含 `id 81 -> 10082`；配置由配置维护方接入，本次未修改 JSON 或 Excel。
- 2026-07-20 PlayMode smoke 在约 27 秒观察期内未离开 Loading；同期日志出现 Guru SDK 账号认证 HTTP 400，退出时出现 `UIManager.DestroyAllUI -> GameManager.ClearAll` 空引用。两处调用栈均不含 Level082，但由于本关实例未创建，不能作为本关运行通过证据。
- Loading 到 Main 启动 smoke 已通过；日志中的 GMPanel/DB 空引用发生在编辑器播放态脚本重载后，调用栈不包含 Level082，本次未改该模块。

## 待补验证

- 先恢复可进入游戏的本地账号/启动环境，再从正常入口创建 Level082；当前只完成编译、导入和静态结构验证。
- 在 Level082 内完整验证 HUD 点击与拖动、入口门开场门禁、低/高处落地、掉沟回收和震屏。
- 连续至少 7 次高处落门，确认生成量为 `8、8、8、8、8、2、2`；连续操作直至跨坑，记录动态/静态数量与帧率并确认软上限和旧石冻结生效。
- 角色站上不同大小和堆叠状态的石头，确认无阴影且仍能落地、行走、再次跳跃；确认门排除 Layer13 后不会扰乱石堆。
- 掉坑、触刺、手动重置、暂停、恢复和通关边界；确认旧协程不会在下一次尝试继续生成石头，且死亡/重置后首批恢复为完整批次。
- Android/iOS 横屏设备的单指拖拽、不同宽高比出生点、物理稳定性、CPU/帧率和内存。

## 回滚路径

只回退 Level082 的关卡 prefab、专用 HUD、本地脚本、石头 prefab/材质和 L82 资源即可。不要为回滚修改公共 `DragableItem`、`GamePlayPanel_DragButtons`、`StartDoor`、`BaseLevelCtrl` 或 Level007/012。

## 2026-07-22 音效风险补充

### 原有基线与受影响项

原有 `OnDoorLanded()` 只播放入口门的 `crash`，石头批次没有专用声音。本次仅影响 `Level82Ctrl.cs`、`Assets/Game/ArtSrc/Audio/Sfx/l82` 和 SoundsGood 的音效集合/生成表，不改变落石数量、间隔、物理、失败链或共享 `AudioManager`。

### 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 在单颗生成点播放导致 8 次近距离叠音 | 播放点位于 `SpawnStoneWave()` 批次入口，每个有效高处落门只调用一次 | 静态通过，听感待验收 |
| 低高度落门误播滚石 | 只有 `shouldSpawnStones` 为真时才启动该协程 | 调用链静态通过 |
| 重置后旧协程继续发声 | 原有 `MarkAttemptEnding()` 会 `StopAllCoroutines()`；未新增循环音效或全局停止 | 静态通过 |
| 未登记 tag 时插件回退错误声音 | `l82RockFall` 已同时登记 Collection、GUID 和生成项 | Unity 导入通过，PlayMode 待验收 |
| 快速连续落门造成两段 1.96 秒声音重叠 | 保留“每个有效批次一次”的玩法语义，不做跨批次全局去重 | PlayMode 待验收 |

### 验证缺口与回滚

音频格式、源/目标哈希、元数据、tag-GUID 映射、独立 C# 全量编译及 Unity 正式音频/Collection 导入均已通过；两个目标程序集于 15:22 重建，最近日志相关错误为 0。Unity PlayMode、暂停/恢复、失败/重开、实际音量与 Android/iOS 真机仍未验证。回滚时删除本关一次播放调用、Collection/生成表条目和 L82 音频资源，不修改共享音频管理器。
