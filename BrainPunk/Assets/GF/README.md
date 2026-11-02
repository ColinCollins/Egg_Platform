# 共创目标：适合小游戏开发的框架

## 目录结构说明-Game目录请严格按照此格式！！！
  ```diff
  -├─ GF                                       所有的框架内容（！！！除共创外，不允许修改！！！）
  ```
```
Assets
├─ Editor
│  ├─ ScriptRulerSetting.asset              脚本生成器配置
│  └─ BuildPackage
│     ├─ BuildParamsHelper.cs               打包脚本
│     ├─ IOSPostProcessBuild.PodFix.cs      XCode工程修复脚本
│     └─ BuildHelper.cs                     打包脚本所需的参数（从python端传入）
├─ GF                                       所有的框架内容（！！！除共创外，不允许修改！！！）
│  ├─ Resources                             框架内的资源目录
│  ├─ Scripts                               脚本目录
│  │  ├─ Editor                             Editor下的脚本
│  │  └─ Runtime                            运行时的脚本
│  │  │  ├─ Utility                         工具类
│  │  │  ├─ Event                           事件管理
│  │  │  ├─ Pool                            对象池管理
│  │  │  ├─ Permission                      Android/iOS权限相关
│  │  │  ├─ Log                             日志管理
│  │  │  ├─ Audio                           音频管理
│  │  │  ├─ Procedure                       流程管理
│  │  │  ├─ Storage                         本地存储
│  │  │  ├─ Fsm                             流程状态
│  │  │  ├─ Logic                           游戏逻辑
│  │  │  ├─ Config                          配置管理
│  │  │  ├─ Define                          常量声明
│  │  │  ├─ Core                            核心代码
│  │  │  ├─ Net                             网络管理
│  │  │  │  ├─ Socket                       长链接
│  │  │  │  └─ Http                         短连接
│  │  │  ├─ Res                             资源管理
│  │  │  ├─ Crypto                          加密工具
│  │  │  ├─ Settings                        GameSetting存储
│  │  │  ├─ UI                              UI管理
│  │  │  └─ Misc                            杂项
│  │  ├─ Third                              已集成的第三方框架
│  │  │  ├─ I2                              多语言
│  │  │  ├─ Demigiant                       DoTweenPro动画
│  │  │  ├─ YooAsset                        YooAsset资源管理
│  │  │  └─ SuperScrollView                 无限滚动列表
├─ Game                                     单个游戏实现
│  ├─ Bundles                               所有的资源目录
│  │  ├─ Root                               启动入口预制体存储目录
│  │  ├─ Prefab                             其他游戏内的所有预制体存储目录
│  │  ├─ Configs                            配置表存储
│  │  ├─ Audio                              音频文件存储
│  │  │  ├─ Music                           背景音乐
│  │  │  └─ Sound                           音效
│  │  ├─ Art                                所有关卡资源
│  │  │  ├─ Levels                          关卡资源
│  │  │  │  ├─ 1                            关卡1的所有资源（文件夹的名字根据项目需求）
│  │  │  │  ├─ 2                            关卡2的所有资源（文件夹的名字根据项目需求）
│  │  │  │  └─ ...
│  │  │  └─ Textures
│  │  │  │  ├─ 1                            关卡1的杂项资源（文件夹的名字根据项目需求，如缩略图）
│  │  │  │  ├─ 2                            关卡2的杂项资源（文件夹的名字根据项目需求，如缩略图）
│  │  │  │  └─ ...
│  │  └─ UI                                 所有UI存储目录
│  ├─ Scripts                               代码目录
│  │  ├─ Editor                             Editor相关的代码目录
│  │  └─ Runtime                            运行时相关的代码目录
│  │     ├─ Model                           较复杂的系统对应的数据存储目录
│  │     ├─ Logic                           游戏逻辑目录
│  │     ├─ Procedure                       流程目录
│  │     ├─ Define                          常量声明目录
│  │     └─ UI                              界面展示目录
│  └─ Resources                             系统资源目录
│     └─ GameSetting.asset                  游戏启动配置
```


