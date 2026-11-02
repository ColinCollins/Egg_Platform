# 明信片解锁粒子 UI 可复用版

这个目录是给其他 Unity 项目复用的 UI 粒子版本，适合放在 `Screen Space Overlay`、`Screen Space Camera` 或普通 UI Canvas 下。

核心文件：

- `Prefabs/PostcardUnlockUIParticlesReusable.prefab`：UI 粒子预制体。
- `Scripts/ReusableCanvasParticleGraphic.cs`：唯一需要的通用 UI 粒子渲染脚本，不依赖 CardRouge 或明信片项目代码。
- `Particles/`：粒子贴图。
- `Materials/PostcardUnlockUI_Additive.mat`：UI 加法材质。
- `Shaders/PostcardUnlockUIAdditiveParticle.shader`：支持 UI 遮罩/裁剪的加法粒子 Shader。
- `ReusableCanvasParticleGraphic` 内置 Canvas 尺寸适配，默认参考分辨率 `1920×1080`、覆盖倍率 `1.15`。

使用方式：

1. 导出或复制整个 `Assets/PostcardUnlockReusableUI` 文件夹。
2. 在其他项目导入后，把 `Prefabs/PostcardUnlockUIParticlesReusable.prefab` 放到 UI Canvas 下。
3. prefab 默认 `Play On Awake`，并通过每个子粒子的 `Start Delay` 保留原始动画节奏。
4. 如果需要手动重播，可以遍历子物体上的 `ParticleSystem`，先 `Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)`，再 `Play(true)`。

这份 UI 包不包含：

- 明信片图像形变
- 背景变暗/恢复
- 缩放净底
- 明信片解锁控制器

说明：

真正的 `Screen Space Overlay` UI 粒子无法只靠 Unity 原生 `ParticleSystemRenderer` 工作，所以这个包保留了一个独立通用的 UI Graphic 脚本。只要整个文件夹一起导出，就不会出现丢脚本或丢材质的问题。
