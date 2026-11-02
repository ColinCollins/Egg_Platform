$ErrorActionPreference = 'Stop'
$sourceRepo = 'D:/GitStore/BrinPunk/BrainPunk'
$targetRepo = 'D:/GitStore/BrinPunk/BrainPunk'
$taskDir = 'C:/Users/admin/AppData/Local/Temp/brainpunk-diggame-20260930'
$baseLines = Get-Content -LiteralPath 'C:/Users/admin/AppData/Local/Temp/brainpunk-diggame-20260929/editor.rsp' -Encoding UTF8
$mappedLines = [Collections.Generic.List[string]]::new()
$sourceFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$missingFiles = [Collections.Generic.List[string]]::new()
$readOnlyGfCount = 0
foreach ($line in $baseLines) {
    if ($line -match '^[-/](out|refout):') { continue }
    if ($line -match '^"(.+\.cs)"$') {
        $file = $Matches[1].Replace('\', '/')
        if (-not [IO.Path]::IsPathRooted($file)) { $file = "$sourceRepo/$file" }
        if (-not $file.StartsWith($sourceRepo + '/', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Unexpected source outside source repository: $file"
        }
        $relative = $file.Substring($sourceRepo.Length + 1)
        $mapped = "$targetRepo/$relative"
        if ($relative.StartsWith('Assets/GF/')) {
            # 两个基线 GF gitlink 相同；仅只读编译原目录，不初始化/写入子模块。
            $mapped = $file
            $readOnlyGfCount++
        }
        if (-not (Test-Path -LiteralPath $mapped)) {
            $missingFiles.Add($relative)
            continue
        }
        if ($sourceFiles.Add($mapped)) { $mappedLines.Add('"' + $mapped + '"') }
    } else {
        # 只读复用原工程已解析的 Unity/第三方引用，不导入 worktree 或改写依赖。
        $mappedLines.Add($line)
    }
}
$expectedAbsent = @('Attribution','Debug','Diagnostics','Retry','Rewards','Url') | ForEach-Object {
    "Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.$_.cs"
}
$unexpected = @($missingFiles | Where-Object {$_ -notin $expectedAbsent})
if ($unexpected.Count -gt 0) { throw "Unexpected missing sources: $($unexpected -join ', ')" }
Write-Output "Source files: $($sourceFiles.Count); read-only GF: $readOnlyGfCount; excluded newer OneLink files: $($missingFiles.Count)"
foreach ($targetName in @('editor','android')) {
    $targetLines = @($mappedLines)
    if ($targetName -eq 'android') {
        $targetLines = @($targetLines | Where-Object {$_ -notmatch '^-define:UNITY_EDITOR'})
    }
    $targetLines += '-out:"' + "$taskDir/common-$targetName.dll" + '"'
    [IO.File]::WriteAllLines("$taskDir/common-$targetName.rsp", $targetLines, [Text.UTF8Encoding]::new($false))
    Set-Location -LiteralPath $sourceRepo
    & dotnet 'C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Data/DotNetSdkRoslyn/csc.dll' "@$taskDir/common-$targetName.rsp" *> "$taskDir/common-$targetName-compile.log"
    $compileExit = $LASTEXITCODE
    $errors = @(Select-String -LiteralPath "$taskDir/common-$targetName-compile.log" -Pattern ': error ')
    $warnings = @(Select-String -LiteralPath "$taskDir/common-$targetName-compile.log" -Pattern ': warning ')
    Write-Output "$targetName exit=$compileExit errors=$($errors.Count) warnings=$($warnings.Count)"
    $errors | Select-Object -First 30
    if ($compileExit -ne 0) { exit $compileExit }
}
