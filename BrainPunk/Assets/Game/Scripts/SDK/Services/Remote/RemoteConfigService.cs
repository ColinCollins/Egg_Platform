using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Firebase.RemoteConfig;
using Newtonsoft.Json.Linq;
using R3;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;
using Game.ConfigModule;
using Guru.SDK.Framework.Utils.Firebase.RemoteConfig;

namespace SDK.Remote
{
    /// <summary>
    /// Firebase Remote Config 相关的工具封装。
    /// 后续如有更多 remote key，可在此集中管理。
    /// </summary>
    public static class RemoteConfigService
    {
        private const string LevelConfigKey = "level_config";
        public const string DrawGameConfigKey = "drawgame_config";

        /// <summary>IWannerGame 启动阶段需要拉取的独立 RemoteConfig key。</summary>
        public const string IWannerGameConfigKey = "iwannergame_config";

        /// <summary>Dig 独立远端 key，沿用现有启动拉取与缓存时序。</summary>
        public const string DigGameConfigKey = "diggame_config";

        private const string CurrentRemoteConfigVersionFieldKey = "currentRemoteConfigVersion";
        private const int ForceFetchMaxAttempts = 1;
        // 本轮拉取与回调等待共用预算；到期只结束业务等待，不取消 SDK 原生请求。
        private const float RemoteConfigMaxWaitSeconds = 5f;
        // 后台强制阶段独立限时，不占用 Loading 的自动拉取等待预算。
        private const float StartupForceFetchMaxWaitSeconds = 10f;

        /// <summary>
        /// 需要在启动阶段拉取的 Remote Config key 列表。
        /// 可在其他地方动态增减。
        /// </summary>
        public static readonly List<string> RemoteConfigKeys = new List<string>
        {
            LevelConfigKey,
            DrawGameConfigKey,
            IWannerGameConfigKey,
            DigGameConfigKey
        };

        #region 应用生命周期拉取回调

        // 启动清缓存前先保留各 key 的最新有效结果，避免默认值覆盖刚拉到的配置。
        private static readonly Dictionary<string, string> latestValidConfigs = new Dictionary<string, string>();
        private static readonly SemaphoreSlim updateRemoteConfigGate = new SemaphoreSlim(1, 1);
        private static IDisposable remoteConfigSubscription;
        private static IDisposable preparedGetterSubscription;
        private static bool isCacheReady;
        private static int fetchOperationSequence;
        private static TaskCompletionSource<IRemoteConfigGetter> sdkAutomaticFetchCompletionSource;
        // 每轮 SDK 初始化只登记一次，Loading 超时不取消自动结果之后的后台强制拉取。
        private static Task<bool> startupForceFetchTask;
        private static IRemoteConfigGetter latestSdkInitializationGetter;
        private static IRemoteConfigGetter sdkPreparedGetter;
        private static bool sdkPreparedGetterCaptured;
        private static DateTime sdkAutomaticFetchStartedAtUtc;
        private static int sdkInitializationCallbackCount;
        private static bool sdkInitializationDispatchReturned;
        // 广告或转场期间只保留最新 Draw 候选，安全后连同派生排序一起保存和提交。
        private static string pendingDrawConfig;
        private static string pendingDigConfig;
        private static int pendingDigCallbackSequence, latestDigCallbackSequence;
        // 仅关联诊断日志，不参与请求成功判定或配置选择。
        private static int configCallbackSequence;
        private static int latestDrawCallbackSequence;
        private static int pendingDrawCallbackSequence;

        /// <summary>
        /// SDK 初始化普通拉取前注册一次已有配置通知，同时接收 ForceFetch 和网络恢复结果。
        /// 仅在运行态主线程调用；订阅持续到应用退出，不随 Loading 超时或销毁解除。
        /// </summary>
        public static void RegisterRemoteConfigCallback()
        {
            if (!Application.isPlaying || remoteConfigSubscription != null) return;

            remoteConfigSubscription = RemoteConfigManager.Instance.ObserveConfigGetter()
                .Skip(1)
                .Subscribe(OnRemoteConfigChanged);
            Application.quitting += ResetRemoteConfigCallback;
            GuruLog.I($"[RemoteConfigService] diagnostics=draw-refresh-v1, appVersion={Application.version}, platform={Application.platform}. RemoteConfig callback registered.");
        }

        // 兼容编辑器关闭 Domain Reload 的再次运行；清理时不创建 SDK 或存档实例。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRemoteConfigCallback()
        {
            remoteConfigSubscription?.Dispose();
            preparedGetterSubscription?.Dispose();
            Application.quitting -= ResetRemoteConfigCallback;
            remoteConfigSubscription = null;
            preparedGetterSubscription = null;
            isCacheReady = false;
            sdkAutomaticFetchCompletionSource?.TrySetCanceled();
            sdkAutomaticFetchCompletionSource = null;
            startupForceFetchTask = null;
            latestSdkInitializationGetter = null;
            sdkPreparedGetter = null;
            sdkPreparedGetterCaptured = false;
            sdkInitializationCallbackCount = 0;
            sdkInitializationDispatchReturned = false;
            latestValidConfigs.Clear();
            pendingDrawConfig = null;
            pendingDigConfig = null;
            pendingDigCallbackSequence = latestDigCallbackSequence = 0;
            configCallbackSequence = 0;
            latestDrawCallbackSequence = 0;
            pendingDrawCallbackSequence = 0;
        }

        /// <summary>
        /// 在 GuruSdk.Initialize 前开始跟踪。Prepare 会先发布缓存激活回调，自动拉取结束后再发布一次回调。
        /// </summary>
        public static void BeginSdkInitializationTracking()
        {
            RegisterRemoteConfigCallback();
            sdkAutomaticFetchCompletionSource?.TrySetCanceled();
            sdkAutomaticFetchCompletionSource = new TaskCompletionSource<IRemoteConfigGetter>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            latestSdkInitializationGetter = null;
            preparedGetterSubscription?.Dispose();
            preparedGetterSubscription = null;
            sdkPreparedGetter = null;
            sdkPreparedGetterCaptured = false;
            sdkInitializationCallbackCount = 0;
            sdkInitializationDispatchReturned = false;
            startupForceFetchTask = ForceFetchAfterSdkAutomaticFetchAsync(sdkAutomaticFetchCompletionSource);
            GuruLog.I("[RemoteConfigService] SDK Remote Config initialization tracking started.");
        }

