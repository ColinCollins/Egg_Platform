# Level096 功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并重复生命周期清理，保持密码线索、拖拽、协程和重置行为不变。
- 影响文件：仅 `Level96Ctrl.cs`。新增私有 `CleanupRuntimeState()` 统一取消待处理协程并解绑事件；`SetPasswordClueVisible(false)` 仍只在 `OnDisable()` 执行。
- 前后差异：清理语句从两个回调的重复实现改为共享私有入口，执行顺序和关卡数据/资源/UI/事件流不变。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：把公共清理体分别展开回两个生命周期回调即可。

更新时间：2026-08-11

## 需求背景

Level096 以 Level006 为场景和密码门基线。玩家打开游戏内暂停设置页时，场景密码提示显示；关闭设置页后提示隐藏。四个问号方块可以像 Level080 一样任意拖走，露出其下方可由策划继续编辑的占位文字。当前正确密码为 `3316`，不需要补充数字 `0` 按键。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_096.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_96/Level96Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_96/Resources/PasswordPopup_96_3316.prefab`
- `Luban/Configs/Datas/LevelData.xlsx`
- `Assets/Game/Configs/tbleveldata.json`

只读复用 `GamePlayPanel_001`、`Level6GamePanelFeature`、`PasswordPopup`、`GameSettingPopup`、`DragableItem`、既有暂停/恢复/状态/重置事件和 Tips 预制体。没有修改 Level006、Level080、共享暂停页、共享密码输入逻辑、存档、打点、RemoteConfig 或平台 SDK。

## 原有行为基线

复制基线中的四个问号方块带有射线触发、音效、反馈、等待和倒计数切换组件，会执行原 Level006 的顶击/锁定/切换逻辑。`Level6GamePanelFeature` 默认打开共享 `PasswordPopup`。场景密码提示没有“仅设置暂停页显示”的本关状态边界。

## 本次行为

- `Level96Ctrl` 在关卡启用后先无条件等待一帧，避开 PlayCtrl“先实例化关卡、再替换旧 HUD”的同帧顺序；随后等待当前玩法面板创建，并仅为本关把 `Level6GamePanelFeature` 的密码资源配置为 `PasswordPopup_96_3316`。
- 密码变体继承既有 `PasswordPopup`，只覆盖资源名和 `CorrectPassword=3316`。共享面板本来只提供数字 1 至 9；当前密码可完整输入，因此没有新增 0 键。
- 场景中的 `3316` 提示保持策划已摆放的位置和旋转，初始为 inactive。收到暂停事件后延迟一帧，并同时确认 FSM 为 `PAUSE`、`GameSettingPopup` 存在且可见，才显示提示。
- 收到恢复、重置或任意非暂停状态事件时立即隐藏提示；脚本禁用和销毁时取消协程、解绑事件并隐藏提示。
- 四个 Box 移除了原 Level006 的 `Raycast2DExecutor`、`PlaySfxListener`、`FeedbackListener`、`WaitNodeHandle`、`CountdownSwitchObjectByListener` 和 `CountdownSwitchComponentByListener`，各保留一个不限制方向和区域的 `DragableItem`。
- 四个 Box 下方各放置一个 Tips 实例，占位文字依次为 `8`、`5`、`2`、`9`。占位 Tips 的排序为 49，Box 为 50；文字未绑定本地化组件，策划可直接修改。
- `LevelData` 新增 `10096 / Level_096`，生成 JSON 只新增同一条记录。因最终关卡顺序未指定且 `LevelSort.xlsx` 正被打开，本次没有修改 LevelSort。

## 运行流程

暂停提示：点击游戏内设置按钮 -> 玩法进入 `PAUSE` 并派发暂停事件 -> 创建 `GameSettingPopup` -> 下一帧本关确认该设置页可见 -> 显示场景 `3316` -> 关闭设置页并恢复玩法 -> 隐藏提示。

解谜流程：拖走四个问号方块 -> 查看下方占位信息 -> 进入既有 PasswordBoard 范围显示 Use 按钮 -> 打开 `PasswordPopup_96_3316` -> 输入 `3316` -> 继续使用既有密码成功和关卡推进链路。

## 已执行验证

- 静态 YAML：`Level_096.prefab` 共 245 个定义，fileID 全部唯一，内部本地引用缺失数为 0。
- 静态绑定：`Level96Ctrl`、`passwordClue`、四个 `DragableItem` 和四个占位 Tips 均可解析；四个 Box 上原六类 Level006 组件计数为 0。
- 密码一致性：关卡提示、控制器资源名和密码变体均为 `3316`；Level096 范围内没有 `0316`、0 键或 `PasswordPopup_96_0316` 残留。
- 生命周期静态检查：密码面板配置在协程入口固定延后一帧，可避开切关和 Reset 时仍被 `CurrentGamePlayPanel` 引用的旧 HUD。
- Luban 在隔离输出目录生成成功。与生成前 JSON 比较后，条目数由 96 增至 97，唯一新增 ID 为 `10096`，既有 ID 内容无变化且无重复 ID。
- Excel 最终预览确认 096 行样式与相邻行一致，`false` 保持文本格式而非 Excel 布尔复选框。
- Excel 全表对比：`level` 表在插入行前后的既有值和公式完全保留，`draw_level` 全表值与公式逐格一致；两张工作表、原定义名称和 3 条批注均保留，单元格公式错误扫描为 0。
- 使用 Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn，将程序集输出到临时目录进行隔离编译：exit code 0；没有 Level096 诊断，输出仅含工程既有 warning。
- 只读检查当前 Unity `Editor.log`：已记录 `PasswordPopup_96_3316.prefab` 和 `Level_096.prefab` 由 PrefabImporter 成功导入；最近 12000 行没有 C# error 或 Missing Script。

## 未验证项

- 未操作 Unity 界面；需在 PlayMode 验证进入关卡、设置页打开/关闭、其它暂停来源不显示提示、密码错误/成功、重开、退出和通关。
- Unity 日志已确认资源导入，但 Inspector 序列化显示、不同横屏比例下提示位置和遮挡关系尚未人工验证。
- Android/iOS 横屏触控拖拽、四个方块的碰撞手感和占位文字可读性尚未验证。
- Level096 的正式关卡排序仍待策划确定；当前未写入 `LevelSort.xlsx`，普通顺序入口是否可达为 `uncertain`。

## 回滚路径

删除 Level096 本地控制器、密码变体和文档，移除 `Level_096.prefab` 中的控制器、四个拖拽组件与四个占位 Tips，并从 `LevelData.xlsx` 和 `tbleveldata.json` 删除 `10096`。Level006、Level080、共享暂停页和共享密码系统无需回退。
