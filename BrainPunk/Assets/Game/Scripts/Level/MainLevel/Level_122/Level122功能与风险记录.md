# Level122 随机密集三消开锁功能与风险记录

## 1. 需求与确认口径

- 玩家靠近场景中央桌子时自动打开游玩面板，画面参考“羊了个羊”的密集随机牌堆与六格暂存区，但不采用上下层解锁规则。
- 三个相同元素自动三消；消除前元素摇晃，随后放大渐隐并播放放射状 UI 粒子反馈。
- 牌堆随机混入六个锁元素；其他元素复用 Level033 转盘奖励 Sprite，且只作为 Level122 的 UI 引用，不修改 Level033。
- 下方六个空方格都可作为拖动起点，但整局只能成功拖出一个。拖动期间方格始终跟随指针，靠近实体锁时仅切为锁后世界 Sprite 绘制；只有松手命中才吸附并计入三消，保留同一个世界方框和屏幕尺寸。实体锁 Transform 不移动，也不生成额外小锁。
- 被方格捕获的实体锁计作一把锁；它与牌堆中任意两把锁顺序无关地凑成三消，不要求清空其他元素。另一条解法是将全部 234 个元素完成三消、清空牌堆和暂存区，此时显示多语言上浮提示，简中为“恭喜完成！过关拉！”。
- 两条成功路径中，实体锁与元素同步摇晃、放大渐隐并出现粒子，不再播放原 DoorLock 的 `unlock` 动画/音效；消失后解锁出口，保持面板与角色输入锁定 1 秒，再播放关闭动画，关闭后恢复角色。仍需走入出口完成正式过关结算。
- 暂存区填满且不存在三消，或牌堆已空但暂存区仍有无法消除的元素时视为死局：先播放 `failed`，再显示多语言上浮提示；保持页面和角色锁定 1.5 秒，随后播放页面关闭动画，关闭完成后派发 `GameResetEvent(Failed)` 重建本关。

## 2. 原始基线与本轮边界

- `Level_122.prefab` 原始内容是 Level115 场景的复制底稿：根节点仍绑定 `Level115Ctrl`，桌子仍绑定 `Level115QuizTrigger`，`gamePlayPanelName` 仍为 `GamePlayPanel_115`。
- 本轮把根控制器和桌子触发器替换为 Level122 私有脚本；保留桌子 Sprite、Actor、StartDoor、右门、DoorLock、背景和基础 HUD Prefab，不修改共享 Level115、Level033、DoorLock 或 `door_0` 源资源。
- `GamePlayPanel_115` 只作为现有移动 HUD 基线继续复用；其专属 Use 按钮默认隐藏且 Level122 不绑定该按钮。Level122 页面由本关独立 `Level122MatchPopup.prefab` 实例化到当前 HUD Canvas 最上层。

## 3. 实现结构

### 3.1 场景控制

- `Level122Ctrl.cs`
  - 桌边自动打开/防重复打开。
  - X 主动关闭后恢复角色控制；角色离开并重新进入桌边范围后才允许再次弹出，避免原地立即重开。
  - 面板期间停止 Actor 并关闭关卡输入；成功后恢复。
  - 持有实体锁吸附 Transform 和出口 Trigger；不再持有/执行共享 DoorLock 解锁句柄。
  - 统一接收特殊三消与全部清空成功；消除后启用出口 Trigger，停留一秒、关闭面板并恢复输入，完成标记防重复触发。
  - 直接在 Ctrl Inspector 字段中配置 12 种开放语言的死局/全部完成文案，按 `DB.GameSetting.CurrentLanguageKeyCode` 选择，英文兜底。
- `Level122TableTrigger.cs`
  - 世界触发范围由 `5.28 × 1.78` 收紧为 `2.8 × 1.6`，贴近桌面本体，仅接受当前 `BaseLevelCtrl.Actor` 的 Player Collider。

### 3.2 面板与牌堆

