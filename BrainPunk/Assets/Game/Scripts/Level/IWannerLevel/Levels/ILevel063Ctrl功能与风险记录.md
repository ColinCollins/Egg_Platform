# ILevel063Ctrl 功能与风险记录

日期：2026-08-25

## 需求与原始基线

- 需求：完善 `ILevel063Ctrl`，提供三个互相独立的公开 API，用于把第 063 关 Actor 切换到三档不同的基础移动速度。
- 修改前基线：脚本只有空的 `Start()`、`Update()` 和“三个 API”的用途注释，没有 Actor 引用、速度参数或公开 API；
  `ILevel_063.prefab` 根节点已经挂载该脚本，但组件没有序列化字段，现有按钮也没有静态绑定到该脚本。
- 共享 `ActorCtrl.SetMoveSpeed(float)` 已存在，能够只修改 Actor 的基础移动速度；本关无需改写共享 Actor 实现。

## 影响文件、资源与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel063Ctrl.cs`
- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel063Ctrl功能与风险记录.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 复用 `Actor_IWanner`、共享 `ActorCtrl.SetMoveSpeed(float)` 与 Bear Logger。
- 本次没有修改 `ILevel_063.prefab`、共享 `ActorCtrl`、`IWannerLevelCtrl`、按钮执行器、输入事件、DB/Save、
  配置、资源加载、UI、打点、广告或平台 SDK。

## 修改后行为与调用流

本关本地 UnityEvent → `SetMoveSpeed1()` / `SetMoveSpeed2()` / `SetMoveSpeed3()`
→ 解析当前关卡子层级的 `Actor_IWanner` → 将对应 Inspector 配置值夹取到非负范围
→ `ActorCtrl.SetMoveSpeed(float)` → Actor 后续移动计算使用新基础速度。

- 三档默认值分别为 `3.3`、`5`、`7` Unity 单位/秒，可在 063 控制器 Inspector 独立调整。
- 三个公开 API 均为无参数方法，便于压力按钮或其它本地 UnityEvent 直接绑定，可安全重复调用。
- API 只修改基础移动速度，不直接改写当前 `Rigidbody2D.linearVelocity`、移动输入、朝向、跳跃、暂停或结算状态；
  新速度从 Actor 后续移动更新开始生效。
- 引用未配置时，每次 API 调用会从当前关卡子层级查找 `Actor_IWanner`；仍找不到时通过 Bear Logger 警告并安全返回。
- 编辑态调用会被 `Application.isPlaying` 门禁拒绝，不访问运行时单例、DB 或资源链路。
- 删除空 `Start()`/`Update()`，不新增逐帧轮询或运行时分配循环。

## 验证证据

- CodeGraph 源码证据确认 `ActorCtrl.SetMoveSpeed(float)` 直接更新 Actor 基础 `MoveSpeed`，无需新增共享 API。
- 静态 YAML 确认 `ILevel_063.prefab` 根节点已挂载 `ILevel063Ctrl`，同一关卡子层级存在
  `Actor_IWanner`，因此未显式序列化引用时具备局部回退查找路径。
- 静态确认三个公开 API 均经过 PlayMode 门禁、空引用保护和非负速度夹取；脚本没有
  `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、资源加载、实例化或新全局事件路径。
- `Assembly-CSharp.csproj` 已包含 `ILevel063Ctrl.cs`；执行
  `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false` 结果为
  `0 error / 106 warning`，warning 均来自工程既有诊断范围。
- Unity Editor 日志确认新脚本已按原有 GUID 重新导入，未检索到 `ILevel063Ctrl` 相关编译错误；
  本次没有执行 Unity PlayMode、设备或按钮实际接线验证。

## 风险与未验证缺口

1. **P1：Prefab 按钮尚未接线。** 当前静态 YAML 中四个压力按钮仍只调用各自 `BaseItemExecutor.Execute()`；
   哪三个按钮应对应三档速度，以及对应顺序，现有脚本注释和参考图均没有可靠证据，因此本次未擅自写入绑定。
2. **P1：三档最终数值待玩法确认。** `3.3/5/7` 是可运行默认值，其中第一档对齐当前 Actor prefab 基础速度；
   第二、三档是否满足陷阱节奏仍为 `uncertain`，需要在 Unity PlayMode 按关卡设计调参。
3. **P2：当前帧速度切换。** API 不立即改写刚体速度；若设计要求按钮按下当帧无延迟改变水平速度，需要在
   PlayMode 观察 Actor 更新顺序后再决定是否增加局部刚体同步，不能在缺少物理证据时直接覆盖。
4. **P2：运行时重用。** 正常 Reset/Replay 若销毁并重建关卡实例，会恢复 prefab 配置的初始速度；若存在复用同一
   Actor 实例的局部重置路径，速度档位不会由本控制器主动复原，该路径目前为 `uncertain`。
5. Android/iOS 横屏触控、低帧率下按钮触发、暂停/恢复、死亡、Reset、Replay、成功门和 60 fps 表现尚未验证。

## 回滚路径

- 将 `ILevel063Ctrl.cs` 恢复为空壳即可撤销运行时速度切换；本次没有改写 prefab 绑定、共享 Actor、存档或配置。
- 若后续在 prefab 接线，回滚时同时移除按钮对三个速度 API 的 UnityEvent 调用；无需迁移数据或资源地址。