## 自动化打包
  ### python + Jenkins + BuildHelper.cs
  - 进入项目根目录
    ```shell
    cd {本都路径}/ArtGame/ArtGame
    ```

  - 执行python脚本: 
    ```python
    python3 Tools/Build/build_package.py -v 1.0.0 -r 1 -b BuildPlayer -m Debug -p Android -u /Applications/Unity/Hub/Editor/2020.3.40f1c1/Unity.app/Contents/MacOS/Unity -g false -t offline -c http://127.0.0.1:8080 -f http://127.0.0.1:8080 -s http://192.168.2.3:7001
    ```
  - 可通过-h 查看具体参数及含义
    ```python
    python3 Tools/Build/build_package.py -h
    ```

    ```
    -v APPVERSION, --appversion=APPVERSION
                          app版本, 显示在app详情内的
    -r RESVERSION, --resversion=RESVERSION
                          资源版本版本
    -b BUILDTYPE, --buildtype=BUILDTYPE
                          打包类型, 选项: buildin/buildplayer/buildbundle
    -m MODE, --mode=MODE
                          打包模式, 选项: Release/Debug
    -p PLATFORM, --platform=PLATFORM
                          打包平台 选项: android/ios/webgl
    -u UNITYEXE, --unityexe=UNITYEXE
                        Unity可执行文件
    -g AAB, --aab=AAB
                        是否为上传Google Play的aab包
    -t PLAYMODE, --playmode=PLAYMODE
                        资源使用模式 选项：offline/host
    -c CDNURL, --cdnurl=CDNURL
                        资源下载地址
    -f CDNFALLBACKURL, --cdnfallbackurl=CDNFALLBACKURL
                        资源下载备用地址
    -s SERVERURL, --serverurl=SERVERURL
                        服务器地址
    -j ART, --art=ART_PATH
                        PSD原始文件目录
    -a ASSETS, --assets=ASSETS_PATH
                        资源库目录（解析psd的文件保存目录）
    -d UPLOADBUNDLE, --uploadbundle
                        是否上传bundles
    -x PSDIDS, --psdids=0,1,2
                        指定打bundle的资源id
    ```

  - 构建的Bundle在：
  ```
      {本地路径}/ArtGame/ArtGame/Build/{打包模式}/Hotupdate/Bundles/{平台}/{资源版本号}/
  ```
  - 构建的apk或者ipa在：
  ```
      {本地路径}/ArtGame/ArtGame/Build/{打包模式}/Package/{平台}/
  ```

  - 命名规则
  ```
    如：AG-v1.0.0-23072877-5-Debug-BuildPlayer-20230728_191604.apk
    项目名称-app版本号-build版本号-资源版本号-打包模式-打包类型-时间年月日_时分秒.apk
  ```

## 流程管理
  ### Procedure
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/App.cs)
- #### 示例
  - 整个游戏运行分成不同的流程（状态），方便管理 
  - 添加流程
     ```c#
     App.AddProcedure(new ProcedureSceneTest());                                                                                             
     ```
  - 切换流程
     ```c#
     App.ChangeProcedure<ProcedureConfigTest>();                                                                                             
     ```
  - 流程生命周期
     ```c#
     // 进入该流程时调用
     public void OnEnter(params object[] args) { }
     public void OnUpdate() { }
     // 离开该流程时调用
     public void OnLeave() { }
     // 该流程销毁时调用
     public void OnDestroy() { }                                                                                        
     ```
  - ps: 一般会在OnEnter中初始化数据，OnLeave中释放数据
