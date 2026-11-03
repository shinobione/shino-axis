using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
namespace Axis.Core.Projects;
public sealed class ProjectGithub(ProjectStore store,HttpClient? client=null) {
 public async Task<(int Updated,int Failed)> SyncAsync(CancellationToken cancellation){
  var baseline=store.Snapshot;var projects=J.A(baseline,"projects").OfType<JsonObject>().Where(p=>J.S(p,"repo")!=""&&!J.B(p["control"],"archived",J.B(p,"archived"))).ToArray();
  using var owned=client is null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(20)}:null;var http=client??owned!;int updated=0,failed=0;
  foreach(var p in projects){
   cancellation.ThrowIfCancellationRequested();string id=J.S(p,"id"),repo=J.S(p,"repo"),path=J.S(p,"repoPath"),key=repo+(path!=""?":"+path:"");
   if(J.A(baseline,"sources").OfType<JsonObject>().Any(s=>J.S(s,"projectId")==id&&J.S(s,"type")=="github_repo"&&J.B(s["control"],"archived")))continue;
   if(!System.Text.RegularExpressions.Regex.IsMatch(repo,@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")){failed++;continue;}
   var previous=baseline["settings"]?["githubCursors"]?[key];var cursor=previous?.DeepClone() as JsonObject??new();var rows=new List<JsonObject>();var types=new HashSet<string>();
   async Task<JsonNode?> Get(string suffix,string etagKey,bool optional=false){
    using var req=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/"+repo+suffix);req.Headers.UserAgent.ParseAdd("SHINO-AXIS/0.5");req.Headers.Accept.ParseAdd("application/vnd.github+json");
    string? token=Environment.GetEnvironmentVariable("GITHUB_TOKEN")??Environment.GetEnvironmentVariable("GH_TOKEN");if(!string.IsNullOrEmpty(token))req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
    if(J.S(previous,etagKey)!="")req.Headers.TryAddWithoutValidation("If-None-Match",J.S(previous,etagKey));
    using var response=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,cancellation);
    if(response.StatusCode==HttpStatusCode.NotModified)return null;
    if(optional&&response.StatusCode==HttpStatusCode.NotFound)return new JsonObject{["axisMissing"]=true};response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>4_194_304)throw new IOException();
    using var stream=await response.Content.ReadAsStreamAsync(cancellation);using var buffer=new MemoryStream();var bytes=new byte[8192];
    int read;while((read=await stream.ReadAsync(bytes,cancellation))>0){if(buffer.Length+read>4_194_304)throw new IOException();buffer.Write(bytes,0,read);}
    cursor[etagKey]=response.Headers.ETag?.ToString();buffer.Position=0;return await JsonNode.ParseAsync(buffer,cancellationToken:cancellation);
   }
   try{
    var meta=await Get("","metaEtag");string branch=J.S(meta,"default_branch",J.S(previous,"defaultBranch","main"));cursor["defaultBranch"]=branch;
    var commits=await Get("/commits?per_page=8"+(path!=""?"&path="+Uri.EscapeDataString(path):""),"commitsEtag");
    void Add(string eid,string type,string title,string summary,string timestamp,string url,double confidence){rows.Add(new(){["id"]=eid,["projectId"]=id,["sourceType"]=type,["title"]=title,["summary"]=summary,["timestamp"]=timestamp,["url"]=url,["confidence"]=confidence,["liveSync"]=true});}
    if(commits is JsonArray list){
     types.Add("github_commit");
     foreach(var c in list.OfType<JsonObject>().Take(5))Add("ghc-"+id+"-"+J.Cut(J.S(c,"sha"),16),"github_commit",J.S(c["commit"],"message").Split('\n')[0],J.S(c["commit"],"message"),J.S(c["commit"]?["committer"],"date"),J.S(c,"html_url"),0.92);
     cursor["headSha"]=list.FirstOrDefault()?["sha"]?.DeepClone();
     types.Add("github_truth");
     foreach(var file in path!=""?new[]{path+"/README.md"}:new[]{"PROJECT_STATE.md","PROJECT-STATE.md","ROADMAP.md","README.md"}){
      string etag="truth:"+file;var data=await Get("/contents/"+string.Join("/",file.Split('/').Select(Uri.EscapeDataString))+"?ref="+Uri.EscapeDataString(branch),etag,true);
      string eid="ght-"+id+"-"+J.Norm(file).Replace(' ','-');
      if(data is null){var prior=J.Find(baseline,"evidence",eid);if(prior is not null)rows.Add((JsonObject)prior.DeepClone());continue;}
      if(J.S(data,"content")==""||J.S(data,"encoding")!="base64")continue;
      string text;try{text=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(J.S(data,"content").Replace("\n","")));}catch(FormatException){continue;}
      var lines=text.Split('\n').Select(l=>l.Trim().TrimStart('#').Trim()).Where(l=>l!="").ToArray();
      var signals=lines.Where(l=>System.Text.RegularExpressions.Regex.IsMatch(l,"NEXT|PASS|PENDING|BLOCKED|COMPLETE|ACCEPTED|DO NOT MERGE|SUPERSEDED|REAL USER|PHYSICAL|CURRENT|STATUS|VERSION",System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Take(12).ToArray();
      string summary=J.Cut(string.Join(" · ",signals.Length>0?signals:lines.Take(8)),2200);
      Add(eid,"github_truth",file,summary,J.S(list.FirstOrDefault()?["commit"]?["committer"],"date",DateTimeOffset.UtcNow.ToString("O")),"https://github.com/"+repo+"/blob/"+branch+"/"+file,0.99);
     }
    }
    if(path==""){
     var prs=await Get("/pulls?state=all&sort=updated&direction=desc&per_page=15","prsEtag");
     if(prs is JsonArray listPr){types.Add("github_pr");foreach(var pr in listPr.OfType<JsonObject>().Take(10)){
      Add("ghpr-"+id+"-"+J.S(pr,"number"),"github_pr","PR #"+J.S(pr,"number")+" — "+J.S(pr,"title")+(pr["merged_at"]!=null?" — MERGED":J.B(pr,"draft")?" — DRAFT":""),J.Cut(J.S(pr,"body"),2400),J.S(pr,"updated_at"),J.S(pr,"html_url"),0.97);
      if(System.Text.RegularExpressions.Regex.IsMatch(J.S(pr,"title")+" "+J.S(pr,"body"),@"physical.*pending|live test|needs? test|smoke pending",System.Text.RegularExpressions.RegexOptions.IgnoreCase)){rows[^1]["derivedStatusHint"]="NEEDS TEST";rows[^1]["resumeAction"]="Review/test PR #"+J.S(pr,"number")+": "+J.S(pr,"title");}
     }}
     var runs=await Get("/actions/runs?per_page=8","runsEtag");
     if(runs!=null){types.Add("github_ci");if(runs["workflow_runs"] is JsonArray runList&&runList.FirstOrDefault() is {} run)Add("ghci-"+id+"-"+J.S(run,"id"),"github_ci","Workflow: "+J.S(run,"name"),J.S(run,"status")+" / "+J.S(run,"conclusion","pending"),J.S(run,"updated_at"),J.S(run,"html_url"),0.9);}
    }
    cursor["observedAt"]=DateTimeOffset.UtcNow.ToString("O");
    bool applied=await store.MutateAsync(current=>{
     var live=J.Find(current,"projects",id);if(live is null||J.S(live,"repo")!=repo||J.S(live,"repoPath")!=path)return false;
     var evidence=J.A(current,"evidence");
     foreach(var e in evidence.OfType<JsonObject>().Where(e=>J.S(e,"projectId")==id&&J.B(e,"liveSync")&&types.Contains(J.S(e,"sourceType"))).ToArray())evidence.Remove(e);
     foreach(var e in rows)evidence.Add(e.DeepClone());
     J.O(J.O(current,"settings"),"githubCursors")[key]=cursor.DeepClone();
     var sources=J.A(current,"sources");var source=sources.OfType<JsonObject>().FirstOrDefault(s=>J.S(s,"projectId")==id&&J.S(s,"type")=="github_repo");
     if(source is null){source=new(){["id"]="src-"+id+"-repo",["projectId"]=id,["type"]="github_repo",["title"]=repo,["url"]="https://github.com/"+repo};sources.Add(source);}source["lastObservedAt"]=DateTimeOffset.UtcNow.ToString("O");
     if(types.Count>0)ControlEngine.Derive(current,[id]);return true;
    });if(applied&&types.Count>0)updated++;
   }catch(Exception e)when(e is HttpRequestException or IOException or System.Text.Json.JsonException or TaskCanceledException){if(cancellation.IsCancellationRequested)throw;failed++;}
  }
  return(updated,failed);
 }
}
