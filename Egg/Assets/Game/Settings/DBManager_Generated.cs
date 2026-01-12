// =========================================
// 此文件由 DBManagerGenerator 自动生成
// 请勿手动修改此文件
// 如需重新生成，请使用菜单: Tools/Save Module/Generate DBManager Code
// 生成时间: 2026-01-12 11:45:36
// =========================================

using UnityEngine;
using Bear.SaveModule;

/// <summary>
/// DBManager 生成的静态数据访问类
/// 此文件由 DBManagerGenerator 自动生成
/// </summary>
public static partial class DB
{

    /// <summary>
    /// 获取 GameData 数据实例
    /// </summary>
    public static GameData GameData
    {
        get
        {
            return DBManager.Instance.Get<GameData>();
        }
    }

    /// <summary>
    /// 保存指定类型的数据
    /// </summary>
    public static bool SaveGameData()
    {
        return DBManager.Instance.Save<GameData>();
    }

}