## 异步实现 - UniTask
  - 参考文档
    - https://github.com/Cysharp/UniTask/blob/master/README_CN.md
  - 基础用法
    - Delay
       ```c#
       // delay毫秒
       await UniTask.Delay(1000);
       // delay帧
       await UniTask.DelayFrame(1);
       ```
    - 加载场景
      ```c#
      await SceneManager.LoadSceneAsync("SceneName");
      ```
      
    - 回调转await
       ```c#
       UniTaskCompletionSource taskCompletionSource = new UniTaskCompletionSource();
       LoadSceneAsync("SceneName", () => {
            taskCompletionSource.TrySetResult();
        });
       ```
      
    - Dotween支持
       ```c#
       await btn_next_level.transform.DOLocalMoveY(-621f, 0.5f).SetEase(Ease.OutBack)
                .WithKillAndCancel(this.GetCancellationTokenOnDestroy());
       ```
      
    - 按钮点击事件
        ```c#
         await btn_continue.OnClickAsync(cancellationToken: this.GetCancellationTokenOnDestroy());
         ```
    - YooAsset支持
       ```c#
       await YooAsset.LoadAssetAsync<GameObject>("Assets/Game/Bundles/Root/Root.prefab").ToUniTask();
       ```
      
## 资源管理
  ### ResKit
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Res/ResKit.cs)
- #### 示例
  - 同步加载unity资源
     ```c#
     AudioClip clip = App.Res.LoadAsset<AudioClip>(path, tag);                                                                                                   
     ```
  - 异步加载unity资源
     ```c#
     GameObject root = await App.Res.LoadAssetAsync<GameObject>("Assets/Game/Bundles/Root/Root.prefab", "ProcedureGame");                                                                                                  
     ```
  - 同步加载原生资源字节数组
     ```c#
     byte[] data = App.Res.LoadRawData("path", "tag");                                                                                                  
     ```
  - 同步加载原生资源文本
     ```c#
     string data = App.Res.LoadRawText("path", "tag");                                                                                                
     ```
  - 异步加载原生资源字节数组
     ```c#
     byte[] data = await App.Res.LoadRawDataAsync("path", "tag");                                                                                              
     ```
  - 异步加载原生资源文本
     ```c#
     string data = await App.Res.LoadRawTextAsync("path", "tag");                                                                                           
     ```
  - 根据tag释放资源
     ```c#
     App.Res.ReleaseAsset(config.prefabPath);                                                                                          
     ```
  
- #### 注意事项
    - 场景资源不需要进行手动释放，[YooAsset](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Third/YooAsset/Runtime/AssetSystem/AssetSystem.cs) 内部在加载场景时会释放所有的缓存场景资源
## UI框架
  ### UIKit的实例：App.UI

  ### 逻辑比较复杂的界面，请严格按照MVP(Modle-View-Presenter)设计模式结构进行代码编写

  ### UI配置
  - 请在Tag & Layers中添加Sorting Layers
    - Layer0 Default
    - Layer1 SceneLayer
    - Layer2 BaseLayer
    - Layer3 MiddleLayer
    - Layer4 PopupLayer
    - Layer5 TopLayer
    - Layer6 NotifyLayer
  ### UI层级分类
  - UILayer根据层级分为6类
    ```c#
    public enum UILayer
    {
        //场景层（可用于场景内的UI）
        SceneLayer,
        //基础层（可用于全屏界面）
        BaseLayer,
        //中间层（可用于Loading，在Base之上，在弹窗之下）
        MiddleLayer,
        //弹出层（可用于弹窗）
        PopupLayer,
        //顶层（可用于新手引导）
        TopLayer,
        //通知层（可用于断线提示）
        NotifyLayer
    }
    ```
  - UI配置类：UIConfig
    ```c#
    public class UIConfig
    {
        //界面对应的Prefab路径
        public string prefabPath;
        //界面移除时是否隐藏
        public bool hideWhenRemove;
        //界面所在的层级
        public UILayer layer;
    }
    ```
  ### 结构
  - 所有界面的父类：UIBase
  - 所有在 UILayer.BaseLayer 层的界面的父类：UIView, 继承于UIBase
  - 所有 UILayer.PopupLayer 层的弹窗的父类：UIPopup, 继承于UIBase
  ```diff
  - 理论上所有的用户交互界面, 只能继承于UIView或UIPopup, 不能直接继承于UIBase
  ```

  ### 使用方法
  - 打开登录界面（LoginView）
    ```c#
      无参:
      App.UI.Open<LoginView>();
      
      有参:
      App.UI.Open<LoginView>(参数1, 参数2, 参数3);
    ```
  - 关闭登录界面
    ```c#
      App.UI.Close<LoginView>();
    ```
  ### UIView 的生命周期
  -  当界面打开时
  ```c#
    public virtual void OnEnter(params object[] args)
    {
      ...
    }
  ```

  - 当界面被再次打开
  ```c#
    public virtual void OnRefresh(params object[] args)
    {
      ...
    }
  ```

  - 当界面退出时
  ```c#
    public virtual void OnExit()
    {
      ...
    }
  ```
  ### UIPopup 的生命周期
  - 进入界面时（与UIView一致）
  - 当弹窗动画播放放完毕时
  ```c#
    public virtual void OnOpenAnimComplete()
    {
      ...
    }
  ```
  - 当界面被再次打开（与UIView一致）
  - 当界面退出时（与UIView一致）
  - 当界面关闭动画播放完毕
  ```c#
    public virtual void OnCloseAnimComplete()
    {
      ...
    }
  ```


  - 原理: 
    - 所有界面通过栈结构管理，当界面在栈顶，就会被激活，若不在栈顶，则新建，并添加到栈顶