        /// <summary>
        /// 仅由 GameRoot 的同步初始化调用：此时 SDK Prepare 已结束，自动请求尚未派发。
        /// 订阅首个重放 getter 保存缓存快照身份，避免下一帧的缓存通知被误认成自动结果。
        /// </summary>
        public static void CaptureSdkPreparedGetter()
        {
            if (!Application.isPlaying || sdkAutomaticFetchCompletionSource == null) return;

            var initialization = sdkAutomaticFetchCompletionSource;
            sdkAutomaticFetchStartedAtUtc = DateTime.UtcNow;
            preparedGetterSubscription = RemoteConfigManager.Instance.ObserveConfigGetter()
                .Take(1)
                .Subscribe(getter =>
                {
                    if (!ReferenceEquals(initialization, sdkAutomaticFetchCompletionSource)) return;
                    sdkPreparedGetter = getter;
                    sdkPreparedGetterCaptured = true;
                    TryCompleteSdkAutomaticFetchTracking();
                });
        }

        /// <summary>
        /// GuruSdk.Initialize 返回只代表自动拉取已派发，不代表拉取已经完成。
        /// </summary>
        public static void NotifySdkInitializationDispatched()
        {
            sdkInitializationDispatchReturned = true;
            TryCompleteSdkAutomaticFetchTracking();
            GuruLog.I(
                $"[RemoteConfigService] SDK initialization dispatch returned. " +
                $"observedCallbacks={sdkInitializationCallbackCount}, " +
                $"automaticFetchCompleted={sdkAutomaticFetchCompletionSource?.Task.Status == TaskStatus.RanToCompletion}.");
        }

        private static void OnRemoteConfigChanged(IRemoteConfigGetter getter)
        {
            int callbackId = ++configCallbackSequence;
            try
            {
                ProcessRemoteConfigChanged(getter, callbackId);
            }
            catch (Exception exception)
            {
                // 诊断异常进入同一份导出日志，随后原样抛出，不改变原回调的失败处理。
                GuruLog.E($"[RemoteConfigService] callback={callbackId}, stage=callback_exception.", exception: exception);
                throw;
            }
        }

        private static void ProcessRemoteConfigChanged(IRemoteConfigGetter getter, int callbackId)
        {
            if (getter == null)
            {
                GuruLog.W($"[RemoteConfigService] callback={callbackId}, stage=received, getter=null.");
                return;
            }

            GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=received, cacheReady={isCacheReady}, network={Application.internetReachability}, {FormatFetchState(getter)}");
            TrackSdkInitializationCallback(getter);

            if (!getter.IsActive || getter.LastFetchStatus != RemoteConfigLastFetchStatus.Success)
            {
                GuruLog.W(
                    $"[RemoteConfigService] callback={callbackId}, stage=ignored, reason=inactive_or_unsuccessful. {FormatFetchState(getter)}");
                return;
            }

            var acceptedCount = 0;
            foreach (var key in RemoteConfigKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                var value = getter.GetString(key);
                if (key == DrawGameConfigKey)
                {
                    // 保留实际下发内容，转义换行以便按回调编号检索；不输出账号或设备凭据。
                    GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=draw_payload, chars={value?.Length ?? 0}, json={value?.Replace("\r", "\\r").Replace("\n", "\\n")}");
                }
                if (!TryValidateRemoteConfigJson(key, value) ||
                    !TryValidateRemoteConfigValueVersion(key, value))
                {
                    GuruLog.W($"[RemoteConfigService] callback={callbackId}, stage=key_rejected, key={key}, reason=invalid_json_or_version.");
                    continue;
                }

                latestValidConfigs[key] = value;
                if (key == DrawGameConfigKey) latestDrawCallbackSequence = callbackId;
                if (key == DigGameConfigKey) latestDigCallbackSequence = callbackId;
                acceptedCount++;
            }

            if (acceptedCount == 0)
            {
                GuruLog.W($"[RemoteConfigService] callback={callbackId}, stage=ignored, reason=no_valid_business_config; previous cache retained.");
                return;
            }

            if (!isCacheReady)
            {
                GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=retained_before_startup, validKeys={acceptedCount}.");
                return;
            }

            var saved = PersistResolvedConfigs(latestValidConfigs, isCallbackUpdate: true, source: "callback", callbackId: callbackId);
            GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=callback_complete, validKeys={acceptedCount}, allConfigsCommitted={saved}.");
        }

        private static void TrackSdkInitializationCallback(IRemoteConfigGetter getter)
        {
            if (sdkAutomaticFetchCompletionSource == null || !getter.IsActive)
            {
                return;
            }

            latestSdkInitializationGetter = getter;
            sdkInitializationCallbackCount++;
            GuruLog.I(
                $"[RemoteConfigService] SDK initialization callback observed. " +
                $"sequence={sdkInitializationCallbackCount}, dispatchReturned={sdkInitializationDispatchReturned}, " +
                $"{FormatFetchState(getter)}");
            TryCompleteSdkAutomaticFetchTracking();
        }

        private static void TryCompleteSdkAutomaticFetchTracking()
        {
            if (sdkAutomaticFetchCompletionSource == null ||
                sdkAutomaticFetchCompletionSource.Task.IsCompleted ||
                latestSdkInitializationGetter == null ||
                !sdkPreparedGetterCaptured)
            {
                return;
            }

            // SDK 每次发布都创建新 getter；重放的 Prepare getter 不代表自动请求结束。
            if (ReferenceEquals(latestSdkInitializationGetter, sdkPreparedGetter))
            {
                return;
            }

            sdkAutomaticFetchCompletionSource.TrySetResult(latestSdkInitializationGetter);
        }

