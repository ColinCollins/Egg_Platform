# ShakeRotateHandle 功能说明

## 需求与背景

- 需求是在 `BreathingScaleHandle` 同目录新增通用旋转抖动组件。
- 组件需要支持首次延迟、循环、自动播放等同类 Handle 设置，并保证每次旋转抖动表现之间使用同一个固定间隔。
- 2026-07-14 补充需求：明信片入口感叹号每轮连续摇晃两次，整轮结束后等待 `0.7` 秒再重复。
- 2026-07-14 手感修正：降低感叹号的旋转幅度和振动频率，消除持续、高频、无衰减的乱抖表现。
- 2026-07-14 可见性修正：PlayMode 反馈上一档低强度参数看不到动作，需要在“明显可见”和“不过度乱抖”之间恢复中等幅度。
- 2026-07-14 手感微调：在可见中间档基础上稍微增大摇晃幅度并加快单次摇晃，不提高 `vibrato`，避免重新引入高频乱抖。

## 影响范围

- 脚本：`Assets/Game/Scripts/Common/Others/ShakeRotateHandle.cs`
- Unity 元数据：`Assets/Game/Scripts/Common/Others/ShakeRotateHandle.cs.meta`
- 使用 prefab：`Assets/Game/UI/Postcard/Prefabs/PostcardEnter.prefab`
- 使用系统：Unity `Transform` 与项目现有 DOTween。
- 不修改 scene、关卡、配置、存档、资源地址、全局事件、分析打点或平台 SDK。

## 原有行为基线

- 同目录原有 `BreathingScaleHandle` 使用 DOTween 控制目标缩放，提供自动开始、循环、暂停、恢复、停止和重置能力。
- 变更前同目录没有独立的 `ShakeRotateHandle`，也没有可直接配置“旋转抖动后固定等待间隔”的公共组件。
- 本次节奏调整前，每轮 Sequence 只执行一次 `DOShakeRotation`；`PostcardEnter.prefab` 的等待间隔为 `2` 秒。
- 手感修正前，`PostcardEnter.prefab` 使用 `duration = 1.2`、Z 轴 `strength = 30`、`vibrato = 50`、`fadeOut = false` 和未指定缓动；单次持续时间、角度和振动次数叠加后表现过于剧烈。
- 第一版降幅参数为 `duration = 0.22`、Z 轴 `strength = 10`、`vibrato = 3`、零随机度、衰减和 `Ease.OutQuad`；用户在 PlayMode 中反馈感叹号不动，说明该组合对当前图标表现过弱。

## 新增行为

- `target` 未绑定时在 `Awake` 使用组件自身 `Transform`，并立即记录初始本地欧拉角，确保首次 `OnEnable` 前已有可靠基线。
- 运行态每次进入 `OnEnable` 时，若 `autoStart` 开启，会重新调用 `StartRotate()`；编辑器非运行态由 `Application.isPlaying` 外层保护，不创建 Tween。
- `StartRotate()` 会先停止旧序列、恢复初始角度，再按配置创建新的 DOTween Sequence。
- 首次执行前等待 `delay`；该延迟通过 DOTween 的非前置循环间隔模式设置，因此只生效一次。
- 每轮结构固定为连续执行 `shakeCount` 次 `DOShakeRotation` 后等待 `interval`；`shakeCount` 默认是 `2`，`interval` 默认是 `0.7` 秒。
- `PostcardEnter.prefab` 明确序列化 `shakeCount = 2`、`interval = 0.7`，因此已有感叹号实例会使用双摇节奏，而不是依赖新增字段的反序列化默认值。
- 组件默认值和感叹号实例统一为 `duration = 0.30`、Z 轴 `strength = 18`、`vibrato = 5`、`randomness = 10`、`fadeOut = true`、`Ease.Linear`；相比上一档 `0.35s / 15°` 略快且幅度略大，同时保持低振动次数和衰减收尾。
- `loops = -1` 时无限循环，`loops = 0` 时执行一轮，正数沿用项目现有 DOTween Handle 的循环次数语义。
- 对外提供 `StartRotate()`、`StopRotate()`、`PauseRotate()`、`ResumeRotate()` 和 `ResetToStart()`；禁用或销毁组件时停止序列。
- `ignoreTimeScale` 开启后，旋转抖动、首次延迟和固定间隔均使用不受 `Time.timeScale` 影响的更新时间。

## 数据、资源与事件流

- 输入仅来自组件序列化字段和公开控制方法。
- 运行时只创建和管理一个本地 DOTween Sequence；双摇通过有限的内层 Tween 循环完成，不新增脚本 `Update` 或协程，也不执行 DB/save、资源加载、实例化、全局事件或分析打点。
- 组件停止时 Kill 当前 Sequence；`ResetToStart()` 额外恢复 `Awake` 时记录的本地欧拉角。

## 验证记录

