using System.Collections.Generic;

namespace GF
{
    public abstract class TableBase
    {
        protected Dictionary<int, TableItemBase> _dataDic = new Dictionary<int, TableItemBase>();
        public abstract void Deserialize(string jsonData);

        public T GetItem<T>(int id) where T: TableItemBase
        {
            if (_dataDic.TryGetValue(id, out var val))
            {
                return val as T;
            }

            LogKit.E($"不存在该id：id = {id},table = {GetType()}");
            return null;
        }
        
        /// <summary>
        /// 获取整张表
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, TableItemBase> GetDataDic()
        {
            return _dataDic;
        }
    }
}