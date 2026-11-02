# DLevel_004 石头碰撞特效定位功能与风险评估

## 1. 需求与背景

用户反馈 DrawGame 第 4 关中，落下的石头碰到角色时，`Knock_Explosion` 击中特效位于
角色根节点而与石头接触位置存在视觉偏差。本次仅调整 DLevel_004 的命中特效坐标：
特效必须在石头与角色的实际 2D 物理接触点播放。

## 2. 原行为基线与原因

- DLevel_004 的 10 个石头均使用非 Trigger 的 `PolygonCollider2D` 与 `Rigidbody2D`，并启用
  共享 `OnTriggerTrapHandle.isCollision`。
- 共享 `OnTriggerTrapHandle.OnCollisionEnter2D(...)` 只将 `collision.collider` 传入后续流程；
  `PlayKnock(...)` 固定执行 `knock.transform.position = actor.transform.position`。
- 因此粒子总是位于角色根节点，而不是本次石头碰撞的接触点。该共享组件同时用于
  DLevel_005、DLevel_008、Caltrop 与多个主线关卡，直接修改会扩大既有关卡回归范围。

## 3. 修改后行为

1. 新增由 DLevel_004 首次使用的 `DLevel4RockTrapHandle`；玩家身份以碰撞体父级
   `ActorCtrl` 是否等于宿主绑定 Actor 为准，并保留死亡、向上击退、`trap` 音效、0.5 秒
   失败事件和销毁取消逻辑。
2. 石头的 `OnCollisionEnter2D(...)` 在确认碰撞对象为 Player 后，优先取得
   `collision.GetContact(0).point` 作为特效世界坐标的 XY。
3. 粒子保留原有 Z 坐标，避免改变现有渲染排序；Unity 未提供 contact 时才以玩家 Collider
   对石头中心的 `ClosestPoint(...)` 作为安全回退。
4. DLevel_004 中 10 个石头组件均已切换到本地处理器，并继续引用同一个既有
   `Knock_Explosion` ParticleSystem；没有新增运行时实例化、资源地址或粒子资源。

## 4. 影响范围与边界

- 修改：`Level4/DLevel4RockTrapHandle.cs`、其 `.meta`，以及
  `Assets/Game/Prefabs/DrawLevel/DLevel_004.prefab` 的 10 个石头组件引用。
- 不修改：共享 `OnTriggerTrapHandle`、`ActorCtrl`、`DrawLevelCtrl`、`DrawLineFeature`、
  DB/存档、Luban、资源加载、UI、打点、广告、SDK 或平台配置。
- 事件：继续复用既有 `OnTriggerTrapEvent`，不新增 `PlayEvents` 或全局事件。

## 5. 验证证据

- 静态检查：DLevel_004 prefab 中本地脚本 GUID 出现 10 次，旧共享 trap GUID 在本关出现
  0 次；10 个组件均保留原有 `actor`、`knock` 和 `isCollision: 1` 序列化值。
- 源码检查：本地 Collision2D 路径含 `contactCount > 0` 门禁与
  `collision.GetContact(0).point`；仅 Trigger 兼容路径保留角色根节点坐标。
- Unity 编译：当前 Unity Editor 日志显示 `*** Tundra build success (5.15 seconds)`，未见
  `DLevel4RockTrapHandle` 或 DLevel_004 的 C# 编译错误；工程原有 warning 未在本次处理。
- `git diff --check`：新增 C# 与 meta 通过。DLevel_004 prefab 当前已有工作区的大范围未提交
  调整，Unity 空序列化字段的行尾空白会使该文件返回警告；本次仅替换 10 个 `m_Script` GUID，
  未清理或改写其它 prefab 内容。

## 6. 剩余风险与未验证项

- Unity PlayMode 尚未直接观察石头从不同方向接触角色时的实际粒子落点、失败时序与重开。
- Actor 层级新增的激活子 Collider 会按同一玩家处理；这是复合角色 Collider 的预期语义，
  但新增几何范围仍需在对应宿主关卡验证。
- Android/iOS 横屏尚未验证粒子排序、触控流程和多石头物理碰撞时的帧率。
- 命令行 `dotnet build` 未执行成功：当前环境没有可用 .NET SDK；Unity Editor 的本次编译
  结果作为替代证据。

## 7. 回滚路径

1. 将 DLevel_004 的 10 个石头组件重新绑定为共享 `OnTriggerTrapHandle`。
2. 只有同时移除 DLevel_016 对该脚本的引用后，才删除 `DLevel4RockTrapHandle.cs` 与对应 `.meta`；
   若只回滚 DLevel_004，则保留脚本供 DLevel_016 使用。
3. 删除或回退本文、DLevel_004 结构记录和 DrawGame 演进记录中的本次条目。

共享陷阱组件、角色、事件、资源、存档和其它关卡无需回滚。

## 8. 2026-08-02 DLevel_016 复用更新

- DLevel_016 的 Stone 与 Actor 均为普通非 Trigger Collider，原 OnTrigger2DHandle 无法响应；
  当前 Stone 直接复用 DLevel4RockTrapHandle，并独立绑定本关 Actor 与既有 Knock 粒子。
- 初次复用只泛化 XML 职责注释；同日为 DLevel_016 的复合角色 Collider 修正运行时目标识别：
  从命中 Collider 向父级解析 ActorCtrl，并要求等于序列化绑定 Actor。DLevel_004 的 10 个
  根 Player Collider 仍满足该条件，绑定、contact point 与失败时序不变。
- DLevel_016 prefab 导入、Assembly-CSharp 编译及 136 个 YAML 锚点/引用检查通过；两关完整
  PlayMode 与移动端碰撞表现仍待验收。

## 9. 2026-08-02 DLevel_016 推线命中复合 Collider 更新

- DLevel_016 启用了共享 Actor 右侧的 `pushCollider`；它属于同一 Actor 层级并位于 Actor
  Layer，但自身 Tag 为 `Untagged`。Stone 从右侧推动运行时线时会先撞到该辅助 Collider，
  旧“碰撞体自身必须是 Player Tag”的门禁会漏判；直接画线命中主体则正常。
- 当前处理器以 `collider.GetComponentInParent<ActorCtrl>() == actor` 为唯一目标身份条件，
  因此主体和辅助 Collider 都只会命中当前宿主明确绑定的 Actor。不修改 `pushCollider` Tag，
  避免成功门、水区、FailArea 等共享 Player Tag 触发器扩大或重复响应。
- 本次只修改复用处理器和文档，不修改两关 prefab、共享 Actor、Layer 矩阵或 Physics2D
  IgnoreCollision；Unity 记录 `*** Tundra build success (4.00 seconds)` 与 CopyFiles，完整
  Stone 直撞、直接画线、Stone 推线和移动端仍待 PlayMode 验收。
- 单独回滚时可恢复 Collider 自身 Layer/Tag 门禁，但会重新引入 DLevel_016 的
  `Untagged pushCollider` 漏判；DLevel_004 的绑定、资源和 prefab 无需回滚。
