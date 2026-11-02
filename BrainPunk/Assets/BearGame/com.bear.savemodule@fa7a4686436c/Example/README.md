# 数据存储系统使用示例

## 快速开始

### 1. 使用编辑器工具创建 ScriptableObject 脚本

1. 在 Unity 编辑器中，右键点击项目窗口
2. 选择 `Assets/Create/Save Data/New Save Data Script`
3. 输入脚本名称（如：`PlayerData`）
4. 添加字段：
   - Type: `int`, Name: `level`
   - Type: `string`, Name: `playerName`
   - Type: `float`, Name: `experience`
5. 点击 `Generate Script` 生成脚本

### 2. 生成 Partial 类（自动生成 get/set 属性）

1. 选择刚才创建的脚本文件
2. 选择菜单 `Assets/Create/Save Data/Generate Partial Class`
3. 查看生成的代码
4. 点击 `Save to File` 保存 partial 类

### 3. 配置 DBSetting（推荐）

1. 在 Unity 编辑器中，选择菜单 `Tools/Save Module/DB Setting Manager`
2. 创建或选择一个 DBSetting 资源
3. 点击 `Scan All BaseSaveDataSO Classes` 扫描所有数据类
4. 为每个数据类设置存储类型（PlayerPrefs 或 Json）
5. 点击 `Apply Changes` 应用更改（会自动生成 DBManager_Generated.cs）

### 4. 在代码中使用

**推荐方式：使用 DBManager**

```csharp
// 初始化 DBManager（会自动初始化 SaveManager）
DBManager.Instance.Initialize();

// 方式1: 使用 DB 静态类（推荐，需要先通过 DBSetting 扫描并生成代码）
DB.ExampleData.Number = 100;
DB.ExampleData.Save();
Debug.Log($"Number = {DB.ExampleData.Number}");

// 方式2: 使用 DBManager 获取数据实例（推荐）
var playerData = DBManager.Instance.Get<PlayerDataExample>();
playerData.Level = 10;
playerData.PlayerName = "TestPlayer";
playerData.Experience = 5000f;
playerData.Save();

// 方式3: 使用静态 Instance 属性（如果已生成 Partial 类并创建了资源文件）
// var playerData = PlayerDataExample.Instance;
// playerData.Level = 10;
// playerData.Save();

// 方式4: 创建数据实例（如果未生成资源文件）
// var playerData = ScriptableObject.CreateInstance<PlayerDataExample>();
// playerData.Level = 10;
// PlayerDataExample.StorageType = StorageType.Json;
// playerData.Save();

// 方式5: 使用 SaveManager 直接保存（不推荐，建议使用 DBManager）
// SaveManager.Instance.Save("PlayerData", playerData, StorageType.Json);
// var loadedData = SaveManager.Instance.Load<PlayerData>("PlayerData", StorageType.Json);
```

## 示例说明

### PlayerDataExample.cs
展示了使用编辑器工具生成的完整脚本结构，包括：
- 继承自 `BaseSaveDataSO`，自动获得保存功能
- 用户自定义的 private 字段
- 自动生成的 partial 类（包含 get/set 属性和静态 Instance）
- 静态 `StorageType` 字段（由 DBSetting 统一管理）
- 自动生成的 ScriptableObject 资源文件（生成 Partial 类时自动创建）

### SaveSystemUsageExample.cs
展示了各种使用场景：
1. **基本保存和加载** - 最简单的使用方式
2. **ScriptableObject 使用** - 使用继承自 `BaseSaveDataSO` 的类，演示基类的 `Save()` 方法
3. **异步操作** - 使用 async/await 进行异步保存和加载
4. **服务器同步** - 上传和下载数据到服务器

### ExampleData.cs / ExampleData_2.cs
展示了继承 `BaseSaveDataSO` 的简单示例：
- 只需定义静态 `StorageType` 字段
- 自动获得保存功能
- 可通过 DBSetting 统一管理存储方式
- 生成 Partial 类时会自动创建 ScriptableObject 资源文件

## 存储方式选择

- **PlayerPrefs**: 适合存储简单的配置数据，数据存储在系统注册表（Windows）或 plist 文件（Mac）
- **Json**: 适合存储复杂的数据结构，数据存储在持久化数据目录
- **Auto**: 自动选择（默认使用 Json）

## 注意事项

1. **DBManager 初始化**：建议在游戏启动时调用 `DBManager.Instance.Initialize()`，会自动初始化 SaveManager 并加载所有数据
2. **DBSetting 配置**：使用 DBSetting 统一管理所有数据类的存储类型，避免在代码中硬编码
3. **代码生成**：通过 DBSetting 扫描数据类后，点击 `Apply Changes` 会自动生成 `DBManager_Generated.cs`，提供 `DB` 静态类访问
4. **Partial 类生成**：生成 Partial 类时会自动创建 ScriptableObject 资源文件，可通过 `ClassName.Instance` 访问
5. **存储类型**：每个数据类需要定义静态 `StorageType` 字段，可通过 DBSetting 统一管理
6. **服务器同步**：服务器同步功能需要实现实际的服务器 API
7. **推荐使用方式**：
   - 优先使用 `DB.ClassName` 静态访问（需要先生成代码）
   - 其次使用 `DBManager.Instance.Get<T>()` 获取实例
   - 最后使用 `ClassName.Instance` 或 `ScriptableObject.CreateInstance<T>()`

