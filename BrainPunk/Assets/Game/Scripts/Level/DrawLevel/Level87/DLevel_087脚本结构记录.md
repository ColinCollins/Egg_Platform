# DLevel_087 脚本结构记录

## 基本信息与参考基线

2026-09-15，用户确认将误存入 D086 的今天新地图另存为 D087，同时恢复昨天 D086 的布局并保留防穿模修复。D087 为 20087 / 弹性的线4，复用 D086 最新的零弹线材质、有界球弹射和新线出生保护，而非早期仅靠高弹材质的方案。

Prefab：Assets/Game/Prefabs/DrawLevel/DLevel_087.prefab。
独立 GUID：5b15da667d504018a854e095ad5ac8fc。参考 Level86/Level85 脚本结构记录的最新章节及中央 DrawGame 规范。当前完成静态接入，整关试玩状态仍为 uncertain。

## 组件树、职责和接口

```text
DLevel_087
├─ DrawLevelCtrl                  尝试状态、输入、成功/失败与标准重建
├─ DrawLineFeature                多笔画线、墨水、完成笔画分发
├─ DrawLevelCountdownCtrl         首笔后 20 秒超时失败
├─ DLevel2Ctrl                    可见入场、猫夹球首笔释放、Retry 到位
├─ DLevel85SpikeTrapHandle        显式球身份判断、受击失败演出与取消
├─ DLevel84StrokeSpawnGuard       完成新笔画后暂缓动态出生重叠碰撞
├─ Alien / DrawGame_AlienClipBallRig
│  └─ yellow_ball / DLevel84BallSpeedLimiter
├─ SpikeStrip_00 / OnTrigger2DHandle
├─ DrawGame_SuccessDoor / rope    今天新地图的右侧成功门
├─ Ground_long                   今天新地图中下方水平地面
├─ StoneB (1)、StoneB (2)、Ground_mid（停用）
└─ 相机、背景、Tips、提示线、出界区域与受击粒子
```

D002、尖刺及出生保护通过既有 IDrawStrokeCompletedHandler 边界消费首笔/重置；D002 同时实现 IDrawLevelVisibleIntroHandler。球限速器挂在实际球宿主，接收物理回调。没有新增 DLevel87Ctrl、共享接口、运行时查找、全局事件或 Actor API。

## 生命周期与关键调用

1. Draw 加载链按 DLevel_087 创建关卡与 HUD，D002 执行可见入场后开放输入。
2. 首笔完成后开夹、球脱离动画父级并切 Dynamic，启动 20 秒倒计时；后续画线不重复放球。
3. 线材质零弹，球组件在有效弹性线接触后每步至多补一次法向弹射，倍率 2，速度上限 30，窄框进一步降速；不改变角速度。
4. 出生保护通过已绑定 DrawLineFeature 的 TryGetCompletedStrokeObject 获取当前笔画，只暂缓出生时已重叠的动态碰撞对，分离超过 0.02 后恢复。
5. 尖刺只接受绑定球，先 TryBeginFailurePerformance，再受击/音效，0.5 秒后 CompleteFailurePerformance。成功门仍调用本关 OnTriggerSuccess；超时/出界沿既有失败链。
6. Reset/Replay 沿用黑幕下整关与 HUD 重建；组件仅清理自己的临时碰撞关系、入场与失败演出，不增加重复复位。下一关转场、暂停、存档、Analytics 和 SDK 归共享流程。

## 序列化绑定与资源

| 用途 | 明确绑定 |
| --- | --- |
| 根控制器 / 画线 | 900000000000000001 / 900000000000000002 |
| 出生保护 | 840860000000000001，drawFeature 指向本关画线 |
| 球刚体 / 限速器 | 6262018380601849612 / 840850000000000001 |
| 限速器宿主 | yellow_ball 本地对象 4500677910547608250 |
| 线材质 | 8db86cf100124ebfbbb13b4e50990f8f，实际 bounciness=0 |
| 尖刺 Handler / 碰撞体 | 850000000000000001 / 2235164531252919237 |
| 受击粒子 | 982062655520787473，实例名 DLevel087_Knock_Explosion |
| 有效成功门 | 2322687108484577280，事件指向根 OnTriggerSuccess |
| 成功 Spine / 表现组 | 7594331605050263445 / 3091325050892645083 |

继续只读引用 Level84 线/地面材质、Level85 球材质与已验证组件。墨水 50、星级 0/10/20、多笔、质量 32/最小比例 0.25、球滚动与回弹参数均未改。若未来只调整本关物理材质，先复制局部资源，不能直接改前关材质。

## 数据、标题与差异

- LevelData.xlsx / draw_level 第67行：20087 / DLevel_087 / 弹性的线4 / 1.14.0 / 0 / 1 / 50 / 0 / 10 / 20，位于20086后、20999前。
- LevelSort.xlsx / drawLevelSort 第67行：62 / 20087 / 弹性的线4，接在61 / 20086后。
- 两份 Draw JSON 与 Luban 生成内容一致，仅新增本关记录，不改变主线/IWanner或旧关排序。
- Tips 使用 S_DrawLevel_Tips_Level_87。本地 I2 从系列3沿用12种现有翻译并改系列序号为4，其余语言槽保持空，未写外部翻译源。
- D087 相比修改前误存的 D086，只有根名称、粒子名称、标题词条及缓存显示文本不同；地图、对象启停、参数与组件全部保留。
- 两个 Prefab 内局部 fileID 可相同，资源 GUID 独立；所有引用仍在各自实例内，不表示跨关共享运行状态。

## 结构复盘、验证与回滚

复盘结论：复制关卡时将地图身份与玩法实现分开。相同机制沿用实例组件，不添加空控制器；恢复旧地图只恢复历史布局块，保留后续修复组件，不直接用较早整文件覆盖。

本次两个 Prefab 各156个唯一对象、本地引用无缺失，42个外部 GUID 均可定位。逐块断言验证今天地图完整保留、昨天地图恢复、两关 guard/有界弹射保持；33个受保护前关/共享文件哈希不变。两表旧值/类型/样式、无关 ZIP 部件保持，局部渲染与错误扫描通过，Luban 校验和两份 JSON 增量核对通过。

没有 C# 改动，未重复编译或开展新一轮 Physics2D 压力测试。历史测试不是本关完整解法证明，当前路线、提示画法、连续失败重开、暂停和 Android/iOS 均为 uncertain。

证据与修改前备份：E:/WorkSpace/outputs/dlevel86-87-split-20260915/。回滚仅移除本关新增资源/词条/20087和排序62并重生成 Draw JSON；保留86恢复后的布局和其它用户改动。删除87前必须另存用户后续地图。完整风险见同目录功能与风险评估。
