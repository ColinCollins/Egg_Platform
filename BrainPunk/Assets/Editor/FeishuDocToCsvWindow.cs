#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

/// <summary>
/// 从飞书在线文档拉取内容并保存为 UTF-8 CSV 的编辑器工具。
/// 使用前需在飞书开放平台创建自建应用并开通「查看文档」等权限。
/// 配置保存在工程内 Assets/Editor/FeishuDocToCsvSettings.json，随工程一起可被他人使用。
/// </summary>
public class FeishuDocToCsvWindow : EditorWindow
{
    /// <summary>配置保存在工程内，相对 Assets 的路径，便于团队共享。</summary>
    private const string SettingsRelativePath = "Editor/FeishuDocToCsvSettings.json";

    /// <summary>OAuth 回调地址，需在飞书开放平台 安全设置 → 重定向 URL 中添加。</summary>
    private const string OAuthRedirectUri = "http://127.0.0.1:2847/feishu/callback";
    private const int OAuthCallbackPort = 2847;

    /// <summary>输出文件路径（相对工程根目录），拉取后将覆盖该文件。</summary>
    private const string OutputRelativePath = "Luban/Configs/Datas/localization.csv";

    private enum SourceType { Document, Sheet }

    [Serializable]
    private class FeishuDocToCsvSettingsData
    {
        public string AppId = "";
        public string AppSecret = "";
        public string DocId = "";
        public bool TabToComma = true;
        public int SourceTypeValue = 1; // 1 = Sheet
        public string UserToken = "";
        public string UserRefreshToken = "";
        public string UserTokenExpire = "";
        public string ExcludeColumns = "修改状态";
    }

    private string _appId = "";
    private string _appSecret = "";
    private string _documentId = "";
    private bool _tabToComma = true;
    private bool _useDocxApi = true;
    private SourceType _sourceType = SourceType.Sheet;
    private string _sheetIdForExport;
    private string _userAccessTokenOptional = "";
    private string _userRefreshToken = "";
    private long _userTokenExpireTicks;
    private string _excludeColumns = "修改状态";
    private Vector2 _scrollPos;
    private string _statusText = "";
    private bool _isLoading;
    private string _pendingOAuthCode;
    private string _pendingOAuthAppToken;
    private Thread _oauthListenerThread;

