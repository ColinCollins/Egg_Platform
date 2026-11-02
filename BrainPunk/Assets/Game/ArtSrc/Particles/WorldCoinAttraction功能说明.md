# WorldCoinAttraction 功能说明

## 需求与参考基线

- 需求：参考外部工程
  `Assets/AssetKits/ParticleImage/Demo/Prefabs/CoinAttraction.prefab`，制作可用于 World Scene 的
  `ParticleSystem` 2D Sprite 粒子移动路径版本。
- 用户于 2026-08-17 明确收窄范围：当前只实现“散开、旋转、末段吸附”的移动路径，不实现金币图集序列帧效果。
- 外部参考 prefab 的静态参数证据包括：`0.3s` 发射时长、`75/s` 发射率、`1.5~2s` 生命周期、圆形发射、
  约从单颗粒子生命周期 `69%` 开始向目标收束、后半段旋转，以及 `0.74` Trail 比例和 `0.2s` Trail 生命周期。
- 修改前，当前工程没有可直接复用的 ParticleSystem 目标吸附组件或对应 World Scene prefab。

## 影响文件与资源

- `Assets/Game/Scripts/Common/Particle/WorldSpriteParticleAttractor.cs`
- `Assets/Game/ArtSrc/Particles/Prefabs/WorldCoinAttraction.prefab`
- `Assets/Game/ArtSrc/Particles/Material/WorldCoinAttractionParticle.mat`
- `Assets/Game/ArtSrc/Particles/Material/WorldCoinAttractionTrail.mat`
- 复用既有单张纹理：
  `Assets/Game/UI/Postcard/PostcardUnlockReusableUI/Particles/Particle_SoftGlow.png`

不修改参考工程、既有关卡 prefab、共享 `ActorCtrl`、`GamePlayPanel`、存档、配置、资源加载、全局事件、
analytics、广告、支付或平台 SDK。

## 实现行为

### ParticleSystem 职责

- 使用 `World` Simulation Space、圆形发射区、`0.3s` 非循环系统和最多 `32` 颗粒子。
- 用户调整后的当前发射方式为：`Rate over Time = 0`，在 `Time = 0` 一次 Burst `5` 颗；生命周期为
  `1.5~2s`，初速度为 `2.5~7` 世界单位/秒。后续节奏修改必须保留这组 Burst 表现，除非用户再次明确调整。
- 使用单张 SoftGlow 纹理和短 Trail 表现路径；`Texture Sheet Animation` 保持关闭，不依赖金币图集。
- Size over Lifetime 在抵达前短暂放大，便于观察汇聚点；该表现不承担移动逻辑。

### 路径控制职责

- `WorldSpriteParticleAttractor` 在 PlayMode 的 `LateUpdate` 或显式 Editor 预览回调中复用粒子数组，通过
  `GetParticles/SetParticles` 只改写存活粒子位置。
- 每颗新粒子以 `randomSeed` 保存自由轨迹位置、初速度、出生发射中心和上一帧剩余生命周期。
- 前段按 `freeFlightSpeedCurve` 散开并减速，后半段按 `vortexCurve` 围绕出生发射中心旋转；
  `attractionCurve` 约在生命周期 `69%` 后把自由轨迹插值到当前目标位置，并在死亡前汇聚。
- Inspector 暴露 `中间停顿时间（秒）`，默认 `0.22s`：自由飞行曲线在生命周期约 `36%` 结束后，粒子保持
  完全静止，停顿结束再进入原旋涡/吸附曲线。设为 `0` 可取消完全停顿；极端大值会限制在生命周期 `85%` 前结束，
  为末段吸附保留时间。
- Inspector 暴露 `最短飞行时间（秒）` 与 `最长飞行时间（秒）`，默认保持当前 ParticleSystem 的 `1.5~2s`。
  点击 Odin `Play` 或 PlayMode 自动播放时，这两个值会同步到 `Start Lifetime`；设置成相同值可让所有粒子使用固定
  飞行时间。若下限大于上限，播放时会自动按数值大小排序，不改写 Inspector 中的原值。
- 默认保持粒子原 Z，只在 XY 平面移动，适合正交 2D World Scene；目标播放期间移动时会实时跟随。
- 目标在播放中失效时清空本轮粒子，避免速度已被脚本接管的粒子停留在场景中。

## 调用方式

1. 把 `WorldCoinAttraction.prefab` 放入 World Scene。
2. 将 prefab 内 `AttractionTarget` 移到目标位置，或在 Inspector 将 `attractionTarget` 改绑到外部 Transform。
3. prefab 默认在 PlayMode 启用时播放一次；对象池或业务代码可关闭 `playOnEnable`，再调用：
   - `SetTarget(Transform)`：只替换目标；
   - `Play()`：使用当前目标清残留并播放；
   - `Play(Transform)`：换目标并播放；
   - `Stop(bool clear)`：停止发射，可选是否清空存活粒子。
