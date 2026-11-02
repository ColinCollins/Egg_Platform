# StartPanel 主线星星进度功能说明

## 需求与修改前基线

- `StartPanel/ui_zhuye_01_txt` 展示当前主线已经永久获得的星星数量。
- `StartPanel/ui_zhuye_02_txt` 以 `/{总数}` 展示当前最终主线关卡序列的可获得星星总数。
- 修改前两个 TMP 已由 `StartPanel.Generated.cs` 和 `StartPanel.prefab` 正确绑定，但 `StartPanel` 没有刷新代码，运行时只保留 prefab 占位值。

## 影响文件与修改后行为

- 运行时代码：`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`。
- 序列化字段：复用 `UiZhuye01Txt`、`UiZhuye02Txt`；本次不修改 `StartPanel.prefab`、字体、旋转、布局或生成脚本。
- `StartPanel.OnOpen()` 每次打开或复用首页时遍历当前 `PlayCtrl.Level.LevelSorts`，按唯一有效 `LevelData.Id` 统计：
  - 总数读取每关 `LevelSumStarCount`，并按现有主线星星规则夹取到 `0～3`。
  - 已获得数量读取 `DB.Postcard.LevelCollectedStar`，单关只统计唯一有效星星 Id `1～3`，并不超过该关配置总数。
  - 无星星关卡、无效配置、重复排序和旧存档残留不会计入。
- 配置或控制器未就绪时安全显示 `0`、`/0` 并记录 Bear Logger 告警；不触发配置初始化、存档写入、事件或逐帧刷新。

## 验证、剩余风险与回滚

- CodeGraph 确认调用链为 `StartPanel.OnOpen() -> RefreshMainLevelStarProgress() -> SetMainLevelStarProgress()`。
- 当前 `tblevelsort.json` 与 `tbleveldata.json` 静态计算为 106 个排序项、104 个有星星关卡、总计 312 星。
- `UiZhuye01Txt`、`UiZhuye02Txt` 在当前 `StartPanel.prefab` 中均为非空 fileID；C# 外部构建通过：`0 error / 118 warning`。
- `uncertain`：Unity PlayMode 的清档、部分收集、全部收集、返回首页刷新，以及 Android/iOS 横屏文本宽度尚未目测。
- 回滚时删除 `OnOpen()` 的主线星星刷新调用和 `Main Level Star Progress` 区域；prefab、配置和存档无需回滚。
