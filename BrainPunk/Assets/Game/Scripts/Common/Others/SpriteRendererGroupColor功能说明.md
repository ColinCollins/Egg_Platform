# SpriteRendererGroupColor 功能说明

## 需求与边界

- 新增可复用的 `SpriteRendererGroupColor`，结构与既有 `SpriteRendererGroupAlpha` 保持一致：支持自动收集子节点，也支持 Inspector 手工配置 Renderer 数组，并提供运行时公开 Set/Get API。
- 组件只统一写入 RGB，保留每个 `SpriteRenderer` 当前 Alpha，因此可与 `SpriteRendererGroupAlpha` 或其它透明度逻辑组合使用。
- 不访问 DB、Save、配置、资源加载或全局事件，不创建运行时对象。

## 行为

- `Awake()`：按 `autoCollectRenderers` 决定是否收集当前节点及子节点 Renderer，随后应用当前颜色。
- `SetColor(Color)`：更新缓存颜色并同步写入所有已配置 Renderer；传入 Alpha 被忽略。
- `CollectRenderers()`：按 `includeInactive` 重新收集，调用时会覆盖 Inspector 手工列表。
- `OnValidate()`：只处理本地序列化字段与 Renderer 预览，不访问运行时单例或数据链。

## Level71 使用

- `Level71Ctrl.environmentColorGroup` 已暴露组件引用，并在每次更新昼夜颜色时调用 `SetColor(...)`。
- 当前 Unity Hot Reload 未把新增脚本即时纳入编辑器工程，写入 Prefab 的未知组件会被导入器剥离；因此 `Level_071.prefab` 暂不强写该组件引用。完整刷新或重启 Unity 后，需要在合适的美术根节点添加组件并绑定给 `Level71Ctrl`。
- 为避免再次误判美术语义，建议关闭自动收集并在 `renderers` 数组人工加入需要参与昼夜渐变的场景 SpriteRenderer。
- `Level71Ctrl` 每次计算昼夜颜色后，同时写入 `CommonBg/Bg` 与该颜色组；颜色组不应重复包含 `CommonBg/Bg`，也不应包含 `Sun` 或 `Moon`。

## 验证、风险与回滚

- 将新脚本临时纳入 Unity 生成的 `Assembly-CSharp.csproj` 后执行定向构建，结果为 `0 error / 101 个项目既有 warning`；验证结束后已撤回生成项目文件的临时项。
- 待验证：Unity Inspector 手工配置多个 Renderer 后，夜晚色到白天色的同步渐变、各成员原 Alpha 保持，以及 Android/iOS 横屏运行性能。
- 每帧只遍历固定数组并写 Color，不产生托管分配；实际成本随组成员数量线性增长。
- 回滚时移除 `Level71Ctrl.environmentColorGroup` 字段与调用，再删除本脚本及文档；原 `CommonBg/Bg` 单独渐变不受影响。
