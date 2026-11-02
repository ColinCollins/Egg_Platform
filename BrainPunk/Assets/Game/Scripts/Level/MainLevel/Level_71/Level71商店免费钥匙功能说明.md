# Level71 商店免费钥匙功能说明

## 需求与本次范围

- 需求依据：飞书《购物计划》及用户补充确认。
- Level71 的 ShopPanel 中立即显示免费钥匙；其它关卡或非关卡暂停商店不显示。
- 点击免费钥匙后，商店内钥匙商品向右上飞走并渐隐，动画完成后关闭 ShopPanel。
- 同一关卡尝试只能领取一次；Reset、Replay 或重新进入 Level71 后恢复未领取状态。
- 商店入口、领取状态、UI 动画和关闭链由本组件负责；实体钥匙投放、拾取及开锁由 `Level71Ctrl` 与标准 `Key.prefab` 负责。实体钥匙会等待 ShopPanel 完全关闭后再延迟 1 秒出现，仍与睡觉/昼夜循环保持无逻辑依赖。

## 影响文件与职责

- `Level71ShopBtnCtrl.cs`
  - 仅在 `CurrentLevelData.Path == "Level_071"`、当前主线关卡实例存在、GameplayPanel 存在且状态为 `PAUSE` 时显示 `L71_Obj`。
  - 关卡身份不使用 `CurrentLevel`：该字段实际是可随排序表调整的 `LevelSort.Id`，必须沿用项目加载链中的 `LevelData.Path`识别真实关卡。
  - 通过 `IPointerClickHandler` 接收 `L71_Obj` 子节点射线点击，无需把免费钥匙加入 `ShopPanel.items` 或走 IAP/广告商品配置。
  - 点击后记录当前 `BaseLevelCtrl` 为已领取尝试所有者，锁定 ShopPanel 输入，并播放未缩放时间动画。
  - `HasClick` 只对当前存活的 Level71 关卡实例返回 true，供后续实体钥匙逻辑读取。
- `ShopPanel.cs`
  - 将原关闭按钮逻辑收口到 `TryClosePanel()`；Level71 动画完成后复用同一入口。
  - 继续执行 `UIManager.DestroyUI()`、`IAP_CLOSE(lastUIName)` 和 `ShopPanel.OnClose()` 的暂停恢复、引导清理链。
- `ShopPanel.prefab`
  - 本次代码依赖当前工作区已经存在的 `L71_Obj/Level71ShopBtnCtrl` 接线及其全尺寸 `clickArea` 射线节点。
  - 本次没有改写该 Prefab 的现有本地改动；组件序列化字段为空时由脚本使用当前根节点并在运行态补充 `CanvasGroup`。

## 行为前后对比

### 修改前

- `Level71ShopBtnCtrl` 只有空的 `Start()` / `Update()`，未控制显示、领取、动画或关闭。
- 原占位脚本的类注释已明确要求依据 `LevelPath == Level_071` 判断；初次实现却误改为 `CurrentLevel == 71`，属于实现偏离既定约束。
- `HasClick` 为未赋值的公共静态字段，没有与 Reset/Replay 生命周期绑定。

### 修改后

1. ShopPanel 创建时，脚本核对 Level71 与暂停上下文；不符合时隐藏整个 `L71_Obj`。
2. 当前关卡实例未领取时显示免费钥匙；同一实例领取后再次打开 ShopPanel 时隐藏。
3. 首次有效点击立即关闭自身射线并开启 ShopPanel `ClickBlock`，防止动画期间重复操作。
4. `L71_Obj` 在 `0.45s` 内按 `anchoredPosition + (240, 180)` 向右上飞出，同时线性渐隐到 0；DOTween 使用 `SetUpdate(true)`，不受 Gameplay 暂停影响。
5. 动画完成后调用 `ShopPanel.TryClosePanel()`，沿原有关闭链恢复 Gameplay。
6. Reset/Replay 会由 `PlayCtrl` 销毁并重建 `LevelCtrl`；`HasClick` 比较当前关卡实例引用，因此新实例自动回到未领取状态，不新增 Reset 事件或存档字段。

