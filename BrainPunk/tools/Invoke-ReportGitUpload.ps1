param(
  [Parameter(Mandatory = $true)]
  [string]$ReportPath,

  [string]$GitCommitMessage,

  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

function Join-Chars {
  param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [int[]]$Codes
  )

  return -join ($Codes | ForEach-Object { [char]$_ })
}

function Write-GitUploadInfo {
  param([string]$Message)

  Write-Host "[GitUpload] $Message"
}

function Convert-CommandOutputToText {
  param([object[]]$OutputLines)

  if ($null -eq $OutputLines) {
    return ""
  }

  return (($OutputLines | ForEach-Object {
    if ($null -eq $_) { "" } else { "$_" }
  }) -join [Environment]::NewLine).Trim()
}

function Invoke-GitCommand {
  param(
    [string]$WorkingDirectory,
    [string[]]$GitArgs
  )

  $previousErrorActionPreference = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  try {
    $output = & git -c core.quotePath=false -C $WorkingDirectory @GitArgs 2>&1
    $exitCode = $LASTEXITCODE
  }
  finally {
    $ErrorActionPreference = $previousErrorActionPreference
  }

  $outputLines = @($output)
  return @{
    ExitCode = $exitCode
    Lines = $outputLines
    Text = Convert-CommandOutputToText -OutputLines $outputLines
    Command = "git -c core.quotePath=false -C `"$WorkingDirectory`" $($GitArgs -join ' ')"
  }
}

function Invoke-GitCommandOrThrow {
  param(
    [string]$WorkingDirectory,
    [string[]]$GitArgs,
    [string]$OperationName
  )

  $result = Invoke-GitCommand -WorkingDirectory $WorkingDirectory -GitArgs $GitArgs
  if ($result.ExitCode -ne 0) {
    throw "Git command failed during ${OperationName}: $($result.Command)`n$($result.Text)"
  }

  return $result
}

function Resolve-ExistingFile {
  param([string]$Path)

  $candidate = $Path
  if (-not [System.IO.Path]::IsPathRooted($candidate)) {
    $candidate = Join-Path (Get-Location).Path $candidate
  }

  $resolved = Resolve-Path -LiteralPath $candidate
  if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
    throw "Report path is not a file: $($resolved.Path)"
  }

  return [System.IO.Path]::GetFullPath($resolved.Path)
}

function Get-ScriptProjectRoot {
  if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    return [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
  }

  return [System.IO.Path]::GetFullPath((Get-Location).Path)
}

function Resolve-GitTopLevel {
  param([string]$StartDirectory)

  $result = Invoke-GitCommandOrThrow -WorkingDirectory $StartDirectory -GitArgs @("rev-parse", "--show-toplevel") -OperationName "resolve git top level"
  $topLevel = $result.Text
  if ([string]::IsNullOrWhiteSpace($topLevel)) {
    throw "Unable to resolve git top level from $StartDirectory"
  }

  return [System.IO.Path]::GetFullPath($topLevel)
}

function Resolve-GitDirectory {
  param([string]$GitRoot)

  $result = Invoke-GitCommand -WorkingDirectory $GitRoot -GitArgs @("rev-parse", "--absolute-git-dir")
  if ($result.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($result.Text)) {
    $result = Invoke-GitCommandOrThrow -WorkingDirectory $GitRoot -GitArgs @("rev-parse", "--git-dir") -OperationName "resolve git directory"
  }

  $gitDir = $result.Text
  if ([System.IO.Path]::IsPathRooted($gitDir)) {
    return [System.IO.Path]::GetFullPath($gitDir)
  }

  return [System.IO.Path]::GetFullPath((Join-Path $GitRoot $gitDir))
}

