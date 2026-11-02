# GameTips 提示弹窗逻辑文档

## 2026-09-10 确认按钮点击后关闭不重复打开金币礼包

- 需求与基线：主线 `GameTipsPopup` 已在 closeBtn 关闭时检测金币不足并打开 `CoinGiftPopup`；此前玩家先点确认按钮触发过金币不足礼包，返回提示弹窗后再点关闭，会重复打开礼包。
- 修改范围：仅修改 `GameTipsPopup.cs` 的实例运行态和按钮回调，不修改 Prefab、价格配置、金币消费、提示发放、跳关、存档、事件或 CoinGift 内部逻辑。
- 修改后行为：每次 `OnOpen()` 清空 `_hasClickedConfirmButton`；Normal 与 PassLevel 的确认按钮回调一进入就置为 `true`，包括余额不足、首次免费、无限金币和成功消费路径。closeBtn 仅在该标记仍为 `false` 时执行原金币不足检测；标记为 `true` 时正常关闭和恢复游戏，不再打开 CoinGift。
- UI 与生命周期：CoinGift 覆盖提示弹窗时只触发底层 `OnHide`，返回时只触发 `OnShow`，因此当前实例的确认点击标记会保留；弹窗下次重新 `OnOpen` 时重置，不跨弹窗实例、关卡或会话持久化。
- 验证：已静态核对两个确认入口和 closeBtn 门禁；使用现有 Unity Bee 响应文件执行 Unity Roslyn 运行时程序集编译，退出码 0、0 error；目标差异检查通过。
- 未验证与回滚：`uncertain`：尚未执行 Unity PlayMode、CoinGift 往返和 Android/iOS 横屏实测。回滚时移除 `_hasClickedConfirmButton` 及三个读写点，即恢复每次 closeBtn 都按余额判断的行为，无数据迁移。

## 2026-09-09 CloseBtn 金币不足入口补充

- 需求：用户明确 closeBtn 始终正常关闭提示界面，只额外检测金币不足并弹出 CoinGiftPopup，不代替确认按钮扣币或发放提示。
- 基线与范围：原 `GameTipsPopup.Close(CustomButton)` 直接恢复游戏并关闭 Popup。本次在关闭前计算不足标志、原关闭流程后复用 `OpenCoinGiftPopup()`，覆盖 Normal 与 PassLevel；Draw 独立弹窗、Prefab 和生成绑定不改。
- 判断与 UI 流：首次免费 Normal、无限金币不额外弹礼包；其余按当前状态分别读取提示价或跳关价，余额小于价格时记录标志。无论余额多少，均完成原关闭等待、PLAYING 派发和 CloseUI/CloseAllUI，然后按标志打开 CoinGift。余额等于价格不弹礼包，不扣币、不派发提示或通关事件。CoinGift 创建失败沿用系统提示，不撤销提示界面关闭。
- 复用边界：广告、购买和飞奖励继续由既有 CoinGift 流程负责，无新配置、存档、全局事件、资源地址或 SDK 调用。确认按钮不足时仍保留底层提示和暂停；关闭按钮入口按用户要求恢复原游戏状态，CoinGift 不额外接管暂停。首次免费提示后的答案弹窗属于 PassLevel，关闭等待正常完成，余额不足时额外弹礼包。
- 验证：最终代码执行 `dotnet build ./Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，0 错误、123 警告；目标 `git diff --check` 通过。静态确认 UIManager.CloseUI/CloseAllUI 同步移除旧栈并调用 OnClose 后返回，新礼包在原关闭流程之后创建。
- 缺口：CodeGraph CLI 不可用、引用的 feature plan 不存在，知识库“UI 开发”无匹配，本次以本地脚本、Prefab 和已有文档为证据。Unity PlayMode、首次免费引导答案关闭、金币不足连续关闭、CoinGift 返回、Android/iOS 横屏及原生覆盖恢复为 `uncertain`。本次无新增逐帧逻辑。
- 回滚：仅移除 Close 方法新增的金币不足判断及本节记录，恢复原关闭行为，无数据迁移。

> 当前主线按钮契约已于 2026-09-07 改为单金币按钮，详见第 21 节；下文旧广告按钮映射与广告流程作为历史基线保留，不再代表主线当前入口。

## 1. 概述

`GameTipsPopup` 是游戏内提示系统的核心弹窗，负责在玩家卡关时提供两种帮助：

- **获取提示**（Normal 状态）：显示关卡提示文本
- **直接通关**（PassLevel 状态）：显示答案并结束当前关卡

弹窗通过 `TipPopupState` 枚举区分三种状态，其中 `Lack` 状态已在 1.5.0 版本废弃。

---

## 2. 弹窗状态

```csharp
public enum TipPopupState
{
    Normal = 0,   // 正常提示状态
    Lack,         // 缺少道具（1.5.0 废弃）
    PassLevel     // 直接通关状态
}
```

### 2.1 预制体变体

| 面板名称            | state 值  | 用途                            |
| ------------------- | --------- | ------------------------------- |
| `GameTipsPopup`   | Normal    | 首次请求提示时显示              |
| `GameTipsPopup_2` | Lack      | 道具不足提示（已废弃）          |
| `GameTipsPopup_3` | PassLevel | 已获得提示后显示答案 / 直接通关 |

> `state` 为 `[SerializeField]`，由 Inspector 预设，代码中根据预制体变体自动匹配。

---

## 3. 按钮行为映射

按钮在 `OnCreate()` 中根据 `state` 动态绑定：

### 3.1 Normal 状态

| 按钮               | 点击行为            | 说明               |
| ------------------ | ------------------- | ------------------ |
| **CloseBtn** | `Close()`         | 关闭弹窗，恢复游戏 |
| **NoBtn**    | `ShowRewardAd()`  | 看广告获得提示     |
| **YesBtn**   | `TryToUseCoins()` | 消耗金币获得提示   |

### 3.2 PassLevel 状态

| 按钮               | 点击行为             | 说明               |
| ------------------ | -------------------- | ------------------ |
| **CloseBtn** | `Close()`          | 关闭弹窗，恢复游戏 |
| **NoBtn**    | `Close()`          | 关闭弹窗           |
| **YesBtn**   | `TryToPassLevel()` | 消耗金币直接通关   |

---

## 4. 核心流程

### 4.1 Normal → 获取提示（消耗金币）

```
TryToUseCoins()
  ├── 检查金币数量（GameTipsPopupCoinCost）
  ├── 不足 → CoinGiftPopup.Create()
  └── 足够
       ├── Bag.RemoveTool(Coin, cost)
       ├── DispatchEvent(UseTipsEvent)
       └── Create()  →  创建 GameTipsPopup_3（PassLevel 状态）