4. 调试路径时，选中 `WorldSpriteParticleAttractor` 并点击组件底部的 Odin `Play` 按钮：
   - PlayMode：复用正常的 `Play()`，使用当前 `attractionTarget`，先清理上一轮再播放；
   - Editor 非运行态：从时间 `0` 重置当前 ParticleSystem，通过本地 Editor 更新回调逐步执行 `Simulate` 和同一套
     路径计算；再次点击会清理并从头预览。按钮面向 Scene/Prefab Stage 中的有效组件，直接选中 Project 中的持久化
     prefab 资源时不启动预览。
5. 调整整体飞行节奏时修改组件上的 `最短飞行时间（秒）` / `最长飞行时间（秒）`，再点击 `Play` 预览；例如都设为
   `1.0` 表示所有粒子约用 `1s` 完成整段路径。该时间包含散开、停顿、旋涡和最终吸附。

## 性能与生命周期边界

- prefab 的粒子上限为 `32`，每帧只遍历当前存活粒子；粒子缓冲区和状态字典会复用，不产生逐帧托管数组分配。
- `OnEnable` 和 `LateUpdate` 仍以 `Application.isPlaying` 为运行边界；Editor 预览仅能由 Odin `Play` 按钮显式触发。
- Editor 预览只读取当前组件的序列化字段和既有 ParticleSystem，使用 `EditorApplication.update`、`ParticleSystem.Simulate`
  与 `SceneView.RepaintAll` 驱动；预览结束、组件禁用或切入 PlayMode 时解除回调。
- Editor 预览不访问 `DB`、Save、RemoteConfig、运行时单例或资源加载链路，不使用 `new GameObject`、`Instantiate`
  或 `ScriptableObject.CreateInstance`。
- 不使用 `OnValidate`、`Reset`、`ExecuteAlways`、协程、Tween、物理 Collider 或全局事件。
- 目标、曲线、旋转角速度和 ParticleSystem 参数均可在 Inspector 局部调整。
- `中间停顿时间（秒）` 是 prefab 局部序列化参数，Editor Odin 预览与 PlayMode 使用同一数值，不新增配置或全局状态。
- 两个飞行时间字段也是 prefab 局部序列化参数，只在显式 `Play` 或 PlayMode 自动播放入口同步到当前 ParticleSystem，
  不会修改 Burst 数量、发射速度、尺寸、路径曲线、材质或全局配置。

## 2026-08-18：ILevel_036 Portal 显式触发接入

- `ILevel_036.prefab` 对关卡内 WorldCoinAttraction 实例局部覆写 `playOnEnable=0`，避免关卡加载时在 Portal 入口
  distortion 之前自动播放；共享 prefab 继续保持默认自动播放，避免改变其它使用方。
- ILevel_036 的三个 `IWannerGame_Portal` 共用同一个 `WorldSpriteParticleAttractor` 实例；默认在入口 distortion 完成前
  `0.3s`，Portal 先把特效根节点的世界坐标改为当前 Actor 世界坐标，再调用 `Play(endPoint)`，由该调用把吸附目标切换到
  当前 Portal 的 `endPoint` 并清空上一轮后重新播放。
- 特效播放不参与 Portal 的传送完成门禁：播放触发后，Portal 继续把 IWanner Scale 设为 0，并按既有
  `teleportDelay`、出口 distortion 和出口 Scale 流程推进。
- 当前只在 `ILevel_036.prefab` 建立引用；Portal 字段未绑定时直接跳过，不影响其它关卡。未新增全局事件、配置、存档、
  资源加载、实例化、UI、打点或平台 SDK。
- 完整 `Assembly-CSharp` 构建结果为 `0 error / 109 warning`，warning 为工程既有项；prefab GUID、脚本 GUID、三个 Portal
  override 与同一个 stripped 组件引用已做静态核对。
- `uncertain`：尚未在 Unity PlayMode 目测 distortion 完成前 `0.3s` 的触发帧、Actor Scale 归零帧、五颗粒子的发射位置与三个不同
  `endPoint` 的吸附轨迹，也未验证快速连续进入不同 Portal、Reset/Replay 及 Android/iOS 横屏表现。
- 回滚本次接入时，移除 ILevel_036 实例的 `playOnEnable=0` 局部 override，以及三个 Portal 的
  `worldCoinAttraction` override；共享 prefab、WorldSpriteParticleAttractor 通用播放 API 和粒子参数无需回滚。

## 2026-08-18：Portal 入口粒子提前 0.3 秒

- 修改前 WorldCoinAttraction 在入口 distortion 收到 `Complete` 后触发；修改后 Portal 新增
  `worldCoinAttractionLeadTime=0.3s`，按当前 TrackEntry 的剩余播放时间调度到完成点前触发。
