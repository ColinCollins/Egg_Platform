# DLevel_096 脚本结构记录

## 需求与参考基线

2026-09-16，用户要求制作D096，明确“石头也要受到重力区影响”。保留当前Prefab的行走角色、首笔启动、三个反重力区域、10秒超时失败及进门成功。画线参数保持80墨水、星级0/20/40、线质量200。

参考中央Draw结构规范、D004首笔刚体释放/石头碰撞记录、D095行走结构及D088区域说明。D004旧“箱子释放”文档已明确是历史记录，实际复用当前DLevel4Ctrl与石头结构，不依据历史幂等字段描述修改源码。各参考关完整设备验收仍为uncertain。通用feature plan `specs/037-remote-resource-loading/plan.md` 缺失，无有效CodeGraph索引，依据当前代码、Prefab和真实物理对照核验。

## 原问题与最终差异

- 原地图有15个激活的石头，每个都有Kinematic Rigidbody2D、非Trigger PolygonCollider2D、DLevel4RockTrapHandle。区域的affectedLayers已包含石头Default层。
- DLevel4Ctrl.boxRigidbodies原有10槽：5个有效引用、5个空引用；其余10块石头没有绑定。区域明确只处理Dynamic刚体，漏绑石头首笔后仍为Kinematic，不能响应重力。
- 本轮只把释放列表补为15个不同且有效的现有刚体引用；保持初始Kinematic，首笔完成后统一Dynamic并唤醒。区域复用原D88代码，进区gravityScale由1变为-1，离开最后一个区域恢复1；不添加上抛力或瞬间翻转速度。
- 本次写入的另一项变化为独立提示键04→96。15块石头的层级、位置、旋转、碰撞形状、质量、阻尼、初始重力、陷阱绑定保持。期间工作区另有两块区域宽度5→4及Collider同步保存，本次保留这些并发变化，按最新尺寸补跑检查；没有新增运行时组件或修改旧关/共享代码。

## 脚本树与职责

```text
DLevel_096
├─ DrawLevelCtrl                 主运行时、输入/结果门禁、成功门演出、失败重建接口
├─ DrawLineFeature               80墨水、多笔物理线、有效笔画结果
├─ DLevel001Ctrl                 首笔后向右驱动独立Actor
├─ DLevel4Ctrl                   首笔将显式列表中的15块石头切为Dynamic
├─ DelayAutoBeginDrawHandle      首次可见Intro，额外延迟0秒
├─ DrawLevelCountdownCtrl        首笔后10秒失败，最后5秒显示原HUD
├─ Actor                        独立Actor.prefab / ActorCtrl
├─ 石头×15                      Rigidbody2D + PolygonCollider2D + DLevel4RockTrapHandle
├─ DLevel088_ReverseGravityZone×3
└─ 成功门 / Knock_Explosion / 平台 / 背景 / 相机 / Tips
```

三个首笔消费者均实现IDrawStrokeCompletedHandler，通过现有DrawLevelCtrl分派：D001开始行走，D4释放石头，倒计时启动。职责相互独立，不新增顺序依赖、全局事件或D96空控制器。D4保持既有非首笔门禁，不添加新的重复首笔语义。

## 生命周期、成功与失败

1. Prefab实例创建时，所有石头静止，区域运行态按rectangleSize生成轮廓；区域忽略Kinematic石头。可见Intro完成后按既有流程开放画线。
2. 首笔完成触发行走、释放和10秒倒计时；区域在下一物理扫描纳入已释放石头。当前15块初始位置都在区域内，因此释放后均获得向上的重力加速度。
3. 后续笔画不重复释放。角色进入门后走当前Actor身份/死亡/结果门禁及门演出；石头本身不会满足本关Actor成功条件。
4. 每块石头继续通过DLevel4RockTrapHandle识别已绑定Actor或其子Collider；命中后沿原接触点粒子、Actor.Die、击退、音效和延迟陷阱事件链失败。新增可运动石头也保留伤害能力。
5. 暂停清理行走输入、停止扣时；成功/失败Resolved停止行走与计时。区域不负责角色动画、输入或结算。
6. 当前Reset/Replay由原流程销毁并重建DLevel，石头恢复Prefab初态。D4的OnDrawAttemptReset为空，不会原地把石头还原，不为当前不存在的原地重置新增状态缓存。区域停用/销毁释放重力所有权。
7. 存档、解锁、打点、失败演出、转场和平台流程沿用既有实现，本轮未修改。

## 序列化引用与保留参数

| 对象/组件 | 绑定或参数 |
| --- | --- |
| DrawLevelCtrl 900000000000000001 | DrawLineFeature 900000000000000002；门Spine 2984154252043883271；普通Actor无球壳，额外组0 |
| DLevel001Ctrl 900000000000000003 | 同根主控；Actor 1716601560298323527；guideRoot=0 |
| DLevel4Ctrl 8129912782062927283 | 当前15个Rigidbody2D完整集合，逐一Inspector引用，无空项/重复/新增运行时搜索 |
| 倒计时 3865479001201854532 | 同根主控，cdTime=10、isSuccess=0 |
| DelayAutoBeginDrawHandle | 同根主控，delay=0 |
| 15个石头陷阱 | 同一Actor 1716601560298323527、粒子3757748896162189796、isCollision=1 |
| 成功门 6932855938480942226 | 位置(8.3,-1.28)，onEnter指向本关DrawLevelCtrl.OnTriggerSuccess |
| 石头物理 | Layer0、质量1、gravityScale=1、角阻尼0.05、初始Kinematic、非Trigger原多边形 |
| 三个区域 | 位置(-5.94,-9.96)、(4.87,-5.59)、(-0.55,-7.81)，当前均为4×13.3；共享区域GUID28708e2742eeaa04289e119786a027e3 |

