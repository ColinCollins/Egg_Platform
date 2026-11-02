# UIStunStarExplosion 缩小与渐隐修复记录

## 2026-07-10：UI 星星逐粒子缩小与渐隐

### 需求背景

- `UIStunStarExplosion` 中的黄色星星保持原尺寸和不透明状态，最后直接消失，与场景版 `StunStarExplosion` 的末段缩小、渐隐表现不一致。
- 第一次尝试只给 UI 嵌套预制体覆盖尺寸曲线和透明度关键帧时间；用户在 Unity 中复测后确认仍然没有缩小和渐隐，因此该方案不能作为有效修复。

### 影响文件与系统

- `Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/UIStunStarExplosion.prefab`
- `Assets/Game/UI/Global/Scripts/UIStunStarParticleFade.cs`
- `Assets/Game/UI/Global/Scripts/UIStunStarParticleFade.cs.meta`
- 使用方包括 `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab` 和 `Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab`。

### 原有行为基线

- 场景版主星星粒子在约 77% 生命周期后缩小，并在 80% 生命周期后渐隐。
- UI 版通过 Coffee `UIParticle` 烘焙嵌套场景粒子；用户实测表明，单纯调整 Size over Lifetime 和 Color over Lifetime 的 UI 覆盖没有产生可见变化。
- `star_AB.mat` 使用 Alpha Blend，可读取粒子顶点透明度；材质本身不能解释尺寸完全不变。

### 修复后行为

- 移除之前无效的两个嵌套曲线时间覆盖。
- `UIStunStarParticleFade` 只绑定 UI 版主星星粒子，保存每颗新粒子的初始尺寸和颜色。
- 每颗星星在自身生命周期前 50% 保持初始状态，后 50% 使用平滑曲线把 `startSize3D` 和顶点 alpha 同步降到 0。
- 组件通过 `Canvas.preWillRenderCanvases` 在 Coffee 烘焙 UI mesh 前回写粒子，并使用 `ExecuteAlways` 让单独打开 `UIStunStarExplosion` Prefab 时也能预览该表现；该编辑器路径只读取和回写临时粒子状态，不访问 DB、存档、服务或资源加载。
- 粒子系统被清空、停止、禁用或重新播放时会重置缓存，避免重复播放沿用上一次的缩放值或透明度。
- 场景版 `StunStarExplosion.prefab`、`StarBurstDebris`、`Glow`、材质、寿命、速度和发射参数均不修改。

### 数据、资源与事件流

- 现有 prefab GUID、使用方引用和明信片解锁播放入口保持不变。
- 不新增资源加载、全局事件、DB/存档、配置表、打点、广告、内购、RemoteConfig 或平台 SDK 调用。
- 每次 Canvas 渲染前仅遍历当前主星星粒子；使用复用数组和小型字典保存基线，不产生持续的逐帧托管分配。

### 验证记录

- 静态确认脚本不包含 `OnValidate`、`Reset`、`ExecuteInEditMode`、DB、存档或全局事件路径；`ExecuteAlways` 仅用于满足 Prefab Mode 特效预览，编辑器回调不访问任何业务管理器。
- 静态确认 UI prefab 组件绑定到嵌套主粒子系统，场景 prefab 无差异。
- Unity Editor 诊断确认 `PostcardPopup.prefab` 中两个 `UIStunStarExplosion` 实例都继承到了该组件。
- Unity Editor 诊断确认 Coffee UI mesh 会读取逐粒子尺寸和顶点 alpha：强制修改后，测试星星的 mesh 尺寸由 `113.24` 降至 `5.66`，顶点 alpha 由 `255` 降至 `0`，因此不是材质不读取粒子数据的问题。
- Unity Editor 诊断确认 75% 生命周期时，兜底逻辑把测试粒子的 `startSize` 改为 `0.5`、`startColor.a` 改为 `128`，Coffee 烘焙 mesh 同步缩小并写入 alpha `128`。
- `git diff --check` 已通过；仅有工作区 prefab 行尾将由 Git 转换为 CRLF 的提示，没有空白错误。
- Unity 6000.0.59f2 强制编译通过：`Tundra build success`，程序集重新加载成功；现有 iOS/Firebase 引用和空 asmdef 警告与本修改无关。
- 待验证：明信片解锁特效和 HUD 星星到达特效中，星星后半程是否同时缩小并渐隐。
- 待验证：Android/iOS 横屏真机表现和移动端帧率。

### 剩余风险

- Prefab Mode 与 PlayMode 的最终视觉仍需在 Unity 中复测；数值诊断已确认组件数据可进入 Coffee 烘焙 mesh。
- 50% 过渡起点是当前针对 UI 可读性选择的值，若节奏过慢或过快，需要继续做视觉微调。

### 回滚路径

