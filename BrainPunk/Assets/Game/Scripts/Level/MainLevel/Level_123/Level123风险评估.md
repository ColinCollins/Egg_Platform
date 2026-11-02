# Level123 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：`SetPaperMode(bool)` 仅调用 `SetJumpLocked(bool)`，仓库内只有本控制器两处直接调用，Prefab/UnityEvent 未发现方法名引用。
- **直接结论**：纸片进入/退出时写入的跳跃锁状态和时机不变；无物理参数、输入、门、资源、事件或结算变化。
- **假设**：不假定仓库外二进制或反射字符串依赖该关卡私有使用语义的公共别名；当前无此证据。
- **缺失证据**：尚未执行 Unity PlayMode 的纸片转换、跳跃拦截、暂停/恢复、门吸入及 Android/iOS 输入回归，标记为 `uncertain`。
- **回滚**：恢复别名方法并改回两处调用。

## 评估范围

本评估覆盖 Level123 的按钮投放、刺墙动作、门压碎、Boxy 压扁、烟雾和纸片移动，以及本关与 075/共享 Actor/过关门的边界。结论只基于当前代码、Prefab YAML、资源 `.meta` 和静态编译证据；未把未执行的 PlayMode 假设写成已验证事实。

## 基线与风险矩阵

| 风险 | 当前控制 | 状态 |
| --- | --- | --- |
| 误触按钮导致拖拽或跳跃 | 本关私有输入组件记录整段位移，`60px` 后才拖拽；短按和拖拽在松手时互斥 | 代码/静态完成，手感待 PlayMode |
| 吸附后按钮又被拖走或再次跳跃 | 命中后按钮挂到红框，关闭拖拽；点击分支跳过 `PlayerJumpEvent` | 代码完成，待触摸验证 |
| HUD 尚未初始化导致按钮永久缺失 | 协程等待 `jump_btn` 槽位并重试；失败只告警，不隐藏不存在的共享按钮 | 静态完成，正式入口待验证 |
| 普通角色可以移动但 Spine 不播放走路 | 本关子类的 `spineCtrl` 已从误绑的 `SpriteRendererGroupAlpha` 改为 Actor 实例内 `ActorSpineCtrl`；源组件和 stripped fileID 均已静态闭合 | Unity 编译/Prefab 导入通过，PlayMode 画面待验证 |
| 开场刺墙保持静止 | `Start()` 显式调用本关 `StartCeilingIntro()`，使用 `2s / 2.5s / -3.15`；默认下降仍存在但给纸片留出地面间隙 | 调用链和后台编译通过，实际节奏待 PlayMode |
| 开场自动下降与按钮陷阱耦合纸片判定 | 两条下降都只在实际向下运动时设置同一 `ceilingDescending`；按钮只中断旧 Tween并驱动抬升/抖动/快落，不进入 `CanCrushActor` 条件 | 静态完成，Tween/物理时序待 PlayMode |
| 刺墙抬升与下坠节奏不符合策划预期 | 所有时长、强度、终点 Y 均为 Level123 Prefab 私有序列化值 | 参数可调，PlayMode 未验证 |
| 门移到刺区下方但下降时不碎 | 角色与门已拆成独立入口；门上的本关 `Level123DoorCrushTrigger` 用 Enter/Stay 接收墙体绿色块或任意刺 Collider，控制器再校验来源属于当前下降墙 | Prefab/代码静态完成，碰撞时机待 PlayMode |
| 门在齿墙落定后仍能横向拖穿，或快速拖动跨过触发区 | 本关 `LateUpdate` 从上一允许位置向拖拽目标执行门 Collider 扫掠，只接受显式绑定的 `Ground_mid` 实体面并在命中前保留 `0.01` skin；回退和落点均显式 `SyncTransforms` | 代码/Prefab 静态完成，边界手感待 PlayMode |
| 门残骸漂浮/排序错误 | 支持 `CrushedDoorSpawnPoint` 手调，缺失时才使用 `crushedDoorGroundY` 兜底；排序跟随红框层 | 静态绑定完成，画面待验证 |
| 纸片化错误依赖按钮或由刺触发 | `CanCrushActor` 只读取实际下降状态；独立绿色压区只覆盖墙体左侧无刺宽度，Enter/Stay 精确匹配本关 Actor | 代码/几何静态完成，物理时序待验证 |
| 墙体落定后仍能压扁角色或碎门 | 开场和按钮下降均在 Tween 完成或被 Kill 时立即清除下降状态，不保留旧 `0.12s` 宽限窗口 | 静态完成，低帧率终帧命中待 PlayMode |
| 刺在机关期间不再致死，或绿区先暂停刚体抢过刺回调 | 删除运行时替换/禁用处理器逻辑；六段刺保持共享 `OnTriggerTrapHandle`。压扁前用 `ColliderDistance2D.isOverlapped` 检查角色全部有效 Collider，碰刺时让共享死亡优先 | Prefab/代码静态完成，受击表现待 PlayMode |
| 合法纸片生成时因宽碰撞体擦到相邻刺而立即死亡 | 只对纸片 Collider 与缓存到的本关六段刺成对 `IgnoreCollision`，清理时恢复；普通 Actor Collider 不参与忽略 | 静态完成，纸片通行与重开恢复待 PlayMode |
| 压缩中或纸片状态仍触发跳跃/跳跃音效 | 压缩首帧即设置 `SetJumpLocked(true)` 并将跳跃力度归零，覆盖 `JumpButton` 和 `HandleLanding`；纸片切换后继续保持禁跳 | 代码完成，实际触摸音效待 PlayMode |
| 纸片仍显示影子或左右移动时翻转 | 进入纸片时停用整个 Shadow 节点并在清理时恢复初始 `activeSelf`；纸片更新分支跳过 `HandleFlip()`，普通形态继续走共享更新 | 代码/后台编译完成，视觉与重开待 PlayMode |
| 烟雾特效覆盖不足、排序错误或实例残留 | 直接引用用户导入的 `SmokeExplosionWhite`，实例排序提升到角色目标层上方；所有子粒子使用非缩放时间并设 `StopAction.Destroy` | GUID 闭合，实际覆盖/性能待验证 |
| 纸片惯性与标准 Actor 移动或过关吸入互相覆盖 | 本关 `LateUpdate` 在 Actor 更新后写入水平速度，独立保存 `paperVelocityX`；暂停、死亡、进门、成功/失败时清零刚体 X 速度并停止写回 | 代码完成，移动手感/吸入待验证 |
| Editor 中键盘轮询覆盖 GamePanel 移动 | Level123 本地角色组件的 `IsEditor=0`，与 Level075 的 Prefab 覆写一致 | Prefab 完成，PlayMode 待验证 |
| Reset/重开后共享按钮状态被污染 | 记录并恢复共享 jump 槽位 active、enabled、Interactable、旧 up 槽位状态；清理会 Kill Tween/协程 | 静态完成，重开实测待验证 |
| 正式选关找不到 Level123 | 当前表文件搜索不到 `100123/Level_123`；本轮保留用户配置改动，未写入新行 | `uncertain`，需配置维护方确认 |

