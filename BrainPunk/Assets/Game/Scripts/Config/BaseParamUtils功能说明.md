# BaseParamUtils 功能说明

## 2026-07-03 ParamBase 参数解析工具

### 需求背景

- `PostcardConfig.Rewards` 已调整为 `Dictionary<GameProps, ParamBase>`，业务侧需要从 Luban 生成的 `IntParam`、`StringParam` 中读取实际参数值。
- `CommonGetRewardPopup` 原先直接在弹窗内部处理 `ParamBase` 的 Value 反射读取，解析职责混在 UI 奖励展示和发放逻辑里。
- 本次按要求把基础参数解析抽离到 `Assets/Game/Scripts/Config/BaseParamUtils.cs`，作为配置工具层的静态 API。

### 影响范围

- `Assets/Game/Scripts/Config/BaseParamUtils.cs`
  - 新增 `TryGetValue(Config.ParamBase, out object)`。
  - 新增 `TryGetInt(object, out int)`。
  - 新增 `TryGetString(object, out string)`。
- `Assets/Game/UI/CommonPopup/Scripts/CommonGetRewardPopup/CommonGetRewardPopup.cs`
  - Coin / Star 数量转换改为调用 `BaseParamUtils.TryGetInt(...)`。
  - Skin id 转换改为调用 `BaseParamUtils.TryGetString(...)`。
  - 弹窗不再持有 `ParamBase` 的 Value 反射读取逻辑。
- 依赖但未修改的生成代码：
  - `Assets/Configs/Code/ParamBase.cs`
  - `Assets/Configs/Code/IntParam.cs`
  - `Assets/Configs/Code/StringParam.cs`
  - `Assets/Configs/Code/PostcardConfig.cs`

### 原行为基线

- `CommonGetRewardPopup` 已支持 `Dictionary<GameProps, object>` 和 `Dictionary<GameProps, Config.ParamBase>` 两类奖励输入。
- 数量奖励支持 `int`、安全范围内的 `long`、`float`、`double` 和可解析为整数的字符串。
- `Config.ParamBase` 派生对象通过运行时反射读取公开 `Value` 字段，再继续转为数量或 skin id。

### 新行为

- `BaseParamUtils.TryGetValue(...)` 优先直接识别当前已生成的 `Config.IntParam` 和 `Config.StringParam`，取不到明确类型时再降级读取公开 `Value` 字段。
- `BaseParamUtils.TryGetInt(...)` 可接收普通对象或 `Config.ParamBase`，并保持 `float` / `double` 向下取整的数量解析口径。
- `BaseParamUtils.TryGetString(...)` 可接收普通对象或 `Config.ParamBase`，用于把 `StringParam` 或其他对象转换为字符串 id。
- `CommonGetRewardPopup` 仍只负责奖励 UI、查 Skin 表、发放 Coin / Star / Skin 和广告按钮状态，不负责 Luban 参数拆包。

### 数据、资源与事件流程

- 本次不新增配置表、不新增存档字段、不新增资源地址、不新增全局事件。
- `PostcardConfig.Rewards` 中的 `ParamBase` 仍由 Luban 生成代码反序列化。
- 业务调用链为：配置对象 `ParamBase` -> `BaseParamUtils` 读取基础值 -> 弹窗按 `GameProps` 解释为数量或 skin id -> 原有发奖与 UI 刷新路径。

### 验证记录

- 已运行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`。
- 首次运行失败原因：当前本地 `Assembly-CSharp.csproj` 是 Unity 生成且被 `.gitignore` 忽略的文件，新脚本未自动进入编译列表。
- 临时把 `Assets/Game/Scripts/Config/BaseParamUtils.cs` 加入本地 `Assembly-CSharp.csproj` 后重新运行，结果为 0 error / 97 warnings。

### 未覆盖风险

- 尚未在 Unity Editor 重新生成 csproj 后复核新脚本自动进入工程文件。
- 尚未在 Unity PlayMode 中用真实 `PostcardConfig.Rewards` 打开 `CommonGetRewardPopup`。
- 尚未在 Android/iOS 横屏真机上验证奖励弹窗 UI、广告回调和 Skin 图标资源加载。
- `TryGetValue(...)` 的降级反射依赖后续 Luban 派生类继续暴露公开 `Value` 字段；如果 Luban 模板变化，需要同步调整工具类。

### 回滚路径

- 删除 `Assets/Game/Scripts/Config/BaseParamUtils.cs` 和对应 `.meta`。
- 将 `CommonGetRewardPopup` 中的 `TryGetRewardCount(...)` 与 `ResolveSkinReward(...)` 改回本地 `ParamBase` 解析实现。
- 如只需回滚配置工具，不需要回滚 Luban 表结构和生成代码；若要恢复旧奖励入参，则需同时回滚 `PostcardConfig.Rewards` 的表定义与生成产物。
