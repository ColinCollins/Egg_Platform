# Level110 功能与风险记录

## 需求结论

- 本关答案固定为 `450`。三个问号块从左到右分别代表百位、十位、个位；任意时刻组合成为 `450` 就解除右侧门锁。
- 吃草莓不是解锁前置条件。即使没有吃草莓，只要顶出 `450`，也可以解除门锁并按主线标准流程进入出口门通关。
- 草莓被角色吃掉后，右侧算式的“+草莓”整体消失，结果从 `1250` 改为 `1000`；同时在原结果位置上浮渐隐一个 `-250`。
- 金币、钻石、草莓都允许拖动；金币、钻石和未命中角色的草莓在松手后回到初始位置。
- Level110 的提示入口气泡等待时间覆盖为 30 秒，其它主线关卡仍使用远端/本地公共配置。

## 实现范围

- `Assets/Game/Prefabs/Level/Level_110.prefab`
  - 以现有 Level110 场景结构为基础，仅在本关实例接入数字块、题面图标、出口锁和本关角色扩展。
  - 三个数字块绑定 `Level110QuestionDigit`；六个题面图标绑定 `Level110DraggableItem`。
  - 角色实例用 `Level110ActorCtrl` 替换共享 `ActorCtrl` 组件，但不修改共享 Actor Prefab。
  - 出口锁复用 DoorLock 原有事件链；解锁前关闭正常门的成功 Trigger，答案成立后启用。
- `Assets/Game/Scripts/Level/MainLevel/Level_110/Level110Ctrl.cs`
  - 维护三位数字、`450` 判定、密码正确反馈、门锁特写、解锁、草莓投放/食用和右侧算式改写。
- `Assets/Game/Scripts/Level/MainLevel/Level_110/Level110QuestionDigit.cs`
  - 仅角色从方块下方顶撞时响应，按 `? → 0 → 1 … 9 → 0` 循环。
  - 每次顶撞播放 `getCoin`，文字先向上淡出，再从下方滚入新数字；数字态改用直立字体与对称边距，问号初态不变。
- `Assets/Game/Scripts/Level/MainLevel/Level_110/Level110DraggableItem.cs`
  - 使用图标 Sprite 的实际边界拾取；拖动期间临时提层。
  - 无效投放缓动回原位；草莓命中角色后飞向嘴部，同时缩小、渐隐并隐藏。
- `Assets/Game/Scripts/Level/MainLevel/Level_110/Level110ActorCtrl.cs`
  - 仅为 Level110 保持 `eat` 动画，防止共享移动动画在食用期间覆盖它；结束后恢复待机与控制。
- `Assets/Game/Scripts/Play/ILevelTipsWarningIntervalProvider.cs`
  - 提供单关提示等待时间覆盖接口。
- `Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Playing.cs`
  - 进入 PLAYING 时读取当前关卡根节点的覆盖；没有有效覆盖时保留公共远端配置。

## 运行流程

### 数字与通关

1. 角色从下方碰到任一问号块底面。
2. 当前块播放顶金币音效与数字滚动，新值按循环规则推进一次。
3. 三个数字每次变化后均检查当前组合。
4. 组合为 `450` 时，三个块停止继续计数并播放 `passwordCorrect`；镜头用 0.28 秒向门锁靠近 75%，正交尺寸缩到当前的 88%。
5. 聚焦完成后启用正常出口 Trigger，并调用 DoorLock 的标准解锁音效、动画与回收链；特写保持 1.05 秒覆盖现有约 0.9167 秒 `unlock` 动画，再用 0.3 秒恢复原镜头位置和尺寸。
6. 解锁本身不直接结算；角色进入门后仍走现有主线成功流程。

### 草莓与题面

1. 只有草莓松手时与角色碰撞体范围相交才进入食用流程。
2. 角色停止移动并播放 `eat`；草莓同时向嘴部移动、缩小和渐隐。
3. 食用完成后隐藏右侧“+草莓”项，结果改为 `1000`，并在原 `1250` 位置播放 `-250` 上浮渐隐，再恢复角色控制。
4. 角色已死亡或已进入出口门时不启动食用；食用回调也不会在这些状态恢复控制。

## 配置与既有资产

