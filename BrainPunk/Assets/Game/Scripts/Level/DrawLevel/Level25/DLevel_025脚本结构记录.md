# DLevel_025 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 25 关
- 记录日期：2026-08-03
- 最终玩法口径：开场门和石柱保持稳定；玩家完成任意一笔有效画线后立即解锁两者的真实物理，不要求线先碰到机关
- 玩家玩法：在门后绘制斜线，解锁后依靠画线重量和碰撞推倒门柱；脚本不额外施加冲量
- 局部脚本：`DLevel25Ctrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_025.prefab`
- 共享修正：成功门 Trigger 属于禁画区域；`DrawLineFeature` 显式使用 `ContactFilter2D.useTriggers=true`
- 验证状态：C# 全量编译、Prefab YAML/引用和静态行为断言通过；最终 PlayMode 和移动端仍待验收

## 2. 脚本与组件树

```text
DLevel_025
├─ DrawLevelCtrl                         # 既有画线关生命周期、成功演出与结算
├─ DrawLineFeature                       # 有效笔画完成事件；显式检测非 Draw Trigger 障碍
├─ DLevel10Ctrl                          # 既有猫夹球入场、放球与重开完成态
├─ DLevel25Ctrl                          # 门柱开场锁定、首个有效笔画释放、Attempt Reset
├─ DrawLevelCountdownCtrl                # 既有 10 秒倒计时
├─ level10
│  ├─ Alien / DrawGame_AlienClipBallRig # 既有共享装置；玩家球位于 Actor Layer 11
│  └─ DrawGame_SuccessDoor               # OpenDoor Layer 3
│     ├─ Rigidbody2D                     # Prefab/Awake/Reset 为 Kinematic，画线完成后 Dynamic
│     ├─ BoxCollider2D (Trigger)         # 原成功 Trigger；完整范围同时作为禁画障碍
│     └─ PolygonCollider2D (Solid)       # 12 点贴合门框；排除 Actor，接收实体碰撞
└─ shizhu                                # 正式石柱 Sprite
   ├─ Rigidbody2D                        # Prefab/Awake/Reset 为 Kinematic，画线完成后 Dynamic
   └─ BoxCollider2D (Solid)              # 当前石柱实体碰撞
```

## 3. 类与组件职责

| 类/组件 | 负责 | 不负责 |
| --- | --- | --- |
| `DLevel25Ctrl` | 缓存初态；开场保持门柱 Kinematic；任意有效笔画完成后同步切 Dynamic；Reset 归位 | 不等待画线碰撞，不覆盖质量/阻尼/重力，不 AddForce/AddTorque，不判断胜负 |
| `DrawLineFeature` | 生成/清理运行时线；派发有效笔画完成事件；显式把非 Draw Trigger 纳入落笔与续画阻挡 | 不直接控制 D025 门柱的位姿、速度或参数 |
| 门成功 Trigger | 继续调用 `DrawLevelCtrl.OnTriggerSuccess`；作为 OpenDoor Layer 3 障碍阻止其完整范围绘制 | 不承受实体冲量，不阻挡 Actor |
| 门实体 Collider | 接收画线、柱和地面的实体碰撞；仅局部排除 Actor bit 2048 | 不接管成功判断 |
| 门/柱 Rigidbody2D | 解锁后承载真实重力、接触与倾倒 | 画线完成前不参与动态求解 |

## 4. 生命周期与调用顺序

1. `Awake()` 解析门和柱绑定，缓存两体初始局部位置/旋转，并只强制门实体碰撞体排除 Actor。
2. `RestoreInitialState()` 把门柱暂时 `simulated=false`，归位清速后恢复为 `Kinematic + simulated=true` 并同步 Transform。
3. 玩家完成任意一笔有效画线后，`OnDrawStrokeCompleted()` 立即调用 `ReleaseMechanism()`；不检查线的位置或碰撞。
4. 释放时门柱同步切为 Dynamic、清零锁定阶段速度并 WakeUp；之后只由真实 Physics2D 运动。
5. `_isReleased` 保证同一次 Attempt 只释放一次；后续多笔不会重复清速度或重启物理。
6. Reset 恢复初始位姿、Kinematic 和零速度，下次有效画线可再次释放。

## 5. Prefab 与参数

