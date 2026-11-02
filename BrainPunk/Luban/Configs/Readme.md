用于记录数据表的设计思路

## 数据表分类

1. static
    用于存储游戏核心需要的数据
2. level_N
    游戏内的核心增长表，可以根据设计需求增添设计
3. item
    物品背包表

## 配置变量命名规则

### 1. Bean 类命名
- **规则**：使用 **PascalCase**（首字母大写的驼峰命名）
- **示例**：`GlobalConfig`, `DateTimeRange`, `ItemData`, `LevelConfig`
- **说明**：Bean 类名会直接映射到生成的 C# 类名

### 2. Bean 变量命名（XML/Excel 中）
- **规则**：使用 **snake_case**（小写字母 + 下划线）
- **示例**：`start_time`, `end_time`, `item_id`, `level_config`, `max_count`
- **说明**：Luban 会自动将 snake_case 转换为 C# 的 PascalCase 属性名
  - `start_time` → `StartTime`
  - `item_id` → `ItemId`

### 3. Table 表名命名
- **规则**：使用 **`Tb` 前缀 + PascalCase**
- **示例**：`TbGlobalConfig`, `TbItemData`, `TbLevelConfig`
- **说明**：表名用于生成配置管理器类，`Tb` 前缀表示 Table

### 4. Enum 枚举命名
- **规则**：使用 **`E` 前缀 + PascalCase**
- **示例**：`EBoolOperator`, `EItemType`, `ELevelStatus`
- **说明**：枚举值使用全大写，多个单词用下划线分隔
  - `AND`, `OR`
  - `ITEM_TYPE_WEAPON`, `ITEM_TYPE_CONSUMABLE`

### 5. 配置文件命名（Excel 文件名）
- **规则**：使用 **snake_case**（小写字母 + 下划线）
- **示例**：`level_2.xlsx`, `item.xlsx`, `global_config.xlsx`
- **说明**：文件名应清晰表达配置内容，避免使用中文文件名

### 6. Module 模块命名
- **规则**：使用 **小写字母**，多个单词用下划线分隔
- **示例**：`common`, `item`, `level`, `player_data`
- **说明**：模块名用于组织相关的 Bean、Table、Enum

### 命名规范总结

| 类型 | 命名规则 | 示例 | 生成 C# 代码 |
|------|---------|------|-------------|
| Bean 类 | PascalCase | `ItemData` | `public class ItemData` |
| Bean 变量 | snake_case | `item_id` | `public int ItemId { get; }` |
| Table 表 | `Tb` + PascalCase | `TbItemData` | `public class TbItemData` |
| Enum 枚举 | `E` + PascalCase | `EItemType` | `public enum EItemType` |
| 配置文件 | snake_case | `item_data.xlsx` | - |
| Module | snake_case | `item` | `namespace Config.item` |

### 注意事项

1. **避免使用缩写**：除非是通用缩写（如 `id`, `url`），否则使用完整单词
2. **保持一致性**：同一模块内的命名风格应保持一致
3. **语义清晰**：变量名应能清晰表达其含义，必要时添加注释
4. **避免关键字冲突**：不要使用 C# 关键字作为变量名（如 `class`, `int`, `string`）