最初只读审计时三块区域宽度为5，复制后的Collider高度缓存仍为旧值；备份及实现期间工作区陆续保存了宽度4和对应Collider。最终验证用当前三块4×13.3区域以及生产脚本重建后的实际Collider，不用初次审计缓存推断运行边界。

## 配置、提示、资源与交付状态

- 数据20096→DLevel_096、排序96→20096已经存在；不重复新增、不重排。类型0、锁1、版本1.15.0保持。
- 独立I2键S_DrawLevel_Tips_Level_96完整复制90现有语言及Flags，中文“反重力区域”。旧04与90等词条保持。
- 本地tbdrawleveldata.json只把20096的占位15/0/2/5对齐为Prefab的80/0/20/40，其余数据行和整个tbdrawlevelsort.json保持本轮前状态。
- LevelData.xlsx/draw_level第101行候选更新D101说明、H101:K101参数；LevelSort.xlsx/drawLevelSort第101行候选仅更新D101说明。
- 源Excel被占用，尚未写回。最新两份合并候选在`outputs/dlevel96-20260916/`，包含D092～D096全部待写回内容，取代此前候选。安装脚本写前核对原字节并先打开两表；若源文件被用户保存修改，必须重新合并，不能覆盖。
- 无新美术、UI、资源加载代码、存档字段或远端发布。当前97之后仍为预留内容，96之后完整连玩不在已验证范围。

## 验证与结构复盘

`outputs/dlevel96-20260916/verification.json`记录244个对象、518处本地引用闭合；本次写入释放列表与Tips两个对象块，额外保留工作区并发保存的两个区域块，240个其余原块逐字保持。2339份旧脚本、Prefab和meta哈希一致；另外D094在期间发生独立地图修改，记录在concurrent-protected-changes.json，本轮未写入或撤销这些内容。

真实Unity隔离物理前后对照54项通过：原仅5块释放、修复后15块按实际多边形与位置向上运动、离区恢复、再次进入、两区共同作用、区域启停及3次重建等。D001、D4、倒计时和D88源码逐字复制；Actor/HUD/主控为替身，完整地图、动画、伤害失败/门演出、平台与打点仍为uncertain。详见相邻风险记录。

复盘决定：使用现有显式刚体列表修复复制后漏绑，保持首笔门禁；不把Kinematic强行加入重力区，不改旧D4/D88，也不新增自动扫描或新的共享抽象。复制机关后应核对“实际刚体集合”和“释放引用集合”是否一致。

## 2026-09-16：画线不接收角色阴影

用户反馈“踩线上不要有影子”。原D096画线模板继承Ground层6，本关ShadowController.groundLayer=64，因而运行时线段被视为阴影接收面。最终在本关Prefab中覆盖画线模板m_Layer=13（WithoutShadowGround），Actor.whatIsGround从16712增加该层至24904；阴影接收层仍为64，ignoreLayer继承原值0。

只排除接收层仍会向下层地面投影；使用ignoreLayer又不能完整覆盖线端，且会因实体地面下方有线误屏蔽地面阴影。因此最终参考Level143“实际踩绳隐藏整组”的职责，在Level96目录新增DLevel096LineShadowHandle，挂本关根节点。它显式绑定DrawLevelCtrl、Actor主PolygonCollider2D及Shadow根，不扫描节点。每帧LateUpdate只读取Layer13的真实接触点：向上的支撑法线且在脚底0.2范围内才隐藏整组；侧碰/顶线不计。离线恢复进入前activeSelf；结果锁定后不再写可见性，避免成功淡出时重新开影子；非结算态停用恢复。现有整关重建负责Reset/Replay，不新增事件、重置接口或统计链。

DrawLineFeature通过Instantiate复制模板，并让Box/Capsule子碰撞段继承笔画根层；全项目2D碰撞矩阵允许层13与Actor/石头碰撞，当前三个重力区affectedLayers全选，改层不会从区域筛选中排除画线。新增组件fileID910000000000000001绑定主控900000000000000001、Collider stripped910000000000000002（Actor源8856887770667473999）、Shadow stripped910000000000000003（Actor源408348339682887402），lineLayers=8192。

最终247对象、525处本地引用闭合；原根节点组件表、Actor实例、画线模板实例3块修改，新增组件及两个显式引用块，241原块保持。共享Actor、画线模板、ShadowController、DrawLineFeature、重力区Prefab和Physics2DSettings六份文件哈希不变。证据：`outputs/dlevel96-line-shadow-20260916/verification.json`。

Unity隔离PlayMode使用正式ShadowController、新组件源码及正式Actor七点多边形，26项检查通过，包括水平线、正负20/40度斜线实际接触、线端与下层地面、普通地面下面另有画线、侧碰/顶线、离线恢复、结果锁和停用。DrawLevelCtrl结果锁为替身，完整地图、正式Actor动画、真正门演出和手机实机仍为uncertain，详见相邻风险记录。结构复盘：保留行走/释放/计时原组件，以本关独立表现组件处理接触边界，不改旧Level143或共享Actor/阴影算法。
