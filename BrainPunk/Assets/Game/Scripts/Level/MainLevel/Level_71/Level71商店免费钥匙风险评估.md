# Level71 商店免费钥匙风险评估

## 原有行为基线

- `ShopPanel.prefab` 已存在 Level71 免费钥匙视觉节点和本地组件接线，但 `Level71ShopBtnCtrl` 为空实现。
- `Level71ShopBtnCtrl` 原类注释已规定使用 `LevelPath == Level_071` 判断关卡；修正前的实现误用 `CurrentLevel == 71`，与既定约束和实际加载链都不一致。
- 免费钥匙不会按关卡上下文隐藏、不能领取、没有动画，也没有关闭 ShopPanel 或防重复状态。
- ShopPanel 关闭按钮原本在私有 `Close(CustomButton)` 内直接检查忙碌态、销毁面板并发送 `IAP_CLOSE`。

## 影响文件、资源与隔离边界

- 直接修改：`Level71ShopBtnCtrl.cs`、`ShopPanel.cs`、`Level71Ctrl.cs`、`Level_071.prefab`、本目录 Level71 中文文档、`DevelopMD/ShopPanel_ShopItem_Design.md`、`DevelopMD/已有功能设计与使用记录.md`。
- 依赖但不改写：当前工作区的 `ShopPanel.prefab` 本地改动、`PlayCtrl.LevelCtrl` 重建流程、`ShopPanel.ClickBlock`、DOTween、Unity EventSystem。
- 不修改：Actor、睡觉/昼夜循环、实体钥匙 Prefab 资源、其它关卡、DB/Save、配置、资源地址、全局事件、广告或购买发奖逻辑。
- 权威生命周期核对：本功能不改变 `level_start`、`level_end`、revive、Reset 或 Replay 打点顺序；只把免费钥匙运行态绑定到既有 `LevelCtrl` 重建边界，与 `DevelopMD/关卡打点生命周期约定.md` 无冲突。

## 变更入口到影响链路

```text
Level71ShopBtnCtrl 点击
  -> ShopPanel ClickBlock
  -> DOTween 未缩放 UI 动画
  -> ShopPanel.TryClosePanel
  -> UIManager.DestroyUI
  -> ShopPanel.OnClose
  -> 主线 PAUSE 恢复 PLAYING
```

## 风险项

1. **P1：关卡身份误用排序 ID（已修正）**
   - 修正前 `Level71ShopBtnCtrl` 使用 `CurrentLevel == 71`，但 `LevelCtrl.CurrentLevel` 实际是 `LevelSort.Id`；当前配置中 `Level_071` 对应排序 ID `91`，所以免费钥匙节点会被关闭。
   - 显示判断与 `HasClick` 已统一改为 `CurrentLevelData.Path == "Level_071"`，避免排序表调整导致本关功能失效。

2. **P1：实体钥匙流程已接入但尚未动态验证**
   - `HasClick` 仍只表示当前关卡实例已经点击免费钥匙；`Level71Ctrl` 据此启动协程，等待 ShopPanel 实例关闭后再以未缩放时间延迟 1 秒，最后激活标准 `Key.prefab`。
   - 下落起点直接使用 Key Prefab 实例保存的初始局部位置，代码不再按相机覆盖或缓存具体坐标；等待协程属于当前关卡实例，Reset/Replay/退出销毁该实例时会取消，不会跨尝试生成。
   - 激活时钥匙根节点在默认 0.3 秒内从 `scale 0` 缓动到 `scale 1`，并同时以 DirectMove `(-2, 0)/(0, -15)` 下落；固定初始位置、Ground 触发、缩放与位移组合观感和可拾取性仍需 PlayMode/实机验证。
   - ShopPanel 销毁边界、1 秒延迟观感、钥匙下落、Ground 层停下、角色拾取、启用出口碰撞体和进门仍需 Unity PlayMode 完整验证。
3. **P1：PlayMode 接线与点击冒泡未动态验证**
   - 静态证据显示 `clickArea` 是 `L71_Obj` 全尺寸子节点且可接收射线，Unity EventSystem 会向父节点查找 `IPointerClickHandler`。
   - 仍需在 Unity PlayMode 确认当前 Canvas、GraphicRaycaster 和遮挡顺序下点击确实到达本组件。
4. **P1：ShopPanel 暂停恢复链需要动态验证**
   - `TryClosePanel()` 复用原 `DestroyUI + IAP_CLOSE`，并由 `ShopPanel.OnClose()` 恢复 Gameplay；静态调用链保持一致。
   - 仍需验证动画期间 `ClickBlock` 开启、动画结束关闭面板、关闭后输入恢复，且不会闪现其它商品状态。
5. **P1：Reset/Replay 自动恢复依赖关卡实例重建**
   - `PlayCtrl.ResetGame()` 和 `ReplayGame()` 当前都会 `DestroyLevel()` 后 `CreateLevelAsync()`；新 `BaseLevelCtrl` 引用与已领取引用不同，因此状态恢复。
   - 如果未来改为同实例软重置，需要同步为 Level71 本地重置 API；当前实现不应在没有证据时新增全局事件。
