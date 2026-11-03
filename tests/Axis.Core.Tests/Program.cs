using Axis.Core.Presentation;
using System.Text.Json.Nodes;
using Axis.Core.Projects;
using Axis.Core.Missions;
using Axis.Core;
using Axis.Core.Modules;

if(args.Contains("--codex-live-timing"))return await CodexLiveTiming.RunAsync();

if(args.Contains("--live-github")){
 var store=new ProjectStore(Path.Combine(Path.GetTempPath(),"axis-live-github-"+Guid.NewGuid().ToString("N")));await store.LoadAsync();
 var fixture=ProjectStore.Empty();J.A(fixture,"projects").Add(new JsonObject{["id"]="public-validation",["name"]="Fixture publique CONTROL",["repo"]="shinobione/shinobione-shino-control"});
 await store.ImportAsync(new(fixture,1,0,0));var result=await new ProjectGithub(store).SyncAsync(CancellationToken.None);
 int count=J.A(store.Snapshot,"evidence").Count;
 Console.WriteLine($"Live GitHub public fixture: changed={result.Updated}, failed={result.Failed}, evidence={count}");
 var again=await new ProjectGithub(store).SyncAsync(CancellationToken.None);int after=J.A(store.Snapshot,"evidence").Count;Console.WriteLine($"Live GitHub second sync: changed={again.Updated}, failed={again.Failed}, evidence={after}, duplicates={after-count}");return result.Failed==0&&again.Failed==0&&count>0&&after==count?0:1;
}
if(args.Contains("--control-preview")||args.Contains("--control-compare")){
 int at=Array.IndexOf(args,"--source");string source=args[at+1];string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SHINO","AXIS");
 var store=new ProjectStore(profile);await store.LoadAsync();var preview=await store.PreviewAsync(source);var r=preview.Review!;
 Console.WriteLine($"CONTROL preview: projects={preview.Projects}, sources={preview.Sources}, evidence={preview.Evidence}; create={r.Created}, update={r.Updated}, conflicts={r.Conflicts}; aliases={((preview.Data["settings"]?["projectAliases"]) as JsonObject)?.Count??0}");
 var ids=J.A(preview.Data,"projects").OfType<JsonObject>().Select(p=>J.S(p,"id")).ToHashSet();
 var second=ProjectImportMerge.Apply(store.Snapshot,preview.Data,false);Console.WriteLine($"Current-profile reimport review: create={second.Created}, update={second.Updated}, conflicts={second.Conflicts}");
 Console.WriteLine($"Relationships: orphanSources={J.A(preview.Data,"sources").OfType<JsonObject>().Count(s=>J.S(s,"projectId")!=""&&!ids.Contains(J.S(s,"projectId")))}, orphanEvidence={J.A(preview.Data,"evidence").OfType<JsonObject>().Count(e=>J.S(e,"projectId")!=""&&!ids.Contains(J.S(e,"projectId")))}");
 if(args.Contains("--control-compare")){
  var expected=(JsonArray)JsonNode.Parse(await File.ReadAllTextAsync(args[Array.IndexOf(args,"--expected")+1]))!;
  var copy=(JsonObject)preview.Data.DeepClone();ControlEngine.Derive(copy);
  int status=0,resume=0,action=0,normalizedActions=0;var actionKinds=new Dictionary<string,int>();var resumeDifferences=new Dictionary<string,int>();var mismatchKinds=new Dictionary<string,int>();
  foreach(var want in expected.OfType<JsonObject>()){
   var got=J.A(copy,"derived").OfType<JsonObject>().FirstOrDefault(d=>J.S(d,"projectId")==J.S(want,"projectId"));if(got is null)continue;
   if(J.S(got,"status")==J.S(want,"status"))status++;else{string kind=J.S(want,"status")+" -> "+J.S(got,"status");mismatchKinds[kind]=mismatchKinds.GetValueOrDefault(kind)+1;}
   if(J.S(got,"resumeSource")==J.S(want,"resumeSource"))resume++;else{string kind=J.S(want,"resumeSource")+" -> "+J.S(got,"resumeSource")+" / same evidence="+(J.S(want,"resumeEvidenceId")==J.S(got,"resumeEvidenceId"));resumeDifferences[kind]=resumeDifferences.GetValueOrDefault(kind)+1;}
   if(J.Norm(J.S(got,"nextAction"))==J.Norm(J.S(want,"nextAction")))normalizedActions++;if(J.S(got,"nextAction")==J.S(want,"nextAction"))action++;else{string kind=J.S(want,"resumeSource");actionKinds[kind]=actionKinds.GetValueOrDefault(kind)+1;}
   if(J.S(want,"resumeSource") is "chat_resume_action" or "chat_state_summary"&&J.S(got,"resumeSource")=="title_heuristic"){
    string candidate=J.S(want,"nextAction").Split(" — ",2).Last().TrimEnd('.');
    var method=typeof(ControlEngine).GetMethod("ResumeScore",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
    Console.WriteLine($"Resume diagnostic only: chars={candidate.Length}, score={method.Invoke(null,[candidate])}, inResume={J.S(J.Find(copy,"evidence",J.S(want,"resumeEvidenceId")),"resumeAction").Contains(candidate)}");
   }
  }
  Console.WriteLine($"Fresh derivation comparison ({expected.Count} CONTROL visible projects): statusEqual={status}, resumeSourceEqual={resume}, nextActionEqual={action}, normalizedActionEqual={normalizedActions}, actionDifferences={System.Text.Json.JsonSerializer.Serialize(actionKinds)}; differences={System.Text.Json.JsonSerializer.Serialize(mismatchKinds)}; resumeDifferences={System.Text.Json.JsonSerializer.Serialize(resumeDifferences)}");
 }
 return 0;
}
int passed=0;
var failures=new List<string>();
async Task Test(string name,Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch(Exception e) { failures.Add(name); Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Check(bool condition,string message="Assertion failed") { if(!condition)throw new Exception(message); }
var epoch=new DateTimeOffset(2030,1,1,12,0,0,TimeSpan.Zero);

await Test("Start and stop are idempotent; stopped modules do not publish",async()=> {
    var module=new FakeModule(); var runtime=new ModuleRuntime([module]);
    await runtime.SetEnabledAsync("fake",true,epoch); await runtime.SetEnabledAsync("fake",true,epoch);
    Check(module.Starts==1); Check(runtime.Modules.Single().State==ModuleState.Active);
    await runtime.TickAsync(epoch); Check(runtime.Modules.Single().Data!.Values["value"]=="1");
    await runtime.SetEnabledAsync("fake",false,epoch); await runtime.SetEnabledAsync("fake",false,epoch);
    await runtime.TickAsync(epoch.AddSeconds(2)); Check(module.Polls==1 && module.Stops==1);
    Check(runtime.Modules.Single().Data is null);
});
await Test("Failure is isolated and retry can recover",async()=> {
    var bad=new FakeModule("bad"){Fail=true}; var good=new FakeModule("good");
    var runtime=new ModuleRuntime([bad,good]);
    await runtime.SetEnabledAsync("bad",true,epoch);await runtime.SetEnabledAsync("good",true,epoch);
    await runtime.TickAsync(epoch);
    Check(runtime.Modules.Single(m=>m.Id=="bad").State==ModuleState.Error);
    Check(runtime.Modules.Single(m=>m.Id=="good").Data is not null);
    bad.Fail=false;await runtime.SetEnabledAsync("bad",true,epoch);await runtime.TickAsync(epoch.AddSeconds(2));
    Check(runtime.Modules.Single(m=>m.Id=="bad").State==ModuleState.Active);
});
await Test("Known registry rejects duplicate identifiers",()=> {
    bool rejected=false;try{_=new ModuleRuntime([new FakeModule(),new FakeModule()]);}catch(ArgumentException){rejected=true;}
    Check(rejected); return Task.CompletedTask;
});
await Test("Activation preferences survive configuration roundtrip",async()=> {
    string dir=Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"));
    var store=new SettingsStore(dir);var settings=AppSettings.Default();settings.EnabledModules["pc"]=true;settings.MinimizeToTray=false;
    await store.SaveAsync(settings);var loaded=await store.LoadAsync();
    Check(loaded.Warning is null&&loaded.Settings.EnabledModules["pc"]&&!loaded.Settings.MinimizeToTray);
});
await Test("Corrupt configuration is preserved and reports a useful warning",async()=> {
    string dir=Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
    string path=Path.Combine(dir,"settings.json");await File.WriteAllTextAsync(path,"{broken");
    var loaded=await new SettingsStore(dir).LoadAsync();Check(loaded.Warning is not null);Check(await File.ReadAllTextAsync(path)=="{broken");
    Check(loaded.Settings.EnabledModules["pc"]);
});
await Test("Unknown settings version is retained, not silently rewritten",async()=> {
    string dir=Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
    await File.WriteAllTextAsync(Path.Combine(dir,"settings.json"),"{\"version\":42}");
    var loaded=await new SettingsStore(dir).LoadAsync();Check(loaded.Warning is not null);
});
await Test("Runtime shutdown stops all enabled modules",async()=> {
    var a=new FakeModule("one");var b=new FakeModule("two");var runtime=new ModuleRuntime([a,b]);
    await runtime.SetEnabledAsync("one",true,epoch);await runtime.SetEnabledAsync("two",true,epoch);
    await runtime.StopAllAsync(epoch);Check(a.Stops==1&&b.Stops==1);Check(runtime.Modules.All(m=>m.State==ModuleState.Stopped));
});
await Test("PC values preserve missing sensors and source timestamp",async()=> {
    var module=new PcModule(()=>Task.FromResult(new PcReading(25,6,16,null,null,"Test CPU","Indisponible")));
    var runtime=new ModuleRuntime([module]);await runtime.SetEnabledAsync("pc",true,epoch);await runtime.TickAsync(epoch);
    var data=runtime.Modules.Single().Data!;
    Check(data.Timestamp==epoch&&!data.IsSimulation);
    Check(data.Values["cpu"].Contains("25")&&data.Values["gpu"]=="Indisponible");
});
await Test("GPU parser rejects invalid values independently",()=> {
    var gpu=WindowsPcSource.ParseGpu("N/A, 62");
    Check(gpu.Usage is null&&gpu.Temperature==62);
    Check(WindowsPcSource.ParseGpu("NaN, Infinity").Usage is null);
    return Task.CompletedTask;
});
await Test("Configuration with unknown version cannot be overwritten",async()=> {
 var dir=Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
 var path=Path.Combine(dir,"settings.json");await File.WriteAllTextAsync(path,"{\"version\":42}");
 var store=new SettingsStore(dir);await store.LoadAsync();bool blocked=false;
 try{await store.SaveAsync(AppSettings.Default());}catch(IOException){blocked=true;}
 Check(blocked&&await File.ReadAllTextAsync(path)=="{\"version\":42}");
});
await Test("Event history remains bounded",async()=> {
 var runtime=new ModuleRuntime([new FakeModule()]);
 for(int i=0;i<250;i++){await runtime.SetEnabledAsync("fake",true,epoch);await runtime.SetEnabledAsync("fake",false,epoch);}
 Check(runtime.Events.Count==200);
});
await Test("Network uses elapsed time, not nominal link speed",async()=>{
 NetworkReading? current=new("a","Ethernet","Ethernet","127.0.0.1",true,100,100,1);
 var module=new NetworkModule(()=>current);await module.StartAsync();
 var first=await module.PollAsync(epoch,(_,_)=>{});Check(first.Values["download"]=="Indisponible");
 current=current with{Received=2_000_100,Sent=1_000_100,Seconds=3};
 var next=await module.PollAsync(epoch.AddSeconds(2),(_,_)=>{});Check(next.Values["download"]=="1,00 Mo/s"&&next.Values["upload"]=="0,50 Mo/s");
 current=current with{Id="b",Received=999999,Seconds=4};
 Check((await module.PollAsync(epoch,(_,_)=>{})).Values["download"]=="Indisponible");
 current=current with{Received=1,Sent=1,Seconds=5};
 Check((await module.PollAsync(epoch,(_,_)=>{})).Values["download"]=="Indisponible");
 current=null;Check((await module.PollAsync(epoch,(_,_)=>{})).Values["connection"]=="Indisponible");
});
await Test("Network stops baseline and preserves session totals",async()=>{
 var current=new NetworkReading("a","a","a","a",true,0,0,1);var module=new NetworkModule(()=>current);
 await module.StartAsync();await module.PollAsync(epoch,(_,_)=>{});
 current=current with{Received=1_000_000,Seconds=2};await module.PollAsync(epoch,(_,_)=>{});
 await module.StopAsync();await module.StartAsync();current=current with{Received=99_000_000,Seconds=3};
 var next=await module.PollAsync(epoch,(_,_)=>{});Check(next.Values["download"]=="Indisponible"&&next.Values["downloaded"]=="1,00 Mo");
});
await Test("Media end clears metadata and artwork",async()=>{
 var source=new FakeMedia();var module=new MediaModule(source);await module.StartAsync();
 var data=await module.PollAsync(epoch,(_,_)=>{});Check(data.Values["title"]=="Song"&&module.Artwork is not null);
 source.Reading=new("Aucune session");data=await module.PollAsync(epoch,(_,_)=>{});
 Check(data.Values["title"]=="Aucune session"&&module.Artwork is null&&data.Values["duration"]=="");
 await module.StopAsync();Check(source.Stopped);
});
await Test("Media missing duration is never fabricated",async()=>{
 var source=new FakeMedia{Reading=new("Disponible","Paused",Playing:false)};var module=new MediaModule(source);await module.StartAsync();
 var data=await module.PollAsync(epoch,(_,_)=>{});Check(data.Values["playback"]=="Pause"&&data.Values["duration"]=="");
 await module.StopAsync();
});
await Test("Codex uses duration and consumed percentage; deduplicates map",()=>{
 var json="""{"rateLimits":{"limitId":"codex","primary":{"windowDurationMins":10080,"usedPercent":30}},"rateLimitsByLimitId":{"codex":{"primary":{"windowDurationMins":10080,"usedPercent":30},"secondary":{"windowDurationMins":300,"usedPercent":75,"resetsAt":1800000000}}}}""";
 var result=QuotaParser.Parse(json,epoch);
 Check(result.Windows.Count==2&&result.Windows.Single(w=>w.Minutes==300).Remaining==25);
 Check(result.Windows.Single(w=>w.Minutes==10080).Remaining==70);
 Check(result.Windows.Single(w=>w.Minutes==300).Reset?.Offset==TimeSpan.Zero);
 return Task.CompletedTask;
});
await Test("Codex absent/invalid windows never invent percentages",()=>{
 var r=QuotaParser.Parse("""{"rateLimits":{"primary":null,"secondary":{"windowDurationMins":300,"usedPercent":101}}}""",epoch);
 Check(r.Windows.Count==0&&r.ResetCredits is null);
 var other=QuotaParser.Parse("""{"rateLimits":{"primary":{"windowDurationMins":60,"usedPercent":0}},"rateLimitResetCredits":{"availableCount":2}}""",epoch);
 Check(other.Windows.Single().Minutes==60&&other.ResetCredits==2);return Task.CompletedTask;
});
await Test("Codex history keeps observed gaps and bounded records",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"));var history=new QuotaHistory(dir,3);
 for(int i=0;i<5;i++)await history.AppendAsync(new(epoch.AddMinutes(i),[new("codex",300,i,epoch.AddHours(5))],null));
 var loaded=new QuotaHistory(dir,3);await loaded.LoadAsync();Check(loaded.Samples.Count==3&&loaded.Samples[0].Timestamp==epoch.AddMinutes(2));
 Check(!QuotaHistory.Connect(new(epoch,[new("codex",300,20,epoch.AddHours(5))],null),new(epoch.AddMinutes(6),[new("codex",300,10,epoch.AddHours(10))],null),"codex",300));
});
await Test("Codex refresh coalesces, retains last success on failure and stops",async()=>{
 var source=new FakeCodex(epoch);var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-test-"+Guid.NewGuid().ToString("N"))));
 await module.StartAsync();await module.PollAsync(epoch,(_,_)=>{});
 for(int i=0;i<100&&module.Snapshot is null;i++){await Task.Delay(5);await module.PollAsync(epoch.AddSeconds(1),(_,_)=>{});}
 Check(module.Snapshot is not null&&source.Calls==1);
 source.Fail=true;module.RequestRefresh();module.RequestRefresh();await module.PollAsync(epoch.AddSeconds(2),(_,_)=>{});
 for(int i=0;i<100&&module.Error is null;i++){await Task.Delay(5);await module.PollAsync(epoch.AddSeconds(3),(_,_)=>{});}
 Check(module.Error is not null&&module.Snapshot!.Timestamp==epoch&&source.Calls==2);Check(module.History.Samples.Last().Windows.Count==0);
 source.Block=true;module.RequestRefresh();await module.PollAsync(epoch.AddSeconds(4),(_,_)=>{});
 await module.StopAsync();Check(!module.IsReading);
});
string Event(string kind,string id,int seconds)=>System.Text.Json.JsonSerializer.Serialize(new{timestamp=epoch.AddSeconds(seconds).ToString("O"),type="event_msg",payload=new{type=kind,turn_id=id}});
const string RootMeta="""{"type":"session_meta","payload":{"id":"root-id","thread_source":"user","cwd":"C:/fixture/project"}}""";
LiveTask[] Turns(params string[] lines)=>LiveTaskReader.ParseTurns(new[]{RootMeta}.Concat(lines),epoch,epoch).ToArray();
await Test("Missions start, measured duration and explicit completion",()=>{
 var task=Turns(Event("task_started","a",-40),Event("task_complete","a",-10)).Single();
 Check(task.Phase==LiveTaskPhase.Completed&&task.Duration(epoch)=="00:30"&&task.Workspace=="project");
 return Task.CompletedTask;
});
await Test("Missions interruption is distinct from completion",()=>{
 Check(Turns(Event("task_started","a",-20),Event("turn_aborted","a",-1)).Single().Phase==LiveTaskPhase.Interrupted);
 return Task.CompletedTask;
});
await Test("Missions error presence retained without private message",()=>{
 var error=System.Text.Json.JsonSerializer.Serialize(new{timestamp=epoch.AddSeconds(-1).ToString("O"),type="event_msg",payload=new{type="task_complete",turn_id="a",error=new{message="PRIVATE-ERROR"}}});
 Check(Turns(Event("task_started","a",-20),error).Single().Phase==LiveTaskPhase.Failed);
 var clean=new MetadataJson().Serialize(new MetadataJson().DeserializeObject(error));Check(!clean.Contains("PRIVATE-ERROR"));
 return Task.CompletedTask;
});
await Test("Missions absence of start, unmatched terminal and expired start",()=>{
 Check(Turns(Event("task_complete","a",-1)).Length==0);
 Check(Turns(Event("task_started","a",-20),Event("task_complete","other",-1)).Single().Phase==LiveTaskPhase.Active);
 var old=LiveTaskReader.ParseTurns(new[]{RootMeta,Event("task_started","a",-300)},epoch.AddSeconds(-100),epoch).Single();
 Check(old.Phase==LiveTaskPhase.Unconfirmed&&!old.FinishedUtc.HasValue);return Task.CompletedTask;
});
await Test("Missions subagents and unknown origins cannot become root activity",()=>{
 var child=LiveTaskReader.ParseTurns(new[]{"""{"type":"session_meta","payload":{"id":"child","parent_thread_id":"root-id","thread_source":"subagent"}}""",Event("task_started","child-turn",-1)},epoch,epoch).Single();
 Check(child.IsSubagent&&!child.IsRootConfirmed);
 var report=new LiveTaskReport{CheckedUtc=epoch};report.Tasks.Add(child);
 Check(report.DisplayAt(epoch).Kind!=TaskDisplayKind.ObservedActive);return Task.CompletedTask;
});
await Test("Missions multiple turns remain separate and model comes from context",()=>{
 var context="""{"type":"turn_context","timestamp":"2030-01-01T11:59:59Z","payload":{"turn_id":"b","model":"fixture-model","effort":"medium","user_instructions":"PRIVATE-PROMPT"}}""";
 var tasks=Turns(Event("task_started","a",-50),Event("task_complete","a",-30),Event("task_started","b",-10),context);
 Check(tasks.Length==2&&tasks[0].Phase==LiveTaskPhase.Completed&&tasks[1].Model=="fixture-model");
 var clean=new MetadataJson().Serialize(new MetadataJson().DeserializeObject(context));Check(!clean.Contains("PRIVATE-PROMPT"));return Task.CompletedTask;
});
await Test("Mission notifications require new confirmed same-root completion; no startup replay",()=>{
 var beforeTask=LiveTaskReader.ParseTurns(new[]{RootMeta,Event("task_started","a",-40)},epoch.AddSeconds(-20),epoch).Single();
 var done=Turns(Event("task_started","a",-40),Event("task_complete","a",-1)).Single();
 beforeTask.SessionKey=done.SessionKey="same-file";
 var before=new LiveTaskReport{CheckedUtc=epoch.AddSeconds(-20)};before.Tasks.Add(beforeTask);
 var after=new LiveTaskReport{CheckedUtc=epoch};after.Tasks.Add(done);
 Check(TaskNotificationRules.NewTerminals(before,after).Count==1);
 Check(TaskNotificationRules.NewTerminals(null!,after).Count==0);
 done.SessionKey="different-file";Check(TaskNotificationRules.NewTerminals(before,after).Count==0);
 return Task.CompletedTask;
});
await Test("Mission recovery is bounded, sanitized and reconstructs after restart",()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-mission-"+Guid.NewGuid().ToString("N"));
 var folder=Path.Combine(dir,"sessions",epoch.ToString("yyyy"),epoch.ToString("MM"),epoch.ToString("dd"));Directory.CreateDirectory(folder);
 var path=Path.Combine(folder,"rollout-fixture.jsonl");File.WriteAllLines(path,new[]{RootMeta,Event("task_started","a",-40),Event("task_complete","a",-1)});
 File.SetLastWriteTimeUtc(path,epoch.UtcDateTime);
 var prior=Environment.GetEnvironmentVariable("CODEX_HOME");
 try{Environment.SetEnvironmentVariable("CODEX_HOME",dir);LiveTaskReader.ClearCacheForTests();var first=LiveTaskReader.Read(epoch);
  Check(first.Tasks.Any(t=>t.Phase==LiveTaskPhase.Completed));
  LiveTaskReader.ClearCacheForTests();var restarted=LiveTaskReader.Read(epoch);Check(restarted.Tasks.Any(t=>t.Phase==LiveTaskPhase.Completed));
  Check(TaskNotificationRules.NewTerminals(null!,restarted).Count==0);
 }finally{Environment.SetEnvironmentVariable("CODEX_HOME",prior);LiveTaskReader.ClearCacheForTests();}
 return Task.CompletedTask;
});
await Test("Mission observer expires snapshots and stop removes activity",async()=>{
 var observer=new MissionObserver();var report=new LiveTaskReport{CheckedUtc=epoch};
 report.Tasks.Add(Turns(Event("task_started","a",-10)).Single());observer.Read=_=>report;observer.Start();
 await observer.TickAsync(epoch);
 for(int i=0;i<100&&observer.Current.Checked==DateTimeOffset.MinValue;i++){await Task.Delay(5);await observer.TickAsync(epoch);}
 Check(observer.SummaryAt(epoch).StartsWith("EN COURS"));
 Check(observer.SummaryAt(epoch.AddSeconds(26)).StartsWith("NON VÉRIFIÉ"));
 await observer.StopAsync();Check(observer.SummaryAt(epoch).StartsWith("NON VÉRIFIÉ"));
});
JsonObject ProjectFixture(){
 var state=ProjectStore.Empty();J.A(state,"projects").Add(new JsonObject{["id"]="fixture",["name"]="Fixture",["notes"]="manual-note",["tags"]=new JsonArray("test")});
 J.O(J.O(state,"settings"),"chatgptProjectMappings")["project-key"]="fixture";return state;
}
JsonObject DeltaFixture(string text="NEXT: implement feature")=>new(){["conversationKey"]="fixture-chat",["projectKey"]="project-key",["projectTitle"]="Fixture",["title"]="Fixture work",["url"]="https://chatgpt.com/c/fixture-chat",["fingerprint"]=J.Hash(text),["messages"]=new JsonArray(new JsonObject{["role"]="assistant",["text"]=text})};
await Test("CONTROL status fixtures match next-action, physical gate, blocked and closeout rules",()=>{
 foreach(var pair in new[]{("NEXT: implement feature","ACTIVE"),("NEXT: run the live hardware test","NEEDS TEST"),("Waiting for access before continuing","BLOCKED"),("Continue in the synced ChatGPT thread.","STABLE")}){
  var e=new JsonObject{["sourceType"]="chatgpt_thread",["summary"]=pair.Item2=="STABLE"?"Program closeout complete. No remaining work.":"Implementation complete.",["resumeAction"]=pair.Item1};
  Check(ControlEngine.Classify(e)==pair.Item2,pair.Item2);
 }return Task.CompletedTask;
});
await Test("CONTROL delta dedup, latest state and manual status survive",()=>{
 var state=ProjectFixture();var first=ChatDelta.Ingest(state,DeltaFixture());Check(J.B(first,"changed"));
 Check(!J.B(ChatDelta.Ingest(state,DeltaFixture()),"changed")&&J.A(state,"evidence").Count==1);
 J.O(J.Find(state,"projects","fixture")!,"control")["statusOverride"]="WAITING";
 ChatDelta.Ingest(state,DeltaFixture("NEXT: run the live hardware test"));
 Check(J.S(J.A(state,"derived")[0],"status")=="WAITING"&&J.S(J.A(state,"derived")[0],"autoStatus")=="NEEDS TEST");
 Check(J.S(J.Find(state,"projects","fixture"),"notes")=="manual-note");
 return Task.CompletedTask;
});
await Test("CONTROL manual detached source is not auto-reassigned",()=>{
 var state=ProjectFixture();ChatDelta.Ingest(state,DeltaFixture());var source=(JsonObject)J.A(state,"sources")[0]!;
 J.O(source,"control")["assignment"]="MANUAL";J.O(source,"control")["projectId"]=null;source["projectId"]=null;
 var result=ChatDelta.Ingest(state,DeltaFixture("NEXT: build another thing"));Check(J.B(result,"detached")&&J.B(J.A(state,"evidence")[0],"controlExcluded"));return Task.CompletedTask;
});
await Test("CONTROL unassigned conversation remains discovered",()=>{
 var state=ProjectStore.Empty();var delta=DeltaFixture();delta["projectKey"]=null;delta["projectTitle"]=null;
 Check(J.B(ChatDelta.Ingest(state,delta),"discovered")&&J.A(state,"projects").Count==0);return Task.CompletedTask;
});
await Test("CONTROL import preview is read-only, repeated import preserves AXIS edits and cursors",async()=>{
 string dir=Path.Combine(Path.GetTempPath(),"axis-projects-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
 var incoming=ProjectFixture();J.O(J.O(incoming,"settings"),"githubCursors")["owner/repo"]=new JsonObject{["commitsEtag"]="fixture-tag"};
 string file=Path.Combine(dir,"control.json");string text=incoming.ToJsonString();await File.WriteAllTextAsync(file,text);
 var store=new ProjectStore(Path.Combine(dir,"axis"));await store.LoadAsync();var preview=await store.PreviewAsync(file);
 Check(J.A(store.Snapshot,"projects").Count==0);await store.ImportAsync(preview);
 await store.EditProjectAsync("fixture",p=>p["notes"]="AXIS edit");await store.ImportAsync(preview);
 Check(J.A(store.Snapshot,"projects").Count==1&&J.S(J.Find(store.Snapshot,"projects","fixture"),"notes")=="AXIS edit");
 Check(await File.ReadAllTextAsync(file)==text);
 var reloaded=new ProjectStore(Path.Combine(dir,"axis"));await reloaded.LoadAsync();Check(J.S(J.Find(reloaded.Snapshot,"projects","fixture"),"notes")=="AXIS edit");
 Check(reloaded.Snapshot["settings"]?["githubCursors"]?["owner/repo"]?["commitsEtag"]?.ToString()=="fixture-tag");
});
await Test("PROJECTS receiver refuses foreign origins and requires explicit collection opt-in",async()=>{
 string dir=Path.Combine(Path.GetTempPath(),"axis-http-"+Guid.NewGuid().ToString("N"));var store=new ProjectStore(dir);await store.LoadAsync();await store.ImportAsync(new(ProjectFixture(),1,0,0));
 var receiver=new ProjectReceiver(store,44187);await receiver.StartAsync();
 try{using var client=new HttpClient();var body=DeltaFixture().ToJsonString();
  using var rejected=await client.PostAsync("http://127.0.0.1:44187/api/ingest/chatgpt-delta",new StringContent(body,System.Text.Encoding.UTF8,"application/json"));Check((int)rejected.StatusCode==409);
  await store.MutateAsync(s=>{J.O(s,"axisProjects")["acceptChat"]=true;return true;});
  using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:44187/api/ingest/chatgpt-delta"){Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json")};request.Headers.Add("Origin","https://evil.invalid");
  using var foreign=await client.SendAsync(request);Check((int)foreign.StatusCode==403);
  using var accepted=await client.PostAsync("http://127.0.0.1:44187/api/ingest/chatgpt-delta",new StringContent(body,System.Text.Encoding.UTF8,"application/json"));Check(accepted.IsSuccessStatusCode&&J.A(store.Snapshot,"evidence").Count==1);
 }finally{await receiver.StopAsync();}
});
await Test("GitHub staged sync preserves concurrent manual changes and deduplicates",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-github-"+Guid.NewGuid().ToString("N"));var store=new ProjectStore(dir);await store.LoadAsync();var fixture=ProjectFixture();J.Find(fixture,"projects","fixture")!["repo"]="owner/repo";await store.ImportAsync(new(fixture,1,0,0));
 using var client=new HttpClient(new FixtureGithubHandler(async()=>await store.EditProjectAsync("fixture",p=>p["notes"]="edited during sync")));
 var result=await new ProjectGithub(store,client).SyncAsync(CancellationToken.None);
 Check(result.Failed==0&&J.S(J.Find(store.Snapshot,"projects","fixture"),"notes")=="edited during sync");
 int count=J.A(store.Snapshot,"evidence").Count;await new ProjectGithub(store,client).SyncAsync(CancellationToken.None);
 Check(J.A(store.Snapshot,"evidence").Count==count&&store.Snapshot["settings"]?["githubCursors"]?["owner/repo"]!=null);
});
await Test("CONTROL multiline tail and authoritative ownership change remain incremental",()=>{
 var state=ProjectFixture();J.A(state,"projects").Add(new JsonObject{["id"]="second",["name"]="Second"});
 var delta=DeltaFixture("PS C:\\test> Get-Content file\nImplementation complete.\nNEXT: run the live hardware test");
 ChatDelta.Ingest(state,delta);Check(J.S(J.A(state,"derived").OfType<JsonObject>().Single(d=>J.S(d,"projectId")=="fixture"),"status")=="NEEDS TEST");
 delta["projectTitle"]="Second";var moved=ChatDelta.Ingest(state,delta);Check(J.S(moved,"projectId")=="second");
 Check(J.A(state,"sources").Count==1&&J.A(state,"evidence").Count==1&&J.S(J.A(state,"evidence")[0],"projectId")=="second");return Task.CompletedTask;
});
await Test("CONTROL source archive and empty inventory are retained",()=>{
 var state=ProjectFixture();ChatDelta.Ingest(state,DeltaFixture());J.O(J.A(state,"sources")[0]!,"control")["archived"]=true;
 ChatDelta.Ingest(state,DeltaFixture("NEXT: continue feature"));Check(J.B(J.A(state,"evidence")[0],"controlExcluded"));
 J.O(state,"settings")["lastChatgptInventory"]=new JsonObject{["projects"]=new JsonArray(new JsonObject{["key"]="project-key",["title"]="Fixture",["conversationCount"]=0})};
 ControlEngine.Derive(state,["fixture"]);Check(J.S(J.A(state,"derived")[0],"status")=="EMPTY");return Task.CompletedTask;
});
await Test("GitHub stale repository response cannot overwrite changed assignment",async()=>{
 var store=new ProjectStore(Path.Combine(Path.GetTempPath(),"axis-github-"+Guid.NewGuid().ToString("N")));await store.LoadAsync();var fixture=ProjectFixture();J.Find(fixture,"projects","fixture")!["repo"]="owner/repo";await store.ImportAsync(new(fixture,1,0,0));
 using var http=new HttpClient(new FixtureGithubHandler(async()=>await store.EditProjectAsync("fixture",p=>p["repo"]="owner/changed")));
 await new ProjectGithub(store,http).SyncAsync(CancellationToken.None);Check(J.A(store.Snapshot,"evidence").Count==0&&J.S(J.Find(store.Snapshot,"projects","fixture"),"repo")=="owner/changed");
});
await Test("Chrome extension authenticated preflight succeeds without exposing the token",async()=>{
 var store=new ProjectStore(Path.Combine(Path.GetTempPath(),"axis-preflight-"+Guid.NewGuid().ToString("N")));await store.LoadAsync();
 string origin="chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";var receiver=new ProjectReceiver(store,44188,"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","fixture-token");
 await receiver.StartAsync();try{using var client=new HttpClient();
  using var preflight=new HttpRequestMessage(HttpMethod.Options,"http://127.0.0.1:44188/api/ingest/chatgpt-delta");preflight.Headers.Add("Origin",origin);preflight.Headers.Add("Access-Control-Request-Headers","authorization,content-type");
  using var allowed=await client.SendAsync(preflight);Check((int)allowed.StatusCode==204&&allowed.Headers.GetValues("Access-Control-Allow-Origin").Single()==origin);
  using var denied=await client.PostAsync("http://127.0.0.1:44188/api/ingest/chatgpt-delta",new StringContent("{}"));Check((int)denied.StatusCode==401);
 }finally{await receiver.StopAsync();}
});
await Test("Obsidian palette uses rounded remaining percentage and exact boundaries",()=>{
 Check(DisplayRules.QuotaHex(76)=="#A477FF"&&DisplayRules.QuotaHex(75)=="#51D7AF");
 Check(DisplayRules.QuotaHex(51)=="#51D7AF"&&DisplayRules.QuotaHex(50)=="#E5B64D");
 Check(DisplayRules.QuotaHex(26)=="#E5B64D"&&DisplayRules.QuotaHex(25)=="#F28C38");
 Check(DisplayRules.QuotaHex(11)=="#F28C38"&&DisplayRules.QuotaHex(10)=="#EE4B55");
 Check(DisplayRules.QuotaHex(null)=="#8BA0B1"&&DisplayRules.QuotaHex(75.5)=="#A477FF");return Task.CompletedTask;
});
await Test("LINK color policy distinguishes temperatures and RAM denominator; hysteresis",()=>{
 Check(Math.Abs(DisplayRules.TemperatureLoad(60)!.Value-50)<0.001);
 Check(DisplayRules.RamLoad(11,0) is null&&DisplayRules.RamLoad(11,22)==50);
 Check(DisplayRules.LinkBand(50.1,1)==1&&DisplayRules.LinkBand(52,1)==2&&DisplayRules.LinkBand(47.9,2)==1);
 Check(DisplayRules.LinkBand(null,2)==-1);return Task.CompletedTask;
});
await Test("Quota history range really filters 24 hours, 7 days and 30 days",()=>{
 QuotaSnapshot Sample(int days)=>new(epoch.AddDays(-days),[new("codex",300,20,null)],null);
 var samples=new[]{Sample(31),Sample(29),Sample(6),Sample(0)};
 Check(DisplayRules.FilterHistory(samples,epoch,1).Count==1);
 Check(DisplayRules.FilterHistory(samples,epoch,7).Count==2);
 Check(DisplayRules.FilterHistory(samples,epoch,30).Count==3);return Task.CompletedTask;
});
await Test("Reset credit expiry remains source-only and rejects a date without offset",()=>{
 var r=QuotaParser.Parse("""{"rateLimitResetCredits":{"availableCount":1,"expiresAt":"2030-01-02T12:00:00Z"}}""",epoch);
 Check(r.ResetCredits==1&&r.ResetCreditsExpiry==epoch.AddDays(1));
 var absent=QuotaParser.Parse("""{"rateLimitResetCredits":{"availableCount":0,"expiresAt":"2030-01-02 12:00"}}""",epoch);
 Check(absent.ResetCredits==0&&absent.ResetCreditsExpiry is null);return Task.CompletedTask;
});
await Test("V07 reimport reconciles upstream fields and preserves local field conflicts",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-v07-merge-"+Guid.NewGuid());var store=new ProjectStore(dir);
 var initial=ProjectStore.Empty();J.A(initial,"projects").Add(new JsonObject{["id"]="p",["name"]="Original",["description"]="one"});
 await store.ImportAsync(new(initial,1,0,0));
 await store.EditProjectAsync("p",p=>p["name"]="AXIS edit");
 var update=(JsonObject)initial.DeepClone();update["projects"]![0]!["name"]="CONTROL edit";update["projects"]![0]!["description"]="two";
 await store.ImportAsync(new(update,1,0,0));
 var row=J.Find(store.Snapshot,"projects","p")!;Check(J.S(row,"name")=="AXIS edit");Check(J.S(row,"description")=="two","unchanged local field must accept upstream update");
 var reload=new ProjectStore(dir);await reload.LoadAsync();Check(J.S(J.Find(reload.Snapshot,"projects","p"),"description")=="two");
});
await Test("V07 identical mutation is a true disk no-op",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-v07-noop-"+Guid.NewGuid());var store=new ProjectStore(dir);
 await store.MutateAsync(s=>{J.O(s,"axisProjects")["acceptChat"]=true;return true;});
 var file=Path.Combine(dir,"projects.json");File.SetLastWriteTimeUtc(file,new DateTime(2001,1,1));
 await store.MutateAsync(s=>false);Check(File.GetLastWriteTimeUtc(file).Year==2001,"no-op rewrote state");
});
await Test("V07 manual mapping takes precedence over live ownership",()=>{
 var state=ProjectStore.Empty();foreach(string id in new[]{"one","two"})J.A(state,"projects").Add(new JsonObject{["id"]=id,["name"]=id});
 J.O(J.O(state,"settings"),"manualMappings")["thread"]="one";
 var result=ChatDelta.Ingest(state,new JsonObject{["conversationKey"]="thread",["projectKey"]="key",["projectTitle"]="two",["messages"]=new JsonArray(new JsonObject{["role"]="assistant",["text"]="NEXT: verify build"})});
 Check(J.S(result,"projectId")=="one","live owner overrode manual mapping");return Task.CompletedTask;
});
await Test("V07 catchup is bounded, deduplicated and identifies changed or missing baselines",()=>{
 var state=ProjectStore.Empty();J.A(state,"sources").Add(new JsonObject{["id"]="s",["type"]="chatgpt_thread",["externalId"]="known",["chatgptStateSchemaVersion"]=2,["conversationUpdatedAt"]=epoch.ToString("O")});
 var inventory=new JsonObject{["maxPlan"]=1,["threads"]=new JsonArray(new JsonObject{["key"]="known",["updatedAt"]=epoch.ToString("O")},new JsonObject{["key"]="new",["updatedAt"]=epoch.AddHours(1).ToString("O")},new JsonObject{["key"]="other"},new JsonObject{["key"]="new",["updatedAt"]=epoch.ToString("O")})};
 string before=state.ToJsonString();var plan=ChatCatchup.Plan(state,inventory,epoch);Check(J.S(plan,"inventoryCount")=="3"&&J.S(plan,"unchanged")=="1"&&J.S(plan,"changedCount")=="2"&&J.S(plan,"deferredCount")=="1");Check(state.ToJsonString()==before);
 Check(J.S(J.A(plan,"plan")[0],"key")=="new");return Task.CompletedTask;
});
await Test("V07 timeout and inaccessible reports cool down, bypass new remote timestamps, and sanitize errors",()=>{
 var state=ProjectStore.Empty();var report=new JsonObject{["failedCount"]=1,["failures"]=new JsonArray(new JsonObject{["key"]="slow",["error"]="CHATGPT_FETCH_TIMEOUT_12000MS private-content",["updatedAt"]=epoch.ToString("O")}),["inaccessible"]=new JsonArray(new JsonObject{["key"]="gone",["updatedAt"]=epoch.ToString("O")})};
 ChatCatchup.Report(state,report,epoch);
 var inv=new JsonObject{["threads"]=new JsonArray(new JsonObject{["key"]="slow",["updatedAt"]=epoch.ToString("O")},new JsonObject{["key"]="gone",["updatedAt"]=epoch.ToString("O")})};
 var blocked=ChatCatchup.Plan(state,inv,epoch.AddMinutes(1));Check(J.S(blocked,"timeoutQuarantinedCount")=="1"&&J.S(blocked,"inaccessibleCount")=="1"&&J.S(blocked,"changedCount")=="0");Check(!state.ToJsonString().Contains("private-content"));
 inv["threads"]![0]!["updatedAt"]=epoch.AddMinutes(2).ToString("O");Check(J.S(ChatCatchup.Plan(state,inv,epoch.AddMinutes(3)),"changedCount")=="1");
 Check(J.S(ChatCatchup.Plan(state,new JsonObject{["threads"]=new JsonArray(new JsonObject{["key"]="slow",["updatedAt"]=epoch.ToString("O")})},epoch.AddMinutes(31)),"changedCount")=="1");return Task.CompletedTask;
});
await Test("V07 archived sources stay unchanged; discovered fingerprints deduplicate",()=>{
 var state=ProjectStore.Empty();var payload=new JsonObject{["conversationKey"]="loose",["fingerprint"]="fp",["title"]="Unknown topic",["messages"]=new JsonArray(new JsonObject{["text"]="NEXT: check next item",["role"]="assistant"})};
 ChatDelta.Ingest(state,payload);string before=state.ToJsonString();Check(!J.B(ChatDelta.Ingest(state,payload),"changed"));Check(state.ToJsonString()==before);
 J.A(state,"sources").Add(new JsonObject{["id"]="arch",["externalId"]="loose",["type"]="chatgpt_thread",["control"]=new JsonObject{["archived"]=true}});Check(J.B(ChatDelta.Ingest(state,payload),"archived"));return Task.CompletedTask;
});
await Test("V07 resolver scores aliases and repo URLs but rejects equal candidates",()=>{
 var state=ProjectStore.Empty();J.A(state,"projects").Add(new JsonObject{["id"]="p",["name"]="Project One",["repo"]="owner/repo"});
 J.O(J.O(state,"settings"),"projectAliases")["p"]=new JsonArray("Special Alias");
 Check(J.S(ProjectResolver.Resolve(state,new JsonObject{["title"]="Special Alias setup"},"t",""),"id")=="p");
 Check(J.S(ProjectResolver.Resolve(state,new JsonObject(),"t","https://github.com/owner/repo"),"id")=="p");
 J.A(state,"projects").Add(new JsonObject{["id"]="q",["name"]="Project One",["repo"]="owner/repo"});Check(ProjectResolver.Resolve(state,new JsonObject{["title"]="Project One"},"t","") is null);return Task.CompletedTask;
});
await Test("V07 old evidence is not inferred active; manual status stays explicit",()=>{
 var state=ProjectStore.Empty();J.A(state,"projects").Add(new JsonObject{["id"]="p",["name"]="Test"});
 J.A(state,"evidence").Add(new JsonObject{["id"]="e",["projectId"]="p",["sourceType"]="chatgpt_thread",["timestamp"]=DateTimeOffset.UtcNow.AddDays(-90).ToString("O"),["summary"]="Feature implementation underway",["confidence"]=.9});
 ControlEngine.Derive(state);Check(J.S(J.A(state,"derived")[0],"status")=="UNSYNCED");
 state["projects"]![0]!["control"]=new JsonObject{["statusOverride"]="WAITING"};ControlEngine.Derive(state);Check(J.S(J.A(state,"derived")[0],"status")=="WAITING");return Task.CompletedTask;
});
await Test("V07 import projection excludes transcripts and HQ, retaining derived evidence",()=>{
 var input=ProjectStore.Empty();input["hq"]=new JsonObject{["private"]="skip"};J.A(input,"evidence").Add(new JsonObject{["id"]="e",["summary"]="derived fact",["transcript"]="full private"});
 var safe=ProjectImportMerge.Sanitize(input);Check(!safe.ContainsKey("hq")&&!safe.ToJsonString().Contains("full private")&&J.S(J.A(safe,"evidence")[0],"summary")=="derived fact");return Task.CompletedTask;
});
await Test("V07 collector HTTP contract plan/report/delta, auth and no-op persistence",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-v07-http-"+Guid.NewGuid());var store=new ProjectStore(dir);
 await store.MutateAsync(s=>{J.O(s,"axisProjects")["acceptChat"]=true;J.A(s,"projects").Add(new JsonObject{["id"]="p",["name"]="Project"});return true;});
 var receiver=new ProjectReceiver(store,44189,"fixture","test-token");await receiver.StartAsync();try{
 using var http=new HttpClient();http.DefaultRequestHeaders.Authorization=new("Bearer","test-token");http.DefaultRequestHeaders.Add("Origin","chrome-extension://fixture");
 async Task<JsonObject> Post(string path,string body){using var response=await http.PostAsync("http://127.0.0.1:44189"+path,new StringContent(body,System.Text.Encoding.UTF8,"application/json"));Check(response.IsSuccessStatusCode,"collector contract rejected");return (JsonObject)JsonNode.Parse(await response.Content.ReadAsStringAsync())!;}
 var plan=await Post("/api/chatgpt/catchup-plan","""{"threads":[{"key":"x","projectTitle":"Project","updatedAt":"2030-01-01T12:00:00Z"}]}""");Check(J.S(plan,"newCount")=="1");
 string delta="""{"conversationKey":"x","projectTitle":"Project","fingerprint":"same","conversationUpdatedAt":"2030-01-01T12:00:00Z","messages":[{"role":"assistant","text":"NEXT: verify build"}]}""";
 Check(J.B(await Post("/api/ingest/chatgpt-delta",delta),"changed"));
 string file=Path.Combine(dir,"projects.json");File.SetLastWriteTimeUtc(file,new DateTime(2001,1,1));Check(!J.B(await Post("/api/ingest/chatgpt-delta",delta),"changed"));Check(File.GetLastWriteTimeUtc(file).Year==2001);
 var report=await Post("/api/chatgpt/catchup-report","""{"inventoryCount":1,"refreshedCount":1}""");Check(J.S(report,"status")=="HEALTHY");
 http.DefaultRequestHeaders.Remove("Origin");http.DefaultRequestHeaders.Add("Origin","https://foreign.example");using var denied=await http.PostAsync("http://127.0.0.1:44189/api/chatgpt/catchup-plan",new StringContent("{}"));Check((int)denied.StatusCode==403);
 }finally{await receiver.StopAsync();}
});
await Test("V07 import preserves locally deleted fields and rows",()=>{
 var target=ProjectStore.Empty();var origin=ProjectStore.Empty();J.A(origin,"projects").Add(new JsonObject{["id"]="p",["name"]="Original",["description"]="old"});
 ProjectImportMerge.Apply(target,origin);target["projects"]![0]!.AsObject().Remove("description");
 var changed=(JsonObject)origin.DeepClone();changed["projects"]![0]!["description"]="upstream";
 var review=ProjectImportMerge.Apply(target,changed);Check(!target["projects"]![0]!.AsObject().ContainsKey("description")&&review.Conflicts>0,"deleted field restored silently");
 J.A(target,"projects").Clear();review=ProjectImportMerge.Apply(target,changed);Check(J.A(target,"projects").Count==0,"deleted row recreated");return Task.CompletedTask;
});
await Test("V07 import keeps all incoming evidence aligned with manual source ownership",()=>{
 var input=ProjectStore.Empty();foreach(string id in new[]{"a","b"})J.A(input,"projects").Add(new JsonObject{["id"]=id,["name"]=id});
 J.A(input,"sources").Add(new JsonObject{["id"]="s",["type"]="chatgpt_thread",["projectId"]="a"});
 var target=ProjectStore.Empty();ProjectImportMerge.Apply(target,input);
 var source=J.Find(target,"sources","s")!;source["projectId"]="b";source["control"]=new JsonObject{["assignment"]="MANUAL",["projectId"]="b"};
 J.A(input,"evidence").Add(new JsonObject{["id"]="e",["sourceId"]="s",["projectId"]="a",["sourceType"]="chatgpt_thread",["timestamp"]=DateTimeOffset.UtcNow.ToString("O"),["summary"]="Feature implementation underway",["confidence"]=.9});
 ProjectImportMerge.Apply(target,input);Check(J.S(J.Find(target,"evidence","e"),"projectId")=="b","incoming evidence escaped manual ownership");return Task.CompletedTask;
});
await Test("V07 French action scoring follows CONTROL ECMAScript word boundaries",()=>{
 var result=ControlEngine.Resume(new JsonObject{["name"]="Fixture"},new JsonObject{["sourceType"]="chatgpt_thread",["title"]="Fixture",["resumeAction"]="Version testée sur le poste."});
 Check(result.Source=="chat_resume_action");return Task.CompletedTask;
});
await Test("V07 resume removes nested role/action prefixes before scoring candidates",()=>{
 var result=ControlEngine.Resume(new JsonObject{["name"]="Fixture"},new JsonObject{["sourceType"]="chatgpt_thread",["title"]="Fixture",["resumeAction"]="USER: NEXT: First ordinary sentence. Another test pending."});
 Check(result.Action.Contains("Another test pending"));return Task.CompletedTask;
});
await Test("V07 catchup requests repair for incomplete local source references",()=>{
 var state=ProjectStore.Empty();J.A(state,"projects").Add(new JsonObject{["id"]="p",["name"]="Project"});
 J.A(state,"sources").Add(new JsonObject{["id"]="s",["type"]="chatgpt_thread",["externalId"]="x",["projectId"]="p",["chatgptStateSchemaVersion"]=2,["conversationUpdatedAt"]=epoch.ToString("O")});
 var plan=ChatCatchup.Plan(state,new JsonObject{["threads"]=new JsonArray(new JsonObject{["key"]="x",["updatedAt"]=epoch.ToString("O")})},epoch);
 Check(J.S(J.A(plan,"plan").FirstOrDefault(),"reason")=="local-reference-missing");return Task.CompletedTask;
});
await Test("V07 ownership repair realigns every source evidence even on an unchanged fingerprint",()=>{
 var state=ProjectStore.Empty();foreach(string id in new[]{"a","b"})J.A(state,"projects").Add(new JsonObject{["id"]=id,["name"]=id});
 var payload=new JsonObject{["conversationKey"]="x",["projectTitle"]="b",["fingerprint"]="same",["messages"]=new JsonArray(new JsonObject{["role"]="assistant",["text"]="NEXT: verify build"})};
 ChatDelta.Ingest(state,payload);string sid=J.S(J.A(state,"sources")[0],"id");
 J.A(state,"evidence").Add(new JsonObject{["id"]="old-evidence",["sourceId"]=sid,["projectId"]="a",["sourceType"]="chatgpt_thread",["timestamp"]=DateTimeOffset.UtcNow.ToString("O"),["summary"]="Feature underway",["confidence"]=.9});
 Check(J.B(ChatDelta.Ingest(state,payload),"changed"),"inconsistent ownership must be repaired");
 Check(J.A(state,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"sourceId")==sid).All(e=>J.S(e,"projectId")=="b"));return Task.CompletedTask;
});
await Test("Live quota default polls at 30 seconds from request start",async()=>{
 var source=new FakeCodex(epoch);var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid())));
 await module.StartAsync();
 try{
  await module.PollAsync(epoch,(_,_)=>{});
  for(int i=0;i<100&&module.Snapshot is null;i++){await Task.Delay(5);await module.PollAsync(epoch.AddSeconds(1),(_,_)=>{});}
  await module.PollAsync(epoch.AddSeconds(29),(_,_)=>{});Check(source.Calls==1);
  await module.PollAsync(epoch.AddSeconds(30),(_,_)=>{});
  for(int i=0;i<100&&source.Calls<2;i++)await Task.Delay(5);
  Check(source.Calls==2,"default cadence must be 30 seconds");
 }finally{await module.StopAsync();}
});
await Test("Live history suppresses unchanged samples but retains continuity and gaps",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid());var h=new QuotaHistory(dir);
 QuotaSnapshot Sample(int seconds)=>new(epoch.AddSeconds(seconds),[new("codex",300,25,epoch.AddHours(5))],2);
 await h.AppendAsync(Sample(0));var bytes=await File.ReadAllTextAsync(Path.Combine(dir,"quota-history.json"));
 await h.AppendAsync(Sample(30));Check(h.Samples.Count==1,"unchanged sample duplicated");
 Check(bytes==await File.ReadAllTextAsync(Path.Combine(dir,"quota-history.json")),"unchanged sample rewrote disk");
 await h.AppendAsync(Sample(300));Check(h.Samples.Count==2,"continuity checkpoint missing");
 await h.AppendAsync(new(epoch.AddSeconds(330),[],null));await h.AppendAsync(new(epoch.AddSeconds(360),[],null));
 Check(h.Samples.Count==3,"duplicate error gap");await h.AppendAsync(Sample(390));Check(h.Samples.Count==4);
});
await Test("Live configurable cadence persists with legacy defaults",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid());var store=new SettingsStore(dir);
 Check((await store.LoadAsync()).Settings.CodexRefreshSeconds==30);
 foreach(int seconds in new[]{30,60,120,300}){var settings=AppSettings.Default();settings.CodexRefreshSeconds=seconds;await store.SaveAsync(settings);Check((await store.LoadAsync()).Settings.CodexRefreshSeconds==seconds);}
});
await Test("Live quota backoff doubles, caps and resets after success",async()=>{
 var source=new FakeCodex(epoch){Fail=true};var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid())));
 await module.StartAsync();
 try{var time=epoch;
  foreach(int expected in new[]{60,120,240,300}){
   await module.PollAsync(time,(_,_)=>{});
   for(int i=0;i<100&&module.IsReading;i++){await Task.Delay(5);await module.PollAsync(time.AddSeconds(1),(_,_)=>{});}
   Check(module.NextRefresh==time.AddSeconds(1+expected),"incorrect retry delay");time=module.NextRefresh;
  }
  source.Fail=false;await module.PollAsync(time,(_,_)=>{});
  for(int i=0;i<100&&module.IsReading;i++){await Task.Delay(5);await module.PollAsync(time.AddSeconds(1),(_,_)=>{});}
  Check(module.Error is null&&module.NextRefresh==time.AddSeconds(30));
 }finally{await module.StopAsync();}
});
await Test("Live blocked quota cannot overlap manual or resumed request",async()=>{
 var source=new ControlledQuota(epoch);var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid())));
 await module.StartAsync();
 try{
  await module.PollAsync(epoch,(_,_)=>{});await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
  for(int i=1;i<=10;i++){module.RequestRefresh();await module.PollAsync(epoch.AddSeconds(i),(_,_)=>{});}
  Check(source.Calls==1&&!module.IsFresh(epoch));module.OnResume();Check(!module.IsFresh(epoch));
  await module.PollAsync(epoch.AddSeconds(20),(_,_)=>{});source.Release.TrySetResult();
  for(int i=0;i<100&&source.Calls<2;i++){await Task.Delay(5);await module.PollAsync(epoch.AddSeconds(21),(_,_)=>{});}
  Check(source.Calls==2&&source.MaxConcurrent==1,"overlapping reads");
  Check(module.History.Samples.Count<=1,"pre-resume response persisted");
 }finally{await module.StopAsync();}
});
await Test("Live mission cadence is five seconds and never overlaps slow reads",async()=>{
 var observer=new MissionObserver();int reads=0,active=0,max=0;using var release=new ManualResetEventSlim(false);
 observer.Read=now=>{Interlocked.Increment(ref reads);max=Math.Max(max,Interlocked.Increment(ref active));release.Wait(TimeSpan.FromSeconds(2));Interlocked.Decrement(ref active);return new LiveTaskReport{CheckedUtc=now};};observer.Start();
 try{
  await observer.TickAsync(epoch);for(int i=0;i<100&&reads<1;i++)await Task.Delay(5);
  await observer.TickAsync(epoch.AddSeconds(4));Check(reads==1);release.Set();
  for(int i=0;i<100&&observer.Current.Checked==DateTimeOffset.MinValue;i++){await Task.Delay(5);await observer.TickAsync(epoch.AddSeconds(4));}
  await observer.TickAsync(epoch.AddSeconds(5));for(int i=0;i<100&&reads<2;i++)await Task.Delay(5);
  Check(reads==2&&max==1,"mission cadence or concurrency incorrect");
  observer.OnResume();Check(observer.Current.Checked==DateTimeOffset.MinValue&&observer.TakeNotifications().Length==0);
 }finally{release.Set();await observer.StopAsync();}
});
await Test("Live mission projection expires evidence, preserves terminal proofs and ticks duration",()=>{
 var m=new MissionObservation("a","thread","project","EN COURS","00:10","observed-model","medium",false,epoch.AddSeconds(-10),null,epoch);
 Check(m.DurationAt(epoch.AddSeconds(1))=="00:11");Check(m.StatusAt(epoch.AddSeconds(91))=="À VÉRIFIER");Check(m.StatusAt(epoch,false)=="À VÉRIFIER");
 Check(m.StatusAt(epoch.AddSeconds(-6))=="À VÉRIFIER");
 foreach(string terminal in new[]{"TERMINÉ","ÉCHEC","INTERROMPU"})Check((m with{Status=terminal,Finished=epoch}).StatusAt(epoch.AddHours(1),false)==terminal);
 return Task.CompletedTask;
});
await Test("Live cached quota stays stale across restart until a fresh success",async()=>{
 var dir=Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid());var history=new QuotaHistory(dir);await history.AppendAsync(new(epoch,[new("codex",300,25,null)],null));
 var module=new CodexModule(new FakeCodex(epoch),new QuotaHistory(dir));await module.StartAsync();
 try{Check(module.Snapshot is not null&&!module.IsFresh(epoch),"cached value presented as current");}finally{await module.StopAsync();}
});
await Test("Live runtime recovers polling after wall clock rollback",async()=>{
 var fake=new FakeModule();var runtime=new ModuleRuntime([fake]);await runtime.SetEnabledAsync("fake",true,epoch);await runtime.TickAsync(epoch);await runtime.TickAsync(epoch.AddMinutes(-5));Check(fake.Polls==2,"clock rollback stopped polling");await runtime.StopAllAsync(epoch);
});
await Test("Live quota respects all four selected intervals",async()=>{
 foreach(int seconds in new[]{30,60,120,300}){
  var source=new FakeCodex(epoch);var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-live-"+Guid.NewGuid()))){RefreshSeconds=seconds};await module.StartAsync();
  try{await module.PollAsync(epoch,(_,_)=>{});for(int i=0;i<100&&module.IsReading;i++){await Task.Delay(5);await module.PollAsync(epoch.AddSeconds(1),(_,_)=>{});}
   await module.PollAsync(epoch.AddSeconds(seconds-1),(_,_)=>{});Check(source.Calls==1);await module.PollAsync(epoch.AddSeconds(seconds),(_,_)=>{});for(int i=0;i<100&&source.Calls<2;i++)await Task.Delay(5);Check(source.Calls==2);
  }finally{await module.StopAsync();}
 }
});
await Test("Live observer transitions preserve separate roots and deliver completion only once",async()=>{
 foreach(string end in new[]{"task_complete","task_failed","turn_aborted"}){
  var observer=new MissionObserver();bool terminal=false;
  observer.Read=now=>{var report=new LiveTaskReport{CheckedUtc=now};var lines=new List<string>{RootMeta,Event("task_started","a",-10)};
   if(terminal)lines.Add(end=="task_failed"?System.Text.Json.JsonSerializer.Serialize(new{timestamp=epoch.AddSeconds(4).ToString("O"),type="event_msg",payload=new{type="task_complete",turn_id="a",error=new{message="PRIVATE"}}}):Event(end,"a",4));
   var root=LiveTaskReader.ParseTurns(lines,now,now).Single();root.SessionKey="first";report.Tasks.Add(root);
   var other=Turns(Event("task_started","b",-2)).Single();other.SessionKey="second";report.Tasks.Add(other);return report;};observer.Start();
  try{await observer.TickAsync(epoch);for(int i=0;i<100&&observer.Current.Checked!=epoch;i++){await Task.Delay(5);await observer.TickAsync(epoch);}
   Check(observer.Current.Recent.Count(m=>m.Status=="EN COURS")==2);Check(observer.TakeNotifications().Length==0);terminal=true;await observer.TickAsync(epoch.AddSeconds(5));
   for(int i=0;i<100&&observer.Current.Checked!=epoch.AddSeconds(5);i++){await Task.Delay(5);await observer.TickAsync(epoch.AddSeconds(5));}
   string status=end=="task_complete"?"TERMINÉ":end=="task_failed"?"ÉCHEC":"INTERROMPU";Check(observer.Current.Recent.Any(m=>m.Status==status));Check(observer.Current.Recent.Count(m=>m.Status=="EN COURS")==1);
   Check(observer.TakeNotifications().Length==(end=="task_complete"?1:0));Check(observer.TakeNotifications().Length==0);
  }finally{await observer.StopAsync();}
 }
});
await Test("Live inaccessible mission source is distinct from ambiguous silence",async()=>{
 var observer=new MissionObserver();observer.Read=now=>new LiveTaskReport{CheckedUtc=now,Message="Unavailable · fixture"};observer.Start();
 try{await observer.TickAsync(epoch);for(int i=0;i<100&&observer.Current.Checked!=epoch;i++){await Task.Delay(5);await observer.TickAsync(epoch);}
  Check(observer.Current.Summary.StartsWith("INDISPONIBLE"));Check(observer.Current.Recent.Count==0);
 }finally{await observer.StopAsync();}
});
Console.WriteLine($"{passed} passed; {failures.Count} failed");
return failures.Count==0?0:1;

