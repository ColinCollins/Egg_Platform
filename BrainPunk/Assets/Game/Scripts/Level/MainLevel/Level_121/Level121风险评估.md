# Level121「金钱的力量」风险评估

## 结论

本轮采用关卡局部派生控制器、垂直金币雨脚本和 Prefab override，不修改 Level31、共享门、门锁、金币、Actor、影子或相机脚本。门实例已进入 Actor 原生 Stuff/地面判定，实体碰撞框向门画面内收；Actor 影子平时保持默认，只在主 Collider 实际踩住门顶时由本关控制器临时隐藏，`CameraFollowArea` 组件与开关双重关闭；气泡改为等待入场擦除完成事件后才开始动画。门体增加质量、阻尼和限速；锁的左右倾斜由金币命中位置决定，第二击幅度放大，第三击当帧停止并清空仍在下落的金币。静态引用闭合；实际首次入场、门顶影子切换、碰撞边界、固定镜头、推动、锁姿态与移动端表现尚未 PlayMode 验证，状态为 `uncertain`。

## 原行为与影响面

- 原 Level121 是未登记到 `tbleveldata.json` 的场景 Prefab，沿用 Level31 的持续金币雨、金币拾取飞入、10000 内存计数和计数归零旧行为。
- 本次影响文件为 `Assets/Game/Prefabs/Level/Level_121.prefab`、三个 Level121 私有脚本及本地文档。
- 复用系统包括 `Level31Ctrl`、`Level31CoinTriggerHandle`、`ThrowItemHandle`、`GoldCoin.prefab`、`door_0.prefab`、`DoorLock.prefab`、`ActorSpineCtrl`、DOTween、`BaseLevelCtrl` 标准进门成功链。
- 未修改 GF 子模块、Luban 表、运行时配置、DB/Save、全局事件、HUD、分析打点、广告/IAP 或 Android/iOS 平台配置。

## 风险项

