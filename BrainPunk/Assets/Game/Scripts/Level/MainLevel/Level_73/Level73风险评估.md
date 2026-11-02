# Level073 风险评估

更新时间：2026-07-17

## 原有行为基线

Level073 通过 `GamePlayPanel_003` 的按下回调驱动既有 `PlayerJumpEvent` 和 `ActorCtrl.TriggerJump()`；默认没有本关镜头跟随或长按蓄力控制。场景中已有关闭的 `L73` 蓄力条及角色 `jump_ready_3` 动画配置。

## 受影响资源与系统

- Level073 prefab 根节点、`L73` 三张 SpriteRenderer、主角与 GameCamera 引用。
- 新增的 Level073 本地输入/蓄力脚本。
- 复用的 `CameraFollowArea`、`GamePlayPanel_003`、`ActorCtrl`、`ActorSpineCtrl`、Rigidbody2D。

## 风险与处理

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 长按导致普通跳被破坏 | 不拦截既有 OnClickDown 与 PlayerJumpEvent；短按只计时，不追加速度 | 静态已检查，PlayMode 未验证 |
| 普通状态机覆盖蓄力动画 | 在本地控制器 LateUpdate 对蓄力状态保持 `jump_ready_3` | 静态已检查，PlayMode 未验证 |
| 中心 Pivot 使填充向两端缩放 | 缩放时下移填充图中心，保持底边不动 | 源图 Pivot 已核对，画面未验证 |
| UI 禁用或关卡销毁时误触发松手加速 | Up 回调延迟到 LateUpdate，按钮失活则取消；Destroy 总是取消本地蓄力 | 代码静态已检查，暂停/重置未验证 |
| 镜头跟随影响其他关卡 | `CameraFollowArea` 只绑定 Level073 实例，不修改共享实现或其他 prefab | 静态已检查，PlayMode 未验证 |
| 蓄力参数不符合关卡节奏 | 3 秒阈值、3 秒填充、18-30 速度均为本关私有序列化值 | Inspector 调参和实际高度未验证 |

## 验证证据

- `CameraFollowArea` 已有主角、相机、平滑和范围边界实现；Level073 绑定其 actor 为本关 `ActorCtrl`、targetCamera 为本关 GameCamera。
- `Sp_l73_jd_01.png` 为中心 Pivot，且是渐变填充素材；`Sp_l73_jd_02/03` 保持底图和边框，不参与缩放。
- `GamePlayPanel.cs` 明确在 JumpBtn 的 `OnClickDown` 派发跳跃事件；本关只追加 OnClickDown/OnClickUp 计时回调。
- Unity 在新增脚本后重新生成 `Assembly-CSharp.dll`，最近 Editor.log 没有 Level073 相关导入或 C# 错误；这不替代实际蓄力、镜头和触控路径验证。

## 未验证项

- Unity 重新导入后的脚本 GUID、序列化引用与 Console。
- PlayMode 中点按、恰好 3 秒、半满、满蓄、UI 失焦松手、暂停、死亡和重开。
- Android/iOS 横屏触控、60 fps 表现、相机追随边界和长关卡滚动。

## 2026-07-17 第二轮风险评估

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 长按开始时仍先触发普通跳跃 | 本关关闭共享 `jump_btn` 的 Interactable，并在同一按钮 RectTransform 范围内自行识别输入；普通跳跃仅在未蓄力的松手时调用 | 静态已核对，PlayMode 待验收 |
| 关闭共享按钮导致不同关卡 UI 失效 | 仅保存并关闭当前 Level073 的运行时按钮；`OnDestroy` 恢复原 Interactable，未改动共享 prefab 或 `GamePlayPanel.cs` | 静态已核对，关卡切换待验收 |
| 蓄力期间仍可横向移动或动画被覆盖 | 进入蓄力时 StopMoving，释放/取消时 ResumeMoving；LateUpdate 保持 `jump_ready_3` 循环 | 静态已核对，PlayMode 待验收 |
| 填充图不可见 | 底图、填充、边框分别为排序层 0、1、2，填充仍按底边固定缩放 | 静态已核对，画面待验收 |
| 失焦、触点丢失或暂停状态误跳 | 失焦、禁用、销毁、控制权失效和触点消失均走取消路径，不派发跳跃 | 静态已核对，移动端待验收 |
| 复制遗留拖拽或碰撞 | prefab 未命中 `DragableItem` GUID；L73 仅有 Transform/SpriteRenderer；运行时关闭跳跃 UI 的原 Collider2D | 静态已核对 |
| Actor 朝向翻转影响蓄力条 | L73 作为 Actor 子物体会继承角色根节点的 Transform；需在 PlayMode 验证左右转向时蓄力条的朝向和位置 | 待验收 |

