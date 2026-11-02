# Bear StateMachine 模块文档

## 概述

Bear StateMachine 是一个基于特性的状态机框架，用于 Unity 游戏开发。该模块提供了灵活的状态管理机制，支持通过特性标记状态节点，实现状态之间的切换和管理。

**版本**: 0.0.7  
**Unity 版本**: 2022.3.18f1  
**命名空间**: `Bear.Fsm`

## 目录结构

```
Fsm/
├── Runtime/                    # 运行时核心代码
│   ├── StateMachine/          # 状态机核心实现
│   │   ├── Interface/         # 接口定义
│   │   │   ├── IBearMachine.cs
│   │   │   ├── IBearMachineOwner.cs
│   │   │   └── IStateMachineBaseNode.cs
│   │   ├── StateMachine.cs    # 状态机主类
│   │   ├── StateNode.cs       # 状态节点基类
│   │   ├── MonoBaseNode.cs    # Mono 状态节点
│   │   ├── StateMachineNodeAttribute.cs  # 状态节点特性
│   │   └── com.bear.package.statemachine.asmdef
│   └── Scenes/                # 示例场景
└── Example/                    # 使用示例
    ├── MainGame.cs            # 状态机使用示例
    ├── MainGame_StartGame.cs  # 开始游戏状态
    ├── MainGame_PlayingGame.cs # 游戏中状态
    └── GameState.cs           # 状态常量定义
```

## 核心组件

### 1. StateMachine（状态机主类）

**位置**: `Runtime/StateMachine/StateMachine.cs`

状态机的核心控制器，负责管理状态节点的注册、切换和执行。

#### 主要功能

- **状态注册**: 通过 `Inject()` 方法注册状态节点类型
- **状态应用**: 通过 `Apply()` 方法应用状态机到指定类型
- **状态切换**: 通过 `Enter()` 方法切换到指定状态
- **状态执行**: 通过 `Update()` 和 `Execute()` 方法执行当前状态逻辑
- **状态查询**: 通过 `IsRunning()` 方法检查当前是否处于指定状态

#### 关键方法

```csharp
// 注册状态节点类型
public void Inject(params Type[] types)

// 应用状态机到指定类型
public void Apply(Type _type)

// 进入指定状态
public virtual void Enter(string state)

// 更新当前状态
public virtual void Update()

// 执行当前状态
public virtual void Execute()

// 检查是否处于指定状态
public virtual bool IsRunning(string state)

// 获取当前状态节点
public IStateMachineBaseNode GetCurrent()

// 释放资源
public virtual void Dispose()
```

### 2. StateNode（状态节点基类）

**位置**: `Runtime/StateMachine/StateNode.cs`

所有状态节点的抽象基类，定义了状态节点的生命周期方法。

#### 生命周期方法

- `OnEnter()`: 进入状态时调用
- `OnExecute()`: 执行状态逻辑时调用
- `OnUpdate()`: 每帧更新时调用
- `OnFixUpdate()`: 固定更新时调用
- `OnLateUpdate()`: 延迟更新时调用
- `OnExit()`: 退出状态时调用

#### 关键方法

```csharp
// 设置状态机拥有者
public void SetOwner(IBearMachineOwner owner)
```

### 3. StateMachineNode（状态节点特性）

**位置**: `Runtime/StateMachine/StateMachineNodeAttribute.cs`

用于标记状态节点的特性，定义状态节点所属的状态机和状态名称。

#### 特性参数

- `Owner`: 状态机拥有者类型
- `State_Name`: 状态名称（字符串常量）
- `IsDefault`: 是否为默认状态

#### 使用示例

```csharp
[StateMachineNode(typeof(MainGame), GameState.STARTGAME, true)]
public class MainGame_StartGame : StateNode
{
    // 状态实现
}
```

### 4. MonoBaseNode（Mono 状态节点）

**位置**: `Runtime/StateMachine/MonoBaseNode.cs`

继承自 `StateNode`，提供 Unity Transform 引用的状态节点基类。

## 接口定义

### IBearMachineOwner

**位置**: `Runtime/StateMachine/Interface/IBearMachineOwner.cs`