- 调度使用 `TrackComplete - TrackTime` 并按 TrackEntry `TimeScale` 换算，尚未触发时若 distortion 中断、Actor 销毁或
  Portal 取消，会取消等待；若动画无法启动或调度未能提前执行，完整入口的完成分支仍提供一次性兜底触发。
- 粒子触发时 Actor 仍处于入口 distortion；动画完整完成后 Portal 才把 Actor Scale 设为 0 并进入原传送延迟。
  出口反向 distortion、WorldCoin 的 Actor 世界坐标/endPoint 目标和粒子自身生命周期均不变。
- 完整 `Assembly-CSharp` 构建为 `0 error / 109 warning`；`uncertain`：实际提前量、动画全局 TimeScale 变化、最后
  `0.3s` 内中断后已发出粒子的残留，以及 Android/iOS 横屏视觉尚未 PlayMode/真机验证。
- 回滚时删除 Portal 的 `worldCoinAttractionLeadTime`、预完成调度和一次性回调，把 `PlayWorldCoinAttraction()` 恢复到
  入口 distortion 完成检查之后；WorldCoinAttraction prefab、ILevel_036 绑定和出口方向无需回滚。

## 验证记录

- Unity 6000.0.59f2 已成功导入并生成 prefab 与两份材质，Editor 日志记录 prefab 导入成功。
- Editor 预览与中间停顿控制实现后执行
  `dotnet build Assembly-CSharp.csproj -t:Rebuild --no-restore -p:BuildProjectReferences=false ...`：
  `0 error / 109 warning`；当前 warning 没有指向 `WorldSpriteParticleAttractor.cs`。
- 静态确认 prefab 的脚本、ParticleSystem、目标 Transform 和两份材质引用存在；Texture Sheet/UV Module 为关闭，
  Trail Module 为开启。
- 静态确认粒子材质只引用既有 SoftGlow 单张纹理，不引用外部 Demo 图集。
- 2026-08-17：Odin `Play` 按钮支持 Editor 非运行态显式预览；编辑器分支通过 `ParticleSystem.Simulate` 推进当前系统，
  路径仍复用 `UpdateParticlePositions()`，没有复制第二套吸附算法。
- 2026-08-17：复核用户最新 prefab，确认开场已改为 `Time 0 / Count 5` Burst 且持续发射率为 `0`；保留该表现，
  仅新增默认 `0.22s` 的 `middlePauseDuration` 控制完整静止段。
- 2026-08-17：新增默认 `1.5~2s` 的最短/最长飞行时间字段，运行态与 Editor Odin 预览都从同一个 `Play` 边界写入
  `ParticleSystem.MainModule.startLifetime`；静态确认 prefab 的 Burst、初速度、尺寸、曲线和材质数据未随之改写。
- 2026-08-17：飞行时间字段接入后执行 `Assembly-CSharp.csproj` 定向 Rebuild（关闭 warning 输出），结果为
  `0 error / 0 warning`；并静态扫描确认脚本未新增 `OnValidate`、`ExecuteAlways`、DB/Save/RemoteConfig、实例创建或
  运行时单例访问。
- `uncertain`：尚未在 PlayMode 目测路径曲线、移动目标跟随、重复播放、暂停/恢复和对象池复用；
  Editor 按钮的实际 SceneView 播放、脚本重载/切换 PlayMode 清理，以及 Android/iOS 横屏的排序、Trail 折线和
  32 粒子上限下的性能也未做人工目测或设备验证。

## 剩余风险与回滚

- 路径手感受目标距离、初速度和摄像机世界比例影响，接入具体场景后可能需要局部调整 prefab 参数；
  不应为单一场景手感直接修改其它既有粒子或关卡。
- Trail 会记录逐帧写回后的位置；低帧率下可能比高帧率更容易看到折线，需在代表性移动设备目测。
- `randomSeed` 在单轮最多 32 颗的范围内用作状态键；若未来显著提高粒子数或改为长时间循环发射，应重新评估键冲突与状态清理。
- 只回滚本次 Editor 预览时，移除 `Editor Preview` 条件编译区，并恢复 Odin 按钮的 `DisableInEditorMode`；
  PlayMode 的既有 `Play()`、目标吸附和 prefab 参数无需回退。
- 只回滚停顿控制时，移除 `middlePauseDuration`、`GetMotionCurveAge()` 及对应 prefab 字段，并恢复旋涡和吸附曲线
  直接使用 `normalizedAge`；用户当前的 Burst、速度、尺寸和材质无需回退。
- 只回滚飞行时间控制时，移除 `minimumFlightDuration`、`maximumFlightDuration`、`ApplyFlightDuration()` 及两个
  `Play` 调用点，并删除 prefab 对应字段；ParticleSystem 当前原始 `1.5~2s` Start Lifetime 仍可继续使用。
- 回滚时删除上述脚本、prefab、两份材质及其 `.meta`，并移除
  `DevelopMD/已有功能设计与使用记录.md` 中对应日期记录；没有既有功能文件需要恢复。