6. **P2：动画参数与横屏画幅**
   - 默认向右上 `(240, 180)` 飞出并在 `0.45s` 渐隐，功能正确但最终观感尚未由美术/策划在不同横屏安全区验收。
   - 参数均可在组件 Inspector 局部覆盖，不影响其它商店商品。
7. **P2：共享 ShopPanel 关闭入口回归**
   - 原关闭按钮改为调用 `TryClosePanel()`，忙碌门禁、销毁、IAP 关闭打点的执行内容和顺序保持不变。
   - 新增的 `UIManager` 空检查只在管理器不可用时返回 false；正常运行路径无行为差异，仍建议抽查普通 ShopPanel 关闭。

## 已完成验证

- 配置与代码静态核对：`LevelSort.Id 91 -> LevelData 10071 -> Level_071`，显示和领取状态判断均使用最终 `LevelData.Path`。
- C# 完整定向构建：`0 error / 101 个工程既有 warning`。
- 目标差异格式检查：通过。
- CodeGraph/源码核对：Reset 与 Replay 均销毁并重建关卡实例；ShopPanel 关闭继续进入 `OnClose()` 清理与恢复链。
- Prefab 静态核对：`L71_Obj` 当前已挂本脚本，`clickArea` 为其子节点并开启射线。

## 未验证缺口

- `uncertain`：Unity PlayMode 中 Level71/非 Level71 显隐、点击命中、右上飞出渐隐、面板关闭、暂停恢复和二次打开防重复。
- `uncertain`：手动 Reset、失败重试、VictoryPanel Replay 后免费钥匙重新出现。
- `uncertain`：Android/iOS 横屏、不同分辨率和多点触控下的动画与输入锁表现。
- `uncertain`：实体钥匙对 `HasClick` 的消费、等待 ShopPanel 关闭、1 秒延迟、从 Prefab 初始位置进行 `scale 0 -> 1` 动画、`(-2, 0)/(0, -15)` 落下、落地悬浮、拾取与出口解锁的实际运行表现。

## 回滚路径

1. 恢复 `Level71ShopBtnCtrl.cs` 的原空占位内容。
2. 将 `ShopPanel.Close(CustomButton)` 恢复为直接执行忙碌检查、`DestroyUI(this)` 和 `IAP_CLOSE(lastUIName)`，删除 `TryClosePanel()`。
3. 删除本目录两份新增文档及其 `.meta`，移除 `DevelopMD/已有功能设计与使用记录.md` 对应记录。
4. 不自动回滚 `ShopPanel.prefab`，因为组件接线是本次开始前已经存在的用户工作区改动。

## 2026-08-10：当前相机中心生成风险补充

> 当前有效行为已由“固定 Prefab X”改为“生成当帧的当前相机视口中心 X”；上文对应固定位置风险描述仅作为历史保留。

- **已处理：跨场景/画幅仍沿用旧世界 X。** `Level71Ctrl` 在实体 Key 激活前，把现有 Key 世界位置转换到当前本关 Camera 视口，将 X 设置为 `0.5` 后换回世界坐标，并恢复原 Y/Z；无需修改 `Level71ShopBtnCtrl` 或共享 ShopPanel。
- **已处理：相机引用异常导致领取链空引用。** Prefab 显式绑定 GameCamera，同时保留本关子层级和 `Camera.main` 兜底；仍无法解析时输出 warning 并使用 Prefab 位置继续生成。
- **P1 / `uncertain`：原水平初速使最终落点偏左。** 当前需求按屏幕中心设置的是生成点；DirectMove 的 X 初速 `-2` 保持不变。如验收要求最终接地点正好居中，需要另行确认是否允许改变既有抛落轨迹。
- **P1 / `uncertain`：相机在领取期间移动。** 位置以 ShopPanel 销毁并完成 1 秒延迟后的生成当帧为准，符合“当前屏幕”语义；仍需 PlayMode 验证暂停恢复、角色移动和镜头 SmoothDamp 的实际时序。
- Bee/Roslyn 编译为 `0 error`；`Level_071.prefab` 的 183 个 YAML fileID 唯一且无缺失本地引用；PlayMode、Reset/Replay、不同横屏画幅和移动端仍未验证。
- 回滚只需移除钥匙视口换算及 `keySpawnViewportX` 序列化字段；免费商品显示/动画/关闭与用户场景布局均不属于该回滚范围。

## 2026-08-10：奖励提示抑制与 UI 出口坐标风险补充

> 当前交接点由“相机中心 X”改为“领取动画结束时的实际屏幕像素 X/Y”，中心 X/Prefab Y/Z 只保留为异常回退。

