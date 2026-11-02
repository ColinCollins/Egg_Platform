$ErrorActionPreference = 'Stop'
$taskDir = 'C:/Users/admin/AppData/Local/Temp/brainpunk-dig-selection-20261012'
$repoDir = 'D:/GitStore/BrinPunk/BrainPunk'
$sourceLines = Get-Content -LiteralPath "$repoDir/Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp" -Encoding UTF8
$allSources = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$baseLines = [Collections.Generic.List[string]]::new()
foreach ($line in $sourceLines) {
    if ($line -match '^-(out|refout):') { continue }
    if ($line -match '^"?(Assets/.+\.cs)"?$') {
        $sourceFile = $Matches[1]
        if (Test-Path -LiteralPath "$repoDir/$sourceFile") {
            if ($allSources.Add($sourceFile)) { $baseLines.Add('"' + $repoDir + '/' + $sourceFile + '"') }
        }
    } else { $baseLines.Add($line) }
}
$digSources = Get-ChildItem -LiteralPath "$repoDir/Assets/Game/Scripts/Level/DigLevel","$repoDir/Assets/Game/Scripts/Play/Dig","$repoDir/Assets/Game/UI/DigGame","$repoDir/Assets/Game/UI/LevelChoicePanel/Scripts/DigGame","$repoDir/Assets/Game/Scripts/SDK/Services/Events","$repoDir/Assets/Game/Scripts/Ads" -Filter '*.cs' -Recurse
foreach ($sourceFile in $digSources) {
    $relative = $sourceFile.FullName.Substring($repoDir.Length + 1).Replace('\','/')
    if ($allSources.Add($relative)) { $baseLines.Add('"' + $sourceFile.FullName + '"') }
}
Set-Location -LiteralPath $repoDir
foreach ($targetName in @('editor','android','ios')) {
    $targetLines = @($baseLines)
    if ($targetName -ne 'editor') {
        $targetLines = @($targetLines | Where-Object { $_ -notmatch '^-define:(UNITY_EDITOR.*|UNITY_STANDALONE.*)$' })
        $targetLines += if ($targetName -eq 'android') { '-define:UNITY_ANDROID' } else { '-define:UNITY_IOS' }
    }
    $targetLines += '-out:"' + "$taskDir/compiled-$targetName.dll" + '"'
    [IO.File]::WriteAllLines("$taskDir/$targetName.rsp", $targetLines, [Text.UTF8Encoding]::new($false))
    & dotnet 'C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Data/DotNetSdkRoslyn/csc.dll' "@$taskDir/$targetName.rsp" *> "$taskDir/compile-$targetName.log"
    $compileExit = $LASTEXITCODE
    $errors = @(Select-String -LiteralPath "$taskDir/compile-$targetName.log" -Pattern ': error ')
    $warnings = @(Select-String -LiteralPath "$taskDir/compile-$targetName.log" -Pattern ': warning ')
    Write-Output "$targetName sources=$($allSources.Count) exit=$compileExit errors=$($errors.Count) warnings=$($warnings.Count)"
    $errors | Select-Object -First 16
}

