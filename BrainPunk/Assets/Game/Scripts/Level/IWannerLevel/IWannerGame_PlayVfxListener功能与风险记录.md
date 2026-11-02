# IWannerGame_PlayVfxListener 功能与风险记录

## 需求与原始基线

- 需求是在 IWanner 关卡新增一个参考 `PlayVfxListener` 的粒子播放句柄，保留其 `particle`、`autoPlay` 与执行完成
  行为，并把粒子父节点与播放位置拆成 `root`、`target` 两个 Inspector 引用。
- 共享 `PlayVfxListener` 原行为是 `Instantiate(particle, target)`，因此 `target` 同时承担父节点职责；实例激活后
  等待 0.1 秒把 `BaseItemEventHandle` 标记完成。
- 本次不修改共享 `PlayVfxListener`，避免改变已有主线或 DrawGame 使用者行为。

## 影响文件、资源与系统

- 新增 `IWannerGame_PlayVfxListener.cs` 及 Unity `.meta`。
- 复用 `Game.ItemEvent.BaseItemEventHandle`、`TriggerExecutor/BaseItemExecutor`、`Bear.Logger` 与 UniTask 等待。
- 未修改或绑定 IWanner prefab、场景、ParticleSystem 资源、Luban 配置、DB/Save、UI、全局事件、Analytics、广告或
  平台 SDK。

## 修改后行为与资源流

1. Inspector 配置 `root`、`target`、`particle` 与 `autoPlay`；`autoPlay` 默认 false，与共享 Listener 一致。
2. `Execute()` 以 `root` 为父节点实例化一个 `particle`，随后只把粒子实例的世界坐标设置为
   `target.position`，不复制 `target` 的旋转与缩放。
3. 实例被激活；`autoPlay` 开启时，组件通过 `GetComponentsInChildren<ParticleSystem>(true)` 获取全部子级粒子并
   逐一调用 `Play(false)`。关闭时不显式播放，由粒子预制体自身的 Play On Awake 配置决定。
4. 触发 0.1 秒后把 `IsRunning` 设为 false、`IsDone` 设为 true，保持参考 Listener 的执行器推进节奏。
5. `root`、`target` 或 `particle` 任一缺失时使用 Bear Logger 记录警告并立即完成，避免 Sequence Executor 卡住。
6. 每次触发都会创建新实例；粒子自身销毁逻辑或 `root` 所属关卡的层级清理负责回收实例。

## 验证记录

- `autoPlay` 补充后的隔离式定向编译通过：`0 error / 0 warning`。
- 静态确认 `Instantiate(particle, root)` 建立父子关系，随后 `particleInstance.transform.position = target.position`
  设置指定世界位置。
- 静态确认 `autoPlay` 默认 false；开启时与共享 `PlayVfxListener` 一致，递归获取包含失活子节点在内的全部
  `ParticleSystem`，并逐一调用 `Play(false)`。
- 静态确认缺失绑定路径会同步收口 `IsRunning/IsDone`，且脚本没有 `Update`、`OnValidate`、`Reset`、
  `[ExecuteAlways]`、DB、Save、资源管理器或全局事件访问。
- 直接构建生成的 `Assembly-CSharp.csproj` 被当前工程既有的 `Temp/bin/Debug` 依赖缺失阻断，共报告 200 条
  `CS0006` 元数据缺失；该命令未形成 Unity 全工程编译通过证据。

## 剩余风险与未验证项

- `uncertain`：尚未把组件绑定到具体 IWanner prefab，也未在 Unity PlayMode 验证父子层级、目标世界坐标、
  粒子 `playOnAwake` 表现、重复触发和执行器顺序推进。
- `uncertain`：尚未执行 Android/iOS 横屏设备验证；脚本没有平台分支，但实际粒子开销与视觉位置需结合目标资源确认。
- 组件不主动销毁粒子实例；若配置的粒子预制体没有自销毁逻辑且 `root` 长期存在，重复触发会持续累积对象。
- 组件只设置位置。若后续需要跟随 `target` 移动或复制旋转/缩放，应作为新需求明确增加，不能假设当前行为已经覆盖。

## 回滚路径

- 从未来使用方的 Executor 列表或 prefab 组件中移除 `IWannerGame_PlayVfxListener`，再删除脚本与 `.meta` 即可。
- 当前没有 prefab、配置、存档或资源迁移，因此本次代码回滚不需要数据修复。
