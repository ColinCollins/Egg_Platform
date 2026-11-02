using YooAsset;
using System;

namespace GF
{
    public class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;
        private string _generation;
        private readonly string _packageName;

        public RemoteServices(string defaultHostServer, string fallbackHostServer, string generation, string packageName)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
            SetGeneration(generation);
            _packageName = packageName;
        }

        public void SetGeneration(string generation)
        {
            _generation = generation;
        }
        
        public string GetGeneration()
        {
            return _generation;
        }
        
        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            //version文件通过Generation进行版本控制，其他文件均为hash值进行版本控制
            if (fileName.EndsWith(".version"))
            {
                // string filePath = $"{Platform}/{_packageName}/{fileName}";
                // string encodedPath = Uri.EscapeDataString(filePath);
                // return $"{_defaultHostServer}/{encodedPath}?alt=media";
                // //TODO 正确的是下方的，但是由于测试项目没有CDN的问题，暂时使用上方的
                return $"{_defaultHostServer}/Bundles/{Utility.Path.Platform}/{_packageName}/{fileName}?generation={_generation}";
            }
            else
            {
                // string filePath = $"{Platform}/{_packageName}/{fileName}";
                // string encodedPath = Uri.EscapeDataString(filePath);
                // return $"{_defaultHostServer}/{encodedPath}?alt=media";
                // //TODO 正确的是下方的，但是由于测试项目没有CDN的问题，暂时使用上方的
                return $"{_defaultHostServer}/Bundles/{Utility.Path.Platform}/{_packageName}/{fileName}";
            }
        }
        
        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            //version文件通过Generation进行版本控制，其他文件均为hash值进行版本控制 
            if (fileName.EndsWith(".version"))
            {
                // string filePath = $"Bundles/{Platform}/{_packageName}/{fileName}";
                // string encodedPath = Uri.EscapeDataString(filePath);
                // return $"{_fallbackHostServer}/{encodedPath}?alt=media";
                //TODO 正确的是下方的，但是由于测试项目没有CDN的问题，暂时使用上方的
                return $"{_fallbackHostServer}/Bundles/{Utility.Path.Platform}/{_packageName}/{fileName}?generation={_generation}";
            }
            else
            {
                // string filePath = $"Bundles/{Platform}/{_packageName}/{fileName}";
                // string encodedPath = Uri.EscapeDataString(filePath);
                // return $"{_fallbackHostServer}/{encodedPath}?alt=media";
                //TODO 正确的是下方的，但是由于测试项目没有CDN的问题，暂时使用上方的
                return $"{_fallbackHostServer}/Bundles/{Utility.Path.Platform}/{_packageName}/{fileName}";
            }
        }
    }
}