function Test-PathUnderRoot {
  param(
    [string]$Root,
    [string]$Path
  )

  $resolvedRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
  $resolvedPath = [System.IO.Path]::GetFullPath($Path)

  if ($resolvedPath.Equals($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    return $true
  }

  $rootWithSeparator = $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar
  return $resolvedPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-RelativeGitPath {
  param(
    [string]$GitRoot,
    [string]$AbsolutePath
  )

  $resolvedRoot = [System.IO.Path]::GetFullPath($GitRoot).TrimEnd('\', '/')
  $resolvedPath = [System.IO.Path]::GetFullPath($AbsolutePath)

  if (-not (Test-PathUnderRoot -Root $resolvedRoot -Path $resolvedPath)) {
    throw "Path is outside git root. Git root: $resolvedRoot Path: $resolvedPath"
  }

  $relative = $resolvedPath.Substring($resolvedRoot.Length).TrimStart('\', '/')
  return ($relative -replace '\\', '/')
}

function Normalize-GitPath {
  param([string]$Path)

  return ($Path -replace '\\', '/').Trim()
}

function Convert-GitLinesToPathArray {
  param([object[]]$Lines)

  $paths = New-Object System.Collections.Generic.List[string]
  foreach ($line in @($Lines)) {
    if ($null -eq $line) {
      continue
    }

    $normalized = Normalize-GitPath -Path "$line"
    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
      $paths.Add($normalized) | Out-Null
    }
  }

  return $paths.ToArray()
}

function Get-ReportArtifactPaths {
  param([string]$PrimaryReportPath)

  $artifacts = New-Object System.Collections.Generic.List[string]
  $seen = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)

  $resolvedPrimary = Resolve-ExistingFile -Path $PrimaryReportPath
  $artifacts.Add($resolvedPrimary) | Out-Null
  $seen.Add($resolvedPrimary) | Out-Null

  $reportDirectory = Split-Path -Parent $resolvedPrimary
  $reportName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedPrimary)

  if ($reportName -match '^(\d{4}-\d{2}-\d{2}-\d{4})-') {
    $batchPrefix = $Matches[1]
    $batchFiles = Get-ChildItem -LiteralPath $reportDirectory -File -Filter "$batchPrefix-*.md" |
      Sort-Object FullName

    foreach ($file in $batchFiles) {
      $fullName = [System.IO.Path]::GetFullPath($file.FullName)
      if ($seen.Add($fullName)) {
        $artifacts.Add($fullName) | Out-Null
      }
    }
  }

  return $artifacts.ToArray()
}

function Assert-GitDirectoryWritable {
  param(
    [string]$GitRoot,
    [string]$GitDirectory
  )

  $probePath = Join-Path $GitDirectory (".codex-git-write-probe-{0}.tmp" -f [System.Guid]::NewGuid().ToString("N"))

  try {
    [System.IO.File]::WriteAllText($probePath, "git write probe")
  }
  catch {
    $probeError = $_.Exception.Message
    throw "[GitUpload] Git directory is not writable: $GitDirectory. Git root: $GitRoot. This is not a stale index.lock; run the upload outside the sandbox or include the repository root in writable roots. Probe error: $probeError"
  }
  finally {
    if (Test-Path -LiteralPath $probePath -PathType Leaf) {
      Remove-Item -LiteralPath $probePath -Force -ErrorAction SilentlyContinue
    }
  }
}

function Get-ActiveGitProcessSummary {
  $processes = @(Get-Process git,sh -ErrorAction SilentlyContinue)
  if ($processes.Count -eq 0) {
    return ""
  }

  return (($processes | ForEach-Object { "$($_.ProcessName):$($_.Id)" }) -join ", ")
}

function Clear-StaleGitIndexLock {
  param(
    [string]$GitDirectory,
    [int]$MinimumAgeSeconds = 10
  )

  $lockPath = Join-Path $GitDirectory "index.lock"
  if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    return $false
  }

  $activeGitProcesses = Get-ActiveGitProcessSummary
  if (-not [string]::IsNullOrWhiteSpace($activeGitProcesses)) {
    Write-Warning "[GitUpload] index.lock exists but active git processes are present: $activeGitProcesses"
    return $false
  }

  $lockItem = Get-Item -LiteralPath $lockPath
  $lockAgeSeconds = ((Get-Date) - $lockItem.LastWriteTime).TotalSeconds
  if ($lockAgeSeconds -lt $MinimumAgeSeconds) {
    Write-Warning "[GitUpload] index.lock exists but is only $([Math]::Round($lockAgeSeconds, 1))s old; leaving it in place."
    return $false
  }

  Remove-Item -LiteralPath $lockPath -Force
  Write-Warning "[GitUpload] Removed stale git lock: $lockPath"
  return $true
}

