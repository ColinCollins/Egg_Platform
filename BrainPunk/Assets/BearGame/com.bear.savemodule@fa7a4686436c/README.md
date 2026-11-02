# Bear SaveModule 功能设计文档

> 版本: 1.0.6  
> 依赖: Newtonsoft.Json, Unity 6000.0.59f2+

---

## 1. 模块概述

Bear SaveModule 是 Unity 项目的本地数据持久化模块，提供统一的存档管理、多存储后端支持（PlayerPrefs / Json 文件）、数据可视化调试以及自动化代码生成能力。

### 1.1 核心能力

| 能力 | 说明 |
|------|------|
| 统一存储接口 | 通过 `SaveManager` 屏蔽 PlayerPrefs 与 Json 文件的差异 |
| ScriptableObject 数据驱动 | 数据类继承 `BaseSaveDataSO`，天然支持 Inspector 可视化与 SO 工作流 |
| 自动代码生成 | 根据数据类字段自动生成 `DBManager` 访问代码与 Partial 初始化代码 |
| Editor 数据调试 | `DataViewer` 窗口支持实时查看、树形分析、逐条编辑 PlayerPrefs 与 Json 存档 |
| 服务端同步扩展 | 预留 `IServerSyncProvider` 接口，支持服务端数据同步 |

---

## 2. 架构设计

### 2.1 核心类图

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   DBManager     │────▶│ BaseSaveDataSO  │◀────│   DBSetting     │
│  (数据实例管理)  │     │  (数据基类)      │     │ (存储配置资产)   │
└────────┬────────┘     └────────┬────────┘     └─────────────────┘
         │                       │
         │                       │ Init() / Save() / FromJson()
         │                       ▼
         │              ┌─────────────────┐
         │              │  SaveManager    │
         └─────────────▶│ (统一存储管理)   │
                        └────────┬────────┘
                                 │
                    ┌────────────┼────────────┐
                    ▼            ▼            ▼
            ┌──────────┐  ┌──────────┐  ┌──────────────┐
            │PlayerPrefs│  │  Json    │  │ ServerSync   │
            │Provider  │  │ Provider │  │ (Extension)  │
            └──────────┘  └──────────┘  └──────────────┘
```

### 2.2 数据流

```
运行时加载:
DBManager.Initialize() → 扫描所有 BaseSaveDataSO 子类
                       → 创建 ScriptableObject 实例
                       → 根据 StorageType 选择 Provider 加载数据
                       → 有存档则 FromJson()，无存档则 Init() + Save()

运行时保存:
instance.Save() → SaveManager.Save(key, instance, StorageType)
                → Provider.Save(key, json)

