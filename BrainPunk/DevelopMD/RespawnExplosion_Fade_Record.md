# RespawnExplosion 渐隐修复记录

## 2026-07-09：最外圈渐隐

### 需求背景

- `RespawnExplosion` 最外层圆环消失时需要渐隐，不能突然结束。
- 可见圆环原本在主圆环粒子上使用加法材质 `ring_ADD.mat`。`UIRespawnExplosion` 内嵌对应粒子系统，但 Canvas 粒子渲染与场景渲染的混合要求不同。

### 影响文件

- `Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/RespawnExplosion.prefab`
- `Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/UIRespawnExplosion.prefab`
- `Assets/Epic Toon FX/Materials/Basics/ring_AB.mat`

### 原有行为基线

- 主圆环粒子的透明度先从不可见变为可见，再在生命周期末端变为不可见。
- 在 UI 特效中，末段过渡实际看起来仍像突然消失。

### 修复后行为

- 场景主圆环保持原有 `0.3s` 生命周期，使用 `ring_AB.mat`，在生命周期前 60% 保持 alpha 1，至 90% 时渐隐到 alpha 0，并保持透明直到生命周期结束。
- `ring_AB.mat` 复用原圆环贴图，并为普通场景粒子渲染器使用 Alpha Blend。
- 外层 `Glow` 粒子保持原有 `0.2s` 生命周期，前 50% 保持透明度，至 90% 时渐隐到 0。
- `Sparks` 粒子系统未修改。
- `UIRespawnExplosion` 根节点的隐藏容器粒子未修改，因为它的 Renderer 已禁用，不是可见圆环。

### 数据、资源与事件流

- 本次仅修改 prefab 与材质的视觉时序。
- 未新增或修改 DB/存档字段、资源路径、配置表、全局事件、打点、广告、内购、RemoteConfig 或平台 SDK 行为。
- 明信片 HUD 到达特效对 `UIRespawnExplosion` 的既有资源引用保持不变。

### 验证记录

- 已静态检查两个 prefab 的差异。
- 用户已目测确认场景 prefab 的渐隐正确。
- 待验证：使用 `UIRespawnExplosion` 的明信片星星到达特效在 Android/iOS 横屏真机上的表现。

### 剩余风险

- 在 Unity 中打开并保存 prefab 时，可能连带重序列化附近字段。
- 渐隐体感仍受粒子生命周期影响，最终节奏需要在 Unity 中目测确认。

### 回滚路径

- 恢复场景主圆环 `ColorModule` 最大渐变中的 alpha 关键帧，并把 Renderer 材质从 `ring_AB.mat` 切回 `ring_ADD.mat`。

## 2026-07-10：UI 黑色方块修复

### 需求背景

- 场景 prefab 显示正确，但 UI 特效中的圆环贴图在发光周围显示成黑色方块。
- 圆环贴图包含为加法混合准备的黑色背景；改用 `ring_AB.mat` 后，Canvas 粒子路径把该背景显示了出来。

### 影响文件

- `Assets/Epic Toon FX/Prefabs/Combat/Explosions (Misc)/UIRespawnExplosion.prefab`

### 修复后行为

- UI 主圆环恢复使用原加法材质 `ring_ADD.mat`，贴图黑色背景不再贡献可见颜色。
- 生命周期颜色渐变在 `0.3s` 生命周期前 60% 保持白色，至 90% 时把 RGB 渐变到黑色，alpha 使用相同渐隐区间。
- 即使 Canvas 粒子路径没有完整复现场景 alpha 混合，RGB 归零也会让加法颜色贡献降至 0。
- 此修正不改动 `RespawnExplosion.prefab`、`Glow`、`Sparks`、生命周期数值或 UI 层级。

### 数据、资源与事件流

- 此修正只调整 UI prefab 中一个既有材质引用和一条粒子颜色渐变。
- 未引入新材质、贴图、资源路径、DB/存档字段、配置、事件、打点、广告、内购、RemoteConfig 或平台集成。

### 验证记录

- 静态 YAML 检查确认 UI 圆环引用 `ring_ADD.mat`，使用三个有效 RGB 关键帧，并在生命周期 90% 时把 RGB 降至 0。
- 静态检查确认场景 prefab 仍引用 `ring_AB.mat`。
- 待验证：在 Unity UI prefab 或 PlayMode 中确认黑色方块消失且圆环平滑渐隐。
- 待验证：明信片星星到达特效的 Android/iOS 横屏真机表现。

### 剩余风险

- 圆环生命周期为 `0.3s`，最终渐隐区间只有 `0.09s`，体感仍需在 Unity 中目测判断。
- 在 Unity 中打开并保存 prefab 时，可能连带重序列化附近粒子字段。

### 回滚路径

- 把 UI 圆环 Renderer 材质恢复为 `ring_AB.mat`，并恢复原来的两个白色 RGB 关键帧；这也会恢复已知的黑色方块问题。
