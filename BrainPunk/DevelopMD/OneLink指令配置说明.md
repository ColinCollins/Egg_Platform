# OneLink 指令与示例

## 关卡跳转指令

```text
deep_link_value=open_level
deep_link_sub1=<main|default|draw|iwanner>
deep_link_sub2=<目标，可省略>
```

| 参数 | 填写内容 |
| --- | --- |
| `deep_link_value` | 固定为 `open_level` |
| `deep_link_sub1` | `main`／`default`：主线；`draw`：画线；`iwanner`：IWanner |
| `deep_link_sub2` | 按下表填写；省略或未命中配置时使用对应玩法的默认回退 |

| 目标 | sub1 | sub2 示例 | 含义 |
| --- | --- | --- | --- |
| 主线关卡 | `main` 或 `default` | `Level_002` | 进入对应资源 Path 的主线关卡 |
| Draw 关卡 | `draw` | `DLevel_002` | 进入对应资源 Path 的画线关卡 |
| IWanner Group | `iwanner` | `IGroup_2` | 进入 Group 2；同组续关，换组从第一小关开始 |
| IWanner 小关 | `iwanner` | `ILevel_007` | 强制进入对应小关；当前为 Group 2 第二小关 |
| IWanner TBN | `iwanner` | `ILevel_999` | 进入 TBN |
| 玩法默认关卡 | 任一支持的玩法 | 不填写 | 使用该玩法的默认回退 |

IWanner 使用 `IGroup_<组ID>` 或实际资源 Path `ILevel_XXX`，不要填写裸数字 `2`／`30007`。

## 链接示例

将示例中的 `https://<已配置域名>/<路径>` 替换为实际配置的链接地址。

### 主线：Level_002

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=main&deep_link_sub2=Level_002
```

### Draw：DLevel_002

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=draw&deep_link_sub2=DLevel_002
```

### IWanner：Group 2

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=IGroup_2
```

### IWanner：Group 2 第二小关 ILevel_007

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=ILevel_007
```