Editor 查看:
DataViewer → 读取 PlayerPrefs / Json 文件 → 解析为 JToken → 树形展示
```

---

## 3. 核心功能详细设计

### 3.1 SaveManager — 统一存储管理器

- **职责**: 作为存储门面，管理所有 `ISaveProvider` 实例，提供同步/异步存取接口
- **单例模式**: 运行时自动创建 `DontDestroyOnLoad` 的 GameObject
- **自动初始化**: 首次调用时自动注册 `PlayerPrefsSaveProvider` 与 `JsonSaveProvider`
- **StorageType.Auto 处理**: 自动回落为 `StorageType.Json`

**关键 API**:

```csharp
bool Save<T>(string key, T data, StorageType storageType = StorageType.Auto)
T Load<T>(string key, StorageType storageType = StorageType.Auto)
bool HasKey(string key, StorageType storageType = StorageType.Auto)
bool Delete(string key, StorageType storageType = StorageType.Auto)
```

### 3.2 DBManager — 数据实例管理器

- **职责**: 运行时统一管理所有 `BaseSaveDataSO` 的 ScriptableObject 实例
- **生命周期**: `Initialize()` → `ScanAndInitializeDataClasses()` → 按需 `SaveAll()` / `ClearCache()`
- **泛型访问**: `Get<T>()`, `Save<T>()`, `SaveAll()`

**缓存清理**:
- `ClearCache(bool destroyInstances)` — 销毁 SO 实例并清空字典
- `ClearPlayerPrefsCache(bool onlySaveModuleData)` — 清除 PlayerPrefs 存档
- `ClearJsonCache(bool onlySaveModuleData)` — 清除 Json 文件存档

### 3.3 BaseSaveDataSO — 数据基类

- **继承**: `ScriptableObject`
- **核心方法**:
  - `Init()` — 初始化默认值（子类在 Partial 中实现）
  - `ToJson()` / `FromJson(string json)` — 基于 Newtonsoft.Json 的序列化/反序列化
  - `Save()` / `SaveAsync()` — 快捷保存到存储系统
- **存储类型声明**: 子类通过 `public static StorageType StorageType = StorageType.Json;` 声明存储方式

### 3.4 ISaveProvider — 存储提供者接口

定义统一的存取契约，支持扩展自定义存储后端（如 SQLite、加密存储等）。

**已实现 Provider**:

| Provider | 存储路径 | 适用场景 |
|----------|----------|----------|
| `PlayerPrefsSaveProvider` | `PlayerPrefs` (`SaveData_{key}`) | 小数据量、高频读取、移动端兼容 |
| `JsonSaveProvider` | `{persistentDataPath}/SaveData/{key}.json` | 大数据量、需要文件级操作 |

### 3.5 StorageType — 存储方式枚举

```csharp
public enum StorageType
{
    PlayerPrefs,  // PlayerPrefs 存储
    Json,         // Json 文件存储
    Auto          // 自动选择（实际回落为 Json）
}
```

---

## 4. Editor 工具设计

### 4.1 DataViewer — 数据查看器

**入口**: `Tools > DataViewer`

#### 4.1.1 双 Tab 架构

| Tab | 数据来源 | 展示形式 |
|-----|----------|----------|
| PlayerPrefs | `PlayerPrefs.GetString()` | 卡片列表，每个卡片内树形展示 JSON 结构 |
| Json Files | `{persistentDataPath}/SaveData/*.json` | 卡片列表，每个卡片内树形展示文件内容 |

#### 4.1.2 JSON 树形结构分析（查看模式）

- **解析**: 使用 `Newtonsoft.Json.Linq.JToken.Parse()` 将 Value/Content 解析为 token 树
- **递归渲染**: `DrawJToken()` 根据 `JTokenType` 分发到 Object / Array / Value 渲染器
- **嵌套 JSON 字符串自动展开**: 若 `JValue` 为字符串且内容以 `{` 或 `[` 开头，尝试二次解析并递归展示
- **类型高亮**:

| 类型 | 颜色 | 标注 |
|------|------|------|
| string | `#72E772` 绿色 | `"value" (string)` |
| int / float | `#6BC7F7` 蓝色 | `123 (int)` / `(float)` |
| bool | `#F7C66B` 橙色 | `true (bool)` |
| null | `#9E9E9E` 灰色 | `null (null)` |
| date | `#D78CF0` 紫色 | `(date)` |

- **折叠状态**: `Dictionary<string, bool> _jsonFoldoutStates`，key 格式为 `{fileName}::{jsonPath}` 或 `pp:{key}::{jsonPath}`，**默认折叠**

#### 4.1.3 编辑面板（重点优化）

点击卡片上的 **编辑** 按钮后，窗口底部弹出编辑面板。

**面板特性**:
- **高度可调**: 面板顶部有 6px 灰色拖拽条，按住上下拖动可调整面板高度（范围 120px ~ 窗口高度 - 150px）
- **双模式切换**:
  - **树形结构**（默认）：按 JSON 层级展示，每个字段旁内联输入框
  - **原始 JSON**：传统多行文本编辑

**树形编辑模式**:
- 使用独立的 `_editScrollPosition`，避免与主列表滚动冲突
- ScrollView 高度动态计算: `Mathf.Max(60f, _editPanelHeight - 110f)`
- 字段按类型渲染不同输入控件:

| 类型 | 控件 | 修改方式 |
|------|------|----------|
| string | `EditorGUILayout.TextField` | 直接修改 `jVal.Value` |
| integer | `EditorGUILayout.LongField` | 直接修改 `jVal.Value` |
| float | `EditorGUILayout.DoubleField` | 直接修改 `jVal.Value` |
| boolean | `EditorGUILayout.Toggle` | 直接修改 `jVal.Value` |
| null | `LabelField("null")` | 只读 |

- 折叠状态: `Dictionary<string, bool> _editFoldoutStates`，key 前缀为 `edit`，**默认折叠**
- 保存时: 若处于树形模式，将 `_editingJToken` 序列化为 `Formatting.None` 的 JSON 字符串后写入

#### 4.1.4 通用操作

| 操作 | 说明 |
|------|------|
| 刷新 | 重新扫描 PlayerPrefs 键与 Json 文件目录 |
| 搜索 | 按文件名/键名/内容文本过滤 |
| 删除选中 | 删除当前选中的单条数据 |
| 删除所有 | 清空当前 Tab 下的所有数据（带确认弹窗） |
| 打开保存目录 | Json Tab 专属，在资源管理器中打开 `{persistentDataPath}/SaveData` |
| 打开 | Json Tab 专属，在资源管理器中定位到具体文件 |

### 4.2 DBSettingEditor — DB Setting 管理器

**入口**: `Tools > Save Module > DB Setting Manager`

- **DBSetting 资产**: 记录项目中所有 `BaseSaveDataSO` 子类的存储方式配置
- **扫描功能**: 一键扫描所有 Assembly 中的 `BaseSaveDataSO` 子类，自动注册到 `DBSetting.DataClasses`
- **存储方式修改**: 在列表中直接修改每个数据类的 `StorageType`（PlayerPrefs / Json / Auto）
- **Apply Changes**: 自动向数据类脚本中注入/修改 `public static StorageType StorageType = ...;` 静态字段，并重新生成 `DBManager` 访问代码

### 4.3 代码生成工具

| 工具 | 文件 | 功能 |
|------|------|------|
| `DBManagerGenerator` | `Runtime/Editor/DBManagerGenerator.cs` | 根据 `DBSetting` 生成 `DBManager` 的泛型访问代码 |
| `PartialClassGenerator` | `Runtime/Editor/PartialClassGenerator.cs` | 为数据类生成 `Partial` 初始化代码与属性访问器 |
| `SaveDataScriptGenerator` | `Runtime/Editor/SaveDataScriptGenerator.cs` | 根据字段定义生成完整的 `BaseSaveDataSO` 子类代码模板 |

---

## 5. 目录结构

```
com.bear.savemodule/
├── Changelog.md                          # 版本变更记录
├── package.json                          # UPM 包配置
├── Example/                              # 使用示例
│   ├── README.md
│   ├── SaveSystemUsageExample.cs
│   └── DB/
│       ├── ExampleData.cs
│       ├── ExampleData_Partial.cs
│       ├── PlayerDataExample.cs
│       └── PlayerDataExample_Partial.cs
└── Runtime/
    ├── com.bear.savemodule.asmdef        # Runtime 程序集定义
    ├── Core/                             # 核心管理层
    │   ├── SaveManager.cs                # 统一存储管理器
    │   ├── DBManager.cs                  # 数据实例管理器
    │   └── ISaveProvider.cs              # 存储提供者接口
    ├── Data/                             # 数据定义层
    │   ├── BaseSaveDataSO.cs             # 数据基类
    │   ├── DBSetting.cs                  # 存储配置资产
    │   ├── SaveData.cs                   # 存档数据封装
    │   └── StorageType.cs                # 存储方式枚举
    ├── Providers/                        # 存储实现层
    │   ├── JsonSaveProvider.cs           # Json 文件存储
    │   └── PlayerPrefsSaveProvider.cs    # PlayerPrefs 存储
    ├── Extensions/                       # 扩展接口
    │   ├── IServerSyncProvider.cs        # 服务端同步接口
    │   └── ServerSyncProvider.cs         # 服务端同步实现基类
    └── Editor/                           # Editor 工具层
        ├── com.bear.savemodule.Editor.asmdef
        ├── DataViewer.cs                 # 数据查看器（含树形展示与编辑）
        ├── DBSettingEditor.cs            # DBSetting 配置管理窗口
        ├── DBManagerGenerator.cs         # DBManager 代码生成器
        ├── PartialClassGenerator.cs      # Partial 类代码生成器
        ├── PartialClassGeneratorSettings.cs
        └── SaveDataScriptGenerator.cs    # 数据类代码模板生成器
```

---

## 6. 使用流程

### 6.1 定义数据类

```csharp
public partial class GameData : BaseSaveDataSO
{
    public static StorageType StorageType = StorageType.Json;
    
    public int CurrentLevel;
    public int MaxLevel;
    public List<int> UnlockLevels;
}
```

### 6.2 运行时初始化与存取

```csharp
// 初始化
DBManager.Instance.Initialize();

// 读取
var gameData = DBManager.Instance.Get<GameData>();

// 修改并保存
gameData.CurrentLevel++;
gameData.Save();

// 或统一保存
DBManager.Instance.SaveAll();
```

### 6.3 Editor 调试

1. 打开 `Tools > DataViewer`
2. 在 **Json Files** 或 **PlayerPrefs** Tab 中查看存档数据
3. 点击卡片上的 **编辑** 按钮，在底部树形编辑器中逐条修改值
4. 点击 **保存** 写回存档文件

---

## 7. 注意事项

- `OnValidate`、`Reset`、`[ExecuteAlways]` 等 Editor 生命周期方法中**禁止**直接访问 `DBManager` 或触发存档加载链
- Editor 预览逻辑必须使用本地序列化数据、常量或本地只读资源，不可依赖运行时单例初始化
- `BaseSaveDataSO` 子类的 `Init()` 方法用于设置默认值，应在其中初始化所有字段以避免 null 引用
- `PlayerPrefsSaveProvider` 使用 `SaveData_` 前缀隔离 SaveModule 数据与 Unity 系统 PlayerPrefs
