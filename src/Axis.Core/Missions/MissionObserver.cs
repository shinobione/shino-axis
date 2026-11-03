using System.Collections.Concurrent;
namespace Axis.Core.Missions;
public record MissionObservation(string Identity,string Thread,string Workspace,string Status,string Duration,string Model,string Effort,bool Subagent,DateTimeOffset Started,DateTimeOffset? Finished,DateTimeOffset? LastActivity=null) {
 public string StatusAt(DateTimeOffset now,bool fresh=true)=>Status=="EN COURS"&&(!fresh||LastActivity is not {} activity||now<activity.AddSeconds(-5)||now-activity>TimeSpan.FromSeconds(90))?"À VÉRIFIER":Status;
 public string DurationAt(DateTimeOffset now,bool fresh=true){if(StatusAt(now,fresh)!="EN COURS")return Duration;var span=now-Started;return span<TimeSpan.Zero?"—":span.TotalHours>=1?$"{(int)span.TotalHours} h {span.Minutes:00}:{span.Seconds:00}":$"{(int)span.TotalMinutes:00}:{span.Seconds:00}";}
}
public record MissionSnapshot(DateTimeOffset Checked,string Summary,IReadOnlyList<MissionObservation> Recent,int ChildCount,bool Limited);
public sealed class MissionObserver {
 internal Func<DateTimeOffset,LiveTaskReport> Read=LiveTaskReader.Read;
 Task<LiveTaskReport>? pending;LiveTaskReport? previous;DateTimeOffset next;int resume;bool discard;
 readonly HashSet<string> sent=[];readonly ConcurrentQueue<MissionObservation> notifications=new();
 public MissionSnapshot Current {get;private set;}=new(DateTimeOffset.MinValue,"NON VÉRIFIÉ — aucune observation",[],0,false);
 public void OnResume(){Interlocked.Exchange(ref resume,1);TakeNotifications();Current=new(DateTimeOffset.MinValue,"À VÉRIFIER — reprise",[],0,false);}
 public bool IsFresh(DateTimeOffset now)=>Volatile.Read(ref resume)==0&&Current.Checked!=DateTimeOffset.MinValue&&now-Current.Checked<=TimeSpan.FromSeconds(25)&&now>=Current.Checked.AddSeconds(-5);
 public string SummaryAt(DateTimeOffset now)=>!IsFresh(now)?"NON VÉRIFIÉ — observation périmée":Current.Summary;
 public MissionObservation[] TakeNotifications(){var list=new List<MissionObservation>();while(notifications.TryDequeue(out var item))list.Add(item);return Volatile.Read(ref resume)==1?[]:list.ToArray();}
 public void Start(){discard=false;Interlocked.Exchange(ref resume,0);previous=null;next=DateTimeOffset.MinValue;Current=new(DateTimeOffset.MinValue,"NON VÉRIFIÉ — initialisation",[],0,false);}
 public async Task StopAsync(){if(pending is not null)try{await pending;}catch(Exception){}pending=null;previous=null;LiveTaskReader.ClearCacheForTests();TakeNotifications();Current=new(DateTimeOffset.MinValue,"NON VÉRIFIÉ — observation arrêtée",[],0,false);}
 public async Task TickAsync(DateTimeOffset now){
  if(Interlocked.Exchange(ref resume,0)==1){previous=null;TakeNotifications();Current=new(DateTimeOffset.MinValue,"À VÉRIFIER — reprise",[],0,false);discard=pending is not null;next=DateTimeOffset.MinValue;}
  if(pending is {IsCompleted:true} task){
   try{
    var report=await task;
    if(discard){discard=false;pending=null;previous=null;next=DateTimeOffset.MinValue;return;}
    foreach(var terminal in TaskNotificationRules.NewTerminals(previous,report).Where(t=>t.Phase==LiveTaskPhase.Completed)){
     if(sent.Add(terminal.Identity)&&notifications.Count<32)notifications.Enqueue(Project(terminal,now));
    }
    if(sent.Count>1000)sent.Clear();
    previous=report;var display=report.DisplayAt(now);
    string summary=display.Kind==TaskDisplayKind.ObservedActive&&display.Primary is {} active?
     "EN COURS • tour observé localement • "+active.Workspace:
     display.Kind==TaskDisplayKind.DelegatedObserved?"NON VÉRIFIÉ • sous-agent lié observé ; parent non attesté":
     report.Message.StartsWith("Unavailable",StringComparison.Ordinal)?"INDISPONIBLE • source locale inaccessible":
     "À VÉRIFIER • aucune activité courante attestée";
    var recent=report.Tasks.OrderBy(t=>!t.IsSubagent&&t.Phase==LiveTaskPhase.Active?0:t.IsSubagent?2:1).ThenByDescending(t=>t.StartedUtc).Take(20).Select(t=>Project(t,now)).ToArray();
    Current=new(report.CheckedUtc,summary,recent,report.Tasks.Count(t=>t.IsSubagent),report.RecoveryLimitedSessions>0||report.RootSessionsExcludedByCap>0);
   }catch(Exception){previous=null;Current=new(now,"INDISPONIBLE — source locale inaccessible",[],0,true);}
   pending=null;
  }
  if(pending is null&&(now>=next||now<next.AddSeconds(-10))){next=now.AddSeconds(5);pending=Task.Run(()=>Read(now));}
 }
 static MissionObservation Project(LiveTask t,DateTimeOffset now){
  string status=t.Phase switch{LiveTaskPhase.Completed=>"TERMINÉ",LiveTaskPhase.Interrupted=>"INTERROMPU",LiveTaskPhase.Failed=>"ÉCHEC",LiveTaskPhase.Active when t.IsRootConfirmed&&!t.IsSubagent&&now>=t.LastWriteUtc.AddSeconds(-5)&&now-t.LastWriteUtc<=TimeSpan.FromSeconds(90)=>"EN COURS",_=>"À VÉRIFIER"};
  if(t.IdentityUncertain||(!t.IsRootConfirmed&&!t.IsSubagent))status="À VÉRIFIER";
  return new(t.Identity,t.ThreadId,t.Workspace,status,t.Duration(now),t.Model,t.Effort,t.IsSubagent,t.StartedUtc,t.FinishedUtc,t.LastWriteUtc);
 }
}