```

金币不足时，`CoinGiftPopup` 以同层叠加窗口覆盖在提示弹窗上方；原提示弹窗不关闭，暂停状态不交给
CoinGift。广告或购买成功只补充金币并播放飞奖励动画，CoinGift 保持打开且不自动再次执行提示消费；
玩家主动关闭 CoinGift 后回到提示弹窗重新确认。
CoinGift 创建失败时仅回退显示原金币不足系统提示，不扣币。

`UseTipsEvent` 被 `PlayCtrl.OnTipsUsed()` 监听：

```csharp
Level.CurrentLevelState.SwitchTips(true);  // HasTips = true
```

因此 `Create()` 检测到 `HasTips == true` 时，会打开 `GameTipsPopup_3`。

### 4.2 Normal → 获取提示（看广告）

```
ShowRewardAd()
  └── RewardAdHelper.TryToShowRewardAd(InGameTips)
       └── 成功
            ├── DispatchEvent(UseTipsEvent)  →  HasTips = true
            └── Create()  →  创建 GameTipsPopup_3
```

### 4.3 PassLevel → 直接通关（消耗金币）

```
TryToPassLevel()
  └── PassLevelByCostCoin()
       ├── 检查金币数量（GameTipsPopupPassLevelCoinCost）
       ├── 不足 → CoinGiftPopup.Create()
       └── 足够
            ├── Bag.RemoveTool(Coin, cost)
            └── OnPassLevelRewardAdCallback("", true)
                 ├── CloseAllUI(Popup)
                 └── DispatchEvent(SwitchGameStateEvent, SUCCESS)
```

### 4.4 PassLevel → 直接通关（看广告）

```
PassLevelByAdReward()
  └── RewardAdHelper.TryToShowRewardAd(InGamePassLevel)
       └── 成功 → OnPassLevelRewardAdCallback("", true)
            ├── CloseAllUI(Popup)
            └── DispatchEvent(SwitchGameStateEvent, SUCCESS)
```

---

## 5. PassLevel 文本显示

`RefreshTips()` 在 `PassLevel` 状态下：

1. 从 `CurrentLevelData.AnswerTips` 读取本地化 key
2. 通过 `LocalizationManager.GetTranslation()` 获取答案文本
3. 如果本地化结果为 `null`，回退显示 `AnswerTips` key；key 也为空时显示空字符串，避免 `null` 文本进入 TMP 打字机计数流程后抛异常
4. 如果 `tipTextAnimDuration > 0`，播放打字机动画（`DOTween.To` 控制 `maxVisibleCharacters`）；完整文本先执行 `UpdateFontAsset()` 与 `ForceMeshUpdate(true, true)`，可见字符数来自强制刷新后的 `TipTxt.textInfo.characterCount`，Tween 完成时再把 `maxVisibleCharacters` 恢复为 `int.MaxValue`
5. 如果 `tipTextAnimDuration <= 0`，直接显示全文

---

## 6. 创建入口

```csharp
// 主入口：创建提示弹窗
public static GameTipsPopup Create()
{
    string panelName = "GameTipsPopup";
    if (PlayCtrl.Instance.Level.CurrentLevelState.HasTips)
    {
        panelName += "_3";  // PassLevel 状态
    }
    return UIManager.Instance.OpenUI<GameTipsPopup>(panelName, UILayer.Popup);
}
```

```csharp
// 已废弃：1.5.0 移除 Lack 状态
[System.Obsolete]
private static GameTipsPopup CreateLackStatePopup()
{
    return UIManager.Instance.OpenUI<GameTipsPopup>("GameTipsPopup_2", UILayer.Popup);
}
```

---

## 7. 配置字段

### 7.1 GlobalConst 本地默认值

| 字段                               | 类型 | 默认值 | 说明               |
| ---------------------------------- | ---- | ------ | ------------------ |
| `GameTipsPopupCoinCost`          | int  | 100    | 提示弹窗消耗金币数 |
| `GameTipsPopupPassLevelCoinCost` | int  | 200    | 直接通关消耗金币数 |

### 7.2 RemoteConfig 远程覆盖

| Remote Key       | 字段 Key                           | 读取方法                                                           |
| ---------------- | ---------------------------------- | ------------------------------------------------------------------ |
| `level_config` | `gameTipsPopupCoinCost`          | `ConfigManager.RemoteConfig.GetGameTipsPopupCoinCost()`          |
| `level_config` | `gameTipsPopupPassLevelCoinCost` | `ConfigManager.RemoteConfig.GetGameTipsPopupPassLevelCoinCost()` |

优先级：**RemoteConfig JSON** → **GlobalConst 本地值** → **fallback 0**

> 注：当前代码中 `PassLevelByCostCoin()` 与 `TryToUseCoins()` 均使用 `GameTipsPopupCoinCost`，`GameTipsPopupPassLevelCoinCost` 已配置但暂未接入代码。

---

## 8. 事件流

| 事件                              | 发送方                                         | 监听方                    | 作用               |
| --------------------------------- | ---------------------------------------------- | ------------------------- | ------------------ |
| `SwitchGameStateEvent(PAUSE)`   | `OnShow()`                                   | PlayCtrl                  | 弹窗显示时暂停游戏 |
| `SwitchGameStateEvent(PLAYING)` | `OnClose()` / `Close()`                    | PlayCtrl                  | 弹窗关闭时恢复游戏 |
| `UseTipsEvent`                  | `TryToUseCoins()` / `OnRewardAdCallback()` | `PlayCtrl.OnTipsUsed()` | 标记已使用提示     |
| `SwitchGameStateEvent(SUCCESS)` | `OnPassLevelRewardAdCallback()`              | PlayCtrl                  | 触发关卡通关       |
| `GamePlayPanelSwitchTipsEvent`  | PlayCtrl_Playing                               | UI 层                     | 控制提示引导显示   |

---

## 9. 文件位置

- **主逻辑**：`Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs`
- **自动生成绑定**：`Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.Generated.cs`
- **事件定义**：`Assets/Game/Scripts/Play/PlayEvents.cs`
- **关卡状态**：`Assets/Game/Scripts/Play/LevelRuntimeState.cs`
- **配置读取**：`Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
- **本地配置表**：`Assets/Configs/Code/LubanConfig/GlobalConst.cs`