- `dotnet build Assembly-CSharp.csproj --no-restore`：通过，`0` 个错误、`1116` 个项目既有警告。
- 调整为 `OnEnable` 自动重启后，再次执行目标程序集构建（关闭项目引用重建）：通过，`0` 个错误、`98` 个项目既有警告。
- `git diff --check -- Assets/Game/Scripts/Common/Others/ShakeRotateHandle.cs Assets/Game/Scripts/Common/Others/ShakeRotateHandle.cs.meta`：通过，无空白错误。
- 静态敏感项扫描：脚本未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、实例化、全局事件或每帧 `Update`。
- 已通过项目内 DOTween 程序集反射确认 `SetDelay(T, float, bool)` 重载存在；代码使用 `false` 保证首次 `delay` 不随 Sequence 循环重复。
- 2026-07-14 双摇节奏调整后，已打开的 Unity `6000.0.59f2` 自动完成脚本编译和域重载；`Library/ScriptAssemblies/Assembly-CSharp.dll` 的生成时间晚于脚本修改时间，Editor 日志显示 `Mono: successfully reloaded assembly`，未出现本次脚本的 C# 编译错误。
- 本次目标文件 `git diff --check` 通过，仅输出仓库既有的 LF/CRLF 转换提示；敏感项扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、实例化、全局事件或脚本 `Update`。
- 独立执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 时，本机 PATH 下的 `dotnet` 没有 SDK，命令在加载编译器前失败；以已打开的正确版本 Unity 自动编译结果作为替代证据。
- Unity PlayMode 尚未目视确认双摇节奏；Editor 日志中另有 Firebase Editor 插件缺少 iOS Xcode 引用的项目既有加载错误，与本次脚本改动无直接证据关联。
- 2026-07-14 手感参数调整后，目标字段扫描确认脚本默认值与 `PostcardEnter.prefab` 的 8 个相关参数一致；敏感项扫描无命中，目标文件 `git diff --check` 通过，仅有仓库既有的 LF/CRLF 转换提示。
- 验证时 Unity 正处于 Play Mode，`Assembly-CSharp.dll` 时间早于本次脚本默认值调整，因此最新参数尚未由 Unity 重新编译，也未刷新到当前运行实例；需退出并重新进入 Play Mode 后目视验证。
- 可见性排查确认 `StartPanel.prefab` 使用 `PostcardEnter.prefab` 实例，但没有覆盖 `ShakeRotateHandle` 的参数；组件与目标红点对象均在源 prefab 中启用，当前中间档参数会从源 prefab 继承。
- 中间档参数写入后 Unity 已退出 Play Mode，但仍处于资源 Loading；`Assembly-CSharp.dll` 时间暂时早于脚本修改时间。现有 prefab 会直接读取序列化参数，但脚本默认值编译和最终 PlayMode 观感仍待 Unity 完成加载后验证。
- 本次 `0.30s / 18°` 微调后，脚本与 prefab 字段扫描一致，感叹号已有位置 `m_AnchoredPosition = (61.6, 54.8)` 未被覆盖，目标文件 `git diff --check` 通过。Unity 仍处于 Loading，最新脚本默认值编译和 PlayMode 观感待验证。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 中目视确认 `0.30s / 18° / vibrato 5 / 双摇 / 0.7s 间隔` 的最终手感；如仍偏弱或偏强，应优先小幅调整 `strength`，不要再次大幅提高 `vibrato`。
- 尚未在 Unity PlayMode 中计时验证 `delay + (duration * shakeCount) + interval` 的连续多轮节奏，以及暂停、恢复、禁用、重新启用自动重启和重置的视觉结果。
- 尚未在 Android/iOS 横屏真机验证高频振动参数下的表现和移动端性能；组件没有每帧脚本轮询，但 DOTween 本身仍会在表现期间逐帧更新 Transform。
- 当前已由 `PostcardEnter.prefab` 的感叹号使用；其他 prefab 或 scene 是否使用该组件未发现本地证据。

## 建议手工验证

1. 打开 `PostcardEnter.prefab` 并进入 PlayMode，确认感叹号以小角度短促摇晃两次、每次都会减弱回正，随后静止 `0.7` 秒再开始下一轮。
2. 分别调用暂停、恢复、停止和重置接口，确认间隔计时可暂停，重置后角度回到进入 PlayMode 时的值。
3. 在等待或抖动中禁用 GameObject，确认 Sequence 停止；重新启用后确认 `autoStart` 开启时从初始角度重新播放，且没有 DOTween 残留警告。
4. 将 `Time.timeScale` 设为 0，分别验证 `ignoreTimeScale` 关闭和开启时的行为差异。

## 回滚方式

- 若只回滚本次双摇节奏，可移除 `shakeCount` 字段和内层 `SetLoops`，并把 `PostcardEnter.prefab` 的 `interval` 恢复为 `2`。
- 若只回滚本次手感参数，可把 prefab 恢复为 `duration = 1.2`、Z 轴 `strength = 30`、`vibrato = 50`、`randomness = 1`、`fadeOut = false`、`easeType = Unset`，并恢复脚本原默认值。
- 若只回滚可见性修正，可恢复上一档 `0.22s / 10° / vibrato 3 / randomness 0 / Ease.OutQuad`，但该组合已有 PlayMode 不可见反馈。
- 若只回滚本次手感微调，可把 `duration` 恢复为 `0.35`、Z 轴 `strength` 恢复为 `15`，其他参数保持不变。
- 若整体移除组件，先从相关 prefab/scene 移除 `ShakeRotateHandle`，再删除脚本和元数据，避免产生 Missing Script。