### 2026-08-07 LevelPath 判断修正

- 修正前误用 `CurrentLevel == 71` 识别 Level71，但 `CurrentLevel` 是 `LevelSort.Id`，当前 `tblevelsort.json` 中 `Level_071` 对应的排序 ID 为 `91`，导致 `RefreshVisibility()` 把 `L71_Obj` 关闭。
- 修正后显示与 `HasClick` 统一使用 `PlayCtrl.Level.CurrentLevelData.Path == "Level_071"`，与 `PlayCtrl.CreateLevelAsync(CurrentLevelData.Path)` 的实际关卡加载身份保持一致。
- 本次不修改 `tblevelsort.json`、`tbleveldata.json`、ShopPanel Prefab 或其它关卡。

## 数据、资源、UI 与事件流

```text
Level71 Gameplay 点击 HUD Shop
  -> PlayCtrl 进入 PAUSE
  -> ShopPanel 创建
  -> Level71ShopBtnCtrl 显示 L71_Obj
  -> 点击免费钥匙
  -> 当前 LevelCtrl 记为 claimedAttemptOwner
  -> ShopPanel ClickBlock 开启
  -> L71_Obj 右上飞走 + 渐隐（未缩放时间）
  -> ShopPanel.TryClosePanel()
  -> UIManager.DestroyUI + IAP_CLOSE
  -> ShopPanel.OnClose 恢复 Gameplay
```

- 数据：只保存运行时静态关卡实例引用，不访问 DB、Save、RemoteConfig 或 Luban。
- 资源：复用当前 ShopPanel 免费钥匙 UI，不新增资源加载。
- 事件：不新增或订阅全局 `PlayEvents`；Reset/Replay 复用既有关卡销毁重建边界。
- 平台：动画依赖 Unity UI 与 DOTween 未缩放时间，Android/iOS 横屏共用同一代码路径。

## 验证

