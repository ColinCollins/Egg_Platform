# DLevel_035 脚本结构记录

## 1. 需求与参考基线

- 需求：第一笔有效画线完成后，`Wood` 围绕自身中心缓慢匀速旋转，旋转速度由 prefab 参数控制。
- 原始基线：`DLevel35Ctrl` 只有空的 `Start/Update`，prefab 未挂载该组件，因此 `Wood` 不会响应画线。
- 参考基线：沿用 DLevel_037 的关卡根节点局部 `IDrawStrokeCompletedHandler` 模式；本关仅驱动视觉
  Transform，不复制 DLevel_037 的 Rigidbody2D/HingeJoint2D 物理释放逻辑。

## 2. 最终脚本树与职责

```text
DLevel_035
├─ DrawLevelCtrl                  # Draw 生命周期、暂停、结算和 Attempt Reset 分发
├─ DrawLineFeature                # 有效笔画生成与 StrokeCompleted 通知
├─ DrawLevelCountdownCtrl         # 第一笔后 15 秒成功倒计时
├─ DLevel35Ctrl                   # Wood 首笔旋转、暂停/结算门禁和 Reset 复位
└─ Scene
   └─ Wood                        # 视觉根节点，子级随根节点共同旋转
```

`DLevel35Ctrl` 只负责：

1. 绑定 `Scene/Wood` 与同根 `DrawLevelCtrl`，缓存 Wood 初始局部角度。
2. 第一笔有效画线完成后开启旋转，后续笔画不重复改变状态。
3. 未暂停且未结算时，在 `Update` 中按 `rotationSpeed * Time.deltaTime` 旋转。
4. Attempt Reset 时停止并恢复初始局部角度；结算时停止在当前角度。
5. 不判断胜负、不访问 UI/DB/配置/资源，也不派发全局事件。

## 3. 序列化绑定与参数

| 字段 | 当前 prefab 值 | 约束 |
| --- | --- | --- |
| `wood` | `Scene/Wood` Transform，本地 fileID `1625330878690576578` | 只绑定本关视觉根节点 |
| `drawLevelCtrl` | 根节点 `DrawLevelCtrl`，fileID `900000000000000001` | 读取暂停并订阅局部结算 |
| `rotationSpeed` | `-50` 度/秒 | 当前为顺时针；正值逆时针、零为静止 |

## 4. 生命周期与调用链

1. `OnEnable` 仅在 PlayMode 解析引用、缓存初始角度并订阅 `DrawLevelCtrl.Resolved`。
2. `DrawLevelCtrl` 在完成一笔有效画线后调用 `OnDrawStrokeCompleted`；只有
   `DrawStrokeResult.IsFirstStroke` 会开启旋转。
3. `Update` 在旋转已开启、关卡未暂停且未结算时按帧时间推进 Wood 局部 Z 轴角度。
4. `OnDrawAttemptReset` 恢复初始局部角度并重新等待第一笔；`Resolved` 只停止旋转，不改变结算角度。
5. `OnDisable` 解除局部结算订阅，不创建或清理任何共享运行时对象。

## 5. 数据、资源、UI 与结构复盘

- 数据/存档/Luban/RemoteConfig：无变更。
- 资源：只修改现有 `DLevel_035.prefab` 的本地组件与引用，不新增运行时资源路径。
- UI/事件/analytics/SDK：继续使用共享 Draw 流程；无新 UI、全局事件、打点或平台分支。
- 性能：运行时仅在当前关卡且旋转激活时执行一次 Transform 旋转，无集合分配、场景扫描或实例化。
- 结构复盘：这是单关视觉机关，继续保留在 Level35；出现多个职责一致且已验收的关卡前不抽取共享组件。

## 6. 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1 /nodeReuse:false`：
  `0 warning / 0 error`。
- Prefab 静态检查：125 个 YAML anchor、0 重复；125 个唯一非零本地引用、0 缺失；
  `DLevel35Ctrl` 脚本 GUID 只出现一次，Wood 与 DrawLevelCtrl 引用均可解析。
- `uncertain`：Unity PlayMode 的旋转方向/速度手感、暂停恢复、15 秒结算、清线/失败重试、Replay，
  以及 Android/iOS 横屏表现尚未人工验收。
- 完整回滚：移除 prefab 根节点的 `DLevel35Ctrl` 组件并恢复旧空脚本；共享 Draw、倒计时、配置与存档无需迁移。
