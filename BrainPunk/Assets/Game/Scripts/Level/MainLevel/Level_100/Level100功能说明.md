# Level100 功能说明

更新时间：2026-08-31

## 需求与背景

Level100 以 Level128 的密码门关卡为基线，保留原有的四个可拖拽 Box、密码区域、暂停页提示和事件 ID `1` 的开门链。本关的正确密码固定为 `2679`；中间镜子点击后会显示数字、图片或两颗既有明信片星中的一颗。数字与图片是误导项，不参与正确密码判定；两颗星都沿用各自的标准收集链，关卡外部不再摆放星星。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_100.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_100/Level100Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_100/Resources/PasswordPopup_100_2679.prefab`
- `Assets/Game/ArtSrc/Sprites/l100/` 中镜子候选素材
- `Luban/Configs/Datas/LevelData.xlsx`
- `Luban/Configs/Datas/LevelSort.xlsx`
- `Assets/Game/Configs/tbleveldata.json`
- `Assets/Game/Configs/tblevelsort.json`
- `Assets/Guru/guru_spec.yaml`
- `Assets/Guru/Gen/GuruAllSpecs.G.cs`

本次不修改 Level128、共享 `PasswordPopup`、`Level6GamePanelFeature`、`DragableItem`、`Postcard_Star`、门 Prefab、存档结构、分析事件、广告或平台 SDK。

## 原有行为基线

Level100 Prefab 来自 Level128 的场景结构。Level128 会在新 HUD 实例就绪后配置本关密码弹窗，暂停设置页显示问号提示，密码成功后派发 `OnTiggerItemEvent(1)` 并禁用当次尝试的 Use 入口。其场景内四个 Box 原本已挂载 `DragableItem`，属于误导互动的一部分。

Level100 实现前，镜子内的图片节点仅是摆放素材，没有本关私有的随机结果、切换时序或拖拽优先级保护。Level128 基线已有两颗 `Postcard_Star`；本关将 `index=1/2` 两颗既有星都迁入镜子，不新增第三个星实例或存档槽。

## 当前行为

### 密码与原有误导链

- Level100 根节点只挂载一个本关私有 `Level100Ctrl`，不与 `Level128Ctrl` 并挂。
- 控制器在当前 `GamePlayPanel_001` 创建完成后，通过既有 `Level6GamePanelFeature.ConfigurePasswordPopup()` 指定 `PasswordPopup_100_2679`。该私有 Prefab Variant 只覆写 `CorrectPassword=2679`，不改共享密码盘。
- 密码成功仍沿用原事件 ID `1` 的开门和通关链；事件派发后，当次尝试的 Use 按钮会被阻断、禁用并隐藏。
- 暂停提示继续沿用 Level128 的来源检查：只在游戏内 `GameSettingPopup` 真正可见时显示，Resume、Reset、离开或其它状态会隐藏。

### 镜子误导项

- 仅在关卡处于 `PLAYING` 时处理触摸开始；Editor 中同时支持鼠标按下。指针落在 UI 上时不触发镜子。
- Prefab 绑定四张图片、一个 TMP 数字节点和 `index=1/2` 两颗镜中星。只要还有未收集星，星星类别的总概率保持 `20%`、数字类别为 `40%`、四张图合计 `40%`；两颗都可用时在两颗星间均匀选择，只有一颗可用时由该星承接完整 `20%`。数字类别仍在 `0-9` 中等概率抽取。
- 两颗星都已收集后，星星类别永久移出本关随机池；数字仍为 `40%`，剩余 `60%` 在四张有效图间均分，即每张为 `15%`。
- 相邻两次按玩家看到的结果去重：数字不能连续显示同一个数，图片不能连续显示同一张图；两颗星素材相同，因此任意星星显示后，下一次整个星星类别都会暂时排除。数字类别始终保持 `40%`；星类别因相邻去重被排除的那一次为 `0%`，四张图片承接剩余 `60%`。数字/图片类别内部只在排除相同值后的候选间均分，Reset 后清空上一结果。
- 首次点击直接将抽中结果从 alpha `0` 渐显到原素材 alpha，当前时长为 `0.32s`。
- 后续点击先用 `0.28s` 渐隐旧结果，关闭旧节点后再用 `0.32s` 渐显新结果。
- 过渡期间的快速连点只保留一个待处理请求；当前过渡完成后再抽取一次，不堆积无限 Tween 或每个连点结果。
- `SwitchGameStateEvent` 和 `GamePause/GameResumeEvent` 都幂等接入镜子暂停状态：暂停时冻结当前 Sequence 并关闭两颗星的 Trigger，恢复时继续原 Sequence。若恢复时已没有 Sequence，但当前是完整显示的星，则只重新启用当前星的收集。
- 若“星淡入 + 快速连点”后又进入暂停，Sequence completion 会在待处理点击因非 `PLAYING` 被状态门禁丢弃后，再执行星可收集恢复检查，避免星可见却永久不可收集。
- Reset 会停止本关镜子 Sequence、关闭两颗镜中星 Trigger、隐藏当前结果、清空相邻去重记录并恢复未点击状态；Disable/Destroy 会关闭两颗 Trigger、停止协程/Tween 并解除事件订阅。每颗星是否继续进入随机池，始终以各自的标准星收集存档为准。

### 镜中可收集星

- 复用 Prefab 原有两颗 `Postcard_Star`，保持 `index=1/2`；全关仍只有两个实例，没有新增星槽，关卡根节点下没有外置星。
- 两颗星的初始状态都为 inactive，均挂在 `mirror` 下，局部坐标均为 `(-0.01, -0.411, 0)`，`SortingGroup` order 均为 `2`，与镜内其它结果对齐；控制器数组按 `index 1 -> index 2` 绑定。
- 抽中某颗星时只激活该颗视觉、将其所有子 `SpriteRenderer` alpha 设为 0，并保持两颗 Trigger 关闭；只在 `0.32s` 渐显完成且没有待处理的下一次点击时启用当前星 Trigger。
- 星正在显示时再点镜子，会在 `0.28s` 渐隐开始前立即关闭当前 Trigger；渐隐后再切换新结果，避免收集到半透明或已离开的星。
- Actor 命中已开启的 Trigger 后，完全复用 `Postcard_Star` 标准链：以当前 `LevelData.Id=10100 + 所选 index 1/2` 分别去重写入收集状态，向 Bag 发放 `GameProps.Star x1`，再以星的世界坐标派发 `PostcardGetStarEvent` 播放飞 UI，隐藏所选场景星并播放 Prefab 既有收集特效和 `getStar` 音效。本关不复制奖励或存档逻辑。

### 拖拽优先级保护

镜子使用 `Level100Ctrl` 内的私有矩形点击区（Prefab 当前为 `2.5 x 3.99`）做屏幕坐标到世界/局部坐标判定，不依赖新增的全镜面 Collider 抢占射线。点击点命中任一 `DragableItem` 自身或子 Collider 时，`Physics2D.OverlapPointAll` 会让本次镜子点击直接失败，保留四个原有 Box 的拖拽优先级。

## 数据、资源、事件与存档

- `LevelData` 新增唯一 `10100 / Level_100`，名称为“镜中密码”，版本为 `1.13.0`；运行时结算与答案键为 `S_congraktTips_Des_level_100` 和 `S_answer_Des_level_100`。
- `LevelSort` 在 Level99 后新增 `id=99 -> path=10100`，原 `10103-10119` 尾段的排序 ID 整体顺延一位，最终为 112 条且 ID/Path 均无重复。
- Luban 源表与两份运行时 JSON 已同步。Guru `level_config.level_sort` 也在 `10099` 与 `10103` 之间加入 `10100`，`currentRemoteConfigVersion` 从 `1.12.0` 更新为 `1.13.0`，YAML 与生成 C# 默认值保持一致。
- 镜子当前结果、相邻去重记录和密码成功入口锁只存在当前关卡尝试。例外是已收集的两颗镜中星：它们复用现有 `PostcardManager` 按 `LevelData.Id=10100/index 1/2` 分别持久化，并复用 Bag 奖励与 `PostcardGetStarEvent`；本次不新增存档字段、全局事件 ID 或分析埋点。

## 验证记录

- 静态检查确认 `PasswordPopup_100_2679.prefab` 是共享密码弹窗的私有 Variant，唯一覆写名称与 `CorrectPassword=2679`。
- Level100 Prefab 当前为 276 个唯一 YAML 定义、0 个重复定义、0 个悬空本地 fileID；根节点绑定本关控制器、四个镜子图片、数字节点、镜中星与点击区，四个 Box 均保留 `DragableItem`。
- 镜子序列化参数静态回读为 `star total 0.2 / digit 0.4 / fade-out 0.28s / fade-in 0.32s / click 2.5x3.99`，第四张图片已绑定 `Sp_l100_jz_05`。
- Prefab 中 `Postcard_Star` 实例总数仍为 2；静态断言确认 `index=1/2` 两个实例均以 `mirror` 为父节点、局部坐标均为 `(-0.01,-0.411,0)`、初始均 inactive、`SortingGroup` order 均为 `2`。关卡根节点不再包含星 Transform，`Level100Ctrl.mirrorStarRoots` 恰好按 index 1/2 绑定两颗星。
- 两份运行时 JSON 可解析；`10100` 的 ID、Path 和排序 Path 均唯一，LevelSort 共 112 条且无重复 ID/Path。
- 两张 Excel 源表已回读并渲染全部 6 个 Sheet，公式错误扫描为 0；Luban 隔离生成退出码为 0，同步目标仅为 LevelData/LevelSort 运行时 JSON。
- 目标跟踪文件的 `git diff --check` 通过；新增 C#、密码 Variant 与文档另行扫描无尾随空白。新增 Level100 Prefab 保留 Unity YAML 的既有空值字段格式，并已完成 fileID 闭包检查。
- 双星、相邻去重与新渐变时长合入后，使用 Unity `6000.0.59f2` 自带 Roslyn `csc` 按当前 `Assembly-CSharp.rsp` 后台完整编译；退出码为 0、0 errors，输出只有工程既有 warnings，`Level100Ctrl` diagnostics 为 0。输出为 `E:/WorkSpace/.codex-temp/level100-star-compile/Level100StarAudit.dll`；该命令没有启动或操作前台 Unity。

## 未验证与剩余风险

- 后台 Roslyn 编译已通过，但 Unity Editor 正式导入、PlayMode 完整通关、快速连点观感、四个 Box 与镜子重叠区的实际拖拽手感尚未执行，均为 `uncertain`。
- `Random` 不保证短样本内严格呈现基础权重。通常为星总计 `20%`、数字 `40%`、图片合计 `40%`；紧接星星的一次为星 `0%`、数字 `40%`、图片合计 `60%`。数字/图片类别内部还会排除上一具体值。两颗星分别处于未收集/已收集状态的长时概率统计尚未运行，为 `uncertain`。
- Android/iOS 真机触摸、多指、不同横屏比例、镜子图层与渐变观感尚未验证。星渐显后开 Trigger、渐隐前关 Trigger、渐变中暂停/恢复、快速连点后暂停的可收集恢复，以及收集去重/存档/奖励/飞 UI/特效/音效整链均尚未做 PlayMode 验收。
- 两个 Level100 本地化键当前只在 LevelData 登记，未在本次范围内提供正式文案，结算/答案显示为 `uncertain`。
- 仓库 Guru 只是本地默认值；若线上 RemoteConfig 启用旧版 `level_config`，仍可能覆盖本地排序，发布侧同步状态为 `uncertain`。

## 回滚路径

只回滚本轮双星改动时，将 `mirrorStarRoots` 恢复为单颗 index 2 绑定、把 index 1 星恢复到 Level128 基线的父节点/Transform/激活/排序配置，并移除完整结果的相邻去重记录；不删除或改号 `index=1/2` 的存档槽。完整回滚 Level100 时，删除 Level100 Prefab、私有控制器、密码 Variant、镜子素材与本地文档；从 LevelData、LevelSort、两份运行时 JSON、Guru YAML 和生成 C# 中删除唯一 `10100` 并恢复后续排序 ID。`currentRemoteConfigVersion` 应继续保持与工程 `Application.version` 一致的 `1.13.0`。Level128 和共享密码/拖拽/明信片星/门逻辑无需跟随回滚。
