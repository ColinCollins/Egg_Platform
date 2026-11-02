# DLevel_079 足球动作与进球表现功能风险评估

## 2026-09-20：进球表现接线修复

### 行为基线与修复范围

本轮验收对象是“足球进入球门后，先显示 FIFA Goal 界面并播放吹哨，等待 0.5 秒，再进入标准胜利结算”。现有两个 DLevel79 脚本已经实现这条流程，但修复前的 Prefab 没有消费它：`fifaGoal` 为空，球门仍走旧共享直接成功组件，并且 FIFA Prefab 实例不存在，所以玩家实际看不到中间表现，且会直接结算。

修复开始时的 HEAD `39d544c78` 历史未找到包含本关局部成功组件或 FIFA 资源 GUID 的已提交 DLevel79 Prefab。由此可确认的范围是：脚本没有被合并还原，缺失的是此前未落库的 Prefab 接线；旧文档对“已完成绑定”的描述超出了实际资源证据。本轮处理中新增提交 `5a72b56b6` 已包含接线，但其 Inspector 延迟保存为 `1s`，当前工作区另行修正为需求值 `0.5s`，尚未提交。

本轮只修改 `Assets/Game/Prefabs/DrawLevel/DLevel_079.prefab`：加入默认隐藏的 FIFA 嵌套实例，绑定 `DLevel79Ctrl.fifaGoal`，并将 `SuccessArea` 改为 `DLevel79GoalSuccessHandle` 回调根 Ctrl。没有修改主线 Level87、共享 Draw 成功流程、FIFA 源 Prefab、音频资源、配置、存档或打点。

### 数据、资源与状态流

足球 Trigger → 本关 Handle 做目标球过滤 → Ctrl 竞争共享成功表现占用 → 关闭后续重复触发 → 激活 FIFA UI、播放 `whistle` → 未暂停的累计时间达到 `0.5s` → 完成共享成功表现 → 原胜利面板与转场。Reset、Disable 或外部 Resolved 会停止本关等待并隐藏 FIFA，避免晚到协程再次结算。

### 风险与证据

| 风险 | 当前措施/证据 | 未验证边界 |
| --- | --- | --- |
| 球门仍绕过本关表现直接成功 | `SuccessArea` 已移除旧直接成功组件，改绑局部 Handle 与根 Ctrl | 正式场景 Trigger 是否由目标足球进入 |
| FIFA 开场误显示或引用为空 | 嵌套实例默认隐藏，`fifaGoal` 指向实例 GameObject | Canvas 层级与全面屏视觉 |
| 重复进球造成多次结算 | Ctrl 使用共享成功表现占用并保留本关完成门禁 | 多碰撞帧与极端 Reset 竞争需 PlayMode |
| 暂停时等待继续推进 | 现有协程只累计未暂停帧的 `deltaTime` | 原生覆盖层冻结 PlayerLoop 时的视觉恢复 |
| 退出/重开后晚到完成 | 现有 Reset、Disable、Resolved 清理协程并隐藏 UI | 完整 Replay/转场重建 |

YAML 静态验证为 175 个锚点无重复、非零本地引用全部闭合；FIFA、局部 Handle、Ctrl 三类关键 GUID/fileID 均存在，旧直接成功 GUID 不再存在，外部 GUID 均能解析到 `.meta`。Unity 6000.0.59f2 当前 Bee 响应文件全量编译退出码为 0，接线版本由 `PrefabImporter` 成功导入；最后把延迟修正为 `0.5s` 后仅完成磁盘静态复核，Editor.log 尚无该末次修改的新导入证据。`uncertain`：尚未完成正式 DLevel79 的 PlayMode/真机进球实测，吹哨听感、0.5 秒节奏、胜利面板衔接、暂停及 Android/iOS 横屏仍不能声明已验收。

### 回滚

回滚只需在 DLevel79 Prefab 中移除 FIFA 嵌套实例与根子节点引用、清空 `fifaGoal`，并恢复 `SuccessArea` 原共享直接成功组件及 `drawLevelCtrl` 引用。两个现有 DLevel79 脚本和共享系统均无需回退。

## 需求与行为基线

2026-09-18 用户要求：第一笔完成后 Actor 向右移动X距离、向左转身、蓄力、踢球，足球向左受力且方向和大小可调，Actor动作参考主线84。

原prefab只有共享Draw/画线与10秒失败倒计时，Actor为D11坡度子类，足球Kinematic且主线拖拽启用；没有本关动作驱动。最终新增DLevel79Ctrl和DLevel79ActorCtrl并显式绑定，保留50度Draw层坡度限制与原物理参数；右移2、方向(-1,0)、冲量20为可调初值。仅在本关关闭复制的足球DragableItem，避免鼠标/触摸直接改球Transform与Draw输入争用。

