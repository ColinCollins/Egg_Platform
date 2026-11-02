# Shop2SkinGuideFlow 功能说明

## 需求与背景

- 玩家首次取得 RemoteConfig 指定的免费皮肤后，在 `GetSkinPopup` 点击黄色按钮，不再直接跳到 Skin 页，而是依次引导点击 Gameplay 商店按钮、ShopPanel 的 Skin Toggle、对应 SkinPart Toggle 和对应 `skinId` 的皮肤槽位。
- Gameplay 引导执行期间必须让 `PlayCtrl` 进入 Pause，避免关卡操作与引导 UI 同时响应。
- 所有 GuideFlow 继续由 `GuideFlowManager` 串行控制，新的流程不能与提示、明信片等现有引导重叠执行。

## 影响文件、资源与系统

- `Shop2SkinGuideFlow.cs`：首次皮肤引导资格、流程编排、Pause、存档和取消收尾。
- `GetSkinPopup.cs`：黄色按钮优先调用 `Shop2SkinGuideFlow.TryStart(...)`，不符合资格时保留原 `ShopPanel.CreateSkin(...)` 跳转。
- `GamePlayPanel.cs`：只读暴露 Shop 按钮的 `RectTransform`，关闭时取消依赖当前 Panel 的流程；未修改生成绑定文件。
- `PlayCtrl.cs`：重置、重玩、切关、销毁时调用 Flow 的集中取消入口。
- `ShopPanel.cs` / `ShopPanelSwitchGroup.cs`：提供 Skin Toggle 目标、局部切页入口和通用 ClickBlock 控制，ShopPanel 关闭时取消剩余步骤并解除本流程持有的拦截。
- `ShopPanel_SkinPart.cs` / `InfiniteScrollView.cs`：按动态目录中的 `skinId` 聚焦虚拟列表槽位，并在点击完成后重新校验、复用已有穿戴逻辑。
- `GameData.cs` / `GameData_Partial.cs`：序列化字段 `hasShopToSkinGuide` 及其受控属性。
- `ShopPanel.prefab`：本次没有主动修改；静态证据确认已有 Skin Toggle、SkinPart、默认 inactive 的全屏 ClickBlock 和有限三列虚拟列表绑定。

## 原行为基线

- `GetSkinPopup` 黄色按钮关闭弹窗后直接调用 `ShopPanel.CreateSkin(skinData.SkinPart)`。
- `GetSkinPopup.Create(data)` 在弹窗创建时已经通过 Bag 授予皮肤；黄色按钮负责导航，不负责再次发奖。
- ShopPanel Skin 目录包含 Restore 行，并按拥有状态、品质和配置来源动态排序，不能用固定列表下标定位免费皮肤。
- `SkinItem` 由 `InfiniteScrollView` 复用，持有某个槽位引用跨滚动或刷新后可能指向其它皮肤。

## 新行为

1. `Shop2SkinGuideFlow.TryStart(acquiredSkin)` 校验弹窗皮肤 id 与 `ConfigManager.RemoteConfig.GetFreeSkinId()` 一致、皮肤已拥有、存档未触发、当前 `CurrentGamePlayPanel` 与 Shop 按钮可用。
2. Flow 不接收 GamePlayPanel 构造参数；取得 Manager 执行权后重新读取 `PlayCtrl.Instance.CurrentGamePlayPanel`，避免排队期间使用旧 Panel。
3. 首次正式执行时写入 `HasShopToSkinGuide = true` 并调用 `DB.SaveGameData()`，随后通过既有 `SwitchGameStateEvent` 切到 Pause。
4. 第一枚 GuideFinger 指向当前 Gameplay Shop 按钮；点击遮罩后创建 `ShopPanel.Create(true, currentPanelType)`。
5. ShopPanel 创建后由 Flow 调用通用 `SetClickBlocked(true)` 开启根节点 `ClickBlock`，让出一个 `Update` 后读取 Skin Toggle 子节点 `CommonRedDot` 的世界坐标并创建第二枚 GuideFinger。缺少红点时回退到 Skin Toggle 自身。
6. 第二枚 GuideFinger 点击后复用 `ShopPanelSwitchGroup.SelectSkin()` 切换到 Skin 大页，再等待 1.5 秒非缩放时间并让出到 `LastPostLateUpdate`；部位布局稳定后，第三枚 GuideFinger 根据目标皮肤的 `SkinPart` 指向对应部位 Toggle。ClickBlock 在 ShopPanel 内的其余引导步骤保持开启，只允许更高排序层级的 GuideFinger 接收点击。
7. 第三枚 GuideFinger 点击后调用既有 `SelectSkinPart(...)` 切换部位筛选。
8. `ShopPanel_SkinPart` 重新构建目录，按 `skinId` 查找真实行号；`InfiniteScrollView.TryFocusItem(...)` 将对应虚拟槽位移到可见范围。
9. 第四枚 GuideFinger 指向该槽位；点击后再次校验当前绑定项的 id 和拥有状态，再复用 `OnSkinItemClicked(...)` 完成穿戴、存档和新皮肤红点清理。
10. Flow 完成、取消或失败时统一关闭 ClickBlock；ShopPanel 的 `OnOpen` / `OnClose` 也会复位拦截，避免 UIManager 复用实例时继承旧状态。Flow 完成后 ShopPanel 保持打开且游戏保持 Pause，玩家关闭时由既有 `needResumeGame` 路径恢复 Playing。
11. 资格或依赖不满足时，GetSkinPopup 仍使用原 `ShopPanel.CreateSkin(...)` 行为，避免黄色按钮失效。