---

## 10. 2026-06-30 PassLevel 文本空值崩溃修复记录

### 需求与背景

- 用户提供的运行时堆栈显示：`GameTipsPopup.RefreshTips()` 在 `System.Linq.Enumerable.Count()` 处抛出 `ArgumentNullException: source`，触发路径为 `TryToUseCoins()` 消耗金币后派发 `UseTipsEvent`，随后 `Create()` 打开 `GameTipsPopup_3`。
- 本地代码证据显示，`PassLevel` 状态会读取 `PlayCtrl.Instance.Level.CurrentLevelData.AnswerTips`，再调用 `LocalizationManager.GetTranslation(tipsKey)`；I2 的 `GetTranslation()` 在未找到或传入空 term 时可能返回 `null`。
- 本地配置证据显示 `Assets/Game/Configs/tbleveldata.json` 中存在 `Level_999` 的 `answerTips: ""`，因此答案 key 为空或本地化缺失属于需要兜底的配置边界。

### 影响文件与系统

- 修改文件：`Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs`
- 文档更新：`DevelopMD/GameTips_Logic.md`、`DevelopMD/已有功能设计与使用记录.md`
- 影响系统：仅限 `GameTipsPopup_3` 的 PassLevel 答案文本显示与打字机动画计数。
- 未修改：金币扣减、广告回调、`UseTipsEvent`、`SwitchGameStateEvent`、通关派发、存档、RemoteConfig、Luban 配置、资源加载、analytics、IAP、平台 SDK 和 prefab 绑定。

### 原行为基线

- `LocalizationManager.GetTranslation(tipsKey)` 返回 `null` 时，代码会把 `null` 赋给 `TipTxt.text`。
- `tipTextAnimDuration > 0` 时随后调用 `TipTxt.text.Count()`，当 `TipTxt.text` 为 `null` 会触发 `ArgumentNullException`，导致弹窗打开流程中断。

### 修复后行为

- 本地化结果为 `null` 时，优先回退显示 `tipsKey`；`tipsKey` 也为空时使用 `string.Empty`。若 I2 返回明确的空字符串，则保留空字符串显示，避免改变有意为空的本地化行为。
- 打字机动画的字符数量改为 `ForceMeshUpdate()` 后读取 `TipTxt.textInfo.characterCount`，与 TMP 渲染数据保持一致，并避免对可能为空的字符串执行 LINQ `Count()`。
- 如果答案 key 或本地化配置缺失，弹窗仍可打开、关闭或继续直接通关流程；显示内容可能是 key 或空文本，属于内容配置待补齐问题，不再阻断 UI 流程。

### 数据、资源、UI、事件与平台流

- 数据流：`CurrentLevelData.AnswerTips` → `LocalizationManager.GetTranslation()` → `TipTxt.text`。本次只在 UI 显示边界增加文本兜底，不改配置表或本地化资源。
- UI 流：`TryToUseCoins()` / 广告成功 → `UseTipsEvent` → `Create()` → `GameTipsPopup_3.OnOpen()` → `RefreshTips()`。本次不改变弹窗层级、遮罩或按钮绑定。
- 事件流：继续沿用 `UseTipsEvent` 标记提示已使用，`SwitchGameStateEvent(PAUSE/PLAYING/SUCCESS)` 控制暂停、恢复和通关。
- 平台流：未触碰 Android/iOS SDK、构建配置或平台条件分支；横屏触摸与真机表现仍需手工补验。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已通过，结果为 0 error / 97 warnings；warnings 属于当前工程既有范围，其中包含 `GameTipsPopup.OnCreate()` 的非穷举 switch warning，本次未扩展处理。
- 静态复核确认 `GameTipsPopup.cs` 已不再引用 `System.Linq`，`RefreshTips()` 不再调用 `TipTxt.text.Count()`。

### 剩余风险与未验证项

- 未运行 Unity PlayMode，仍需手工验证：普通提示弹窗、消耗金币获取提示后打开 `GameTipsPopup_3`、答案 key 正常/缺失/为空三类文本显示、关闭恢复游戏、直接通关按钮。
- 未进行 Android/iOS 横屏触摸 smoke，仍需验证弹窗文本空内容时布局、按钮点击和暂停恢复不异常。
- 本次不修正缺失或为空的 `AnswerTips` 配置；若线上内容要求展示真实答案，需要另行补齐 Luban/I2 本地化数据。

### 回滚路径

- 回滚 `Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs` 中 `RefreshTips()` 的文本兜底和 `textInfo.characterCount` 计数改动，即恢复到直接使用 `LocalizationManager.GetTranslation()` 结果和 LINQ `Count()` 的旧实现。
- 若回滚后仍存在空 key 或缺失本地化，原崩溃风险会恢复；回滚前应确认所有可进入关卡的 `AnswerTips` 都有有效本地化文本。

---

## 11. 2026-07-16 Hint 消耗补记 Tips 记录

### 需求与背景

- `GameAchievementUtils.TryToAchievement_ToolConsumes()` 以 `DB.GameData.ToolConsumesCache["Tips"]` 判断提示消耗成就。
- 当前金币获取提示和金币直接通关都通过 `SimpleBag.RemoveTool(GameProps.Coin, ..., CostType.Hint)` 扣费，原逻辑只累计实际道具键 `Coin`，无法推进读取 `Tips` 的成就条件。