| 宿主 | 字段/组件 | 当前值 | 说明 |
| --- | --- | --- | --- |
| `DLevel25Ctrl` | 门/柱 Rigidbody 与实体 Collider | 门/柱 Rigidbody、门实体显式绑定；柱 Collider 运行时从 `shizhu` 解析 | 门实体不能误绑原成功 Trigger |
| 门 Rigidbody2D | Body / Mass | Kinematic / 100 | 有效画线完成后 Dynamic；Inspector 值不被控制器覆盖 |
| 石柱 Rigidbody2D | Body / Mass | Kinematic / 50 | 有效画线完成后 Dynamic；Inspector 值不被控制器覆盖 |
| 两体 | Damping / Gravity | 门 0.05/0.35/1；柱 0.05/0.3/1 | Rigidbody2D Inspector 是唯一参数来源 |
| 两体 | Interpolate / Detection | Interpolate / Continuous | 解锁后的真实物理参数 |
| 两个实体 Collider | Material | `DrawStrokeStable` | friction 0.8、bounce 0 |
| 门实体多边形 | path / bounds | 12 点；局部 `x[-1.18,1.18]`、`y[-1.37,1.17]` | 贴合门框中下段收窄，替代透明区过大的旧方盒 |
| 石柱实体盒 | size / offset | `(1.0939388,5.4596634)` / `(0,0.002503872)` | 当前正式石柱实体范围 |
| 门成功 Trigger | size / offset | `(2.5446355,3.045598)` / 约 `(0,-0.104367256)` | 完整范围禁画 |
| 门/柱接触 | 门根 y / 柱顶 | 3.374 / 约 1.962336 | 门实体底约 2.004，非穿透缝约 0.041664 |
| 门实体多边形 | excludeLayers | Actor bit 2048 | 仍碰撞 Stuff、Default、Ground |
| 门根 | Layer | OpenDoor 3 | 非 Draw Layer，参与 Trigger 障碍查询 |
| DrawLineFeature | strokeMass | 20，按长度缩放，最低比例 0.25 | 本轮未改；可解性仍需 PlayMode 调试 |

## 6. 共享边界与数据流

- `DrawLineFeature` 只把原有 `_blockingLayerMask` 封装进 `ContactFilter2D` 并固定 `useTriggers=true`；起笔仍为 OverlapCircle，续画仍为 OverlapCircle + CircleCast。
- Draw Layer 14 仍从阻挡 Mask 排除，因此明确设计成可绘制的 Draw Trigger 不会被误拦。
- 未修改共享成功门 Prefab、Physics2D Layer Matrix、猫夹球、DB/Luban、存档、UI、analytics、SDK 或平台配置。
- D025 只消费既有 `IDrawStrokeCompletedHandler` 回调，不再向运行时线添加关卡专用组件。

## 7. 验证与未决项

- Unity 6000.0.59f2 当前响应文件完成全量 Roslyn 编译，结果 `0 error`，只有工程既有 warnings。
- Prefab 129 个 YAML anchor 全部唯一；129 个唯一、254 处本地 fileID 引用均可解析。门实体 fileID 保持不变并由 `Collider2D` 字段继续引用。
- 静态断言确认：两体 Prefab 初态 Kinematic；`OnDrawStrokeCompleted()` 直达唯一 `ReleaseMechanism()`；同次 Attempt 只释放一次；Reset 恢复 Kinematic；旧接触 Relay/距离门禁已移除。
- 静态断言确认：DrawLineFeature 起笔和续画都使用显式 `useTriggers=true` ContactFilter；门根无 Layer 14 override，原成功 Trigger 和 `OnTriggerSuccess` 保持。
- `uncertain`：PlayMode 中“画任意有效线立刻解锁但不凭空散架、门 Trigger 全范围不能落笔/穿线”的实际反馈。
- 静态几何确认：门实体多边形 12 点、顺时针、无自交；原方盒在中下段每侧约 `0.1～0.22` 的透明实体碰撞已被收紧，且与柱顶无初始穿透。
- Editor.log 最近 6000 行未发现 D025、反序列化、无效序列化或编译错误；本次未修改运行时代码。
- `uncertain`：100/50 质量下的可解性、门柱倾倒手感、实体轮廓边缘反馈、Reset/Replay、门倒下后的成功范围，以及 Android/iOS 横屏触控。

## 8. 回滚

- 回滚“画线即解锁”只需移除 `DLevel25Ctrl` 的 Stroke Handler 接口/释放入口；会使机关始终保持 Kinematic，玩法不可用。
- 回滚显式 Trigger 查询会让禁画重新依赖运行时全局 `Physics2D.queriesHitTriggers`，可能恢复门 Trigger 可画问题。
- 仅回滚本次轮廓修正时，将 fileID `900000000000025002` 恢复为旧 `BoxCollider2D`，size `(2.3,2.55)`、offset `(0,-0.104367256)`。
- 完整移除机关需成组移除 DLevel25Ctrl、门实例新增 Rigidbody2D/实体 Collider，并处理石柱物理；共享门、数据和存档无需迁移。
