using System.Text.Json;
namespace Axis.Core;
public enum ModuleState { Stopped, Active, Error }
public record ModuleData(DateTimeOffset Timestamp,bool IsSimulation,IReadOnlyDictionary<string,string> Values);
public record AxisEvent(DateTimeOffset Timestamp,string ModuleId,string Kind,string Message);
public interface IAxisModule {
 string Id {get;} string Name {get;} string Description {get;} TimeSpan Interval {get;}
 ValueTask StartAsync(); ValueTask StopAsync();
 ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish);
}
public sealed class ModuleEntry(IAxisModule module) {
 public IAxisModule Module {get;}=module;
 public string Id=>Module.Id; public string Name=>Module.Name;public string Description=>Module.Description;
 public ModuleState State {get;internal set;}=ModuleState.Stopped;
 public ModuleData? Data {get;internal set;} public string? Error {get;internal set;}
 internal DateTimeOffset? LastPoll;
 internal bool Enabled; internal DateTimeOffset RetryAt; internal int Failures;
}
public sealed class ModuleRuntime {
 public IReadOnlyList<ModuleEntry> Modules {get;}
 readonly List<AxisEvent> events=[]; readonly SemaphoreSlim gate=new(1);
 int shuttingDown;
 public IReadOnlyList<AxisEvent> Events {get{lock(events)return events.ToArray();}}
 public ModuleRuntime(IEnumerable<IAxisModule> modules) {
  Modules=modules.Select(m=>new ModuleEntry(m)).ToArray();
  if(Modules.Select(m=>m.Id).Distinct(StringComparer.Ordinal).Count()!=Modules.Count)throw new ArgumentException("Duplicate module ID");
 }
 void Publish(DateTimeOffset now,string id,string kind,string message) {
  lock(events){events.Add(new(now,id,kind,message)); if(events.Count>200)events.RemoveAt(0);}
 }
 public async Task SetEnabledAsync(string id,bool enabled,DateTimeOffset now) {
  await gate.WaitAsync();try {if(Volatile.Read(ref shuttingDown)!=0)return;var entry=Modules.Single(m=>m.Id==id);entry.Enabled=enabled;await Set(entry,enabled,now);} finally{gate.Release();}
 }
 async Task Set(ModuleEntry entry,bool enabled,DateTimeOffset now) {
  if(enabled&&entry.State==ModuleState.Active||!enabled&&entry.State==ModuleState.Stopped)return;
  try {
   if(enabled) {if(entry.State==ModuleState.Error)await entry.Module.StopAsync();await entry.Module.StartAsync();}
   else await entry.Module.StopAsync();
   entry.State=enabled?ModuleState.Active:ModuleState.Stopped;entry.Data=null;entry.Error=null;entry.LastPoll=null;
   Publish(now,entry.Id,"lifecycle",enabled?"Activé":"Arrêté");
  }catch(Exception e){Failed(entry,now,e);}
 }
 void Failed(ModuleEntry entry,DateTimeOffset now,Exception error){
  entry.State=ModuleState.Error;entry.Data=null;entry.Error="Opération indisponible ("+error.GetType().Name+").";
  entry.Failures=Math.Min(5,entry.Failures+1);entry.RetryAt=now.AddSeconds(Math.Min(60,5*Math.Pow(2,entry.Failures-1)));
 }
 public async Task TickAsync(DateTimeOffset now) {
  await gate.WaitAsync();try {
   if(Volatile.Read(ref shuttingDown)!=0)return;
   foreach(var m in Modules.Where(m=>m.Enabled&&m.State==ModuleState.Error&&now>=m.RetryAt))await Set(m,true,now);
   foreach(var m in Modules.Where(m=>m.State==ModuleState.Active)) {
    if(m.LastPoll is {} last&&now>=last&&now-last<m.Module.Interval)continue;
    try {m.Data=await m.Module.PollAsync(now,(k,v)=>Publish(now,m.Id,k,v));m.LastPoll=now;m.Failures=0;}
    catch(Exception e) {Failed(m,now,e);Publish(now,m.Id,"error",m.Error!);}
   }
  } finally{gate.Release();}
 }
 public async Task StopAllAsync(DateTimeOffset now) {
  await gate.WaitAsync();try {foreach(var m in Modules){m.Enabled=false;await Set(m,false,now);}}finally{gate.Release();}
 }
 public Task ShutdownAsync(DateTimeOffset now){Interlocked.Exchange(ref shuttingDown,1);return StopAllAsync(now);}
}
public sealed class AppSettings {
 public int Version {get;set;}=1;
 public int CodexRefreshSeconds {get;set;}=30;
 public bool NotifyMissionCompletion {get;set;}=false;
 public bool MinimizeToTray {get;set;}=true;
 public bool CloseToTray {get;set;}=false;
 public bool StartWithWindows {get;set;}=false;
 public bool AnimationsEnabled {get;set;}=true;
 public bool RestoreLastPage {get;set;}=false;
 public string LastPage {get;set;}="Accueil";
 [System.Text.Json.Serialization.JsonExtensionData]
 public Dictionary<string,JsonElement>? AdditionalSettings {get;set;}
 public Dictionary<string,bool> EnabledModules {get;set;}=new(){{"pc",true},{"network",true},{"media",true},{"codex",true},{"projects",true}};
 public static AppSettings Default()=>new();
}
public sealed class SettingsStore(string directory) {
 readonly string path=Path.Combine(directory,"settings.json");bool writable=true;
 readonly SemaphoreSlim saveGate=new(1);string? saved;
 static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
 public async Task<(AppSettings Settings,string? Warning)> LoadAsync() {
  if(!File.Exists(path))return(AppSettings.Default(),null);
  try {
   var s=JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(path),Options);
   if(s is null||s.Version!=1||s.EnabledModules is null)throw new InvalidDataException();
   if(s.CodexRefreshSeconds is not (30 or 60 or 120 or 300))s.CodexRefreshSeconds=30;
   return(s,null);
  }catch(Exception e)when(e is JsonException or IOException or UnauthorizedAccessException or InvalidDataException) {
   writable=false;return(AppSettings.Default(),"Configuration illisible ou incompatible, conservée. Utilisation des valeurs par défaut.");
  }
 }
 public async Task SaveAsync(AppSettings settings) {
  string json=JsonSerializer.Serialize(settings,Options);
  await saveGate.WaitAsync();try{
  if(!writable)throw new IOException("Configuration conservée : sauvegarde bloquée pour protéger le fichier existant.");
  if(saved==json)return;
  Directory.CreateDirectory(directory);string temp=path+".tmp";
  await File.WriteAllTextAsync(temp,json);File.Move(temp,path,true);saved=json;
  }finally{saveGate.Release();}
 }
}
