# DLevel_034 脚本结构记录

## 1. 需求与基线

- D034 使用两组十字风车作为可受力机关，风车需要围绕各自中心齿轮自由旋转。
- 玩家第一笔有效画线后开始物理过程；关卡继续使用现有倒计时结束即成功，不新增专用过关判定。
- 关卡四周需要共享 FallArea，Actor 越界后沿用既有失败链。
- 角色已替换为共享猫猫夹子球装置；首次进关从正上方闭合入场，夹持球保持 Kinematic。入场收稳后
  允许画线，第一笔有效线完成时才开夹并把球切为 Dynamic。
- 修改前两组 `fengche` 只有 Transform、SortingGroup 和 SpriteRenderer，没有 Rigidbody2D、Joint2D 或
  Collider2D；根节点已有 `DLevel4Ctrl` 空刚体绑定和 `DrawLevelCountdownCtrl(cdTime=7,isSuccess=true)`。

## 2. 最终结构

```text
DLevel_034
|-- DrawLevelCtrl
|-- DrawLineFeature
|-- DelayAutoBeginDrawHandle
|-- DLevel2Ctrl                         # 共享猫夹闭合入场，第一笔后开夹放球
|-- DLevel4Ctrl                         # 第一笔后释放两组风车
|-- DrawLevelCountdownCtrl              # 第一笔后倒计时 7 秒，归零成功
|-- Alien                               # DrawGame_AlienClipBallRig，共享猫 + 夹子 + 球装置
|-- fengche
|   |-- Rigidbody2D                     # Kinematic -> Dynamic，mass 5
|   |-- HingeJoint2D                    # 世界锚点 (-2.61,-0.63)，无限位/无电机
|   |-- Sp_hz_mthz_01 / BoxCollider2D   # 横向叶片
|   |-- Sp_hz_mthz_01 (1) / BoxCollider2D # 纵向叶片
|   `-- Sp_hz_mtcl_01                   # 中心齿轮视觉
|-- fengche (1)
|   |-- Rigidbody2D                     # Kinematic -> Dynamic，mass 5
|   |-- HingeJoint2D                    # 世界锚点 (2.55,-0.63)，无限位/无电机
|   |-- Sp_hz_mthz_01 / BoxCollider2D
|   |-- Sp_hz_mthz_01 (1) / BoxCollider2D
|   `-- Sp_hz_mtcl_01
|-- FallArea_Left
|-- FallArea_Right
|-- FallArea_Bottom
`-- FallArea_Top
```

## 3. 行为与职责

- 两组风车初始保持 Kinematic，避免玩家画线前 Actor 重量提前推动机关；`DLevel4Ctrl` 在第一笔有效画线
  完成时把两个刚体同步切为 Dynamic。
- 每组 HingeJoint2D 的本地 anchor 为 `(0,0)`，connected body 为空，connected anchor 使用当前中心世界
  坐标；关闭 motor、limits 和相互碰撞，仅由 Physics2D 接触力矩决定转动。
- 四片叶片各有 `4.55 x 0.45` 的 BoxCollider2D，略收于 `4.81 x 0.59` 的精灵可见尺寸，降低透明边缘
  误碰；中心齿轮不增加碰撞盒。
- `DrawLevelCountdownCtrl` 保留 `cdTime=7`、`isSuccess=true`，与 `DLevel4Ctrl` 都由根 `DrawLevelCtrl`
  派发第一笔回调，二者互不调用。本关没有角度、转速、接触或位置成功条件。
- 四个 FallArea 复用 `Assets/Game/Prefabs/LevelItems/FallArea.prefab` 及现有失败事件，不新增全局事件。
- `DLevel2Ctrl` 显式绑定 D034 当前共享 Rig，`entranceDelay=0`、`entranceDuration=0.78`，并复用
  D002 的夹子惯性衰减摆动参数。Rig 最终 Transform 保持用户当前 `position=(-0.1,5.2)`、`scale.x=0.9`；只把
  `entranceStartPoint` 改为本地 `(0,5.277918)`，Scene 轨迹与运行时入场方向一致。
- 共享球 Rigidbody2D 初态为 Kinematic；D034 保留 `mass=5`、`gravityScale=1.5` 覆写。Intro 始终播放
  `clip_close` 并保持夹持；第一笔完成回调才播放 `clip_open`，将球脱离夹子层级并切为 Dynamic。
- `DrawLevelCtrl` 会等待闭合入场与惯性摆动收稳后才由外层进入 Drawing。第一笔完成时，`DLevel2Ctrl`
  开夹放球、`DLevel4Ctrl` 释放双风车、`DrawLevelCountdownCtrl` 启动 7 秒计时，三者各自消费同一首笔事件。

## 4. 数据与系统边界

- 本轮只修改 `Assets/Game/Prefabs/DrawLevel/DLevel_034.prefab` 和本地文档。
- 不修改 DB/存档、Luban 表、RemoteConfig、Draw HUD、成功门、Physics2D Layer Matrix、analytics、广告、
  IAP、SDK 或平台配置。
- 运行时 `tbdrawleveldata.json` 已有 `20034 -> DLevel_034`；当前生成的 `tbdrawlevelsort.json` 未包含
  `20034`，源表排序意图和菜单可达性为 `uncertain`，本轮不改用户已有排序工作簿。
- 风车使用关卡内现有美术节点；没有新增脚本、材质、贴图、运行时 Instantiate 或逐帧控制器。

## 5. 验证

- Prefab 静态结构：173 个唯一 YAML anchor、0 duplicate、0 missing local reference。
- 组件计数：2 个风车 Rigidbody2D、2 个 HingeJoint2D、4 个叶片 BoxCollider2D、4 个共享 FallArea、
  1 个 `DrawLevelCountdownCtrl(cdTime=7,isSuccess=true)`、1 个共享 `DLevel2Ctrl` 及其显式 Rig 内部引用。
- Unity Editor 日志已记录 D034 PrefabImporter 成功导入。
- Unity 自带 Roslyn 使用当前 `Assembly-CSharp.rsp/rsp2` 完整编译通过（退出码 0）。
- PlayMode 的上方闭合入场落点、首笔开夹时机、首次/重开输入门禁、首笔同帧释放/计时、风车受 Actor
  与画线碰撞后的转动手感、四向越界失败、暂停、
  Reset/Replay、7 秒成功和 Android/iOS 横屏仍为 `uncertain`。

## 6. 回滚

- 回滚风车物理时，成组移除两个 Rigidbody2D、两个 HingeJoint2D、四个叶片 BoxCollider2D，并清空
  `DLevel4Ctrl.boxRigidbodies`；不要只移除 Joint 留下自由刚体。
- 回滚边界时成组移除四个 FallArea nested instance 及根 Transform 子引用。
- 回滚本次猫夹入场接线时，仅移除根 `DLevel2Ctrl`、对应 stripped Rig 内部引用，并恢复
  `entranceStartPoint` 覆写；不要改动用户当前 Rig 最终 Transform 或双风车物理。
- 倒计时是修改前既有规则，本轮未调整；不应随风车物理一起删除。
