<#
.SYNOPSIS
    Generate a P0 risk guard report from recent git changes.
.DESCRIPTION
    This script is a lightweight sentinel for high-risk infrastructure changes.
    It scans recent commits and the current working tree, then writes a markdown
    report compatible with tools/Send-AdsRiskReport.ps1.

    Primary target: prevent ClassCollector/link.xml/build stripping risks from
    silently entering the daily flow again.
.PARAMETER Days
    Number of recent days to scan from git history. Default: 3.
.PARAMETER ReportDir
    Output directory for the generated markdown report.
.PARAMETER SendFeishu
    When set, send the generated report using Send-AdsRiskReport.ps1.
.PARAMETER WebhookUrl
    Feishu webhook URL. Falls back to FEISHU_WEBHOOK_URL.
.PARAMETER Secret
    Feishu webhook secret. Falls back to FEISHU_WEBHOOK_SECRET.
#>
param(
    [int]$Days = 3,
    [string]$ReportDir = "",
    [switch]$SendFeishu,
    [switch]$SkipGitUpload,
    [string]$GitCommitMessage = "每日风险报告",
    [string]$WebhookUrl = $env:FEISHU_WEBHOOK_URL,
    [string]$Secret = $env:FEISHU_WEBHOOK_SECRET
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

if ([string]::IsNullOrWhiteSpace($ReportDir)) {
    $ReportDir = Join-Path $projectRoot "风险评估报告"
}

if (-not (Test-Path -LiteralPath $ReportDir)) {
    New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
}

function Invoke-GitLines {
    param([string[]]$GitArgs)

    $output = & git -C $projectRoot @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed: $output"
    }
    return @($output)
}

function Get-RelativeProjectPath {
    param([string]$AbsolutePath)

    $resolvedPath = [System.IO.Path]::GetFullPath($AbsolutePath)
    $resolvedRoot = [System.IO.Path]::GetFullPath($projectRoot)

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside project root: $resolvedPath"
    }

    return $resolvedPath.Substring($resolvedRoot.Length).TrimStart('\', '/')
}

function Get-ReportArtifactPaths {
    param([string]$PrimaryReportPath)

    $artifacts = New-Object System.Collections.Generic.List[string]
    $resolvedPrimary = (Resolve-Path -LiteralPath $PrimaryReportPath).Path
    $artifacts.Add($resolvedPrimary) | Out-Null

    $reportDirectory = Split-Path -Parent $resolvedPrimary
    $reportName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedPrimary)

    if ($reportName -match '^(\d{4}-\d{2}-\d{2}-\d{4})-') {
        $batchPrefix = $Matches[1]
        $batchFiles = Get-ChildItem -LiteralPath $reportDirectory -File -Filter "$batchPrefix-*.md" |
            Sort-Object FullName

        foreach ($file in $batchFiles) {
            if (-not $artifacts.Contains($file.FullName)) {
                $artifacts.Add($file.FullName) | Out-Null
            }
        }
    }

    return $artifacts.ToArray()
}

function Invoke-ReportGitUpload {
    param(
        [string[]]$ArtifactPaths,
        [string]$CommitMessage
    )

    $preExistingStaged = @(Invoke-GitLines -GitArgs @("diff", "--cached", "--name-only") | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })

    if ($preExistingStaged.Count -gt 0) {
        Write-Warning "Skip auto git upload because there are already staged changes: $($preExistingStaged -join ', ')"
        return
    }

    $relativePaths = @()
    foreach ($path in $ArtifactPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $relativePaths += Get-RelativeProjectPath -AbsolutePath $path
        }
    }

    if ($relativePaths.Count -eq 0) {
        Write-Host "No report artifacts found for git upload."
        return
    }

    $addOutput = & git -C $projectRoot add -- @relativePaths 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git add failed: $addOutput"
    }

    $stagedReportFiles = @(Invoke-GitLines -GitArgs @("diff", "--cached", "--name-only", "--") + $relativePaths | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })

    if ($stagedReportFiles.Count -eq 0) {
        Write-Host "No report changes detected for auto git upload."
        return
    }

    $commitOutput = & git -C $projectRoot commit -m $CommitMessage 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git commit failed: $commitOutput"
    }
    Write-Host $commitOutput

    $pushOutput = & git -C $projectRoot push 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git push failed: $pushOutput"
    }
    Write-Host $pushOutput
}

function Add-Unique {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($Value) -and -not $List.Contains($Value)) {
        $List.Add($Value) | Out-Null
    }
}

