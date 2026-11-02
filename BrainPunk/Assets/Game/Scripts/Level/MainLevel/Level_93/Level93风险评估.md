# Level093 风险评估

## 原有行为基线

Level093 从 Level073 Prefab 复制，原先只有 073 的蓄力控制、共享角色/关卡控制、镜头与新摆放的关卡物体。本轮目标是移除 073 专用控制，建立 093 本地的平台、齿轮杠杆、成功飞行和尖刺失败玩法，不改变任何已发布关卡或共享系统行为。

## 受影响文件与资源

- `Assets/Game/Prefabs/Level/Level_093.prefab`
- `Assets/Game/ArtSrc/Sprites/Sp_l93_jc_01.png`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93VerticalPlatformDrag.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93PlatformPingPong.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_93/Level93GearLever.cs`
- 复用但未修改：`BaseLevelCtrl`、`ActorCtrl`、`CameraFollowArea`、`TrapEventHandlle`、`OnTrigger2DHandle`、DOTween、`platform_Stone.prefab`、`Knock_Explosion.prefab`

## 风险清单

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 从 073 复制后仍由蓄力脚本接管输入 | 已从 093 根节点移除 `Level73Ctrl`；不修改 073 脚本或共享跳跃 | Prefab GUID 静态检查通过；PlayMode 待验证 |
| 齿轮组件未搬运角色时被禁用，误把 Rigidbody2D 关闭 | 只有 `actorLockedByLever` 为真时才恢复保存过的 `simulated` 状态 | 独立 C# 编译与源码审查通过；Disable 场景待验证 |
| 跳起或刚离开平台仍被识别为成功站位 | 成功判定同时要求 `ActorCtrl.IsGround`、Rigidbody2D 正在模拟且没有向上速度、角色与目标平台水平重叠并位于平台上方 | 静态检查通过；边缘手感待验证 |
| 速度 `30` 的上下平台周期性把角色抛起、落回后再次抛起 | 原因是脚本同时对 Dynamic 角色和实体 Kinematic 平台执行 `MovePosition`，单步约 `0.6` 又大于首次锁存容差 `0.25`。现改为仅由平台 `MovePosition`，角色同步平台速度；首次锁存只接受当前平台实际接触或不超过 `max(0.02, Physics2D.defaultContactOffset * 2)` 的紧贴状态，不做预测扫掠；端点同时清平台与角色载具速度；拖拽和往复速度按 Physics2D 上限约束；自然释放只移除仍存在且同方向的平台 Y 分量，主动跳跃不清跳跃速度 | 独立编译、Unity 正式编译与双脚本源码复审通过；三个高速平台 PlayMode 复测待完成 |
| 拖拽初始位置不在限制区间时启用即跳位 | `clampPositionOnEnable` 默认关闭，只有开始拖拽后才 Clamp | Prefab 参数检查通过；PlayMode 待验证 |
| 多指同时操作多个平台时触点串线 | 每个拖拽组件记录唯一 fingerId，后续只跟踪该触点；触点结束、取消或丢失即停止 | 源码静态检查通过；Android/iOS 待验证 |
| 暂停、过场或重开等待时仍响应关卡输入 | 三个本关组件从 `PlayCtrl` 当前状态初始化，并订阅既有状态事件；非游戏态停止新交互，齿轮飞行 Tween 暂停。普通物理抛射额外缓存速度、关闭模拟并暂停恢复计时，Resume 后一帧重新锁定输入再继续速度 | 独立编译通过；完整状态切换待验证 |
| 成功飞行沿途触发尖刺 | 从随杠杆旋转开始保存并关闭角色 Rigidbody2D 模拟，飞向 EndPoint 期间不会产生尖刺 Trigger | 与 073 的本地成功飞行策略一致；PlayMode 待验证 |
| 杠杆转完后再抛射形成停顿、轨迹过平或初速度偏弱 | 旋转保持 `0.2` 秒 Linear；转到目标角度的 `48%`（约 43 度）即释放，平台继续转完。成功路线保持 `2.45` 秒并将显式抛物线弧高提高到 `20`，附加初始竖直速度约为 `32.7` 且不加快横向；普通路线力度保持 `12`，顺时针切线继续向上抛模板混合 `40%` | 参数与一次性释放守卫静态检查通过；相机上边界、落点和最终手感待 PlayMode 验收 |
| 隐藏旋转平台上的玩家影子后，Actor 丢失落地状态或影子穿透显示到下层地面 | 只把 `platform_Stone (3)` 根 Collider 改为 `WithoutShadowGround` Layer 13；093 Actor 实例局部覆盖 `whatIsGround.m_Bits=8520` 和 Shadow `ignoreLayer.m_Bits=8192`。前者保留落地检测，后者在 Layer 13 命中时提前终止阴影射线；不把 Layer 13 加入 `whatIsStuff`，避免新增侧向推物行为 | Layer、掩码、Collider 根节点和全层碰撞矩阵静态检查通过；实际站立、跳跃与阴影显示待 PlayMode 验证 |
| 普通抛射落地后仍锁住角色 | 由“固定等待后恢复”改为检测离地再落地立即恢复，`0.8` 秒只作为最大兜底；死亡、非 PLAYING 或关卡成功时不会恢复旧控制权 | 源码审查与独立编译通过；PlayMode 待验证 |
| 往复脚本被误挂到已有拖拽平台 | 文档和字段注释明确禁止两个组件同时驱动同一 Transform；当前 3 个拖拽目标与 7 个往复目标为不同对象 | Prefab 目标绑定静态检查通过 |
| 尖刺漏绑、图标与 Trigger 尺寸不一致或重复触发 | 19 个尖刺统一使用 `Sp_l93_jc_01`，Transform scale 为 1，渲染与 Trigger 尺寸均为 `0.49 x 0.48`，并各自进入单个根陷阱处理器 | 19/19 Sprite、Collider 与 `PlayKnock` 静态绑定通过；死亡与重开待验证 |
| 复制残留增加误配置概率 | `Level73Ctrl` 已删除；失活 `L73` 视觉层级仍保留但无驱动脚本 | 无运行时影响的静态结论；是否删除视觉残留待关卡设计确认 |
| 093 首次 Unity 编译失败 | Unity 17:30 的首次编译发现 `AudioManager` 命名空间缺失并已修复；随后同时执行独立编译和 Unity 正式重编 | 17:55 正式 DLL 已重建，修复后无 Level93 编译错误 |

## 性能与移动端边界

- 三个拖拽组件和一个齿轮组件只检查当前帧触点及少量本地 Collider，不做全场景扫描；引用解析完成后复用缓存。
- 七个往复组件在 `FixedUpdate` 各执行一次 `MoveTowards`、`Rigidbody2D.MovePosition` 和少量站立包围盒判断。预计开销较小，但代表性 Android/iOS 设备 60 fps 尚未验证。
- 没有新增运行时资源加载、Instantiate、持续 GC 容器创建、存档、网络或平台 SDK 调用。

## 验证证据

- 独立编译产物：`Temp/CodexLevel93HighSpeedCompile/Level93HighSpeedCompile.dll`，两个平台脚本和 `Level93GearLever.cs` 使用 Unity 6000.0.59f2 Roslyn 与 Bee 引用编译成功，无 C# 错误。
- Unity 正式编译：刷新资源后，`Assembly-CSharp.dll` 已于 2026-07-21 11:24:35 重建；`Editor.log` 中没有 `error CS`，也没有 Level093 脚本的编译错误。
- Prefab YAML：424 个文档、424 个唯一 fileID、0 个重复、0 个缺失本地引用。
- 组件计数：3 个 `Level93VerticalPlatformDrag`、7 个 `Level93PlatformPingPong`、1 个 `Level93GearLever`、10 个 Kinematic Rigidbody2D、0 个 `Level73Ctrl`。
- 阴影配置：齿轮旋转平台根 Layer 为 `13`，其他石台仍为 `6`；本关 Actor 实例恰有一组 `whatIsGround=8520` 与 `ignoreLayer=8192` 覆盖；共享 Actor、ShadowController 和平台 Prefab 未修改。
- 陷阱计数：19 个新 Sprite GUID、19 个 `0.49 x 0.48` Trigger 与 19 个 `TrapEventHandlle.PlayKnock` 持久绑定；旧 Sprite GUID 为 0。
- 文本检查：本关新增 C# 与文档没有行尾空白；脚本 meta 与 Prefab 引用 GUID 可解析。

## 未验证缺口

- 前台编辑器当前打开 `Loading` 场景；本轮最新改动已通过独立 Roslyn 编译和 Unity 正式程序集重建，但还没有执行 093 PlayMode 流程。
- 未执行从选关进入 093、失败、重开、通关、退出、解锁/存档和关键打点回归。
- 未验证成功飞行期间暂停、退出或对象 Disable 时，角色模拟与操作状态在实际关卡流程中完整恢复。
- 待复测三个速度 `30` 的上下平台在上升、下降、端点停留与反向时是否稳定携带角色，并确认主动跳跃、走出边缘、高速拖拽和暂停恢复不会产生额外速度。
- 未验证普通抛射过程中暂停/恢复的速度连续性；力度 `12`、方向混合 `40%` 与成功弧高 `20` 的相机上边界、落点、尖刺节奏和最终手感仍需 PlayMode 验收。
- 未验证旋转平台横向和竖向时的站立、跳跃、齿轮成功判定及 Shadow 完全隐藏效果。
- 未验证 GamePlayPanel 的屏幕 UI 与世界点击区域重叠时是否产生误触。
- 未进行 Android/iOS 横屏触控与性能测试。

## 待确认项

- 七个往复平台已有轨迹和速度；其中高速纵向平台的最终速度需在 PlayMode 根据角色跟随与尖刺节奏验收。
- 三个拖拽平台暂用统一 `-1.5 ~ 1.2` 本地 Y 范围；最终黑色轨道边界和平台中心可达范围需在 Scene Gizmo 中逐个调定。
- 失活的 `L73` 视觉层级是否从 093 Prefab 彻底删除，由关卡设计确认；当前只按要求删除无关脚本。

## 回滚路径

撤回 `Level_093.prefab` 中的本关组件、10 个本地 Kinematic Rigidbody2D、Ground 层局部覆盖、尖刺 Sprite/Collider 与事件绑定，并删除 `Level_93` 新增脚本与文档即可。共享 `ActorCtrl`、`BaseLevelCtrl`、GamePlayPanel、尖刺脚本、平台 Prefab、加载流程和其他关卡均未修改，不需要联动回滚。

## 2026-07-22 音效风险补充

### 原有基线与受影响项

原有齿轮旋转静音，两条抛射路径都播放通用 `jump`。本次影响 `Level93GearLever.cs`、`Assets/Game/ArtSrc/Audio/Sfx/l93` 与 SoundsGood Collection/生成表，不改变旋转和飞行运动学、角色控制权、失败/成功、存档或其他关卡。

### 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 新抛射声与旧 `jump` 同时播放 | 两处分支均直接替换旧调用，没有追加双播 | 静态通过 |
| 角色未站上平台时旋转无齿轮声 | 齿轮声绑定合法旋转本身，与角色站位无关 | 静态通过 |
| 失败建立成功飞行时提前播抛射声 | 成功分支只在引用有效且 Tween 已创建后播放；失败回退由普通抛射播放一次 | 静态通过 |
| 1.05 秒声音长于 0.2 秒旋转并可产生尾音 | 保留完整用户素材，不在共享系统增加截断；连续点击听感待实测 | PlayMode 待验收 |
| 暂停或退出时一次性尾音继续 | 本次不使用全局停止，避免误停其他声音；是否需要关卡定向停止待体验确认 | PlayMode 待验收 |

### 验证缺口与回滚

格式、源/目标哈希、tag-GUID 映射、调用分支、独立 C# 编译及 Unity 正式导入/程序集重建已通过，最近日志相关错误为 0。Unity PlayMode 的旋转同步、两条抛射、连续点击、暂停/重开和移动端仍待验证。回滚只恢复 Level093 本地声音调用并删除 L93 新音频/登记，不修改共享 `AudioManager`。