- 2026-08-07 定向静态核对：`tblevelsort.json` 当前以 `id: 91 -> path: 10071`映射到 `tbleveldata.json` 的 `10071 -> Level_071`；代码已不再使用排序 ID 识别本关。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo`：`0 error / 101 个工程既有 warning`。
- `git diff --check`：目标 C# 文件无空白错误。
- 静态核对：`ShopPanel.prefab` 的 `L71_Obj` 已挂 `Level71ShopBtnCtrl`，`clickArea` 为根节点全尺寸子节点且 `Image.raycastTarget = true`。
- `uncertain`：尚未执行 Unity PlayMode 点击、动画观感、ShopPanel 实际恢复 Gameplay，以及 Reset/Replay 后重新显示的动态验证。

## 实体钥匙交接

- `Level71Ctrl` 已消费 `HasClick`：免费商品领取后，先等待 `UIManager` 中不再存在 `ShopPanel`，再以未缩放时间延迟 `keySpawnDelay`（默认 1 秒）。
- 实体钥匙不再由代码覆盖位置，直接从 `Level_071.prefab` 中策划摆放并保存的初始局部位置激活；后续调整飞出位置只需移动并保存该 Key 实例。
- 钥匙激活时以 `Ease.OutCubic` 在 `keyScaleDuration`（默认 0.3 秒）内从 `scale 0` 缓动到 `scale 1`，同时按 DirectMove 当前初速度 `(-2, 0)`、加速度 `(0, -15)` 下落。
- 钥匙落地后悬浮，拾取后启用 `Level_071.prefab/door_0` 的标准出口碰撞体；完整说明见 `Level71关卡功能说明.md`。

## 剩余风险与回滚

- 实体钥匙等待 ShopPanel 关闭、1 秒延迟、下落、拾取和开锁已完成静态接线，但 Unity PlayMode 动态验证仍为 `uncertain`。
- 若美术需要调整飞行方向或速度，可在 `L71_Obj` 组件中覆盖 `flyOffset`、`flyDuration`、`flyEase`，无需改共享 ShopPanel。
- 回滚时可恢复 `Level71ShopBtnCtrl.cs` 空占位实现，并把 `ShopPanel.Close()` 恢复为直接执行原关闭代码；Prefab 的现有本地接线属于本次开始前改动，不应随代码回滚自动删除。

## 2026-08-10：实体钥匙交接位置更新

> 本节覆盖上文“实体钥匙直接使用 Prefab 固定初始局部位置”的历史交接说明；ShopPanel 内免费商品的显示、点击、动画、关闭和同尝试防重复行为没有变化。

- `Level71Ctrl` 仍等待 ShopPanel 完全销毁并延迟 `keySpawnDelay = 1s`，但激活 Key 前会按当帧本关 GameCamera 计算视口坐标，只把横向值设置为 `keySpawnViewportX = 0.5`。
- Key Prefab 当前世界 Y/Z 保持不变，因此策划仍通过场景实例调整掉落高度；不再通过摆放绝对 X 决定在哪一屏出现。
- 镜头若已在角色到达首屏 `2/3` 后开始跟随，钥匙会落在跟随后的当前屏幕中心；尚未触发跟随时则落在首屏中心。
- DirectMove `(-2, 0)/(0, -15)`、缩放、Ground 停止、悬浮、拾取和开门均未修改，所以最终落地点会相对屏幕中心生成点略向左。
- 静态编译为 `0 error`，Prefab fileID/本地引用检查无缺失，`git diff --check` 通过；不同相机位置领取、Reset/Replay、落地可达和 Android/iOS 横屏仍需 PlayMode/真机验证。
- 只撤回该更新时，删除 `PositionKeyAtCurrentCameraViewport` 调用与 `keySpawnViewportX` Prefab 字段即可；不要回滚免费商品 UI，也不要覆盖用户调整的 Level071 场景坐标。

## 2026-08-10：领取关闭不弹提示与右上出口交接

> 本节覆盖上文“场景钥匙从当前屏幕中心生成”的历史说明；中心位置现在只在 UI 出口采集失败时使用。

- Level071 免费钥匙点击通过校验后，`Level71ShopBtnCtrl` 立即调用当前 `ShopPanel.SuppressRewardTipAutoShowOnClose()`；因此动画结束关闭商店时不会再创建截图中的 `ShopRewardTipPopup`。
- 抑制仅绑定这次有效领取和当前 ShopPanel 生命周期。普通关闭 Level071 商店不会抑制，所有其它关卡调用仍保持默认；`OnOpen()` 会复位字段，且跳过时不占用每日提示展示次数。
- `L71_Obj` 仍按 Prefab 的 `(240, 180) / 0.45s / Ease.InQuad` 向右上飞走。动画完成且面板尚未销毁时，组件从根 Canvas 取得飞出终点的实际屏幕像素坐标，并与当前领取的 `BaseLevelCtrl` 实例一起交给 `Level71Ctrl`。
- 场景 Key 在 ShopPanel 销毁并延迟 1 秒后，以生成当帧相机的 `ScreenToWorldPoint` 把记录的 X/Y 换算到 Key 原 Z 平面，然后继续原缩放和 DirectMove；即使 GameCamera 在 4:3 等画幅使用非全屏 `pixelRect`，也不会把整屏比例误当成相机 viewport。
- 采集异常时只输出 warning，回退到 `keySpawnViewportX = 0.5` 和 Prefab Y/Z；不会阻止商店关闭或钥匙生成。
- 本次修改 `Level71ShopBtnCtrl.cs`、`Level71Ctrl.cs`、`ShopPanel.cs` 及说明文档，没有改两个 Prefab、配置、存档、广告发奖、IAP 打点、其它关卡或场景 Transform。
- Bee/Roslyn 定向编译通过：`0 error`，仅工程既有 warning。静态检查确认弹窗抑制调用只有 Level071 一处；Unity PlayMode、不同横屏比例、Reset/Replay 和完整过关仍为 `uncertain`。
- 回滚时删除本次抑制调用/API与出口坐标缓存，让 `PositionKeyAtCurrentCameraViewport` 永久走中心回退即可；保留此前镜头跟随、用户布局和 Key 运动参数。

## 2026-08-10：Gameplay 购物篮入口与商店前定点落地

> 本节覆盖上文“使用 `L71_Obj` 飞出终点”和“继续原 DirectMove 落地”的历史说明。ShopPanel 内商品动画只负责领取反馈，不再决定场景 Key 的起点；领取后不弹 `ShopRewardTipPopup` 的 Level071 隔离逻辑不变。

- 起点绑定当前 Gameplay HUD 的 `Shop2SkinGuideTarget`，取按钮 Rect 内 `(0.2, 0.4)`，对应截图购物篮左侧偏下。HUD/Canvas 无法读取时回退到相机视口 `(0.915, 0.875)`；两个值均为 `Level_071.prefab` 上的本关参数。
- 目标为当前 GameCamera 视口 `(0.59, 0.35)`，对应截图粉色标记和商店前地面。正常路径以 `1.1s` 二次贝塞尔轨迹飞行，弧高为 `1` 世界单位，原 `keyScaleDuration = 0.3s` 保留。
- 起终点在飞行每帧随当前相机重新投影，避免本关延迟镜头跟随在飞行中改变最终屏幕落点。轨迹结束后 Key 固定为场景实体，进入悬浮、拾取和开门链。
- 飞行中 Collider、DirectMove、MoveFloatHandle 均关闭；完成后恢复悬浮和 Collider。只有本关相机或 Key 深度不可用时才启用原 DirectMove 回退，不修改共享 Key/DirectMove Prefab。
- 修改范围为 `Level71Ctrl.cs`、`Level_071.prefab` 及本地文档；`Level71ShopBtnCtrl.cs` 仅保留领取动画、同尝试防重和本关弹窗抑制，没有新增存档、配置、资源加载、全局事件或其它关卡分支。
- Unity 6000.0.59f2 Roslyn 编译通过：`exit 0 / 0 error`；截图、HUD Rect 与 Collider 高度已静态校准，Prefab 为 `183` 个对象、`0` 个重复 fileID、`0` 个悬空本地引用，`git diff --check` 通过。未操作 Unity 界面，PlayMode、不同画幅、移动端、Reset/Replay 和完整过关仍为 `uncertain`。
- 回滚时删除五个定位/轨迹参数与本关贝塞尔换算方法，恢复实体 Key 正常启用 DirectMove；不得回滚用户场景 Transform、镜头跟随、免费商品 UI 或弹窗抑制。

## 2026-08-10：场景钥匙飞行缓动与受限镜头适配

- 场景 Key 的定点贝塞尔进度不再固定使用 `Ease.Linear`；新增 Inspector 参数 `keyFlightEase`，`Level_071.prefab` 默认 `Ease.OutQuad`（序列化值 `6`）。该变更只调整飞行速度分布，不改变 HUD 起点、商店前落点、`1.1s` 时长、弧高 `1`、缩放、Collider/悬浮、拾取和开门链。
- 飞行仍在每帧按当前 GameCamera 重投影视口起终点。镜头到达黄色 Area 的动态右限后，钥匙仍保持配置的 viewport 起落点，不会切回绝对世界坐标或重新启用 DirectMove。
- 商店领取、领取后弹窗抑制、同尝试防重、关店后延迟 `1s` 和异常 DirectMove 回退均未修改；没有改共享 UI、Key Prefab、存档、配置、资源、事件或其它关卡。
- Unity Roslyn 编译通过：`0 error / 104 个工程既有 warning`，Level71 诊断为 `0`；Ease 字段在代码与 Prefab 各一处，`git diff --check` 通过。未操作 Unity 界面，OutQuad 手感、不同横屏比例、镜头触边和完整领取/过关链仍为 `uncertain`。
- 回滚本节只需把 Tween 恢复为 `Ease.Linear` 并移除 `keyFlightEase` 字段；保留已确认的定点轨迹和黄色 Area 镜头边界。
