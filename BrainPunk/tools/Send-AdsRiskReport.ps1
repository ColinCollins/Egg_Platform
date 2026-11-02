<#
.SYNOPSIS
    Send module risk assessment report to Feishu (interactive card format).
    P0 and P1 risks are sent as separate messages. Supports ignore-risk filtering.
.DESCRIPTION
    Reads a markdown risk report and ignore-risk.json, filters out ignored risks,
    then sends two interactive cards: one for P0 risks (red header) and one for P1 risks (orange header).
.PARAMETER WebhookUrl
    Feishu webhook URL. Falls back to FEISHU_WEBHOOK_URL env var.
.PARAMETER Secret
    Feishu webhook secret. Falls back to FEISHU_WEBHOOK_SECRET env var.
.PARAMETER ReportPath
    Path to the markdown risk report. Defaults to ads-module-risk-report.md.
.PARAMETER ModuleName
    Module name used in card titles, e.g. "Account/Analytics/Config模块".
#>
param(
    [string]$WebhookUrl = $env:FEISHU_WEBHOOK_URL,
    [string]$Secret = $env:FEISHU_WEBHOOK_SECRET,
    [string]$ReportPath = "",
    [string]$ModuleName = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $reportPath = Join-Path $projectRoot ".codex-skills\module-risk-assessment\references\ads-module-risk-report.md"
    if (-not (Test-Path -LiteralPath $reportPath)) {
        $reportPath = Join-Path $env:USERPROFILE ".codex\skills\module-risk-assessment\references\ads-module-risk-report.md"
    }
} else {
    $reportPath = $ReportPath
}

if (-not (Test-Path -LiteralPath $reportPath)) {
    throw "Risk assessment report not found. Path: $reportPath"
}

# Load ignore-risk config
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
    return $risks
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
    return $filtered
}

function Build-Elements {
    param([System.Collections.Generic.List[hashtable]]$Risks)

    $elements = New-Object System.Collections.Generic.List[hashtable]
    $first = $true

    foreach ($risk in $Risks) {
        if (-not $first) {
            $elements.Add(@{
                tag = "div"
                text = @{ tag = "lark_md"; content = "--------------------------------" }
            })
        }
        $first = $false

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

    $body = $payload | ConvertTo-Json -Depth 10 -Compress
    $utf8Body = [System.Text.Encoding]::UTF8.GetBytes($body)

    Write-Host "Sending card: $HeaderTitle"
    Invoke-RestMethod -Method Post -Uri $Webhook -ContentType "application/json; charset=utf-8" -Body $utf8Body
}

# Read and parse report
$lines = Get-Content -LiteralPath $reportPath -Encoding UTF8
$allP0 = Parse-Risks -Lines $lines -Prefix "P0"
$allP1 = Parse-Risks -Lines $lines -Prefix "P1"

# Filter ignored
$p0Risks = Filter-Ignored -Risks $allP0 -Label "P0"
$p1Risks = Filter-Ignored -Risks $allP1 -Label "P1"

# Determine titles
if ($ModuleName) {
    $p0Title = "🔴 BrainPunk $($ModuleName)风险评估 — P0 风险"
    $p1Title = "🟠 BrainPunk $($ModuleName)风险评估 — P1 风险"
} else {
    $p0Title = "🔴 BrainPunk 广告模块风险评估 — P0 风险"
    $p1Title = "🟠 BrainPunk 广告模块风险评估 — P1 风险"
}

# Send P0 card
$p0Elements = Build-Elements -Risks $p0Risks
Send-Card -Webhook $WebhookUrl -SigningSecret $Secret `
    -HeaderTitle $p0Title `
    -Template "red" -Elements $p0Elements

# Send P1 card
$p1Elements = Build-Elements -Risks $p1Risks
Send-Card -Webhook $WebhookUrl -SigningSecret $Secret `
    -HeaderTitle $p1Title `
    -Template "orange" -Elements $p1Elements
