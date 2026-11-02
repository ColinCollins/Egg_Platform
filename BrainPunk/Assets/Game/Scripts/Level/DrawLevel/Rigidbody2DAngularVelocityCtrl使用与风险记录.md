# Rigidbody2DAngularVelocityCtrl 使用与风险记录

## 需求与原行为

2026-09-18：用户要求提供设置 Rigidbody2D 角速度的脚本，并在 Draw77 的 Sp_boxr_dj_01 上使用。未找到 Common 下现成的角速度组件或同名功能文档；既有 ShakeRotateHandle 为 Transform 抖动，不承担物理角速度设置，不能替代本需求。

新增独立组件，不修改既有共享脚本。首次绑定仅 DLevel_077.prefab 的四个同名机关及编号副本；范围/速度先按四个机关开场 90°/秒实施，可通过 Inspector 改值。当前工作区没有 DLevel077Ctrl/DLevel77Ctrl 专用脚本，本次实际目标为 DLevel_077.prefab，不新增空壳 LevelCtrl。

## 用法与职责

- 组件完整名称：Game.ItemEvent.Rigidbody2DAngularVelocityCtrl。
- targetRigidbody：Inspector 显式绑定目标 Rigidbody2D，不自动查找。
- angularVelocity：度/秒，默认90；正数逆时针，负数顺时针，0停止。
- applyOnEnable：默认开启，在运行态 OnEnable 应用一次配置。关闭后通过 API/UnityEvent 触发。
- ApplyAngularVelocity()：无参数 API，应用配置值，可直接绑定 Inspector 事件。
- SetAngularVelocity(float value)：写入当前角速度，可绑定 float 事件；SetAngularVelocity(0) 停止旋转，不改配置值。

只写 angularVelocity，不改变类型、位置、质量、重力、阻尼、线速度或约束，没有 Update/FixedUpdate。物理系统继续积分角度；Dynamic 刚体可能受阻尼/碰撞改变速度，本组件不逐帧强制恒速。禁用组件不清除已经写入的刚体角速度；停止请调用 SetAngularVelocity(0)。重新启用且 applyOnEnable 开启时会重新应用 Inspector 配置。

所有写入由 Application.isPlaying 门禁保护；无编辑器回调、实例化、DB/存档/远端、全局事件或管理器链。目标为空不写入。Static、旋转冻结、未模拟等条件仍由 Unity 原刚体配置决定，本组件不擅自解除。

## 文件与流程

新增本目录脚本/.meta/本文档；D077 增加四份组件并分别绑定原刚体，不改原物理与地图。角速度配置 → OnEnable 或公开 API → 目标 angularVelocity → Unity Physics2D 旋转；没有资源加载、UI、存档、配置表、打点或 SDK 接入。

## 验证与风险

- Unity6000.0.59f2 现有响应文件引用的独立 Roslyn 编译0 error；主工程编译证据见任务临时目录。
- 独立 Unity PlayMode，逐字生产脚本 + 真实 Rigidbody2D/CircleCollider2D/Physics2D.Simulate，12项通过，没有替身刚体或物理 API。
- Kinematic、角阻尼0.05：90°/秒真实1秒转90°；-180°/秒真实半秒转-90°；传0后停止。参数保留、无参数配置应用、开关自动应用、重新启用、禁用后继续物理模拟均通过；Dynamic 阻尼衰减保持。
- 单位一手来源：Unity6000.0.59f2 的 UnityEngine.Physics2DModule.xml，P:UnityEngine.Rigidbody2D.angularVelocity 明确单位为度/秒；正负方向由上述真实物理测试确认。
- D077：138唯一YAML锚点，本地引用闭合；只变4个机关组件表并新增4份组件，130其它原块逐字保持，Prefab meta GUID保持。
- `uncertain`：正式D077地图刀图/碰撞/球与Actor、暂停/结算/重建/退出、Android/iOS横屏手感未实测。没有在所有既有关卡挂载或回归，不将组件可复用性当成已验证所有玩法。

证据：C:/Users/admin/AppData/Local/Temp/brainpunk-angularvelocity-d77-20260918/，含原Prefab、bindings.json、static-validation.json、生产脚本物理夹具、probe-result.txt（CHECKS=12）与Unity日志。正式关卡结构/风险见本目录 Level77 两份记录。

## 回滚

仅移除D077四份新组件及其组件表引用、删除新组件文件/.meta；不恢复整份Prefab覆盖人工地图修改，不改旧关或原刚体。若改变速度/触发时机，只调整该组件配置或事件绑定。

## 2026-09-18目录调整

按用户要求将脚本、使用记录和对应.meta从Common/Others移至Assets/Game/Scripts/Level/DrawLevel。脚本字节、类名/命名空间和meta GUID保持，77关绑定仍引用12be788b070b4e0a8a629d253166d6a5。只改资源位置与文档路径，没有新增运行时行为；沿用12项物理验证，不重复测试物理。迁移检查源码/meta哈希一致、目标路径编译及Prefab GUID引用。回滚只将四个文件移回原目录并同步文档路径。
