param(
  [string]$ReportPath,

  [string]$ModuleName = "每日扫描",

  [switch]$SkipGitUpload = $true,

  [string]$GitCommitMessage = "每日风险报告",

  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
[Net.ServicePointManager]::Expect100Continue = $false

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls13
} catch {
    # PowerShell 5.1 on older .NET builds may not expose Tls13. Tls12 is enough for Feishu.
}

function Join-Chars {
  param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [int[]]$Codes
  )

  return -join ($Codes | ForEach-Object { [char]$_ })
}

function Get-DefaultReportDirectory {
  $reportFolderName = Join-Chars 0x98CE 0x9669 0x8BC4 0x4F30 0x62A5 0x544A

  if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    return Join-Path (Split-Path -Parent $PSScriptRoot) $reportFolderName
  }

  return Join-Path (Get-Location) $reportFolderName
}

function Resolve-LatestMarkdownReport {
  param([string]$DirectoryPath)

  $resolvedDirectory = (Resolve-Path -LiteralPath $DirectoryPath).Path
  $latestReport = Get-ChildItem -LiteralPath $resolvedDirectory -File -Filter "*.md" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

  if ($null -eq $latestReport) {
    throw "No markdown report found in $resolvedDirectory."
  }

  return $latestReport.FullName
}

function Resolve-ReportPath {
  param([string]$Path)

  if ([string]::IsNullOrWhiteSpace($Path)) {
    return Resolve-LatestMarkdownReport -DirectoryPath (Get-DefaultReportDirectory)
  }

  $candidate = $Path
  if (-not [System.IO.Path]::IsPathRooted($candidate)) {
    $candidate = Join-Path (Get-Location) $candidate
  }

  if (Test-Path -LiteralPath $candidate -PathType Container) {
    return Resolve-LatestMarkdownReport -DirectoryPath $candidate
  }

  return (Resolve-Path -LiteralPath $candidate).Path
}

# Load ignore-risk config
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ignorePath = Join-Path $scriptDir "ignore-risk.json"
$ignoredIds = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
if (Test-Path -LiteralPath $ignorePath) {
    try {
        $config = Get-Content -LiteralPath $ignorePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($config.ignored) {
            foreach ($id in $config.ignored) {
                if (-not [string]::IsNullOrWhiteSpace($id)) {
                    $ignoredIds.Add($id.Trim()) | Out-Null
                }
            }
        }
        Write-Host "Loaded ignore-risk.json: $($ignoredIds.Count) item(s) configured"
    } catch {
        Write-Warning "Failed to parse ignore-risk.json: $_"
    }
} else {
    Write-Host "ignore-risk.json not found, no risks will be filtered"
}

function Parse-Risks {
    param([string[]]$Lines, [string]$Prefix)

    $risks = New-Object System.Collections.Generic.List[hashtable]
    $current = $null

    foreach ($raw in $Lines) {
        $line = $raw.TrimEnd()
        $trimmed = $line.Trim()

        if ($trimmed -match ("^### " + $Prefix + "-(\d+)\s+(.+)")) {
            if ($current) { $risks.Add($current) }
            $current = @{
                id = "$Prefix-$($Matches[1])"
                title = $Matches[2]
                problem = ""
                files = ""
                advice = ""
            }
            continue
        }

        # 进入其它标题时结束当前条目，避免下一风险等级的字段覆盖本条正文。
        if ($trimmed -match '^#{1,6}\s+') {
            if ($current) {
                $risks.Add($current)
                $current = $null
            }
            continue
        }

        if (-not $current) { continue }

        if ($trimmed -match '^-\s*\*问题\*：\s*(.+)') {
            $current.problem = $Matches[1] -replace '`', ''
        }
        elseif ($trimmed -match '^-\s*\*影响文件\*：\s*(.+)') {
            $current.files = $Matches[1] -replace '`', ''
        }
        elseif ($trimmed -match '^-\s*\*建议\*：\s*(.+)') {
            $current.advice = $Matches[1] -replace '`', ''
        }
    }

    if ($current) { $risks.Add($current) }
    return ,$risks
}

