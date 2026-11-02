# DLevel_035 木板旋转功能与风险评估

## 1. 需求、原始行为与影响范围

- 玩家完成第一笔有效画线后，`Scene/Wood` 应围绕自身中心缓慢旋转，并允许策划控制旋转速度。
- 修改前 `DLevel35Ctrl` 只有空生命周期方法，`DLevel_035.prefab` 也未挂载该脚本，运行时没有旋转入口。
- 本次影响仅限 `DLevel35Ctrl.cs`、`DLevel_035.prefab`、Level35 本地文档与中央设计记录。
- 不修改共享 `DrawLevelCtrl`、`DrawLineFeature`、倒计时、Actor、UI、配置、存档、资源加载或平台 SDK。

## 2. Facts

- `DLevel35Ctrl` 实现 `IDrawStrokeCompletedHandler`，由既有 `DrawLevelCtrl` 同步分发有效笔画完成和
  Attempt Reset，不新增 `PlayEvents` 或其它全局事件。
- prefab 显式绑定 `wood=Scene/Wood`、同根 `drawLevelCtrl`，当前保存 `rotationSpeed=-50` 度/秒。
- 只有第一笔有效笔画会把 `_isRotating` 置为 true；后续笔画不会重置角度或重复启动。
- 旋转使用 `wood.Rotate(..., rotationSpeed * Time.deltaTime, Space.Self)`，速度与渲染帧率解耦。
- `DrawLevelCtrl.IsPaused` 为 true 或收到 `Resolved` 后不推进；Attempt Reset 停止并恢复缓存的初始局部角度。
- 缺少 Wood 引用时组件记录 Bear 错误日志并自停用，避免空引用持续刷错。

## 3. Direct conclusions

- Wood 的三个现有视觉子节点会随共同根节点旋转，旋转中心由 Wood 根 Transform 原点决定。
- 正负 `rotationSpeed` 同时承担速度和方向调节；设置为零可保留触发链但关闭视觉旋转。
- 本实现不接入 Rigidbody2D，不会向 Actor、Draw Line 或其它物理对象施加力，也不会改变本关成功条件。
- 运行时持续成本是当前关卡激活旋转期间的一次 Transform 写入，未发现逐帧 GC 分配路径。
- 本次改动不会直接改变 DB、Luban、RemoteConfig、存档、资源地址、UI、analytics、广告、IAP 或 SDK 流程。

## 4. Hypotheses / risks

- `P1`：Wood 根节点原点是否与美术期望的视觉中心完全一致尚未经过 PlayMode 观察；若不一致会表现为偏心公转。
- `P1`：当前 `-50` 度/秒与 15 秒成功倒计时组合后约顺时针旋转 750 度，最终节奏需用户验收。
- `P1`：结算时 Wood 停在当前角度而不回正；这是为冻结结果画面，但是否符合关卡演出仍需 PlayMode 确认。
- `P2`：暂停只停止脚本角度推进；若 Wood 子节点以后增加独立 Animator/物理组件，其状态不由本控制器接管。
- `OBS`：不同移动设备帧率下 `Time.deltaTime` 可保持平均角速度，但极低帧率的单帧角度跳变仍需真机观察。

## 5. Verification evidence

- 已静态确认 `DLevel35Ctrl` 组件只在 `DLevel_035` 根节点挂载一次，Wood 与 DrawLevelCtrl 引用均指向本地 fileID。
- 已静态检查运行时代码未新增 `OnValidate`、`Reset`、`ExecuteAlways`、DB/Save、全局事件、资源加载或运行时集合分配。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:minimal -m:1 /nodeReuse:false`：
  `0 warning / 0 error`。
- Prefab YAML：125 个 anchor、0 重复；125 个唯一非零本地引用、0 缺失；脚本 GUID 只出现一次。
- 目标文件尾随空白扫描通过；仓库级 `git diff --check` 仅报告任务开始前已有的 DLevel_038 与 Android
  Manifest 空白问题，本次未修改这些文件。

## 6. Missing evidence

- `uncertain`：尚未执行 Unity PlayMode 的首笔触发、连续多笔、暂停/恢复、清线、失败重试、Replay、15 秒成功结算和视觉中心检查。
- `uncertain`：Android/iOS 横屏设备上的触控、帧率和旋转视觉尚未验证。

## 7. Rollback path

- 功能停用可把 prefab 上 `DLevel35Ctrl` 禁用或将 `rotationSpeed` 设为零。
- 完整回滚需移除 prefab 根节点的 `DLevel35Ctrl` 组件，并恢复旧脚本内容；无需数据迁移、资源清理或共享系统回滚。