本轮没有新增 DB/save、资源地址、全局事件、统计、广告、IAP、RemoteConfig 或平台 SDK。仍须在 Unity PlayMode 和 Android/iOS 横屏确认触摸区域、蓄力高度、动画循环、暂停恢复以及场景重载后的按钮交互。

## 2026-07-17 第三轮风险评估

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 进度条缩放导致渐变素材被压扁 | `Sp_l73_jd_01` 固定原始 Transform；新增本关 `ChargeFillMask` 仅缩放遮罩并从底边对齐，填充 Renderer 改为 `VisibleInsideMask` | YAML 引用、排序和代码静态已核对，PlayMode 画面待验收 |
| 遮罩误裁切底图或边框 | 遮罩自定义排序范围限定为层 2、Order 1；底图 Order 0、边框 Order 2 均保持不参与 MaskInteraction | 静态已核对，Unity 渲染待验收 |
| 满蓄力终点飞行仍显得过快或过低 | 本关 `DOJump` 参数改为 2 秒、弧高 5.5；不修改共享 DOTween 或角色控制器 | 参数及终点跨度静态已核对，手感待验收 |

本轮没有改变普通短按、未满蓄力跳、EndPoint 成功调用、共享输入、HUD、相机、加载、存档或其他关卡。仍需在 Unity PlayMode 与 Android/iOS 横屏确认遮罩完整显示、满蓄力直达终点、重开和退出后状态恢复。

## 2026-07-17 第四轮风险评估

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| Actor 的 SortingGroup 使遮罩范围完全裁空填充 | 关闭本关遮罩的 Custom Range；只有填充图设置为 `VisibleInsideMask`，因此其他角色与进度条层不受影响 | YAML 与代码静态已核对，重新进入 Play Mode 待验收 |
| Alpha Cutoff 为 1 导致压缩或抗锯齿像素无法写入遮罩 | 本关将 Cutoff 降至 0.01；源图存在完全不透明像素，且低阈值能保留边缘 | 源图像素静态已核对，画面待验收 |
| 角色入场透明度污染本关淡入目标值 | 淡入淡出固定写入 L73 三个 Renderer 的 0 到 1，不读取或修改 Actor 的共享透明度组件 | 代码静态已核对，入场后长按待验收 |
| 淡出回调在下一次蓄力后错误关闭面板 | 本关淡出 Tween 使用版本号；新的淡入会终止旧 Tween 并递增版本 | 代码静态已核对，连续蓄力待验收 |

当前 Unity 运行实例尚未重新加载本轮脚本，必须停止后重新进入 073 才能进行视觉验收。没有修改共享角色、HUD、镜头、加载、存档或其他关卡。

## 2026-07-17 第五轮风险评估

| 风险 | 处理方式 | 验证状态 |
| --- | --- | --- |
| 满蓄力临近 EndPoint 时横向速度被缓出曲线压低 | 仅将本关 `DOJump` 的 Ease 改为 `Linear`；飞行时长和跳跃高度不变 | 代码静态已核对，重新进入 Play Mode 待验收 |
| 调整 Ease 改变整体横向速度 | 保持 `endpointFlightDuration=2`，线性曲线的平均速度与原时长目标一致，仅移除末段过度减速 | 参数静态已核对，手感待验收 |

本轮不修改普通蓄力跳的 Rigidbody2D 速度、ActorCtrl、DOTween 全局设置或其它关卡。

## 回滚路径

移除 Level073 prefab 上的两个新增组件并删除 `Level_73` 本地文件；`L73` 保持隐藏，原有 GamePlayPanel、ActorCtrl、相机和关卡内容都无需回退。
