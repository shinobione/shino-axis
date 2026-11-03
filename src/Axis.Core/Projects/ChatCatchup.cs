using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Axis.Core.Projects;
// Native implementation of the active CONTROL collector wire contract. No browser API access here.
public static class ChatCatchup {
 static DateTimeOffset Time(string text)=>DateTimeOffset.TryParse(text,out var t)?t:DateTimeOffset.MinValue;
 static int Count(JsonNode? n,string key,int fallback=0)=>int.TryParse(J.S(n,key),out int v)?Math.Clamp(v,0,100000):fallback;
 public static string Key(JsonNode source)=>J.S(source,"externalId",J.S(source,"conversationKey")) is {Length:>0} key?key:Regex.Match(J.S(source,"url"),@"/c/([^/?#]+)",RegexOptions.IgnoreCase).Groups[1].Value;
 public static JsonObject Plan(JsonObject state,JsonObject inventory,DateTimeOffset? clock=null){
  var now=clock??DateTimeOffset.UtcNow;int limit=Math.Clamp(Count(inventory,"maxPlan",32),1,100);
  var threads=new Dictionary<string,JsonObject>();
  foreach(var raw in (inventory["threads"] as JsonArray??new()).OfType<JsonObject>().Take(1000)){
   string key=J.S(raw,"key",J.S(raw,"conversationKey",J.S(raw,"id")));if(key=="")key=Key(raw);if(key==""||key.Length>200)continue;
   var t=new JsonObject{["key"]=key,["title"]=J.Cut(J.S(raw,"title",J.S(raw,"name","ChatGPT conversation")),180),["url"]=J.S(raw,"url"),["projectKey"]=J.S(raw,"projectKey"),["projectTitle"]=J.S(raw,"projectTitle"),["projectUrl"]=J.S(raw,"projectUrl"),["updatedAt"]=J.S(raw,"updatedAt",J.S(raw,"update_time",J.S(raw,"updated_at"))),["createdAt"]=J.S(raw,"createdAt",J.S(raw,"create_time",J.S(raw,"created_at")))};
   if(!threads.TryGetValue(key,out var old)||Time(J.S(t,"updatedAt"))>=Time(J.S(old,"updatedAt")))threads[key]=t;
  }
  var sources=J.A(state,"sources").OfType<JsonObject>().Where(s=>J.S(s,"type") is "chatgpt_thread" or "chatgpt_archived").GroupBy(Key).ToDictionary(g=>g.Key,g=>g.First());
  int known=0,unchanged=0,missing=0,baseline=0,mismatches=0,upgrades=0,inaccessible=0,quarantined=0,newCount=0;
  var changed=new List<JsonObject>();
  foreach(var t in threads.Values){
   string key=J.S(t,"key");sources.TryGetValue(key,out var source);if(source is not null)known++;
   if(J.B(source?["control"],"archived")||J.S(source?["control"],"assignment")=="MANUAL"&&J.S(source,"projectId")==""){unchanged++;continue;}
   var remote=Time(J.S(t,"updatedAt"));var blocked=state["settings"]?["chatgptInaccessible"]?[key];
   if(blocked is not null&&remote==Time(J.S(blocked,"remoteUpdatedAt"))){inaccessible++;continue;}
   var timeout=state["settings"]?["chatgptTimeoutQuarantine"]?[key];var marked=Time(J.S(timeout,"remoteUpdatedAt"));
   if(timeout is not null&&Time(J.S(timeout,"retryAt"))>now&&(remote==DateTimeOffset.MinValue||remote==marked)){quarantined++;continue;}
   string reason="";var local=Time(J.S(source,"conversationUpdatedAt"));
   foreach(var e in J.A(state,"evidence").OfType<JsonObject>().Where(e=>J.B(e,"inventoryCurrent",true)&&(source is not null&&J.S(e,"sourceId")==J.S(source,"id")||Key(e)==key)))local=local>Time(J.S(e,"conversationUpdatedAt"))?local:Time(J.S(e,"conversationUpdatedAt"));
   if(source is null){reason="new-conversation";newCount++;}
   else{
    string expected="";var exact=J.A(state,"projects").OfType<JsonObject>().Where(p=>J.Norm(J.S(p,"name"))==J.Norm(J.S(t,"projectTitle"))&&J.S(t,"projectTitle")!="").ToArray();
    if(exact.Length==1)expected=J.S(exact[0],"id");else expected=J.S(state["settings"]?["chatgptProjectMappings"],J.S(t,"projectKey"));
    if(J.S(source["control"],"assignment")!="MANUAL"&&!(state["settings"]?["manualMappings"] as JsonObject??new()).ContainsKey(key)&&expected!=""&&expected!=J.S(source,"projectId")){reason=exact.Length==1?"project-ownership-mismatch":"project-key-mismatch";mismatches++;t["expectedProjectId"]=expected;t["currentProjectId"]=source["projectId"]?.DeepClone();}
    else if(J.S(source,"chatgptStateSchemaVersion")!="2"){reason="state-schema-upgrade";upgrades++;}
    else if(J.S(source,"projectId")!=""&&(J.Find(state,"projects",J.S(source,"projectId")) is null||!J.A(state,"evidence").OfType<JsonObject>().Any(e=>J.S(e,"sourceId")==J.S(source,"id")&&J.B(e,"inventoryCurrent",true)&&!J.B(e,"controlExcluded")))){reason="local-reference-missing";baseline++;}
    else if(remote==DateTimeOffset.MinValue){missing++;unchanged++;continue;}
    else if(local==DateTimeOffset.MinValue){reason="baseline-missing";baseline++;}
    else if(remote>local.AddMilliseconds(1500))reason="remote-newer";
    else{unchanged++;continue;}
   }
   t["reason"]=reason;t["localUpdatedAt"]=local==DateTimeOffset.MinValue?null:local.ToString("O");
   if(timeout is not null){t["slowRetry"]=true;t["timeoutAttempts"]=Count(timeout,"attempts");}
   changed.Add(t);
  }
  return new(){["ok"]=true,["inventoryCount"]=threads.Count,["known"]=known,["unchanged"]=unchanged,["inaccessibleCount"]=inaccessible,["timeoutQuarantinedCount"]=quarantined,["remoteWithoutTimestamp"]=missing,["baselineMissing"]=baseline,["ownershipMismatches"]=mismatches,["stateSchemaUpgrades"]=upgrades,["changedCount"]=changed.Count,["newCount"]=newCount,["deferredCount"]=Math.Max(0,changed.Count-limit),["plan"]=new JsonArray(changed.OrderByDescending(t=>Time(J.S(t,"updatedAt"))).Take(limit).Select(t=>(JsonNode)t).ToArray())};
 }
 public static JsonObject Report(JsonObject state,JsonObject payload,DateTimeOffset? clock=null){
  var now=clock??DateTimeOffset.UtcNow;var settings=J.O(state,"settings");var inaccessible=J.O(settings,"chatgptInaccessible");var quarantine=J.O(settings,"chatgptTimeoutQuarantine");
  JsonObject Item(JsonObject raw,string kind)=>new(){["key"]=J.Cut(J.S(raw,"key"),120),["title"]=J.Cut(J.Clean(J.S(raw,"title")),180),["kind"]=kind,["error"]=kind,["remoteUpdatedAt"]=J.S(raw,"updatedAt",J.S(raw,"remoteUpdatedAt")),["observedAt"]=now.ToString("O")};
  var unavailable=new JsonArray();
  foreach(var raw in (payload["inaccessible"] as JsonArray??new()).OfType<JsonObject>().Take(20)){var row=Item(raw,"inaccessible");if(J.S(row,"key")=="")continue;inaccessible[J.S(row,"key")]=row.DeepClone();unavailable.Add(row);}
  var failures=new JsonArray();
  foreach(var raw in (payload["failures"] as JsonArray??new()).OfType<JsonObject>().Take(10)){
   bool timed=Regex.IsMatch(J.S(raw,"error"),@"CHATGPT_FETCH_TIMEOUT_\d+MS",RegexOptions.IgnoreCase)||J.S(raw,"kind")=="timeout";
   bool limited=Regex.IsMatch(J.S(raw,"error"),@"\b429\b|rate.?limit",RegexOptions.IgnoreCase);
   var row=Item(raw,timed?"timeout":limited?"rate_limited":"transient");failures.Add(row);
   string key=J.S(row,"key");if(key==""||!timed&&!limited)continue;
   var previous=quarantine[key];int attempts=previous is not null&&Time(J.S(previous,"remoteUpdatedAt"))==Time(J.S(row,"remoteUpdatedAt"))?Count(previous,"attempts")+1:1;
   row=(JsonObject)row.DeepClone();row["attempts"]=attempts;row["retryAt"]=now.AddMinutes(new[]{30,120,360,1440}[Math.Min(3,attempts-1)]).ToString("O");quarantine[key]=row;
  }
  // Bound stale retry metadata without retaining any raw failure messages.
  foreach(var pair in quarantine.ToArray())if(Time(J.S(pair.Value,"retryAt"))<now.AddDays(-30))quarantine.Remove(pair.Key);
  var active=quarantine.Where(p=>Time(J.S(p.Value,"retryAt"))>now).Take(20).Select(p=>{var row=(JsonObject)p.Value!.DeepClone();row["key"]=p.Key;return (JsonNode)row;}).ToArray();
  var result=new JsonObject{["at"]=now.ToString("O"),["source"]="control-collector-api-metadata",["status"]=Count(payload,"failedCount")>0||Count(payload,"deferredCount")>0?"PARTIAL":"HEALTHY",["failures"]=failures,["inaccessible"]=unavailable,["quarantinedCount"]=quarantine.Count(p=>Time(J.S(p.Value,"retryAt"))>now),["quarantined"]=new JsonArray(active)};
  foreach(string key in new[]{"inventoryCount","knownCount","unchangedCount","changedCount","newCount","baselineMissingCount","inaccessibleCount","plannedCount","refreshedCount","skippedCount","failedCount","deferredCount"})result[key]=Count(payload,key);
  settings["lastChatgptCatchup"]=result.DeepClone();result["ok"]=true;return result;
 }
}
