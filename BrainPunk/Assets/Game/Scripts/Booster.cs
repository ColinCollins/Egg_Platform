using Cysharp.Threading.Tasks;
using GF;
using SDK.Remote;
using UnityEngine;

namespace Game
{
    public class Booster : MonoBehaviour
    {
        public static LocalStorageKit LocalStorage
        {
            private set;
            get;
        }

        public static ResKit Res
        {
            private set;
            get;
        }
        public static HttpKit Http
        {
            private set;
            get;
        }

        public static I2Kit I2
        {
            private set;
            get;
        }

        public static MailKit Mail
        {
            private set;
            get;
        }

        [SerializeField] private Reporter reporter; 

        public static Booster Instance;

        public bool IsReady { get; private set; }

        public void Init()
        {
            DontDestroyOnLoad(this);

#if DEBUG_MODE && !UNITY_EDITOR
            reporter.gameObject.SetActive(true);
#endif

            Instance = this;

            //初始化Logger
            YooAsset.ILogger logger = new DefaultLogger();
            LogKit.SetLogger((GF.ILogger)logger);

            //初始化本地存储
            LocalStorage = new LocalStorageKit();

            //初始化资源管理
            Res = new ResKit();
            Res.Init(logger);
            //初始化Http
            Http = new HttpKit();

            // 邮件
            Mail = new MailKit();

            // SDK 启动
            GameSDKService.Instance.InitService(
                onInitCallback: OnSDKInitCallback
            ).Forget();
        }


        private void Update()
        {
            // Audio?.Update();
        }

        private void OnApplicationPause(bool pause)
        {
            LocalStorage?.OnApplicationPause(pause);
        }

        private void OnDestroy()
        {
            LocalStorage?.Destroy();
            Res?.Destroy();
        }

        private void OnSDKInitCallback()
        {
            IsReady = true;
        }
    }
}

