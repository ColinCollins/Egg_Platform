# DLevel_092 脚本结构记录

## 需求与基线

2026-09-16，用户要求“来做D092，过关方式和D006一样”。以当前 D006 实际 Prefab 和生产源码为通关行为基线；D006 旧记录中“碰撞直接成功”等早期描述已被双球相遇演出取代。

D092 已由用户复制 D006 并加入右侧反重力区。核对后原有双球释放和成功组件已完整，因此只读复用，不创建 DLevel92Ctrl。保留用户地图、夹子姿态、两球镜像、反重力区域与碰撞设置。通用 feature plan `specs/037-remote-resource-loading/plan.md` 不存在，本关依据当前 Draw 契约与源资产实现。

## 实际脚本树与职责

```text
DLevel_092
├─ DrawLevelCtrl                 输入、首笔派发、两阶段成功、重开接口
├─ DrawLineFeature               多笔物理画线，40墨水，星级0/10/20
├─ DelayAutoBeginDrawHandle      可见入场后放行绘制
├─ DLevel6Ctrl                   双Alien进场、首笔双球释放、重置
├─ DLevel001Ctrl                 复制基线保留；actor/guide为空，不承担本关成功
├─ level6 / 左右Alien            共享DrawGame_AlienClipBallRig嵌套实例
│  ├─ 左yellow_ball              Rigidbody2D + CircleCollider2D
│  └─ 右yellow_ball              Rigidbody2D + CircleCollider2D
│     └─ DrawTwoBallReunionSuccessHandle（显式配对左球）
├─ DLevel088_ReverseGravityZone  右半场动态刚体反重力
└─ GameCamera / Tips / 四向失败区 / 用户地形
```

- D6 入场完成才可画线；首笔有效完成打开双夹，同时将两球从骨骼/摆动层级转挂稳定父节点、切 Dynamic。右球镜像沿原实现移入 payload，球根保持正缩放。后续笔画不会重复放球。
- 成功只接受配对球刚体接触。先通过 `TryBeginSuccessPerformance` 占用成功，再清零两球速度/角速度，切 Kinematic + FreezeAll；地面、画线或其它物体碰撞不能直接过关。
- 接触点播放爱心；镜头0.28秒聚焦至原正交尺寸的0.91倍；总演出1.8秒，正式成功前0.22秒恢复镜头，通过 `CompleteSuccessPerformance` 沿原结果链结算。爱心尺寸1.5倍、每秒4颗、Sorting340，复用 Bubble 和 Resources/Sp_hx_ax_01。
- 暂停由共享成功Handler显式暂停 Tween/粒子。AttemptReset、Disable/销毁取消旧演出并恢复镜头/初始刚体状态；D6恢复父级及初始姿态。实际 Replay 继续由 DrawPlayCtrl 重建实例。
- 反重力只管理动态刚体重力，不覆盖成功Handler的刚体类型或约束；两球成功冻结后退出其动态租约。关外失败继续走原失败区；本关没有新增倒计时或成功门。

## 显式绑定与本关参数

- Prefab GUID：`c8a3999d5f0451e4d9e2b2e9fddcad0d`。本轮唯一 Prefab 内容变化为提示键 `S_DrawLevel_Tips_Level_06 → S_DrawLevel_Tips_Level_92`。
- 根 D6 fileID `900000000000000005`，firstRig `2927379990285402166`，secondRig `6319669924655802215`，ballReleaseParent `2298191772296322170`。
- 右球成功组件 `6016664840628768835`，otherBall `4113373739594850306`，drawLevelCtrl `900000000000000001`，presentationCamera `2887384192957805437`。两个球的 CircleCollider `ExcludeLayers=0`，避免共享装置原 Actor 层排除阻挡双球相遇。
- 原画线 `isConfig=false`，40墨水、0/10/20、质量200、按长度缩放质量均保留；Excel预留行原为15/0/2/5，本轮候选配置对齐地图现值。
- 反重力根位置 `(7.97,-1.03)`，矩形 `(15.69,16.42)`，左边界约 `x=0.125`；填色、描边、材质与源Prefab都保持。

