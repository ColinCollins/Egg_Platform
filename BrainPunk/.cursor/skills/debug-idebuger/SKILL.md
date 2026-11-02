---
name: debug-idebuger
description: >-
  Prefer Bear.Logger IDebuger extension logging (this.Log / LogWarning / LogError)
  over UnityEngine.Debug when adding debug or diagnostic output in this Unity
  project. Use when writing Debug.Log, troubleshooting, metrics, or verbose traces
  in Game or Bear-related code.
---

# Debug 输出优先 IDebuger（Bear.Logger）

## 原则

- 需要**调试数据、过程量、临时排查**时，**优先**让当前类型实现 **`Bear.Logger.IDebuger`**，并用 **`this.Log` / `this.LogWarning` / `this.LogError` / `this.LogColor`**。
- **不要默认**在新逻辑里堆 **`UnityEngine.Debug.Log`**；除非有明确理由（见下方「例外」）。

## 写法

1. `using Bear.Logger;`
2. 类声明末尾增加 **`IDebuger`**（与其它接口并列即可）。接口**无成员**，无需额外实现。
3. 在实例方法里输出：**`this.Log($"…")`**。日志会带 **`[类型名]-`** 前缀，格式与 `PurchaseManager`、`GamePlayPanel` 等一致。

示例：

```csharp
using Bear.Logger;
using UnityEngine;

public class MyCtrl : MonoBehaviour, IDebuger
{
    private void Foo()
    {
        this.Log($"state={x}");
    }
}
```

## 与 `DEBUG_MODE`（条件编译）

- `BearLogger` 对 `IDebuger` 的扩展方法带有 **`[Conditional("DEBUG_MODE")]`**。
- **未**在 Player Settings 的 Scripting Define Symbols 里启用 **`DEBUG_MODE`** 时，这些调用在**编译期会被移除**，正式包默认不产生 Bear 侧日志代码路径。
- 开发机需要 Bear 管道日志时：Unity 菜单 **Tools → Debug → Open**（或 **Open Current Platform Only**）打开符号。
- **Inspector 开关**（如 `debugDragMetrics`）只控制「是否值得打日志」；**若未定义 DEBUG_MODE，整条 `this.Log` 仍不会出现**，这是预期行为。

## 例外（可用 `UnityEngine.Debug`）

- **编辑器专用脚本**（`#if UNITY_EDITOR` 内）且明确不进包体。
- **必须**在未定义 `DEBUG_MODE` 的构建里也输出**单行关键信息**，且团队已认可污染 Console —— 须在注释写清原因。

## 相关

- Bear 包职责总览：**`beargame-packages`**（`com.bear.logger`、`DebugSetting` 等）。
