# DLevel_034 双风车功能风险评估

## 1. 影响范围与原始状态

- 目标 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_034.prefab`。
- 复用系统：`DLevel2Ctrl` 猫夹闭合入场与首笔放球、`DLevel4Ctrl` 首笔刚体释放、`DrawLevelCountdownCtrl` 倒计时
  成功、共享 FallArea 失败链。
- 原始风车只有视觉层级，无法碰撞或绕齿轮旋转；根倒计时已经是 7 秒成功，本轮不改变成功规则。
- 本轮不修改已有 Excel/JSON 配置、共享 C#、存档、UI、资源加载或平台系统。

## 2. 风险与控制

| 风险 | 等级 | 当前控制 |
| --- | --- | --- |
| 风车没有碰撞，画线无法施力 | 高 | 四片叶片分别增加贴合视觉的 BoxCollider2D |
| 整组风车被推走而不是绕中心旋转 | 高 | 每组根刚体使用 HingeJoint2D 固定到当前中心世界锚点 |
| 玩家画线前机关提前转动或 Actor 掉落 | 高 | 两组刚体初态 Kinematic，复用现有 DLevel4Ctrl 首笔同步释放 |
| 铰链限制角度或电机改变纯物理手感 | 中 | motor/limits 均关闭，不使用脚本 AddForce/AddTorque |
| 碰撞盒覆盖透明边缘造成误碰 | 中 | 碰撞盒使用 `4.55 x 0.45`，小于精灵 `4.81 x 0.59` |
| 新增其它成功条件与倒计时竞争 | 高 | 只保留现有 `DrawLevelCountdownCtrl(cdTime=7,isSuccess=true)` 提交成功 |
| 越界后没有失败保护 | 高 | 四周复用 D037 同布局的共享 FallArea |
| 手写 YAML 引用断链 | 高 | 静态检查 anchor 唯一，本地 fileID 引用闭合；Unity 已成功导入 |
| 猫夹入场时角色提前受重力下落 | 高 | 共享球初态 Kinematic，开夹脱离层级后才切 Dynamic |
| 入场方向仍沿复制关卡从左侧进入 | 中 | 起点标记改为本地 `(0,5.277918)`，`DLevel2Ctrl` 直接按标记从正上方移动 |
| 入场结束就错误开夹放球 | 高 | `DLevel2Ctrl` 的 Intro 只保持 `clip_close`；首笔完成回调才 `clip_open + ReleaseBall` |
| 动画未完成便可画线 | 高 | `DLevel2Ctrl` 作为可见 Intro 被顺序等待，闭合入场及惯性摆动收稳后才结束门禁 |
| 覆盖用户刚调整的猫夹位置 | 高 | 保留 Rig 最终 `position=(-0.1,5.2)` 与现有子 Transform，只修改入场标记和控制器引用 |
| 运行时数据存在但菜单排序缺失 | 中 | 已确认 `20034 -> DLevel_034`；当前排序 JSON 无 `20034`，不覆盖用户工作簿并标记 `uncertain` |

## 3. 验证与未验证项

- 静态回读确认两组风车根刚体为 Kinematic、mass 5、gravity scale 0，均绑定到 `DLevel4Ctrl`。
- 两个 HingeJoint2D 的 anchor 为 `(0,0)`，connected anchor 分别为 `(-2.61,-0.63)` 与
  `(2.55,-0.63)`，motor/limits 关闭。
- 四个叶片碰撞盒、四个共享 FallArea、`DLevel2Ctrl` 与 Rig 内部引用均已计数；Prefab 为 173 个唯一 anchor、
  0 duplicate、0 missing local reference。
- 静态回读确认共享球初态 Kinematic，D034 覆写 `mass=5`、`gravityScale=1.5`；控制器仅在第一笔完成后
  开夹、脱父级并明确切 Dynamic，Rig 最终 Transform 未改。
- Unity PrefabImporter 已成功，Unity 自带 Roslyn 完整编译退出码 0；PlayMode 的上方闭合入场、第一笔
  开夹落球、低帧率高速碰撞、第一笔三路回调顺序、暂停、连续 Replay、
  FallArea 失败、7 秒成功和 Android/iOS 仍需动态验证，结论为 `uncertain`。

## 4. 回滚路径

- 风车物理回滚必须同时恢复刚体、铰链、叶片碰撞盒和 `DLevel4Ctrl` 绑定，避免留下半套物理拓扑。
- FallArea 可按四个 nested instance 成组回滚，不影响倒计时或风车组件。
- 猫夹接线可单独回滚根 `DLevel2Ctrl`、stripped Rig 内部引用和入场起点覆写，不回滚用户当前 Rig 布局。
- 现有 7 秒倒计时不是本轮新增内容，回滚本轮时保持不变。
