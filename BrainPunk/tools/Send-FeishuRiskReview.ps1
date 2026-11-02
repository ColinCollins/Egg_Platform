param(
  [Parameter(Mandatory = $true)]
  [string]$ReportPath,

  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

#region 报告解析

function Get-SectionItems {
  param(
    [string[]]$Lines,
    [string]$Heading
  )

  $inside = $false
  $items = New-Object System.Collections.Generic.List[string]
  foreach ($line in $Lines) {
    if ($line -eq "## $Heading") {
      $inside = $true
      continue
    }
    if ($inside -and $line -match '^## ') { break }
    if ($inside -and $line -match '^-[ ]+(.+)$') {
      $item = $Matches[1].Trim()
      if ($item -ne '无') { $items.Add($item) | Out-Null }
    }
  }

  return $items.ToArray()
}

function Get-ReportField {
  param(
    [string[]]$Lines,
    [string]$Name
  )

  $prefix = "- ${Name}："
  foreach ($line in $Lines) {
    if ($line.StartsWith($prefix, [StringComparison]::Ordinal)) {
      return $line.Substring($prefix.Length).Trim()
    }
  }
  return ''
}

function Add-SectionSummary {
  param(
    [System.Collections.Generic.List[string]]$Output,
    [string]$Heading,
    [string[]]$Items
  )

  $Output.Add("${Heading}：$($Items.Count)") | Out-Null
  foreach ($item in @($Items | Select-Object -First 6)) {
    $compact = ($item -replace '\s+', ' ').Trim()
    if ($compact.Length -gt 220) { $compact = $compact.Substring(0, 220) + '…' }
    $Output.Add("• $compact") | Out-Null
  }
  if ($Items.Count -gt 6) {
    $Output.Add("• 其余 $($Items.Count - 6) 项见完整报告") | Out-Null
  }
}

#endregion

#region 飞书发送

$resolvedPath = (Resolve-Path -LiteralPath $ReportPath).Path
if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
  throw "复核报告不是文件：$resolvedPath"
}

$lines = @(Get-Content -LiteralPath $resolvedPath -Encoding UTF8)
if ($lines.Count -eq 0 -or $lines[0] -notmatch '^# .+每日风险复核报告$') {
  throw "文件标题不是每日风险复核报告：$resolvedPath"
}

$sourceReport = Get-ReportField -Lines $lines -Name '对照日报'
if ([string]::IsNullOrWhiteSpace($sourceReport)) {
  throw '复核报告缺少“对照日报”字段。'
}

foreach ($heading in @('已消除风险', '仍存在风险', '待验证风险', '新发现风险')) {
  if ($lines -cnotcontains "## $heading") {
    throw ('复核报告缺少 {0} 段落。' -f $heading)
  }
}

$eliminated = @(Get-SectionItems -Lines $lines -Heading '已消除风险')
$remaining = @(Get-SectionItems -Lines $lines -Heading '仍存在风险')
$pending = @(Get-SectionItems -Lines $lines -Heading '待验证风险')
$newRisks = @(Get-SectionItems -Lines $lines -Heading '新发现风险')

$summary = New-Object System.Collections.Generic.List[string]
$summary.Add($lines[0].Substring(2)) | Out-Null
$summary.Add("对照日报：$sourceReport") | Out-Null
$summary.Add("复核报告：$resolvedPath") | Out-Null
Add-SectionSummary -Output $summary -Heading '已消除' -Items $eliminated
Add-SectionSummary -Output $summary -Heading '仍存在' -Items $remaining
Add-SectionSummary -Output $summary -Heading '待验证' -Items $pending
Add-SectionSummary -Output $summary -Heading '新发现' -Items $newRisks
$message = $summary -join "`n"

if ($DryRun) {
  Write-Output $message
  return
}

$webhook = $env:FEISHU_WEBHOOK_URL
if ([string]::IsNullOrWhiteSpace($webhook)) {
  throw '缺少 FEISHU_WEBHOOK_URL 环境变量。'
}

$payload = @{
  msg_type = 'text'
  content = @{ text = $message }
}

$secret = $env:FEISHU_WEBHOOK_SECRET
if (-not [string]::IsNullOrWhiteSpace($secret)) {
  $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
  $hmac = New-Object System.Security.Cryptography.HMACSHA256
  try {
    $hmac.Key = [Text.Encoding]::UTF8.GetBytes("$timestamp`n$secret")
    $payload.timestamp = "$timestamp"
    $payload.sign = [Convert]::ToBase64String($hmac.ComputeHash([byte[]]::new(0)))
  }
  finally {
    $hmac.Dispose()
  }
}

$body = $payload | ConvertTo-Json -Depth 6 -Compress
$response = Invoke-RestMethod -Uri $webhook -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 30
if ($null -ne $response.code -and [int]$response.code -ne 0) {
  throw "飞书拒绝复核报告：code=$($response.code)，msg=$($response.msg)"
}
if ($null -ne $response.StatusCode -and [int]$response.StatusCode -ne 0) {
  throw "飞书拒绝复核报告：StatusCode=$($response.StatusCode)，StatusMessage=$($response.StatusMessage)"
}

Write-Output "飞书复核摘要发送成功：$resolvedPath"

#endregion
