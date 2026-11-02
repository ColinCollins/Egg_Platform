# CommmonPopup 变体使用记录

## 2026-08-04 PencilSkinPopup 金币购买确认界面

### 需求与背景

- `PencilSkinPopup` 的金币解锁确认需要改用新美术变体 `CommmonPopup_2.prefab`。
- 默认 `CommmonPopup.prefab` 仍供退出确认等既有调用使用，不改变其玩家可见行为。

### 影响文件、资源与系统

- `Assets/Game/UI/CommonPopup/Scripts/CommmonPopup.cs`
  - 新增 `CreateVariant2(...)`，内部使用资源名 `CommmonPopup_2` 打开同类型弹窗。
  - 默认 `Create(...)` 继续使用资源名 `CommmonPopup`。
- `Assets/Game/UI/DrawGame/PencilSkinPopup/Scripts/PencilSkinPopup.cs`
  - 金币解锁确认入口改为调用 `CommmonPopup.CreateVariant2(...)`。
- `Assets/Game/UI/CommonPopup/Resources/CommmonPopup_2.prefab`
  - 该资源是 `CommmonPopup.prefab` 的 Unity Prefab Variant，继承同一个 `CommmonPopup` 组件及按钮、标题、正文序列化绑定。

### 原有行为基线

- PencilSkinPopup 在金币充足且未打开确认弹窗时，通过 `CommmonPopup.Create(...)` 显示默认通用弹窗。
- 确认后执行原有金币二次校验、扣费、授予并选中画笔；取消只清理本地弹窗引用。
- PencilSkinPopup 关闭或销毁时会销毁仍存在的确认弹窗。

### 修改后行为

- PencilSkinPopup 的确认购买弹窗使用 `_2` 美术布局。
- 标题、正文、确认/取消回调、重复打开保护、关闭清理、金币校验、扣费、授予和保存流程均保持原逻辑。
- 其他 `CommmonPopup.Create(...)` 调用仍加载默认 prefab，不受本次变体影响。

### 数据、资源、UI、保存与事件流

- UI 流：点击金币画笔 -> `CommmonPopup.CreateVariant2(...)` -> Bear `UIManager.OpenUI<CommmonPopup>("CommmonPopup_2", UILayer.Popup)` -> 确认或取消回调。
- 确认后的 DB、Bag 扣费、画笔授予与保存流程未修改。
- 不新增存档字段、Luban 配置、全局事件、analytics、广告、IAP、RemoteConfig 或平台分支。

### 验证记录

- 静态确认 `CommmonPopup_2.prefab` 的源 prefab GUID 指向 `CommmonPopup.prefab`，且只覆写布局与根节点名称。
- 静态确认默认创建入口和 `_2` 创建入口使用不同资源名；Bear UIManager 会将类型名与资源文件名组合为实例键，因此两个资源不会误复用同一缓存实例。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 个错误、101 个项目既有警告。
- `git diff --check` 对目标脚本和中文记录检查通过；仅有工作副本后续按 Git 配置转换为 CRLF 的提示。
- 静态扫描未发现本次新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、每帧 `Update`、PlayerPrefs、RemoteConfig 或全局事件路径。

### 剩余风险与未验证项

- 尚未执行 Unity PlayMode，需验证 PencilSkinPopup 点击金币解锁时显示 `_2` 布局，确认、取消和关闭父弹窗均正常。
- 尚未执行 Android/iOS 横屏触控 smoke；本次没有新增平台分支。

### 回滚路径

- 将 PencilSkinPopup 的调用恢复为 `CommmonPopup.Create(...)`，并移除 `CreateVariant2(...)`、资源名常量及内部共用创建方法，即可恢复默认弹窗布局；不涉及数据迁移。

## 2026-08-07 确认与取消关闭顺序修复

### 需求与原有行为基线

- `CommmonPopup` 原先在 Yes/No 业务回调执行完成后才调用 `UIManager.CloseUI(this)`。
- 当业务回调同步打开另一个 Popup（例如 PencilSkin 金币不足时打开 `ShopPanel`），新界面会先进入 UI 栈；原 `CommmonPopup` 随后已不再是栈顶，无法稳定完成关闭。
- 用户要求 Yes 或 No 点击后即可关闭；Yes 引发后续页面跳转时，更应先关闭确认弹窗。

### 修改后行为与影响范围

- Yes/No 点击时先暂存对应回调，再关闭当前 `CommmonPopup`，最后执行暂存的业务回调。
- 必须先暂存回调，因为 `OnClose()` 会清空 `_onYesCallback` 与 `_onNoCallback`。
- PencilSkin 点击 Yes 后先关闭购买确认弹窗，再执行余额二次校验；余额不足时随后打开 `ShopPanel`，余额足够时随后扣费并授予画笔。点击 No 时先关闭弹窗，再执行本地引用清理。
- 静态调用扫描确认当前外部调用为 `PencilSkinPopup` 的金币购买确认，以及 `PlayCtrl`、`DrawPlayCtrl` 的退出确认；退出确认仍会在弹窗关闭后执行原退出回调。
- 不修改 prefab、UI 层级配置、Bag、存档、资源、Luban 配置、全局事件、analytics、IAP 或平台分支。

### 验证、剩余风险与回滚

- 已通过静态调用链核对确认：业务回调不再有机会在确认弹窗关闭前同步压入新 Popup。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`
  通过，结果为 `0 error / 101 warning`，warning 为工程既有项。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏验证 Yes、No、关闭按钮、PencilSkin 余额不足跳转商店及退出确认表现。
- 回滚时将 Yes/No 方法恢复为先执行回调、再调用 `CloseUI(this)`；无需迁移数据或资源，但会重新引入回调同步打开新 Popup 时的关闭顺序问题。