<br></br>
  - 注意事项:
    - 界面第一次进入时调用 OnEnter, 之后的所有情况导致界面被激活，均调用OnRefresh
<br></br>
  - 界面再次被打开的场景（以下V界面表示继承于UIView，P界面表示继承于UIPopup）
    - 场景1: V1界面中打开了P1弹窗，当P1弹窗关闭时，会调用V1界面的OnRefresh
    - 场景2: V1界面中打开了V2界面，当V2界面关闭时，V1界面再次被呈现，会调用V1界面的OnRefresh
    - 场景3: V1界面中打开了P1弹窗中，P1弹窗中打开了P2弹窗，当P2弹窗关闭时，V1界面和P1界面的OnRefresh均会被调用
<br></br>
  ## UI 推荐写法
  ### 若View较为庞大，需要的逻辑较为复杂，如Daily、核心玩法View，推荐MVP设计模式
  - 第一步：新建ALogic，继承于BaseLogic，并实现所有抽象方法（同理对应到Mono的生命周期）
  - 第二步：新建AView，继承于UIView，并实现抽象方法CreateLogic，并返回ALogic的实例
  - 第三步：通过App.UI.Open<AView>()，打开界面
  - 第四步：通过ALogic的生命周期回调，实现具体的逻辑
  ### 若View较为简单，不需要过多逻辑，如确认弹窗，可直接使用MV
  - 第一步：新建AView，继承于UIView，并实现抽象方法CreateLogic，直接返回为null
  - 第二步：通过App.UI.Open<AView>()，打开界面
  - 第三步：通过AView的生命周期回调，实现具体的逻辑