## 影响文件与既有系统

- `DLevel79Ctrl.cs`：IDrawStrokeCompletedHandler、实际距离行走、动作推进、足球单次施力、局部Resolved和Reset清理。
- `DLevel79ActorCtrl.cs`：ActorCtrl子类，保留本关坡度检测并管理ready两次/shoot一次；没有修改原D11、Level84ActorCtrl、ActorCtrl。
- `Assets/Game/Prefabs/DrawLevel/DLevel_079.prefab`：根Ctrl绑定、原Actor局部替换与IsEditor关闭、足球拖拽组件关闭；没有改源Actor/主线84、SDK、配置、UI或存档。
- 本关不新增胜负API；超时失败保留共享链，踢球结束只停止动作。原触地事件目标为0，本轮不替换为新胜利规则。

## 风险与措施

| 风险 | 当前措施/证据 | 未验证边界 |
| --- | --- | --- |
| Editor输入覆盖自动右移 | 本关IsEditor=0；使用共享移动API，未修改父类 | 真实Actor行走与碰撞 |
| 阻挡时按时间穿墙到终点 | 使用RB实际世界X，只有越过终点后校正X | 整图坡面/坠落/重画线接触 |
| 朝向反复翻转 | 子类调用受保护的幂等方向设置，重复朝左测试通过 | 实际Spine镜像视觉 |
| 共享待机覆盖蓄力 | Actor子类持有轨道时屏蔽HandleAnimators | 真实死亡/受击动画与帧顺序 |
| Kinematic施力无效 | 开始踢球时切Dynamic后Impulse，真实RB检查通过 | 正式球重力20/阻尼5可能快速下坠/减速，需调方向力度 |
| 方向向量长度意外改变力度 | normalized direction × kickImpulse，(-3,4)/10得到(-6,8) | 地形接触后速度不同属物理结果 |
| 重复笔画、旧回调多次施力 | 一次尝试状态门禁，只有踢球阶段开始返回true | 正式10秒结果竞争 |
| 暂停Complete回调继续踢球 | 回调仅标记，Tick暂停冻结轨道且不切阶段；两次临界暂停检查通过 | 暂停切换帧与已踢足球/整关物理仍须实测 |
| Reset/退出/外部替换晚到施力 | 清自身轨道前解绑；Reset恢复本关初态，5类取消检查通过 | 完整Draw转场、Replay、HUD、存档打点 |
| 缺动画仍假踢 | 启动前检查两种动画，无延迟兜底，缺shoot测试通过 | 替换皮肤/骨架后的实际动画资源 |
| 拖拽与画线争用足球 | 仅D079禁用原DragableItem，保留原资源与其它组件 | Android/iOS多指、横屏输入 |

## 验证与缺口

主工程Unity6000.0.59f2编译及重载完成；171锚点和337本地引用闭合，167原块保持。独立同版本Unity PlayMode的42项检查通过，使用逐字生产脚本、真实Unity2D物理和Spine回调，但共享Actor/Draw为桩、动画为合成空时间线。第一次夹具全清动画导致idle失败后改为仅删除踢球动画重跑通过，失败产物保留。

外部验证目录 `C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel79-20260918/` 有原快照、夹具、主工程编译截取、最后运行结果与prefab核对报告。完整脚本树、绑定和调用链见同目录 `DLevel_079脚本结构记录.md`。

uncertain：未实测正式D079解法、真实Spine姿势/脚触球时刻、原球物理参数下弹道、坡度规则、10秒超时、进球判定、完整暂停/结算/重建、存档/打点和Android/iOS横屏。默认在shoot开始施力，实际动画若需要延后触球需后续明确时机；当前不推断素材中存在事件帧。不承诺整关已验收。

建议实际PlayMode核对：首笔后2单位右移 → 左向两次蓄力 → 踢球仅一次；Inspector调(-1,1)与不同力度观察弹道；蓄力中暂停/重开及球飞行中重开；必要时另行配置球到目标的成功入口。本次不把“踢球完成”当胜利。

## 回滚与待确认

回滚逐块移除新根组件/引用，原Actor脚本GUID恢复D11并恢复IsEditor原值1，按需重新启用足球拖拽，删除新增本关文件；不整份回写before快照以免覆盖用户地图编辑。

待确认仅为实际调参和整关验收，不阻止当前动作脚本交付。未新建全局事件或共享封装，Packages/GuruSDK/GF均只读。知识库无Level84专项结果；引用的远端加载feature plan缺失、CodeGraph CLI/MCP不可用，本关以当前源码、JSON、prefab及验证日志为依据。
