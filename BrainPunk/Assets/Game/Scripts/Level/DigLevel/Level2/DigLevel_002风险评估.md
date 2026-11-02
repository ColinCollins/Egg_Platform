# DigLevel_002 入笼风险评估

## 2026-10-13 多角色与碰撞隔离增量

- 行为基线：原002单角色到达即锁整关，根南瓜缩放包含静态碰撞。多角色改为部分到达仍继续玩法，旧安全前提不再成立。
- 处理：原图和Light移至无碰撞Visual，动效仅绑定Visual；保留原物理节点、Renderer文件ID和地图／Base配置。002保留历史单actor兼容；基础目标Prefab同步隔离，使003及目标变体复用。
- 证据：真实Unity23项包括002旧单角色、003部分到达时Visual变化而所有Box世界Bounds保持、三个变体Renderer有效；Editor／Android条件编译0错误。5个既有块和2个新增块是本次002资源范围，其余174块保留，逐块结果见prefab-blocks.txt。
- 未验证：主工程完整视觉／UI、003实际路线、真机及物理性能为uncertain；不因隔离验证宣称全关完成。恢复旧根缩放会重新引入多角色玩法中的墙体移动，应同时回退多角色行为。
- 回滚：按TEMP brainpunk-multi-actors-20261013的before.prefab定向恢复Visual相关块及动效绑定，不覆盖地图、排序或其它用户编辑。共享多角色风险与验证见模块当前说明和Dig多角色验证记录。

## 2026-10-13 成功缩放摇摆增量

- 用户已确认到达成功，现要求南瓜表现可爱且干脆；新增本关DigPumpkinSuccessEffect和显式OnReached持久监听，保留点亮监听。0.36秒压扁／上弹／轻摆／归位，默认XY缩放幅度0.1、最大Z摆角6度；不循环、不延长原1秒结算。
- 维护边界：用户当前Base、地图、材质、音频、渲染排序和Actor原样保留。Sprite和静态Collider在同一南瓜节点，动画同时改变两者变换；仅在BeginVictory锁定并冻结主角／关闭输入后执行，重复接触拒绝，无额外胜负入口。半途禁用／销毁或重播停止自有Sequence并恢复原姿态，未引入全局事件、DB、实例生成或Update轮询。
- 性能：仅自然成功时创建一个Sequence和8个Tween，0.36秒结束，无长期轮询；Mobile IL2CPP／真机内存／60fps未实测。普通暂停已被原结果锁拒绝，背景期间不依赖ignoreTimeScale推进；不改变原暂停／结算时序。今后将VictoryDelay调短可能截断表现，应保证动效短于延迟。
- 验证：真实Unity／DOTween14项场景通过，含实际Trigger、一次动效、XY/角度时间线、归位、原1秒结果、禁用／失活／重建、重播不累乘；持久监听保存重导入和Editor／Android条件编译0错误。177个未涉Prefab块逐字一致。完整生产UI／原生广告／双端真机与主观美术仍uncertain。
- 证据与回滚：`DevelopMD/验证记录/Dig南瓜成功动效_2026-10-13/`；只撤新增组件／脚本及第二个OnReached监听。TEMP备份 `brainpunk-pumpkin-fx-20261013/before.prefab` 保留动画前用户布局，不回滚本关其它修改或共享目标／Actor／控制器。

## 基线、范围和复用

基线为用户已有002、Static笼子和熄灭灯光；原目标未绑定，没有实体边界。复用 DigTargetTrigger／TryReachTarget 的身份、编号和结果门禁、原延迟结算、Actor物理及Editor InitMap。修改前核对见 `DevelopMD/已有功能设计与使用记录/2026-10-13.md`。本次有意改变002的入笼物理和成功亮灯；共享事件为空时保留其它关卡原逻辑。

## 风险和验收

| 项目 | 处理和验收依据 |
| --- | --- |
| 笼口提前冻结 | 目标移至底部薄Trigger；大Box只作地图轮廓，真正开口下落必须验证 |
| 沙土挡住内部／漏底 | Editor烘焙内部空腔，左右墙与底纵向重叠0.03；使用实际Tilemap下落及独立墙体检查 |
| 重复亮灯／错误Actor | UnityEvent仅在TryReachTarget返回true后执行，使用既有关卡结果锁；核对重复、暂停、编号和不同Actor |
| 重开残留亮灯 | Prefab保持inactive；核对新实例而非添加通用清理 |
| 视觉排序 | 去除笼整体组，后片9／Actor10-11／Light12；现有灯图是正面脸部，并非完整笼身前景，完整遮挡体验仍需主工程试玩 |
| 性能 | 仅3个固定实体Box、一个成功Trigger和一次UnityEvent；不加轮询、实例生成、GPU读取或运行时烘焙。Android／iOS真机60fps未经测量 |
| 结算／广告／存档 | 保持原BeginVictory／Submit链；隔离测试不覆盖正式HUD、广告、资源包和存档，标记uncertain |
| 并行Prefab编辑 | 从最新用户基线在隔离项目配置；按YAML块合并并核对未涉对象，不覆盖用户资源布局 |

## 验证证据、未决项和回滚

真实Unity6000.0.59f2：14项Editor检查、26项PlayMode检查通过；合并后的Prefab再次导入并重跑物理／回调检查。笼口无提前冻结、笼口上方实际挖竖井后入笼成功、灯光一次回调、重建熄灯、实体墙／底、不匹配Actor／Index、暂停、重复、禁用、延迟0及空事件均通过。原出生点竖直路径则被现有FixedBrick阻挡，实际接触该砖并停稳y≈0.75，不误亮灯；保留布局，需要绕砖通道，尚未证明整关实际路线已通关。Editor／Android条件C#编译0错误；156个未涉YAML块逐字保留，预览材质不变。原隔离工程相机脚本GUID冲突以及第二实例未挖竖井的底板测试摆放问题，已在TEMP修正后重跑，不改变生产代码应对测试环境问题。详见 `DevelopMD/验证记录/Dig入笼_2026-10-13/验证记录.md`。

双端真机、生产完整进入／不同挖掘路线／暂停／胜利／广告／Reset／退出链仍uncertain，需主工程重新载入保存Prefab并试玩。灯光Sprite和笼身的完整遮挡效果只检查排序及激活绑定，未进行主工程视觉验收。没有阻塞本地配置的未决产品取舍。

回滚只恢复002笼子相关块、对应MapMask／MapPreview与DigTargetTrigger本轮事件增量；不恢复或删除用户新资源、不改Packages／GF／GuruSDK。备份和日志目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-cage-20261013/`。
