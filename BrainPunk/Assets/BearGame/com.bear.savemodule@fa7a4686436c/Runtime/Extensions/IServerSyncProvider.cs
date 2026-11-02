using System.Threading.Tasks;

namespace Bear.SaveModule
{
    /// <summary>
    /// 服务器同步提供者接口
    /// </summary>
    public interface IServerSyncProvider
    {
        /// <summary>
        /// 上传数据到服务器
        /// </summary>
        Task<bool> UploadAsync(string key, string data);
        
        /// <summary>
        /// 从服务器下载数据
        /// </summary>
        Task<string> DownloadAsync(string key);
        
        /// <summary>
        /// 检查服务器上是否存在数据
        /// </summary>
        Task<bool> ExistsAsync(string key);
        
        /// <summary>
        /// 删除服务器上的数据
        /// </summary>
        Task<bool> DeleteAsync(string key);
    }
}