## 网络
  ### 短连接
  - 工具类：HttpKit
    - Get
    ```c#
        string data = await HttpKit.GetString(url,queryDict,headers);
        byte[] bytesData = await HttpKit.GetBinary(url,queryDict,headers);
        UnityWebRequest request = await HttpKit.Get(url,queryDict,headers);
    ```
      - Post
    ```c#
        string data = await HttpKit.PostString(url,postData,headers);
        byte[] bytesData = await HttpKit.PostBinary(url,postData,headers);
        UnityWebRequest request = await HttpKit.Post(url,postData,headers);
    ```

  ### 长连接
  - 工具类: NetKit
  - NetKit的实例：App.Net
    - 协议使用Protobuf 请克隆：git@github.com:cylemonVip/IdleGameProtocol.git
    - Unity项目（客户端），请克隆到: Assets同级目录的Tools目录内
    - Golang项目（服务器），请克隆到: server/ext_tools目录内

  - 文件功能
    - proto/api.proto 定义所有的消息体
    - proto/def.proto 定义消息中使用的结构体
    - proto/errors.proto 定义错误类型
    - proto/msg.proto 根据api.proto的Req和Ack自动生成的消息序列号 （请勿手改）
    - proto/wsapi.proto 预留给推送服的请求消息体（如聊天服）
    - proto/third_proto/ 文件夹 存放第三方引用的proto文件
    - gen_msg_proto.py 生成proto/msg.proto脚本（在gen_pb.py中执行）
    - gen_pb.py 生成各个语言对应的proto文件

  - 使用方法
    - 进入到克隆的 IdleGameProtocol 文件夹
    ```shell
      cd IdleGameProtocol
    ```
    
    - 生成Proto和消息分发文件
      - 生成 Golang 语言对应的proto文件
        ```shell
        python3 gen_pb.py go
        ```
        - 生成proto文件在server/protocol中，如api.pg.go等等
        - 生成注册消息分发文件server/msg/msg.go

      - 生成 C# 语言对应的proto文件
        ```shell
        python3 gen_pb.py csharp
        ```
        - 生成proto文件在Assets/Game/Scripts/Runtime/Protocol中，如Api.cs等等
        - 生成注册消息分发文件Assets/Game/Scripts/Runtime/Net/MessageParser.cs(请勿手改)

      - 生成 Javascript 语言对应的proto文件
        ```shell
        python3 gen_pb.py js
        ```
        - 生成proto在web/protocol中
        - TODO 暂无注册消息分发文件

    - Unity使用方法
        - 消息发送(示例发生心跳)
          ```c#
          using Google.Protobuf;
          using Protocol;

          //创建心跳请求（该类存在于Protocol命名空间中）
          HeartBeatReq req = new HeartBeatReq();
          long scnd = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
          //请求参数赋值
          req.Timestamp = scnd;
          //发生请求(第一个参数为消息序列号，第二个参数为示例化的Req对象)
          //消息序列号来源于msg.proto
          //msg.proto来源于遍历api.proto自动生成
          NetMgr.Instance.Send(MsgID.HeartBeatReq, req);
          ```
        - 消息接收（示例心跳）
          - 消息接收通过事件进行分发，在Socket收到消息后，通过MessageParser.cs进行解析，并派发事件
          - 接收注册一般在Manager中，然后将收到的消息存在Model，最终更新UI
          ```c#
            using Google.Protobuf;
            using Protocol;

            //注册消息事件接收
            NetMgr.AddNetEvent<HeartBeatAck>(MsgID.HeartBeatAck, OnReceiveHeartbreat);
            //注销消息事件接收
            NetMgr.RemoveNetEvent<HeartBeatAck>(MsgID.HeartBeatAck, OnReceiveHeartbreat);

            /// <summary>
            /// 收到心跳处理
            /// </summary>
            /// <param name="msgData"></param>
            private void OnReceiveHeartbreat(HeartBeatAck msgData)
            {
                long scnd = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                long delay = scnd - msgData.CTimestamp;
                long c2sDelay = msgData.STimestamp - msgData.CTimestamp;
                long s2cDelay = scnd - msgData.STimestamp;

                LogKit.I($"收到心跳包 C->S 延迟: {c2sDelay} S->C 延迟: {s2cDelay}   C->S->C 延迟: {delay}");
            }
          ```
## 音频
### AudioKit
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Audio/AudioKit.cs)
- #### 示例
  - 循环播放bgm
    ```c#
    int channel = App.Audio.PlaySound("Assets/Game/Bundles/Audio/Music/BGM01.mp3", SoundType.BGM, true);                                                                                                            
    ```
  - 播放场景音效
     ```c#
     App.Audio.PlaySound("Assets/Game/Bundles/Audio/Sound/put.mp3", SoundType.Scene, false);                                                                                                            
     ```
  - 暂停bgm
     ```c#
     App.Audio.PauseSound(channel, SoundType.BGM);                                                                                                            
     ```
  - 继续播放bgm
     ```c#
     App.Audio.ResumeSound(channel, SoundType.BGM);                                                                                                           
     ```
  - 停止bgm
     ```c#
     App.Audio.StopSound(channel, SoundType.BGM);                                                                                                         
     ```
  - 设置bgm音量
     ```c#
     App.Audio.BGMVolume = 0.8f;                                                                                                        
     ```
  - 设置bgm静音
     ```c#
     App.Audio.BGMMute = true;                                                                                                 
     ```
  - #### 注意
    - 音频播放的资源需要手动释放
     ```c#
       string path = "xxx";
       App.Res.ReleaseAsset(path);                                                                                                 
       ```