- 工作区现有 JSON 配置已经包含 `10110 -> Level_110` 以及对应排序，本次没有改写 Excel 或生成配置。
- 复用现有 Box Spine 的 `eat` 动画、`getCoin` 音效、DoorLock 解锁事件和标准出口门流程。
- 密码输入正确复用已有 `passwordCorrect` 音效 Tag，不新增声音资源；DoorLock 自身原有 `unlock` 音效仍在开锁时播放。
- 不修改共享 Actor、DoorLock、DragableItem、GamePlayPanel Prefab，也不修改其它关卡的题面或提示等待值。

## 验证记录

- 使用 Unity 当前 `Assembly-CSharp` 响应文件进行 Roslyn 定向编译，结果为 `exit 0`、0 个编译错误；输出中的 warning 为工程既有 warning。
- Level110 Prefab YAML 静态检查：本轮新增组件 fileID 唯一，GameObject 组件引用均有对应定义，三个数字位和六个拖拽物接线闭合。
- 静态确认答案数组为 `4/5/0`，草莓状态不参与 `MatchesAnswer()`，解锁调用标准 DoorLock 事件且只执行一次。
- 2026-09-03 修正后使用当前 Unity/Bee `Assembly-CSharp` 响应文件完整 Roslyn 编译，结果 `exit 0`、0 个编译错误；Prefab 为 301 个定义、301 个唯一 fileID、0 个组件定义缺失。

## 风险与待验收项

- 当前仍需 Unity PlayMode 目视/手感验收：直立数字的字号与居中、`-250` 层级/速度、门锁特写幅度与恢复构图、两段正确/开锁音效节奏，以及连续顶块、草莓食用和进门成功。
- PC 鼠标与移动端触摸都沿用共享拖拽输入，但移动端实际触控、多分辨率和不同帧率尚未验证，状态为 `uncertain`。
- 当前 Unity 编辑器可能仍处于 PlayMode；最新脚本和 Prefab 是否已经完成编辑器自动导入需退出 PlayMode 后确认，状态为 `uncertain`。
- 仓库根规则引用的 `specs/037-remote-resource-loading/plan.md` 在当前工作区缺失，与 Level110 无直接行为冲突，但该计划依据不可核对，状态为 `uncertain`。

## 回滚边界

- 删除 Level110 Prefab 上新增的三个数字组件、六个拖拽组件和本关角色扩展，并恢复本关原控制器/Actor 接线。
- 删除 `Assets/Game/Scripts/Level/MainLevel/Level_110/` 下本关代码与本记录。
- 若不再需要任何单关提示等待覆盖，再同时恢复 `PlayCtrl_Playing.cs` 的接口读取，并删除 `ILevelTipsWarningIntervalProvider.cs`。
- 不回滚工作区原有 Level110 Prefab 布局、JSON/Excel 配置或其它关卡改动。

## 2026-09-03 表现修正补充

- 用户截图显示数字态在橙色块中视觉偏斜。Prefab 静态回读确认三个 TMP 节点、三个方块根节点的本地旋转均为 0；本轮没有硬旋转文本，而是在首次显示数字时切换到 `Twinkly SDF` 数字字体，并把问号专用的不对称 Margin 改为 `(9,1,9,1)` 对称边距，强制更新 TMP 网格。
- 吃草莓后的旧表现为把加号 TMP 改成 `+0`；新表现直接隐藏该加号节点，草莓仍按原流程缩小渐隐，结果改为 `1000`，并克隆结果 TMP 生成一次性 `-250`，从原结果位置上浮 0.75 世界单位并在 0.75 秒内渐隐后销毁。
- `450` 判定仍与吃草莓无关。新时序为 `passwordCorrect → 0.28s 聚焦 → DoorLock 标准解锁链 → 1.05s 特写保持 → 0.3s 恢复`。镜头状态在序列开始时缓存，关卡禁用/销毁时会立即恢复；未找到正交相机或门锁目标时跳过特写但仍正常解锁。
- 变更仅修改 Level110 私有脚本和 Prefab，并更新本记录/中央记录；共享字体资产、Camera Prefab、DoorLock、Actor、拖拽和通关逻辑未改。PlayMode 视觉与移动端横屏仍为 `uncertain`。

## 2026-09-03 数字字号修正

- 用户截图确认切换到 `Twinkly SDF` 后，TMP 自动字号会把三个数字放大到橙色方块之外。本轮关闭数字状态的自动字号，并固定为 `7.5`；问号初态、循环顺序、`450` 解锁与其它过关逻辑均不变。
- 三个数字位使用同一字号和原有对称边距。最终视觉尺寸仍需在 Unity 重新导入脚本与 Prefab 后进行 PlayMode 验收。