function Filter-Ignored {
    param(
        [System.Collections.Generic.List[hashtable]]$Risks,
        [string]$Label
    )

    $filtered = New-Object System.Collections.Generic.List[hashtable]
    $skipped = New-Object System.Collections.Generic.List[string]

    foreach ($risk in $Risks) {
        if ($ignoredIds.Contains($risk.id)) {
            $skipped.Add($risk.id)
        } else {
            $filtered.Add($risk)
        }
    }

    if ($skipped.Count -gt 0) {
        Write-Host "[$Label] Filtered out ignored risks: $($skipped -join ', ')"
    }
    Write-Host "[$Label] Sending $($filtered.Count) risk(s), ignored $($skipped.Count)"
    return ,$filtered
}

function Build-Elements {
    param([System.Collections.Generic.List[hashtable]]$Risks)

    $elements = New-Object System.Collections.Generic.List[hashtable]

    foreach ($risk in $Risks) {
        $content = "**$($risk.id) $($risk.title)**`n"
        if ($risk.problem) { $content += "• 问题：$($risk.problem)`n" }
        if ($risk.files) { $content += "• 影响文件：$($risk.files)`n" }
        if ($risk.advice) { $content += "• 建议：$($risk.advice)" }

        $elements.Add(@{
            tag = "div"
            text = @{
                tag = "lark_md"
                content = $content.TrimEnd("`n")
            }
        })
    }

    return $elements.ToArray()
}

function ConvertTo-CompactJson {
    param([object]$Value)

    return $Value | ConvertTo-Json -Depth 20 -Compress
}

function Test-FeishuResponse {
    param(
        [string]$ResponseText,
        [string]$HeaderTitle
    )

    if ([string]::IsNullOrWhiteSpace($ResponseText)) {
        throw "Feishu returned an empty response for card: $HeaderTitle"
    }

    try {
        $response = $ResponseText | ConvertFrom-Json
    }
    catch {
        throw "Feishu returned non-JSON response for card: $HeaderTitle`n$ResponseText"
    }

    if ($null -ne $response.code -and [int]$response.code -ne 0) {
        throw "Feishu rejected card '$HeaderTitle'. code=$($response.code), msg=$($response.msg)"
    }

    if ($null -ne $response.StatusCode -and [int]$response.StatusCode -ne 0) {
        throw "Feishu rejected card '$HeaderTitle'. StatusCode=$($response.StatusCode), StatusMessage=$($response.StatusMessage)"
    }
}

