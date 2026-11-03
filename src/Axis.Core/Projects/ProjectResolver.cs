using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Axis.Core.Projects;
public static class ProjectResolver {
 public static JsonObject? Resolve(JsonObject state,JsonObject payload,string key,string transcript){
  var projects=J.A(state,"projects").OfType<JsonObject>().Where(p=>!J.B(p["control"],"archived")).ToArray();
  string title=J.Norm(J.S(payload,"title")),owner=J.Norm(J.S(payload,"projectTitle")),body=J.Norm(transcript),raw=J.S(payload,"title")+"\n"+transcript;
  int Occurs(string text,string alias){int count=0,pos=0;while((pos=text.IndexOf(alias,pos,StringComparison.Ordinal))>=0&&count<8){count++;pos+=alias.Length;}return count;}
  var scored=projects.Select(p=>{
   var aliases=new[]{J.S(p,"name"),J.S(p,"repo"),J.S(p,"repo").Split('/').Last()}.Concat((state["settings"]?["projectAliases"]?[J.S(p,"id")] as JsonArray??new()).Select(x=>x?.ToString()??"")).Where(x=>x!="");
   int score=0,ts=0;
   foreach(string a in aliases){string alias=J.Norm(a);if(alias.Length<3)continue;bool generic=alias.Length<=5||new[]{"music","studio","riso","astea","launchpad"}.Contains(alias);
    if(title==alias)ts=Math.Max(ts,generic?115:145);else if(title.Contains(alias,StringComparison.Ordinal))ts=Math.Max(ts,generic?82:118);
    if(owner==alias)score+=generic?72:96;else if(owner.Contains(alias,StringComparison.Ordinal))score+=generic?46:70;
    int n=Occurs(body,alias);if(n>0){int v=(generic?5:12)*Math.Min(n,5);if(J.S(p,"repo")!=""&&J.Norm(J.S(p,"repo"))==alias)v=Math.Max(v,82);else if(J.S(p,"repo")!=""&&J.Norm(J.S(p,"repo").Split('/').Last())==alias)v=Math.Max(v,68);score+=v;}
   }
   if(J.S(p,"repo")!=""&&raw.Contains("github.com/"+J.S(p,"repo"),StringComparison.OrdinalIgnoreCase))score+=95;
   return(P:p,Score:score+ts);
  }).OrderByDescending(x=>x.Score).ToArray();
  // CONTROL scoring retained; equal best scores remain unclassified rather than depend on list order.
  return scored.Length>0&&scored[0].Score>=55&&(scored.Length<2||scored[0].Score>scored[1].Score)?scored[0].P:null;
 }
 // Import only the constant alias table beside a selected CONTROL state. Never execute the JS.
 public static JsonObject ReadAliases(string stateFile){
  string? root=Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(stateFile)));var aliases=new JsonObject();
  string file=Path.Combine(root??"","lib","resolver.mjs");if(!File.Exists(file)||new FileInfo(file).Length>100000)return aliases;
  string text=File.ReadAllText(file);int start=text.IndexOf("export const PROJECT_ALIASES = {",StringComparison.Ordinal),end=start<0?-1:text.IndexOf("};",start,StringComparison.Ordinal);if(end<0)return aliases;
  foreach(Match m in Regex.Matches(text[start..end],"""['"](?<key>[^'"]+)['"]\s*:\s*\[(?<values>[^\]]*)\]""")){
   var rows=new JsonArray();foreach(Match value in Regex.Matches(m.Groups["values"].Value,"""
'(?<single>(?:\\.|[^'\\])*)'|"(?<double>(?:\\.|[^"\\])*)"
""")){
    string v=value.Groups["single"].Success?value.Groups["single"].Value:value.Groups["double"].Value;rows.Add(v.Replace("\\'","'").Replace("\\\"","\""));
   }aliases[m.Groups["key"].Value]=rows;
  }return aliases;
 }
}
