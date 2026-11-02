$ErrorActionPreference='Stop'
$repoDir='D:/GitStore/BrinPunk/BrainPunk'
$taskDir='C:/Users/admin/AppData/Local/Temp/brainpunk-dig-pagination-20261013'
$drawTask='C:/Users/admin/AppData/Local/Temp/brainpunk-draw-pagination-20261013'
$enc=New-Object Text.UTF8Encoding($false)
function Read-MethodText([string]$text,[string]$signature){
 $start=$text.IndexOf($signature,[StringComparison]::Ordinal);if($start -lt 0){throw "找不到方法 $signature"};$open=$text.IndexOf('{',$start);$depth=0
 for($i=$open;$i -lt $text.Length;$i++){if($text[$i] -eq '{'){$depth++};if($text[$i] -eq '}'){$depth--;if($depth -eq 0){return $text.Substring($start,$i-$start+1)}}};throw '括号不完整'
}
function Read-Method([string]$path,[string]$signature){Read-MethodText ([IO.File]::ReadAllText($path)) $signature}
function Convert-Dig([string]$text){$text.Replace('Draw','Dig').Replace('draw','dig').Replace('DigGame_ChoiceLevelPanel','DigGame__ChoiceLevelPanel')}
$stubs=Convert-Dig ([IO.File]::ReadAllText("$drawTask/Stubs.cs"))
$oldEnter=Read-MethodText $stubs 'public bool TryEnterDig(out string error)';$stubs=$stubs.Replace($oldEnter,'')
$stubs=$stubs.Replace('public class GameManager{','public partial class GameManager{public EntryManager ControllerManager=new();private FocusProxy _digPlayCtrl=new();')
$start=$stubs.IndexOf('public partial class HomeHarness:');if($start -lt 0){throw '首页夹具标记缺失'};$stubs=$stubs.Substring(0,$start)
$stubs+=@'
public partial class HomeHarness:IDebuger{public void Click()=>Enter(new CustomButton());}
public partial class ExitHarness:IDebuger{
 public bool IsActive=true;private bool _transitioning,_instance,_hud;private object _runtime;private int _levelId=21;
 private DigGame__ChoiceLevelPanel _choice=>GameManager.Instance.Panel;
 public ExitHarness(bool opening,bool running){_transitioning=opening;_runtime=running?new object():null;}
}
public class EntryManager{public bool TryEnter(PlayEntryRequest request,out string error){error=null;Trace.Calls.Add("dig");GameManager.Instance.Panel?.OnOpen();return true;}}
public class FocusProxy{public void FocusHighestPlayableLevel(){var p=GameManager.Instance.Panel;p.GetType().GetMethod("ShowHighestPlayableLevelPage",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(p,null);}}
public enum PlayModeType{Dig}
public class PlayEntryRequest{public PlayEntryRequest(PlayModeType type){}}
namespace Game.ConfigModule{public static class ConfigManager{public static class RemoteConfig{public static bool GetDigGameIsOpen()=>global::ConfigManager.RemoteConfig.Open;}}}
'@
[IO.File]::WriteAllText("$taskDir/Stubs.cs",$stubs,$enc)
$program=Convert-Dig ([IO.File]::ReadAllText("$drawTask/Program.cs"))
$program=$program.Replace('Setup(21,2);new ExitHarness','Setup(5,1);new ExitHarness').Replace('Page(dig)==2&&Trace.Calls.SequenceEqual(new[]{"home","dig"})','Page(dig)==0&&Trace.Calls.SequenceEqual(new[]{"home","dig"})')
$extra=@'
  Setup();Call(dig,"ChangePage",2);Check(dig.TryEnterOneLinkLevel(GameManager.Instance.DigGameLevel.LevelSorts[20],GameManager.Instance.DigGameLevel.Data["data21"],out var linkError)&&DB.DigGame.ChoiceLevelPanelPageIndex==2&&DB.DigWrites==1,"OneLink仍复用进关页保存链");
  Setup();dig.OnClose();dig.EnterLevel(GameManager.Instance.DigGameLevel.LevelSorts[20],GameManager.Instance.DigGameLevel.Data["data21"],1);Check(DB.DigWrites==0&&DB.DigGame.CurrentLevel==1,"已关闭面板拒绝进关写入");
'@
$program=$program.Replace('  Console.WriteLine("RESULT checks="',$extra+'  Console.WriteLine("RESULT checks="')
[IO.File]::WriteAllText("$taskDir/Program.cs",$program,$enc)
$mainSource="$repoDir/Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs"
$methods=@('private int GetInitialPageIndex(int maxIndex)','private void RefreshPageIndex()','private void OnQuickTurnPageChanged(int targetPageIndex)','public void EnterLevel(LevelSort sort)','public override void OnClose()')|ForEach-Object{Read-Method $mainSource $_}
[IO.File]::WriteAllText("$taskDir/MainMethods.cs","using System;using Bear.Logger;using Bear.UI;public partial class MainPanel{"+($methods -join [Environment]::NewLine)+"}",$enc)
$levelSource="$repoDir/Assets/Game/Scripts/Play/LevelCtrl.cs"
[IO.File]::WriteAllText("$taskDir/LevelMethods.cs","using System;using System.Linq;using Bear.Logger;public partial class LevelCtrl{"+(Read-Method $levelSource 'public int CurrentLevel')+(Read-Method $levelSource 'public LevelData CurrentLevelData')+"}",$enc)
$compiler='C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Data/DotNetSdkRoslyn/csc.dll'
$refs=@(Get-ChildItem -LiteralPath 'C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/10.0.1/ref/net10.0' -Filter '*.dll'|ForEach-Object{'-r:"'+$_.FullName+'"'})
foreach($version in @('before','after')){
 $source=if($version -eq 'before'){"$taskDir/before"}else{$repoDir};$out="$taskDir/$version-test";[IO.Directory]::CreateDirectory($out)|Out-Null
 $panel='Assets/Game/UI/LevelChoicePanel/Scripts/DigGame/DigGame__ChoiceLevelPanel.cs';Copy-Item -LiteralPath "$source/$panel" -Destination "$out/DigGame__ChoiceLevelPanel.cs"
 $homeMethod=Read-Method "$source/Assets/Game/UI/StartPanel/Scripts/DigGameEnter.cs" 'private void Enter(CustomButton button)'
 $exit=Read-Method "$source/Assets/Game/Scripts/Play/Dig/DigPlayCtrl.cs" 'public void ExitToLevelSelection()'
 $entry=Read-Method "$source/Assets/Game/Scripts/GameManager.cs" 'public bool TryEnterDig(out string error)'
 [IO.File]::WriteAllText("$out/EntryMethods.cs","using Bear.Logger;public partial class HomeHarness{"+$homeMethod+"}public partial class ExitHarness{"+$exit+"}public partial class GameManager{"+$entry+"}",$enc)
 $compileArgs=@('-target:exe','-nologo','-nowarn:0649,0067',('-out:"'+$out+'/checks.dll"'))+$refs+@("$taskDir/Stubs.cs","$taskDir/Program.cs","$taskDir/MainMethods.cs","$taskDir/LevelMethods.cs","$out/EntryMethods.cs","$out/DigGame__ChoiceLevelPanel.cs")
 [IO.File]::WriteAllLines("$out/checks.rsp",$compileArgs,$enc);& dotnet $compiler "@$out/checks.rsp" *> "$out/compile.log";if($LASTEXITCODE -ne 0){Get-Content "$out/compile.log" -Encoding UTF8;throw '隔离编译失败'}
 [IO.File]::WriteAllText("$out/checks.runtimeconfig.json",'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.1"}}}',$enc)
 & dotnet "$out/checks.dll" *> "$out/results.log";$runExit=$LASTEXITCODE;"$version exit=$runExit";Get-Content "$out/results.log" -Encoding UTF8|Select-String '^FAIL|^RESULT';if($version -eq 'after' -and $runExit -ne 0){throw '修改后验证失败'}
}
