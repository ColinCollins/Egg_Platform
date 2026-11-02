# DLevel_094 脚本结构记录

## 需求、基线与边界

2026-09-16，用户要求制作 D094/D095，并澄清“就是要角色行走的，所以才复制的002”。以这一澄清及两关现有独立 Actor、DLevel001Ctrl 和地图为准，保留首笔完成后向右行走、进入成功门的流程。当前磁盘 D002 已是另一套夹球结构，不能据编号覆盖用户当前地图；本轮没有把该结构写入新关。

先读中央结构记录和 Level1/DLevel_001脚本结构记录.md，以首笔驱动行走的接口与生命周期为可复用代码基线。没有找到对应 D003 结构记录，参考关整体验收也缺少证据，状态为 uncertain；并不据此改写旧关。通用 feature plan specs/037-remote-resource-loading/plan.md 当前缺失。

本轮仅替换本关提示键、增添独立多语言词条、对齐本地配置、记录结构与风险。地图对象、Actor、物理、机关开关、相机和共享脚本保持。不存在本关新增运行时类、倒计时或额外通关规则。

## 脚本树、职责与接口

```text
DLevel_094
├─ DrawLevelCtrl                  初始化、输入门禁、Actor成功/失败、结果与重开接口
├─ DrawLineFeature                首笔事件、多笔物理画线、20墨水、星级0/5/10
├─ DLevel001Ctrl                  IDrawStrokeCompletedHandler；首笔后向右驱动Actor
├─ DelayAutoBeginDrawHandle       可见开场接口；首次入场额外延迟0秒
├─ TrapEventHandlle               原有Actor死亡/击退链，当前地刺节点关闭
├─ Actor                         独立Actor.prefab / ActorCtrl
├─ DLevel088_ReverseGravityZone   原有反重力矩形区域
├─ DrawGame_SuccessDoor           触发本关DrawLevelCtrl.OnTriggerSuccess
└─ level3 / 地形 / 相机 / 背景 / Tips
```

DLevel001Ctrl 只读复用：有效首笔完成开启向右输入，通过 Actor.OnUpdate 推進现有行走；暂停或死亡清理输入，Resolved、AttemptReset 和 Disable 停止驱动。OnEnable/OnDisable 订阅和解除局部事件；guideRoot 为空且无新引导。DelayAutoBeginDrawHandle 仍属于进入时可见Intro，不新增Reset等待。

DrawLevelCtrl 接收画线完成事件并分派接口；成功门沿现有 Actor 身份/死亡/结果门禁、停止行走、开门与渐隐后结算。失败重开、Replay、退出、黑幕/翻页、解锁/存档和打点继续由原主流程负责；本轮不改这些系统。区域按刚体处理反重力、离开或停用恢复，不负责 Actor 动画或行走输入。

## 显式绑定与地图事实

- 主控 900000000000000001，画线 900000000000000002；成功门 Spine 2984154252043883271。普通独立 Actor 没有附属球壳，successExtraRendererGroup=0 保持。
- 行走组件 900000000000000003 显式绑定同根主控和 Actor 1716601560298323527。Actor 来自实例 1238832509353424896，源 GUID 10877d0fc30026040bef4d574b8ebaf0；位置 (-8.74,-2.15)、moveSpeed=3 保持。
- 延迟组件 900000000000000004 绑定同根主控，delay=0。画线 isConfig=0，maxInkLength=20，stage=0/5/10，strokeMass=200，多笔及按长度缩放保持。
- 成功门实例 6932855938480942226，位置 (8.55,-1.28)，onEnter 显式指向本关主控 OnTriggerSuccess。
- 地刺实例 6182022342620756899 的根 m_IsActive=0，两关原来均关闭，本轮保持。其已存在的 Player 触发器 5731353086021851682 绑定 TrapEventHandlle.PlayKnock；该组件绑定同一 Actor、Knock_Explosion 和力度1。只能说死亡链绑定存在，不能把当前地图描述为会触发地刺伤害。
- 反重力区域实例 8198435457132530806，源 GUID 28708e2742eeaa04289e119786a027e3；位置 (1.12,-3.36)，矩形 5×10.29，描边、颜色、平铺及地形布局保持。
- 每关132个YAML对象、252处本地引用；只更改Tips实例内mTerm，131个完整对象块不变。原有关闭平台/背景节点也保持。

## 数据、资源、提示与界面流

- 数据ID 20094 → DLevel_094，排序94 → 20094已经存在，无重复新增或重排。版本1.15.0、levelType=0、lockType=1保持。
- 独立提示 S_DrawLevel_Tips_Level_94 从90复制既有完整语言/Flags，中文“反重力区域”。原来03提示“小心地刺！”与关闭地刺不符，故只更新本关独立文案；不改03、90等原词条。
- LevelData.xlsx/draw_level第99行候选修改说明及H:K为20/0/5/10；LevelSort.xlsx/drawLevelSort第99行候选仅改说明。运行时tbdrawleveldata.json已定点对齐94/95，其它条目以及整个排序JSON与本轮前相同。
- 源Excel仍被占用，写回待完成。最新合并候选在 outputs/dlevel94-95-20260916/LevelData.xlsx、LevelSort.xlsx，包含92/93先前待写回修改。install_workbooks.py写前比较原文件字节，并先同时打开两表；若用户保存了新改动，必须先合并，不可覆盖旧候选。
- 资源随原Prefab入口加载；无新美术、全局事件、UI类、SDK或云控发布。当前配置仍有后续96等未制作占位；不代表95之后连续游玩已验收。

## 验证、差异与结构复盘

与行走基线的差异由当前地图和区域参数承载，不增加编号空壳控制器，不改共享Actor或旧关。复制关卡应按实际角色、首笔行为、机关显隐和绑定判断，不能仅按源编号推断玩法；本次没有形成共享迁移。

两关26项隔离Unity检查通过；生产D001/D88代码逐字复制，Actor/主控为替身，覆盖行走输入生命周期及真实Physics2D区域进出。静态Prefab/词条/配置/Luban核验通过，2329份既有脚本、Prefab及元文件哈希保持。完整地图、正式Spine/Actor运动与地形接触、移动端、重开/成功/退出/解锁和打点仍为uncertain。详见相邻功能与风险评估。


## 2026-09-16：待写回源表候选合并至D096

源表继续占用，最新合并候选移至 outputs/dlevel96-20260916/LevelData.xlsx 与 LevelSort.xlsx，包含92～96。后续写回使用该目录install_workbooks.py检查源内容是否变化；若有用户新保存需重新合并，不使用旧候选覆盖。本关运行JSON条目未被D096任务修改。