### 影响文件与系统

- 修改文件：`Assets/Game/Scripts/Common/SimpleBag.cs`。
- 文档更新：`DevelopMD/GameTips_Logic.md`、`DevelopMD/已有功能设计与使用记录.md`。
- 影响系统：背包消耗记录、`GameData.ToolConsumesCache` 存档和 `ToolConsumes` 类型成就检查。
- 未修改：金币实际扣减数量、`UseTipsEvent`、提示 UI、广告免费提示、首次引导免费提示、分析打点、配置、资源和平台 SDK。

### 原行为基线与修改后行为

- 原行为：枚举重载 `RemoveTool` 只按实际消耗的 `GameProps` 累计一次；`Coin + Hint` 只增加 `ToolConsumesCache["Coin"]`。
- 修改后：`SimpleBag.RecordToolConsume(tool, type)` 统一保留实际道具记录；当 `CostType.Hint` 时额外增加一次 `ToolConsumesCache["Tips"]`，并在同一次保存后执行既有成就检查。`RemoveTool(...)` 在完成扣减与打点后调用该 API。
- 如果实际消耗道具已经是 `GameProps.Tips`，不会再次补记，避免一次调用重复累计两次 `Tips`。
- 因金币获取提示和金币直接通关目前都使用 `CostType.Hint`，两条扣费路径都会补记一次 `Tips`；广告、无限金币和首次免费等未调用 `RemoveTool(..., CostType.Hint)` 的路径不会产生该记录。

### 数据、存档、成就与平台流

- 数据流：`RemoveTool(tool, count, CostType.Hint)` → `RecordToolConsume(tool, CostType.Hint)` → 累计实际道具键 → 补记 `Tips` 键 → `DB.SaveGameData()` → `TryToAchievement(ToolConsumes)` 读取最新 `Tips` 次数。
- 计数语义保持为“成功进入枚举消耗流程的次数”，每次调用增加 1，不按 `count` 增加。
- 未新增 DB 字段或迁移逻辑；旧存档中的 `ToolConsumesCache` 继续沿用现有字典结构。
- 未增加运行时循环、资源加载或平台分支；Android/iOS 共用相同 C# 路径。

### 验证记录

- CodeGraph 复核确认 `SimpleBag.RemoveTool` 保存后调用 `TryToAchievement(AchievementType.ToolConsumes)`，成就检查读取 `ToolConsumesCache["Tips"]`。
- 静态分支复核确认：`Hint + Coin` 同时累计 `Coin` 与 `Tips`；`Hint + Tips` 只累计一次 `Tips`；非 `Hint` 消耗保持原记录行为。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 个错误、98 个现有警告，目标文件没有新增编译错误。

### 剩余风险与未验证项

- 未运行 Unity PlayMode；仍需分别验证金币获取提示、金币直接通关以及非 Hint 的商店消耗后，存档字典和成就解锁结果符合预期。
- 未进行 Android/iOS 真机验证；本次无平台条件分支，但平台成就服务的实际解锁仍依赖登录、远端配置与 SDK 就绪状态。
- 当前规则按 `CostType.Hint` 识别业务语义；未来新增复用该枚举但不应计入提示成就的扣费路径时，需要重新确认分类。

### 回滚路径

- 删除 `SimpleBag.RemoveTool` 中 `type == CostType.Hint && tool != GameProps.Tips` 的补记分支，即恢复只记录实际消耗道具的原行为。
- 回滚不会影响既有金币扣减和分析打点，但回滚前已写入存档的 `Tips` 次数不会自动减少；如需数据回退，必须另行设计明确的存档迁移。

---

## 12. 2026-07-17 首次 FreeTips 补记 Tips 消耗

### 需求与原有基线

- `GameTipsPopup.UseFirstGuideFreeHint()` 原只关闭首次免费状态、派发 `UseTipsEvent` 并打开答案弹窗，没有调用背包消耗入口。
- 因此首次 FreeTips 不会增加 `GameData.ToolConsumesCache["Tips"]`，也不会触发 `ToolConsumes` 成就检查。

### 修改后行为与边界

- `SimpleBag.RecordToolConsume(GameProps tool, CostType type)` 从原 `RemoveTool(...)` 的内联逻辑抽出，统一负责实际道具计数、Hint 补记 Tips、保存和消耗成就检查，不修改 `GameTools` 数量。
- FreeTips 实际消费时，`UseFirstGuideFreeHint()` 只调用 `RecordToolConsume(GameProps.Tips, CostType.Hint)`；`GameTipsPopup` 不再直接读取或修改 `GameData.ToolConsumesCache`。
- 计数调用发生在 `_isFirstGuideFree` 有效性检查通过后、状态消费和 `UseTipsEvent` 派发前；引导 Flow 和 YesBtn 复用同一入口，重复调用不会重复计数。
- `SimpleBag` 保存失败时仅记录 Bear Logger 警告并保留内存计数，不阻止免费提示继续发放，与现有提示消费的运行时优先语义一致。
- 本次不扣金币、不增加 `Coin` 消耗次数，也不改变广告、无限金币、普通金币提示、UI、配置、资源、analytics 或平台 SDK 流程。

### 验证、风险与回滚

- CodeGraph 已确认 `RemoveTool(...)` 与 `UseFirstGuideFreeHint()` 都调用 `RecordToolConsume(...)`；Popup 已无 `DB.GameData`、`ToolConsumesCache`、保存或成就调用。静态检查未新增编辑器生命周期、每帧逻辑或全局事件。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 构建成功，结果为 0 个错误、98 个项目既有警告；最终 diff 检查结果见本次交付记录。Unity PlayMode、存档重载、成就阈值和 Android/iOS 成就服务仍待验证。
- 回滚时把消耗记录恢复到 `RemoveTool(...)` 内联实现，并删除 `RecordToolConsume(...)` 及 FreeTips 调用；已持久化次数不会自动减少。

---

## 13. 2026-07-18 首次提示引导 ClickBlock 点击拦截

### 需求与背景