- 从 `UIStunStarExplosion.prefab` 移除 `UIStunStarParticleFade` 组件，并删除对应脚本与 meta，即恢复为只依赖嵌套场景粒子模块的 UI 表现。

## 2026-07-10：GamePlayPanel 场景提前清理修复

### 需求背景

- 用户确认单独播放 `UIStunStarExplosion` Prefab 时已经能缩小和渐隐，但在关卡场景的 HUD 到达特效中仍会直接消失。
- 静态调用链确认默认 `GamePlayPanel` 会实例化该 UI 特效，并在 `postcardStarArriveEffectDuration` 到时销毁整个实例。

### 原有行为基线

- 默认 `GamePlayPanel.prefab` 的到达特效持续时间为 `0.45s`。
- 主星星粒子生命周期范围为 `0.6-1.0s`，逐粒子缩小和渐隐从自身生命周期 50% 开始；最长寿命粒子在 `0.5s` 后才开始过渡，因此会在进入过渡前被场景逻辑销毁。

### 修复后行为

- 默认 `GamePlayPanel.prefab` 的 `postcardStarArriveEffectDuration` 调整为 `1.1s`，覆盖主星星最长 `1.0s` 生命周期并留出一帧以上的清理余量。
- `UIStunStarExplosion` 的材质、发射、寿命和缩小渐隐曲线不再修改。
- `GamePlayPanel_025_70.prefab` 使用另一套到达特效，继续保持 `0.45s`，避免扩大视觉改动范围。

### 数据、资源与事件流

- 仅调整默认 HUD 到达表现的序列化持续时间，不改变奖励、事件、DB/存档、资源引用、配置表、打点、广告、内购、RemoteConfig 或平台 SDK。

### 验证记录

- 静态确认默认面板的特效列表包含 `UIStunStarExplosion`，场景播放代码在持续时间结束时销毁实例。
- 静态确认主星星生命周期最大值为 `1.0s`，原 `0.45s` 清理时间早于其 `0.5s` 过渡起点。
- 待验证：关卡 PlayMode 中 HUD 星星到达后，黄色星星是否完整缩小并渐隐。

### 回滚路径

- 把默认 `GamePlayPanel.prefab` 的 `postcardStarArriveEffectDuration` 从 `1.1` 恢复为 `0.45`，即可恢复原有提前清理行为。

## 2026-08-26：star_AB 还原至今天之前版本

### 需求、基线与影响范围

- 用户要求把共享材质 `Assets/Epic Toon FX/Materials/Misc/Stars/star_AB.mat` 还原到昨天状态。
- Git 历史中 2026-08-25 没有该文件提交；目标按“今天之前最后可确认状态”确定为提交
  `8fa45f58fa179bc4ab4c33e75a1c4bea73099bee`（2026-08-04）的文件版本。
- 执行前工作区已有未提交材质改动，Shader 为内置 `fileID 200`；当前 `HEAD` 则是 2026-08-26 提交的
  URP `Particles/Unlit` 版本。本次只还原 `star_AB.mat`，不修改 prefab、贴图、meta、配置、存档、事件或脚本。
- 静态直接引用该共享材质的资源包括 `StunStarExplosion.prefab`、`StunnedCirclingStarsSimple.prefab` 和
  `StunCoinExplosion.prefab`；嵌套这些资源的 UI/关卡表现也可能继承材质变化。

### 还原后配置与资源流

- Shader 恢复为 Unity 内置 `fileID 203` 粒子 Shader，`_Mode=2`、`SrcBlend=5`、`DstBlend=10`、
  `ZWrite=0`、`Lighting/Emission/SoftParticles/Distortion=0`。
- `_MainTex` 恢复为 `Assets/Game/UI/Postcard/Sprites/UI_common_icon_xing2_01.png`，颜色为白色，
  `_TintColor=(0.5,0.5,0.5,0.5)`，`_ColorMode=4`。
- 材质 GUID 保持 `d05bd487e32791148ab238ebabb0154b`，因此引用关系和资源地址不变；仅共享材质序列化配置回退。

### 验证、风险与回滚

- `git hash-object` 验证当前文件 Blob 为 `d93e239aec668fe637a728251049b3c46bd3f998`，与目标提交 Blob 完全一致。
- `git diff --exit-code 8fa45f58... -- star_AB.mat` 返回 0；`git diff --check` 通过，仅有仓库行尾转换提示。
- `uncertain`：尚未在 Unity PlayMode 检查场景/UI 星星外观、透明混合、URP 兼容性、Android/iOS 横屏表现和构建结果。
- 该材质是共享资源，回退会同时影响所有直接或嵌套使用方；若需要撤销本次回退，可恢复当前 `HEAD`
  `cc1062e0469f4e2b25fb36711a5cc507b6e27c6e` 的材质版本，或由用户指定其它历史提交。