## 消息事件
### EventKit
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Event/EventKit.cs)
- #### 示例
  - 添加无参数的事件，target为this
    ```c#
    App.Event.AddEvent("test001", Test001, this);                                                                                              
    ```
  - 添加string类型参数事件
    ```c#
    App.Event.AddEvent<string>("test002", Test002, this);                                                                                              
    ```
  - 添加int，object为参数的事件
    ```c#
    App.Event.AddEvent<int, object>("test003", Test003);                                                                                           
    ```
  - 移除单个事件
    ```c#
    App.Event.RemoveEvent("test001", Test001);                                                                                           
    ```
  - 移除id的所有事件
    ```c#
    App.Event.RemoveEventAll("test001");                                                                                           
    ```
  - 移除该target的所有事件
    ```c#
    App.Event.RemoveEventTarget(this);                                                                                           
    ```
  - 同步派发无参数事件
    ```c#
    App.Event.DispatchEvent("test001");                                                                                           
    ```
  - 异步派发无参数事件
    ```c#
    App.Event.DispatchEvent("test001", true);                                                                                          
    ```
  - 同步派发带bool参数事件（需要注明泛型参数，不然默认会和异步无参数混淆）
    ```c#
    App.Event.DispatchEvent<bool>("test001", true);                                                                                        
    ```
  
- ####注意事项
    - 添加事件id必须是同一种类型的事件，比如不能在同一个id添加无参和有参事件
    - 异步派发会延迟一帧，主要用于子线程调用 
## 配置生成、加载、读取
## 热更新-HybirdCLR
  - HybirdCLR
## 日志
### LogKit
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Log/LogKit.cs)
- #### 示例
  - info日志
    ```c#
    LogKit.I("hello world");                                                                                       
    ```
  - info带参日志
    ```c#
    LogKit.I("hello {0}", "world");                                                                              
    ```
  - error日志
    ```c#
    LogKit.E("hello world");                                                                             
    ```
  - warning日志
    ```c#
    LogKit.W("hello world");                                                                          
    ```
  - exception日志
    ```c#
    LogKit.Exception(new Exception("异常"));                                                                          
    ```

## SDK模块
## 用户设置
## 评价
## RemoteConfig
## 用户数据管理-序列化和反序列化
### Utility.Json
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Utility/Utility.Json.cs)
- #### 示例
  - 序列化json
    ```c#
    string jsonData = Utility.Json.Serialize(_cacheData);                                                                     
    ```
  - 反序列化json
    ```c#
    _cacheData = Utility.Json.Deserialize<StorageData>(jsonData);                                                                    
    ```
## 本地存储
### LocalStorageKit
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/Storage/LocalStorageKit.cs)
- #### 示例
  - 保存float类型，注意添加f，才能明确是保存float
      ```c#
      App.LocalStorage.SetData("key1", 0.1f);                                                                   
      ```
  - 保存int类型
      ```c#
      App.LocalStorage.SetData("key1", 1);                                                              
      ```
  - 保存string类型
      ```c#
      App.LocalStorage.SetData("key1", "hello world");                                                            
      ```
  - 保存
      ```c#
      App.LocalStorage.Save();                                                          
      ```
  - 检测更新服务器Storage到本地
      ```c#
      App.LocalStorage.CheckRemoteStorage();                                                          
      ```
  - 上传本地Storage到服务器
      ```c#
      App.LocalStorage.UploadStorage();                                                          
      ```
## 异形屏适配
### SafeAreaSizer
- #### [源码](https://github.com/cylemonVip/IdleGame/blob/main/IdleGame/Assets/GF/Scripts/Runtime/UI/Aspect/SafeAreaSizer.cs)
- #### 用法
- 在需要适配的节点添加AdaptComp组件，组件可以设置适配类型：Up,Down,All，适配节点的适配类型必须是全屏拉伸的方式