- 首次提示引导打开 Normal `GameTipsPopup` 后，需要在引导流程结束前屏蔽关闭、广告、购买等额外交互，只保留动态 `GuideFinger` 的指定点击区。
- 原有首次免费模式只通过 `CustomButton.Interactable` 控制三个已知按钮，没有全屏射线拦截；Popup 中若存在其它可交互内容，不受该按钮状态保护。

### 影响文件、资源与系统

- 脚本：`Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs`、`Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs`。
- 资源：`Assets/Game/UI/GameTipsPopup/Resources/GameTipsPopup.prefab`，绑定既有全屏 `ClickBlock.prefab` 实例并设置为默认 inactive。
- 影响范围：仅首次免费提示引导打开的 Normal 提示弹窗，以及引导结束/取消/失败时的同步清理。
- 未修改：普通提示消费、广告/金币扣费、`UseTipsEvent`、存档、RemoteConfig、答案弹窗、资源地址、analytics、IAP 和平台 SDK。

### 修改后行为与 UI 流

- `GameTipsPopup.SetClickBlocked(bool)` 是不感知引导、动画或其它场景的通用运行态开关，只切换当前 Popup 根节点下的 `ClickBlock`；序列化引用缺失时只按当前根节点的同名直接子节点兜底，不创建对象或访问全局层级。
- ClickBlock 是否展示完全由调用方判断。当前 `GameTipsGuideFlow` 创建并准备 Normal 弹窗后立即开启；若节点缺失则返回 `Failed`，并由 `GuideFlowBase.RunAsync(...)` 的 `finally` 进入统一收尾，避免在无完整拦截时继续引导。
- 层级顺序为普通弹窗内容 → ClickBlock → 运行时动态创建的 GuideFinger。ClickBlock 拦截底层额外内容，GuideFinger 的点击区位于最后层级；点击完成后 Flow 继续调用既有 `UseFirstGuideFreeHint()`，不依赖底层 YesBtn 接收射线。
- 流程完成、取消、失败或异常退出时，`OnFlowStopped()` 负责关闭 ClickBlock 并恢复首次免费模式；即使 Popup 已关闭，也会先同步关闭遮罩，避免 UI 实例复用留下旧锁。Popup 自身生命周期不判断 ClickBlock 显隐。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，结果为 0 个错误、99 个项目既有警告。
- 静态检查确认 Prefab 序列化引用指向本地 stripped GameObject，ClickBlock 默认 `m_IsActive=0`、位于普通内容之后；脚本未新增 `Update`、编辑器生命周期、DB/save、资源加载或全局事件。
- 最终 `git diff --check` 与目标文件差异复核结果见本次交付记录。

### 剩余风险与未验证项

- 未运行 Unity PlayMode；仍需验证等待免费剧情、GuideFinger 点击、流程取消/失败、打开答案弹窗和关闭后的实际 EventSystem 射线命中及遮罩恢复。
- 未进行 Android/iOS 横屏触摸 smoke；代码无平台分支，但不同分辨率下的全屏拉伸与手指点击区仍需真机补验。
- `GameTipsPopup_3.prefab` 未增加 ClickBlock；该答案弹窗在既有 Flow 中承担关闭或直接通关交互，本次拦截限定在其下方的 Normal 引导弹窗。

### 回滚路径

- 删除 `GameTipsGuideFlow` 中 `SetClickBlocked(true/false)` 调用和 `GameTipsPopup` 的对应 API、字段。
- 从 `GameTipsPopup.prefab` 移除 ClickBlock 实例及序列化绑定，即可恢复仅依赖三个按钮 `Interactable` 的原行为；不涉及存档、配置或资源迁移。

## 14. 2026-07-24 PassLevel 提示全文显示兜底

### 需求背景与原有行为

- 用户反馈 `GameTipsPopup_3` 的 tips 内容在现有 `maxVisibleCharacters` 打字机动画结束后仍可能显示不全。
- 原实现把完整本地化文本写入 `TipTxt` 后只调用无参数 `ForceMeshUpdate()`，再以
  `textInfo.characterCount` 作为 Tween 终点；Tween 完成后没有显式恢复全文可见状态。
- `GameTipsPopup.OnShow()` 虽会派发 `SwitchGameStateEvent(PAUSE)`，但当前
  `PlayCtrl_Pause` 中写入 `Time.timeScale=0/1` 的代码均已注释，因此本次不把时间缩放视为故障原因，
  也不改变 Tween 的更新模式。
- 静态 prefab 证据显示 `GameTipsPopup_3.prefab` 把 `tip_txt` 高度覆盖为 200、字号覆盖为 56，
  并把 Auto Size 设为 false；运行时代码在 PassLevel 分支会继续把 Auto Size 设为 true。

### 修改后行为

- 完整本地化文本写入后，先调用 `UpdateFontAsset()` 与 `ForceMeshUpdate(true, true)`，
  强制 TMP 在当前对象状态下重新解析文本、字体和网格，再读取 `textInfo.characterCount`。
- 原有 1 秒延迟、`tipTextAnimDuration`、`maxVisibleCharacters` 逐字显示和 Tween target 保持不变。
- Tween 正常完成时把 `TipTxt.maxVisibleCharacters` 设为 `int.MaxValue`，不再让最终全文可见性依赖
  字符统计值与整数 Tween 终点完全一致。
- `OnClose()` 仍通过 `TipTxt.DOKill()` 清理 Tween；没有新增 Tween 字段、公开 API、每帧逻辑或事件。

### 数据、资源、UI 与平台边界

- 仍只读取 `CurrentLevelData.AnswerTips` 和 I2 本地化结果；不修改关卡配置、本地化资源或 prefab。
- 金币、广告、`UseTipsEvent`、暂停/恢复/通关事件、存档、RemoteConfig、analytics、IAP 和平台 SDK
  保持原路径。
- Android/iOS 共用同一 TMP/DOTween 路径，没有新增平台分支或资源加载。

### 验证、风险与回滚

- `git diff --check` 与静态敏感项扫描通过；目标 C# 构建通过，结果为 0 个错误、99 个项目既有警告。
- `uncertain`：尚未取得问题发生语言、实际 tips 文本、运行时 `characterCount` 与最终
  `maxVisibleCharacters` 日志，不能把根因限定为某一个 TMP 内部步骤；本次修复覆盖已确认的薄弱边界。
