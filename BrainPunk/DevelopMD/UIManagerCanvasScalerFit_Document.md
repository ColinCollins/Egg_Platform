# UIManagerCanvasScalerFit 功能文档与优化方案

> 对应脚本：`Assets/Game/Scripts/Common/ResolutionFit/UIManagerCanvasScalerFit.cs`

---

## 一、功能概述

`UIManagerCanvasScalerFit` 是一个运行时 Canvas 适配管理器，挂载于场景中的某个管理节点上。它会遍历指定根节点下的所有 `CanvasScaler`，根据当前屏幕的**宽高比（Aspect Ratio）**动态计算 `CanvasScaler.matchWidthOrHeight`，并可选择性地切换 `referenceResolution`。

### 核心职责

| 职责 | 说明 |
|------|------|
| **动态 Match 计算** | 根据屏幕比例相对于参考比例的位置，在 `[minMatch, maxMatch]` 范围内插值得到 `matchWidthOrHeight` |
| **分辨率切换** | 当屏幕比例超过阈值时，使用预设列表中的另一套 `referenceResolution`（如从 16:9 切换到 21:9 的参考值） |
| **自动刷新** | 支持启动时应用，并在运行时分辨率变化后自动更新 |

---

## 二、关键字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| `targetRoot` | `Transform` | 搜索 CanvasScaler 的根节点，为空时默认使用自身 `transform` |
| `applyOnStart` | `bool` | 是否在 `Start` 时自动执行适配 |
| `autoUpdateOnResolutionChange` | `bool` | 是否在 `Update` 中监听分辨率变化并自动刷新 |
| `aspectRatioThreshold` | `float` | 屏幕比例切换阈值（默认 `2.17f`，约 21:9） |
| `fixResolutionList` | `List<Vector2>` | 预设的参考分辨率列表，索引 0 用于窄屏，索引 1 用于宽屏 |
| `minMatch` / `maxMatch` | `float [0,1]` | 限制 `matchWidthOrHeight` 的最终输出范围 |

---

## 三、核心算法

```
screenAspect = Screen.width / Screen.height

// 1. 选择参考分辨率
index = (screenAspect > aspectRatioThreshold) ? 1 : 0
referenceResolution = fixResolutionList[index]  或 scaler 自身值

// 2. 计算插值系数
referenceAspect = referenceResolution.x / referenceResolution.y
rawMatch = InverseLerp(referenceAspect * 0.75, referenceAspect * 1.25, screenAspect)

// 3. 映射到用户指定范围
match = Lerp(minMatch, maxMatch, rawMatch)
```

### 插值区间解释

- 屏幕比例 < 参考比例的 **75%**：`match = minMatch`（完全以宽度为准）
- 屏幕比例 > 参考比例的 **125%**：`match = maxMatch`（完全以高度为准）
- 两者之间：线性过渡

---

## 四、运行时行为

1. **Awake**：若 `targetRoot` 未指定，则默认使用自身 `transform`
2. **Start**：若 `applyOnStart` 为 `true`，立即执行一次全量刷新
3. **Update**：若 `autoUpdateOnResolutionChange` 为 `true`，每帧检查 `Screen.width/height` 是否变化，变化则重新刷新
4. **RefreshAllCanvasScalerMatch**：
   - 在 `targetRoot` 下递归查找所有 `CanvasScaler`（包含隐藏对象）
   - 根据当前屏幕比例确定应使用的参考分辨率
   - 逐一对每个 `CanvasScaler` 计算并设置 `matchWidthOrHeight`

---

## 五、现存问题与优化方案

### 问题 1：Update 每帧轮询 Screen 尺寸

**现状：** 即使分辨率未变化，仍在 `Update` 中执行整数比较。当 `targetRoot` 为 null 或找不到 Scaler 时，`isDirty` 置为 `true`，导致后续每帧持续重试空转。

**优化方案：**
- 移除 `isDirty` 机制，改为在异常情况下直接 `Debug.LogWarning` 并提前返回
- 使用事件驱动替代轮询，例如监听 `Canvas.onScreenSizeChanged` 或重写 `OnRectTransformDimensionsChange`
- 若必须轮询，最低限度应在检测到变化后通过一个标志位跳过无效重试

---

### 问题 2：硬编码的 ±25% 插值区间

**现状：**
```csharp
float rawMatch = Mathf.InverseLerp(referenceAspect * 0.75f, referenceAspect * 1.25f, screenAspect);
```
`0.75f` / `1.25f` 是魔法数字，不可配置。

