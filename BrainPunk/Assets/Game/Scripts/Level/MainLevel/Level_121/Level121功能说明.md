# Level121「金钱的力量」功能说明

## 需求与范围

Level121 在现有海边场景、金币雨和 10000 金币气泡基础上增加独立过关机制：玩家把右侧门推到金币雨落点下，金币连续三次砸中门锁后，锁逐次倾斜下沉，并在第三次下滑、渐隐；锁消失后门恢复标准入口 Trigger，玩家进门通关。首轮试玩后追加：气泡必须明显入场、持续浮动并跟门，破锁后动画消失；天上移除人物和平台，金币改为从横线随机点完全垂直落下；锁被第三击砸掉后立即停止金币雨，不再保留空中落币。二轮试玩继续要求：首次进入时必须看见完整气泡动画；收紧门的实体碰撞范围；本关不启用镜头跟随。影子要求经用户澄清为：地面和其它位置照常显示，只在玩家实际踩住可推动门顶时隐藏。后续试玩反馈门仍偏滑，要求进一步增加推动重量并缩短松手滑行。

本轮只修改 `Level_121.prefab` 并新增 Level121 私有脚本。已验收的 Level31 脚本、`GoldCoin.prefab`、`door_0.prefab`、`DoorLock.prefab`、`Knock_Explosion.prefab`、共享 Actor、HUD、存档、配置、打点和平台 SDK 均未修改。

## 修改前基线

- `Level_121.prefab` 直接挂载 `Level31Ctrl`，`defaultScore=10000`。
- 原 `MoneyGod` 沿 `FlyPath` 往复移动，`ThrowHandle` 以 `0.3s` 间隔按随机水平偏移和弧线高度 `2` 抛出既有 `GoldCoin`；金币被 Actor 吃到后仍通过 `Level31CoinTriggerHandle` 飞向气泡，并按金币/银币数值调用 `CutdownScore`。
- 右门沿用 `door_0`，其入口 Collider 在本关实例中为禁用；`DoorLock` 已作为右门子节点存在。
- 场景已有 `Knock_Explosion`。该实例已经移除源 Prefab 的 `Knock 1` 子节点；该子节点材质 GUID 与 `Knock _Word.prefab` 一致，因此保留的根粒子不含英文 Knock 文字。

## 当前行为

### 金币气泡

- `Level121Ctrl` 继承 `Level31Ctrl`，保持现有金币计数和拾取链的序列化类型兼容。
- 进入关卡创建 Level121 时，先把 World Space `Canvas` 整组气泡设为激活但 Scale 0、Alpha 0。控制器监听入场擦除遮罩完成后派发的 `SwitchGameStateEvent(PLAYING)`，收到后再等待 `0.25s`，从右下 Pivot 使用 `Ease.OutBack` 在 `0.42s` 内放大到原有 `0.03` 并同步淡入，避免动画在遮罩后方提前播完。直接运行 Prefab 或异常漏发事件时，使用 `2.5s` unscaled fallback 启动；事件与 fallback 只允许一个入口生效。
- 气泡保留与门的初始世界偏移，`LateUpdate` 跟随右门位置，同时以高度 `0.12`、周期 `1.6s` 循环上下浮动。
- 第三击门锁完成下滑渐隐后，气泡在 `0.28s` 内缩小到 `35%` 并淡出，然后关闭整组 Canvas。
- 数字由基类 `Start()` 初始化为 `10000`；玩家吃到金币后的减数与飞字继续使用原实现。

### 垂直金币雨

- 原 `MoneyGod` 人物和脚下 `Plantform_long` 仅在 Level121 实例中关闭；对象根重命名为 `CoinRainRoot`，共享人物/平台 Prefab 不改。
- 原 `FlyPath` 重命名为 `CoinSpawnLine`，关闭旧的往复路径组件，复用其 `p1/p2` 作为同高的横线两端；`Level121VerticalCoinRain.OnDrawGizmos` 在 Scene 视图绘制黄色线和端点。
- `Level121VerticalCoinRain` 每 `0.3s` 在横线 X 范围随机选一点。旧 `ThrowItemHandle` 仅保留池化、缩放和回收，其自动生成已关闭，弧线高度与水平落点偏移均为 `0`，因此金币在 `2.5s` 内从随机点完全垂直落到世界 Y=`-3`。
- 第三次有效命中触发锁掉落的同一帧，`Level121Ctrl` 调用雨控件停止后续生成，并立即隐藏仍带有效 Collider 的下落金币或落地渐隐金币；这些对象等待 `2.6s`、覆盖既有 `2.5s` 下落 Tween 后再安全回收到原对象池，避免隐藏对象过早复用时被旧 Tween 继续改位置。已经被 Actor 拾取、Collider 已关闭且正在飞向气泡的金币继续完成原拾取计数动画。

### 可推动门

