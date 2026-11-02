using UnityEngine;
using YooAsset.Editor;

namespace BrainPunk.Editor.BuildPipeline
{
    [CreateAssetMenu(fileName = "BPAssetBuildConfig", menuName = "BrainPunk/Create AssetBuildConfig")]
    public class BPAssetBuildConfig : ScriptableObject
    {
        [Header("Package")]
        public string PackageName = "GamePackage";

        [Header("Pipeline")]
        [Tooltip(" true = SBP (推荐), false = BBP (兼容)")]
        public bool UseScriptableBuildPipeline = true;

        [Header("Build Mode (SBP 只支持 Incremental)")]
        public EBuildMode BuildMode = EBuildMode.IncrementalBuild;

        [Header("Version")]
        [Tooltip("时间戳 / GitCommit / 自定义")]
        public EVersionStrategy VersionStrategy = EVersionStrategy.TimeStamp;
        [Tooltip("VersionStrategy=Custom 时生效")]
        public string CustomVersion = "1.0.0";

        [Header("Compression")]
        public ECompressOption CompressOption = ECompressOption.LZ4;

        [Header("File Name")]
        public EFileNameStyle FileNameStyle = EFileNameStyle.HashName;

        [Header("Builtin File Copy")]
        public EBuildinFileCopyOption BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;

        [Header("SBP Exclusive")]
        [Tooltip("SBP Build Cache，默认开启")]
        public bool SbpUseCache = true;
        [Tooltip("团队缓存服务器地址，留空则使用本地缓存")]
        public string SbpCacheServerHost = "";
        [Tooltip("团队缓存服务器端口")]
        public int SbpCacheServerPort = 8126;
        [Tooltip("生成 link.xml 防止代码裁剪")]
        public bool SbpWriteLinkXML = true;

        [Header("Encryption")]
        public bool EnableEncryption = true;

        [Header("Advanced")]
        public bool EnableSharePackRule = true;
        public bool VerifyBuildingResult = true;

        public enum EVersionStrategy
        {
            TimeStamp,
            GitCommitHash,
            Custom,
        }
    }
}
