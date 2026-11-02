using System;
using AppsFlyerSDK;
using Guru.SDK.Framework.Utils.Network;
using Newtonsoft.Json.Linq;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>GM 与编辑器测试入口；复用正式协议、路由和当前玩家存档，仅进关跳过永久历史，其它门禁保留；访问由现有 GM 隐藏入口与密码控制。</summary>
    public sealed partial class OneLinkManager
    {
        #region GM 与编辑器测试

        /// <summary>GM 只读显示当前会话收到的真实系统 URL；不以测试输入或 SDK 解析地址替代，空值表示尚未收到。</summary>
        public string SystemUrl => _systemUrl ?? string.Empty;

        /// <summary>
        /// GM 主动将保存的系统 URL 交给 SDK；不模拟 ERROR、不重启 SDK、不改写系统 URL。
        /// 每次受理是独立的手动请求，真实 ERROR 仍走正式单次恢复；成功参数保留首页及奖励防重，进关绕过且不写永久历史。
        /// </summary>
        /// <param name="message">本次是否发起的说明；发起不代表 SDK 已解析成功或已经进关。</param>
        public bool RequestSystemUrl(out string message)
        {
            if (!Application.isPlaying)
            {
                message = "仅支持运行态。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(_systemUrl))
            {
                message = "当前 _systemUrl 为空，尚未收到系统链接。";
                return false;
            }
            if (!_initialized || !_appsFlyerBound || AppsFlyer.instance == null)
            {
                message = "AppsFlyer 尚未就绪。";
                return false;
            }
            if (_playingGameType.HasValue)
            {
                message = "请先返回首页，再主动请求当前 URL。";
                return false;
            }
            if (_urlResolution != null || _errorRetry?.Scheduled == true || _pending != null)
            {
                message = "已有链接正在处理，请等待本次结果。";
                return false;
            }
            if (!NetworkUtils.IsNetworkConnected())
            {
                message = "网络尚未连接，本次未发起请求。";
                return false;
            }
#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
            message = "SDK 主动请求仅支持 Android/iOS 真机调试包。";
            return false;
#else
            if (!IsRoutingUrl(_systemUrl) || !Uri.TryCreate(_systemUrl.Trim(), UriKind.Absolute, out Uri uri))
            {
                message = "当前系统 URL 不符合 OneLink 协议。";
                return false;
            }

            CancelGcdCandidate();
            ResetActivationState();
            _gmSystemUrlActivation = true;
            _activation++;
            _explicitLinkReceived = true;
            _externalUrlSeen = true;
            _pendingUrl = null;
            _sdkUrlBeforeSystem = null;
            _waitReason = null;
            message = "已向 SDK 请求当前 URL；结果请查看 OneLink 日志，不代表跳转成功。";
            string failure = null;
            bool started = StartUrlResolution(uri, (queued, result) => { if (!queued) failure = result; },
                automatic: true, manualSystemUrl: true);
            if (!started) message = failure ?? "请求未发起，请查看 OneLink 日志。";
            return started;
#endif
        }

        /// <summary>
        /// 在编辑器 Play Mode 或通过密码打开的 GM 中模拟一次新的 OneLink 回调，复用正式参数解析、首页门禁、解锁和进关流程。
        /// 不请求 AppsFlyer 网络，不跳过启动弹窗；每次调用均视为新点击，使用当前设备的运行存档。
        /// GM 进关不受每玩法一次历史限制，也不写该历史；仍执行现有解锁/进度流程，不是独立测试存档。
        /// </summary>
        /// <param name="sub1">目标玩法：main、draw、iwanner 或 dig。</param>
        /// <param name="sub2">资源 Path，如 DLevel_002；省略或不存在时回退该玩法最小未通关关卡。</param>
        /// <returns>参数有效且已排队时为 true；不代表已进入关卡。</returns>
        public static bool TestDeepLink(string sub1, string sub2 = null)
        {
            if (!Application.isPlaying)
            {
                UnityEngine.Debug.LogWarning("[OneLink] 测试仅支持运行态，请先正常启动游戏。");
                return false;
            }

            if (_instance._playingGameType.HasValue)
                return RejectUrlTest("gameplay_active", "当前正在关卡中，本次跳转已放弃；请返回首页后重新测试。", null);

            if (_instance._urlResolution != null)
                return RejectUrlTest("busy", "已有短链接正在解析。", null);

            return SubmitTestPayload(new JObject
            {
                ["deep_link_value"] = "open_level",
                ["deep_link_sub1"] = sub1,
                ["deep_link_sub2"] = sub2
            });
        }

        /// <summary>
        /// 在运行态 GM 中测试 getCoin/getSkin 参数，复用正式账号校验、首页发奖与存档防重。
        /// 不请求 SDK；仅排队，关闭弹窗后才执行。会真实修改当前存档，不自动生成或清除领取标识。
        /// </summary>
        /// <param name="action">仅支持 getCoin 或 getSkin，不区分大小写。</param>
        /// <param name="sub1">奖励唯一标识；两种奖励共用同一防重记录。</param>
        /// <param name="sub2">金币正整数数量或皮肤配置的精确 Id。</param>
        /// <param name="sub3">可选的目标 Guru Uid；留空不限制用户，填写后由正式流程核对。</param>
        /// <param name="message">参数拒绝或排队说明；排队不代表奖励已经到账。</param>
        /// <returns>参数有效且已排队返回 true，实际发奖结果查看 reward_completed 日志。</returns>
        public static bool TestDeepLinkReward(string action, string sub1, string sub2, string sub3, out string message)
        {
            if (!Application.isPlaying)
            {
                message = "仅支持运行态，请先正常启动游戏。";
                return false;
            }
            if (_instance._playingGameType.HasValue)
            {
                message = "请先返回首页，再测试领奖。";
                return false;
            }
            if (_instance._urlResolution != null)
            {
                message = "已有短链接正在解析，请等待或关闭 URL 测试。";
                return false;
            }
            if (!string.Equals(action, "getCoin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(action, "getSkin", StringComparison.OrdinalIgnoreCase))
            {
                message = "领奖测试仅支持 getCoin/getSkin。";
                return false;
            }

            var payload = new JObject
            {
                ["deep_link_value"] = action,
                ["deep_link_sub1"] = sub1,
                ["deep_link_sub2"] = sub2,
                ["deep_link_sub3"] = sub3
            };
            if (!OneLinkRequest.TryParse(payload, out _, out string error))
            {
                message = $"未提交：{error}。sub1 填奖励唯一标识，sub2 填正整数金币数量或皮肤 ID。";
                return false;
            }

            bool queued = SubmitTestPayload(payload);
            message = queued ? "领奖参数已排队，等待首页执行；实际结果查看 reward_completed 日志。" : "未提交，请查看 [OneLink] 日志。";
            return queued;
        }

        /// <summary>
        /// 在运行态测试 URL：普通长链接本地解析 query；Android 真机中的 OneLink 短链接交给 SDK 解析。
        /// 短链最多等待 10 秒，成功后复用正式首页门禁；这不验证系统唤起、签名或安装归因。
        /// 进关指令绕过且不写永久进入历史；奖励指令仍按真实账号和领取记录校验。
        /// </summary>
        /// <param name="url">带深链 query 的 HTTP(S) URL，或 brainpunk.onelink.me 下的模板／短码 URL。</param>
        /// <param name="onCompleted">主线程结果回调；true 表示已排队，false 的说明包含拒绝、取消或超时原因。</param>
        /// <returns>true 表示已排队或已开始异步解析；短链的最终结果须以回调为准，不代表已进关或已领取奖励。</returns>
        public static bool TestDeepLinkUrl(string url, Action<bool, string> onCompleted = null)
        {
            if (!Application.isPlaying)
                return RejectUrlTest("not_playing", "请先正常启动游戏。", onCompleted);
            if (_instance._playingGameType.HasValue)
                return RejectUrlTest("gameplay_active", "当前正在关卡中，本次跳转已放弃；请返回首页后重新测试。", onCompleted);

            LogUrl(url, "gm_test");
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                return RejectUrlTest("invalid_http_url", "请输入完整的 HTTP(S) URL。", onCompleted);

            if (_instance._urlResolution != null)
                return RejectUrlTest("busy", "已有短链接正在解析，请等待或关闭测试弹窗。", onCompleted);

            // OneLink 短码的后台参数优先于追加的 query，始终交给 SDK 合并解析。
            if (IsOneLinkShortUrl(uri))
                return _instance.StartUrlResolution(uri, onCompleted);

            bool parsed = TryParseUrlQuery(uri, out JObject payload, out string error);
            LogPayload(payload, "gm_url_query");
            if (!parsed)
                return RejectUrlTest(error, "长链接需提供不重复的 deep_link_value、deep_link_sub1；奖励需 sub2，可选 sub3 限定用户。", onCompleted);

            bool queued = SubmitTestPayload(payload);
            onCompleted?.Invoke(queued, queued ? "参数已排队，等待首页执行指令。" : "参数无效，请查看 [OneLink] 日志。");
            return queued;
        }

        /// <summary>仅 GM 测试弹窗关闭时调用；不取消自动解析，也不宣称中止 SDK 原生网络请求。</summary>
        internal void CancelUrlTest()
        {
            if (_urlResolution?.IsAutomatic == false)
                CancelUrlResolution();
        }

        private static bool SubmitTestPayload(JObject payload)
        {
            _instance.Initialize();
            _instance.CancelGcdCandidate();
            _instance._externalUrlSeen = true;
            _instance._pendingUrl = null;
            _instance._sdkUrlBeforeSystem = null;
            _instance._activation++;
            _instance.ResetActivationState(testActivation: true);
            GuruLog.I("[OneLink] stage=debug_test.");
            LogPayload(payload, "gm_parameters");
            return _instance.AcceptPayloadCore(payload, false, resolvedLinkSource: null, bypassEntryHistory: true);
        }

        #endregion
    }
}
