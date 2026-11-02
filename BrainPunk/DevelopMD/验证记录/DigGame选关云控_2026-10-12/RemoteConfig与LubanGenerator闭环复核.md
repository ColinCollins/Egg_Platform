# RemoteConfig 与 LubanGenerator 闭环复核

日期：2026-10-12。本次由用户质疑触发，属于第二轮结果的事后补查。验收对象是全部 Dig Global 字段及排序从输入、默认生成、拉取/缓存、getter 到实际消费者的完整链。

## 可观察事实与结论范围

- 当前工作区已存在 Dig 生成白名单、独立合并方法和统一导出菜单调用；ConfigManager.RemoteConfig 的独立 key / 字段常量 / 11个 getter、RemoteConfigService 四key注册及独立延期提交、真实消费者均已接入。此次没有发现需再补的运行时消费者缺口。
- 已直接比对14字段在Global生成JSON、生成器白名单、Guru YAML、Guru生成默认C#、RemoteConfig常量的名称和值；Guru YAML/C#完整Dig载荷相等，level_sort对应当前30行排序输入。3个区间字段经两个间隔/失败getter消费，不另造未调用API。
- 本次发现并修正共享文档的旧三key/未纳入Dig范围，以及统一生成菜单无变化提示的“四组配置”旧文案。业务行为、ForceFetch预算、Loading顺序和只读SDK边界未改。

## 逐字段调用证据

|字段|类型|getter / 区间读取入口|实际消费者|
|---|---|---|---|
|`digGameIsOpen`|bool|`GetDigGameIsOpen`|`Assets/Game/UI/StartPanel/Scripts/DigGameEnter.cs`|
|`digGameShowTipsFailCount`|int|`GetDigGameShowTipsFailCount`|`Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs`|
|`digGameShowTipsTimeLimit`|float|`GetDigGameShowTipsTimeLimit`|`Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs`|
|`digGame_interstitialCD`|int|`GetDigGameInterstitialCD`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_rewardResetInterstitialCD`|int|`GetDigGameRewardResetInterstitialCD`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstitialShowMinLevel`|int|`GetDigGameInterstitialShowMinLevel`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstitialShowIntervalCount`|int|`GetDigGameInterstitialShowIntervalCount`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstitialShowFailedCount`|int|`GetDigGameInterstitialShowFailedCount`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstitalAdLevelRange`|int列表|`GetDigGameInterstitialShowIntervalCount / GetDigGameInterstitialShowFailedCount`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstitalAdRangeLevelCount`|int列表|`GetDigGameInterstitialShowIntervalCount`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_interstititalAdRangeFailedCount`|int列表|`GetDigGameInterstitialShowFailedCount`|`Assets/Game/Scripts/Ads/DigInterstitialAdPolicy.cs`|
|`digGame_dailyNOAdPopupAutoShowCount`|int|`GetDigGameDailyNOAdPopupAutoShowCount`|`Assets/Game/Scripts/Ads/InterstitialNOAdPopupAutoShowModule.cs`|
|`digGame_gameTipsPopupCoinCost`|int|`GetDigGameGameTipsPopupCoinCost`|`Assets/Game/UI/DigGame/TipsPopup/Scripts/DigGame_GameTipsPopup.cs`|
|`digGame_gameTipsPopupPassLevelCoinCost`|int|`GetDigGameGameTipsPopupPassLevelCoinCost`|`Assets/Game/UI/DigGame/TipsPopup/Scripts/DigGame_GameTipsPopup.cs`|

`level_sort` 另由 LubanGenerator.TryMergeDigGameConfig 写入，DigGameLevelCtrl.SyncRemoteConfig / TryBuildRemoteConfigCandidate 从独立diggame_config读取并验证，RemoteConfigService.PersistResolvedConfigs在落盘成功后同步应用。enabled控制是否采用远端排序，玩法入口开关是digGameIsOpen；两者独立。

## 验证结果与证据边界

- `generator-audit/generator-tests.log`：27项生成器断言通过；执行当前源码的纯合并/序列化方法，覆盖14字段/30整数排序、当前默认语义相等、非Dig YAML行保留、enabled/扩展字段保留、缺字段/旧JSON损坏/缺锚点拒绝及地区浮点格式，不执行Unity菜单或写生产YAML。首次测试夹具读取CRLF尾部未匹配，修正夹具后通过，不是生产生成器故障。
- `generator-audit/runtime-tests.log`：51项既有实际getter/缓存提交/排序/存档/统计隔离断言复跑通过，最终IO、SDK发送及Unity管理边界为替身，不等于Firebase联网实测。
- `generator-audit/compile-generator.log`：当前编辑器程序集编译0错误；目标代码与文档diff检查通过。Guru默认值当前已存在，编译/模拟不能证明远端后台已经发布或设备拿到新值。
- 真实Unity统一导出及官方代码生成菜单、Firebase发布/网络恢复晚到回调、真机广告/暂停恢复仍uncertain，应分别实测。现有代码保持ForceFetch最多1次、自动等待5秒、条件后台强制10秒与原Loading初始化顺序。

## 文档、维护边界与回滚

详细流程已补入DevelopMD/LubanGenerator功能说明.md和RemoteConfigService_Design.md，修改前记录及结果在当日使用记录。只回滚本次提示/文档增量及新增验证材料；第二轮真实14字段及消费者已接入，不能删除这些内容作为“文档回滚”。Packages/GuruSDK/GF无写入。

本次LubanGenerator源码新增一处文案修正，因此原source-hashes.json中的该文件历史hash已不代表当前文字；最新生成器源码hash见generator-audit/generator-sources.json。旧运行时业务方法保持原证据范围。