    [MenuItem("Tools/飞书文档/拉取文档并保存为 CSV")]
    public static void OpenWindow()
    {
        var win = GetWindow<FeishuDocToCsvWindow>("飞书文档 → CSV");
        win.minSize = new Vector2(400, 320);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Application.dataPath, SettingsRelativePath).Replace('\\', '/');
    }

    private void LoadSettings()
    {
        string path = GetSettingsPath();
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var data = JsonUtility.FromJson<FeishuDocToCsvSettingsData>(json);
                if (data != null)
                {
                    _appId = data.AppId ?? "";
                    _appSecret = data.AppSecret ?? "";
                    _documentId = data.DocId ?? "";
                    _tabToComma = data.TabToComma;
                    _sourceType = (SourceType)Mathf.Clamp(data.SourceTypeValue, 0, 1);
                    _userAccessTokenOptional = data.UserToken ?? "";
                    _userRefreshToken = data.UserRefreshToken ?? "";
                    _excludeColumns = data.ExcludeColumns ?? "修改状态";
                    long.TryParse(data.UserTokenExpire ?? "0", out _userTokenExpireTicks);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("FeishuDocToCsv: 读取工程配置失败，使用默认值 - " + e.Message);
            }
        }
        _userTokenExpireTicks = 0;
    }

    private void SaveSettings()
    {
        var data = new FeishuDocToCsvSettingsData
        {
            AppId = _appId ?? "",
            AppSecret = _appSecret ?? "",
            DocId = _documentId ?? "",
            TabToComma = _tabToComma,
            SourceTypeValue = (int)_sourceType,
            UserToken = _userAccessTokenOptional ?? "",
            UserRefreshToken = _userRefreshToken ?? "",
            UserTokenExpire = _userTokenExpireTicks.ToString(),
            ExcludeColumns = _excludeColumns ?? "修改状态"
        };
        string path = GetSettingsPath();
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning("FeishuDocToCsv: 保存工程配置失败 - " + e.Message);
        }
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void OnDisable()
    {
        // 不在此自动保存，仅通过「保存信息」按钮主动保存
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "支持：飞书文档（纯文本）或 飞书电子表格（导出为 CSV）。\n" +
            "可粘贴完整链接，将自动识别并提取 token；表格会导出第一个工作表。",
            MessageType.Info);
        EditorGUILayout.Space(4);

        EditorGUI.BeginDisabledGroup(_isLoading);

        _appId = EditorGUILayout.TextField("App ID", _appId);
        _appSecret = EditorGUILayout.PasswordField("App Secret", _appSecret);
        _sourceType = (SourceType)EditorGUILayout.EnumPopup("来源类型", _sourceType);
        _documentId = EditorGUILayout.TextField(_sourceType == SourceType.Sheet ? "表格链接 / spreadsheet_token" : "文档 ID / docToken", _documentId);
        if (_sourceType == SourceType.Sheet)
        {
            _userAccessTokenOptional = EditorGUILayout.PasswordField("User Access Token (可选，个人表格必填)", _userAccessTokenOptional);
            _excludeColumns = EditorGUILayout.TextField("导出时排除的列（逗号分隔）", _excludeColumns);
            if (GUILayout.Button("通过浏览器授权并自动获取 User Token", GUILayout.Height(22)))
                StartOAuthUserTokenFlow();
            EditorGUILayout.HelpBox(
                "表格在个人空间或未授权给应用时，可点击上方按钮用浏览器授权，工具会自动获取并保存 User Token。\n\n" +
                "授权前请先在飞书开放平台完成两项配置：\n" +
                "1. 安全设置 → 重定向 URL 中添加: " + OAuthRedirectUri + "\n" +
                "2. 权限管理 → 申请并开通以下「用户身份权限」：\n" +
                "   · 获取用户基本信息\n" +
                "   · 查看新版文档\n" +
                "   · 查看、评论和下载云空间中所有文件\n" +
                "   · 导出云文档（drive:export:readonly）\n" +
                "   · 文档导出（docs:document:export）\n" +
                "   · 查看、评论和导出电子表格\n" +
                "（若提示 20027「未申请相关权限」，即需先在权限管理中开通上述用户身份权限。）",
                MessageType.Info);
        }
        if (_sourceType == SourceType.Document)
        {
            _useDocxApi = EditorGUILayout.Toggle("使用新版文档接口 (docx)", _useDocxApi);
            _tabToComma = EditorGUILayout.Toggle("Tab 转逗号 (表格→CSV)", _tabToComma);
        }
        EditorGUILayout.LabelField("输出文件", OutputRelativePath);

        EditorGUILayout.Space(6);
        if (GUILayout.Button("保存信息", GUILayout.Height(22)))
        {
            if (EditorUtility.DisplayDialog("保存配置", "确定要将当前配置保存到工程内吗？\n保存后，团队其他人打开本界面时会自动读取这些配置。\n\n配置文件：Assets/Editor/FeishuDocToCsvSettings.json", "确定保存", "取消"))
            {
                SaveSettings();
                _statusText = "已保存到工程内 " + SettingsRelativePath;
            }
        }
        EditorGUILayout.Space(8);

        if (GUILayout.Button(_isLoading ? "拉取中..." : "拉取并保存为 CSV", GUILayout.Height(32)))
        {
            PullAndSaveCsv();
        }

        EditorGUI.EndDisabledGroup();

        if (!string.IsNullOrEmpty(_statusText))
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(_statusText, _statusText.StartsWith("错误") ? MessageType.Error : MessageType.Info);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("复制错误/状态信息", GUILayout.Width(140)))
            {
                EditorGUIUtility.systemCopyBuffer = _statusText;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void PullAndSaveCsv()
    {
        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_appSecret))
        {
            _statusText = "错误：请填写 App ID 和 App Secret（飞书开放平台 → 自建应用 → 凭证与基础信息）";
            return;
        }

        if (string.IsNullOrEmpty(_documentId))
        {
            _statusText = _sourceType == SourceType.Sheet
                ? "错误：请填写表格链接或 spreadsheet_token（可从表格 URL 的 /sheets/ 后复制）。"
                : "错误：请填写文档 ID 或粘贴飞书文档完整链接（新版 /docx/ 或旧版 /doc/）。";
            return;
        }

        if (_sourceType == SourceType.Sheet)
        {
            string spreadsheetToken;
            string sheetId;
            NormalizeSheetInput(_documentId, out spreadsheetToken, out sheetId);
            if (string.IsNullOrEmpty(spreadsheetToken))
            {
                _statusText = "错误：无法解析表格 token，请粘贴完整飞书表格链接（含 /sheets/ 的 URL）。";
                return;
            }
            _documentId = spreadsheetToken;
            _sheetIdForExport = sheetId;
        }
        else
        {
            _documentId = NormalizeDocumentId(_documentId);
            if (string.IsNullOrEmpty(_documentId))
            {
                _statusText = "错误：无法从输入中解析出文档 ID，请粘贴完整链接或只填 /docx/ 或 /doc/ 后的 ID。";
                return;
            }
        }

        _isLoading = true;
        _statusText = "正在获取访问凭证...";
        Repaint();

        GetTokenAndPullContent();
    }

    /// <summary>从用户输入解析表格 spreadsheet_token 和可选的 sheet_id（用于导出 CSV）</summary>
    private static void NormalizeSheetInput(string input, out string spreadsheetToken, out string sheetId)
    {
        spreadsheetToken = null;
        sheetId = null;
        if (string.IsNullOrWhiteSpace(input)) return;
        input = input.Trim();
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(input);
                var segs = uri.AbsolutePath.Trim('/').Split('/');
                for (int i = 0; i < segs.Length - 1; i++)
                {
                    if (string.Equals(segs[i], "sheets", StringComparison.OrdinalIgnoreCase) && i + 1 < segs.Length)
                    {
                        spreadsheetToken = segs[i + 1].Split('?')[0].Trim();
                        break;
                    }
                }
                var query = uri.Query;
                if (!string.IsNullOrEmpty(query) && query.StartsWith("?")) query = query.Substring(1);
                foreach (var part in query.Split('&'))
                {
                    var eq = part.IndexOf('=');
                    if (eq > 0 && string.Equals(part.Substring(0, eq), "sheet", StringComparison.OrdinalIgnoreCase))
                    {
                        sheetId = eq + 1 < part.Length ? part.Substring(eq + 1) : null;
                        break;
                    }
                }
            }
            catch { /* ignore */ }
            return;
        }
        spreadsheetToken = input.Split('?')[0].Trim();
        if (spreadsheetToken.Length == 0) spreadsheetToken = null;
    }

    /// <summary>从用户输入中解析出文档 ID（支持粘贴完整 URL 或纯 ID）</summary>
    private static string NormalizeDocumentId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(input);
                var segs = uri.AbsolutePath.Trim('/').Split('/');
                for (int i = 0; i < segs.Length - 1; i++)
                {
                    if (segs[i].Equals("docx", StringComparison.OrdinalIgnoreCase) ||
                        segs[i].Equals("doc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < segs.Length)
                            return segs[i + 1].Split('?')[0];
                    }
                }
                return segs.Length > 0 ? segs[segs.Length - 1].Split('?')[0] : null;
            }
            catch { return null; }
        }
        var id = input.Split('?')[0].Trim();
        return id.Length > 0 ? id : null;
    }

    private void GetTokenAndPullContent()
    {
        var body = "{\"app_id\":\"" + EscapeJson(_appId) + "\",\"app_secret\":\"" + EscapeJson(_appSecret) + "\"}";
        var req = new UnityWebRequest("https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string tokenJson = null;
            string errorMsg = null;
            bool success = false;
            try
            {
                success = req != null && req.result == UnityWebRequest.Result.Success;
                if (req != null)
                {
                    if (success)
                        tokenJson = req.downloadHandler?.text;
                    else
                        errorMsg = req.error ?? "Unknown error";
                    req.Dispose();
                    req = null;
                }
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                try { req?.Dispose(); } catch { /* ignore */ }
                req = null;
            }

            if (!success)
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：获取 token 失败 - " + (errorMsg ?? "未知错误");
                    Repaint();
                });
                return;
            }

            if (string.IsNullOrEmpty(tokenJson))
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：token 返回为空。";
                    Repaint();
                });
                return;
            }

            string token = null;
            int code = -1;
            ParseTokenResponse(tokenJson, out code, out token);

            if (code != 0 || string.IsNullOrEmpty(token))
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：token 返回异常 - " + tokenJson;
                    Repaint();
                });
                return;
            }

            if (_sourceType == SourceType.Sheet)
                SheetExportFlow(GetSheetAccessToken(token));
            else
                PullRawContent(token);
        };
    }

    private string _exportToken;
    private string _exportTicket;
    private int _exportPollCount;
    private DateTime _exportPollStartUtc;
    private DateTime _exportPollNextUtc;
    private const int ExportTimeoutSeconds = 180;
    private const int ExportPollIntervalSeconds = 2;

    /// <summary>表格流程使用的 token：优先使用已保存的 User Token（未过期则用），否则用手填或租户 token。</summary>
    private string GetSheetAccessToken(string tenantAccessToken)
    {
        var storedUser = _userAccessTokenOptional != null ? _userAccessTokenOptional.Trim() : "";
        if (!string.IsNullOrEmpty(storedUser) && _userTokenExpireTicks > 0 &&
            DateTime.UtcNow.Ticks < _userTokenExpireTicks - TimeSpan.FromMinutes(5).Ticks)
            return storedUser;
        var userToken = _userAccessTokenOptional != null ? _userAccessTokenOptional.Trim() : "";
        if (!string.IsNullOrEmpty(userToken)) return userToken;
        return tenantAccessToken != null ? tenantAccessToken.Trim() : "";
    }

    private static void ParseUserTokenResponse(string json, out string accessToken, out string refreshToken, out int expiresIn)
    {
        accessToken = null;
        refreshToken = null;
        expiresIn = 0;
        if (string.IsNullOrEmpty(json)) return;
        var codeIdx = json.IndexOf("\"code\":", StringComparison.Ordinal);
        if (codeIdx >= 0)
        {
            var codeEnd = json.IndexOfAny(new[] { ',', '}' }, codeIdx + 7);
            if (codeEnd > codeIdx + 7 && int.TryParse(json.Substring(codeIdx + 7, codeEnd - codeIdx - 7).Trim(), out int c) && c != 0)
                return;
        }
        accessToken = ParseJsonString(json, "access_token");
        refreshToken = ParseJsonString(json, "refresh_token");
        var exp = ParseJsonString(json, "expires_in");
        if (!string.IsNullOrEmpty(exp)) int.TryParse(exp, out expiresIn);
    }

    private static string ParseJsonString(string json, string key)
    {
        var pattern = "\"" + key + "\":";
        var idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx += pattern.Length;
        if (idx < json.Length && json[idx] == '"')
        {
            var start = idx + 1;
            var end = json.IndexOf('"', start);
            if (end > start) return json.Substring(start, end - start);
        }
        return null;
    }

    private void StartOAuthUserTokenFlow()
    {
        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_appSecret))
        {
            _statusText = "错误：请先填写 App ID 和 App Secret。";
            Repaint();
            return;
        }
        _isLoading = true;
        _statusText = "正在获取 app_access_token...";
        Repaint();
        var body = "{\"app_id\":\"" + EscapeJson(_appId) + "\",\"app_secret\":\"" + EscapeJson(_appSecret) + "\"}";
        var req = new UnityWebRequest("https://open.feishu.cn/open-apis/auth/v3/app_access_token/internal", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            string err = null;
            try
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                    json = req.downloadHandler?.text;
                else if (req != null)
                    err = req.error + (req.downloadHandler?.text ?? "");
                req?.Dispose();
            }
            catch (Exception ex) { err = ex.Message; try { req?.Dispose(); } catch { } }
            if (!string.IsNullOrEmpty(err))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：获取 app_access_token 失败 - " + err; Repaint(); });
                return;
            }
            var appToken = ParseJsonString(json, "app_access_token");
            if (string.IsNullOrEmpty(appToken))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：app_access_token 返回为空 - " + json; Repaint(); });
                return;
            }
            var state = Guid.NewGuid().ToString("N").Substring(0, 16);
            var redirectEscaped = Uri.EscapeDataString(OAuthRedirectUri);
            // 请求云文档/电子表格权限；创建导出任务还需 drive:export:readonly、docs:document:export
            var scope = Uri.EscapeDataString("contact:user.base:readonly docx:document:readonly drive:drive:readonly drive:export:readonly docs:document:export sheets:spreadsheet:readonly");
            var authorizeUrl = "https://accounts.feishu.cn/open-apis/authen/v1/authorize?client_id=" + Uri.EscapeDataString(_appId) + "&redirect_uri=" + redirectEscaped + "&state=" + state + "&scope=" + scope;
            _pendingOAuthCode = null;
            _pendingOAuthAppToken = appToken;
            _oauthListenerThread = new Thread(() => RunOAuthCallbackListener(state));
            _oauthListenerThread.IsBackground = true;
            _oauthListenerThread.Start();
            Application.OpenURL(authorizeUrl);
            MainThreadCall(() => { _statusText = "请在浏览器中完成授权，授权成功后会自动关闭并保存 Token。\n若未自动关闭，请将浏览器地址栏中的 code= 后参数粘贴到此处（可选）。"; Repaint(); });
        };
    }

    private void RunOAuthCallbackListener(string expectedState)
    {
        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:" + OAuthCallbackPort + "/");
            listener.Start();
            var ctx = listener.GetContext();
            var req = ctx.Request;
            var code = req.QueryString["code"];
            var state = req.QueryString["state"];
            var response = ctx.Response;
            var html = "<html><head><meta charset=\"utf-8\"/><title>授权结果</title></head><body><p>授权成功，请关闭此页面并回到 Unity。</p></body></html>";
            var buf = Encoding.UTF8.GetBytes(html);
            response.ContentLength64 = buf.Length;
            response.ContentType = "text/html; charset=utf-8";
            response.OutputStream.Write(buf, 0, buf.Length);
            response.OutputStream.Close();
            listener.Stop();
            if (!string.IsNullOrEmpty(code) && (string.IsNullOrEmpty(expectedState) || state == expectedState))
            {
                _pendingOAuthCode = code;
                EditorApplication.delayCall += ExchangeOAuthCodeForToken;
            }
        }
        catch (Exception ex)
        {
            MainThreadCall(() => { _isLoading = false; _statusText = "OAuth 回调异常: " + ex.Message; Repaint(); });
        }
    }

    private void ExchangeOAuthCodeForToken()
    {
        var code = _pendingOAuthCode;
        var appToken = _pendingOAuthAppToken;
        _pendingOAuthCode = null;
        _pendingOAuthAppToken = null;
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(appToken))
        {
            _isLoading = false;
            Repaint();
            return;
        }
        _statusText = "正在用授权码换取 User Access Token...";
        Repaint();
        var body = "{\"grant_type\":\"authorization_code\",\"code\":\"" + EscapeJson(code) + "\"}";
        var req = new UnityWebRequest("https://open.feishu.cn/open-apis/authen/v1/access_token", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.SetRequestHeader("Authorization", "Bearer " + appToken);
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            string err = null;
            try
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                    json = req.downloadHandler?.text;
                else if (req != null)
                    err = req.error + (req.downloadHandler?.text ?? "");
                req?.Dispose();
            }
            catch (Exception ex) { err = ex.Message; try { req?.Dispose(); } catch { } }
            if (!string.IsNullOrEmpty(err))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：换取 user_access_token 失败 - " + err; Repaint(); });
                return;
            }
            string accessToken, refreshToken;
            int expiresIn;
            ParseUserTokenResponse(json, out accessToken, out refreshToken, out expiresIn);
            if (string.IsNullOrEmpty(accessToken))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：无法解析 user_access_token - " + json; Repaint(); });
                return;
            }
            _userAccessTokenOptional = accessToken;
            if (!string.IsNullOrEmpty(refreshToken)) _userRefreshToken = refreshToken;
            if (expiresIn > 0) _userTokenExpireTicks = DateTime.UtcNow.Ticks + TimeSpan.FromSeconds(expiresIn).Ticks;
            MainThreadCall(() => { SaveSettings(); _isLoading = false; _statusText = "已通过浏览器授权并保存 User Access Token，可直接拉取表格。"; Repaint(); });
        };
    }

    private void SheetExportFlow(string tokenForSheetAndDrive)
    {
        tokenForSheetAndDrive = tokenForSheetAndDrive != null ? tokenForSheetAndDrive.Trim() : "";
        if (string.IsNullOrEmpty(tokenForSheetAndDrive))
        {
            MainThreadCall(() => { _isLoading = false; _statusText = "错误：未获取到 access token（请确认 App ID / Secret 正确，或填写 User Access Token）。"; Repaint(); });
            return;
        }
        _exportToken = tokenForSheetAndDrive;
        if (!string.IsNullOrEmpty(_sheetIdForExport))
        {
            CreateSheetExportTask(_sheetIdForExport);
            return;
        }
        MainThreadCall(() => { _statusText = "正在获取表格工作表信息..."; Repaint(); });
        // 官方示例中 spreadsheetToken 直接放在路径中，不进行 URL 编码
        var metainfoUrl = "https://open.feishu.cn/open-apis/sheets/v2/spreadsheets/" + _documentId + "/metainfo";
        var req = new UnityWebRequest(metainfoUrl, "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + tokenForSheetAndDrive.Trim());
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            string err = null;
            try
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                    json = req.downloadHandler?.text;
                else if (req != null)
                {
                    err = req.error ?? "";
                    // HTTP 4xx/5xx 时 Unity 的 error 多为 "Bad Request" 等，读取响应体以显示飞书错误码与说明
                    string body = req.downloadHandler?.text;
                    if (!string.IsNullOrEmpty(body))
                        err = err + " " + body;
                }
                req?.Dispose();
            }
            catch (Exception ex) { err = ex.Message; try { req?.Dispose(); } catch { } }
            if (!string.IsNullOrEmpty(err))
            {
                string msg = "错误：获取表格元数据失败 - " + err;
                if (err.IndexOf("99991668", StringComparison.Ordinal) >= 0)
                    msg += "\n\n若表格在个人空间或未授权给应用，请填写上方的「User Access Token」后重试（开放平台 → 接口调试 → 获取 user_access_token）。" +
                        "\n若已添加文档应用仍报错，请确认：(1) 应用权限「查看、评论、编辑和管理电子表格」已开通 (2) 在表格页面右上角「···」→「添加文档应用」已添加本应用 (3) 等待 1～2 分钟后重试 (4) 或改用上方的 User Access Token 用当前用户身份访问。";
                if (err.IndexOf("99991679", StringComparison.Ordinal) >= 0)
                    msg += "\n\n当前 User Token 无权限访问该表格（Unauthorized）。请点击「通过浏览器授权并自动获取 User Token」重新授权（会请求云文档/表格权限），且授权时使用的飞书账号需能访问该表格。";
                MainThreadCall(() => { _isLoading = false; _statusText = msg; Repaint(); });
                return;
            }
            string sheetId = ParseFirstSheetIdFromMetainfo(json);
            if (string.IsNullOrEmpty(sheetId))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：无法解析工作表 ID。"; Repaint(); });
                return;
            }
            CreateSheetExportTask(sheetId);
        };
    }

    private static string ParseFirstSheetIdFromMetainfo(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        var idx = json.IndexOf("\"sheets\":", StringComparison.Ordinal);
        if (idx < 0) return null;
        idx = json.IndexOf("\"sheetId\":", idx, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx += 10;
        var end = json.IndexOf('"', idx + 1);
        if (end <= idx) return null;
        return json.Substring(idx, end - idx).Trim('"');
    }

    private void CreateSheetExportTask(string sheetId)
    {
        MainThreadCall(() => { _statusText = "正在创建导出任务..."; Repaint(); });
        var body = "{\"file_extension\":\"csv\",\"token\":\"" + EscapeJson(_documentId) + "\",\"type\":\"sheet\",\"sub_id\":\"" + EscapeJson(sheetId) + "\"}";
        var req = new UnityWebRequest("https://open.feishu.cn/open-apis/drive/v1/export_tasks", "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        req.SetRequestHeader("Authorization", "Bearer " + _exportToken);
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            string err = null;
            try
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                    json = req.downloadHandler?.text;
                else if (req != null)
                    err = req.error + (req.downloadHandler?.text ?? "");
                req?.Dispose();
            }
            catch (Exception ex) { err = ex.Message; try { req?.Dispose(); } catch { } }
            if (!string.IsNullOrEmpty(err))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：创建导出任务失败 - " + err; Repaint(); });
                return;
            }
            string ticket = null;
            int code = -1;
            ParseExportTicketResponse(json, out code, out ticket);
            if (code != 0 || string.IsNullOrEmpty(ticket))
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：导出任务返回 - " + json; Repaint(); });
                return;
            }
            _exportTicket = ticket;
            _exportPollCount = 0;
            _exportPollStartUtc = DateTime.UtcNow;
            EditorApplication.delayCall += PollExportTask;
        };
    }

    private static void ParseExportTicketResponse(string json, out int code, out string ticket)
    {
        code = -1;
        ticket = null;
        if (string.IsNullOrEmpty(json)) return;
        var ci = json.IndexOf("\"code\":", StringComparison.Ordinal);
        if (ci >= 0) { var s = ci + 7; var e = json.IndexOf(',', s); code = int.Parse(json.Substring(s, (e < 0 ? json.Length : e) - s).Trim()); }
        var ti = json.IndexOf("\"ticket\":\"", StringComparison.Ordinal);
        if (ti >= 0) { var s = ti + 10; var e = json.IndexOf('"', s); if (e > s) ticket = json.Substring(s, e - s); }
    }

    private void PollExportTask()
    {
        EditorApplication.delayCall -= PollExportTask;
        EditorApplication.update -= ExportPollUpdate;
        _exportPollCount++;
        var elapsedSec = (int)(DateTime.UtcNow - _exportPollStartUtc).TotalSeconds;
        if (elapsedSec >= ExportTimeoutSeconds) { MainThreadCall(() => { _isLoading = false; _statusText = "错误：导出任务超时（已等待 " + elapsedSec + " 秒，可稍后重试或检查表格是否过大）。"; Repaint(); }); return; }
        var url = "https://open.feishu.cn/open-apis/drive/v1/export_tasks/" + _exportTicket + "?token=" + Uri.EscapeDataString(_documentId);
        var req = new UnityWebRequest(url, "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + _exportToken);
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            try { if (req != null) json = req.downloadHandler?.text; req?.Dispose(); } catch { req?.Dispose(); }
            int jobStatus = -1;
            string fileToken = null;
            ParseExportResult(json, out jobStatus, out fileToken);
            // 飞书文档：0=成功 1=初始化 2=处理中 3+=错误(如3内部错误,107过大,108超时等)
            if (jobStatus == 0 && !string.IsNullOrEmpty(fileToken))
            {
                DownloadExportFile(fileToken);
                return;
            }
            if (jobStatus >= 3)
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：导出任务失败 (job_status=" + jobStatus + ") - " + (json ?? ""); Repaint(); });
                return;
            }
            var sec = (int)(DateTime.UtcNow - _exportPollStartUtc).TotalSeconds;
            var statusTip = jobStatus >= 0 ? "状态:" + jobStatus : "解析异常，请复制下方响应反馈。响应: " + (string.IsNullOrEmpty(json) ? "(空)" : (json.Length > 300 ? json.Substring(0, 300) + "..." : json));
            MainThreadCall(() => { _statusText = "正在导出表格... " + sec + "s (" + statusTip + ")"; Repaint(); });
            _exportPollNextUtc = DateTime.UtcNow.AddSeconds(ExportPollIntervalSeconds);
            EditorApplication.update += ExportPollUpdate;
        };
    }

    private void ExportPollUpdate()
    {
        if (DateTime.UtcNow < _exportPollNextUtc) return;
        EditorApplication.update -= ExportPollUpdate;
        PollExportTask();
    }

    private static void ParseExportResult(string json, out int jobStatus, out string fileToken)
    {
        jobStatus = -1;
        fileToken = null;
        if (string.IsNullOrEmpty(json)) return;
        // 格式1: data.result.job_status (0=成功 1=初始化 2=处理中 3+=错误)
        var ji = json.IndexOf("\"job_status\":", StringComparison.Ordinal);
        if (ji >= 0)
        {
            var s = ji + 14;
            var e = json.IndexOf(',', s);
            if (e < 0) e = json.IndexOf('}', s);
            if (e < 0) e = json.Length;
            var num = json.Substring(s, e - s).Trim();
            int.TryParse(num, out jobStatus);
        }
        // 格式2: data.status (1=处理中 2=失败 3=完成) 部分接口返回此格式
        if (jobStatus < 0)
        {
            var si = json.IndexOf("\"status\":", StringComparison.Ordinal);
            if (si >= 0)
            {
                var s = si + 9;
                var e = json.IndexOf(',', s);
                if (e < 0) e = json.IndexOf('}', s);
                if (e < 0) e = json.Length;
                var num = json.Substring(s, e - s).Trim();
                int sval;
                if (int.TryParse(num, out sval))
                {
                    if (sval == 3) jobStatus = 0;      // 完成 -> 成功
                    else if (sval == 2) jobStatus = 3; // 失败
                    else if (sval == 1) jobStatus = 2; // 处理中
                }
            }
        }
        var fi = json.IndexOf("\"file_token\":", StringComparison.Ordinal);
        if (fi >= 0)
        {
            var vStart = fi + 13;
            while (vStart < json.Length && (json[vStart] == ' ' || json[vStart] == ':')) vStart++;
            if (vStart < json.Length && json[vStart] == '"')
            {
                var start = vStart + 1;
                var end = json.IndexOf('"', start);
                if (end > start) fileToken = json.Substring(start, end - start);
            }
        }
    }

    private void DownloadExportFile(string fileToken)
    {
        MainThreadCall(() => { _statusText = "正在下载 CSV..."; Repaint(); });
        var req = new UnityWebRequest("https://open.feishu.cn/open-apis/drive/v1/export_tasks/file/" + fileToken + "/download", "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + _exportToken);
        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string err = null;
            byte[] data = null;
            try
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                    data = req.downloadHandler?.data;
                else if (req != null)
                    err = req.error;
                req?.Dispose();
            }
            catch (Exception ex) { err = ex.Message; try { req?.Dispose(); } catch { } }
            if (!string.IsNullOrEmpty(err) || data == null || data.Length == 0)
            {
                MainThreadCall(() => { _isLoading = false; _statusText = "错误：下载失败 - " + (err ?? "无数据"); Repaint(); });
                return;
            }
            var content = Encoding.UTF8.GetString(data);
            SaveAsCsv(content);
        };
    }

    private static void ParseTokenResponse(string json, out int code, out string token)
    {
        code = -1;
        token = null;
        try
        {
            var i = json.IndexOf("\"code\":", StringComparison.Ordinal);
            if (i >= 0)
            {
                var start = i + 7;
                var end = json.IndexOf(',', start);
                if (end < 0) end = json.Length;
                code = int.Parse(json.Substring(start, end - start).Trim());
            }

            var ti = json.IndexOf("\"tenant_access_token\":\"", StringComparison.Ordinal);
            if (ti >= 0)
            {
                var start = ti + 24;
                var end = json.IndexOf('"', start);
                if (end > start)
                    token = json.Substring(start, end - start);
            }
        }
        catch { /* ignore */ }
    }

    private void PullRawContent(string tenantAccessToken)
    {
        MainThreadCall(() => { _statusText = "正在拉取文档内容..."; Repaint(); });

        // document_id 仅允许字母数字与 -_，不进行 URL 编码避免 400
        string docId = _documentId;
        var safe = new System.Text.StringBuilder();
        foreach (char c in docId)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_') safe.Append(c);
        }
        if (safe.Length == 0) docId = _documentId;
        else docId = safe.ToString();

        string url;
        if (_useDocxApi)
            url = "https://open.feishu.cn/open-apis/docx/v1/documents/" + docId + "/raw_content";
        else
            url = "https://open.feishu.cn/open-apis/doc/v2/" + docId + "/raw_content";

        var req = new UnityWebRequest(url, "GET");
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", "Bearer " + tenantAccessToken);

        var op = req.SendWebRequest();
        op.completed += _ =>
        {
            string json = null;
            string errorMsg = null;
            bool success = false;
            try
            {
                success = req != null && req.result == UnityWebRequest.Result.Success;
                if (req != null)
                {
                    if (success)
                        json = req.downloadHandler?.text;
                    else
                    {
                        errorMsg = req.error ?? "Unknown error";
                        var body = req.downloadHandler?.text;
                        if (!string.IsNullOrEmpty(body))
                            errorMsg = errorMsg + "\n响应: " + (body.Length > 200 ? body.Substring(0, 200) + "..." : body);
                    }
                    req.Dispose();
                    req = null;
                }
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                try { req?.Dispose(); } catch { /* ignore */ }
                req = null;
            }

            if (!success)
            {
                var msg = "错误：拉取文档失败 - " + (errorMsg ?? "未知错误");
                if (errorMsg != null && errorMsg.IndexOf("400", StringComparison.OrdinalIgnoreCase) >= 0)
                    msg += "\n\n若为 400：请确认文档类型并切换「使用新版文档接口」——新版文档(docx)勾选，旧版文档(doc)不勾选。";
                var finalMsg = msg;
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = finalMsg;
                    Repaint();
                });
                return;
            }

            if (string.IsNullOrEmpty(json))
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：文档内容为空。";
                    Repaint();
                });
                return;
            }

            string content;
            int code;
            // 旧版 doc v2 有时直接返回纯文本，新版 docx 返回 JSON
            if (json.TrimStart().StartsWith("{"))
            {
                ParseRawContentResponse(json, out code, out content);
            }
            else
            {
                code = 0;
                content = json;
            }

            if (code != 0)
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：文档接口返回异常 - " + json;
                    Repaint();
                });
                return;
            }

            if (string.IsNullOrEmpty(content))
            {
                MainThreadCall(() =>
                {
                    _isLoading = false;
                    _statusText = "错误：文档内容为空或解析失败。若为新版文档请勾选「使用新版文档接口」；若为旧版请取消勾选。";
                    Repaint();
                });
                return;
            }

            SaveAsCsv(content);
        };
    }

    private static void ParseRawContentResponse(string json, out int code, out string content)
    {
        code = -1;
        content = null;
        try
        {
            var ci = json.IndexOf("\"code\":", StringComparison.Ordinal);
            if (ci >= 0)
            {
                var start = ci + 7;
                var end = json.IndexOf(',', start);
                if (end < 0) end = json.Length;
                code = int.Parse(json.Substring(start, end - start).Trim());
            }

            // 新版 docx: "data": { "content": "..." }
            var dataContent = json.IndexOf("\"content\":\"", StringComparison.Ordinal);
            if (dataContent >= 0)
            {
                var start = dataContent + 10;
                content = UnescapeJsonString(json, start);
                return;
            }

            // 有的接口可能 content 在 data 下且为多行，尝试 "content": " 形式
            var alt = json.IndexOf("\"content\": \"", StringComparison.Ordinal);
            if (alt >= 0)
            {
                var start = alt + 12;
                content = UnescapeJsonString(json, start);
            }
        }
        catch { /* ignore */ }
    }

    private static string UnescapeJsonString(string json, int startIndex)
    {
        var sb = new StringBuilder();
        var i = startIndex;
        while (i < json.Length)
        {
            var c = json[i];
            if (c == '"' && (i == startIndex || json[i - 1] != '\\'))
                break;
            if (c == '\\' && i + 1 < json.Length)
            {
                var next = json[i + 1];
                if (next == 'n') { sb.Append('\n'); i += 2; continue; }
                if (next == 'r') { sb.Append('\r'); i += 2; continue; }
                if (next == 't') { sb.Append('\t'); i += 2; continue; }
                if (next == '"') { sb.Append('"'); i += 2; continue; }
                if (next == '\\') { sb.Append('\\'); i += 2; continue; }
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>解析 CSV 内容为行列表（支持引号内逗号、换行、双引号转义）</summary>
    private static List<List<string>> ParseCsvToRows(string content)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var current = new StringBuilder();
        bool inQuoted = false;
        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (inQuoted)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuoted = false;
                }
                else
                    current.Append(c);
            }
            else
            {
                if (c == '"') inQuoted = true;
                else if (c == ',') { currentRow.Add(current.ToString()); current.Clear(); }
                else if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') { currentRow.Add(current.ToString()); current.Clear(); rows.Add(currentRow); currentRow = new List<string>(); i++; }
                else if (c == '\r' || c == '\n') { currentRow.Add(current.ToString()); current.Clear(); rows.Add(currentRow); currentRow = new List<string>(); }
                else current.Append(c);
            }
        }
        currentRow.Add(current.ToString());
        rows.Add(currentRow);
        return rows;
    }

    private static string SerializeCsvRow(List<string> row)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < row.Count; i++)
        {
            if (i > 0) sb.Append(',');
            string s = row[i] ?? "";
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0) { sb.Append('"'); sb.Append(s.Replace("\"", "\"\"")); sb.Append('"'); }
            else sb.Append(s);
        }
        return sb.ToString();
    }

    /// <summary>按表头名移除整列，返回新 CSV 内容。</summary>
    private static string RemoveCsvColumnsByHeader(string content, string[] excludeHeaderNames)
    {
        var rows = ParseCsvToRows(content);
        if (rows.Count == 0) return content;
        var header = rows[0];
        var excludeIndices = new HashSet<int>();
        for (int i = 0; i < header.Count; i++)
        {
            string h = (header[i] ?? "").Trim();
            foreach (var ex in excludeHeaderNames)
                if (string.Equals(h, ex, StringComparison.OrdinalIgnoreCase)) { excludeIndices.Add(i); break; }
        }
        if (excludeIndices.Count == 0) return content;
        var sb = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var newRow = new List<string>();
            for (int c = 0; c < row.Count; c++)
                if (!excludeIndices.Contains(c)) newRow.Add(row[c]);
            if (r > 0) sb.Append('\n');
            sb.Append(SerializeCsvRow(newRow));
        }
        return sb.ToString();
    }

    private void SaveAsCsv(string content)
    {
        if (string.IsNullOrEmpty(content)) return;
        // 去掉开头的 BOM 字符（U+FEFF），避免第一格前出现多余空白
        if (content[0] == '\uFEFF')
            content = content.Substring(1);
        if (_sourceType == SourceType.Document && _tabToComma)
        {
            content = content.Replace("\t", ",");
        }
        if (_sourceType == SourceType.Sheet && !string.IsNullOrWhiteSpace(_excludeColumns))
        {
            var excludeNames = _excludeColumns.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < excludeNames.Length; i++) excludeNames[i] = excludeNames[i].Trim();
            if (excludeNames.Length > 0)
                content = RemoveCsvColumnsByHeader(content, excludeNames);
        }

        var utf8NoBom = new UTF8Encoding(false);
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var fullPath = Path.Combine(projectRoot ?? "", OutputRelativePath);

        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(fullPath, content, utf8NoBom);
            AssetDatabase.Refresh();

            var pathForDialog = fullPath;
            MainThreadCall(() =>
            {
                _isLoading = false;
                _statusText = "已覆盖保存为 UTF-8 CSV：\n" + fullPath;
                Repaint();
                // 再延迟一帧弹出对话框，确保在主线程且窗口就绪
                EditorApplication.delayCall += () => AskAndImportI2LanguagesFromCsv(pathForDialog);
            });
        }
        catch (Exception e)
        {
            MainThreadCall(() =>
            {
                _isLoading = false;
                _statusText = "错误：写入文件失败 - " + e.Message;
                Repaint();
            });
        }
    }

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    /// <summary>弹出对话框询问是否将 CSV 导入到 I2Languages 并替换；若用户确认则执行 I2 的 Import+Replace。</summary>
    private static void AskAndImportI2LanguagesFromCsv(string csvFullPath)
    {
        if (string.IsNullOrEmpty(csvFullPath))
            csvFullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", OutputRelativePath);
        if (!File.Exists(csvFullPath))
        {
            EditorUtility.DisplayDialog("导入 I2Languages", "未找到刚保存的 CSV 文件，无法导入：\n" + csvFullPath, "OK");
            return;
        }
        bool doImport = EditorUtility.DisplayDialog(
            "导入 I2Languages",
            "CSV 已保存。是否导入到 I2Languages 并替换？\n\n（将读取 " + OutputRelativePath + " 并执行 I2 的 Import → Replace）",
            "导入并替换",
            "跳过");
        if (!doImport)
            return;
        try
        {
            const string assetPath = "Assets/Resources/I2Languages.asset";
            var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
            if (asset == null)
            {
                EditorUtility.DisplayDialog("导入失败", "未找到 I2Languages.asset：\n" + assetPath, "OK");
                return;
            }
            Type assetType = asset.GetType();
            object source = null;
            var prop = assetType.GetProperty("mSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? assetType.GetProperty("Source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? assetType.GetProperty("source", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
                source = prop.GetValue(asset);
            if (source == null)
            {
                var field = assetType.GetField("mSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    source = field.GetValue(asset);
            }
            if (source == null)
            {
                EditorUtility.DisplayDialog("导入失败", "I2Languages.asset 未找到 mSource/Source 属性或字段（当前类型: " + assetType.Name + "）。请手动在 I2 面板中导入 CSV。", "OK");
                return;
            }
            string csvContent = File.ReadAllText(csvFullPath, Encoding.UTF8);
            Type sourceType = source.GetType();
            MethodInfo importCsv = null;
            foreach (var m in sourceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!m.Name.Contains("Import") || !m.Name.Contains("CSV")) continue;
                var ps = m.GetParameters();
                if (ps.Length >= 2 && ps[1].ParameterType == typeof(string))
                {
                    importCsv = m;
                    break;
                }
            }
            if (importCsv == null)
            {
                EditorUtility.DisplayDialog("导入失败", "未在 I2 LanguageSource 中找到 Import CSV 方法。请手动在 I2 面板中导入。", "OK");
                return;
            }
            var paramList = importCsv.GetParameters();
            object[] args;
            if (paramList.Length >= 3 && paramList[2].ParameterType.IsEnum)
            {
                Type enumType = paramList[2].ParameterType;
                object replaceMode = null;
                foreach (var n in enumType.GetEnumNames())
                {
                    if (n.IndexOf("Replace", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        replaceMode = Enum.Parse(enumType, n);
                        break;
                    }
                }
                if (replaceMode == null)
                    replaceMode = Enum.ToObject(enumType, 1);
                if (paramList.Length >= 4)
                    args = new object[] { "", csvContent, replaceMode, ',' };
                else
                    args = new object[] { "", csvContent, replaceMode };
            }
            else if (paramList.Length >= 2)
                args = new object[] { "", csvContent };
            else
                args = new object[] { csvContent };
            importCsv.Invoke(source, args);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("导入完成", "已导入到 I2Languages 并替换。", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("导入失败", ex.Message + "\n\n" + ex.StackTrace, "OK");
        }
    }

    private static void MainThreadCall(Action a)
    {
        EditorApplication.delayCall += () => a();
    }
}
#endif