- `Level122MatchPanel.cs`
  - 从本关 `Resources/Level122MatchPopup.prefab` 实例化居中页面；根节点只有完全透明的输入拦截，不显示黑色半透明遮罩。
  - 页面背景 `Sp_l122_k_01` 配置 `68/68/68/68` Sprite Border，Prefab Image 使用 Sliced；页面中心、牌堆区、六格区和 X 均可直接在 Prefab 内编辑。按试玩红框把页面由 `1080×700` 扩到 `1350×780`，X 与槽位同步外移。
  - 每次本关创建时生成 234 个元素：228 个普通道具（76 组三连）与 6 把锁，全部随机洗牌；总量是上一版 78 个的三倍。
  - 牌堆取消 `18×13` 矩形基准网格和会空出四角的椭圆分布，改为在 `535×214` 四次方超椭圆（圆角矩形）内均匀拒绝采样；三频轻微轮廓扰动和边界收束保留自然外缘，同时把元素散入试玩标出的四个空角。Prefab 中 `RandomPile` 为 `1210×550`、Y=`42`，234 个元素数量不变。所有道具只显示素材图标并采用 `0°～360°` 完整随机角度；牌堆普通道具为 122 像素、锁为 100 像素，进入六格后分别收为 92 与 78 像素。
  - 不保存或判定上下层关系，也不因视觉遮挡降低 Alpha。只要元素当前能被点击命中，就可以直接进入暂存区。为避免 6 把锁被 228 个普通道具完全埋没，锁与 12 个普通道具混排在最后 18 个绘制顺序中；这只影响视觉可发现性，不构成层级解锁规则，锁的位置仍由同一随机散点池决定。
  - 六格占用、普通三消、剩余元素左移、特殊锁三消、死局检测均归口在面板，不把临时状态写入存档或共享配置。
  - 三把普通锁与其它同类元素一样会直接三消；若三锁中包含吸附到实体锁后的外部方框，则走特殊三消并开门。
- `Level122TileView.cs`：转发当前可命中元素的点击；交互锁定时仅停用 Raycast，不改变视觉亮度。UI 射线使用 Sprite Tight Mesh 三角形轮廓过滤透明矩形，点击 A 的可见像素不会再被前方 B 的透明留白抢走；不要求开启纹理 Read/Write，也不修改共享 Level033 素材导入设置。
- `Level122SlotDragHandle.cs`：只转发空槽拖动；面板统一保证仅有一次成功吸附。
- `Level122CloseButton.cs`：只转发页面 X 的左键/单指点击，不承载公共 Popup 行为。

### 3.3 视觉与资源

- 面板底与槽位分别引用 `Sp_l122_k_01`、`Sp_l122_k_02`，锁图引用 `Sp_l122_s_01`。
- 道具图引用 `Assets/Game/ArtSrc/Sprites/Turntable/rewards/Sp_prop_1_01～12.png`；没有复制或修改这些 Sprite。
- 普通和特殊三消均采用 `shake → scale/fade`，结束时生成十个双色放射粒子并播放 `passwordCorrect`。成功时原场景锁同步使用 0.2 秒摇晃、0.18 秒放大渐隐，直接调运行时 Spine Alpha，不改共享材质或执行旧开锁链。锁体处额外生成同款粒子；原方框由父锁带着摇晃和缩放，避免叠加两次位移/缩放。
- 页面打开时从 `0.82` 缩放与透明状态以 `OutBack` 回弹到原尺寸；X 关闭或成功关闭时以 `InBack` 缩回并渐隐，关卡销毁时则立即清理，不延迟场景卸载。
- 拖动时不显示黄色目标提示；成功落点不生成小锁图标。远离锁时用原 UI 方框绘制，靠近锁时隐藏 UI Image、显示等屏幕尺寸的世界 Sprite，并继续跟随指针；松手才把它移到锁体中心、锁定并计入三消。松手不重建世界 Sprite，不改变其缩放；取消则删除世界预览并恢复原槽位。
- DoorLock 的一个 Spine Mesh 同时包含锁体和两侧链条，不能用整个 Renderer Bounds 的中心对齐锁体。当前读取 Spine `suo` 顶点附件的实际世界范围中心；锁资源失配时才回退根 Renderer 或 Collider。顶点数组复用，拖放本身不修改锁的 Transform、骨骼或动画；只有确认成功后的三消表现修改运行时 Transform/Alpha，中断时恢复。
- 排序固定为“门图 → 捕获方框 → 原锁/链条”：仅在 Level122 运行时给 DoorLock 增加局部 SortingGroup，组 Order 沿用锁的 101（位于原门组内、门图 100 之前方），方框作为锁子节点使用组内 100，原锁 Renderer 保持 101。关闭或销毁面板时清理预览、捕获方框和临时组，使用已有默认 Sprite 材质，不克隆/修改共享材质。
- 更正：此前“Spine 与 Sprite 混排不稳定”“不同透明材质导致 SortingOrder 失效”“Collider 必然偏左下”均未取得实测证据，不能作为根因。2026-09-08 PlayMode 对照发现，旧 UI Ghost 确实覆盖场景锁；旧世界方框 Order=90 则藏在整扇门后；把方框放回门内同组即可看见锁正常盖在方框上。相关旧代码注释因与该证据冲突一并更正。

