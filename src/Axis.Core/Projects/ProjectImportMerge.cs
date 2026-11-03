using System.Text.Json.Nodes;
namespace Axis.Core.Projects;
public sealed record ImportReview(int Created,int Updated,int Conflicts,IReadOnlyList<string> ConflictFields);
public static class ProjectImportMerge {
 static readonly string[] Arrays=["projects","sources","evidence","discovered","derived"];
 public static JsonObject Sanitize(JsonObject input){
  var copy=new JsonObject();
  foreach(string key in Arrays)copy[key]=input[key]?.DeepClone()??new JsonArray();
  copy["version"]=input["version"]?.DeepClone()??JsonValue.Create(1);
  var settings=new JsonObject();copy["settings"]=settings;
  foreach(var pair in input["settings"] as JsonObject??new())
   if(pair.Key is "projectAliases" or "githubRepos" or "manualMappings" or "chatgptProjectMappings" or "lastChatgptInventory" or "lastChatgptCollector" or "lastChatgptCatchup" or "chatgptInaccessible" or "chatgptTimeoutQuarantine" or "githubCursors" or "lastGithubSync")settings[pair.Key]=pair.Value?.DeepClone();
  void Strip(JsonNode? n){
   if(n is JsonObject o)foreach(var key in o.Select(p=>p.Key).ToArray()){
    if(new[]{"transcript","transcriptText","messages","rawMessages","rawTranscript","conversation","conversationJson","conversationData","rawPayload","raw","content","body","prompt","response","output","mapping","accessToken","token","authorization","apiKey"}.Contains(key,StringComparer.OrdinalIgnoreCase))o.Remove(key);else Strip(o[key]);
   }else if(n is JsonArray a)foreach(var row in a)Strip(row);
  }
  Strip(copy);return copy;
 }
 public static ImportReview Apply(JsonObject target,JsonObject incoming,bool record=true){
  var baseline=target["axisImportBaseline"] as JsonObject??new JsonObject();int created=0,updated=0,conflicts=0;var details=new List<string>();
  void Merge(JsonObject local,JsonObject source,JsonObject? previous,string path){
   foreach(var pair in source){
    string field=path+"."+pair.Key;var old=previous?[pair.Key];var current=local[pair.Key];
    if(JsonNode.DeepEquals(current,pair.Value))continue;
    if(!local.ContainsKey(pair.Key)&&previous?.ContainsKey(pair.Key)==true){if(!JsonNode.DeepEquals(pair.Value,old)){conflicts++;if(details.Count<40)details.Add(field+" (suppression locale conservée)");}continue;}
    if(!local.ContainsKey(pair.Key)||previous?.ContainsKey(pair.Key)==true&&JsonNode.DeepEquals(current,old)){local[pair.Key]=pair.Value?.DeepClone();continue;}
    if(current is JsonObject a&&pair.Value is JsonObject b){Merge(a,b,old as JsonObject,field);continue;}
    if(previous?.ContainsKey(pair.Key)==true&&JsonNode.DeepEquals(pair.Value,old))continue;
    conflicts++;if(details.Count<40)details.Add(field);
   }
  }
  foreach(string name in Arrays){
   var rows=J.A(target,name);var prior=baseline[name] as JsonArray??new();
   foreach(var item in (incoming[name] as JsonArray??new()).OfType<JsonObject>()){
    string key=name=="derived"?"projectId":"id",id=J.S(item,key);if(id=="")continue;
    var existing=rows.OfType<JsonObject>().FirstOrDefault(p=>J.S(p,key)==id);
    if(existing is null){if(prior.OfType<JsonObject>().Any(p=>J.S(p,key)==id)){conflicts++;if(details.Count<40)details.Add(name+"["+id+"] (suppression locale conservée)");continue;}rows.Add(item.DeepClone());created++;continue;}
    var before=existing.ToJsonString();Merge(existing,item,prior.OfType<JsonObject>().FirstOrDefault(p=>J.S(p,key)==id),name+"["+id+"]");
    if(before!=existing.ToJsonString())updated++;
   }
  }
  Merge(J.O(target,"settings"),incoming["settings"] as JsonObject??new(),baseline["settings"] as JsonObject,"settings");
  var dirty=new HashSet<string>();
  foreach(var source in J.A(target,"sources").OfType<JsonObject>()){
   string sid=J.S(source,"id"),oldId=J.S(source,"projectId"),wanted=oldId;
   bool manual=J.S(source["control"],"assignment")=="MANUAL",archived=J.B(source["control"],"archived");
   if(manual)wanted=J.S(source["control"],"projectId",oldId);
   else if(target["settings"]?["manualMappings"] is JsonObject mappings&&mappings.ContainsKey(ChatCatchup.Key(source))){wanted=J.S(mappings,ChatCatchup.Key(source));manual=true;}
   if(manual&&wanted!=""&&J.Find(target,"projects",wanted) is null)wanted="";
   if(manual&&oldId!=wanted){source["projectId"]=wanted==""?null:JsonValue.Create(wanted);dirty.Add(oldId);dirty.Add(wanted);}
   if(!manual&&!archived)continue;
   foreach(var evidence in J.A(target,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"sourceId")==sid)){
    bool excluded=archived||wanted=="";
    if(J.S(evidence,"projectId")!=wanted||J.B(evidence,"controlExcluded")!=excluded){
     dirty.Add(J.S(evidence,"projectId"));dirty.Add(wanted);evidence["projectId"]=wanted==""?null:JsonValue.Create(wanted);evidence["controlExcluded"]=excluded;updated++;
    }
   }
  }
  if(created+updated>0)foreach(var project in J.A(target,"projects").OfType<JsonObject>().Where(p=>J.S(p["control"],"statusOverride")!=""))dirty.Add(J.S(project,"id"));
  foreach(var row in J.A(target,"derived").OfType<JsonObject>().Where(d=>J.S(d,"status")=="ACTIVE"&&J.S(d,"statusSource")!="manual_override"&&DateTimeOffset.UtcNow-J.Date(d,"lastMovementAt")>TimeSpan.FromDays(60))){
   row["lastKnownStatus"]="ACTIVE";row["status"]="UNSYNCED";row["statusSource"]="expired_evidence";row["freshness"]="STALE";row["statusReason"]="Dernière preuve trop ancienne pour attester une activité actuelle.";
  }
  if(dirty.Count>0)ControlEngine.Derive(target,dirty.Where(id=>id!=""));
  if(record&&(created>0||updated>0||!JsonNode.DeepEquals(baseline,incoming))){target["axisImportBaseline"]=incoming.DeepClone();target["axisLastImport"]=new JsonObject{["at"]=DateTimeOffset.UtcNow.ToString("O"),["created"]=created,["updated"]=updated,["conflicts"]=conflicts};}
  return new(created,updated,conflicts,details);
 }
}
