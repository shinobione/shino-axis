using Axis.Core.Projects;
namespace Axis.Core.Modules;
public sealed class ProjectsModule(ProjectStore store,int port) : IAxisModule {
 public string Id=>"projects";public string Name=>"AXIS // PROJECTS";public string Description=>"CONTROL natif • collecte loopback et projets locaux";public TimeSpan Interval=>TimeSpan.FromSeconds(2);
 public ProjectStore Store=>store;public int Port=>port;
 ProjectReceiver? receiver;public bool ReceiverReady=>receiver is not null;public DateTimeOffset? LastDeltaReceivedAt=>receiver?.LastDeltaReceivedAt;
 public async Task RestartReceiverAsync(){if(lifetime is null)return;if(receiver is not null)await receiver.StopAsync();receiver=null;var next=new ProjectReceiver(store,port,J.S(store.Snapshot["axisProjects"],"extensionId",Environment.GetEnvironmentVariable("AXIS_CONTROL_EXTENSION_ID")??""),Environment.GetEnvironmentVariable("SHINO_CONTROL_TOKEN")??"");await next.StartAsync();receiver=next;}
 CancellationTokenSource? lifetime;Task<(int Updated,int Failed)>? sync;DateTimeOffset next;int manual;
 public string SyncStatus {get;private set;}="Pas de synchronisation.";
 public bool IsSyncing=>sync is not null;
 public void RequestSync()=>Interlocked.Exchange(ref manual,1);
 public async ValueTask StartAsync(){await store.LoadAsync();lifetime=new();await RestartReceiverAsync();next=DateTimeOffset.UtcNow.AddMinutes(5);}
 public async ValueTask StopAsync(){lifetime?.Cancel();if(receiver is not null)await receiver.StopAsync();receiver=null;if(sync is not null)try{await sync;}catch(OperationCanceledException){}sync=null;lifetime?.Dispose();lifetime=null;}
 public ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish){
  if(sync is {IsCompleted:true} task){try{var result=task.GetAwaiter().GetResult();SyncStatus=$"GitHub : {result.Updated} modifié(s), {result.Failed} échec(s).";publish("projects.github",SyncStatus);}catch(Exception){SyncStatus="Synchronisation indisponible.";}sync=null;next=now.AddMinutes(5);}
  bool request=Interlocked.Exchange(ref manual,0)==1;var state=store.Snapshot;
  if(sync is null&&(request||now>=next&&J.B(state["axisProjects"],"autoGithub")))sync=Task.Run(()=>new ProjectGithub(store).SyncAsync(lifetime?.Token??CancellationToken.None));
  return ValueTask.FromResult(new ModuleData(now,false,new Dictionary<string,string>{{"summary",$"{J.A(state,"projects").Count} projets • {J.A(state,"sources").Count} sources • réception 127.0.0.1:{port}"},{"sync",SyncStatus}}));
 }
}
