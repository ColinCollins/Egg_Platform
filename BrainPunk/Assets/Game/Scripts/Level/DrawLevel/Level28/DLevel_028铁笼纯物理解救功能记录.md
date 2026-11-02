# DLevel_028 铁笼纯物理解救功能记录

## 需求与背景

- 关卡中的猫使用球形载体，载入关卡后立即参与重力模拟。
- 新增铁笼物理结构，玩家需要用画线形成的刚体把铁笼撬开，使球沿斜坡滚入成功门。
- 解法保持纯物理，不增加“检测笼子已打开”或定制过关脚本。

## 基线状态

- `DLevel_028.prefab` 由既有关卡素材拼接，根节点遗留了 `DLevel27Ctrl`。
- 球的 `Rigidbody2D` 为 Kinematic，必须依赖旧控制器切换状态，不符合本关进入即 Dynamic 的要求。
- 铁笼已有 Dynamic `Rigidbody2D`，但质量为 1 且没有碰撞盒，无法约束球或被画线可靠撬动。
- 成功门已绑定通用 `DrawLevelCtrl.OnTriggerSuccess`；20 秒倒计时失败逻辑也已存在。

## 实现方案

### 铁笼

- 复用现有铁笼节点 `Sp_hx_tlz_01`，放在 `Stuff` 层。
- 保持 `Rigidbody2D` 为 Dynamic，设置质量 8、线性阻尼 0.05、角阻尼 0.15、重力倍率 1。
- 使用 Interpolate 与 Continuous Collision Detection，提高撬动时的稳定性并降低穿透概率。
- 添加底部开放的 U 形 `EdgeCollider2D`：左右边和顶部阻挡球，底部由斜坡平台承担支撑；铁笼被抬起后球可以从底部滚出。
- 碰撞盒使用可复用的 `CageLeverable.physicsMaterial2D`（摩擦 0.4、弹性 0）。该摩擦仍高于当前约 18° 斜坡的下滑阈值，同时比原先 0.8 更容易被画线推动。

### 球与画线

- 球从载入第一帧即 Dynamic、Interpolate，保持当前预制体中已由关卡编辑调整的质量 0.5、重力倍率 1.5 与 Continuous Collision Detection。
- 画线继续复用关卡现有 `DrawLineFeature`，不新增线条或笼子专用运行时代码；当前 `strokeMass` 为 80，可通过杠杆长度和接触位置产生足够撬动力。
- 本关把画线碰撞半径从 0.05 调为 0.1，使物理接触宽度与 0.2 的可见线宽一致，减少视觉上接触铁笼但碰撞未咬住的情况。

### 关卡流程

- 移除根节点遗留的 `DLevel27Ctrl`，避免旧的夹子/球状态机把球重新设为 Kinematic。
- 通关继续复用通用成功门：球进入门的触发区后调用 `DrawLevelCtrl.OnTriggerSuccess`。
- 失败继续复用四周 `FallArea` 与 20 秒倒计时，不增加额外判定。

## 影响范围

- 预制体：`Assets/Game/Prefabs/DrawLevel/DLevel_028.prefab`
- 复用代码：`DrawLevelCtrl`、`DrawLineFeature`、`DrawLevelCountdownCtrl`
- 新增可复用物理材质：`Assets/Game/ArtSrc/Physics/CageLeverable.physicsMaterial2D`
- 不涉及存档、配置表、UI、埋点或平台差异代码。

## 验证

- 已检查球和铁笼均为 Dynamic，铁笼碰撞盒为非 Trigger 且底部开放；用户在 Unity 中微调过的碰撞点保持不变。
- 已检查旧 `DLevel27Ctrl` 组件及脚本引用已移除。
- 已检查有效成功门仍绑定 `DrawLevelCtrl.OnTriggerSuccess`，旧门实例保持禁用。
- 已检查关卡倒计时仍为 20 秒失败计时，没有引入定制过关条件。
- 已执行预制体本地 fileID、脚本 GUID 与 `CageLeverable` 物理材质 GUID 静态引用校验。

## 2026-08-04 撬笼手感修正

- 现象：画线已经接触铁笼，但笼子质量 15、摩擦 0.8，长线也很难形成有效位移或转动。
- 修正：铁笼质量降至 8、角阻尼降至 0.15、接触摩擦降至 0.4；画线碰撞半径增至 0.1。
- 设计边界：只放宽纯物理交互，不增加自动抬笼、吸附、位移阈值或脚本判定。

## 2026-08-04 横推逃课修正

- 现象：降低质量和摩擦后，玩家可以把铁笼连同球直接沿斜坡推到成功门附近，不需要真正撬开笼子。
- 修正：在铁笼 `Rigidbody2D` 上启用 `Freeze Position X`，锁定世界 X 平移。
- 保留自由度：铁笼仍是 Dynamic，Y 方向移动与 Rotation 均不锁定，因此画线仍可从底部把它抬起或掀开，球可从开放底部滚出。
- 用户在 Unity 中把球质量调为 0.5；本次修正保留该参数，仅约束铁笼，不把约束误加到球上。
- 设计边界：使用 Unity 物理约束封堵整笼横推，不增加位移检测、自动复位或定制通关判定。

## 风险与回归点

- 需要在 Unity PlayMode 中确认摩擦降至 0.4 后，铁笼在初始斜坡上不会因接触点微小重叠而抖动或自行滑走。
- 需要分别验证短线、长线、从左侧和从底部撬动时，铁笼不会高速穿透画线或平台。
- 需要确认锁定世界 X 后，铁笼被抬起和旋转时视觉上不会产生明显违和，并且无法再被推到成功门附近。
- 需要确认球脱笼后能沿斜坡进入成功门，并能被四周 `FallArea` 正常判负。
- 移动设备尚未专项验证；若不同固定帧率下手感有差异，优先微调铁笼质量、阻尼和 EdgeCollider 点位，不增加定制过关逻辑。

## 回滚方式

- 撬笼手感参数可分别恢复为质量 15、角阻尼 0.25、画线碰撞半径 0.05，并把铁笼材质恢复为 `DrawStrokeStable.physicsMaterial2D`。
- 如需仅回滚横推限制，将铁笼 `Rigidbody2D` 的 Constraints 从 `Freeze Position X` 恢复为 None。
- 完整回滚时删除铁笼 `EdgeCollider2D`，恢复铁笼原始 Layer 与 Rigidbody2D 参数。
- 将球 Rigidbody2D 恢复为 Kinematic，并重新挂回原控制器（仅在决定恢复旧夹子流程时使用）。