- 尚未在 Unity PlayMode 验证短/长文本、中日韩回退字体、重复打开、Tween 完成与提前关闭；
  Android/iOS 横屏仍待验证。
- 如果全文可见兜底后仍出现视觉截断，应继续核对 `GameTipsPopup_3` 的 400×200 文本区域、
  Auto Size 最小字号和美术容器边界，而不是继续扩大 Tween 逻辑。
- 回滚时恢复无参数 `ForceMeshUpdate()`，删除 `UpdateFontAsset()` 与 Tween `OnComplete` 全文显示兜底即可；
  prefab、配置、数据和调用方无需回滚。

## 15. 2026-07-27 Draw 专用提示弹窗与 HasTips 生命周期

### 需求与行为

- Draw HUD 不再直接显示 `TipsObject`，`DrawGame_GamePlayPanel.OnClickTips()` 直接调用 `DrawGame_GameTipsPopup.Create()`；弹窗工厂负责校验活动 Draw 关卡运行时与 `HasGrantedHint`。
- 新弹窗继承现有 `GameTipsPopup` Normal prefab 的视觉层级，不修改界面；金币价格、无限金币、金币不足商店和 `InGameTips` 奖励广告继续复用原链路。
- 成功后派发 Draw 专用 `DrawGameHintEvent`，不派发主线 `UseTipsEvent`，避免写入主线 `LevelRuntimeState.HasTips`。
- 自 2026-08-18 起，Draw 将当前关卡会话的 `HasGrantedHint` 获取资格与当前尝试提示显示状态分离。每次 DLevel 初始化后只按当前尝试显示状态执行 `ShowHint(...)`；同关手动 Reset、失败重载和 VictoryPanel Replay 从已保留资格恢复显示，不重复收费。
- 返回选关页、切换关卡或退出 Draw 才同时清除提示资格与显示状态。已经获取提示时再次点击 HUD，弹窗工厂依据 `HasGrantedHint` 进入既有 Draw 跳关弹窗，不再进入首次提示获取流程。

### 边界、验证与回滚

- 两个状态均不写 DB，不新增配置、资源路径、analytics 或平台分支；Draw 弹窗显示期间通过当前 `IPlayPauseHost` 暂停，关闭后恢复。
- C# 构建通过（0 error / 109 个工程既有 warning）；Unity 已成功导入 prefab，最新日志未出现目标脚本或 YAML 错误。
- Unity PlayMode、广告沙盒、商店叠层、Reset 玩家可见流程和 Android/iOS 横屏仍待验证。
- 完整影响范围、风险、手工清单与回滚见
  `Assets/Game/UI/GameTipsPopup/Scripts/DrawGame_GameTipsPopup功能与风险评估.md`。

## 16. 2026-07-27 返回游戏主界面的缩放关闭动画

### 需求、基线与影响范围

- 需求：玩家通过 `GameTipsPopup` 关闭提示并返回游戏主界面时，也需要播放与
  `TransformPopup` 相同机制的缩放关闭动画。
- 原行为：`GameTipsPopup.prefab` 已挂载 `UIScaleAnimation`，打开动画启用，但
  `_playCloseAnim` 为 `false`；`Close()` 无论调用 `CloseUI(this)` 还是
  `CloseAllUI(UILayer.Popup)`，`BaseUIView.OnHide()` 都只能同步完成该组件的关闭阶段。
- 影响文件仅为 `Assets/Game/UI/GameTipsPopup/Resources/GameTipsPopup.prefab`。
  `GameTipsPopup_2`、`GameTipsPopup_3` 是该 prefab 的变体，未覆盖此动画开关，因此继承相同行为。

### 修改后行为与 UI 流

- 将基础 prefab 的 `UIScaleAnimation._playCloseAnim` 设为 `true`。
- 普通关闭继续经过 `UIManager.CloseUI(this)`；已经获得提示时返回游戏主界面继续经过
  `UIManager.CloseAllUI(UILayer.Popup)`。两条路径都会先进入 `BaseUIView.OnHide()`，
  由既有 `UIScaleAnimation.PlayCloseAnimation()` 把目标 Root 从原比例缩放到 `0.8`。
- 保留 `GameTipsPopup` 原有 `0.2` 秒时长、`OutBack` 缓动和 unscaled time；没有修改打开动画、
  按钮绑定、暂停/恢复事件、提示获取、直接通关、存档、配置、资源加载、analytics 或平台 SDK。

### 验证、剩余风险与回滚

- 静态校验确认 `GameTipsPopup.prefab` 与 `TransformPopup.prefab` 使用同一个
  `UIScaleAnimation` 脚本 GUID，且缩放起点、缓动和 unscaled time 配置一致。
- 已执行 prefab YAML、变体继承和目标差异检查；Unity PlayMode 与 Android/iOS 横屏触摸效果仍待验证。
- 回滚时把 `GameTipsPopup.prefab` 的 `_playCloseAnim` 恢复为 `false`；不涉及代码、数据或资源迁移。

## 17. 2026-08-07 Draw HUD Tips Warning 控制

### 需求、基线与实现

- Draw HUD 已有默认隐藏的 `tips_btn/warning` 图片与旋转表现，但原 prefab 未挂
  `GamePlayPanelTipsSwitcher`，`DrawPlayCtrl` 也未消费已经存在的
  `drawGameShowTipsTimeLimit`、`drawGameShowTipsFailCount` 配置。
- 当前 `DrawPlayCtrl` 只在 `DrawPlayPhase.Drawing` 且未暂停时累计本次尝试的 warning 等待时间；
  超过时间阈值或本关累计 revive 达到次数阈值后，只派发一次既有
  `GamePlayPanelSwitchTipsEvent(true)`。
- `tips_btn` 复用主线 `GamePlayPanelTipsSwitcher` 控制现有 warning；点击 Tips 或成功获取 Draw 提示时
  派发 `GamePlayPanelSwitchTipsEvent(false)`。Reset、失败重试和 Victory Replay 会重置尝试级显示门禁，
  但不会改变 `_analyticsReviveCount` 的既有打点生命周期。
