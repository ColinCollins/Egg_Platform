# DLevel_093 脚本结构记录

## 需求与参考基线

2026-09-16，用户要求制作D093，并明确确认：首笔放球，利用反重力进入成功门，15秒超时失败，无额外规则。

以D089/D088的反重力单球进门结构、D002首笔放球控制器为基线。用户现有D093已具备完整绑定；保留当前地图和40墨水、星级0/5/15、线质量150，不新增DLevel93Ctrl，不改旧关/共享源码。已有文档不能替代当前Prefab参数。通用feature plan `specs/037-remote-resource-loading/plan.md` 当前不存在，实际资产和共享接口作为本轮依据。参考关的完整设备验收仍为uncertain，不虚称整关已验收。

## 脚本树与职责

```text
DLevel_093
├─ DrawLevelCtrl                输入/结果门禁、成功门演出、失败重开接口
├─ DrawLineFeature              多笔物理画线，40墨水，0/5/15
├─ DrawLevelCountdownCtrl       首笔后15秒失败，最后5秒显示HUD
├─ DLevel2Ctrl                  单猫夹球入场、首笔释放、Retry夹持态
├─ Alien                       嵌套DrawGame_AlienClipBallRig
│  └─ yellow_ball               球Rigidbody、Collider、payload/角色和渲染组
├─ DLevel088_ReverseGravityZone 底部30×5反重力带
├─ Sp_hx_yxwp_01                静态圆形障碍（Static刚体+CircleCollider）
└─ level2 / 成功门 / 平台 / 地形 / 四向失败区 / 相机 / Tips
```

- D2只负责已有入场和放球。DrawLevelCtrl可见Intro链返回后开放画线；首笔完成后D2幂等开夹、球脱离动画层级、切Dynamic。倒计时同步启动，后续笔画不重新开始。
- 区域以绑定PolygonCollider检测动态刚体，多个子Collider共用父刚体时只反转一次；离开最后区域、区域停用/销毁后恢复原重力。Kinematic夹持球和Static圆形障碍不参与。无需新的重力控制器或全局参数。
- 门的onEnter显式调用本关DrawLevelCtrl.OnTriggerSuccess，沿原成功占用、门演出和结果流程结算；球壳额外渲染组随成功演出处理。15秒倒计时`isSuccess=false`，归零调用原TryFailAndReset，不以存活到时判成功。
- 暂停不扣倒计时；Resolved停止倒计时。D2 AttemptReset应用已有Retry入场完成态并恢复夹持球，倒计时Reset停止并隐藏HUD；实际失败/Replay由既有DrawPlayCtrl重建实例，退出由原销毁链清理。未修改Reset、存档、打点或转场时序。

## 显式绑定与保留布局

- Prefab GUID `7de98cbaaa5b48e4085231e1b21f69a5`保持。本轮唯一Prefab差异为`S_DrawLevel_Tips_Level_90 → S_DrawLevel_Tips_Level_93`，158个原对象块逐字保留。
- 主控`900000000000000001`，画线`900000000000000002`；成功门Spine`2984154252043883271`、球壳渲染组`2658148689794915295`。
- D2组件`3876599664482289427`，球刚体`6404711775717088776`，释放父级`7090627380450863792`；Alien、Spine、BoneFollower和摆动支点均已显式绑定。球刚体和成功额外渲染组同属Rig实例`2737920895360066124`。
- 倒计时`900000000000000005`绑定本关主控，cdTime15/isSuccess0；门onEnter目标也是本关主控。没有双球成功Handler或平衡关计时成功。
- 区域嵌套实例`880000000000000001`，共享GUID`28708e2742eeaa04289e119786a027e3`。位置(-0.14,-4.24)、矩形30×5、上边界y=-1.74、下边界y=-6.74。颜色/描边0.2/平铺3.495×3保持。
- 圆形障碍位于(-0.13,-2.95)、缩放0.7070755，原Static刚体保持；不擅自添加转动、下落或伤害机制。Alien最终位置(-7.91,1.97)保持。
- 画线isConfig=false、40墨水、0/5/15、质量150及按长度缩放保持；线模板实例重力3/角阻尼10保持。

## 配置、文案与交付状态

- 独立I2键`S_DrawLevel_Tips_Level_93`完整复制90现有语言/Flags，中文“反重力区域”；原词条和其它语言不变。
- Excel已预留D093：LevelData.xlsx/draw_level第98行20093→DLevel_093，版本1.15.0、类型0/锁1保持；候选仅把D98说明、H98:K98参数对齐地图。LevelSort.xlsx/drawLevelSort第98行排序93→20093保持，仅D98说明改名。
- 承接D092未写回内容，本轮两份Excel候选同时包含第97行的92标题及40/0/10/20。源表被占用，**写回待完成**；最新合并候选和并发检查脚本位于`outputs/dlevel93-20260916/`，替代上一关单独候选用于后续写回。
- 当前运行JSON在本轮开始前已被导成1～100，新排序已含92、93，本轮不追加或重排；仅tbdrawleveldata把92/93的默认15墨水恢复/对齐对应Prefab参数，其余记录与排序保持。D092早期文档“旧1～66后接92”是当时状态，不再描述当前配置。
- 资源/选关/星级/存档/analytics仍沿原ID与Draw流程；本轮无新美术资源、运行时类、UI、数据库字段或远端发布。原JSON包含尚未制作的94等后续资源，属已有占位，本轮不替它们制作地图或改变排序。

## 验证与结构复盘

本关直接复用D2、通用倒计时和D88区域，以配置身份隔离新关。无需空壳控制器；新的地图方向不构成修改旧区域脚本的理由。当前配置必须读实际文件，不沿用上轮“新增排序”假设。具体验证范围、未覆盖项和回滚见相邻风险记录。


## 2026-09-16：待写回源表候选合并至D094/D095

源表仍处于占用状态。最新候选改为 outputs/dlevel94-95-20260916/LevelData.xlsx 与 LevelSort.xlsx，保留本关待写回修改并包含92～95；对应 install_workbooks.py 按最新快照检查并发改动。请勿继续使用较早单关候选覆盖后续配置。本关Prefab、玩法和本轮开始时运行JSON条目未被94/95任务改变。


## 2026-09-16：待写回源表候选合并至D096

源表继续占用，最新合并候选移至 outputs/dlevel96-20260916/LevelData.xlsx 与 LevelSort.xlsx，包含92～96。后续写回使用该目录install_workbooks.py检查源内容是否变化；若有用户新保存需重新合并，不使用旧候选覆盖。本关运行JSON条目未被D096任务修改。
