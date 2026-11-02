# DragEventsListener 功能说明

## 2026-07-13：Unity Editor 鼠标优先

### 需求与原始行为基线

- `DragEventsListener` 同时支持场景对象的鼠标与触摸 Begin/Drag/End 事件。
- 修改前输入分支只判断 `UNITY_ANDROID || UNITY_IOS`。当 Unity Editor 当前 Build Target 为 Android/iOS 时，这些平台宏在 Editor 中同样成立，普通 Game View 因而进入 Touch 分支。
- 普通 Game View 没有模拟触摸时 `Input.touchCount` 为 0，鼠标点击不会进入 Mouse 分支；切到 Device Simulator 后产生模拟触摸才可触发。

### 修改后行为

- 移动触摸分支条件改为 `(UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR`。
- Unity Editor 无论当前 Build Target，都优先走现有 Mouse 分支，可直接在普通 Game View 用左键触发。
- Android/iOS 真机 Player 的 `UNITY_EDITOR` 为 false，继续走现有 Touch/fingerId 跟踪逻辑；Standalone/WebGL 等仍走 Mouse 分支。
- 未修改命中规则、事件顺序、Transform、序列化字段、公共 API、对象池、存档、资源、全局事件或平台 SDK。

### 影响范围与验证

- 代码影响文件：`Assets/Game/Scripts/Common/DragEventsListener.cs`。
- 已知代码调用包含 Level24、Level34、Level69、Level74、Level87 的启停或事件订阅；序列化挂载对象仍需在各关卡 PlayMode 回归。
- 静态检查应确认 Editor 预处理结果进入 Mouse 分支，非 Editor Android/iOS 进入 Touch 分支，并运行 `git diff --check`。
- Unity 普通 Game View、Device Simulator、Android/iOS 真机、多指、移出、Cancel 和应用暂停尚需运行验证，不能仅凭静态条件判断宣称通过。

### 回滚路径

- 将条件恢复为 `#if UNITY_ANDROID || UNITY_IOS` 即可恢复原行为；回滚后 Android/iOS Build Target 下的普通 Editor Game View 将再次依赖模拟触摸。
