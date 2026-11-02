# Level082 功能说明

更新时间：2026-07-20

## 需求背景

Level082 复制自 Level012。顶部 reset、pause、tips 三个按钮需要整体半透明并继续支持拖拽，但不再像 Level012 一样生成对应的场景碰撞体。入口门需要像 Level007 的箱子一样可拖放：每次落地都震屏，从足够高的位置释放后还会从屏幕上方中央落下一批随机大小石头，玩家通过多次操作堆出跨越沟壑的道路。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_082.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_082.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_82/Level82Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_82/Level82DoorDropCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_82/Editor/Level82StonePrefabGenerator.cs`
- `Assets/Game/ArtSrc/Sprites/L82/`
- `Assets/Game/Prefabs/LevelItems/Level_82/Level82Stone.physicsMaterial2D`
- `Assets/Game/Prefabs/LevelItems/Level_82/Stones/Stone_L82_01.prefab` 至 `Stone_L82_13.prefab`

本次只调整 Level082 专用脚本、石头资源和关卡预制体；未修改 Level007、Level012、公共 `DragableItem`、公共 `GamePlayPanel`、`StartDoor`、Actor、`ShadowController` 或基础关卡控制器。Level082 已由配置维护方接入 `tbleveldata.json` 与 `tblevelsort.json`；本次需求未修改 Luban 表、生成配置、存档字段、PlayerPrefs、资源地址规则、埋点、广告、IAP、RemoteConfig 或平台 SDK 调用。

## 原有行为基线

- Level012 通过 `GamePlayPanel_DragButtons` 让顶部 UI 可拖，并有 `Level12SceneItemsCtrl` 将 UI 映射成场景物体和碰撞体。
- 公共 `StartDoor` 只负责角色开场走出门的表现，本身不是可自由拖放的物理物体。
- Level007 的箱子使用公共 `DragableItem` 处理鼠标和移动端触摸拖拽。
- L82 文件夹中的 13 张石头图片原本没有可直接用于玩法的独立物理预制体。

## 本次行为

### 顶部 UI

- Level082 使用独立 `GamePlayPanel_082`。
- reset、pause、tips 三个按钮根节点各有 `CanvasGroup`，`alpha = 0.5`、`interactable = true`、`blocksRaycasts = true`，因此按钮及其全部子图标统一半透明且保留点击与拖拽输入。
- 三个按钮各有一个 `UIDrag`，并由既有 `GamePlayPanel_DragButtons` 保存初始位置、处理拖拽期间的点击屏蔽以及 `GameResetEvent` 后的位置恢复。
- levelBlock 不在拖拽列表中。Level082 不绑定 `Level12SceneItemsCtrl`，HUD 和关卡中也没有为这三个 UI 新增 Collider2D/Rigidbody2D，因此不会生成 Level012 的场景碰撞体。

### 入口门拖放

- 入口门保留公共 `StartDoor` 的原 Trigger，开场走出门流程不变；只在 Level082 实例上追加 `Rigidbody2D`、实体 `BoxCollider2D`、公共 `DragableItem` 和 `Level82DoorDropCtrl`。
- 开场未结束、暂停、角色死亡、进门、尝试切换或本关失败锁定时，入口门不可拖拽且不参与新的落地判定。
- 拖拽时门使用 Kinematic 刚体并暂时关闭实体碰撞；松手后切换为 Dynamic，按重力下落。只有碰到 Ground 层且存在向上的支撑接触才认定为一次落地。
- 每次有效落地播放 `crash` 音效并震屏。震屏强度取释放高度和碰撞相对速度中的较大权重。
- 释放时门的世界 Y 坐标达到 `1.5` 才触发落石；低处落地只播放落地反馈。
- 门掉入沟壑并低于世界 Y `-9` 时，会在整扇门离开画面后回到最近一次 Ground 安全落地点；首次操作前的安全点就是入口门初始位置，避免门离开可点击区域后只能重置整关。

### 落石

- 当前尝试的前 5 次高处落地依次生成 4 块大石、2 块中石、2 块小石，候选预制体在各自尺寸组内随机选择；第 6 次及以后每次只生成 2 块小石，用于少量补缝。
- 完整批次次数在有效高处落地被接受时立即递增，不等待上一批协程结束；死亡、掉坑或手动重置时立即清零，新建关卡实例的 `OnEnable` 也会从 0 开始。
- 出生点使用关卡相机视口坐标，中心为 `x = 0.5`、宽度为视口的 `0.34`、`y = 1.1`，位于屏幕上方中央；初速度、角速度和角度带小范围随机。
- 13 个石头预制体均使用 `WithoutShadowGround`（Layer 13）、`Untagged`、Dynamic Rigidbody2D、Continuous 碰撞检测、单路径 PolygonCollider2D 和共同的零弹性物理材质。大石负责主体堆叠，中石与小石用于填缝。
- Level082 的 Actor prefab 实例局部覆盖 `whatIsGround = 8520`、`whatIsStuff = 8448`、`ShadowController.ignoreLayer = 8192`：角色仍把 Layer13 石头识别为可落地、可侧碰的实体，但阴影检测命中该层时不绘制影子。入口门实体碰撞局部排除掩码为 `10496`，继续排除 Actor、Stuff，并新增排除 Layer13，避免后续落门扰乱石堆。
- `Level82StonePrefabGenerator` 只在编辑器菜单 `Tools/Level 082/Regenerate Stone Prefabs` 下运行，用于按 L82 图片重建 13 个石头，不参与玩家运行时流程。
- 当前同时参与动态模拟的石头软上限为 48。达到软上限后，控制器会优先把最早且已经休眠或接近静止的旧石头切换为 Static，作为稳定桥面并释放动态预算；本次完整 4/2/2 落石不会因此取消。若旧石头仍在运动，当前批次可以短时超过软上限，后续生成时再继续收敛。
- 生成新批次前会清理低于世界 Y `-12` 的石头；冻结后的桥面石头也继续保留在本关清理列表中。

### 失败与重置

`Level82Ctrl` 订阅 `OnTriggerFailAreaEvent`、`OnTriggerTrapEvent` 和 `GameResetEvent`。事件到达时立即把完整批次计数清零、标记本次尝试结束、停止尚未生成完的落石、结束震屏并恢复镜头，同时通知入口门关闭拖拽和坠落判定。实际关卡销毁、重建、死亡表现和失败流程仍由既有系统负责。

## 已执行验证

- 2026-07-20 通过现有 Unity 6000.0.59f2 编辑器执行 `Assets/Refresh`；`Assembly-CSharp.dll` 与 `Assembly-CSharp-Editor.dll` 均于 10:51:31 重新生成，本次刷新完成域重载，最新 8000 行日志中 `error CS` 与 Level082 相关 error/exception 均为 0。
- 本次刷新已由 PrefabImporter 重新导入 `Level_082.prefab` 和 13 个石头 prefab，日志未记录对应资源解析错误。
- Unity 6000.0.59f2 已重新导入 `Level_082.prefab`、`GamePlayPanel_082.prefab`、物理材质和 13 个石头 prefab。
- 最终门回收修正后，`Assembly-CSharp.dll` 于 2026-07-17 22:06:56 重新生成；未变化的 Editor 生成器程序集最近一次于 21:58:09 生成。随后完成域重载，最新 3500 行日志中的 `error CS` 为 0。
- Unity 日志记录 `GamePlayPanel_082.prefab` 修改后通过 PrefabImporter 导入，未命中该资源或 `Level_082.prefab` 的 YAML/Prefab 导入错误。
- 静态检查确认 HUD 列表只有 reset、pause、tips，三者各有一个 `UIDrag` 和一个 `CanvasGroup(0.5)`，levelBlock 无本地 UIDrag。
- 静态检查确认 13 个石头 prefab 均有可解析 Sprite、Layer13、`Untagged`、Dynamic/Continuous Rigidbody2D、PolygonCollider2D 和同一零弹性材质引用。
- 静态检查确认 Level082 prefab 序列化 `fullStoneWaveLimit = 5`、`smallOnlyStoneCount = 2`，Actor 实例仅在本关覆盖 `8520 / 8448 / 8192` 三个掩码，入口门实体碰撞排除掩码为 `10496`。
- 静态检查确认 `Level_082.prefab`、`GamePlayPanel_082.prefab` 与 13 个石头 prefab 共 15 个 YAML 文件均无重复 fileID、无缺失本地引用；Level082 的 13 个石头外部引用均能解析到对应 prefab。
- 配置现已登记：`tbleveldata.json` 包含 `10082 -> Level_082`，`tblevelsort.json` 包含 `id 81 -> 10082`。这些配置由配置维护方接入，本次未编辑对应 JSON 或 Excel。
- 2026-07-20 尝试从现有 Loading 场景进入 PlayMode；观察约 27 秒仍停留在 Loading，同期日志记录 Guru SDK 账号认证 `HTTP/1.1 400 Bad Request`，未创建 Level082 实例。退出 PlayMode 时另有既存 `UIManager.DestroyAllUI -> GameManager.ClearAll` 空引用，两个调用栈均不包含 Level082。本次未用 GM 改关卡或修改玩家存档。

## 未验证项

- 因上述启动流程未进入 Level082，本次没有取得实际落门、石头物理和 Shadow 画面证据。
- 仍需在正常 PlayMode 玩家路径验证开场门动画结束后才可拖、低处/高处落地分支、掉沟回收、震屏强度，以及连续 7 次高处落门对应 `8、8、8、8、8、2、2` 的生成数量。
- 仍需验证约 48 个动态刚体的软预算下能否稳定堆成可通行桥面，以及门与石头的碰撞尺寸、摩擦、落点宽度和高度阈值手感。
- 仍需验证角色站上大小石头时不显示阴影，但仍能正常落地、行走和再次跳跃；并验证后续落门不会碰撞或扰乱石堆。
- 仍需验证掉坑、触刺、手动重置、暂停、通关期间的即时锁定，以及死亡/重置后下一次高处落门重新获得 5 次完整批次。
- 尚未在 Android/iOS 横屏设备验证触摸拖拽、掉帧、堆叠稳定性和不同屏幕比例下的屏幕外出生点。

## 回滚

移除 `Level_082.prefab` 上的 `Level82Ctrl` 与入口门本地追加组件，恢复或删除 `GamePlayPanel_082`，并删除 `Level_82` 石头 prefab、物理材质和本地脚本即可回滚。公共 Level007、Level012、`DragableItem`、`GamePlayPanel`、`StartDoor` 与基础关卡代码无需改动。

## 2026-07-22 落石音效接入

### 需求与影响范围

原有高处落门会生成一整批石头，但只有入口门落地的通用 `crash`，石头开始掉落时没有独立声音。本次新增 `l82RockFall.ogg`，修改 `Level82Ctrl` 的批次生成入口，并在 SoundsGood 的 `SoundCollection.asset` 与 `SFX_Generated.cs` 登记 `l82RockFall`；没有修改共享 `AudioManager`、关卡 prefab、物理、存档、事件或配置。

### 新行为与资源流

- 用户提供的 `滚石.ogg` 原样复制为 `Assets/Game/ArtSrc/Audio/Sfx/l82/l82RockFall.ogg`，保留 Ogg/Vorbis 与 48 kHz 立体声，不再次转码。
- `SpawnStoneWave()` 在清理旧石后、首个石头组开始生成前播放一次 `l82RockFall`。声音按“每批一次”触发，不放在单颗石头的实例化点，因此完整批次的 8 颗石头不会叠播 8 次。
- 低高度落门不启动 `SpawnStoneWave()`，仍只保留原有落地反馈；死亡、掉坑或重置停止旧生成协程，不会产生新的落石音效调用。已经开始的一次性声音沿用 SoundsGood 生命周期自然结束。

### 验证、缺口与回滚

- 四个源音频经 `ffprobe` 验证为合法 Vorbis；目标文件 SHA-256 与用户源文件一致。`l82RockFall` 的 Collection tag、音频 GUID、生成项和脚本调用均静态核对为一处且相互一致。
- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 Bee 响应文件进行 `Assembly-CSharp` 全量独立编译，结果为 0 个错误；项目既有警告未在本次处理。
- 当前 Unity 编辑器已用 `AudioImporter` 成功导入 `l82RockFall.ogg`，并于 15:22 重建 `Assembly-CSharp.dll` 与 `SoundsGood.Application.dll`；最近 25000 行日志中本次音频、Collection 与 `error CS` 相关错误为 0。
- Unity PlayMode 中的实际音量、1.96 秒尾音、快速连续两批时的重叠听感，以及 Android/iOS 真机输出仍待验证。
- 回滚本次音效时，移除 `SpawnStoneWave()` 的播放调用、`l82RockFall` 的 SoundsGood 登记及 `Assets/Game/ArtSrc/Audio/Sfx/l82` 资源即可；无需回滚 Level082 既有玩法或共享音频代码。
