# Level100 风险评估

更新时间：2026-08-31

## 边界结论

Level100 的通关真值只是本关私有密码 Variant 中的 `2679`。镜子上的数字和图片是表现性误导结果，不改密码、不派发通关事件；`index=1/2` 两颗既有星都迁入镜子，作为真实可收集项复用各自的标准存档、去重、奖励与表现链。实现不新增星槽、存档字段或共享事件，关卡外部不再摆放星星。

## 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 修改共享密码导致其它关卡变成 2679 | 新建 `PasswordPopup_100_2679` 私有 Variant，只覆写 `CorrectPassword=2679`；`Level100Ctrl` 通过既有 HUD API 指定资源 | Prefab 静态通过，PlayMode 待验收 |
| Level128 控制器与 Level100 控制器同时订阅事件 | Level100 根节点用私有 `Level100Ctrl` 替换基线控制器，不并挂 | 静态通过 |
| 镜子点击区抢走四个 Box 的拖拽 | 不在镜面增加抢射线的大 Collider；私有矩形点击判定命中任一 `DragableItem` Collider 时直接退出 | 代码/Prefab 静态通过，触摸手感 `uncertain` |
| 指针点在 HUD 或暂停期间仍触发镜子 | 先检查 `InputUtils.IsPointerOverUI` 和 `PLAYING` 状态，不符合时不抽取 | 代码静态通过，多指 `uncertain` |
| 两颗星放入后错误地把概率翻成 40% | 只给星星类别一个总计 `20%` 的权重；两颗都可用时均匀选一颗，只剩一颗时由该星承接完整 20% | 算法/双绑定静态通过，长时分布 `uncertain` |
| 一颗星收集后仍被抽到，或错误移除另一颗 | 每次抽取分别调用两颗 `RefreshSavedCollectionState()`；已收集 index 只从候选移除，另一颗仍可进入总计 20% 的星类别；两颗都收集后恢复数字 40%/四图各 15% | 代码静态通过，Reset/重进 `uncertain` |
| 图片候选重复，收集前/后肉眼分布失真 | 四个 Renderer 均序列化绑定，第四张改为 `Sp_l100_jz_05`；四图收集前各 10%、收集后各 15% | Prefab 静态通过，概率观测 `uncertain` |
| 为镜子新建星实例或改号，与既有收集槽冲突 | 复用 Level128 已有两颗星并保持 `index=1/2`；全关实例数仍为 2，控制器数组按 index 顺序绑定 | Prefab 静态通过 |
| 外部仍残留一颗可直接收集的星 | index 1 已从 Level 根 children 移除并迁入 mirror；静态断言要求 Level 根没有该 Transform、mirror 同时包含两颗 | Prefab 静态通过 |
| 星在渐显期间就被角色收集 | 两颗星都初始 inactive；抽中后只激活所选视觉且所有星 Collider 关闭，只在渐显完成且没有待处理点击时开当前 Trigger | 时序静态通过，重叠触发 PlayMode `uncertain` |
| 星渐隐或切走后仍能收集 | 当前结果为星时，下一次点击会在渐隐开始前立即关闭 Trigger，隐藏时再次保底关闭 | 代码静态通过，边界时序 `uncertain` |
| `RefreshSavedCollectionState()` 把未显示星的 Collider 重新打开 | 每次刷新某颗星的存档状态后立即再次关闭该 Collider；抽取、渐隐、暂停、Reset、Disable/Destroy 都统一关两颗，只在当前星完整渐显后单独打开 | 代码路径静态通过，PlayMode `uncertain` |
| 暂停时 Sequence 继续播放或星仍可收集 | 状态切换与 Pause 事件都幂等暂停 Sequence 并关两颗 Trigger；Resume 继续 Sequence，无 Sequence 时按当前完整显示星恢复当前 Trigger | 代码静态通过，PlayMode `uncertain` |
| 星淡入快速连点后暂停，pending click 被状态门禁丢弃，星可见却不可收集 | Sequence completion 在尝试处理 pending click 后再执行就绪恢复；只在 `PLAYING`、无过渡且星仍有效时开 Trigger | 代码路径静态通过，组合时序 `uncertain` |
| 收集绕过标准去重/存档/奖励链 | 继续由 `Postcard_Star` 以 `LevelData.Id=10100/index 2` 调用 `TryCollectLevelStar`，新收集才发 `GameProps.Star x1`，再派发飞 UI 事件、播特效与 `getStar` 音效 | 共享调用链静态通过，PlayMode `uncertain` |
| 镜中星图层不在镜面结果层 | 两颗星都为 `mirror` 子节点，local 都是 `(-0.01,-0.411,0)`、SortingGroup order 都是 `2`、初始都 inactive | Prefab 静态通过，视觉 `uncertain` |
| 相邻两次出现完全相同结果 | 完整保存上一次已渐显完成的 `MirrorChoice`：数字排除相同值、图片排除相同索引；两颗星视觉相同，因此上一次为任意星时下一次禁用整个星类别 | 算法静态通过，快速连点观感 `uncertain` |
| 禁止连续星后数字概率被重新归一化为 50% | 星类别被相邻去重禁用时显式改为星 0%、数字仍 40%、图片承接剩余 60%；不把数字和图片按 0.8 总权重重新归一化 | 算法静态通过，长时统计 `uncertain` |
| 快速连点并发多条 Tween 或无限积压 | 过渡时用布尔值最多保留一个待处理点击，完成后再抽一次 | 代码静态通过，连点预期待策划验收 |
| Reset/离开后旧结果、去重记录、星 Trigger 或 Tween 残留 | Reset 关闭两颗 Trigger、隐藏所有候选并清空当前/上一次结果；Disable/Destroy 关闭两颗 Trigger并取消 Sequence、协程与事件订阅 | 代码静态通过，PlayMode `uncertain` |
| 密码成功后 Use 入口被共享区域逻辑重新激活 | 收到事件 ID 1 后同时设置 `IsBlockClick`、`Interactable=false` 并隐藏，HUD 重绑时重放状态 | 代码静态通过，PlayMode `uncertain` |
| Prefab 手工 YAML 绑定导致 fileID 断链 | Level100 Prefab 为 276 个唯一定义、0 重复、0 悬空本地引用；密码 Variant 与镜中星 PrefabInstance 均能解析到共享 Prefab | 本地引用静态通过，Unity 导入 `uncertain` |
| Level100 可被资源加载但不在主线排序中 | Excel、Luban JSON、Guru YAML 和生成 C# 都已加入 `10100`，LevelSort 用连续 `id=99` | 本地配置静态通过 |
| 后续关卡排序 ID 重复或丢失 | 原 99-111 从末行向下顺延为 100-112；JSON 回读共 112 条，ID/Path 无重复 | Excel/JSON 通过 |
| 本地 Guru 和线上 RemoteConfig 不一致 | 本地默认排序已加 `10100`并升至 `1.13.0`；线上启用的旧值仍可覆盖本地 | 本地完成，线上 `uncertain` |
| 结算/答案 key 只登记未有正式文案 | LevelData 已登记 Level100 专用 key，本次不编造未提供的文案 | 本地化显示 `uncertain` |