function Test-GitLockFailure {
  param([string]$Message)

  if ([string]::IsNullOrWhiteSpace($Message)) {
    return $false
  }

  return $Message -match 'index\.lock' -or
    $Message -match 'another git process seems to be running'
}

function Invoke-UploadGitCommand {
  param(
    [string]$GitRoot,
    [string]$GitDirectory,
    [string[]]$GitArgs,
    [string]$OperationName,
    [int]$MaxAttempts = 3,
    [int]$RetryDelayMilliseconds = 1500
  )

  for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    $result = Invoke-GitCommand -WorkingDirectory $GitRoot -GitArgs $GitArgs

    if ($result.ExitCode -eq 0) {
      if (-not [string]::IsNullOrWhiteSpace($result.Text)) {
        Write-Host $result.Text
      }
      return $result
    }

    $isGitLockFailure = Test-GitLockFailure -Message $result.Text
    if ($isGitLockFailure -and $attempt -lt $MaxAttempts) {
      Clear-StaleGitIndexLock -GitDirectory $GitDirectory | Out-Null
      Write-Warning "[GitUpload] $OperationName failed on attempt $attempt/$MaxAttempts because Git index is locked. Retrying in $RetryDelayMilliseconds ms..."
      Start-Sleep -Milliseconds $RetryDelayMilliseconds
      continue
    }

    throw "Git command failed during ${OperationName}: $($result.Command)`n$($result.Text)"
  }
}

function Get-StagedPaths {
  param([string]$GitRoot)

  $result = Invoke-GitCommandOrThrow -WorkingDirectory $GitRoot -GitArgs @("diff", "--cached", "--name-only") -OperationName "read staged paths"
  return Convert-GitLinesToPathArray -Lines $result.Lines
}

function Assert-StagedPathsAreArtifacts {
  param(
    [string[]]$StagedPaths,
    [System.Collections.Generic.HashSet[string]]$ArtifactPathSet,
    [string]$Context
  )

  $unexpected = @($StagedPaths | ForEach-Object { Normalize-GitPath -Path "$_" } | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and -not $ArtifactPathSet.Contains($_)
  })
  if ($unexpected.Count -gt 0) {
    throw "[GitUpload] Refusing to auto commit because staged changes outside report artifacts exist during ${Context}: $($unexpected -join ', ')"
  }
}

