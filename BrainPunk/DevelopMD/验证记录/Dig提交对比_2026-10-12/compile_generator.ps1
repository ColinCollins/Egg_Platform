$ErrorActionPreference = 'Stop'
$taskDir = 'C:/Users/admin/AppData/Local/Temp/brainpunk-dig-delivery-20261012'
$repoDir = 'D:/GitStore/BrinPunk/BrainPunk'
$sourceLines = Get-Content -LiteralPath "$repoDir/Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp-Editor.rsp" -Encoding UTF8
$targetLines = foreach ($line in $sourceLines) {
    if ($line -match '^-(out|refout):') { continue }
    if ($line -match '^"?(Assets/.+\.cs)"?$') {
        if (Test-Path -LiteralPath "$repoDir/$($Matches[1])") {
            $deliverySource = "$taskDir/snapshot/BrainPunk/$($Matches[1])"
            if (!(Test-Path -LiteralPath $deliverySource)) { $deliverySource = "$repoDir/$($Matches[1])" }
            '"' + $deliverySource + '"'
        }
    } elseif ($line -match '^-r:".*Assembly-CSharp\.dll"$') {
        '-r:"' + "$taskDir/compiled-editor.dll" + '"'
    } else { $line }
}
$targetLines += '-out:"' + "$taskDir/compiled-generator.dll" + '"'
[IO.File]::WriteAllLines("$taskDir/generator.rsp", $targetLines, [Text.UTF8Encoding]::new($false))
Set-Location -LiteralPath $repoDir
& dotnet 'C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Data/DotNetSdkRoslyn/csc.dll' "@$taskDir/generator.rsp" *> "$taskDir/compile-generator.log"
$errors = @(Select-String -LiteralPath "$taskDir/compile-generator.log" -Pattern 'error CS')
Write-Output "editor-generator exit=$LASTEXITCODE errors=$($errors.Count)"
$errors | Select-Object -First 12
