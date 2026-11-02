# PostcardGrayImage 功能说明

## 需求背景

2026-07-06 需求：为 Postcard 的 page Image 提供一个运行时组件，负责自动切换绑定对象的材质，并把横向灰度填充参数传给 `UI/PostcardPageGrayFill` shader。目标是让业务或动画逻辑只需要驱动 `fillAmount`，组件内部处理材质切换、参数写入和恢复原材质。

## 影响范围

- 新增脚本：`Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs`
- 依赖资源：`Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader`
- 推荐材质模板：`Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat`
- 相关系统：Unity UGUI `Graphic.material` / `Image.material` 渲染链路。

2026-07-06 后续补充中，`Postcard_PageItem` 已开始在普通页完成态调用 `PostcardGrayImage.SetFillAmount(...)`。本组件本身仍不修改 Postcard prefab、`Postcard_ProgressItem`、DB/save、Luban 配置、RemoteConfig、资源加载地址、全局事件、打点、广告、IAP 或平台 SDK。

## 原有行为基线

- `Postcard_PageItem` 中存在明信片页背景 `Image bg` 引用，但当前脚本不主动修改该 Image。
- `PostcardPageGrayFill.shader` 已提供 `_FillAmount` 与 `_GrayAmount`，但缺少统一的运行时组件负责材质替换和参数写入。
- 直接修改共享 `.mat` 的 `_FillAmount` 会影响所有复用同一材质的 UI，因此需要组件侧运行时材质实例。
- 2026-07-06 复查发现：直接在 Inspector 或动画中修改序列化字段 `fillAmount` 时不会调用 `SetFillAmount(float)`，旧实现不会自动刷新材质；shader 直接用 sprite 原始 `uv.x` 做阈值，图片进入图集后 `uv.x` 也不一定是本地 `0..1`。

## 当前行为

- `PostcardGrayImage` 可挂在 page Image 同物体上；`targetGraphic` 为空时运行时回退到当前对象上的 `Graphic`。
- `Postcard_PageItem` 当前会在普通页未完成时设置 `fillAmount = 0`，首次完成时用 DOTween 驱动 `fillAmount` 从 0 到 1；`Postcard_unlock` 锁定占位页不处理该组件。
- `grayMaterial` 建议绑定 `PostcardPageGrayFill.mat`；运行时会复制为当前组件私有材质实例，再写入 `_FillAmount` / `_GrayAmount`。
- `grayMaterial` 为空时，组件会通过 `Shader.Find("UI/PostcardPageGrayFill")` 在运行时创建私有材质。
- 组件会基于目标 `Image` 当前 sprite 的 `sprite.uv` min/max 计算 `_FillUVRect`，传给材质实例，让 shader 先把图集 UV 归一化后再按 `_FillAmount` 做横向灰度。
- `SetFillAmount(float)` 会把参数限制到 `0..1`：
  - `0`：启用灰度材质并写入 `_FillAmount = 0`，画面保持原色但材质已补到目标 Graphic 上。
  - `>0`：启用灰度材质并写入横向灰度填充比例。
- `SetGray(bool)` 可显式切换灰度材质；传入 `false` 时会把 `fillAmount` 收敛到 `0` 并恢复原材质。
- `SetGrayAmount(float)` 只控制灰度强度 `_GrayAmount`，不会修改原材质。
- PlayMode 下 `OnValidate()` 会响应 Inspector 字段变更并刷新本组件材质；`OnDidApplyAnimationProperties()` 会响应动画系统写入的序列化字段。
- `OnDisable()` / `OnDestroy()` 会恢复目标 Graphic 原材质；`OnDestroy()` 同时销毁组件创建的运行时材质实例。

## 数据、资源与 UI 流

- 组件只读取当前 GameObject 上的 `Graphic` 或 Inspector 绑定的 `targetGraphic`，只修改该对象的 `material`。
- 组件不读取或写入 `DB` / save，不消耗星星，不触发解锁逻辑，不创建 GameObject，不实例化 prefab，不发全局事件。
- 运行时新增的材质实例使用 `HideFlags.HideAndDontSave`，用于避免污染共享材质资产和场景序列化数据。
- `Postcard_PageItem` 只通过本组件公开 API 写入 `fillAmount`，不直接修改材质实例或共享 `.mat`。

## 验证记录

- 2026-07-06 静态关键字扫描：`rg -n "OnValidate|\bReset\b|ExecuteAlways|ExecuteInEditMode|\bDB\b|\bSave\b|RemoteConfig|\bInstance\b|Instantiate|PlayEvents" Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat` 仅命中预期的 `OnValidate`；该入口位于 `#if UNITY_EDITOR` 内，只在 PlayMode 调用本组件本地材质刷新，不访问 DB/save/config。
- 2026-07-06 空白检查：`rg -n "[ \t]+$" Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs.meta Assets/Game/UI/Postcard/Scripts/PostcardGrayImage功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardGrayImage功能说明.md.meta` 未发现行尾空白。
- 2026-07-06 新增脚本和文档仍是未跟踪文件，普通 `git diff --check` 不覆盖；行尾空白已通过 `rg` 命令覆盖检查。
- 2026-07-06 `_FillUVRect` 静态检查：`rg -n "_FillUVRect|FillUVRect|OnDidApplyAnimationProperties|OnValidate|sprite\.uv" Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat` 确认组件、shader 和默认材质均包含新参数链路。
- 2026-07-06 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings。
- 2026-07-06 `Postcard_PageItem` 接入灰度控制后，同一编译命令通过，结果为 97 warnings / 0 errors；`git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs` 因用户新增注释行尾空格未通过，按“不修改注释”要求保留未改。
- 2026-07-06 `SetFillAmount(0)` 行为修正后，静态复查确认该方法会把 `isGray` 置为 `true` 并刷新材质；恢复原材质仍由 `SetGray(false)`、`OnDisable()` 或 `OnDestroy()` 负责；同一编译命令复跑通过，结果为 0 error / 97 warnings。
- Unity Editor/PlayMode 尚未运行，实际 Inspector 绑定和 Image 渲染效果待验证。

## 剩余风险

- 未在 Unity Editor 中实际挂载 `PostcardGrayImage` 验证 `targetGraphic` 回退绑定、原材质恢复和参数写入。
- 未在 PlayMode 中配合 `Postcard_PageItem` 验证普通页完成瞬间的 DOTween 连续过渡表现。
- 如果目标 Image 使用旋转打包图集或特殊 Mesh Type，当前基于 `sprite.uv` min/max 的归一化仍需要结合实际资源确认横向方向是否符合美术预期。
- Android/iOS 横屏设备渲染与性能未验证。

## 回滚方式

- 删除 `Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs` 及对应 `.meta`。
- 删除本说明文档及对应 `.meta`。
- 移除 Postcard page Image 上新增的 `PostcardGrayImage` 组件引用。