## 资源与隔离检查

- `Level_075.prefab` 没有 Git 差异；共享 `Actor.prefab`、`ActorCtrl`、`GamePlayPanel`、`DragableItem` 和 075 粒子没有被本轮修改。
- Level123 新脚本只位于 `Assets/Game/Scripts/Level/MainLevel/Level_123/`，新增烟雾资源位于 `Assets/Epic Toon FX/Prefabs/Environment/Smoke/White/`；纸片贴图位于 `Assets/Game/ArtSrc/Sprites/l123/`。
- Prefab fileID 静态闭包通过；新增门触发组件的本地 fileID、脚本 GUID 和控制器引用均闭合，绿色压区绑定保持不变。
- `doorDragBarrier` 指向 `CaltropGround/Ground_mid` 的本地 fileID `8447241844912748476`；目标是启用中的非 Trigger `BoxCollider2D`。门根 Trigger、Kinematic Rigidbody2D 和原压碎组件保持不变。
- 最终脚本已用 Unity 6000.0.59f2 自带 Roslyn 在隔离输出目录后台编译，结果为 `0 error`，Level123 诊断为 `0`。
- 用户当前 Unity 进程已自行完成最新脚本的 Tundra 构建和 `Level_123.prefab` 导入；最近构建区间无 `error CS`，AssetImportWorker 成功生成 artifact `baa2e147769d8f9994c586aa28169171`。日志中的材质 Drawer 和重复 Atlas 是预览警告，未出现本关 Prefab import failed；该证据仍不替代 PlayMode 物理与画面验证。
- 本次门触发修复后再次静态扫描：Prefab 为 `288` 个唯一 fileID 定义、`595` 次内部引用和 `288` 个唯一内部目标，重复/缺失均为 `0`；新脚本 GUID 仅由自身 `.meta` 定义并由门组件引用。Unity 自行生成的最新 `Assembly-CSharp.rsp` 已包含 `Level123Ctrl`、`Level123CrushZone` 和 `Level123DoorCrushTrigger`，随后 Tundra 构建 success 且未记录 `error CS`。
- 本次纸片视觉修正删除 `paperShadowWidth`、影子反射写入及比例缩放，只在 Level123 私有控制器停用 Shadow 根节点；纸片角色分支也只省略共享翻转调用。Unity 6000.0.59f2 自带 Roslyn 隔离后台编译为 `0 error`，Prefab 静态闭包仍为 288 个唯一定义且无缺失引用。
- 门拖拽扫掠最终脚本使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp.rsp` 隔离后台编译，结果 `exit 0 / 0 error / Level123 diagnostics 0`；Prefab 为 288 个唯一定义、596 次本地引用、288 个唯一目标，重复/缺失均为 0。编译未修改工程 `Library` DLL/ref DLL。

## 验证缺口

尚未启动或操控新的 Unity 前台，也未执行 PlayMode，因此以下项目必须由后续人工/专用验证环境补齐：普通形态 `walk` 动画及影子/翻转回归、开场 `2s` 后的可见墙体下降、默认下降和按钮快落各自命中绿色区、普通形态碰刺死亡、合法纸片忽略六段刺且重开后恢复碰撞、纸片切换后无影子且左右移动固定朝向、门在绿色块及各段刺下方随下降压碎、墙高位允许门从下方通过、墙落定后慢拖/快速甩动均不能穿越、最终地面间隙、烟雾覆盖与排序、过关门吸入、禁跳音效、暂停/重开、多分辨率安全区、Android/iOS 横屏及多指触摸。

## 回滚路径

优先回滚本关文件和本地文档，不回滚用户已有无关改动：删除 `Level_123.prefab`、`Level_123` 脚本目录、`l123` 纸片资源和导入的烟雾 Prefab；撤销共享记录末尾的 Level123 条目即可。若只回滚本次门拖拽阻挡，删除 `doorDragBarrier` Prefab 绑定和控制器中的扫掠字段、初始化及 `LateUpdate` 调用；不得改共享 `DragableItem` 或 Layer Matrix。若只回滚纸片视觉修正，恢复本关纸片分支的 `HandleFlip()` 和旧影子逻辑；若只回滚门判定修正，则移除 `Level123DoorCrushTrigger` 及对应绑定。