- **已隔离：Level071 领取后弹出 `ShopRewardTipPopup`。** 只有有效免费钥匙点击调用当前 ShopPanel 实例的抑制入口；共享 Close/`TryClosePanel()`、普通 Level071 关店、其它关卡、广告与购买发奖链未改。`OnOpen()` 复位状态，跳过不消耗每日展示次数。
- **已处理：动画与场景钥匙断点。** 坐标在 DOTween 完成且 ShopPanel 未销毁时采集，经根 Canvas 转为实际屏幕像素；实体 Key 延迟生成时再用当前 Level071 相机的 `ScreenToWorldPoint` 投影到原 Z 平面，因此不依赖绝对场景位置，也兼容非全屏 Camera `pixelRect`。
- **已处理：异常软回退。** 无 Canvas、Canvas Camera 或有效浮点结果时不缓存出口；`Level71Ctrl` 继续采用 `keySpawnViewportX = 0.5` 并保留 Prefab Y/Z，不中断领取链。
- **P1 / `uncertain`：出口可在相机画面外。** 为保持 UI 动画真实终点，缓存实际屏幕像素；需要 PlayMode 在目标横屏比例确认 Key 从商店右上入口可见地进入场景。
- **P1 / `uncertain`：动画中断路径。** 正常 `OnComplete` 才采集出口；关卡/面板被外部销毁时可能走中心回退。当前关卡协程随实例销毁而取消，但 Reset、快速关闭和低帧率仍需动态验证。
- **P2：共享 ShopPanel 新 API。** 新入口仅设置已有 `_skipRewardTipAutoShowOnClose`，不改 `TryClosePanel()` 签名与原调用；静态调用点只有 `Level71ShopBtnCtrl`。Bee/Roslyn 编译为 `0 error`。
- 本次没有改 Prefab、场景坐标、镜头参数和 DirectMove；回滚只需删除抑制 API/调用及出口缓存/投影分支。

## 2026-08-10：最终起点/落点风险补充

> 当前有效坐标源为 Gameplay 购物篮 Rect 与本关相机视口目标；ShopPanel 商品动画终点、中心 X 和 DirectMove 最终落点均不再是正常路径。

- **已处理：起点偏到 ShopPanel 右侧。** 直接读取 `GamePlayPanel.Shop2SkinGuideTarget`，取按钮内部 `(0.2, 0.4)`；无引用时才使用截图校准的视口回退 `(0.915, 0.875)`。
- **已处理：落点仍在画面右侧。** 正常路径不用 DirectMove 决定位置，硬落点为 `(0.59, 0.35)`，飞行期间 Collider 关闭，Ground 事件不会抢断轨迹。
- **已处理：Camera Follow 期间位置失效。** 贝塞尔起终点每帧由当前 GameCamera viewport 换算到 Key 原 Z 平面，完成帧再次写入目标位置。
- **P1 / `uncertain`：不同画幅的入口可见性。** HUD 是全屏 Canvas，而窄屏 GameCamera 可能调整 `pixelRect`；代码保留按钮真实屏幕点，仍需 PlayMode 验证极端横屏比例下是否从可见边缘进入。
- **P1 / `uncertain`：参数观感。** `(0.2, 0.4)/(0.59, 0.35)`、`1.1s` 和弧高 `1` 来自当前截图与静态几何，尚未由策划在运行画面确认。
- **P2：异常回退不保证定点。** 相机或深度解析失败会记录 warning 并启用既有 DirectMove，以保证钥匙仍可领取；正常 Prefab 已绑定 GameCamera。
- Roslyn 编译为 `exit 0 / 0 error`，Level071 修改文件无诊断；Prefab 为 `183` 个对象、`0` 个重复 fileID、`0` 个悬空本地引用，五个新字段各序列化一次，`git diff --check` 通过。未执行 Unity PlayMode 或 Android/iOS 验证。
- 回滚只移除 `Level71Ctrl` 定点飞行与 Prefab 五个相关字段并恢复 DirectMove 正常路径；保留本关免费领取、弹窗抑制、用户布局和镜头跟随。

## 2026-08-10：OutQuad 手感与镜头触边风险补充

- **已处理：Linear progress 使抛物线速度过于机械。** Prefab 默认改为可配置 `Ease.OutQuad`，前段速度更明确、落点前减速；硬终点、飞行时长、弧高和 Collider 门禁均不变。
- **已处理：飞行期间镜头到达黄色 Area 右限导致落点漂移。** `(0.59, 0.35)` 仍每帧使用受限后的当前相机重投影，完成帧再次写入硬终点。
- **P1 / `uncertain`：最终手感。** OutQuad 与 `0.3s` 缩放的组合、落点前减速幅度、低帧率、极端横屏及镜头恰好触边仍需 PlayMode/真机验收；相机或深度异常时启用的 DirectMove 回退不使用该 Ease。
- Roslyn 编译为 `0 error / 104 个工程既有 warning`，Level71 无诊断；Prefab `183/183/0`，Ease/Area 三字段各序列化一次，`git diff --check` 通过。未操作 Unity 界面。
- 回滚只恢复 `Ease.Linear` 并删除 `keyFlightEase`；不回滚起落点、定点贝塞尔、镜头边界、用户场景布局或领取后弹窗抑制。
