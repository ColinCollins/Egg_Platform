# SRN 渠道 GCD 参数与投放交接清单（2026-09-23）

## 2026-10-10：iOS Facebook实际ATT授权后的取链窗口（本轮授权修订）

- 用户已批准iOS授权等待不占原生五秒，并要求同步远端。iOS14+只读系统ATT，未决定／未知最多30秒；Authorized后才开始SDK就绪＋取链共用的五秒，最多调用一次。拒绝／受限／查询异常／超时退出，不自动补发、不重复权限弹窗、不阻塞正常游戏／Loading；iOS13不等待不存在的ATT，Android原五秒及Editor隔离不变。
- AF仍并行初始化与取目标；Meta／来源未知的安装候选按原规则等待Facebook，已知Google仍可由AF执行。显式URI／GM／主动玩法／退出取消等待；已有当前运行执行锁不依赖五秒窗口是否建立，执行后不得换来源再进关或发奖。旧截止不重开，不新增跨重启恢复或存档键；AF自身UDL/GCD仲裁、HTTP(S)／URI／Extras、非空奖励ID和campaign原协议不变。
- 原完整QA包A/C分别在Facebook App自身跟踪开／关时，系统授权后返回Facebook目标并完成Level_022；B旧时序即使Facebook自身开仍出现权限／IDFA前置错误。当前设备／账号／Helper历史条件下，Facebook App自身开关开启不是成功必要条件；不能推广所有SDK、从未授权设备或真实投放／商店安装，也不表示目标游戏ATT不重要。
- 本次净化发布仅迁移游戏侧时序和脱敏诊断，保留已发布解析／执行接口与SDK版本，不附本机QA工具／日志／测试产物。候选回归与编译、完整QA真机证据分别报告，详见OneLink模块文首。
- 发布候选已通过319项四条件回归和四条件Unity引用定向编译（每组1176源码、0 error）；上游Facebook解析、现有执行锁及其它运行文件逐字保留。该候选没有重新出包装机，不能把集成回归冒充另一份真机样本；实测ATT循环与取消／诊断逻辑一致性已核对。

## 2026-10-10：撤销跨进程恢复，防重范围回到当前运行

- 用户明确要求移除上游没有的安装目标持久化／跨重启恢复。下方“重启后仍不重放”的新增消费标记方案撤销；待执行目标仅保留内存，退出后不由游戏新增记录补发。
- 当前运行内仍保留原生优先／AF 兜底及执行锁；上游原有奖励领取 ID 存档、iOS 网络错误恢复／SDK 回调处理不变。非空奖励 ID 和 campaign 旧格式映射均保留，不清领取历史或旧版本可能留下的独立记录。
- 未收到或退出前尚未执行的目标不保证在重开后落地；不同 ID 的跨重启／跨设备领取不由本次内存锁保证。当前规则与验证见 OneLink 模块文档文首，以下持久化内容只保留为旧版本历史。

## 2026-10-10：非空奖励 ID 与安装目标单次消费（本轮已授权变更）

- Facebook／GA4F 原生目标及 AF `af_dp` 奖励使用投放指定的非空 `deep_link_sub1`，不再仅允许 `coindl/skindl`。金额／皮肤／用户校验、已有领取记录保留；Facebook 继续兼容上游 HTTP(S)／业务 URI／Extras，GA4F／`af_dp` 不扩大原 URI 白名单。普通 UDL／完整 GCD 的非空 ID 规则不变。
- campaign 命名不改：`活动名_DL-GC-100` 仍生成 `coindl`，`活动名_DL-GS-201001` 仍生成 `skindl`；未加入显式 ID 新格式，也不删除原格式。下方历史“广告奖励只放行固定 ID”仅对本轮之前的 URL 实现成立，不能继续用于拒绝新 URL／原生目标的非空 ID。
- 方案 A：对应原生目标优先、AF 兜底；AF 中合法 `af_dp` 优先于 campaign。执行前选择完整目标，执行开始前保存安装消费状态，开始执行后不因其它来源填写了不同 ID／内容而重发，重启后仍不重放。后续普通活动链接保持原规则；优先级不把不同 ID 自动合并成同一领取资格，不扩大为服务端跨设备防重。
- 同一活动仍建议各来源使用相同 ID／内容；只有 campaign 时只能采用其映射目标。URL 用户限制不向无该字段的 campaign 自动传播，原已批准的无效 URL 回退边界保留。本轮未改投放后台；验收及回滚见同目录 OneLink 模块文档的 2026-10-10 合并章节。