        #region 启动期顺序强制拉取

        /// <summary>
        /// 自动拉取失败或沿用旧缓存时强制一次，最多等待十秒；结果由长期回调保存。
        /// 让出一帧退出 SDK 回调栈，旧初始化或退出后的任务不得再派发请求。
        /// </summary>
        private static async Task<bool> ForceFetchAfterSdkAutomaticFetchAsync(
            TaskCompletionSource<IRemoteConfigGetter> automaticFetchCompletion)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            try
            {
                var getter = await automaticFetchCompletion.Task;
                await UniTask.SwitchToMainThread();
                await UniTask.Yield();
                if (!Application.isPlaying ||
                    !ReferenceEquals(automaticFetchCompletion, sdkAutomaticFetchCompletionSource))
                {
                    return false;
                }

                if (!ShouldForceFetchAfterAutomaticResult(getter, sdkAutomaticFetchStartedAtUtc))
                {
                    GuruLog.I($"[RemoteConfigService] SDK automatic result does not require startup ForceFetch. {FormatFetchState(getter)}");
                    return getter.LastFetchStatus == RemoteConfigLastFetchStatus.Success;
                }

                var operationId = System.Threading.Interlocked.Increment(ref fetchOperationSequence);
                var remote = RemoteConfigManager.Instance;
                var waitBudget = Task.Delay(TimeSpan.FromSeconds(StartupForceFetchMaxWaitSeconds));
                GuruLog.I(
                    $"[RemoteConfigService] Startup ForceFetch operation {operationId}: automatic fetch failed or used cached data; " +
                    $"forcing once in the background with a {StartupForceFetchMaxWaitSeconds:F0}s budget. {FormatFetchState(remote)}");
                var succeeded = await TryForceFetchOnceAsync(remote, operationId, 1, waitBudget);
                await UniTask.SwitchToMainThread();
                if (!Application.isPlaying ||
                    !ReferenceEquals(automaticFetchCompletion, sdkAutomaticFetchCompletionSource))
                {
                    return false;
                }

                GuruLog.I($"[RemoteConfigService] Startup ForceFetch operation {operationId} business wait ended. freshResult={succeeded}; native requests and the persistent callback are not canceled.");
                return succeeded;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception exception)
            {
                await UniTask.SwitchToMainThread();
                if (Application.isPlaying &&
                    ReferenceEquals(automaticFetchCompletion, sdkAutomaticFetchCompletionSource))
                {
                    GuruLog.E(
                        $"[RemoteConfigService] Startup ForceFetch failed: {exception.GetType().FullName}: " +
                        $"{exception.Message}\n{exception.StackTrace}");
                }

                return false;
            }
        }

        // SDK 没有公开客户端 CD 标识；Success 的历史时间表示本轮没有取得新鲜结果。
        private static bool ShouldForceFetchAfterAutomaticResult(IRemoteConfigGetter getter, DateTime automaticFetchStartedAtUtc)
        {
            if (getter.LastFetchStatus == RemoteConfigLastFetchStatus.Failure) return true;
            if (getter.LastFetchStatus != RemoteConfigLastFetchStatus.Success) return false;
            return !getter.LastFetchTime.HasValue || getter.LastFetchTime.Value == DateTime.MinValue ||
                   getter.LastFetchTime.Value.ToUniversalTime() < automaticFetchStartedAtUtc;
        }

        #endregion