### IWanner：TBN

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=ILevel_999
```

### Draw：默认回退关卡

```text
https://<已配置域名>/<路径>?deep_link_value=open_level&deep_link_sub1=draw
```

## 参数对象示例

```json
{
  "deep_link_value": "open_level",
  "deep_link_sub1": "iwanner",
  "deep_link_sub2": "ILevel_007"
}
```

## 金币和皮肤指令

在 OneLink 后台配置以下参数，每条指令领取一种奖励。

| 参数 | 金币 | 皮肤 |
| --- | --- | --- |
| `deep_link_value` | `getCoin` | `getSkin` |
| `deep_link_sub1` | 奖励唯一标识，必填 | 奖励唯一标识，必填 |
| `deep_link_sub2` | 正整数金币数量 | Skin 配置的精确 ID |
| `deep_link_sub3` | 可选：目标 Guru Uid；留空不限用户 | 同左 |

### 金币示例

领取 500 金币：

```text
deep_link_value=getCoin&deep_link_sub1=coin_20261010_001&deep_link_sub2=500
```

### 皮肤示例

领取皮肤 `201001`（当前本地 Skin 配置中存在）：

```text
deep_link_value=getSkin&deep_link_sub1=skin_20261010_001&deep_link_sub2=201001
```

限定玩家时增加 `deep_link_sub3=<实际 Guru Uid>`，必须与当前玩家精确匹配。

### GM 测试

首页 → GM → OneLink 测试，sub3 可留空。

| 点击按钮 | sub1 | sub2 |
| --- | --- | --- |
| 金币测试 | `coin_20261010_001` | `500` |
| 皮肤测试 | `skin_20261010_001` | `201001` |

### 领取限制

- 金币和皮肤共用领取记录，同一 sub1 只能领取一次；重复测试换新标识，不要清空玩家记录。
- 皮肤填配置 ID，不填图片或 Prefab 名；不存在的 ID 会被拒绝，已拥有皮肤不重复授予。
- 奖励等待首页及阻挡弹窗关闭后执行；关卡内请求会被忽略。GM 测试会真实修改当前存档。
- 成功发奖并保存后显示奖励弹窗。日志：`reward_completed` 为完成，`reward_duplicate` 为已领取，`reward_rejected` 为拒绝。

实现入口：[参数解析](../Assets/Game/Scripts/SDK/Services/Core/OneLinkRequest.cs)、[奖励执行](../Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.Rewards.cs)。

## Facebook 延迟深链：Android／Google Play 测试

### 1. 先验证游戏路由

使用带 `DEBUG_MODE` 的 Android 测试包，在 GM 的 OneLink 测试入口依次提交：

```text
main / Level_002
iwanner / IGroup_2
iwanner / ILevel_007
```

这一步只验证游戏解析与进关，不证明 Facebook 回调、广告归因或 Google Play 首装链路。

### 2. 准备 Google Play 测试包

- 用项目正式包名 `com.brain.tricky.puzzle.games.test.quest` 构建签名 AAB，并使用高于 Play Console 现有版本的 versionCode。
- 上传到 Google Play **Internal testing**，添加测试 Google 账号并发布；测试者必须先打开 Play Console 提供的 opt-in 链接加入测试，再从 Google Play 安装。首次测试链接和更新可能需要数小时生效。
- 不要用 ADB 安装或直接侧载 APK 代替本项；侧载只能测游戏与系统唤起，不能证明“Meta 点击 → Google Play 安装 → Facebook deferred callback”。

Google 官方流程：<https://support.google.com/googleplay/android-developer/answer/9845334>

### 3. 核对 Meta 后台

- Meta App Dashboard 的 Android Package 必须为 `com.brain.tricky.puzzle.games.test.quest`，ClassName 必须与最终合并 Manifest 的启动 Activity 一致，并配置 Play App Signing 对应 Key Hash。
- 开启 App 的 Deep Linking；先在 App Ads Helper 验证 SDK/deep-link 设置。
- 创建面向未安装用户的 App Install／App Promotion 测试广告，在广告的 Deferred Deep Link 中先填稳定主线目标：

```text
uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=main&deep_link_sub2=Level_002
```

主链路通过后，再分别测试：

```text
uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=IGroup_2
uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=ILevel_007
```

Meta 官方说明：<https://developers.facebook.com/docs/android/deep-linking/>、<https://developers.facebook.com/documentation/app-ads/deep-linking>

### 4. 真机步骤与判定

1. 使用加入 Google Play 测试轨的真实 Android 设备；确保游戏未安装。
2. 从 Facebook/Instagram 的测试广告实际点击，不要手工复制最终 URL 代替广告点击。
3. 跳转 Google Play 后安装并首次打开游戏，等待启动弹窗结束。
4. 导出日志，依次确认：`facebook_waiting_sdk` → `facebook_requested` → `facebook_callback` → `queued` → `target_resolved` → `entry_accepted`。
5. `entry_accepted` 只表示同步受理；还必须核对对应玩法的 `level_start` 和实际画面。IWanner 成功样本要求 `target_resolved` 中 `fallback=False`，并确认 Group/Path/index 与目标一致。

Windows 日志命令（ADB 不在 PATH 时使用 Android SDK 的完整路径）：

```powershell
adb logcat -c
adb logcat | Select-String -Pattern '\[OneLink\]|facebook_|target_resolved|entry_accepted|level_start'
```

常见失败定位：

- 没有 `facebook_requested`：Facebook SDK 尚未初始化或当前不是 Android/iOS Player。
- `facebook_rejected, reason=result_failed`：原生 SDK 返回失败、取消或错误。
- `missing_deep_link_parameters`：广告回传的 URL/Extras 没带完整 `deep_link_value` 与 `deep_link_sub1`。
- `target_resolved ... fallback=True`：sub2 格式、大小写或当前配置未命中。
- 只有 `entry_accepted`、没有 `level_start`：继续查原资源加载/玩法入口，不能判定深链已完整成功。
