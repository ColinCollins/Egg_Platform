# CustomButton ClickScaleAnim 缩放目标记录

## 需求背景

`CustomButton` 的业务点击依赖 Unity UI 的 `PointerClick`，而 `ClickScaleAnim` 原本直接缩放按钮自身 `transform`。当玩家点击按钮边缘时，按下阶段命中按钮并触发缩放；缩小后抬起位置可能落到按钮缩小后的射线范围外，导致 `PointerClick` 不成立，但 `PointerDown` / `PointerUp` 反馈仍然发生。

本次需求是在不改 `CustomButton` 事件语义、不批量修改现有 prefab 的前提下，让 `ClickScaleAnim` 支持配置独立视觉缩放目标。

## 影响文件与系统

- 运行时代码：`Assets/Game/Scripts/Common/Buttons/ClickScaleAnim.cs`
- 相关系统：`CustomButton`、`ButtonClickTrigger`、Unity UI EventSystem 的 pointer down/up/click 流程
- 本次未修改：`CustomButton.cs`、UI prefab、UIGenerator 层级生成逻辑、保存、配置、资源加载、广告、IAP、analytics、平台 SDK

## 原行为基线

- `ClickScaleAnim.BeginPressAnim()` 从当前按钮自身 `transform.localScale` 缩放到 `Vector3.one * Scale`。
- `ClickScaleAnim.BeginReleaseAnim()` 从当前按钮自身 `transform.localScale` 回弹到 `Vector3.one`。
- 未配置独立视觉节点时，按钮根节点的视觉缩放会同步改变 Graphic Raycast 的有效命中区域。
- `CustomButton.OnPointerDown()` / `OnPointerUp()` 会触发 `ButtonClickTrigger`，`OnPointerClick()` 才触发业务 `OnClick`。

## 新行为

- `ClickScaleAnim` 新增可选序列化字段 `scaleTarget`，用于指定按钮按压缩放的视觉目标。
- `scaleTarget` 为空时保持旧行为：继续缩放当前按钮物体，并回弹到 `Vector3.one`。
- `scaleTarget` 不为空时，只缩放该目标节点；按钮根节点可保持不变，从而保留原始射线命中范围。
- 新增 `SetScaleTarget(Transform target)`，允许运行时在按钮开始交互前设置缩放目标；交互中切换目标会重置当前动画状态。

## UI 与事件流

- 按下：`CustomButton.OnPointerDown()` 调用 `ClickScaleAnim.OnButtonDown()`，动画读取 `scaleTarget` 或自身作为缩放目标。
- 抬起：`CustomButton.OnPointerUp()` 调用 `ClickScaleAnim.OnButtonUp()`，动画回弹到目标节点记录的常态缩放。
- 点击成立：仍由 Unity UI EventSystem 在抬起时判断是否命中同一个 click handler 后触发 `CustomButton.OnPointerClick()`。
- 该改动不改变 `CustomButton.OnClick` 的订阅、节流、`Interactable` 或 `IsBlockClick` 语义。

## 使用边界

- 要修复边缘点击丢失，需要按钮根节点保留 `CustomButton` 和射线命中 Graphic，并把 `ClickScaleAnim.scaleTarget` 指向子视觉节点。
- 对没有独立视觉子节点的旧 prefab，本次代码不会自动改变表现；仍需要后续手动拆分 HitRoot / VisualRoot 或生成器支持独立视觉节点。
- 如果视觉节点初始缩放不是 `Vector3.one`，配置 `scaleTarget` 后会以该节点首次解析到的 `localScale` 作为回弹常态。

## 验证记录