- 试玩确认门原为 Layer 3，不在 Actor 的 `whatIsGround=320` 和 `whatIsStuff=256` 中：角色物理上站在门顶时却被动画状态机当成空中，从侧面也不会进入标准 Stuff 推物判定。本关门实例现改为 Stuff Layer 8，站立时正常 idle/walk，侧面移动时走 Actor 原生 `push` 动作与推物碰撞。
- 右门实例局部新增 Dynamic `Rigidbody2D`，经二次手感调整后质量为 `12`、Linear Damping 为 `36`，冻结 Y 与旋转；水平速度上限从 `2` 降至 `1.5`，低速归零阈值从 `0.08` 提至 `0.12`。相比上一版 `8 / 24 / 2 / 0.08`，门更难被碰撞惯性带走，推动速度更慢，松手后更快停止。
- 右门原入口 Collider 在锁存在时启用并切为非 Trigger，作为实体门碰撞；本关实例把源门矩形从 Size `2.5446355 × 3.045598`、Offset Y `-0.104367256` 收紧为 Size `2.1 × 2.5`、Offset `(0, -0.38)`。底边基本保持原位置，上边和左右边向门画面内收，减少 Actor 尚未贴到门图形就发生碰撞；未修改共享门 Prefab。
- `DoorLock` 仍挂在门根节点下，门移动时锁和命中区域同步移动。
- 玩家 Actor 自带的 `Shadow` / `ShadowController` 恢复默认激活；`Level121Ctrl` 通过该影子节点的 SpriteRenderer 标记找到玩家主 Collider。只有主 Collider 与锁定中的门实体 Collider 确实接触、玩家中心位于门上方、横向有重叠，并且脚底与门顶距离在 `0.12` 世界单位容差内时，才临时关闭整个 Shadow 根节点。
- 玩家从门顶跳开、走下、门解锁切成 Trigger 或关卡控制器停用时，恢复进入门顶状态前的影子激活状态。侧面推门不会隐藏影子，也不会把其它系统原本关闭的影子错误重新打开；共享 Actor Prefab 与 `ShadowController.cs` 不修改。

### 固定镜头

- 仅在 `Level_121.prefab` 的 GameCamera 实例上关闭本地 `CameraFollowArea` 组件，并把其 `canFollow` 同时设为 `false`；本关运行中不再根据 Actor 的 X/Y 位置移动镜头。
- GameCamera 本身和其它画幅适配组件保留，未修改共享相机 Prefab 或 `CameraFollowArea.cs`，其它关卡仍按各自配置跟随。

### 三次金币破锁

1. `Level121LockHitReceiver` 只接受带 `Level31CoinTriggerHandle` 的金币 Collider，不响应 Actor 或其它物件。
2. 每次有效命中把本关现有无文字 `Knock_Explosion` 移到金币 Collider 的实际命中中心，清理旧粒子后重新播放根粒子。
3. 门锁根据金币命中中心相对锁中心的 X 方向决定倾斜：砸左侧时向左旋，砸右侧时向右旋；命中落在 `0.08` 中心死区内时与上一次方向交替，避免永远朝同一边。第一次倾斜 `12°`，第二次使用 `1.8` 倍即 `21.6°`，下沉仍为 `0.12 / 0.24` 世界单位。
4. 第三次命中立即关闭锁的 Trigger，锁在 `0.45s` 内额外下滑 `0.9` 世界单位，并通过 `ActorSpineCtrl.SetAlpha` 同步渐隐。
5. 第三次命中当帧停止并清空金币雨；锁动画完成后隐藏锁，门刚体切为 Kinematic 并冻结，原实体 Collider 切回 Trigger。后续继续走 `BaseLevelCtrl.OnActorTrigger2D -> CheckFinished -> PlaySuccessAnim` 的标准进门通关链。

## 绑定与数据流

| 对象/引用 | 用途 |
| --- | --- |
| `Level_121/Canvas` | 气泡整组入场、跟门浮动、破锁退场，同时保留金币飞字父节点 |
| `Level_121/CoinSpawnLine` | 黄色 Scene Gizmo 横线和随机垂直落币范围 |
| `Level_121/CoinRainRoot/ThrowHandle` | 复用金币对象池，关闭原自动抛物线生成 |
| 右侧 `door_0` 根 Collider | 锁定时实体阻挡，解锁后入口 Trigger |
| 右侧 `door_0` 新增 Rigidbody2D | 只允许玩家水平推动门 |
| `door_0/DoorLock` | 三次金币命中 Trigger、锁姿态和渐隐对象 |
| `Level_121/Knock_Explosion` | 无 Knock 文字的通用命中粒子 |
| Actor 自带 `Shadow` SpriteRenderer 标记 | 平时保持默认影子；仅用于定位 Shadow 根和玩家主 Collider，踩住门顶期间临时关闭根节点 |
| GameCamera/`CameraFollowArea` | 组件与 `canFollow` 双重关闭，保持固定镜头 |

金币数字只保存在本关控制器内存，不写入玩家真实金币，不访问 DB/Save，不新增 `PlayEvents` 或全局事件。成功、星星、解锁、存档和打点仍由既有 `BaseLevelCtrl` 链路负责。

## 验证记录