$since = "$Days days ago"
$recentLines = Invoke-GitLines -GitArgs @("log", "--since=$since", "--no-merges", "--date=iso", "--pretty=format:COMMIT|%h|%ad|%an|%s", "--name-status")
$statusLines = Invoke-GitLines -GitArgs @("status", "--short")

$changeLines = New-Object System.Collections.Generic.List[string]
foreach ($line in $recentLines) { Add-Unique $changeLines $line }
foreach ($line in $statusLines) { Add-Unique $changeLines "WORKTREE|$line" }

$script:p0 = New-Object System.Collections.Generic.List[hashtable]
$script:p1 = New-Object System.Collections.Generic.List[hashtable]

function Add-Risk {
    param(
        [string]$Level,
        [string]$Title,
        [string]$Problem,
        [string]$Files,
        [string]$Advice
    )

    if ($Level -eq "P0") {
        $target = $script:p0
    } else {
        $target = $script:p1
    }
    $target.Add(@{
        title = $Title
        problem = $Problem
        files = $Files
        advice = $Advice
    }) | Out-Null
}

$joined = ($changeLines -join "`n")

$classCollectorPatterns = @(
    "Assets/Editor/ClassCollectorEditorWindow.cs",
    "Assets/Game/Scripts/Generated/ClassCollector.cs",
    "Assets/link.xml",
    "Assets/Editor/LevelPrefabStripValidator.cs",
    "Assets/Editor/BuildPipline/BPGameCustomBuildDelegate.cs"
)

$classCollectorHits = @()
foreach ($pattern in $classCollectorPatterns) {
    if ($joined -like "*$pattern*") {
        $classCollectorHits += $pattern
    }
}

if ($classCollectorHits.Count -gt 0) {
    Add-Risk `
        -Level "P0" `
        -Title "ClassCollector/link.xml 构建保留链发生变更" `
        -Problem "最近 $Days 天或当前工作区触及 ClassCollector、link.xml、LevelPrefabStripValidator 或构建前自动收集入口。该链路决定 IL2CPP/Managed Stripping 下运行时脚本是否被保留，属于会导致真机运行时组件缺失的最高风险面。" `
        -Files ($classCollectorHits -join "；") `
        -Advice "必须执行一次 ClassCollector 自动生成、Level Prefab Script Preserve 校验和 Android/iOS IL2CPP 构建验证；未验证前不要把相关改动合入发布分支。"
}

if ($joined -match "(?m)^D\s+.*Assets/Game/Scripts/Generated/ClassCollector\.cs") {
    Add-Risk `
        -Level "P0" `
        -Title "Generated/ClassCollector.cs 被删除或移除" `
        -Problem "检测到 Generated/ClassCollector.cs 删除记录。若运行时仍依赖静态类型触达或旧构建流程仍引用该文件，会出现构建期/运行期双重不确定性。" `
        -Files "Assets/Game/Scripts/Generated/ClassCollector.cs" `
        -Advice "确认所有运行时引用已迁移到 link.xml 或 Preserve 规则；保留删除原因、替代链路和构建验证结果。"
}

if ($joined -match "Assets/link\.xml") {
    Add-Risk `
        -Level "P0" `
        -Title "link.xml 发生变更，需验证运行时脚本保留" `
        -Problem "link.xml 是 Managed Stripping 的保底文件；新增、删除或自动重排都可能让 prefab 上的脚本、SDK 反射类型、Timeline/Physics2D 等类型在 IL2CPP 包中被裁剪。" `
        -Files "Assets/link.xml" `
        -Advice "比较变更前后的类型数量和关键模块类型；运行 LevelPrefabStripValidator；抽查最近新增关卡 prefab 的 MonoBehaviour 类型是否全部被保留。"
}

$buildInfraPatterns = @(
    "Assets/Editor/BuildPipline/",
    "Assets/Editor/BuildPipeline/",
    "Assets/Editor/LevelPrefabStripValidator.cs",
    "Assets/Resources/GameSetting.asset",
    "ProjectSettings/ProjectSettings.asset",
    "ProjectSettings/guru_gf_settings.txt"
)

$buildHits = @()
foreach ($pattern in $buildInfraPatterns) {
    if ($joined -like "*$pattern*") {
        $buildHits += $pattern
    }
}

if ($buildHits.Count -gt 0) {
    Add-Risk `
        -Level "P1" `
        -Title "构建管线或项目设置发生变更" `
        -Problem "检测到构建管线、GameSetting 或 ProjectSettings 相关改动。此类改动可能改变包体资源模式、裁剪级别、SDK 配置或构建前后处理顺序。" `
        -Files ($buildHits -join "；") `
        -Advice "把构建设置差异纳入风险报告，并在出包前执行一次 clean build；若同时触及 link.xml/ClassCollector，应升级为 P0 处理。"
}