## 本地配置与文案

- I2新增独立 `S_DrawLevel_Tips_Level_92`，完整复制06的语言数组/Flags，中文“两个Boxy需要在一起！”。原06及其它词条逐字保持。
- Excel已有 D092，不重复追加：LevelData.xlsx / draw_level 第97行 `20092 / DLevel_092 / 1.15.0`；LevelSort.xlsx / drawLevelSort 第97行 `92 → 20092`。候选仅改两表D97说明和LevelData的H97:K97。
- 两份运行Draw JSON仅合入Luban隔离生成的20092及排序92；全部旧运行记录保持。Excel此前提交的新全量排序未统一导出，故当前运行列表是旧1～66后接92；这里的92是排序ID，不是列表第92个。推进按列表下一项查询。后续统一导表时须独立验收新排序和旧存档，不能把本轮定向合入当作全量同步完成。
- **源Excel写回待完成**：文件被表格软件占用。已生成保持原格式的候选 `outputs/dlevel92-20260916/LevelData.xlsx`、`LevelSort.xlsx`；关闭源工作簿后再核对并写回。
- UI、存档、资源加载、失败/成功事件、星级与analytics继续既有链路。本轮未修改 C#、共享资源、D006、D088或GF。

## 验证与回滚

验证范围、证据、未验证项和回滚见同目录《DLevel_092功能与风险评估.md》。没有修改已验收关卡的公共脚本，没有新增全场景查找或并行控制器。

## 2026-09-16 D093制作时的配置状态复核

D093任务开始前，运行Draw JSON已重新导成源表的1～100顺序，本文此前“旧1～66后接92”仅为D092初次交付时状态。D093任务保留新排序，将被旧源表覆盖的D092参数恢复为地图40/0/10/20；源Excel仍占用，最新待写回候选已合并92/93两行，位于outputs/dlevel93-20260916，后续安装应使用合并候选并重新检查并发编辑，不能用旧D092整表覆盖D093。

## 2026-09-16：待写回源表候选合并至D094/D095

源表仍处于占用状态。最新候选改为 outputs/dlevel94-95-20260916/LevelData.xlsx 与 LevelSort.xlsx，保留本关待写回修改并包含92～95；对应 install_workbooks.py 按最新快照检查并发改动。请勿继续使用较早单关候选覆盖后续配置。本关Prefab、玩法和本轮开始时运行JSON条目未被94/95任务改变。


## 2026-09-16：待写回源表候选合并至D096

源表继续占用，最新合并候选移至 outputs/dlevel96-20260916/LevelData.xlsx 与 LevelSort.xlsx，包含92～96。后续写回使用该目录install_workbooks.py检查源内容是否变化；若有用户新保存需重新合并，不使用旧候选覆盖。本关运行JSON条目未被D096任务修改。

## 2026-10-12：共享暂停恢复输入修复

- 用户反馈本关不能画线，当前 Editor.log 显示设置暂停打开后双夹 Intro 才完成。原共享 Resume 仅解除暂停，入场结束时被置 false 的画线开关未恢复；本关仍复用 D6，没有新增编号控制器。
- 按用户确认方案，只修改 `DrawPlayCtrl.Resume`，进入 Drawing 且不在加载/尝试切换、runtime 已就绪时补开输入；`DrawLevelCtrl.SetInputEnabled` 保留成功/失败演出和已结算门禁。首笔双球释放、配对成功、反重力与资源绑定不属于此次改动。
- 本轮开始 DLevel_092.prefab 已有用户/外部 diff，未覆盖。当前检查时画线为 `isConfig=false/maxInkLength=43`，前文 40 是初始交付基线，不替代当前 Inspector 参数。
- 编译和隔离分支验证范围、真实关卡未覆盖项与局部回滚见同目录风险记录及共享 `DrawLevelCtrl功能与风险评估.md` 第 66 节。