## 验证证据

- 静态回读 `Level100Ctrl.cs`、Level100 Prefab 和私有密码 Variant：密码 `2679`、星总计 20%/数字 40%/图片剩余权重、完整结果相邻去重、`0.28/0.32s` 渐变、双星 Trigger 门禁、Reset 和 `DragableItem` 优先级保护路径均能对应。
- Level100 Prefab 本地 fileID 闭包检查为 `276 definitions / 0 duplicate / 0 missing`；四个 Box 均挂载既有 `DragableItem`。全关两个 `Postcard_Star` 实例；`index=1/2` 均定位到 `mirror`，local 均为 `(-0.01,-0.411,0)`、初始均 inactive、SortingGroup order 均为 `2`，Level 根无外置星，控制器数组双绑定通过静态断言。
- LevelData/LevelSort JSON 可解析，`10100 / Level_100 / id 99` 唯一，LevelSort 无重复 ID 和 Path。
- Excel 六个 Sheet 已渲染检查，公式错误为 0；Luban 隔离生成退出码 0。Guru YAML/生成 C# 的 `level_sort` 与 `1.13.0` 已对齐。
- 目标文件 `git diff --check` 通过；本评估不将静态检查等同于 Unity PlayMode 验收。
- 双星、相邻去重与新渐变时长合入后，Unity `6000.0.59f2` 自带 Roslyn 按当前 `Assembly-CSharp.rsp` 后台完整编译退出码为 0、0 errors，输出只有工程既有 warnings，`Level100Ctrl` diagnostics 为 0。输出为 `E:/WorkSpace/.codex-temp/level100-star-compile/Level100StarAudit.dll`；没有启动或操作前台 Unity，此结果不代表已执行 PlayMode 或真机测试。