- Unity 6000.0.59f2 已自动重新编译 `Assembly-CSharp`、完成 Domain Reload，并成功重新导入 `Level_121.prefab`；最新日志段无 C# 编译错误或 PrefabImporter 错误。
- 本轮停雨代码使用当前 Unity/Bee `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn 隔离复编译：完整响应文件被工作区另一个正在编辑的 Level122 缺少 `Level122CloseButton` 阻断；仅排除 Level122 私有目录的 5 个源码条目后为 `exit 0`，生成 `Temp/CodexLevel121Check/Assembly-CSharp-Level121.dll`，Level121 无编译错误。
- 本轮首屏气泡事件、固定镜头和碰撞边界修改后复用同一隔离响应文件再次编译，结果为 `exit 0 / 0 error / 109 warning`；警告均为项目既有警告。
- 门体二次加重后再次运行 Level121 隔离编译，结果仍为 `exit 0 / 0 error / 109 warning`；代码默认值与 Prefab 的 `12 / 36 / 1.5 / 0.12` 四项序列化值逐项一致。
- 静态核对：Level121 根脚本 GUID 指向私有 `Level121Ctrl`；金币模板对控制器的引用仍指向该派生组件；门 Rigidbody、实体/Trigger Collider、DoorLock 命中接收器与粒子引用均存在。
- Prefab 完整性检查：254 个本地 `fileID` 定义全部唯一；`coinRain` 与新增 `actorShadowMarker` 显式绑定后共 534 处本地引用，覆盖的 254 个唯一 ID 均无缺失。
- 定向核对：Actor 源 `Shadow` 的整关 inactive override 已移除，控制器 `actorShadowMarker` 指向玩家 Shadow SpriteRenderer；`CameraFollowArea.m_Enabled=0` 且 `canFollow=0`；源门 Collider 的四条 Offset/Size override 均唯一且数值正确。本轮未编辑共享门、Actor、影子或相机文件；共享 Actor 当前内容哈希与 Git 索引一致。
- 静态核对：`Knock_Explosion` 实例继续移除 `Knock 1` 文字子节点，共享粒子 Prefab 未修改。
- 范围内源码/文档尾随空格检查通过；中心功能记录的 `git diff --check` 通过。
- `dotnet build` 不可用，因为本机未安装 .NET SDK；已改用当前打开的 Unity 6000.0.59f2 实际编译/导入日志作为编译证据。

## 待 PlayMode 验收

- 首次正常选关进入时，气泡是否在入场擦除遮罩完全退场后再从右下角自然放大，10000、金币图标与背景是否同步；直接运行 Prefab、Reset/Replay 时 fallback 是否只播放一次。
- 横线范围内随机分布、垂直轨迹、落币密度与锁 Trigger 命中率是否符合关卡节奏。
- 第三击后是否当帧停止生成并清空仍在落下的金币，同时不截断已经拾取的飞入气泡动画。
- Actor 在普通地面、空中和侧面推门时影子是否照常显示，只有脚底确实踩住门顶时隐藏，并在跳开/走下/解锁时恢复；门顶 idle/walk 与侧面 `push` 动作是否正确。收紧后的 `2.1 × 2.5` 门碰撞框是否既不提前碰撞，也能可靠承托/推动；质量 `12`、阻尼 `36`、限速 `1.5` 和低于 `0.12` 归零是否达到“推得动、松手不溜”的预期手感。
- Actor 在关卡内左右、上下移动时 GameCamera 是否保持固定，宽高比适配是否仍正确。
- 金币落点与移动后的 DoorLock Trigger 是否能稳定形成三次独立命中；快速连续命中时三段姿态是否清晰。
- 左/右/中心命中的锁倾斜方向与第二击 `21.6°` 幅度是否符合画面预期。
- 第三次下滑渐隐、Collider 切换与进门成功动画是否无穿模、无提前触发。
- Reset/Replay、暂停中 Tween、Android/iOS 横屏比例和低端机物理表现均为 `uncertain`。
- 当前运行时 `tbleveldata.json` 尚无 `10121 / Level_121` 记录；本轮未改动正在编辑的 Luban/运行时配置。普通选关/主线能否进入 Level121 为 `uncertain`，需要在关卡排序接入阶段补齐并生成配置。

## 回滚

完整回滚时，把 `Level_121.prefab` 根控制器恢复为 `Level31Ctrl`，删除右门实例新增 Rigidbody2D、DoorLock 上的 `Level121LockHitReceiver` 及新增字段/Collider override，恢复 `MoneyGod/FlyPath/ThrowHandle` 原有启用和抛物线参数，删除 CanvasGroup 与 `Level121VerticalCoinRain`，并删除 `Level_121` 私有脚本目录。只回滚本次门手感调整时，把代码默认值和 Prefab 实例的质量/阻尼/限速/低速阈值恢复为 `8 / 24 / 2 / 0.08`；只回滚影子澄清时，删除 `actorShadowMarker`、`actorDoorContactTolerance` 绑定及门顶影子检测/恢复方法，不要重新加入 Actor Shadow 的整关 inactive override。共享 Level31、金币、门锁、相机、影子、命中特效、配置、存档和打点无需回滚。