$coreRuntimePatterns = @(
    "Assets/Game/Scripts/Play/PlayCtrl.cs",
    "Assets/Game/Scripts/GameManager.cs",
    "Assets/Game/UI/Global/Scripts/EnterLevelLoading.cs",
    "Assets/Game/Scripts/Runtime/Ads/",
    "Assets/Game/Scripts/SDK/Services/"
)

$runtimeHits = @()
foreach ($pattern in $coreRuntimePatterns) {
    if ($joined -like "*$pattern*") {
        $runtimeHits += $pattern
    }
}

if ($runtimeHits.Count -gt 0) {
    Add-Risk `
        -Level "P1" `
        -Title "核心运行时入口发生变更，需联动检查" `
        -Problem "最近变更触及 PlayCtrl、GameManager、Loading、广告或 SDK 服务入口。它们可能与构建保留链互相放大风险，例如类型保留正常但入口状态异常，或入口正常但 IL2CPP 裁剪导致真机缺类。" `
        -Files ($runtimeHits -join "；") `
        -Advice "日报中必须从变更入口扩散到调用链、资源链、状态机和 SDK 回调链；不能只描述单个改动文件。"
}

if ($p0.Count -eq 0 -and $p1.Count -eq 0) {
    Add-Risk `
        -Level "P1" `
        -Title "未发现 P0 哨兵命中项" `
        -Problem "最近 $Days 天 commit 和当前工作区未命中 ClassCollector/link.xml/构建链等预设高危触点。" `
        -Files "git log --since='$since'；git status --short" `
        -Advice "继续按每日风险评估规则扫描业务模块；本脚本只负责高风险工程链路提醒，不替代完整风险评估。"
}

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$reportPath = Join-Path $ReportDir "$timestamp-P0风险哨兵提醒.md"

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# $timestamp P0 风险哨兵提醒")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- **扫描范围**：最近 $Days 天 Git commit + 当前工作区未提交变更")
[void]$sb.AppendLine("- **扫描目的**：提前暴露 ClassCollector/link.xml/构建裁剪/核心入口类 P0 隐患")
[void]$sb.AppendLine("- **整体风险等级**：$(if ($p0.Count -gt 0) { 'P0' } else { 'P1' })")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## P0 风险")
[void]$sb.AppendLine("")

for ($i = 0; $i -lt $p0.Count; $i++) {
    $risk = $p0[$i]
    [void]$sb.AppendLine("### P0-$($i + 1) $($risk.title)")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- *问题*：$($risk.problem)")
    [void]$sb.AppendLine("- *影响文件*：$($risk.files)")
    [void]$sb.AppendLine("- *建议*：$($risk.advice)")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("## P1 风险")
[void]$sb.AppendLine("")

for ($i = 0; $i -lt $p1.Count; $i++) {
    $risk = $p1[$i]
    [void]$sb.AppendLine("### P1-$($i + 1) $($risk.title)")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- *问题*：$($risk.problem)")
    [void]$sb.AppendLine("- *影响文件*：$($risk.files)")
    [void]$sb.AppendLine("- *建议*：$($risk.advice)")
    [void]$sb.AppendLine("")
}

[void]$sb.AppendLine("## 命中的变更证据")
[void]$sb.AppendLine("")
foreach ($line in $changeLines | Select-Object -First 120) {
    [void]$sb.AppendLine(("- ``{0}``" -f $line))
}

[System.IO.File]::WriteAllText($reportPath, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Host "P0 risk guard report generated: $reportPath"
Write-Host "P0=$($p0.Count) P1=$($p1.Count)"

if (-not $SkipGitUpload) {
    $artifactPaths = Get-ReportArtifactPaths -PrimaryReportPath $reportPath
    Invoke-ReportGitUpload -ArtifactPaths $artifactPaths -CommitMessage $GitCommitMessage
}

if ($SendFeishu) {
    $sender = Join-Path $scriptDir "Send-AdsRiskReport.ps1"
    if (-not (Test-Path -LiteralPath $sender)) {
        throw "Send-AdsRiskReport.ps1 not found: $sender"
    }

    & powershell -ExecutionPolicy Bypass -File $sender `
        -WebhookUrl $WebhookUrl `
        -Secret $Secret `
        -ReportPath $reportPath `
        -ModuleName "P0哨兵"

    if ($LASTEXITCODE -ne 0) {
        throw "Feishu send failed with exit code $LASTEXITCODE"
    }
}
