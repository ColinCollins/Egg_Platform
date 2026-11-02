# Editor 态开发约束

## 核心规则

- `OnValidate` 默认禁止访问任何 `DB` 数据链路。
- `OnValidate`、`Reset`、`[ExecuteAlways]`、`[ExecuteInEditMode]`、编辑器 `OnGUI`、prefab 预览回调中，禁止直接或间接触发运行时存档/配置链路。
- 编辑器模式下、非 runtime 状态，极力避免任何 instance 创建行为；默认按禁止处理。

## 明确禁止的内容

- 禁止在上述编辑器生命周期中访问 `DBManager`、`DB.GameData`、`DB.GameSetting`、`SaveManager`。
- 禁止通过 `RemoteConfig -> DB.GameData.RemoteConfigCache` 之类的间接路径读取数据。
- 禁止调用任何可能触发懒加载单例创建的入口，例如 `XXXManager.Instance`、运行时 service locator、运行时 bootstrap。
- 禁止在非 runtime 的编辑器流程中执行 `new GameObject`、`Instantiate`、`ScriptableObject.CreateInstance`，或其他会制造运行时实例的逻辑。

## 允许的编辑器预览来源

- 当前组件已序列化字段。
- 纯静态常量。
- 已存在的本地配置资源或只读 asset。
- 明确经过 `Application.isPlaying` 外层防护后的运行时代码分支。

## 防护要求

- 如果历史代码暂时无法完全拆掉相关调用，必须在最外层入口先做 `Application.isPlaying` 防护，再进入任何运行时数据逻辑。
- 防护必须写在 `OnValidate` 或对应编辑器回调的入口处，不能把风险留给深层方法自己兜底。
- 即使做了防护，编辑器预览也必须提供纯本地 fallback，不能依赖运行时 `DB`、缓存或单例初始化成功。

## 推荐写法

```csharp
private void OnValidate()
{
    NormalizeInspectorData();

    if (!Application.isPlaying)
    {
        RefreshPreviewFromLocalData();
        return;
    }

    RefreshRuntimeDependentState();
}
```

## 审查清单

- 看到 `OnValidate` 时，优先检查是否有 `DB`、`Save`、`RemoteConfig`、`Instance`、`Instantiate`。
- 看到编辑器预览需求时，优先改为本地字段/本地配置 fallback，不要接运行时链路。
- 看到单例或 manager 访问时，确认它不会在 edit mode 下初始化实例。

## 本次问题归纳

- `HorizontalRandomAdRewardCtrl.OnValidate()` 预览阶段不允许再通过 `RemoteConfig` 触发 `DB.GameData` 链路。
- 类似问题的根因不是“读取数据本身”，而是“编辑器非 runtime 状态把运行时实例顶出来了”。
- 后续新增或重构 `Validate` 相关逻辑时，按本规则执行，不做例外默认。
