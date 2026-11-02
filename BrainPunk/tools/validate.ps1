[CmdletBinding()]
param(
    [ValidateSet("compile", "editmode", "playmode", "all")]
    [string]$Mode = "compile",

    [string]$UnityPath,

    [string]$ProjectPath,

    [string]$LogDirectory,

    [switch]$NoGraphics
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$defaultProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $defaultProjectPath
}

if ([string]::IsNullOrWhiteSpace($LogDirectory)) {
    $LogDirectory = Join-Path $defaultProjectPath "Temp\Validation"
}

function Get-ProjectEditorVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $versionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path $versionFile)) {
        throw "ProjectVersion.txt not found: $versionFile"
    }

    $versionLine = Get-Content -Path $versionFile | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionLine)) {
        throw "m_EditorVersion was not found in $versionFile"
    }

    return ($versionLine -split ":", 2)[1].Trim()
}

function Resolve-UnityExecutable {
    param(
        [string]$RequestedPath,
        [Parameter(Mandatory = $true)]
        [string]$EditorVersion
    )

    $candidates = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $candidates.Add($RequestedPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_PATH)) {
        $candidates.Add($env:UNITY_PATH)
    }

    $candidates.Add("C:\Program Files\Unity\Hub\Editor\$EditorVersion\Editor\Unity.exe")
    $candidates.Add("C:\Program Files\Unity\Editor\$EditorVersion\Editor\Unity.exe")
    $candidates.Add("C:\Unity\$EditorVersion\Editor\Unity.exe")

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    $searched = $candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Get-Unique
    throw "Unity.exe not found. Looked in:`n - $($searched -join "`n - ")`nPass -UnityPath or set UNITY_PATH."
}

function Get-KnownLogIssues {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogFile
    )

    $issues = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path $LogFile)) {
        return $issues
    }

    if (Select-String -Path $LogFile -Pattern "Project folder or disk is read only" -Quiet) {
        $issues.Add("Unity reports that the project folder is read-only.")
    }

    if (Select-String -Path $LogFile -Pattern "another Unity instance is running with this project open" -Quiet) {
        $issues.Add("Another Unity instance is already using this project.")
    }

    if (Select-String -Path $LogFile -Pattern "Failed to acquire global mutex Unity-LicenseClient" -Quiet) {
        $issues.Add("Unity licensing client mutex could not be acquired during startup.")
    }

    return $issues
}

function Invoke-UnityValidation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$EditorExe,
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot,
        [Parameter(Mandatory = $true)]
        [string]$ValidationMode,
        [Parameter(Mandatory = $true)]
        [string]$OutputDirectory,
        [switch]$DisableGraphics
    )

    if (-not (Test-Path $OutputDirectory)) {
        New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    }

    $logFile = Join-Path $OutputDirectory ("unity-{0}.log" -f $ValidationMode)
    $arguments = @(
        "-batchmode"
        "-quit"
        "-projectPath", $ProjectRoot
        "-logFile", $logFile
    )

    if ($DisableGraphics) {
        $arguments += "-nographics"
    }

    switch ($ValidationMode) {
        "compile" {
        }
        "editmode" {
            $arguments += @("-runTests", "-testPlatform", "EditMode", "-testResults", (Join-Path $OutputDirectory "editmode-results.xml"))
        }
        "playmode" {
            $arguments += @("-runTests", "-testPlatform", "PlayMode", "-testResults", (Join-Path $OutputDirectory "playmode-results.xml"))
        }
        default {
            throw "Unsupported validation mode: $ValidationMode"
        }
    }

    Write-Host "Running Unity $ValidationMode validation..."
    Write-Host "Editor: $EditorExe"
    Write-Host "Project: $ProjectRoot"
    Write-Host "Log: $logFile"

    $process = Start-Process -FilePath $EditorExe -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    $exitCode = $process.ExitCode

    $knownIssues = @(Get-KnownLogIssues -LogFile $logFile)
    if ($exitCode -ne 0) {
        if ($knownIssues.Count -gt 0) {
            throw "Unity exited with code ${exitCode}:`n - $($knownIssues -join "`n - ")`nSee log: $logFile"
        }

        throw "Unity exited with code $exitCode. See log: $logFile"
    }

    if (-not (Test-Path $logFile)) {
        throw "Unity finished without writing log file: $logFile"
    }

    $errorMatches = Select-String -Path $logFile -Pattern @(
        "error CS[0-9]+:"
        "error( |:)"
        "Exception:"
        "Assertion failed"
        "Compilation failed"
        "test run failed"
    )

    if ($errorMatches) {
        $preview = $errorMatches | Select-Object -First 20 | ForEach-Object { $_.Line.Trim() }
        throw "Validation log contains errors:`n - $($preview -join "`n - ")`nSee log: $logFile"
    }

    Write-Host "Validation succeeded: $ValidationMode"
    return $logFile
}

$resolvedProjectPath = (Resolve-Path $ProjectPath).Path
$editorVersion = Get-ProjectEditorVersion -ProjectRoot $resolvedProjectPath
$resolvedUnityPath = Resolve-UnityExecutable -RequestedPath $UnityPath -EditorVersion $editorVersion

$modes = switch ($Mode) {
    "all" { @("compile", "editmode", "playmode") }
    default { @($Mode) }
}

$logs = New-Object System.Collections.Generic.List[string]
foreach ($singleMode in $modes) {
    $logPath = Invoke-UnityValidation -EditorExe $resolvedUnityPath -ProjectRoot $resolvedProjectPath -ValidationMode $singleMode -OutputDirectory $LogDirectory -DisableGraphics:$NoGraphics
    $logs.Add($logPath)
}

Write-Host ""
Write-Host "Finished validation. Logs:"
$logs | ForEach-Object { Write-Host " - $_" }
