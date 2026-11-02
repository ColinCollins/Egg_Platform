# DLevel_048 脚本结构记录

## 需求与参考基线

2026-09-17。用户要求中心杆围绕指定中心响应碰撞，阻力可调，旋转后可以回弹；允许参考D030或D091。

- D030源码及本地记录：Dynamic刚体、固定质心、Freeze Position X/Y、开放旋转；主动扭矩由本关控制器负责。采用该固定支点与局部Handler生命周期基线。
- D091实际没有DLevel91Ctrl，复用D37首笔释放，另有DLevel91PlatformAnchorStabilizer补正已有铰链的平移误差。该方案需要铰链、静态连接体与显示位置补正，本关不引入这一补正职责。
- D048原始即时备份中，杆上确实已有启用的HingeJoint2D，连接静态齿轮。初次检查遗漏了铰链，已重新核对并修正方案：保留原组件和绑定，在本关Prefab停用铰链，采用固定质心的单一支点控制。禁止同时重新启用铰链与本脚本的位置约束。
- 没有匹配的既有Level48模块记录，本次创建本地记录。引用的feature plan缺失，CodeGraph CLI不可用；以现有代码、关卡文档与Prefab为依据。

## 脚本树、职责与继承

```text
DLevel_048
├─ DrawLevelCtrl                  结果、暂停、尝试重置及局部Handler分发
├─ DrawLineFeature                现有画线与碰撞生成
├─ DrawLevelCountdownCtrl         保留15秒失败倒计时
└─ DLevel48Ctrl                   新增，仅负责本关杆的受力旋转、阻力、回弹及恢复
   └─ GameObject/Rigidbody2D     明确绑定的杆刚体
      ├─ BoxCollider2D           保留Offset(0.91,0)、Size(5.4,0.59)
      └─ HingeJoint2D            保留原连接信息，Prefab停用
```

`DLevel48Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, Bear.Logger.IDebuger`；不新增共享接口、事件、UI或平台逻辑。齿轮原有FollowTargetRotate继续跟随杆角度，本脚本不承担齿轮显示职责。

## 显式序列化绑定与调参

新增根组件fileID `900000000000000006`，脚本GUID `8c541df938344afeab4ab1459ba0221b`。

| 字段 | 当前绑定或默认值 | 用途与生效边界 |
| --- | --- | --- |
| platformRigidbody | 杆RB `8050210209603890716` | 必须显式绑定；无层级扫描兜底 |
| drawLevelCtrl | 根主控 `900000000000000001` | 暂停、结果门禁、Resolved订阅 |
| localRotationCenter | (0,0) | 杆局部支点；启用或重置生效 |
| platformMass | 10 | 碰撞质量；启用或重置生效 |
| angularDamping | 1.5 | 旋转阻力；下一有效FixedUpdate生效 |
| springStrength | 80 | 每弧度偏转的回正扭矩；0关闭自动回弹 |

支点配置写入Rigidbody2D.centerOfMass；位置约束固定世界质心。当前杆世界原点约为(0,0.33)，齿轮原点约为(0,0.34000015)，原布局的0.01差距保留。当前支点使用杆原点；需要精确改支点时，在Inspector设置杆局部坐标并重置。当前杆旋转为-90度，局部坐标不能直接当世界坐标填。

非零支点时，Transform原点绕支点移动是预期行为，不能用Transform.position不变作为固定支点的验收标准。若同时需要原点不动，支点应为(0,0)。质量、阻力及回弹强度共同决定手感，当前默认值不是已试玩参数。

## 调用与生命周期

1. `OnEnable`：仅PlayMode执行；检查两个显式引用，缺失时报Bear错误并停用；缓存初始世界位置与角度；配置Dynamic、手动质量、重力0、局部质心、固定平移、开放旋转、插值及Continuous碰撞；订阅Resolved。
2. 真实碰撞由Physics2D改变角速度；`OnDrawStrokeCompleted`不注入速度，本脚本不承担首笔释放；原D2在收尾外部编辑中被移除，当前首笔释放链待确认。
3. `FixedUpdate`：跳过暂停、结果锁和已结算；更新角阻尼；以最短角差乘Deg2Rad和springStrength施加回正扭矩，目标为开场角度，不是固定世界0度。
4. `OnDrawAttemptReset`：通过既有局部Handler接入；临时停用模拟，显式恢复Transform世界姿态、清零速度，再恢复正常物理配置并SyncTransforms。隔离测试发现仅在停用模拟时写刚体position/rotation，会被重新模拟时的旧Transform覆盖，因此本关采用同步Transform的恢复方式；未修改D030。
5. `Resolved`：清零速度、FreezeAll、Sleep；`OnDisable`在运行时解除订阅。重新启用重新配置，重置恢复旋转能力。

