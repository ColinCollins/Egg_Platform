from pathlib import Path
import re,json,hashlib
r=Path('D:/GitStore/BrinPunk/BrainPunk');t=Path(__file__).parent;d=t/'generator';d.mkdir(exist_ok=True)
s=(r/'Assets/Game/Editor/LubanGenerator.cs').read_text(encoding='utf-8-sig')
def method(name):
    a=re.search(r'^\s*private static [^\r\n]*\b'+name+r'\(',s,re.M).start()
    b=s.index('{',s.index(name+'(',a));depth=1;i=b+1
    while depth:
        if s[i]=='{':depth+=1
        if s[i]=='}':depth-=1
        i+=1
    return s[a:i]+'\n'
keys=re.search(r'private static readonly string\[\] GlobalConstDigGameConfigKeys\s*=\s*\{.*?\};',s,re.S).group()
methods=['TryMergeDigGameConfig','TryGetLevelConfigLineMatch','SerializeLevelConfigJson','AppendLevelConfigValue','ReplaceFirst','EscapeJsonString']
(d/'GeneratorProbe.cs').write_text('using System;using System.Collections.Generic;using System.Globalization;using System.Text;using System.Text.RegularExpressions;using Newtonsoft.Json.Linq;\npublic static class GeneratorProbe {\n private const string DigGameConfigKey="diggame_config",GLOBALCONST_JSON_ASSET_PATH="Assets/Game/Configs/lubanconfig_tbglobalconst.json";\n'+keys+'\n public static string[] Keys=>GlobalConstDigGameConfigKeys;\n public static bool Merge(string yaml,JObject global,IReadOnlyList<string> paths,out string result,out string error)=>TryMergeDigGameConfig(yaml,global,paths,"fixture.yaml",out result,out error);\n'+''.join(method(n) for n in methods)+'}',encoding='utf-8')
lib=r/'Library/PackageCache/com.unity.nuget.newtonsoft-json@74deb55db2a0/Runtime/Newtonsoft.Json.dll'
(d/'Generator.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup><Reference Include="Newtonsoft.Json"><HintPath>'+str(lib)+'</HintPath></Reference></ItemGroup></Project>',encoding='utf-8')
manifest={'生成器源码SHA256':hashlib.sha256(s.encode('utf-8')).hexdigest(),'抽取方法':methods,'白名单字段':re.findall(r'"(digGame\w+)"',keys),'证据边界':'执行实际纯合并/序列化方法；不启动Unity菜单、SDK或远端控制台，不写生产YAML。'}
(t/'generator-sources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('当前生成器纯方法和14字段已抽取到隔离目录。')