**优化方案：**
- 提取为可序列化字段：
  ```csharp
  [SerializeField, Range(0.1f, 2f)] private float aspectToleranceMin = 0.75f;
  [SerializeField, Range(0.1f, 2f)] private float aspectToleranceMax = 1.25f;
  ```
- 方便不同项目根据 UI 设计敏感度调整适配区间

---

### 问题 3：分辨率档位扩展性差

**现状：** `fixResolutionList` 支持多元素，但切换逻辑只有 0/1 两个档位（由单一阈值 `aspectRatioThreshold` 决定），无法优雅支持三档及以上（如 4:3、16:9、18:9、21:9）。

**优化方案：**
- 将阈值也改为列表，与分辨率列表一一对应：
  ```csharp
  [SerializeField] private List<float> aspectRatioThresholds; // 上限阈值列表
  [SerializeField] private List<Vector2> fixResolutionList;
  ```
- 查找逻辑改为遍历阈值列表，找到第一个满足 `screenAspect <= threshold` 的档位

---

### 问题 4：直接修改 `scaler.referenceResolution` 的副作用

**现状：**
```csharp
if (useFixResolution)
    scaler.referenceResolution = referenceResolution;
```
运行时直接修改了 Scaler 的序列化值，退出 PlayMode 后若未还原可能造成编辑器状态污染。

**优化方案：**
- 在编辑器非播放模式下，修改前调用 `Undo.RecordObject(scaler, "Canvas Fit")`
- 或在编辑器模式下避免直接修改 `referenceResolution`，仅调整 `matchWidthOrHeight`
- 运行时修改可接受，但建议在 `OnDestroy` 或应用退出时无感还原（如记录原始值）

---

### 问题 5：未校验 `CanvasScaler.uiScaleMode`

**现状：** 如果子节点中的 `CanvasScaler` 使用 `ConstantPhysicalSize` 或 `ConstantPixelSize`，设置 `matchWidthOrHeight` 与 `referenceResolution` 无意义。

**优化方案：**
- 遍历过滤：
  ```csharp
  if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
      continue;
  ```

---

### 问题 6：缺少分 Canvas 的独立配置能力

**现状：** 所有 CanvasScaler 统一使用同一套 `minMatch/maxMatch` 和分辨率列表。某些特殊 Canvas（如全屏背景、弹窗、SafeArea 面板）可能需要不同的适配策略。

**优化方案：**
- 引入 `FitProfile` 结构体，允许按 Canvas 名称、Tag 或类型应用不同的适配策略
- 或通过配置表将特定 Canvas 路径与 Profile 绑定

---

### 问题 7：`targetRoot` 为 null 时的延迟绑定不够明确

**现状：** `Awake` 中若 `targetRoot` 为 null 则回退到 `transform`，但 `RefreshAllCanvasScalerMatch` 中再次检查 null 并设置 `isDirty = true`，两处逻辑不一致。

**优化方案：**
- 统一在 `Awake` / `OnEnable` 中完成初始化绑定
- 若确实需要外部延迟设置 `targetRoot`，应提供显式 `SetTargetRoot(Transform)` 接口，由调用方在准备好后手动触发 `Refresh`

---

## 六、建议的重构代码片段

```csharp
// 1. 新增可配置字段
[SerializeField, Range(0.1f, 2f)] private float aspectToleranceMin = 0.75f;
[SerializeField, Range(0.1f, 2f)] private float aspectToleranceMax = 1.25f;

// 2. 过滤无效 Scaler
if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
    continue;

// 3. 多档分辨率查找（示例）
int index = fixResolutionList.Count - 1;
for (int i = 0; i < aspectRatioThresholds.Count; i++)
{
    if (screenAspect <= aspectRatioThresholds[i])
    {
        index = i;
        break;
    }
}
```

---

## 七、总结

当前脚本在功能上覆盖了常见的横屏多分辨率适配需求，但存在**硬编码参数、每帧轮询、副作用修改、扩展性不足**等问题。

**建议优先处理：**
1. **移除 Update 轮询**：改为事件驱动或显式调用，减少空转开销
2. **避免副作用修改**：区分 Editor 与 Runtime 修改策略，防止污染编辑器状态
3. **参数外置**：将 `0.75/1.25` 提取为可配置字段，提升项目间复用性