## 2026-09-29：受控广告 URL／campaign 目标（现行约定，更新下方旧范围）

- 仅首次 `af_status=Non-organic/is_first_launch=true` 的 GCD 参与目标转换；UDL 有效目标优先，GCD 先到仍最多等五秒，最终共用既有首页门禁与 `OneLinkRequest`。已具备完整原业务字段的 GCD 仍可独立受理。仅受控 Meta `Facebook Ads`（Android/iOS）及 Google `googleadwords_int`（Android）的 `af_dp` 使用固定 `uribrainpunk://link` URI；既有 Google iOS campaign 兼容性不等于放开 Google iOS 广告 URL。不同来源目标冲突时合法 URL 优先并只输出脱敏告警；无效 URL 仍回退到完整原字段，最后到受控 campaign。下方“现阶段范围”与“逐 campaign ID 登记”是 2026-09-23 旧包历史，不再作为本轮白名单。
- 关卡目标：campaign 名称**最后一个下划线字段**为 `DL-OL-{main|default|draw|iwanner}-{001..999}`，生成 `open_level/main|draw|iwanner/Level_XXX|DLevel_XXX|ILevel_XXX`（`default` 归一为 `main`）。广告 URL 示例：`uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=Level_002`。只接受固定入口、完整目标及已批准来源；不从广告名称前段、广告组或任意 URL 猜测 Path，`000`／错误位数／未知玩法拒绝。同一 campaign 只对应一个投放目标；资源 Path 是否在最终配置存在须投放前检查，不把编号当排序号。
- 奖励目标：`活动名_DL-GC-100` → `getCoin/sub1=coindl/sub2=100`；`活动名_DL-GS-201001` → `getSkin/sub1=skindl/sub2=201001`。金币数量须为正整数且处于 `int` 范围；Skin Id 是纯数字配置 ID，不固定六位。广告 URL 示例分别为 `uribrainpunk://link?deep_link_value=getCoin&deep_link_sub1=coindl&deep_link_sub2=100` 和 `uribrainpunk://link?deep_link_value=getSkin&deep_link_sub1=skindl&deep_link_sub2=201001`，可选 `deep_link_sub3` 限定 Guru Uid；广告奖励只放行与动作精确匹配的上述固定领取 ID，已有 UDL／GCD 原完整字段的领取 ID 仍可变。campaign 不编码 Uid，根部仅带残缺业务字段或 `sub3` 时不凭 campaign 生成公共奖励。
- **用户确认保留原回退逻辑及风险**：定向奖励 URL 无效时，后续完整 GCD 或公共 campaign 若有效仍可发奖，回退目标不继承 URL 的 Uid 限制。投放不得将“定向 URL＋公共 campaign 兜底”当作严格指定用户发奖。两种固定 ID 只在同一份本地 `GameData.OneLinkClaimedRewardIds` 去重，清档／换设备可能重领，不提供服务端验券。真实 Meta／Google 回调字段、广告后台开关、签名包商店首次安装及防伪边界均为 `uncertain`；合成测试不等于上线验收。回滚仅移除受控映射，保留已有 UDL／完整 GCD 协议、上游 ERROR 恢复和玩家已领取存档。

## 现阶段范围