sealed class FakeModule(string id="fake") : IAxisModule
{
    public string Id=>id;public string Name=>id;public string Description=>"test";public TimeSpan Interval=>TimeSpan.FromSeconds(1);
    public int Starts,Stops,Polls;public bool Fail;
    public ValueTask StartAsync(){Starts++;return ValueTask.CompletedTask;}
    public ValueTask StopAsync(){Stops++;return ValueTask.CompletedTask;}
    public ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish)
    {if(Fail)throw new InvalidOperationException("TEST failure");Polls++;return ValueTask.FromResult(new ModuleData(now,false,new Dictionary<string,string>{{"value",Polls.ToString()}}));}
}




sealed class FakeMedia : IMediaSource {
 public MediaReading Reading=new("Disponible","Song","Artist","Album","Player",true,10,100,[1,2]);
 public bool Stopped;
 public Task StartAsync(CancellationToken cancellation)=>Task.CompletedTask;
 public Task<MediaReading> ReadAsync(CancellationToken cancellation)=>Task.FromResult(Reading);
 public Task StopAsync(){Stopped=true;return Task.CompletedTask;}
}


sealed class FakeCodex(DateTimeOffset now) : ICodexQuotaSource {
 public int Calls;public bool Fail,Block;
 public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellation){
  Interlocked.Increment(ref Calls);if(Block)await Task.Delay(Timeout.Infinite,cancellation);
  if(Fail)throw new IOException("test unavailable");return new(now,[new("codex",300,25,now.AddHours(5))],null);
 }
}




