using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Axis.Core.Projects;
public static class ChatDelta {
 public static JsonObject Ingest(JsonObject state,JsonObject payload){
  string key=J.S(payload,"conversationKey"),url=J.S(payload,"url");
  if(key=="")key=Regex.Match(url,@"/c/([^/?#]+)").Groups[1].Value;
  if(key=="")throw new InvalidDataException("Identité de conversation absente.");
  var messages=(payload["messages"] as JsonArray??new()).OfType<JsonObject>().TakeLast(12).Select(m=>(Role:J.S(m,"role","unknown").ToLowerInvariant(),Text:J.Cut(J.S(m,"text").Replace("\r","").Trim(),16000))).Where(m=>m.Text!="").ToArray();
  if(messages.Length==0||messages.Sum(m=>m.Text.Length)>90000)throw new InvalidDataException("Messages absents ou trop volumineux.");
  var sources=J.A(state,"sources");var projects=J.A(state,"projects");var evidence=J.A(state,"evidence");var settings=J.O(state,"settings");var mappings=J.O(settings,"chatgptProjectMappings");
  var existing=sources.OfType<JsonObject>().FirstOrDefault(s=>new[]{"chatgpt_thread","chatgpt_archived"}.Contains(J.S(s,"type"))&&(J.S(s,"externalId")==key||J.S(s,"url")==url&&url!=""||Regex.Match(J.S(s,"url"),@"/c/([^/?#]+)").Groups[1].Value==key));
  if(existing is not null&&J.B(existing["control"],"archived")){
   bool changed=false;foreach(var archivedEvidence in evidence.OfType<JsonObject>().Where(e=>J.S(e,"sourceId")==J.S(existing,"id")&&!J.B(e,"controlExcluded"))){archivedEvidence["controlExcluded"]=true;changed=true;}
   if(changed)ControlEngine.Derive(state,[J.S(existing,"projectId")]);
   return new(){["ok"]=true,["changed"]=changed,["skipped"]=!changed,["archived"]=true};
  }
  string previous=J.S(existing,"projectId"),projectKey=J.S(payload,"projectKey"),title=J.S(payload,"projectTitle");bool authoritative=false,detached=false;
  JsonObject? project=null;
  if(J.S(existing?["control"],"assignment")=="MANUAL"){project=J.Find(state,"projects",J.S(existing?["control"],"projectId",previous));detached=project is null;}
  else if(settings["manualMappings"] is JsonObject manualMap&&manualMap.ContainsKey(key)){project=J.Find(state,"projects",J.S(manualMap,key));detached=project is null;}
  else{
   var exact=projects.OfType<JsonObject>().Where(p=>J.Norm(J.S(p,"name"))==J.Norm(title)&&title!="").ToArray();
   if(exact.Length==1){project=exact[0];authoritative=true;}
   if(project is null&&projectKey!=""&&mappings[projectKey] is {} mappedId){project=J.Find(state,"projects",mappedId.ToString());authoritative=project!=null;}
   if(project is null&&previous!=""&&(projectKey==""||J.S(existing,"chatgptProjectKey")==""||J.S(existing,"chatgptProjectKey")==projectKey))project=J.Find(state,"projects",previous);
   if(project is null)project=ProjectResolver.Resolve(state,payload,key,string.Join("\n",messages.Select(m=>m.Text)));
   if(project is null&&projectKey!=""&&title!=""){
    string id=J.Norm(title).Replace(' ','-');if(id=="")id="chatgpt-"+J.Hash(projectKey);
    string original=id;int i=1;while(J.Find(state,"projects",id)!=null)id=original+"-"+J.Hash(projectKey)+"-"+i++;
    project=new(){["id"]=id,["name"]=title,["kind"]="CHATGPT_PROJECT"};projects.Add(project);authoritative=true;
   }
   if(project is null&&previous!="")project=J.Find(state,"projects",previous);
   if(project is null&&settings["manualMappings"]?[key] is {} manual)project=J.Find(state,"projects",manual.ToString());
  }
  string transcript=string.Join("\n\n",messages.Select(m=>m.Role.ToUpperInvariant()+": "+m.Text));
  string fingerprint=J.S(payload,"fingerprint",J.Hash(key+"|"+J.S(payload,"messageCount",messages.Length.ToString())+"|"+transcript));
  string projectId=project is null?"":J.S(project,"id");
  if(existing is not null&&J.S(existing,"collectorFingerprint")==fingerprint&&previous==projectId&&J.S(existing,"chatgptStateSchemaVersion")=="2"&&evidence.OfType<JsonObject>().Where(row=>J.S(row,"sourceId")==J.S(existing,"id")).All(row=>J.S(row,"projectId")==projectId&&J.B(row,"controlExcluded")==detached))return new(){["ok"]=true,["changed"]=false,["skipped"]=true,["projectId"]=projectId};

  if(project is null&&!detached){
   var discovered=J.A(state,"discovered");string did="discovered-chat-"+J.Hash(key);var row=discovered.OfType<JsonObject>().FirstOrDefault(d=>J.S(d,"id")==did);
   if(row is not null&&J.S(row,"collectorFingerprint")==fingerprint)return new(){["ok"]=true,["changed"]=false,["skipped"]=true,["discovered"]=true};
   if(row is null){row=new(){["id"]=did};discovered.Add(row);}row["collectorFingerprint"]=fingerprint;
   row["type"]="chatgpt_thread";row["title"]=J.S(payload,"title");row["url"]=url;row["conversationKey"]=key;row["observedAt"]=DateTimeOffset.UtcNow.ToString("O");
   return new(){["ok"]=true,["changed"]=true,["mapped"]=false,["discovered"]=true,["discoveredId"]=did};
  }
  var now=DateTimeOffset.UtcNow.ToString("O");string observed=J.S(payload,"conversationUpdatedAt",J.S(payload,"clientTimestamp",now));
  if(!DateTimeOffset.TryParse(observed,out _))observed=now;
  var source=existing??new JsonObject{["id"]="src-chat-"+J.Hash(key)};if(existing is null)sources.Add(source);
  source["projectId"]=projectId==""?null:projectId;source["type"]="chatgpt_thread";source["externalId"]=key;source["title"]=J.S(payload,"title","ChatGPT conversation");source["url"]=url;source["state"]=detached?"DETACHED":"COLLECTED";source["inventoryCurrent"]=true;source["collectorFingerprint"]=fingerprint;source["chatgptStateSchemaVersion"]=2;source["lastObservedAt"]=now;source["collectorObservedAt"]=now;source["collectorMessageCount"]=payload["messageCount"]?.DeepClone()??JsonValue.Create(messages.Length);source["conversationUpdatedAt"]=observed;source["chatgptProjectKey"]=projectKey;source["chatgptProjectTitle"]=title;
  if(authoritative&&projectKey!="")mappings[projectKey]=projectId;
  var affected=new HashSet<string>(new[]{previous,projectId}.Where(id=>id!=""));
  foreach(var priorEvidence in evidence.OfType<JsonObject>().Where(row=>J.S(row,"sourceId")==J.S(source,"id"))){
   if(J.S(priorEvidence,"projectId")!="")affected.Add(J.S(priorEvidence,"projectId"));
   priorEvidence["projectId"]=projectId==""?null:JsonValue.Create(projectId);priorEvidence["controlExcluded"]=detached;
  }
  var semantic=messages.SelectMany((m,i)=>m.Text.Split('\n').Select(t=>(m.Role,Text:J.Clean(t),Index:i))).Where(x=>x.Text.Length is >0 and <=900&&!Regex.IsMatch(x.Text,@"^(```|powershell|PS [a-z]:\\|Get-|Set-|npm |node |git )|ConvertFrom-Json|collectorFingerprint",RegexOptions.IgnoreCase)).ToArray();
  int last=semantic.LastOrDefault().Index;int start=semantic.LastOrDefault().Role=="user"?last:Math.Max(0,last-1);
  string summary=J.Cut(string.Join(" · ",semantic.Where(s=>s.Index>=start).TakeLast(5).Select(s=>s.Role.ToUpperInvariant()+": "+s.Text)),700);
  int assistant=semantic.Where(s=>s.Role=="assistant").Select(s=>s.Index).DefaultIfEmpty(last).Max();
  var actions=semantic.Where(s=>s.Index==assistant).Reverse().ToArray();
  var action=actions.FirstOrDefault(s=>Regex.IsMatch(s.Text,@"^(NEXT|TODO|ACTION|RESUME)\s*[:—-]",RegexOptions.IgnoreCase));
  if(action.Text is null)action=actions.FirstOrDefault(s=>s.Text.Length<=420&&Regex.IsMatch(s.Text,@"next|resume|test|pending|todo|remaining|must|verify|run|implement|build|fix|continue|check",RegexOptions.IgnoreCase));
  string resume=action.Text is null?"Continue in the synced ChatGPT thread.":J.Cut(Regex.Replace(action.Text,@"^(NEXT|TODO|ACTION|RESUME)\s*[:—-]\s*","",RegexOptions.IgnoreCase),700);
  string eid="chat-current-"+J.Hash(key);var e=J.Find(state,"evidence",eid);if(e is null){e=new(){["id"]=eid};evidence.Add(e);}
  e["projectId"]=projectId==""?null:projectId;e["sourceId"]=J.S(source,"id");e["sourceType"]="chatgpt_thread";e["type"]="chat_delta";e["timestamp"]=observed;e["conversationUpdatedAt"]=observed;e["title"]=J.S(source,"title");e["summary"]=summary;e["currentStateSummary"]=summary;e["resumeAction"]=resume;e["url"]=url;e["confidence"]=0.9;e["inventoryCurrent"]=true;e["controlExcluded"]=detached||J.B(source["control"],"archived");e["collectorFingerprint"]=fingerprint;
  foreach(var d in J.A(state,"discovered").OfType<JsonObject>().Where(d=>J.S(d,"conversationKey")==key).ToArray())J.A(state,"discovered").Remove(d);
  J.O(state,"settings")["lastChatgptCollector"]=new JsonObject{["at"]=now,["changed"]=true,["mapped"]=!detached,["sourceId"]=J.S(source,"id")};
  ControlEngine.Derive(state,affected);
  return new(){["ok"]=true,["changed"]=true,["mapped"]=!detached,["detached"]=detached,["projectId"]=projectId,["sourceId"]=J.S(source,"id"),["evidenceId"]=eid,["conversationKey"]=key};
 }
}