        private static bool TryValidateRemoteConfigJson(string key, string value)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(value) && JToken.Parse(value) is JObject)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // 非法配置按 key 拒绝，保留上一份缓存，不把解析异常带到 SDK 请求队列。
            }

            GuruLog.W($"[RemoteConfigService] RemoteConfig '{key}' is empty or is not a valid JSON object; previous cache or defaults will be used.");
            return false;
        }

        #endregion

        /// <summary>
        /// 适配 LoadManager 的统一入口：
        /// 使用当前的 RemoteConfigKeys 列表进行拉取和日志输出。
        /// </summary>
        public static UniTask UpdateRemoteConfigsAsync()
        {
            return UpdateRemoteConfigsAsync(RemoteConfigKeys);
        }

        /// <summary>
        /// GM 等手动入口使用：等待正在进行的 SDK 拉取结束后，仍强制请求一次最新配置。
        /// </summary>
        public static UniTask ForceUpdateRemoteConfigsAsync()
        {
            return UpdateRemoteConfigsAsync(RemoteConfigKeys, acceptFreshSdkFetch: false);
        }

        /// <summary>
        /// 按传入的 key 列表拉取 Remote Config。
        /// </summary>
        public static async UniTask UpdateRemoteConfigsAsync(IReadOnlyList<string> keys)
        {
            await UpdateRemoteConfigsAsync(keys, acceptFreshSdkFetch: true);
        }

        private static async UniTask UpdateRemoteConfigsAsync(
            IReadOnlyList<string> keys,
            bool acceptFreshSdkFetch)
        {
            if (!HasRemoteConfigKeys(keys))
            {
                return;
            }

            // 从业务入口开始计时，排队与后续请求共享预算；不依赖 Unity 帧或时间缩放计时。
            var waitBudget = Task.Delay(TimeSpan.FromSeconds(RemoteConfigMaxWaitSeconds));
            using (var gateWaitCancellation = new CancellationTokenSource())
            {
                var gateWaitTask = updateRemoteConfigGate.WaitAsync(gateWaitCancellation.Token);
                await Task.WhenAny(gateWaitTask, waitBudget);
                if (!gateWaitTask.IsCompleted)
                {
                    // 超时后撤销排队，避免晚到的门所有权没有调用者负责释放。
                    gateWaitCancellation.Cancel();
                }

                try
                {
                    // 若取消与获得门同时发生，以实际等待结果决定是否需要释放门。
                    await gateWaitTask;
                }
                catch (OperationCanceledException)
                {
                    await UniTask.SwitchToMainThread();
                    GuruLog.W(
                        $"[RemoteConfigService] Update queue exhausted the {RemoteConfigMaxWaitSeconds:F0}s shared wait budget. " +
                        "Existing cached or default values will remain in use.");
                    return;
                }
            }

            try
            {
                await UniTask.SwitchToMainThread();
                await UpdateRemoteConfigsCoreAsync(keys, acceptFreshSdkFetch, waitBudget);
            }
            finally
            {
                updateRemoteConfigGate.Release();
            }
        }

        private static async UniTask UpdateRemoteConfigsCoreAsync(
            IReadOnlyList<string> keys,
            bool acceptFreshSdkFetch,
            Task waitBudget)
        {
            var operationId = System.Threading.Interlocked.Increment(ref fetchOperationSequence);
            var remote = RemoteConfigManager.Instance;
            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId} started. " +
                $"keys=[{string.Join(", ", keys)}], appVersion={Application.version}, " +
                $"acceptFreshSdkFetch={acceptFreshSdkFetch}, " +
                $"internetReachability={Application.internetReachability}, initialState=({FormatFetchState(remote)}).");
            RegisterRemoteConfigCallback();
            TryToClearRemoteCache();
            var shouldUseFallbackValues = await ShouldUseFallbackValuesAsync(
                remote,
                operationId,
                acceptFreshSdkFetch,
                waitBudget);
            await UniTask.SwitchToMainThread();
            var resolvedConfigs = ResolveRemoteConfigs(keys, remote, shouldUseFallbackValues);
            var saved = PersistResolvedConfigs(resolvedConfigs);
            var cacheCommit = resolvedConfigs.Count == 0
                ? "notAttempted"
                : saved ? "succeeded" : "failed";
            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId} completed. " +
                $"fetchMode={(shouldUseFallbackValues ? "fallbackRequired" : "freshFirebaseAccepted")}, " +
                $"resolvedKeys={resolvedConfigs.Count}/{keys.Count}, cacheCommit={cacheCommit}, " +
                $"finalState=({FormatFetchState(remote)}).");
        }

        private static bool HasRemoteConfigKeys(IReadOnlyList<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                GuruLog.W("[RemoteConfigService] No remote config keys specified to update.");
                return false;
            }

            return true;
        }

        private static async UniTask<bool> ShouldUseFallbackValuesAsync(
            RemoteConfigManager remote,
            int operationId,
            bool acceptFreshSdkFetch,
            Task waitBudget)
        {
            var automaticFetchGetter = await WaitForSdkAutomaticFetchCompletionAsync(remote, operationId, waitBudget);
            if (automaticFetchGetter == null)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}: SDK automatic fetch completion could not be confirmed. " +
                    "Cached or default values will be used; startup ForceFetch remains deferred until the SDK automatic result returns.");
                return true;
            }

            // Loading 只等待自动结果，不加入后台强制阶段；缓存结果可先使用，晚到成功另行保存。
            if (acceptFreshSdkFetch)
            {
                return automaticFetchGetter.LastFetchStatus != RemoteConfigLastFetchStatus.Success;
            }

            // GM 手动入口仍串行等待启动任务，且只使用自己的剩余预算。
            var startupRequest = startupForceFetchTask;
            if (startupRequest == null || !await WaitForRequestWithinBudgetAsync(startupRequest, waitBudget))
            {
                await UniTask.SwitchToMainThread();
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}: startup ForceFetch has not completed within the " +
                    $"{RemoteConfigMaxWaitSeconds:F0}s shared budget. Cached or default values will be used; the background request is not canceled.");
                return true;
            }

            await UniTask.SwitchToMainThread();
            if (waitBudget.IsCompleted)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId} exhausted the {RemoteConfigMaxWaitSeconds:F0}s shared wait budget " +
                    $"while waiting for the SDK automatic fetch. Cached or default values will be used. {FormatFetchState(remote)}");
                return true;
            }

            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId}: startup ForceFetch completed; the manual request will ForceFetch once " +
                "within the remaining shared budget.");

            if (await TryForceFetchOnceAsync(remote, operationId, 1, waitBudget))
            {
                return false;
            }

            GuruLog.W(
                $"[RemoteConfigService] Fetch operation {operationId} did not produce a fresh successful result after one ForceFetch attempt. " +
                $"Cached or default values will be used. {FormatFetchState(remote)}");
            return true;
        }

        private static async UniTask<IRemoteConfigGetter> WaitForSdkAutomaticFetchCompletionAsync(
            RemoteConfigManager remote,
            int operationId,
            Task waitBudget)
        {
            var completionSource = sdkAutomaticFetchCompletionSource;
            if (completionSource == null)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}: SDK initialization tracking was not started. " +
                    FormatFetchState(remote));
                return null;
            }

            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId}: waiting for the tracked SDK automatic fetch callback " +
                $"within the {RemoteConfigMaxWaitSeconds:F0}s shared budget. observedCallbacks={sdkInitializationCallbackCount}, " +
                $"dispatchReturned={sdkInitializationDispatchReturned}, {FormatFetchState(remote)}");
            var automaticFetchTask = completionSource.Task;
            await Task.WhenAny(automaticFetchTask, waitBudget);
            await UniTask.SwitchToMainThread();
            if (automaticFetchTask.Status == TaskStatus.RanToCompletion)
            {
                var getter = await automaticFetchTask;
                GuruLog.I(
                    $"[RemoteConfigService] Fetch operation {operationId}: tracked SDK automatic fetch completed. " +
                    FormatFetchState(getter));
                return getter;
            }

            GuruLog.W(
                $"[RemoteConfigService] Fetch operation {operationId}: no confirmed SDK automatic fetch completion was observed " +
                $"within the {RemoteConfigMaxWaitSeconds:F0}s shared budget. {FormatFetchState(remote)}");
            return null;
        }

        private static async UniTask<bool> TryForceFetchOnceAsync(
            RemoteConfigManager remote,
            int operationId,
            int attempt,
            Task waitBudget)
        {
            // 已过截止时间时不再派发强制请求，也不重新开启等待。
            if (waitBudget.IsCompleted)
            {
                return false;
            }

            var forceFetchStartedAtUtc = DateTime.UtcNow;
            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}/{ForceFetchMaxAttempts} started at " +
                $"{forceFetchStartedAtUtc:O}. internetReachability={Application.internetReachability}, beforeState=({FormatFetchState(remote)}).");
            var nextFetchCallbackTask = WaitForNextActiveFetchCallbackAsync(remote, operationId, attempt, waitBudget).AsTask();

            try
            {
                var requestCompleted = await WaitForRequestWithinBudgetAsync(remote.ForceFetch().AsTask(), waitBudget);
                await UniTask.SwitchToMainThread();
                if (!requestCompleted)
                {
                    GuruLog.W(
                        $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: ForceFetch task did not finish before the shared wait budget. " +
                        $"{FormatFetchState(remote)}");
                    return false;
                }
            }
            catch (Exception exception)
            {
                await UniTask.SwitchToMainThread();
                GuruLog.E(
                    $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: ForceFetch task threw " +
                    $"{exception.GetType().FullName}: {exception.Message}. {FormatFetchState(remote)}\n{exception.StackTrace}");
                return false;
            }

            GuruLog.I(
                $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: ForceFetch task completed. " +
                $"afterState=({FormatFetchState(remote)}).");
            if (HasFreshSuccessfulFetchResult(remote, forceFetchStartedAtUtc, operationId, attempt))
            {
                GuruLog.I($"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt} completed with a fresh Firebase result.");
                return true;
            }

            if (!await WaitForRequestWithinBudgetAsync(nextFetchCallbackTask, waitBudget))
            {
                await UniTask.SwitchToMainThread();
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: active callback wait did not finish before the shared wait budget. " +
                    $"{FormatFetchState(remote)}");
                return false;
            }
            var observedFetchCallback = await nextFetchCallbackTask;
            await UniTask.SwitchToMainThread();
            if (!observedFetchCallback)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: no active fetch callback was observed " +
                    $"before the current stage budget expired. {FormatFetchState(remote)}");
            }

            return HasFreshSuccessfulFetchResult(remote, forceFetchStartedAtUtc, operationId, attempt);
        }

        // 仅退出等待；同帧收到结果与预算到期时优先处理已完成请求，不清 SDK 队列。
        private static async Task<bool> WaitForRequestWithinBudgetAsync(Task request, Task waitBudget)
        {
            await Task.WhenAny(request, waitBudget);
            if (!request.IsCompleted)
            {
                return false;
            }
            await request;
            return true;
        }

        private static async UniTask<bool> WaitForNextActiveFetchCallbackAsync(
            RemoteConfigManager remote,
            int operationId,
            int attempt,
            Task waitBudget)
        {
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable subscription = null;

            subscription = remote.ObserveConfigGetter()
                .Skip(1)
                .Subscribe(getter =>
                {
                    if (getter == null)
                    {
                        GuruLog.W(
                            $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: observed a null callback getter.");
                        return;
                    }

                    GuruLog.I(
                        $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: callback observed. {FormatFetchState(getter)}");
                    if (!getter.IsActive || !gate.TrySetResult(true))
                    {
                        return;
                    }

                    subscription?.Dispose();
                });

            try
            {
                await Task.WhenAny(gate.Task, waitBudget);
                await UniTask.SwitchToMainThread();
                return gate.Task.Status == TaskStatus.RanToCompletion && await gate.Task;
            }
            finally
            {
                // 临时订阅只保留到回调或本轮预算结束；长期缓存订阅不受影响。
                subscription?.Dispose();
            }
        }

        private static bool HasFreshSuccessfulFetchResult(
            RemoteConfigManager remote,
            DateTime fetchStartedAtUtc,
            int operationId,
            int attempt)
        {
            if (!TryValidateFetchResult(remote, operationId, attempt))
            {
                return false;
            }

            if (!remote.LastFetchTime.HasValue || remote.LastFetchTime.Value == DateTime.MinValue)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: Firebase reported Success but did not provide a valid LastFetchTime. " +
                    $"{FormatFetchState(remote)}");
                return false;
            }

            var lastFetchTimeUtc = remote.LastFetchTime.Value.ToUniversalTime();
            if (lastFetchTimeUtc >= fetchStartedAtUtc)
            {
                return true;
            }

            GuruLog.W(
                $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: Firebase result was not fresh. " +
                $"lastFetchTimeUtc={lastFetchTimeUtc:O}, forceFetchStartedAtUtc={fetchStartedAtUtc:O}, {FormatFetchState(remote)}");
            return false;
        }

        private static Dictionary<string, string> ResolveRemoteConfigs(
            IReadOnlyList<string> keys,
            RemoteConfigManager remote,
            bool shouldUseFallbackValues)
        {
            var resolvedConfigs = new Dictionary<string, string>(keys.Count);
            foreach (var key in keys)
            {
                if (!TryResolveRemoteConfigValue(key, remote, shouldUseFallbackValues, out var value))
                {
                    continue;
                }

                resolvedConfigs[key] = value;
            }

            return resolvedConfigs;
        }

        private static bool TryResolveRemoteConfigValue(
            string key,
            RemoteConfigManager remote,
            bool shouldUseFallbackValues,
            out string value)
        {
            value = null;
            var resolvedSource = "none";

            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (shouldUseFallbackValues)
            {
                if (!TryResolveFallbackRemoteConfigValue(key, out value))
                {
                    return false;
                }

                resolvedSource = "fallbackAfterFetchFailure";
            }
            else
            {
                if (!latestValidConfigs.TryGetValue(key, out value))
                {
                    value = remote.GetString(key);
                    resolvedSource = "sdkActiveValue";
                }
                else
                {
                    resolvedSource = "validatedCallbackValue";
                }
                if (!TryValidateRemoteConfigJson(key, value) || !TryValidateRemoteConfigValueVersion(key, value))
                {
                    if (!TryResolveFallbackRemoteConfigValue(key, out value))
                    {
                        return false;
                    }

                    resolvedSource = "fallbackAfterInvalidFirebaseValue";
                }
            }

            if (!TryValidateRemoteConfigJson(key, value))
            {
                return false;
            }

            GuruLog.I(
                $"[RemoteConfigService] RemoteConfig '{key}' resolvedSource={resolvedSource}, raw json: {value}");
            return true;
        }

        private static bool TryResolveFallbackRemoteConfigValue(string key, out string value)
        {
            if (TryGetCachedOrDefaultRemoteConfigValue(key, out value))
            {
                return true;
            }

            GuruLog.W($"[RemoteConfigService] No cached or default fallback found for remote config key '{key}'.");
            return false;
        }

        private static bool TryValidateRemoteConfigValueVersion(string key, string value)
        {
            if (!RequiresRemoteConfigVersionValidation(key))
            {
                return true;
            }

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (!JsonUtils.TryGetValue<string>(value, CurrentRemoteConfigVersionFieldKey, out var remoteConfigVersion) ||
                string.IsNullOrWhiteSpace(remoteConfigVersion))
            {
                GuruLog.W(
                    $"[RemoteConfigService] RemoteConfig '{key}' is missing '{CurrentRemoteConfigVersionFieldKey}'. Cached or default values will be used.");
                return false;
            }

            remoteConfigVersion = remoteConfigVersion.Trim();
            if (string.Equals(remoteConfigVersion, Application.version, StringComparison.Ordinal))
            {
                return true;
            }

            GuruLog.W(
                $"[RemoteConfigService] RemoteConfig '{key}' version mismatch. remote={remoteConfigVersion}, app={Application.version}. Cached or default values will be used.");
            return false;
        }

        private static bool RequiresRemoteConfigVersionValidation(string key)
        {
            return string.Equals(key, LevelConfigKey, StringComparison.Ordinal);
        }

        private static bool PersistResolvedConfigs(
            IReadOnlyDictionary<string, string> resolvedConfigs,
            bool isCallbackUpdate = false,
            string source = "loading_or_manual",
            int callbackId = 0)
        {
            if (resolvedConfigs.Count == 0)
            {
                return true;
            }

            var gameManager = GameManager.IsInitialized ? GameManager.Instance : null;
            var drawLevels = gameManager?.DrawGameLevel;
            List<Config.DrawLevelSort> drawCandidate = null;
            bool refreshDraw = false;
            bool drawDeferredOrRejected = false;
            IReadOnlyDictionary<string, string> entries = resolvedConfigs;
            string trace = $"[RemoteConfigService] callback={callbackId}, source={source}, lastObservedDrawCallback={latestDrawCallbackSequence}, lastObservedDigCallback={latestDigCallbackSequence}";
            GuruLog.I($"{trace}, stage=commit_begin, keys=[{string.Join(",", resolvedConfigs.Keys)}], drawReady={drawLevels?.IsReady == true}, generation={drawLevels?.ConfigGeneration ?? 0}.");
            if (drawLevels?.IsReady == true && resolvedConfigs.TryGetValue(DrawGameConfigKey, out string drawJson))
            {
                ConfigManager.RemoteConfig.TryGetRaw(DrawGameConfigKey, out string previousJson);
                if (drawJson == previousJson)
                {
                    // 新回调可以撤销旧候选；手动请求超时后的缓存回退不能丢掉已收到的待提交更新。
                    if (isCallbackUpdate) pendingDrawConfig = null;
                    GuruLog.I($"{trace}, stage=draw_unchanged, reason=same_json, pendingCleared={isCallbackUpdate}, generation={drawLevels.ConfigGeneration}.");
                }
                else if (!drawLevels.TryPrepareRemoteConfig(drawJson, out drawCandidate, out string error))
                {
                    GuruLog.W($"{trace}, stage=draw_rejected, reason={error}; previous cache and sequence retained.");
                    if (pendingDrawConfig != null || previousJson != null)
                        latestValidConfigs[DrawGameConfigKey] = pendingDrawConfig ?? previousJson;
                    else
                        latestValidConfigs.Remove(DrawGameConfigKey);
                    drawDeferredOrRejected = true;
                }
                else if (gameManager.IsDrawRemoteConfigRefreshBlocked)
                {
                    pendingDrawConfig = drawJson;
                    pendingDrawCallbackSequence = callbackId;
                    drawDeferredOrRejected = true;
                    GuruLog.I($"{trace}, stage=draw_deferred, {gameManager.DrawRemoteConfigRefreshState}.");
                }
                else
                {
                    pendingDrawConfig = null;
                    refreshDraw = true;
                }

                if (drawDeferredOrRejected)
                {
                    var acceptedEntries = new Dictionary<string, string>();
                    foreach (var entry in resolvedConfigs)
                        if (entry.Key != DrawGameConfigKey) acceptedEntries[entry.Key] = entry.Value;
                    entries = acceptedEntries;
                }
            }
            else if (resolvedConfigs.ContainsKey(DrawGameConfigKey))
            {
                GuruLog.I($"{trace}, stage=draw_wait_initialization; cache is committed first, Loading initializes the sequence afterwards.");
            }

            var digLevels = gameManager?.DigGameLevel;
            List<Config.DigLevelSort> digCandidate = null;
            bool refreshDig = false;
            bool digDeferredOrRejected = false;
            if (digLevels?.IsReady == true && resolvedConfigs.TryGetValue(DigGameConfigKey, out string digJson))
            {
                ConfigManager.RemoteConfig.TryGetRaw(DigGameConfigKey, out string previousJson);
                if (digJson == previousJson)
                {
                    // 新回调可以撤销旧候选；手动请求超时后的缓存回退不能丢掉已收到的待提交更新。
                    if (isCallbackUpdate) pendingDigConfig = null;
                    GuruLog.I($"{trace}, stage=dig_unchanged, reason=same_json, pendingCleared={isCallbackUpdate}, generation={digLevels.ConfigGeneration}.");
                }
                else if (!digLevels.TryPrepareRemoteConfig(digJson, out digCandidate, out string error))
                {
                    GuruLog.W($"{trace}, stage=dig_rejected, reason={error}; previous cache and sequence retained.");
                    if (pendingDigConfig != null || previousJson != null)
                        latestValidConfigs[DigGameConfigKey] = pendingDigConfig ?? previousJson;
                    else
                        latestValidConfigs.Remove(DigGameConfigKey);
                    digDeferredOrRejected = true;
                }
                else if (gameManager.IsDigRemoteConfigRefreshBlocked)
                {
                    pendingDigConfig = digJson;
                    pendingDigCallbackSequence = callbackId;
                    digDeferredOrRejected = true;
                    GuruLog.I($"{trace}, stage=dig_deferred, {gameManager.DigRemoteConfigRefreshState}.");
                }
                else
                {
                    pendingDigConfig = null;
                    refreshDig = true;
                }

                if (digDeferredOrRejected)
                {
                    var acceptedEntries = new Dictionary<string, string>();
                    foreach (var entry in entries)
                        if (entry.Key != DigGameConfigKey) acceptedEntries[entry.Key] = entry.Value;
                    entries = acceptedEntries;
                }
            }
            else if (resolvedConfigs.ContainsKey(DigGameConfigKey))
            {
                GuruLog.I($"{trace}, stage=dig_wait_initialization; cache is committed first, Loading initializes the sequence afterwards.");
            }

            // 批量写入，避免多 key 场景下逐 key 落盘
            var saved = ConfigManager.RemoteConfig.TrySetRawBatch(entries);
            GuruLog.I($"{trace}, stage=cache_commit, saved={saved}, drawIncluded={entries.ContainsKey(DrawGameConfigKey)}, drawDeferredOrRejected={drawDeferredOrRejected}, digIncluded={entries.ContainsKey(DigGameConfigKey)}, digDeferredOrRejected={digDeferredOrRejected}.");
            if (!saved)
            {
                GuruLog.E($"{trace}, stage=cache_failed; previous business cache retained.");
            }
            else if (refreshDraw)
            {
                // 保存与列表切换之间不得异步让出，避免业务看到新 JSON 配旧排序。
                int previousGeneration = drawLevels.ConfigGeneration;
                drawLevels.ApplyRemoteConfig(drawCandidate);
                GuruLog.I($"{trace}, stage=draw_applied, generation={previousGeneration}->{drawLevels.ConfigGeneration}, count={drawLevels.LevelSorts.Count}; requesting existing view refresh.");
                gameManager.RefreshDrawRemoteConfigViews();
                GuruLog.I($"{trace}, stage=view_dispatch_complete; see DrawRemoteConfigUI for refreshed or skipped panels.");
            }
            if (saved && refreshDig)
            {
                // 与 Draw 相同：缓存与有效排序之间不异步让出，现存 UI 按新版本刷新。
                digLevels.ApplyRemoteConfig(digCandidate);
                gameManager.RefreshDigRemoteConfigViews();
            }
            return saved && !drawDeferredOrRejected && !digDeferredOrRejected;
        }

        /// <summary>
        /// 由已存在的 GameManager.Update 处理最新的延期 Draw 配置；无候选时立即返回。
        /// 仅在运行态且广告/转场结束后提交，不重新请求 Firebase，不重跑 Loading。
        /// </summary>
        internal static void TryApplyPendingDrawConfig(GameManager gameManager)
        {
            if (!Application.isPlaying || pendingDrawConfig == null || !isCacheReady ||
                gameManager.DrawGameLevel?.IsReady != true || gameManager.IsDrawRemoteConfigRefreshBlocked)
                return;

            string candidate = pendingDrawConfig;
            int callbackId = pendingDrawCallbackSequence;
            pendingDrawConfig = null;
            GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=draw_deferred_resume, {gameManager.DrawRemoteConfigRefreshState}.");
            PersistResolvedConfigs(new Dictionary<string, string> { [DrawGameConfigKey] = candidate }, source: "deferred", callbackId: callbackId);
        }

        /// <summary>
        /// 由已存在的 GameManager.Update 处理最新的延期 Dig 配置；无候选时立即返回。
        /// 仅在运行态且广告/转场结束后提交，不重新请求 Firebase，不重跑 Loading。
        /// </summary>
        internal static void TryApplyPendingDigConfig(GameManager gameManager)
        {
            if (!Application.isPlaying || pendingDigConfig == null || !isCacheReady ||
                gameManager.DigGameLevel?.IsReady != true || gameManager.IsDigRemoteConfigRefreshBlocked)
                return;

            string candidate = pendingDigConfig;
            int callbackId = pendingDigCallbackSequence;
            pendingDigConfig = null;
            GuruLog.I($"[RemoteConfigService] callback={callbackId}, stage=dig_deferred_resume, {gameManager.DigRemoteConfigRefreshState}.");
            PersistResolvedConfigs(new Dictionary<string, string> { [DigGameConfigKey] = candidate }, source: "deferred", callbackId: callbackId);
        }

        private static bool TryValidateFetchResult(RemoteConfigManager remote, int operationId, int attempt)
        {
            var lastFetchStatus = remote.LastFetchStatus;
            if (lastFetchStatus != RemoteConfigLastFetchStatus.Success)
            {
                GuruLog.W(
                    $"[RemoteConfigService] Fetch operation {operationId}, attempt {attempt}: Firebase reported an unusable result. " +
                    $"diagnosis={DescribeFetchFailure(remote)}, {FormatFetchState(remote)}");

                return false;
            }

            return true;
        }

        private static string DescribeFetchFailure(RemoteConfigManager remote)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return "Unity reports no reachable network";
            }

            return remote.LastFetchFailureReason switch
            {
                RemoteConfigFetchFailureReason.Throttled =>
                    $"Firebase throttled the request until {FormatDateTime(remote.ThrottledEndTime)}",
                RemoteConfigFetchFailureReason.Invalid =>
                    "Firebase rejected the request or returned an invalid fetch result",
                RemoteConfigFetchFailureReason.Error =>
                    "Firebase reported a fetch error; inspect the adjacent Guru/Firebase exception log for the underlying SDK message",
                _ when remote.LastFetchStatus == RemoteConfigLastFetchStatus.Pending =>
                    "Firebase fetch is still pending or no terminal callback has been published",
                _ => "Firebase did not expose a specific failure reason"
            };
        }

        private static string FormatFetchState(RemoteConfigManager remote)
        {
            return
                $"lastFetchStatus={remote.LastFetchStatus?.ToString() ?? "null"}, " +
                $"lastFetchFailureReason={remote.LastFetchFailureReason?.ToString() ?? "null"}, " +
                $"lastFetchTime={FormatDateTime(remote.LastFetchTime)}, " +
                $"throttledEndTime={FormatDateTime(remote.ThrottledEndTime)}";
        }

        private static string FormatFetchState(IRemoteConfigGetter getter)
        {
            return
                $"isActive={getter.IsActive}, lastFetchStatus={getter.LastFetchStatus}, " +
                $"lastFetchFailureReason={getter.LastFetchFailureReason?.ToString() ?? "null"}, " +
                $"lastFetchTime={FormatDateTime(getter.LastFetchTime)}, " +
                $"throttledEndTime={FormatDateTime(getter.ThrottledEndTime)}";
        }

        private static string FormatDateTime(DateTime? value)
        {
            return value.HasValue && value.Value != DateTime.MinValue
                ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : "null";
        }

        private static bool TryGetCachedOrDefaultRemoteConfigValue(string key, out string value)
        {
            value = null;

            var cache = DB.GameData?.RemoteConfigCache;
            if (cache != null && cache.TryGetValue(key, out var cachedValue) && !string.IsNullOrEmpty(cachedValue))
            {
                value = cachedValue;
                GuruLog.I($"[RemoteConfigService] Using cached RemoteConfig fallback for key '{key}'.");
                return true;
            }

            if (TryGetRemoteConfigSpecDefaultValue(key, out var defaultValue))
            {
                value = defaultValue;
                GuruLog.I($"[RemoteConfigService] Using RemoteConfigSpec default fallback for key '{key}'.");
                return true;
            }

            return false;
        }

        private static bool TryGetRemoteConfigSpecDefaultValue(string key, out string value)
        {
            value = null;

            if (GuruSdk.Instance == null)
            {
                return false;
            }

            if (GuruSdk.Instance.RemoteConfigSpec is not RemoteConfigSpec remoteConfigSpec)
            {
                return false;
            }

            if (!remoteConfigSpec.DefaultValues.TryGetValue(key, out var defaultValue) || defaultValue == null)
            {
                return false;
            }

            value = defaultValue switch
            {
                string stringValue => stringValue,
                IFormattable formattableValue => formattableValue.ToString(null, CultureInfo.InvariantCulture),
                _ => defaultValue.ToString()
            };

            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// 拉取 Remote Config 并输出 custom_config 的调试日志。
        /// </summary>
        public static async UniTask UpdateCustomConfigAsync()
        {
            // 兼容旧接口，内部复用统一实现
            await UpdateRemoteConfigsAsync(new[] { LevelConfigKey });
        }

        /// <summary>
        /// 适配 LoadManager 的加载流程，提供 IEnumerator&lt;float&gt; 形式的进度协程。
        /// </summary>
        public static IEnumerator<float> UpdateRemoteConfigsForLoadManager()
        {
            var task = UpdateRemoteConfigsAsync();

            // 简单进度：等待远程拉取完成前一直返回 0，完成后返回 1
            while (!task.Status.IsCompleted())
            {
                yield return 0f;
            }

            yield return 1f;
        }

        /// <summary>
        /// 尝试清理 remoteConfig 缓存。版本号不一致，尝试清理一次 cache。不影响更新
        /// </summary>
        public static void TryToClearRemoteCache()
        {
            if (isCacheReady)
            {
                return;
            }

            if (ShouldClearCache())
            {
                ClearRemoteCache();
                ForceUpdateSpecConfigToCache();
            }

            isCacheReady = true;
            if (latestValidConfigs.Count > 0)
            {
                var saved = PersistResolvedConfigs(latestValidConfigs, source: "startup_retained", callbackId: latestDrawCallbackSequence);
                GuruLog.I($"[RemoteConfigService] Startup retained config values: keys={latestValidConfigs.Count}, cacheSaved={saved}.");
            }
        }

        /// <summary>
        /// 强制更新成为当前版本本地 spec config
        /// </summary>
        private static void ForceUpdateSpecConfigToCache()
        {
            var resolvedConfigs = new Dictionary<string, string>(RemoteConfigKeys.Count);
            for (int i = 0; i < RemoteConfigKeys.Count; i++)
            {
                var key = RemoteConfigKeys[i];
                TryGetRemoteConfigSpecDefaultValue(key, out var defaultValue);

                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(defaultValue))
                {
                    continue;
                }

                GuruLog.I($"[RemoteConfigService] RemoteConfig '{key}' raw json: {defaultValue}");
                resolvedConfigs[key] = defaultValue;
            }

            if (resolvedConfigs.Count > 0)
            {
                // 批量写入，避免多 key 场景下逐 key 落盘
                ConfigManager.RemoteConfig.SetRawBatch(resolvedConfigs);
            }
        }

        /// <summary>
        /// 清理 DB 缓存
        /// </summary>
        public static void ClearRemoteCache()
        {
            GuruLog.I("[RemoteConfigService] stage=cache_reset.");
            DB.GameData.RemoteConfigCache.Clear();
            DB.GameData.CurrentAppVersion = Application.version;
            DB.GameData.Save();
        }

        /// <summary>
        /// 是否需要清理条件
        /// </summary>
        /// <returns></returns>
        private static bool ShouldClearCache()
        {
            return DB.GameSetting.IsNewVersion;
        }
    }
}