状态机拥有者接口，实现此接口的类可以作为状态机的控制对象。

### IStateMachineBaseNode

**位置**: `Runtime/StateMachine/Interface/IStateMachineBaseNode.cs`

状态节点接口，定义了状态节点的所有生命周期方法。

### IBearMachine

**位置**: `Runtime/StateMachine/Interface/IBearMachine.cs`

状态机接口（当前为空，预留扩展）。

## 使用示例

### 1. 定义状态常量

```csharp
namespace Bear.Fsm
{
    public class GameState
    {
        public const string STARTGAME = "STARTGAME";
        public const string PLAYINGGAME = "PLAYINGGAME";
    }
}
```

### 2. 创建状态机拥有者

```csharp
public class MainGame : MonoBehaviour, IBearMachineOwner
{
    private StateMachine _machine;

    void Awake()
    {
        _machine = new StateMachine(this);
        _machine.Inject(
            typeof(MainGame_StartGame), 
            typeof(MainGame_PlayingGame));
        _machine.Apply(GetType());
        
        _machine.Enter(GameState.PLAYINGGAME);
    }

    private void Update()
    {
        _machine?.Update();
    }

    private void OnDestroy()
    {
        _machine?.Dispose();
    }
}
```

### 3. 创建状态节点

```csharp
[StateMachineNode(typeof(MainGame), GameState.STARTGAME, true)]
public class MainGame_StartGame : StateNode
{
    public override void OnEnter()
    {
        Debug.Log("Enter StartGame state");
    }

    public override void OnExecute()
    {
        Debug.Log("Execute StartGame state");
    }

    public override void OnUpdate()
    {
        Debug.Log("Update StartGame state");
    }

    public override void OnExit()
    {
        Debug.Log("Exit StartGame state");
    }
}
```

### 4. 在状态中访问拥有者

```csharp
[StateMachineNode(typeof(MainGame), GameState.PLAYINGGAME)]
public class MainGame_PlayingGame : StateNode
{
    public override void OnEnter()
    {
        var game = _owner as MainGame;
        Debug.Log($"PlayingGame state | {game.name}");
    }
}
```

## 工作流程

1. **初始化阶段**
   - 创建 `StateMachine` 实例，传入实现 `IBearMachineOwner` 的对象
   - 使用 `Inject()` 注册所有状态节点类型
   - 使用 `Apply()` 应用状态机到指定类型

2. **状态切换**
   - 使用 `Enter()` 方法切换到指定状态
   - 系统会自动调用旧状态的 `OnExit()` 和新状态的 `OnEnter()`

3. **状态执行**
   - 在 `Update()` 中调用状态机的 `Update()` 方法
   - 状态机会自动调用当前状态的 `OnUpdate()` 方法

4. **资源清理**
   - 在对象销毁时调用 `Dispose()` 方法释放资源

## 特性说明

- **基于特性**: 使用 `[StateMachineNode]` 特性标记状态节点，无需手动注册
- **类型安全**: 通过类型系统确保状态节点的正确性
- **生命周期管理**: 提供完整的生命周期方法，支持状态进入、执行、更新、退出
- **拥有者模式**: 通过 `IBearMachineOwner` 接口实现状态节点与拥有者的解耦
- **默认状态**: 支持设置默认状态，状态机应用时自动进入

## 注意事项

1. 状态节点类必须继承自 `StateNode`
2. 状态节点必须使用 `[StateMachineNode]` 特性标记
3. 状态名称建议使用常量定义，避免硬编码
4. 状态机拥有者必须实现 `IBearMachineOwner` 接口
5. 记得在 `Update()` 中调用状态机的 `Update()` 方法
6. 记得在对象销毁时调用 `Dispose()` 方法

## 扩展建议

1. **状态转换条件**: 可以扩展状态机，支持条件转换
2. **状态历史**: 可以添加状态历史记录功能
3. **状态事件**: 可以添加状态切换事件通知
4. **可视化编辑器**: 可以开发 Unity 编辑器工具，可视化编辑状态机

## 更新日志

详见 [Changelog.md](./Changelog.md)

---

**最后更新**: 2024年  
**维护者**: Bear StateMachine 团队