function Invoke-FeishuWebhook {
    param(
        [string]$Webhook,
        [string]$JsonBody,
        [string]$HeaderTitle,
        [int]$MaxAttempts = 3
    )

    Add-Type -AssemblyName System.Net.Http

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip -bor [System.Net.DecompressionMethods]::Deflate
    $handler.UseProxy = $true
    $handler.Proxy = [System.Net.WebRequest]::DefaultWebProxy
    if ($null -ne $handler.Proxy) {
        $handler.Proxy.Credentials = [System.Net.CredentialCache]::DefaultCredentials
    }

    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(30)

    try {
        for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
            $content = [System.Net.Http.ByteArrayContent]::new([System.Text.Encoding]::UTF8.GetBytes($JsonBody))
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/json; charset=utf-8")

            try {
                $response = $client.PostAsync($Webhook, $content).GetAwaiter().GetResult()
                $responseText = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

                if (-not $response.IsSuccessStatusCode) {
                    throw "HTTP $([int]$response.StatusCode) $($response.ReasonPhrase)`n$responseText"
                }

                Test-FeishuResponse -ResponseText $responseText -HeaderTitle $HeaderTitle
                Write-Host "Sent card successfully: $HeaderTitle"
                return
            }
            catch {
                $message = $_.Exception.Message
                if ($attempt -ge $MaxAttempts) {
                    throw "Failed to send card '$HeaderTitle' after $MaxAttempts attempt(s). Last error: $message"
                }

                $delaySeconds = [Math]::Min(8, [Math]::Pow(2, $attempt - 1))
                Write-Warning "Send card failed (attempt $attempt/$MaxAttempts): $message. Retry in $delaySeconds second(s)."
                Start-Sleep -Seconds $delaySeconds
            }
            finally {
                if ($null -ne $content) {
                    $content.Dispose()
                }
            }
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Send-Card {
    param(
        [string]$Webhook,
        [string]$SigningSecret,
        [string]$HeaderTitle,
        [string]$Template,
        [array]$Elements
    )

    if ($Elements.Count -eq 0) {
        Write-Host "Skipping card (no elements): $HeaderTitle"
        return
    }

    $payload = @{
        msg_type = "interactive"
        card = @{
            header = @{
                title = @{
                    tag = "plain_text"
                    content = $HeaderTitle
                }
                template = $Template
            }
            elements = $Elements
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($SigningSecret)) {
        $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        $stringToSign = "$timestamp`n$SigningSecret"
        $hmac = New-Object System.Security.Cryptography.HMACSHA256
        $hmac.Key = [Text.Encoding]::UTF8.GetBytes($stringToSign)
        $sign = [Convert]::ToBase64String($hmac.ComputeHash([byte[]]::new(0)))
        $payload.timestamp = "$timestamp"
        $payload.sign = $sign
    }

    $body = ConvertTo-CompactJson -Value $payload

    Write-Host "Sending card: $HeaderTitle"
    Invoke-FeishuWebhook -Webhook $Webhook -JsonBody $body -HeaderTitle $HeaderTitle
}

# Resolve report path
$resolvedReportPath = Resolve-ReportPath -Path $ReportPath

if (-not $DryRun -and -not $SkipGitUpload) {
  $gitUploadScript = Join-Path $scriptDir "Invoke-ReportGitUpload.ps1"
  try {
    & $gitUploadScript -ReportPath $resolvedReportPath -GitCommitMessage $GitCommitMessage
  }
  catch {
    Write-Warning "[GitUpload] Auto git upload failed but report sending will continue. $($_.Exception.Message)"
  }
}

$lines = Get-Content -LiteralPath $resolvedReportPath -Encoding UTF8

# Parse risks
$allP0 = Parse-Risks -Lines $lines -Prefix "P0"
$allP1 = Parse-Risks -Lines $lines -Prefix "P1"

# Filter ignored
$p0Risks = Filter-Ignored -Risks $allP0 -Label "P0"
$p1Risks = Filter-Ignored -Risks $allP1 -Label "P1"

# Build titles
$p0Title = "🔴 BrainPunk $($ModuleName)风险评估 - P0 风险"
$p1Title = "🟠 BrainPunk $($ModuleName)风险评估 - P1 风险"

if ($DryRun) {
    Write-Host "=== DRY RUN ==="
    Write-Host "Report: $resolvedReportPath"
    Write-Host "P0 Risks: $($p0Risks.Count)"
    foreach ($r in $p0Risks) { Write-Host "  $($r.id) $($r.title)" }
    Write-Host "P1 Risks: $($p1Risks.Count)"
    foreach ($r in $p1Risks) { Write-Host "  $($r.id) $($r.title)" }
    Write-Host "==============="
    return
}

$webhook = $env:FEISHU_WEBHOOK_URL
$secret = $env:FEISHU_WEBHOOK_SECRET

if ([string]::IsNullOrWhiteSpace($webhook)) {
    throw "Missing FEISHU_WEBHOOK_URL environment variable."
}

# Send P0 card
$p0Elements = Build-Elements -Risks $p0Risks
Send-Card -Webhook $webhook -SigningSecret $secret `
    -HeaderTitle $p0Title `
    -Template "red" -Elements $p0Elements

# Send P1 card
$p1Elements = Build-Elements -Risks $p1Risks
Send-Card -Webhook $webhook -SigningSecret $secret `
    -HeaderTitle $p1Title `
    -Template "orange" -Elements $p1Elements
