from pathlib import Path
import xml.sax.saxutils as x
repo=Path('D:/GitStore/BrinPunk/BrainPunk')
p=Path('C:/Users/admin/AppData/Local/Temp/brainpunk-dig-base-20261012/managed')
src=(repo/'Assets/Game/Scripts/SDK/Services/Core/OneLinkLevelRouter.cs').read_text(encoding='utf-8')
body=src[src.index('        private static bool TryEnterDig('):src.rindex('        #endregion')]
body=body.replace('private static bool TryEnterDig','public static bool TryEnterDig')
(p/'RouteProbe.cs').write_text('using System;using Config;using Config.Level;using GuruLog = Guru.SDK.Framework.Utils.Log.Log;\npublic static class DigRouteProbe {\n'+body+'\n}',encoding='utf-8')
files=['Assets/Game/Scripts/SDK/Services/Core/OneLinkRequest.cs','Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.SrnCampaign.cs','Assets/Game/Scripts/Play/Dig/DigGameLevelCtrl.cs','Assets/Game/DB/DigGame.cs','Assets/Game/DB/DigGame_Partial.cs','Assets/Configs/Code/DigLevelData.cs','Assets/Configs/Code/DigLevelSort.cs','Assets/Configs/Code/Level/GameType.cs','Library/PackageCache/com.code-philosophy.luban@d870def77ce1/Runtime/SimpleJSON/SimpleJSON.cs']
s='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'
files.append('Assets/Configs/Code/Level/LevelLockType.cs')
s+=''.join('<Compile Include="'+x.escape(str(repo/f))+'" />' for f in files)
s+='<Reference Include="Newtonsoft.Json"><HintPath>'+str(repo/'Library/PackageCache/com.unity.nuget.newtonsoft-json@74deb55db2a0/Runtime/Newtonsoft.Json.dll')+'</HintPath></Reference></ItemGroup></Project>'
(p/'Managed.csproj').write_text(s,encoding='utf-8')