sealed class FixtureGithubHandler(Func<Task> edit) : HttpMessageHandler {
 bool edited;
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation){
  if(!edited){edited=true;await edit();}
  if(request.Headers.IfNoneMatch.Count>0)return new(System.Net.HttpStatusCode.NotModified);
  string path=request.RequestUri!.AbsolutePath;
  string body=path.EndsWith("/commits")?"""[{"sha":"abc123","commit":{"message":"Implement feature","committer":{"date":"2030-01-01T12:00:00Z"}},"html_url":"https://github.com/owner/repo/commit/abc123"}]""":path.EndsWith("/pulls")?"[]":path.EndsWith("/runs")?"""{"workflow_runs":[]}""":path.Contains("/contents/")?"{}":"""{"default_branch":"main"}""";
  return new(System.Net.HttpStatusCode.OK){Content=new StringContent(body),Headers={ETag=new System.Net.Http.Headers.EntityTagHeaderValue("\"fixture\"")}};
 }
}





















sealed class ControlledQuota(DateTimeOffset now) : ICodexQuotaSource {
 public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public int Calls,MaxConcurrent,active;
 public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellation){Interlocked.Increment(ref Calls);MaxConcurrent=Math.Max(MaxConcurrent,Interlocked.Increment(ref active));Entered.TrySetResult();try{await Release.Task.WaitAsync(cancellation);return new(now,[new("codex",300,25,null)],null);}finally{Interlocked.Decrement(ref active);}}
}