function Invoke-ReportGitUpload {
  param(
    [string]$ScriptProjectRoot,
    [string[]]$ArtifactPaths,
    [string]$CommitMessage,
    [switch]$DryRun
  )

  if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
    $CommitMessage = Join-Chars 0x6BCF 0x65E5 0x98CE 0x9669 0x62A5 0x544A
  }

  $gitRoot = Resolve-GitTopLevel -StartDirectory $ScriptProjectRoot
  $gitDirectory = Resolve-GitDirectory -GitRoot $gitRoot

  $relativeArtifacts = @()
  foreach ($path in $ArtifactPaths) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
      $relativeArtifacts += Get-RelativeGitPath -GitRoot $gitRoot -AbsolutePath $path
    }
  }

  $relativeArtifacts = @($relativeArtifacts | Sort-Object -Unique)
  if ($relativeArtifacts.Count -eq 0) {
    Write-GitUploadInfo "No report artifacts found for git upload."
    return
  }

  $artifactSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
  foreach ($relativePath in $relativeArtifacts) {
    $artifactSet.Add((Normalize-GitPath -Path $relativePath)) | Out-Null
  }

  Write-GitUploadInfo "Script project root: $ScriptProjectRoot"
  Write-GitUploadInfo "Git root: $gitRoot"
  Write-GitUploadInfo "Git directory: $gitDirectory"
  Write-GitUploadInfo "Artifact count: $($relativeArtifacts.Count)"
  foreach ($relativePath in $relativeArtifacts) {
    Write-GitUploadInfo "Artifact: $relativePath"
  }

  if ($DryRun) {
    Write-GitUploadInfo "Dry run only. Planned checks and commands:"
    Write-Host "  check git directory writable: $gitDirectory"
    Write-Host "  git -C `"$gitRoot`" add -- $($relativeArtifacts -join ' ')"
    Write-Host "  git -C `"$gitRoot`" diff --cached --name-only -- $($relativeArtifacts -join ' ')"
    Write-Host "  git -C `"$gitRoot`" commit -m `"$CommitMessage`""
    Write-Host "  git -C `"$gitRoot`" push"
    return
  }

  Assert-GitDirectoryWritable -GitRoot $gitRoot -GitDirectory $gitDirectory
  Clear-StaleGitIndexLock -GitDirectory $gitDirectory | Out-Null

  $stagedBefore = Get-StagedPaths -GitRoot $gitRoot
  Assert-StagedPathsAreArtifacts -StagedPaths $stagedBefore -ArtifactPathSet $artifactSet -Context "preflight"
  if ($stagedBefore.Count -gt 0) {
    Write-GitUploadInfo "Continuing with pre-existing staged report artifacts: $($stagedBefore -join ', ')"
  }

  Invoke-UploadGitCommand -GitRoot $gitRoot -GitDirectory $gitDirectory -GitArgs (@("add", "--") + $relativeArtifacts) -OperationName "git add report artifacts" | Out-Null

  $stagedAfterAdd = Get-StagedPaths -GitRoot $gitRoot
  Assert-StagedPathsAreArtifacts -StagedPaths $stagedAfterAdd -ArtifactPathSet $artifactSet -Context "post-add"

  $stagedReportResult = Invoke-GitCommandOrThrow -WorkingDirectory $gitRoot -GitArgs (@("diff", "--cached", "--name-only", "--") + $relativeArtifacts) -OperationName "read staged report artifacts"
  $stagedReportFiles = Convert-GitLinesToPathArray -Lines $stagedReportResult.Lines

  if ($stagedReportFiles.Count -eq 0) {
    Write-GitUploadInfo "No report changes detected for auto git upload."
    return
  }

  Write-GitUploadInfo "Staged report artifact count: $($stagedReportFiles.Count)"
  foreach ($stagedPath in $stagedReportFiles) {
    Write-GitUploadInfo "Staged: $stagedPath"
  }

  Invoke-UploadGitCommand -GitRoot $gitRoot -GitDirectory $gitDirectory -GitArgs @("commit", "-m", $CommitMessage) -OperationName "git commit report artifacts" | Out-Null
  Invoke-UploadGitCommand -GitRoot $gitRoot -GitDirectory $gitDirectory -GitArgs @("push") -OperationName "git push report artifacts" | Out-Null
}

$resolvedReportPath = Resolve-ExistingFile -Path $ReportPath
$scriptProjectRoot = Get-ScriptProjectRoot
$artifactPaths = Get-ReportArtifactPaths -PrimaryReportPath $resolvedReportPath

Invoke-ReportGitUpload -ScriptProjectRoot $scriptProjectRoot -ArtifactPaths $artifactPaths -CommitMessage $GitCommitMessage -DryRun:$DryRun