## 4. 变更文件

- `Assets/Game/Prefabs/Level/Level_122.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122MatchPanel.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122CloseButton.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122SlotDragHandle.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122TableTrigger.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Level122TileView.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_122/Resources/Level122MatchPopup.prefab`
- `Assets/Game/ArtSrc/Sprites/l122/Sp_l122_k_01.png.meta`（九宫格 Border）
- 对应 `.meta` 与本文档。
- `DevelopMD/已有功能设计与使用记录.md`

## 5. 验证结果

- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前完整 `Assembly-CSharp.rsp`，并补入尚未刷进响应文件的新关闭按钮脚本定向编译，结果 `exit 0 / 0 error`。
- Prefab 静态引用校验：132 个对象定义全部唯一，132 个本地 `fileID` 引用全部闭合，0 个本地缺失引用。
- 新页面 Prefab 静态引用校验：18 个对象/组件定义唯一，所有本地 `fileID` 引用闭合；根遮罩 Alpha 为 0、页面锚点为中心、背景 Image Type 为 Sliced。
- 新增脚本、三张 l122 Sprite 和十二张 Level033 奖励 Sprite 的 GUID 均可解析；Prefab 中遗留的 `Level115Ctrl/Level115QuizTrigger` GUID 已清除。
- 本轮静态复核确认旧的 `lockBounds.size * 1.2` 方框放大路径已经移除；牌堆道具、锁和共享 Level033 奖励 Sprite 均为 Tight Mesh 导入，可供透明区域射线过滤使用。

## 6. 风险与待验收项

- 当前可以连接 Unity 窗口；本次已执行 2340×1080 PlayMode 运行对象/渲染截图对照。`uncertain`：1920×1080、平板比例及 Android/iOS 真机的密度、过绘和触控体验尚未覆盖。
- `uncertain`：需要补验真实鼠标/移动端触摸的端到端事件、Tight Mesh 点击边界、手感和多指互斥。本次自动化原生拖动未触发游戏拖动回调，改用 PlayMode 中直接调用生产拖放回调并核对真实渲染帧，不能把它记作真机触摸已通过。
- “先拖锁后点两把锁”已通过本次 PlayMode 回调驱动测试；`uncertain`：“先点两把锁后拖锁”“普通三消后再捕获锁”和六格死局路径本轮未重测，既有逻辑保持不变。
- `uncertain`：`Sp_l122_k_01` 已按示意配置 68 像素四边 Border；最终四角保真与不同宽高比拉伸仍需 PlayMode 目视确认。
- 当前 Level122 尚沿用 `GamePlayPanel_115` 作为移动 HUD；如后续要求关卡资源命名完全独立，应复制为 `GamePlayPanel_122` 并移除未使用的 Level115 HUD Feature，不能直接修改共享 115 Prefab。

## 7. 回滚

- 将 `Level_122.prefab` 根脚本和桌子触发脚本恢复为原 Level115 GUID，并移除新增序列化字段，即可回到复制底稿行为。
- 删除 `Assets/Game/Scripts/Level/MainLevel/Level_122` 新增代码与文档即可撤销运行时面板；共享 Level033 奖励图、Level115、DoorLock 和 `door_0` 没有被修改，无需回滚。

## 8. 2026-09-08 PlayMode 锁层级复核

