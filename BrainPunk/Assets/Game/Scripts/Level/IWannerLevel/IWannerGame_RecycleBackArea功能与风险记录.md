# IWannerGame_RecycleBackArea 功能与风险记录

## 需求与原始基线

- IWanner 关卡需要在 Collider2D 进入指定 Trigger 区域后，按 Layer 筛选并销毁或隐藏该对象。
- 本地代码没有同职责的 LayerMask 回收区域组件，因此新增 IWanner 局部脚本，不修改主线或 DrawGame。

## 影响范围

- 新增：`IWannerGame_RecycleBackArea.cs` 及其 Unity `.meta`。
- 未修改或绑定任何关卡 prefab；未新增配置、存档、资源加载、事件、Analytics、广告或平台 SDK。

## 修改后行为

1. 组件挂在带 Collider2D 的回收区域 GameObject 上，由 Unity `OnTriggerEnter2D` 驱动。
2. `targetLayers` 默认 Nothing；只有进入 Collider 所在 GameObject 的 Layer 被选中时才处理。
3. `Destroy` 模式调用 `Destroy(other.gameObject)`；`Hide` 模式调用 `other.gameObject.SetActive(false)`。
4. 处理范围固定为进入 Trigger 的 Collider 所在 GameObject，不自动选择父节点、Rigidbody 根对象或 prefab 根节点。
5. `Awake()` 只核对本对象是否存在 Collider2D 且开启 Trigger，不在编辑器态自动修改 prefab 数据。

## 验证

- 新脚本显式加入生成工程后的 Runtime 定向构建：`0 error / 102 warning`；Editor 定向构建：
  `0 error / 4 warning`。验证后已移除临时 csproj 改动，等待 Unity 正常刷新生成工程。
- 静态确认脚本没有 Update、协程、集合分配、DB、事件总线或平台分支；当前未进行 Unity PlayMode 或真机验证。

## 剩余风险与未验证项

- `uncertain`：Collider 位于子节点时，当前只回收子节点而不是父级主体；使用方必须把目标 Layer 和 Collider 放在
  希望被回收的 GameObject 上。
- `Hide` 不会重置位置、速度、动画或自定义状态，重新启用前应由对象所有者完成恢复。
- `Destroy` 在帧末生效；同一对象若有多个 Collider，当前不建立额外去重集合，但 Unity 重复 Destroy 调用不会创建
  新对象或额外持久状态。
- Unity Layer 碰撞矩阵、区域 Collider/Rigidbody2D 配置、PlayMode 实际触发与 Android/iOS 尚未验证。

## 回滚路径

- 从使用方 GameObject 移除该组件，并删除脚本与 `.meta` 即可；没有数据迁移。
