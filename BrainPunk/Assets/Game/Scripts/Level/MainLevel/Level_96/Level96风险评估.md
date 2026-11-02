# Level096 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：`OnDisable()` / `OnDestroy()` 原本都执行协程取消和事件重置；线索隐藏仍保留在停用专属分支。
- **直接结论**：密码线索可见性、协程终止、事件解绑、暂停/销毁边界和配置读取均未改变。
- **假设**：无新增运行时假设；共享方法为纯私有代码复用。
- **缺失证据**：未执行 Unity PlayMode 密码输入、暂停、重开及移动端拖拽回归，标记为 `uncertain`。
- **回滚**：将 `CleanupRuntimeState()` 内联回两个回调。

更新时间：2026-08-11

## 原有行为基线

Level006 的问号方块由完整的顶击监听和倒计数切换链驱动，密码入口打开共享 PasswordPopup。复制场景本身不能区分“游戏内设置暂停”与商店、提示等其它暂停来源，也没有在恢复、重置和销毁边界统一收起密码提示。

## 受影响资源与既有系统

- 本关 `Level_096.prefab`、`Level96Ctrl` 和 `PasswordPopup_96_3316`。
- `LevelData.xlsx` 及其生成物 `tbleveldata.json`。
- 只读复用 `Level6GamePanelFeature`、`PasswordPopup`、`GameSettingPopup`、`DragableItem`、Tips 和既有玩法状态事件。

没有新增全局事件、共享密码按钮、存档字段、资源加载规则、打点、广告、内购或平台分支。

## 风险与处理

| 风险 | 处理 | 验证状态 |
| --- | --- | --- |
| 商店或提示弹窗同样进入 PAUSE，误显示密码 | 暂停后一帧同时检查当前状态及 `GameSettingPopup.IsVisible` | 代码静态通过，PlayMode 未验证 |
| 设置页关闭、重开或退出后提示残留 | 恢复、重置、非暂停状态、Disable、Destroy 均隐藏，并取消待执行协程 | 代码静态通过，事件时序未实机验证 |
| 本关密码资源影响 Level006/080 | 只在 Level096 初始化时调用既有配置入口；共享默认值保持不变 | 静态调用边界通过 |
| 切关或 Reset 时把 3316 配到即将销毁的旧 HUD | 配置协程入口固定等待一帧，等 PlayCtrl 同帧完成 `RefreshGamePanel` 后再取当前面板 | 调用顺序静态通过，Reset 实机未验证 |
| 密码与输入键不匹配 | 提示和 CorrectPassword 均为 `3316`，只使用共享 1 至 9 按键，不新增 0 键 | 静态通过 |
| 复制方块仍执行旧顶击逻辑 | 四个 Box 的六类旧行为组件全部移除，仅挂本关需要的 DragableItem | YAML 静态通过 |
| 拖走后占位文字不可见或盖住方块 | Tips 排序 49、Box 排序 50，初始由方块遮挡 | 静态通过，画面未验证 |
| Prefab YAML 或脚本 GUID 损坏 | fileID 唯一、本地引用完整、16 个脚本 GUID 均能解析 | 静态与导入日志通过，Inspector 未验证 |
| 配置生成覆盖既有脏改动 | Luban 输出到临时目录，只复制 tbleveldata；结构化比对确认只新增 10096 | 已验证 |
| xlsx 重新导出改变既有表格数据 | 全表对比既有值、公式、两张工作表、定义名称和 3 条批注；Luban 隔离生成通过 | 数据层已验证；Office/WPS 包装元数据被导出器规范化 |
| 未配置排序导致正常入口不可达 | 不猜测策划顺序，不修改正在打开的 LevelSort | 待策划确定，`uncertain` |

## 验证证据

- Prefab：245 个 YAML 定义、245 个唯一 fileID、0 个缺失本地引用。
- 四个 Box 均恰好保留 Transform、BoxCollider2D、SortingGroup、DragableItem；旧六类行为组件在 Box 上均为 0。
- 四个占位 Tips 的文字为 `8/5/2/9`，四个 RectTransform 均挂在关卡根节点下。
- 关卡提示和密码变体均为 `3316`；本关范围无 `0316` 和 0 键相关实现。
- `PlayCtrl.CreateLevelAsync` 的静态顺序为实例化 Level 后立即 `RefreshGamePanel`；本关配置协程先等待一帧，不会在 `OnEnable` 调用栈中配置旧面板。
- Luban 生成 exit code 为 0；生成 JSON 相比原项目 JSON仅新增 `10096`，无删除、无既有条目变化、无重复 ID。
- xlsx 全表检查确认 `level` 的既有行值/公式完整平移，`draw_level` 值/公式完全一致，原有 3 条批注和定义名称保留；单元格公式错误扫描为 0。
- Unity 6000.0.59f2 Bee/Roslyn 隔离编译 exit code 为 0，无 Level096 错误或 warning；其它输出为工程既有 warning。
- 当前 `Editor.log` 已记录 Level096 关卡 prefab 与 3316 密码变体由 PrefabImporter 成功导入；日志尾部无 C# error 或 Missing Script。

## 未验证缺口

- Unity Editor 日志已确认资源导入；Inspector 序列化显示仍需人工确认，命令行 Roslyn 与日志检查不替代 PlayMode。
- PlayMode 的设置页显示/关闭、快速连续打开关闭、Reset、退出、错误密码和正确密码完整链路未验证。
- 复制场景中 Box 的 inactive lock/Coin/反馈子节点仍作为无触发资源保留；旧监听已删除，静态上不会执行，但是否需要进一步做纯资源清理待策划确认。
- Android/iOS 横屏触摸、碰撞、不同安全区/分辨率下的密码提示与文字布局未验证。
- LevelSort 正式位置尚未确定。

## 回滚路径

回退 Level096 本地 prefab、脚本和密码 Resources 变体，删除 LevelData/JSON 中 `10096`。所有共享参考关卡与共享暂停、密码、拖拽系统保持原样，不需要跟随回滚。