- DLevel_001 依据解析后的 `DLevel_001.prefab` 资源路径隐藏整个 Tips 按钮，并跳过 warning 阈值派发；
  不以当前排序 ID 判断。Reset/Replay 重建仍隐藏，进入其它 Draw 关卡时显式恢复显示。

### 边界、风险与回滚

- 未新增事件类型、存档、配置、资源地址、analytics 或平台分支；读取 Draw 专属阈值并复用当前尝试
  的累计 revive，不修改其增减与持久化。
- 目标 prefab 静态检查通过：187 个 YAML anchor 无重复，switcher 与 warning/旋转组件引用完整；
  目标差异检查通过，`Assembly-CSharp.csproj` 构建为 `0 error / 101 个工程既有 warning`。
- `uncertain`：Unity PlayMode 和 Android/iOS 横屏仍需验证时间/次数阈值、暂停计时、点击隐藏、
  Reset 后重新显示、DLevel_001 隐藏与后续关卡恢复，以及快速玩法切换时共享 UI 事件的可见性。
- 完整影响范围、静态绑定要求与回滚见
  `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel提示Warning功能与风险评估.md`。

## 18. 2026-08-13 Tips Warning 展开与自动隐藏

### 需求、行为与影响范围

- `GamePlayPanelTipsSwitcher` 收到 `GamePlayPanelSwitchTipsEvent(true)` 后，先停止旧显示序列、激活
  `Target` 并把本地缩放设为 `0`，再通过 DOTween 以 `OutQuad` 从 `0` 展开到 `1`。
- Inspector 新增 `expandDuration` 和 `displayDuration`：默认分别为 `0.5` 秒和 `3` 秒；展示时间从展开
  完成后开始计算，结束时直接执行 `Target.SetActive(false)`。两段时间使用 unscaled time。
- 重复显示会从头播放并重置计时；`GamePlayPanelSwitchTipsEvent(false)`、组件禁用或销毁会停止旧序列，
  显式隐藏与自动隐藏均不播放收起动画。
- `RotateFloatHandle` 保留为可选表现：目标存在该组件时沿用原复位逻辑，不存在时也能正常展开，避免主线
  新 `warningBubble` 结构触发空引用。主线与 Draw HUD 继续复用同一事件和 switcher；本次未修改 prefab、
  配置、存档、资源加载、analytics 或平台 SDK。

### 验证、风险与回滚

- 完整 C# 构建通过（`0 error / 1119 个工程既有 warning`）；目标脚本 `git diff --check` 通过，仅有
  工作区换行提示。静态确认主线与 Draw HUD 的 switcher 脚本 GUID 和 `target` 引用非空。
- `uncertain`：Unity PlayMode 的展开、自动隐藏、重复显示抢占、主线新美术、Draw 旋转表现及 Android/iOS
  横屏仍待手工验证。原生覆盖层完全暂停 Unity PlayerLoop 时，unscaled Tween 也会暂停到帧恢复。
- 回滚只需恢复 switcher 的直接 `SetActive` 分支；无需迁移事件、prefab、配置、存档或资源。详细记录见
  `Assets/Game/UI/GamePanel/Scripts/GamePlayPanelTipsSwitcher功能与风险记录.md`。

## 19. 2026-08-26 主线同关 Reset 保留答案资格

### 需求与行为边界

- 玩家在当前主线关通过金币、广告或首次免费流程获得答案后，`HasTips=true` 作为当前关卡会话资格保留；主动 Reset 与失败重试只创建新尝试，不再要求重复付费或看广告。
- Reset 重建后再次点击 Tips，`GameTipsPopup.Create()` 读取保留的 `HasTips` 并直接打开 `GameTipsPopup_3`，继续显示当前关 `AnswerTips`。
- 进入主线 ChoiceLevel、启动 VictoryPanel Replay、切换到新关或以后通过 `EnterLevelEvent` 重新进入游戏时清除 `HasTips`；成功结算本身只结束打点周期，资格保留到玩家选择下一步，不会跨关、跨会话或跨应用持久化。
- `HasClickTips`、`HasShownGuideBubble` 与尝试计时仍在每次 Reset/失败重试时清理；提示 Warning 和首次免费引导在 `HasTips=true` 时继续被现有门禁阻止。

### 影响范围与数据流

- 修改 `LevelRuntimeState.ResetDataPreservingRevive()` 与 `ResetData()` 的职责边界，并在 `PlayCtrl.ExitToLevelSelection()` 补充 ChoiceLevel 清理；未修改 `GameTipsPopup`、Prefab、提示价格、广告、背包消耗记录或答案文本。
- 数据流仍为 `UseTipsEvent -> PlayCtrl.OnTipsUsed() -> HasTips=true`；Reset 与成功结算保留该运行时值，ChoiceLevel、Victory Replay 或 `StartLevel()` 边界把它恢复为 `false`。
- 没有新增存档字段、全局事件、配置、资源加载、analytics、IAP 或平台 SDK 调用；Reset 的既有打点与插屏时序保持不变。

### 验证、风险与回滚

- 目标差异、调用点与敏感项静态检查通过；完整 `dotnet build .\Assembly-CSharp.csproj -m:1 --nologo /clp:ErrorsOnly` 补齐依赖后通过（`0 error / 1138 warning`），最终代码的增量定向构建再次通过（`0 error / 120 warning`）。此前定向构建分别因缺少 NuGet 资产文件和 `Temp/bin` Unity 依赖 DLL 被环境阻断。
- `uncertain`：尚需 Unity PlayMode 验证金币、广告、首次免费三条答案获取路径在主动 Reset/失败重试后直接显示答案，并验证 ChoiceLevel、Victory Replay、重新进关会恢复付费入口；Android/iOS 横屏触摸仍未验证。
- 回滚时恢复 `ResetDataPreservingRevive()` 清除 `HasTips` 的旧行为，并移除 `PlayCtrl` 在 ChoiceLevel 与 Victory Replay 边界的对应调整；不涉及存档迁移、Prefab 或配置回滚。

