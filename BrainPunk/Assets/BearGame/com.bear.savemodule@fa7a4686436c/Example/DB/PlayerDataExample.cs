using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// 玩家数据示例 - 使用编辑器工具生成的脚本示例
    /// 这个类展示了如何使用 SaveDataScriptGenerator 创建的脚本结构
    /// 
    /// 主脚本（SaveDataScriptGenerator 生成）：只包含用户自定义的 private 字段
    /// Partial 脚本（PartialClassGenerator 生成）：单独存储在 PlayerDataExample_Partial.cs 中，包含 get/set 属性和静态 Instance
    /// 
    /// 推荐方式：继承 BaseSaveDataSO，自动获得保存功能
    /// </summary>
    public partial class PlayerDataExample : BaseSaveDataSO
    {
        // 静态存储类型（由 DBSetting 统一管理）
        public static StorageType StorageType = StorageType.Auto;

        // 用户自定义的 private 字段（由 SaveDataScriptGenerator 生成）
        [SerializeField] private int level;
        [SerializeField] private string playerName;
        [SerializeField] private float experience;
        [SerializeField] private int gold;
        [SerializeField] private bool isVip;

        // 注意：Save() 和 SaveAsync() 方法已由 BaseSaveDataSO 基类提供
        // 生成 Partial 类时会自动创建 ScriptableObject 资源文件，可通过 PlayerDataExample.Instance 访问
    }
}