- 本次只改 `Level122MatchPanel.cs` 及本关/中央记录；无 Prefab、美术、相机、共享门、共享材质、存档或打点链修改。单关运行时增加一个可清理的 SortingGroup，复用顶点数组；无持续材质创建。
- 环境：Unity 6000.0.59f2、URP Renderer2D、Windows DX11、Android 编辑器目标，GameView 2340×1080。Unity 实际导入编译与完整 Roslyn 响应文件定向编译均通过；未声称 Android/iOS 构建通过。
- 修复前证据：`Temp/Level122-before-drag.png` 显示 Overlay Ghost 遮锁；`Temp/Level122-before-release.png` 显示世界方框藏在门后。`Temp/Level122-same-group-proof.png` 的同组对照确认锁与 Sprite 能正确前后排序。
- 修复后证据：`Temp/Level122-after-release.png/.txt`。捕获方框实例 ID `-652502` 在偏移拖动及松手后保持一致，屏幕大小始终 `110×108`；指针向右偏移 32 像素时方框也偏移 32 像素，`captured=False`，松手才成为 `captured=True`。中心从整锁链的 `(1916.19,402.29)` 改为锁体的 `(1907.44,358.42)`（Unity 屏幕坐标），原 DoorLock 世界位置 `(8.05,-2.71,0)` 不变。
- 目视已确认：居中的方框完整位于原锁/链条后方且门图前方；右移拖动时锁仍显示在方框上。无新小锁、无黄色提示、无尺寸跳变。
- 生命周期：关闭后 `closed-check` 无面板/方框，仅保留原门组；重新打开、开始拖动、取消后 `cancelled-check` 为 `captured=False / dragging=-1` 且无世界预览；取消后可再次捕获。随后调用两次生产锁牌点击回调，Unity GameView 显示面板已关闭、原锁/链条已移除，右门解锁。
- 测试入口：临时 Editor 诊断在 `Application.isPlaying` 分支中驱动生产回调并读取对象，不作为正式功能保留。开锁后诊断读取已销毁的锁 Transform 时自身出现一次 MissingReference，位置在临时 `Level122RenderAudit.cs:59`，不在生产脚本；开锁结果随后通过 Unity 窗口确认。项目既有 Firebase/SDK/退出清理日志不属于本次修复范围。
- 回滚本次修复：只恢复 `Level122MatchPanel.cs` 的本次 diff（本机基线备份 `Temp/Level122MatchPanel-before-lock-fix.cs`），不执行上节整关回滚，不回退用户其它修改。若换用不同 Spine 锁资源，需要重新核对 `suo` 附件及局部排序组。Temp 验证产物会随 Unity 临时目录清理，数值和结论以本节为长期记录。

## 9. 2026-09-08 全部清空解锁与实体锁三消

- 用户新增两条要求：全部元素三消也能解锁并显示“恭喜完成！过关拉！”；实体锁使用三消消失方式，消除后等 1 秒才关闭面板。本节更新第 1/3 节旧口径；第 8 节是上轮层级修复的历史验证，不代表本轮动画已验收。
- 基线：原实现仅外部锁三消能成功，牌堆空直接进入死局；立即关面板并调用 DoorLock 执行 `unlock`，一秒后启用 Trigger。本次在最后三连开始时确认“剩余牌堆为零且暂存区恰好就是本组三个”，优先走成功，不落入死局；普通三消后空暂存区直接继续判定，避免等待空 Tween Sequence 的完成回调。
- 影响文件：本目录 `Level122Ctrl.cs`、`Level122MatchPanel.cs`、`Assets/Game/Prefabs/Level/Level_122.prefab`、本文与中央记录。Prefab 新增 12 语言祝贺文案并移除未使用的共享开锁句柄引用；共享 DoorLock、Level033、其它关卡、UI/音频素材不变。Ctrl 旧注释“才开始自身动画”与新行为冲突，随入口替换为中文完成边界说明；不调整其它旧注释。
- 流程：三消 0.38 秒结束→锁与链条停用、出口 Trigger 启用→仅全清路径显示 SystemTips→实时等待 1 秒→原 0.2 秒关闭动画→恢复角色输入。成功阶段 X/点击/拖拽不接受新操作；关闭或销毁中断时杀掉匹配 Sequence，恢复尚未消失的锁位置/缩放/Alpha，并沿用局部方框/排序组清理。
- 边界：不新增全局事件、存档或打点；正式关卡成功仍由原出口触发。失败 1.5 秒提示及 `GameResetEvent(Failed)` 顺序未改，与 `DevelopMD/关卡打点生命周期约定.md` 无冲突。每次成功增加一组 10 个短命 UI 粒子，Spine 使用实例 Alpha，无材质克隆、持续轮询或额外资源加载。
- 当前验证：Unity 6000.0.59f2 自带 Roslyn、完整 `Assembly-CSharp.rsp` 编译 `exit 0`；Level_122.prefab 的 131 个定义无重复、全部本地引用闭合，12 语言文案与简中文字样检查通过，旧解锁绑定已移除。临时 PlayMode 用例也已编译通过，备份至 `Temp/Level122CompletionAudit.cs` 后从 Assets 移除，未执行。`uncertain`：本轮 PlayMode、全部 234 元素连续消除、两种锁捕获顺序、动画中断及真机测试待结果补记；当前 Unity 在试玩 Level136，未擅自停止，已询问是否可切换验证。
- 回滚：只恢复本轮两个脚本和 Level_122.prefab 的差异；本机基线备份为 `Temp/Level122Ctrl-before-completion.cs`、`Temp/Level122MatchPanel-before-completion.cs`、`Temp/Level_122-before-completion.prefab`。不得回退第 8 节层级修复或用户其它改动。