## 数据、资源、UI、事件与分析流

- RemoteConfig：只读取一次免费皮肤 id，Flow 内保存字符串快照；执行时从当前 `TbSkin` 重新解析目标配置。
- 存档：新增首次触发布尔属性；目标皮肤穿戴继续走现有 SkinPart 保存路径，不重复授予皮肤。
- UI：使用 Bear `UIManager` 的既有 GetSkinPopup 销毁和 ShopPanel 创建路径；没有直接实例化业务 UI。
- 事件：只复用现有 `SwitchGameStateEvent` 切换 Pause/Playing，没有新增 `PlayEvents` 或其它全局事件。
- 资源：没有新增资源加载或资源路径；GuideFinger 和皮肤图标/预览继续沿用现有资源系统。
- 分析：`ShopPanel.Create(...)` 仍执行既有 `IAP_IMP(lastUIName)`，没有新增或移除分析事件。

## 生命周期与失败边界

- 相同 Flow 已经活动或排队时，`TryStart(...)` 视为已接受，避免 GetSkinPopup 额外打开普通 Skin 页。
- 排队期间 GamePlayPanel 关闭、PlayCtrl 重置/重玩/切关/销毁都能取消流程。
- ShopPanel 打开前取消或失败，并且流程原本从 Playing 切入 Pause 时，会恢复 Playing。
- ShopPanel 打开后失败或取消不会由 Flow 抢先恢复 Playing；界面恢复普通交互，关闭 ShopPanel 时再走既有恢复路径。
- ShopPanel 引导开始后若缺少 ClickBlock，Flow 直接失败并进入统一收尾，不在无法隔离商店原交互时继续；正常打开和关闭 ShopPanel 都会主动把 ClickBlock 恢复为 inactive。
- 触发标记在第一步正式显示前写入；后续 Shop 依赖缺失时本次引导仍视为已消费，避免每次进入重复失败。

## 验证记录

- 2026-07-21：增加 Skin 大页切换后的部位定位等待和 ClickBlock 控制后，`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，结果为 `0 error / 99 warning`；warning 均为当前工程既有诊断。
- CodeGraph 确认 GetSkinPopup、Flow、ShopPanel、SkinPart 和 InfiniteScrollView 的新增调用链，并确认三个现有 GuideFlow 没有代码差异。
- Prefab 静态检查确认 `ShopPanel.prefab` 已绑定 Shop/Skin Toggle、Skin Toggle 子节点 `CommonRedDot`、四个 SkinPart Toggle、ShopPart、SkinPart、ScrollRect、Content 和三列有限虚拟列表配置；该红点在当前 prefab 中相对 Skin Toggle 的位置为 `(116, 26)`。
- Prefab 静态检查确认 ShopPanel 的 `UIScaleAnimation` 对内容根执行 `0.3s`、`0.8 -> 1` 的非缩放时间开场动画；根节点 `ClickBlock` 默认 inactive、全屏拉伸且 `raycastTarget=true`，GuideFinger 自身 Canvas 排序高于 ShopPanel。
- `git diff --check` 通过；新增代码未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、PlayerPrefs、直接 Instantiate 或新 PlayEvents。
- Unity Editor 最近日志片段未发现 `error CS` 或 Shop2SkinGuideFlow 异常记录。

## 未验证风险

- 尚未执行 Unity PlayMode 全流程点击；Skin 大页切换后等待 1.5 秒的目标 SkinPart 定位、CommonRedDot 定位、ClickBlock 对原商店交互的拦截、GuideFinger 可点击性、四个目标位置和最终穿戴需要在 Editor 中确认。
- 虚拟列表自动定位的滚动距离、末行目标可见性和不同分辨率布局尚未经过实际 Canvas 验证。
- GuideFinger 的独立点击区消费点击后由 Flow 调用对应业务方法，不会把该 PointerEvent 透传给底层按钮；实际点击音效和视觉反馈需要 PlayMode 确认。
- Android/iOS 横屏、安全区和真机触摸未验证。
- 当前工作区中的 `ShopPanel.prefab` 另有用户改动；本次提交只纳入 ClickBlock 根节点、序列化绑定和默认 inactive 实例三个相关区块，其余改动保持未暂存。

## 回滚方式

1. 删除 `Shop2SkinGuideFlow.cs` 及 `.meta`。
2. 恢复 `GetSkinPopup.OpenSkinPanel(...)` 为直接调用 `ShopPanel.CreateSkin(...)`。
3. 移除 GamePlayPanel、PlayCtrl、ShopPanel、ShopPanelSwitchGroup、ShopPanel_SkinPart 和 InfiniteScrollView 的局部引导 API 与取消调用。
4. 移除 `GameData_Partial.HasShopToSkinGuide`；如完全废弃该需求，再移除 `GameData.cs` 的序列化字段。
5. 不需要回滚皮肤资源、ShopPanel prefab、配置表或现有 GuideFlowManager。
6. 如只回滚本次定位修正，移除 `SkinPartGuideWaitMilliseconds`、Skin 大页切换后的非缩放等待及 Flow 对 `SetClickBlocked(...)` 的调用即可；通用 ClickBlock API 可保留供其它 ShopPanel 业务使用。