共享调用证据：DrawLevelCtrl.cs的`CacheLocalHandlers`（879起）收集现有接口，重置通知（900起）调用`OnDrawAttemptReset`，`IsOutcomeLocked`（105起）提供结果等待门禁；DrawLevelRuntimeTypes.cs（309起）定义接口。本轮不修改共享扫描、重置顺序或结果链。

## 数据、资源、UI、事件与边界

- 只增加Level48脚本和目标Prefab绑定；本轮未写画线质量500、墨水、UFO/球/夹子、门、地图、提示、15秒失败和原通关方式。收尾出现外部Prefab编辑，D2及其若干源组件包装被移除，不声称当前首笔链仍完整。
- 使用已有Resolved及局部Handler，不新增PlayEvents。配置表、云控、存档、打点、资源路径及HUD均未改；不能据静态复用宣称整关通关、配置注册或转场已实测。
- 不修改D030/D091/D2、共享Actor/面板、Physics2D全局参数、Packages、GuruSDK或GF。验证脚本与中间产物只在外部Temp隔离项目，不加入游戏代码。

## 验证、复盘与回滚

- 当前真实程序集引用的独立C#编译：0错误、2项Inspector引用字段未由代码赋值的CS0649警告。
- Unity6000.0.59f2隔离PlayMode使用逐字复制的最终生产脚本，61项检查通过。覆盖零/非零支点、真实两侧碰撞（质量1/100/500）、弹性回正、阻力对照、0回弹、两种支点各3次重置、结算冻结、暂停/结果门禁、实时阻力和启停订阅。
- 外部编辑前Prefab112对象/222本地引用闭合，107个原块保持；收尾最新Prefab105对象/205引用仍闭合，新增杆引用有效，与备份相比100个原块保持，另有7个源块移除（含D2及其引用的部分嵌套组件包装）。本轮不回写这些外部变化及UFO骨骼位姿。
- 结构复盘：固定支点的受力旋转与铰链补正是不同职责；D048只复用D030模式，并以局部角差扭矩替换偏重扭矩。不为两关共性升级共享抽象，不迁移旧关。
- `uncertain`：完整主工程关卡、真实画线/Spine碰撞、门演出、重开/Replay/资源重建与Android/iOS未实测。详细数值、验证边界和风险见同目录功能与风险评估。
- 回滚：移除D048根新增组件引用及块；按即时备份撤回杆质量、阻尼、插值、位置约束，重新启用原铰链；删除本关新增脚本/记录及meta。按块撤回，不能用旧整份Prefab覆盖后续地图/UFO编辑；不用改Packages或共享关卡。

## 收尾并行变化的证据边界

最后一次Prefab检查SHA256为BAD80431072618F7E1DFEF4888371B16CC353E1689A2E98B37B43BB0B92DBAFE。杆配置、停用铰链与控制器绑定仍在；根D2及6个相关源块在本轮写入之外被移除。本轮没有恢复这些编辑。D030/D091 Prefab也出现字节变化，脚本哈希保持；因此不以早先文件哈希保证当前其它关卡未变。上述61项物理检查只覆盖D48杆控制器与隔离夹具，当前夹球/首笔链及整体表现仍uncertain。

## 2026-09-17：局部旋转中心 Gizmos

- 用户要求将 `localRotationCenter` 显示出来。原脚本没有支点 Gizmos；本次仅在 `DLevel48Ctrl.cs` 新增 `Editor Visualization` 分区和 `UNITY_EDITOR` 内的 `OnDrawGizmos()`，不改原注释、物理与结果/重置方法或 Prefab。
- 标记位置为显式绑定的 `platformRigidbody.transform.TransformPoint(localRotationCenter)`，使用杆的局部坐标而非控制器根坐标；显示黄色圆点、圆环、十字及 `Local Rotation Center` 标签。Scene/Prefab 视图打开 Gizmos 即可预览，无须进入 PlayMode。
- Gizmos 仅读取序列化引用和数值，不访问 DB/Manager、不配置刚体、不创建实例。运行中修改数值，预览即时反映配置；实际物理质心仍按原逻辑在启用或尝试重置时更新。此标记表示配置中心，不宣称是尚未应用参数时的物理支点。
- 使用当前实际 DLL 引用的独立 Roslyn 编译，Editor/Player 两分支均退出0、0错误/2个既有 CS0649 警告；Player 检查未定义 UNITY_EDITOR，且移除 UnityEditor 引用。产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel48-gizmos-20260917/{Editor,Player}.rsp/.log`。未在主工程 Scene 视图目视验收，标签遮挡、折叠组件的 Gizmos 显示及非均匀缩放仍待验证；旧61项物理检查不是本次绘制验证。
- 复盘：纯本关编辑器可视化，不增加运行时组件、共享接口或全局事件。回滚只删除新增 Gizmos 分区与本次文档主题，保留已有旋转与回弹实现及用户 Prefab 编辑。