- **2026-09-23 旧包**仅接受 `af_status=Non-organic/is_first_launch=true` 且已有完整关卡业务字段的 GCD；仅有 campaign 或广告组字段时不会跳关。当前新增的严格受控转换见文首，仍须复用原有 UDL／GCD 候选与路由门禁。已安装链接唤起不证明安装归因。
- **历史约束**：原本禁止从任何 campaign 或 `af_dp` 猜关卡及逐活动 ID 放行，现仅允许文首已批准的末尾语法与固定 URI；不接受任意场景 URI，也不能靠给 SRN 链接添加 query 伪造 SDK 回调字段。参考：[AF SRN 回调字段](https://support.appsflyer.com/hc/en-us/articles/360000726098-Conversion-data-payloads-and-scenarios)、[SRN 延迟深链说明](https://support.appsflyer.com/hc/en-us/articles/360001546905-Self-reporting-networks-SRNs)。

## 找谁、要什么、在哪里找

| 对接方 | 需要提供给研发的内容 | 在哪里配置或查询 | 用途与限制 |
| --- | --- | --- | --- |
| 产品／投放负责人 | 核对文首渠道、目标动作、`DL-OL/DL-GC/DL-GS` 语法与每个投放的 Path／奖品，提供真实活动 ID、名称及脱敏 GCD 样本用于审计；无需逐 campaign ID 登记为运行时白名单。 | 渠道广告管理后台的 Campaign / Ad set / Ad；与 AppsFlyer 安装归因明细比对。 | 一个 campaign 只投一个目标；无真实回调不得宣称 SRN 映射验收通过。 |
| AppsFlyer 后台管理员 | AF 应用 ID／平台、正确应用的 Partner Integration 是否启用、渠道授权／Link ID（如需）、测试设备登记、SDK Integration Tests 中首次安装结果、可共享的脱敏 GCD 样本（`af_status`、`is_first_launch`、`media_source`、campaign 层级及 `af_dp` 是否存在）。 | **Collaborate → Partner Integrations → Marketplace / Active integrations**；测试设备和 SDK Integration Tests 入口以当前账户为准。 | 禁止提供 Dev Key、后台登录密码、签名私钥或设备个人标识原值；只共享必要的测试截图与脱敏结果。参考：[Partner Integrations](https://support.appsflyer.com/hc/en-us/articles/9960361873809-Partner-Integrations)、[渠道集成与 SRN 测试](https://support.appsflyer.com/hc/en-us/articles/4410395957521-Set-up-an-integrated-partner)。 |
| 渠道投放／运营 | 实际广告平台、App Campaign 类型、已上线／小预算测试的 campaign / ad set / ad ID 与名称、投放的 Android/iOS 应用、具体落地方式及测试入口、首次安装时间与 AF 对应安装截图。 | 各广告平台的广告系列与广告组报表；AF 对应媒体来源的安装报表。 | SRN 不等同于普通 OneLink 点击；各渠道需单独完成集成并以实际广告安装回传验收。 |
| QA / 签名负责人 | 测试手机与系统版本、安装来源（侧载/Play/App Store）、应用包名／Bundle ID、**最终安装包**证书指纹（Android）、设备测试登记确认、点击→安装→首次启动的时间线、复测隔离设备与日志。 | AF Test devices / Integration Tests、Android `apksigner verify --print-certs`、Play Console **App signing key**／Xcode 签名能力。 | 不能把旧包、覆盖安装、GM 模拟或单纯唤起当成真实 GCD 回调通过。 |

### 按平台索取

- **Google Ads（`googleadwords_int`）**：向 Google Ads **管理员**索取关联该 Android/iOS App 的 **Link ID**（Google Ads → Tools and settings → Data manager → Third-party app analytics → Create link ID → 选择 AppsFlyer）、广告账号／campaign ID 和名称、广告组 ID／名称、广告类型及 App Campaign 类别；AF 管理员在 Google Ads Partner Integration 启用并填写同一 Link ID，并核对 **Deferred deep linking with Google Ads** 开关是否为目标测试配置启用。GCD 可能包含 `campaign`、`campaign_id`、`af_adset`，但 `af_ad` 常为空；`af_dp` 仅适用于特定 Feed 场景，不承诺每次返回。不要要求渠道提供通用“带所有 deep_link_sub 参数的 Google SRN 归因链接”。[Google Ads 对接步骤](https://support.appsflyer.com/hc/en-us/articles/115002504686-Google-Ads-AdWords-Integration-setup-for-advertisers)、[GCD 字段表](https://support.appsflyer.com/hc/en-us/articles/360000726098-Conversion-data-payloads-and-scenarios)。
- **Meta Ads（`Facebook Ads`）**：向 Meta 账户管理员索取 **Facebook App ID**（Meta App Dashboard 对应 App 顶部）、可用时的 **install referrer decryption key**（由管理员直接填 AF，不传给研发）、对应 Android/iOS App、Campaign/Ad set/Ad 各层名称和 ID，以及广告目标、投放地区、是否设置广告层 **Deferred Deep Link URL**；AF 管理员核对 Meta 对接及适用的数据共享／AMM 条件。官方提醒 App Install/App Engagement 等广告类型与其它目标的安装归因前提不同，不能把 *Sales/Traffic* 的普通直链当作同一 DDL 测试。GCD 通常有 `campaign`/`campaign_id`、`adset`/`adset_id`、`adgroup`/`adgroup_id`，不能假定收到 `deep_link_value`。[Meta 对接与广告类型](https://support.appsflyer.com/hc/en-us/articles/207033826-Meta-Ads-integration-setup)、[GCD 字段表](https://support.appsflyer.com/hc/en-us/articles/360000726098-Conversion-data-payloads-and-scenarios)。
- **TikTok for Business（`tiktokglobal_int`）／Snapchat（`snapchat_int`）**：如计划投放，再向各自渠道和 AF 管理员索取已启用的对应 Partner Integration、广告账号、campaign/ad group/ad ID 与名称、测试广告、实际 GCD 样本；若宣称 `af_dp` 或 TikTok `deep_link_value` 可用，必须提供该广告类型的实际回调证据。当前并未为它们配置目标映射。[TikTok 对接](https://support.appsflyer.com/hc/en-us/articles/6722785184913-TikTok-for-Business-Advanced-SRN-integration-setup)、[SRN DDL 字段说明](https://support.appsflyer.com/hc/en-us/articles/360001546905-Self-reporting-networks-SRNs)。
- **Apple Ads（iOS，`Apple Search Ads`）**：若投放，找 Apple Ads 管理员要广告账号、**campaign ID / adgroup ID** 和真实样本；AF 文档指出该渠道某些名称字段在 GCD 中已废弃，不把 `campaign` 名称当作必有字段。[GCD 字段表](https://support.appsflyer.com/hc/en-us/articles/360000726098-Conversion-data-payloads-and-scenarios)、[Apple Ads 对接](https://support.appsflyer.com/hc/en-us/articles/213747963-Apple-Ads-integration-setup)。

## 一次真实 SRN 验收所需交付

1. 产品确认文首平台、动作、语法及广告 URI 的受控规则；研发仅在首次非自然安装且目标通过校验时转换。campaign ID 用于审计而非逐活动运行时白名单；缺字段、错目标、重复／晚到回调及已有协议优先须回归。未经真实渠道与目标系统的商店安装验收，不得宣称 SRN 投放或奖励已通过。
2. AF 管理员启用目标渠道集成并登记隔离测试设备；渠道用小预算、实际运行的广告让未安装的设备点击，安装并首次启动；不能用测试 OneLink 假扮一次 SRN 安装。AF 后台核对媒体来源与活动 ID，再对照脱敏 GCD 日志及最终页面。参考：[AF SRN 测试步骤](https://support.appsflyer.com/hc/en-us/articles/4410395957521-Set-up-an-integrated-partner)。
3. Play 及 App Store 版本、平台签名、用户隐私许可与商店状态各自单独验证；本地侧载模拟器的已安装链接点击不能替代这些生产门禁。

## 当前唤起测试需 AF 提供的链接（不是 SRN 归因测试）

- 请 AF/OneLink 管理员确认模板域名 **`brainpunk.onelink.me/IjED`**、Android App Links 对应**本次最终 APK 的签名证书 SHA-256**、iOS 对应应用和 Universal Links 的 AASA；生成一条 **`https://brainpunk.onelink.me/IjED/<短码>`**，告知其 `open_level`、`sub1`、`sub2` 预期值。若创建归因测试链接，`pid` 是必填的媒体来源参数，可用未被 SRN 保留的自有来源（例如 `owned_sms_test`）；**本轮唤起测试不要求提供 campaign `c`**，不能把“无 campaign”误解为要删除 `pid`。[AF 链接参数结构](https://support.appsflyer.com/hc/en-us/articles/207447163-About-link-structure-and-parameters)。
- 如只验 Android 已安装唤起，可用当前已登记短链 `https://brainpunk.onelink.me/IjED/gacjwdmm`；仅检查新 APK 最终合并 Manifest、实际签名、assetlinks、系统 verified 与**从消息中点击**后进入游戏。AF 模板尚未确认前不能代替后台配置结论。
- 已安装唤起可以不提供 campaign；**GCD 的首次非自然安装条件不能因此实测通过**。不建议用带真实广告参数的示例测试 URL 随意制造假归因；若后续要验 GCD，请 AF 人员提供独立、可复现的测试点击／安装方案。

**2026-09-23 当前阻塞**：Debug APK `Tricky_Box_DEBUG_1.16.0_26092378.apk` 实签 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`，AF 当前在线 `assetlinks.json` 同一包名只列 `78:52:AC:7F:F3:8D:D3:73:45:35:E2:C5:D9:18:59:F7:2B:69:85:70:C7:5B:D6:51:3C:E3:DF:06:8F:57:6B:57`。请 AF OneLink 模板管理员确认是否能**同时**保留已在使用的 78 指纹并添加本次 C3 测试证书，保存后检查线上 JSON 的包名/指纹；不要无确认地替换现有生产证书。新模拟器从 Messages 点击现有短链转到 Chrome，待 AF 更新后须在该新 APK 上重做外部点击。

**同日复测更新（覆盖上段历史阻塞）**：AF 在线文件后来同时列出 78 和 C3 两枚证书，新包在 Android 15 QA 模拟器上的 OneLink 域名已 `verified`；现有短链冷启动、用户提供的长链热唤起和 UDL 跳入 Level 36 均实测成功。该长链的 `c=Deeplink` 就是 campaign，`is_retargeting=true` 还使本次同设备重装 GCD 样本标记为 `re-attribution`，不能拿它证明无 campaign 的首次非重定向投放；仅可确认 UDL 先返回时 GCD 不重复导航。真正测 5 秒 GCD 后备仍需受控的 UDL 无目标、GCD 有目标的归因条件；未做 Play 正式商店安装。参见同目录 `OneLink功能与测试记录.md` 最新章节。

**正式投放／商店安装若仍无法复现时的 AF/渠道交付**：本地已有无 `c` 且带合法 `pid`、`open_level/default/level_022` 的 OneLink 测试链接，无须为此次侧载 GCD-only 补验强制另建链接。如上线或 Play 正式安装场景无法用该链接复现，再请 AF 提供单平台归因测试链接或等效受控方案、可用商店安装轨道、测试设备登记及脱敏归因证据；不得把仅带 campaign 的 SRN 广告信息当作本项目现行 `open_level` 路由。2026-09-23 在**同一已测试设备**等候 17 分钟后重装曾得到 UDL NOT_FOUND **但 GCD Organic 且无目标**，只证明该旧条件未成功；2026-09-24 改用从未安装的独立模拟器、先点击再等待超过 17 分钟才首次侧载，GCD 备用链已成功，详见下文。

**2026-09-24 更正上述“需要单独链接”的前提**：用户提供的同模板新长链去掉 `c` 和再营销参数后，首次从未安装过游戏的隔离模拟器点击并侧载，确实返回 `Non-organic/is_first_launch=true`、完整的 `open_level/default/level_022` GCD 参数，`campaign` 在回调中是字面字符串 `"None"`；UDL 同次先返回有效参数并进入 Level 36。因此**不必为了取得 GCD 数据另建单平台链接**，原链可以继续用于受控安装测试。但这不是 GCD-only 后备跳转的通过证据；只有在原链确实无法复现 `UDL 无有效目标 + GCD 有目标` 时，才请 AF 负责人给另一条单平台归因链接或可复现测试方案。Play 内测/正式安装仍需与模拟器侧载分开验收。详见 `OneLink功能与测试记录.md` 最新章节。

**2026-09-24 GCD-only 实际补验（取代上段“尚未兜底通过”状态）**：第三台从未安装目标包的 Android 15 隔离模拟器，外部点击同一条无 `c` 长链后保持未安装超过 17 分钟，首次侧载启动，实际观察到 UDL `NOT_FOUND`、GCD `Non-organic/is_first_launch=true` 且包含 `open_level/default/level_022`、游戏 `gcd_queued` 并最终显示 Level 36。无需另找 GCD-only 链接即可在**该侧载测试条件下**触发已有备用进关；前述“必须提供新链接”不适用于本次测试。Play 正式安装、真正 SRN campaign 映射、iOS 以及 UDL 未回调时的 5 秒等待上限仍需单独验收，不能由本次侧载结果推导通过。

**2026-09-24 原始有 c 链接复核**：另用全新 Android 15 隔离模拟器，先点击原始 `c=Deeplink` 且 `is_retargeting=true` 的 OneLink，等待超过 21 分钟才首次侧载同一测试 APK。首次打开时 Google Play Services Cronet 模块主动结束进程、未收到归因结果；重启后在未再次点击或重装的条件下，UDL `NOT_FOUND`、GCD **`Non-organic/is_first_launch=true/campaign=Deeplink`** 且含相同 `open_level/default/level_022`，游戏由 `gcd_queued` 进入实际 Level 36。因此有 c 链接在此隔离条件下也可以 GCD 备用进关；旧同设备二次重装 Organic 的精确成因需要 AF 侧归因证据，不能简单归因于 `c`。本轮仍不是正式商店／SRN／iOS 或候选先到等满 5 秒的验收，详见 OneLink 模块测试记录。
