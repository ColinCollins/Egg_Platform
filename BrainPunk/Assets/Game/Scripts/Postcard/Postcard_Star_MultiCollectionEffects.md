# Postcard_Star 多收集特效记录

## 需求背景

2026-07-06，用户要求关卡内明信片星星收集时，星星上绑定的收集特效允许配置多个；同时希望星星飞到 UI 时保持曲线表现。现有 UI 飞行已经由 `GamePlayPanel.PostcardStar` 使用二次贝塞尔曲线完成，本次只扩展 `Postcard_Star` 的收集特效配置能力。

## 影响范围

- 修改脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 读取但未修改脚本：`Assets/Game/UI/GamePanel/Scripts/GamePlayPanel.PostcardStar.cs`
- 兼容资源：`Assets/Game/UI/Postcard/Prefabs/Postcard_Star.prefab` 已把旧 `expolsion` 绑定迁移到 `collectionEffects`
- 未修改：存档字段、`PostcardManager`、`PostcardGetStarEvent`、背包奖励、胜利面板、Luban、RemoteConfig、资源加载、广告、IAP、analytics、平台 SDK

## 原有行为基线

- `Postcard_Star` 只有一个旧版 `ParticleSystem expolsion` 字段。
- 收集成功后，脚本只实例化这个单个特效到 `transform.parent` 下，并把特效世界坐标设为当前星星位置。
- UI 星星飞行不由场景星星本体执行，而是 `PostcardGetStarEvent` 把场景星星 worldPosition 传给 `GamePlayPanel.PostcardStar`，再由 UI 层实例化 `postcardStar` 飞向 HUD。
- UI 飞行路径已经使用 `postcardStarArcHeight` 和 `EvaluateQuadraticBezier(...)` 计算曲线。

## 当前行为

- 新增 `collectionEffects: List<ParticleSystem>`，用于配置多个收集特效预制体。
- 收集保存和发奖成功后，`PlayCollectionEffects()` 会按列表顺序实例化所有非空特效。
- 旧字段 `expolsion` 继续保留但隐藏在 Inspector 中，仅用于未迁移 prefab 的兜底兼容。
- `Postcard_Star.prefab` 已把原来的 `StunStarExplosion` 放入 `collectionEffects` 列表，并清空旧 `expolsion` 字段，Inspector 中只保留一个正常配置入口。
- 运行时只要 `collectionEffects` 有至少一个有效特效，就只播放列表；列表为空时才回退播放隐藏旧字段。
- UI 飞行曲线未改动，仍由 `GamePlayPanel.PostcardStar` 通过二次贝塞尔曲线从场景星星位置飞到 HUD 星星块。

## 数据、事件与 UI 流

1. `Postcard_Star.OnTriggerEnter2D()` 检测玩家触发。
2. `PostcardManager.TryCollectLevelStar(...)` 写入明信片星星收集记录。
3. `PlayCtrl.Instance.Bag.AddTool(GameProps.Star, 1, RewardType.Other)` 发放星星道具。
4. `Postcard_Star` 派发 `PostcardGetStarEvent`，参数为场景星星世界坐标。
5. `Postcard_Star.ApplyCollectedState()` 隐藏场景星星并关闭触发器。
6. `Postcard_Star.PlayCollectionEffects()` 在场景星星位置实例化一个或多个收集特效。
7. `GamePlayPanel.PostcardStar` 接收事件，在 Canvas 下创建 UI 星星，并通过贝塞尔曲线飞向 HUD 星星块。

## 验证记录

- `git diff --check -- Assets/Game/Scripts/Postcard/Postcard_Star.cs`：通过，仅有当前仓库已有的 LF/CRLF 提示。
- 静态扫描 `Postcard_Star.cs`：未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`DB.`、`SavePostcard`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`IAP` 或 `Advertisement`。
- 静态确认曲线飞行仍存在：`GamePlayPanel.PostcardStar.cs` 中仍使用 `postcardStarArcHeight`、`EvaluateQuadraticBezier(...)` 和 `DOTween.To(...)` 更新 `starRect.anchoredPosition`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未完成：当前环境没有 .NET SDK，命令返回 `No .NET SDKs were found`。
- 未运行 Unity Editor PlayMode、Unity 编译或 Android/iOS 真机 smoke。

## 剩余风险

- 新增序列化字段需要在 Unity Inspector 中打开 `Postcard_Star` 或对应 prefab 后确认显示和旧字段兼容情况。
- 多个粒子特效会在同一次收集成功后同时实例化；如果单个星星配置过多高开销特效，移动端可能出现短时粒子和实例化峰值，需要关卡抽样验证。
- 旧 `expolsion` 字段隐藏后不应再作为常规配置入口；若存在未迁移 prefab 且列表为空，仍会播放旧字段作为兼容兜底。
- 曲线飞行本次仅做静态确认，实际起点、弧线高度、终点对齐 HUD 仍需 Unity PlayMode 和移动端横屏抽样确认。

## 回滚路径

- 删除 `Postcard_Star.cs` 中的 `collectionEffects` 字段、`PlayCollectionEffects()`、`InstantiateCollectionEffect(...)`。
- 恢复收集成功后仅检查并实例化 `expolsion` 的旧逻辑。
- 删除本记录文件即可移除本次说明。