## 发布前验收矩阵

- 输入 `2679` 可成功，相邻错误密码不开门；成功只派发一次事件 ID `1`，Use 入口不能再打开。
- 四个原有 Box 仍可在镜子重叠区按下并拖动，不应因按下 Box 同时换镜子结果。
- 镜子首次用 `0.32s` 只渐显；后续每次都先用 `0.28s` 渐隐旧项再用 `0.32s` 渐显新项。
- 相邻两次不能出现同一个数字、同一张图片或连续两次星星；快速连点最多缓存一次，仍以真正完成显示的结果作为下一次排除依据。Reset 后首次抽取不受上一局结果影响。
- 重复点击不应叠图、卡死或残留 Tween；Reset/失败重开后镜子恢复空白。
- 两颗都未收集时，用足够大样本统计星类别基础权重约 20%（两颗均分）、数字约 40%、四图合计约 40%；收掉一颗后另一颗仍承接星类别总计 20%，两颗都收掉后星为 0%、数字约 40%、四图各约 15%。紧接星星的一次必须为星 0%、数字约 40%、四图合计约 60%；数字/图片类别内部不得重复上一具体值，允许随机短期偏差。
- 星在渐显完成前不可收集；星正在显示时点击换项，从渐隐开始即不可收集。
- 在星渐显/渐隐中暂停，alpha 与 Sequence 应冻结、Trigger 应关闭；恢复后从原进度继续。已完整显示的星在暂停期间不可收集，恢复后重新可收集。
- 在星淡入期间快速点击再立即暂停/恢复，不应出现可见星永久不可收集，也不应在非 `PLAYING` 时处理新抽取。
- 首次分别收集 `index=1/2` 时各只发一颗 Bag 星，都有飞 UI、收集特效和 `getStar` 音效；收掉其中一颗后只可能再抽到另一颗，Reset/重进不重复奖励已收集 index。关卡外部不应看到或碰到任何星。
- 暂停设置页提示、原场景误导项、门动画和通关链与 Level128 基线一致。
- Android/iOS 横屏下验收单指/多指、UI 射线、图层、alpha 渐变和快速连点。

## 回滚

只回滚本轮双星与去重时，将控制器恢复为单颗 index 2 绑定、把 index 1 恢复到 Level128 基线的父节点/Transform/激活/排序配置，并移除完整结果的相邻去重记录；不删除或改号 `index=1/2` 槽。完整撤回时删除 Level100 私有资源和文档，从 Excel、JSON、Guru YAML/生成 C# 删除 `10100` 并恢复后续排序 ID；Guru 版本仍保留与工程一致的 `1.13.0`。回滚不应修改 Level128、共享 `PasswordPopup`、`Level6GamePanelFeature`、`DragableItem`、`Postcard_Star` 或门 Prefab。
