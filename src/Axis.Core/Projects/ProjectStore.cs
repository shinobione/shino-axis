using System.Text.Json.Nodes;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Axis.Core.Projects;
public static class J {
 public static string S(JsonNode? n,string key,string fallback="")=>n?[key]?.ToString()??fallback;
 public static bool B(JsonNode? n,string key,bool fallback=false)=>bool.TryParse(S(n,key),out bool value)?value:fallback;
 public static JsonArray A(JsonNode n,string key){if(n[key] is not JsonArray)n[key]=new JsonArray();return (JsonArray)n[key]!;}
 public static JsonObject O(JsonNode n,string key){if(n[key] is not JsonObject)n[key]=new JsonObject();return (JsonObject)n[key]!;}
 public static JsonObject? Find(JsonNode n,string array,string id)=>A(n,array).OfType<JsonObject>().FirstOrDefault(p=>S(p,"id")==id);
 public static string Cut(string text,int length)=>text[..Math.Min(text.Length,length)];
 public static string Clean(string text)=>Regex.Replace(text,@"\s+"," ").Trim();
 public static string Norm(string text)=>Regex.Replace(new string(text.Normalize(NormalizationForm.FormKD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant(),"[^a-z0-9]+"," ").Trim();
 public static string Hash(string value){uint h=2166136261;foreach(char c in value){h^=c;h=unchecked(h*16777619);}const string digits="0123456789abcdefghijklmnopqrstuvwxyz";string result="";do{result=digits[(int)(h%36)]+result;h/=36;}while(h>0);return result;}
 public static DateTimeOffset Date(JsonNode? n,string key)=>DateTimeOffset.TryParse(S(n,key),out var value)?value:DateTimeOffset.MinValue;
}
public record ProjectImport(JsonObject Data,int Projects,int Sources,int Evidence,ImportReview? Review=null);
public sealed class ProjectStore(string directory) {
 readonly string path=Path.Combine(directory,"projects.json");readonly SemaphoreSlim gate=new(1);
 JsonObject state=Empty();bool writable=true;
 public string? Warning {get;private set;}
 public static JsonObject Empty()=>new(){["version"]=1,["projects"]=new JsonArray(),["sources"]=new JsonArray(),["evidence"]=new JsonArray(),["discovered"]=new JsonArray(),["derived"]=new JsonArray(),["settings"]=new JsonObject()};
 public JsonObject Snapshot {get{lock(this)return (JsonObject)state.DeepClone();}}
 public async Task LoadAsync(){
  if(!File.Exists(path))return;
  try{var parsed=await ReadFileAsync(path);lock(this)state=parsed;}
  catch(Exception e)when(e is IOException or System.Text.Json.JsonException or InvalidDataException){writable=false;Warning="Stockage PROJECTS illisible, conservé ; écritures bloquées.";}
 }
 static async Task<JsonObject> ReadFileAsync(string file){
  if(new FileInfo(file).Length>64*1024*1024)throw new InvalidDataException("Fichier trop volumineux.");
  var data=JsonNode.Parse(await File.ReadAllTextAsync(file)) as JsonObject??throw new InvalidDataException("Objet JSON requis.");
  if(data["projects"] is not JsonArray||data["sources"] is not JsonArray||data["evidence"] is not JsonArray)throw new InvalidDataException("Schéma CONTROL incomplet.");
  foreach(string key in new[]{"projects","sources","evidence"}){
   var rows=J.A(data,key).OfType<JsonObject>().ToArray();if(rows.Length!=J.A(data,key).Count||rows.Any(p=>string.IsNullOrWhiteSpace(J.S(p,"id")))||rows.Select(p=>J.S(p,"id")).Distinct().Count()!=rows.Length)throw new InvalidDataException("Identités invalides ou dupliquées.");
  }
  return data;
 }
 public async Task<ProjectImport> PreviewAsync(string file){var data=ProjectImportMerge.Sanitize(await ReadFileAsync(file));var aliases=ProjectResolver.ReadAliases(file);if(aliases.Count>0)J.O(data,"settings")["projectAliases"]=aliases;var review=ProjectImportMerge.Apply(Snapshot,data,false);return new(data,J.A(data,"projects").Count,J.A(data,"sources").Count,J.A(data,"evidence").Count,review);}
 public async Task<T> MutateAsync<T>(Func<JsonObject,T> change){
  await gate.WaitAsync();try{
   if(!writable)throw new IOException(Warning);
   var next=Snapshot;var result=change(next);
   if(JsonNode.DeepEquals(next,state))return result;
   Directory.CreateDirectory(directory);var text=next.ToJsonString(new(){WriteIndented=true});
   await File.WriteAllTextAsync(path+".tmp",text);
   if(File.Exists(path))File.Copy(path,path+".bak",true);
   File.Move(path+".tmp",path,true);lock(this)state=next;return result;
  }finally{gate.Release();}
 }
 public Task<int> ImportAsync(ProjectImport preview)=>MutateAsync(state=>{var result=ProjectImportMerge.Apply(state,ProjectImportMerge.Sanitize(preview.Data));return result.Created+result.Updated;});
 public Task<bool> EditProjectAsync(string id,Action<JsonObject> edit)=>MutateAsync(s=>{var p=J.Find(s,"projects",id)??throw new InvalidDataException("Projet absent.");edit(p);ControlEngine.Derive(s,[id]);return true;});
}
