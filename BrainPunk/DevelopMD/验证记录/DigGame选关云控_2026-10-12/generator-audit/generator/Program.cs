using System;using System.IO;using System.Linq;using System.Globalization;using System.Text.RegularExpressions;using Newtonsoft.Json.Linq;
public static class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
 static string Root="D:/GitStore/BrinPunk/BrainPunk/";
 static JObject Dig(string yaml)=>JObject.Parse(Regex.Match(yaml,@"^    diggame_config: '(.*)'\r?$",RegexOptions.Multiline).Groups[1].Value);
 public static void Main(){
  var global=(JObject)JArray.Parse(File.ReadAllText(Root+"Assets/Game/Configs/lubanconfig_tbglobalconst.json"))[0];
  var sorts=JArray.Parse(File.ReadAllText(Root+"Assets/Game/Configs/tbdiglevelsort.json"));
  var paths=sorts.Select(x=>(string)x["path"]).ToList();
  string yaml=File.ReadAllText(Root+"Assets/Guru/guru_spec.yaml");var old=Dig(yaml);
  Check(GeneratorProbe.Keys.Length==14&&GeneratorProbe.Keys.Distinct().Count()==14,"Dig生成白名单14个唯一字段");
  Check(GeneratorProbe.Merge(yaml,global,paths,out var output,out _),"当前Guru YAML实际合并成功");var actual=Dig(output);
  foreach(var key in GeneratorProbe.Keys)Check(JToken.DeepEquals(actual[key],global[key]),"同步Global字段 "+key);
  Check(JToken.DeepEquals(actual,old),"重生成与生产Dig默认值语义一致");
  Check(((JArray)actual["level_sort"]).Count==30&&((JArray)actual["level_sort"]).All(x=>x.Type==JTokenType.Integer),"30项排序保持整数类型");
  Check(output.Split('\n').Where(x=>!x.TrimStart().StartsWith("diggame_config:")).SequenceEqual(yaml.Split('\n').Where(x=>!x.TrimStart().StartsWith("diggame_config:"))),"非Dig YAML各行保持不变");
  string extend=yaml.Replace(Regex.Match(yaml,@"^    diggame_config:.*$",RegexOptions.Multiline).Value,"    diggame_config: '{\"enabled\":true,\"custom_retained\":23,\"digGame_gameTipsPopupCoinCost\":999}'");
  Check(GeneratorProbe.Merge(extend,global,paths,out output,out _),"已有扩展字段候选合并成功");actual=Dig(output);
  Check((bool)actual["enabled"]&&(int)actual["custom_retained"]==23,"保留已有enabled与未知扩展字段");
  Check((int)actual["digGame_gameTipsPopupCoinCost"]==(int)global["digGame_gameTipsPopupCoinCost"],"已知字段更新为本地默认");
  var missing=(JObject)global.DeepClone();missing.Remove(GeneratorProbe.Keys[4]);
  Check(!GeneratorProbe.Merge(yaml,missing,paths,out output,out var error)&&output==yaml&&error.Contains(GeneratorProbe.Keys[4]),"缺字段拒绝且不部分改写");
  string bad="remote_config:\n  values:\n    level_config: '{}'\n    diggame_config: '{bad}'";
  Check(!GeneratorProbe.Merge(bad,global,paths,out output,out error)&&output==bad&&!string.IsNullOrEmpty(error),"旧Dig JSON错误拒绝且保留原内容");
  string noDig="remote_config:\n  values:\n    level_config: '{}'\n";
  Check(GeneratorProbe.Merge(noDig,global,paths,out output,out _)&&(bool)Dig(output)["enabled"]==false,"没有Dig key时插入完整独立配置");
  Check(!GeneratorProbe.Merge("other: value",global,paths,out output,out _)&&output=="other: value","没有合法锚点拒绝合并");
  var fraction=(JObject)global.DeepClone();fraction["digGameShowTipsTimeLimit"]=1.75;CultureInfo.CurrentCulture=new CultureInfo("fr-FR");
  Check(GeneratorProbe.Merge(yaml,fraction,paths,out output,out _)&&(double)Dig(output)["digGameShowTipsTimeLimit"]==1.75,"非英语地区浮点JSON仍用点号");
  Console.WriteLine($"PASS {checks} generator assertions; actual source methods, no production writes.");
 }
}