- 已静态确认 `ClickScaleAnim.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`Instance`、`Instantiate` 链路。
- 已静态确认本次没有修改 UI prefab，因此不会产生批量 prefab 元数据变更。
- `git diff --check -- Assets/Game/Scripts/Common/Buttons/ClickScaleAnim.cs DevelopMD/CustomButton_ClickScaleAnim_Target_Record.md DevelopMD/已有功能设计与使用记录.md` 已通过；仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。

## 未验证风险

- 尚未在 Unity PlayMode 中验证配置了 `scaleTarget` 的按钮边缘点击能稳定触发 `CustomButton.OnClick`。
- 尚未在 Android/iOS 横屏真机上验证快速点击、滑出边缘、触摸取消时的缩放回弹和业务点击表现。
- 现有未配置 `scaleTarget` 的 44 个 ClickScaleAnim prefab/scene 引用仍保持旧行为，不应视为已修复。

## 回滚路径

回滚 `Assets/Game/Scripts/Common/Buttons/ClickScaleAnim.cs` 中的 `scaleTarget` 字段、`SetScaleTarget(...)` 方法和目标解析逻辑，即可恢复为只缩放按钮自身的旧行为。若后续 prefab 配置了 `scaleTarget`，回滚代码前需先清理对应序列化字段，避免 Unity 显示丢失字段。

## 2026-10-09：共享 Gameplay 按钮层级兼容修复前置核对

### 需求与行为基线

- 本次验收对象是此前共享 `GamePlayPanel.prefab` 将移动/跳跃按钮视觉拆到 `bg` 子节点后，Level 58、131、132、137 的既有关卡行为是否仍成立，并只修复已取得本地证据的问题。
- 必须保留共享按钮根节点稳定命中、`ClickScaleAnim` 只缩放视觉子节点、`CustomButton` 事件语义不变；不得借机调整关卡玩法参数、存档、配置、事件、资源加载或其它已验收关卡。
- `Assets/GF`、`Packages` 与 GuruSDK 保持只读。本轮相关目标文件在任务开始时相对当前工作区无未提交差异；工作区其余已有改动不属于本任务。

### 来源、原流程与维护边界

- 共享来源：`Assets/Game/UI/GamePanel/Prefabs/GamePlayPanel.prefab` 的 `jump_btn/bg/UI_youxizhong_icon_jiantou_01`、根节点 `UIEmptyRaycastTarget` 与 `ClickScaleAnim.scaleTarget`；`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.cs` 继续维护按钮事件。
- Level 131：`Level131Ctrl.ResolvePanelBinding()` 仍按旧直接子节点路径读取图标；图标为空不阻断按钮订阅，但 `SetRocketControlIcon()` 无法给出火箭控制提示。
- Level 132：`Level132Hud` 仍按旧直接子节点路径读取箭头；按钮仍可显示和响应，但翻绳方向旋转提示失效。
- Level 137：`Level137Ctrl.BindPanel()` 仍按旧直接子节点路径读取图标；图标为空会进入错误分支并提前返回，后续 `ApplyHookControls()` 不执行，属于上车/出钩输入阻断。
- Level 58：`GamePlayPanel_020_58.prefab` 为该关移除了继承的 Jump `CustomButton` 与按压动画，并把旧根 Image 的 `raycastTarget` 设为 false；共享层级替换后该覆盖失去目标，而新根 `UIEmptyRaycastTarget` 与 `bg` Image 默认接收射线，可能形成无业务处理器的大命中遮挡。该关已有局部 SkeletonGraphic 射线状态保持不变，不扩大本轮范围。

### 拟修改位置、理由与影响范围

- 在共享 `GamePlayPanel` 增加由基础 Prefab 显式绑定的只读跳跃图标引用，Level 131、132、137 改用该引用；不新增按名称或层级扫描兜底，不改变按钮事件或其它面板实例的行为。
- 在 `GamePlayPanel_020_58.prefab` 仅移除新继承的根 `UIEmptyRaycastTarget`，并把新 `bg` Image 的 `raycastTarget` 覆盖为 false，同时清理已经指向被删除旧组件的两条失效覆盖；保留该关原有新增子对象与射线配置。
- 影响入口限定为使用共享 GameplayPanel 的上述关卡 HUD；Level 117 独立面板、DrawGame、IWanner 以及未引用跳跃图标 API 的普通关卡不在本轮行为修改范围。

### 验证场景与未决项

- 静态核对共享 Prefab 字段引用、全部旧图标路径调用是否清零、Level 58 variant 的移除/覆盖闭合，以及四关源码/Prefab 定向差异。
- 执行可用的 C# 编译与文本/YAML 引用检查；编译只证明源码，不证明 Prefab 可导入或玩家操作通过。
- Unity PlayMode、Level 58 实际触摸穿透、Level 131 火箭图标、Level 132 翻绳图标、Level 137 上车/出钩、Android/iOS 横屏触摸均尚未执行，完成前必须标为 `uncertain`。

### 实现与验证结果

- `GamePlayPanel.cs` 新增 `jumpButtonIcon` 序列化字段和只读 `JumpButtonIcon` 入口，基础 `GamePlayPanel.prefab` 显式绑定现有图标 Image `fileID 5393492493011141298`。Level 131、132、137 已改用该入口，不再读取旧直接子节点路径。
- `GamePlayPanel_020_58.prefab` 已移除继承的 Jump 根 `UIEmptyRaycastTarget`，并把新 `bg` Image 的 `raycastTarget` 覆盖为 false；旧根 Image 的两条失效覆盖已删除。该关原有 SkeletonGraphic、其它新增对象和组件状态未改。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：退出码 0，0 error / 146 warning。警告属于当前工程既有诊断；这不是 Unity 导入或 PlayMode 证据。
- 文本静态断言通过：基础字段恰好绑定一次；`GamePlayPanel_000` 继续继承基础 Prefab 且未移除跳跃图标；Level 58 的根射线组件移除、背景射线关闭及旧 fileID 清理闭合；生产脚本中三处旧图标 `Transform.Find` 路径已清零。
- 现有 Unity 6000.0.59f2 编辑器会话已检测本轮文件并完成基础 `GamePlayPanel`、`GamePlayPanel_000`、`GamePlayPanel_020_58` 及其余派生面板的 PrefabImporter；相关日志没有目标导入错误。目标文件 `git diff --check` 通过，仅有 Git 行尾转换提示。未执行四关 PlayMode、Android/iOS 或 IL2CPP，玩家可观察行为仍需按前述场景人工验收。

### 本轮回滚

- 共享部分：移除 `GamePlayPanel.jumpButtonIcon` 字段/API及基础 Prefab 绑定，并恢复三关旧读取方式；这会重新引入已确认的层级失配，不建议脱离旧层级恢复一起单独回滚。
- Level 58：只恢复该 variant 对新根 `UIEmptyRaycastTarget` 和 `bg` Image 的本轮两项处理；不得整体恢复 Prefab，以免覆盖该关原有自定义对象和覆盖项。