## 20. 2026-09-03 金币不足改为打开 CoinGiftPopup

### 需求、基线与影响范围

- 主线获取提示和直接通关的金币不足分支，需要先打开已有 `CoinGiftPopup`，而不是直接跳转普通商店。
- 原行为是 `ShopPanel.Create()` 后同时显示 `U_HurryTips_NeedCoin` 系统提示；玩家无法先使用 CoinGift 的 60 金币广告或首购/常规商品。
- 本次仅修改 `Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs` 的两个不足分支；用户已调整的三个 GameTips prefab 未重新生成或改写。

### 修改后行为与数据流

- `TryToUseCoins()` 与 `PassLevelByCostCoin()` 在余额不足时统一调用 `CoinGiftPopup.Create()`；无限金币、首次免费提示、金币足够扣除、获取提示和通关事件保持原路径。
- UI 栈为 `GameTipsPopup -> CoinGiftPopup`。打开 CoinGift 时底层 GameTips 只进入 `OnHide`，不执行 `OnClose`，因此不会派发 `PLAYING`。CoinGift 关闭后底层 GameTips 重新 `OnShow`，玩家回到原提示意图。
- CoinGift 广告或购买成功只按其自身配置发金币并播放飞奖励动画，弹窗保持打开，不替玩家自动再次点击金币提示。CoinGift 内进入商店时先关闭 CoinGift，但不关闭底层提示弹窗或主动恢复游戏。
- 未新增存档、Luban/RemoteConfig、资源地址、全局事件、analytics 或平台 SDK 调用；仍由 CoinGift 自身管理广告、IAP 与按钮锁。

### 验证、剩余风险与回滚

- 定向静态检查确认两个余额不足分支均只调用 `OpenCoinGiftPopup()`，已不直接调用 `ShopPanel.Create()`。
- 执行 `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，首次结果为 `0 error / 122 warning`，文档更新后用同命令最终复验为 `0 error / 0 warning`。
- `uncertain`：尚未在 Unity PlayMode 实测 CoinGift 广告、IAP、进入商店再返回、连续开关和 Android/iOS 横屏原生覆盖层。
- 回滚时只需把 `GameTipsPopup` 中两个不足分支恢复为直接打开 Shop 和系统提示；不涉及玩家存档、Prefab 或配置迁移。

## 21. 2026-09-07 新按钮结构绑定与无效脚本清理

### 需求、基线与影响文件

- 用户调整基础 Prefab 后明确确认：普通提示与答案/跳关两个界面都只保留金币按钮，移除广告按钮。
- 任务开始时基础资源已包含用户未提交的美术修改：新按钮容器、消耗区域和装饰图片已存在，但 YesBtn、NoBtn、YesTxt 均为空。答案变体仍指向已删除的旧按钮及其子节点。
- 本次修改 `Assets/Game/UI/GameTipsPopup/Resources/GameTipsPopup.prefab`、`GameTipsPopup_3.prefab` 和 `Assets/Game/UI/GameTipsPopup/Scripts/GameTipsPopup.cs`、`GameTipsPopup.Generated.cs`。
- Draw 的两个 Tips 资源已是独立 Prefab，不引用主线基础 Prefab；此次未修改 Draw 脚本、资源或广告入口。

### 最终行为、复用与数据流

- 基础 Prefab 显式绑定新金币按钮和消耗文本。将已有 CustomButton、音效、缩放与震动组件移到按钮容器 `yes_btn`，让背景、文案和消耗区域共用按钮父级并一起缩放；`yes_txt` 对应动态价格文本。保留美术位置、尺寸与图片。
- 答案变体清除指向已删除基础对象的覆盖、失去父节点的新增文本/图标和旧按钮 stripped 引用；继承新金币按钮与消耗文本，保留 state=2、答案文本尺寸/动画参数、仍有效的显隐覆盖，并将新按钮文案本地化设为 `U_Common_Btn_Skip`。
- 主线移除 NoBtn、Loading3Txt、NoTxt、广告绑定/调用/回调和无调用的 CreateLackStatePopup。保留 Lack 枚举值及旧 `_2` 资源，避免改变已有序列化状态数值；该废弃资源不由现有工厂打开，本次未修复其历史覆盖。
- 首次免费引导只校验和锁定关闭、金币两个按钮，继续复用原有免费发放、Tips 消耗记录、UseTipsEvent、ClickBlock 和关闭等待流程。
- 金币/无限金币提示仍派发 UseTipsEvent 后打开 `_3`；金币不足仍打开 CoinGiftPopup。金币跳关完成方法改为无广告参数的 CompletePassLevel，保留关闭 Popup → SUCCESS 事件 → 成功音效的原顺序，不改关卡打点契约。
- 没有新增配置、存档、全局事件或平台调用。两处旧注释因已移除广告按钮而修正；通关方法内“看广告”注释随失效广告回调结构移除。

### 验证与未验证风险

- 最终定向构建：`dotnet build ./Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，0 个错误、121 个警告。
- 静态资源校验：基础资源 113 个 YAML anchor 无重复，所有本地 fileID 引用可解析，6 个关键序列化字段均非空；仅有关闭和金币两个 CustomButton。答案变体所有基础目标引用均有效，state=2 与 Skip 本地化覆盖存在。
- CodeGraph CLI 不可用，规则引用的 `../specs/037-remote-resource-loading/plan.md` 不存在；本次依据本地代码、Prefab 与已有功能文档核对。
- `uncertain`：无可用 Unity 控制工具，尚未完成 Unity 导入/PlayMode 和 Android/iOS 横屏实测。需验证两个界面的背景/文案/消耗区点击、按压缩放、短长答案、金币足够/不足/无限金币、首次免费引导与取消、关闭恢复及跳关成功。

### 回滚

- 仅撤销本节涉及的脚本和绑定改动，保留用户原有美术修改。若恢复双按钮业务，必须同时恢复对应按钮资源和答案变体有效引用，不能仅恢复旧脚本，否则空按钮引用问题会再次出现。无需迁移存档或配置。
