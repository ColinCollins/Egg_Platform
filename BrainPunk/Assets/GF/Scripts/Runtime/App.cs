using System;
using UnityEngine;
using UnityEngine.Android;
using YooAsset;

namespace GF
{
    public partial class App: MonoBehaviour
    {
        [Header("待启动的游戏根目录")]
        public string startUpAppName;

        public static Fsm<App> Procedure
        {
            private set;
            get;
        }

        public static PoolKit Pool
        {
            private set;
            get;
        }
        
        public static EventKit Event
        {
            private set;
            get;
        }
        
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
        
        public static ConfigKit Config
        {
            private set;
            get;
        }

        public static AudioKit Audio
        {
            private set;
            get;
        }
        
        public static UIKit UI
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

        public static App Instance;

        private void Awake()
        {
            DontDestroyOnLoad(this);
            
            Application.targetFrameRate = 60;

            Instance = this;

            // GuruUtils.Initiallize();

            //初始化Logger
            YooAsset.ILogger logger = new DefaultLogger();
            LogKit.SetLogger((ILogger) logger);

            //初始化本地存储
            LocalStorage = new LocalStorageKit();

            //初始化状态机
            Procedure = new Fsm<App>(this);

            //初始化对象池
            Pool = new PoolKit();

            //初始化事件管理
            Event = new EventKit();

            //初始化资源管理
            Res = new ResKit();
            Res.Init(logger);

            //初始化配置
            Config = new ConfigKit();

            //初始化音频
            Audio = new AudioKit();
            Audio.Init();

            //初始化UI管理器
            UI = new UIKit();
            UI.Initialize();

            //初始化Http
            Http = new HttpKit();
            
            //多语言
            I2 = new I2Kit();
            // 设置语言
            I2.InitGameLanguage();
            
            //邮件
            Mail = new MailKit();

            StartUp();
        }

        private void StartUp()
        {
            Procedure.ChangeState<ProcedureLauncher>();
        }

        private void Update()
        {
            Audio?.Update();
            Procedure?.Update();
            Event?.Update();
            UI?.Update();
        }
        
        private void OnApplicationPause(bool pause)
        {
            LocalStorage?.OnApplicationPause(pause);
        }

        private void OnDestroy()
        {
            Audio?.Destroy();
            Pool?.Destroy();
            LocalStorage?.Destroy();
            Procedure?.Destroy();
            Config?.Destroy();
            Res?.Destroy();
        }
    }
}