| 风险 | 控制措施 | 当前证据 |
| --- | --- | --- |
| 改 Level31 导致已验收关卡回归 | 新控制器继承 Level31，只在 Level121 Prefab 替换脚本 GUID；Level31 文件不改 | Git/文件静态核对通过 |
| 金币模板字段要求 `Level31Ctrl` | `Level121Ctrl : Level31Ctrl`，维持既有序列化引用类型；不改共享 `GoldCoin.prefab` | Unity 编译与 Prefab 导入通过；运行时拾取待验 |
| 门体自由下坠、旋转或过度滑行 | 刚体 Gravity 0，冻结 Y/Rotation；二次调参后质量 `12`、阻尼 `36`、X 限速 `1.5`、低于 `0.12` 归零 | Prefab/代码静态通过；是否过重及可推到落币线下待验 |
| Actor 尚未贴到门图形就提前碰撞 | 只对 Level121 右门实例把源 BoxCollider2D 从 `2.5446355 × 3.045598 / OffsetY -0.104367256` 收紧到 `2.1 × 2.5 / OffsetY -0.38`，底边基本不变 | override 静态通过；承托和侧推边界待验 |
| 角色站门顶时仍播空中动作 | 门根实例从 Layer 3 改为 Stuff 8，同时被 Actor `whatIsGround=320` 和 `whatIsStuff=256` 覆盖 | LayerMask 静态核对通过；顶部/侧面动作待验 |
| 影子被整关关闭，或侧推门时误隐藏 | 移除 Actor Shadow 的整关 inactive override；仅当玩家主 Collider 与门实体真实接触、位于门上方、横向重叠且脚底贴近门顶 `0.12` 时，临时关闭 Shadow 根 | 代码和绑定静态通过；跳上/跳下/侧推边界待验 |
| 门顶隐藏后影子无法恢复或误打开 | 首次进入门顶状态时缓存 Shadow 当时的 `activeSelf`；离开门顶、门切 Trigger 或控制器停用时只恢复该缓存状态 | 生命周期分支静态通过；成功/Reset 并发待验 |
| 本关仍跟随 Actor 移动镜头 | 在 Level121 GameCamera 实例上同时设置 `CameraFollowArea.m_Enabled=0` 与 `canFollow=0`，不关闭 Camera 或画幅适配 | Prefab 静态通过；PlayMode 固定构图待验 |
| 锁与门脱离 | DoorLock 保持为 `door_0` 根 Transform 子节点，命中 Trigger 随门移动 | Prefab 层级静态通过 |
| 仍出现 Knock 英文字样 | 本关现有 `Knock_Explosion` 已移除与 `Knock _Word` 同材质的 `Knock 1` 子节点，代码只播放根 ParticleSystem | 资源 GUID/RemovedGameObject 静态通过 |
| 一枚金币重复计次 | 只在 DoorLock 的 `OnTriggerEnter2D` 计次，金币按单条垂直轨迹穿过触发区 | 物理重叠/门移动边缘待 PlayMode 验证 |
| 第三击时继续触发 | 第三击先置完成门禁并关闭锁 Collider，再播放下滑渐隐 | 代码静态通过 |
| 锁掉后天空仍继续落币 | `PlayLockRelease` 当帧调用本关雨控件停止生成，并隐藏仍带有效 Collider 的下落/落地金币 | 私有脚本与 Prefab 引用静态通过；实际同帧观感待验 |
| 停雨误删已经拾取的金币 | Collider 已关闭表示金币进入飞向气泡的拾取链，清理时跳过该类金币 | 分支代码静态通过；快速拾取与第三击同帧待验 |
| 隐藏金币过早回池后受旧 Tween 干扰 | 延迟 `2.6s` 回池，覆盖本关 ThrowHandle 当前 `2.5s` 下落时长；组件短暂禁用后按剩余时间恢复回收协程 | 参数静态通过；Reset/Replay 待验 |
| 锁始终向固定方向歪 | 用金币 Collider 中心与锁中心 X 差决定左右；中心死区交替方向 | 代码静态通过；左右命中观感待验 |
| 第二击姿态不明显 | 第一击 `12°`，第二击使用 `1.8x` 即 `21.6°` | 参数绑定通过；视觉幅度待验 |
| 未解锁先进入门 | 锁定时门 Collider 为非 Trigger；锁消失完成后才切回 Trigger | Prefab与代码静态通过，时序待验 |
| 气泡缩放改变最终布局 | Pivot 修改前后用 World Corners 补偿位置，最终 Scale 恢复原值 | 代码静态通过；不同分辨率待目视 |
| 气泡入场被擦除遮罩挡住 | Level 创建时先保持 Scale/Alpha 0，监听擦除完成后的 `SwitchGameStateEvent(PLAYING)`，再延迟 `0.25s` 播放；`2.5s` unscaled fallback 只用于直接运行或事件缺失，布尔门禁阻止双播 | 代码静态通过；首次正常入口与 Reset/Replay 待验 |
| 推门时气泡拖影或抖动 | `LateUpdate` 读取门的最终世界位置，只叠加独立正弦浮动偏移 | 代码静态通过；物理插值观感待验 |
| 金币仍水平偏移或带抛物线 | 原自动投放关闭，`randomXRange=0`、`jumpPower=0`，生成点 X 与落点 X 一致 | Prefab 参数与新脚本静态通过 |
| 隐藏人物连带关闭金币池 | 只关闭 `Enemy` 和 `Plantform_long` 嵌套实例，`CoinRainRoot/ThrowHandle` 保持激活 | Prefab 层级与引用静态通过 |
| 普通流程无法加载 Level121 | `tbleveldata.json` 暂无 `10121 / Level_121`，本轮没有推断并覆盖用户正在编辑的配置 | 已确认缺少登记，接入状态 `uncertain` |
| 物理/粒子带来移动端开销 | 仅新增一个刚体、一个触发回调和一个简单时间比较 Update；复用现有粒子与金币对象池，不新建金币实例化链 | 静态评估低风险；真机帧率未测 |

## 验证证据

- Unity Editor 日志：`Assembly-CSharp.dll` 编译、复制、Domain Reload 成功，随后 `Level_121.prefab` PrefabImporter 成功。
- 本轮停雨代码使用 Unity 自带 Roslyn 和当前 Bee 响应文件隔离编译；完整编译仅被无关 Level122 的缺失类型阻断，排除 Level122 私有目录 5 个源码条目后 `exit 0`，Level121 无编译错误。
- 首屏气泡事件、碰撞框、门顶影子和固定镜头修改后再次运行同一 Level121 隔离编译，结果为 `exit 0 / 0 error / 109 warning`；警告均为项目既有警告。
- 门体二次加重后再次隔离编译为 `exit 0 / 0 error / 109 warning`，并逐项确认代码默认值与 Prefab 序列化值均为 `12 / 36 / 1.5 / 0.12`。
- Prefab 静态检查：根控制器、门刚体、收紧后的门 Collider override、DoorLock 接收器、3 次参数、无文字命中特效引用均已绑定；Actor Shadow 使用默认 active 且 `actorShadowMarker` 指向其 SpriteRenderer，`CameraFollowArea` 组件及 `canFollow` 均关闭。
- Prefab 引用检查：254 个本地 `fileID` 定义唯一；534 处本地引用覆盖的 254 个唯一 ID 全部可解析，`coinRain` 与 `actorShadowMarker` 绑定均唯一。
- 落币链检查：`CoinSpawnLine.p1/p2 -> Level121VerticalCoinRain -> ThrowItemHandle`绑定闭合；根控制器也显式引用雨控件；旧路径移动/自动投放关闭，水平偏移和弧高均为 0。
- 源资源检查：`Knock_Explosion/Knock 1` 与 `Knock _Word` 使用同一材质 GUID；Level121 实例的 `m_RemovedGameObjects` 包含该 `Knock 1`。
- 范围内源码/文档尾随空格检查和中心功能记录的 `git diff --check` 均通过；Unity 最新相关日志段无 C# 或 Prefab 导入错误。
- 本轮定向检查确认 254 个本地 `fileID` 定义唯一、534 处本地引用无缺失，四条门 Collider override 均唯一；本轮未编辑共享 `door_0.prefab`、Actor、`ShadowController.cs` 或 `CameraFollowArea.cs`，共享 Actor 当前内容哈希与 Git 索引一致。

## 未验证清单

- Unity PlayMode：首次进入气泡动画必须在擦除遮罩后完整可见且只播一次；门碰撞不能提前，同时门顶 idle/walk、侧面 push 和承托可靠；Actor 普通状态保留影子、仅踩门顶隐藏并能恢复，镜头保持固定；另包括门质量 `12`、阻尼 `36`、限速 `1.5`、低速阈值 `0.12` 下的启动力度、持续推动速度、松手刹停及能否推到落币线下，锁左右命中和第二击幅度、第三击当帧停雨/清空与已拾取金币保留、垂直落币、拾取减数、进门成功、失败/重开/退出。
- Android/iOS 横屏：触控推动、不同安全区/宽高比下气泡位置、物理稳定性与 60 fps。
- Level121 正式配置登记、选关/主线入口、解锁/存档和关卡打点；当前均为 `uncertain`。

## 回滚路径

回滚 `Level_121.prefab` 的根控制器 GUID、新增序列化字段、CanvasGroup、门 Rigidbody、Collider override 和 DoorLock 接收器，恢复 `MoneyGod/FlyPath/ThrowHandle` 实例状态与抛物线参数，并删除 Level121 私有脚本。只回滚本次门手感调整时，把质量/阻尼/限速/低速阈值恢复为 `8 / 24 / 2 / 0.08`；只回滚影子澄清时，删除 `actorShadowMarker`、`actorDoorContactTolerance` 与门顶影子检测/恢复代码，保持 Actor Shadow 默认激活。由于没有修改共享 Prefab、配置、存档或事件，不需要跨关卡数据迁移。
