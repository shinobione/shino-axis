using System.Text.Json;
namespace Axis.Core.Modules;
public record QuotaWindow(string LimitId,int Minutes,double Used,DateTimeOffset? Reset) {
 public double Remaining=>100-Used;
 public string Label=>Minutes switch{300=>"5 heures",10080=>"Hebdomadaire",_=>$"{Minutes} min"};
}
public record QuotaSnapshot(DateTimeOffset Timestamp,IReadOnlyList<QuotaWindow> Windows,int? ResetCredits,DateTimeOffset? ResetCreditsExpiry=null);
public static class QuotaParser {
 public static QuotaSnapshot Parse(string json,DateTimeOffset now){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
  var windows=new Dictionary<(string,int),QuotaWindow>();
  static JsonElement Get(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var v)?v:default;
  void Bucket(JsonElement bucket,string fallback,bool replace){
   if(bucket.ValueKind!=JsonValueKind.Object)return;
   string id=Get(bucket,"limitId").ValueKind==JsonValueKind.String?Get(bucket,"limitId").GetString()??fallback:fallback;
   foreach(var name in new[]{"primary","secondary"}){
    var w=Get(bucket,name);var mins=Get(w,"windowDurationMins");var used=Get(w,"usedPercent");
    if(!mins.TryGetSafeInt(out int m)||m<=0||used.ValueKind!=JsonValueKind.Number||!used.TryGetDouble(out double u)||!double.IsFinite(u)||u<0||u>100)continue;
    DateTimeOffset? reset=null;var time=Get(w,"resetsAt");
    if(time.ValueKind==JsonValueKind.Number&&time.TryGetInt64(out var seconds))try{reset=DateTimeOffset.FromUnixTimeSeconds(seconds);}catch(ArgumentOutOfRangeException){}
    var key=(id,m);var value=new QuotaWindow(id,m,u,reset);
    if(replace||!windows.ContainsKey(key))windows[key]=value;
    else if(windows[key]!=value)throw new InvalidDataException("Fenêtres Codex contradictoires.");
   }
  }
  Bucket(Get(root,"rateLimits"),"codex",false);
  var map=Get(root,"rateLimitsByLimitId");
  if(map.ValueKind==JsonValueKind.Object)foreach(var b in map.EnumerateObject()) {
   // The keyed map is authoritative for its category; replace the legacy bucket.
   foreach(var key in windows.Keys.Where(k=>k.Item1==b.Name).ToArray())windows.Remove(key);
   Bucket(b.Value,b.Name,false);
  }
  int? credits=null;var credit=Get(Get(root,"rateLimitResetCredits"),"availableCount");
  if(credit.TryGetSafeInt(out int c)&&c>=0)credits=c;
  DateTimeOffset? expiry=null;var rawExpiry=Get(Get(root,"rateLimitResetCredits"),"expiresAt");
  if(rawExpiry.ValueKind==JsonValueKind.Number&&rawExpiry.TryGetInt64(out long expirySeconds))try{expiry=DateTimeOffset.FromUnixTimeSeconds(expirySeconds);}catch(ArgumentOutOfRangeException){}
  else if(rawExpiry.ValueKind==JsonValueKind.String&&rawExpiry.GetString() is {} expiryText&&(expiryText.EndsWith("Z",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(expiryText,@"[+-]\d\d:\d\d$"))&&DateTimeOffset.TryParse(expiryText,out var instant))expiry=instant;
  return new(now,windows.Values.OrderBy(w=>w.LimitId=="codex"?0:1).ThenBy(w=>w.LimitId).ThenBy(w=>w.Minutes).ToArray(),credits,expiry);
 }
 static bool TryGetSafeInt(this JsonElement e,out int value){value=0;return e.ValueKind==JsonValueKind.Number&&e.TryGetInt32(out value);}
}
public sealed class QuotaHistory(string directory,int capacity=1000) {
 readonly string path=Path.Combine(directory,"quota-history.json");
 readonly List<QuotaSnapshot> samples=[];
 public IReadOnlyList<QuotaSnapshot> Samples {get{lock(samples)return samples.ToArray();}}
 public string? Warning {get;private set;} bool writable=true;
 public async Task LoadAsync(){
  if(!File.Exists(path))return;
  try{
   if(new FileInfo(path).Length>4_000_000)throw new InvalidDataException();
   var loaded=JsonSerializer.Deserialize<List<QuotaSnapshot>>(await File.ReadAllTextAsync(path))??throw new InvalidDataException();
   if(loaded.Any(s=>s is null||s.Windows is null||s.Windows.Any(w=>w is null||w.Minutes<=0||!double.IsFinite(w.Used)||w.Used<0||w.Used>100)))throw new InvalidDataException();
   lock(samples){samples.Clear();samples.AddRange(loaded.TakeLast(capacity));}
  }catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){writable=false;Warning="Historique illisible, conservé ; nouveaux relevés en mémoire uniquement.";}
 }
 public async Task AppendAsync(QuotaSnapshot snapshot){
  lock(samples){
   var last=samples.LastOrDefault();
   if(last is not null&&last.Windows.SequenceEqual(snapshot.Windows)&&last.ResetCredits==snapshot.ResetCredits&&last.ResetCreditsExpiry==snapshot.ResetCreditsExpiry&&
      (snapshot.Windows.Count==0||snapshot.Timestamp>=last.Timestamp&&snapshot.Timestamp-last.Timestamp<TimeSpan.FromMinutes(5)))return;
   samples.Add(snapshot);if(samples.Count>capacity)samples.RemoveRange(0,samples.Count-capacity);}
  if(!writable)return;
  try{Directory.CreateDirectory(directory);await File.WriteAllTextAsync(path+".tmp",JsonSerializer.Serialize(Samples));File.Move(path+".tmp",path,true);}
  catch(Exception e)when(e is IOException or UnauthorizedAccessException){Warning="Historique conservé en mémoire ; écriture locale indisponible.";}
 }
 public static bool Connect(QuotaSnapshot a,QuotaSnapshot b,string id,int minutes){
  var x=a.Windows.FirstOrDefault(w=>w.LimitId==id&&w.Minutes==minutes);var y=b.Windows.FirstOrDefault(w=>w.LimitId==id&&w.Minutes==minutes);
  return x is not null&&y is not null&&b.Timestamp>a.Timestamp&&b.Timestamp-a.Timestamp<=TimeSpan.FromMinutes(11)&&x.Reset==y.Reset&&y.Used>=x.Used;
 }
}
public interface ICodexQuotaSource {Task<QuotaSnapshot> ReadAsync(CancellationToken cancellation);}
public sealed class CodexModule(ICodexQuotaSource source,QuotaHistory history) : IAxisModule {
 public string Id=>"codex";public string Name=>"AXIS // CODEX";public string Description=>"Quotas app-server • tours locaux observés";
 public TimeSpan Interval=>TimeSpan.FromSeconds(1);
 public QuotaSnapshot? Snapshot {get;private set;}
 public QuotaHistory History=>history;
 public Axis.Core.Missions.MissionObserver Missions {get;}=new();
 public string? Error {get;private set;}
 CancellationTokenSource? lifetime,readCancellation;Task<QuotaSnapshot>? request;DateTimeOffset next,started;int manual,resume,refreshSeconds=30,failures;bool loaded,discardRequest,verified;
 public int RefreshSeconds {get=>Volatile.Read(ref refreshSeconds);set{if(value is not (30 or 60 or 120 or 300))throw new ArgumentOutOfRangeException(nameof(value));Interlocked.Exchange(ref refreshSeconds,value);RequestRefresh();}}
 public bool IsFresh(DateTimeOffset now)=>verified&&Volatile.Read(ref resume)==0&&!discardRequest&&Error is null&&Snapshot is {} s&&now>=s.Timestamp.AddSeconds(-5)&&now-s.Timestamp<=TimeSpan.FromSeconds(Math.Max(90,RefreshSeconds*2));
 public DateTimeOffset NextRefresh=>next;
 public void OnResume(){Interlocked.Exchange(ref resume,1);Missions.OnResume();}
 public bool IsReading=>request is not null;
 public void RequestRefresh()=>Interlocked.Exchange(ref manual,1);
 public async ValueTask StartAsync(){Missions.Start();lifetime=new();if(!loaded){await history.LoadAsync();Snapshot=history.Samples.LastOrDefault(s=>s.Windows.Count>0);loaded=true;}next=DateTimeOffset.MinValue;failures=0;verified=false;discardRequest=false;Interlocked.Exchange(ref resume,0);}
 public async ValueTask StopAsync(){
  lifetime?.Cancel();await Missions.StopAsync();if(request is not null)try{await request;}catch(Exception){}
  request=null;readCancellation?.Dispose();readCancellation=null;lifetime?.Dispose();lifetime=null;Interlocked.Exchange(ref manual,0);
 }
 public async ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish){
  if(Interlocked.Exchange(ref resume,0)==1){discardRequest=request is not null;readCancellation?.Cancel();Error="Reprise • nouveau relevé requis";next=DateTimeOffset.MinValue;failures=0;RequestRefresh();}
  await Missions.TickAsync(now);
  if(request is {IsCompleted:true} task){
   try{
    var result=await task;
    if(!discardRequest){Snapshot=result;verified=true;Error=null;failures=0;await history.AppendAsync(Snapshot);publish("codex.sync","Quotas actualisés");}
   }
   catch(Exception){if(!discardRequest){failures=Math.Min(5,failures+1);await history.AppendAsync(new(now,[],null));Error="Lecture Codex indisponible. Vérifier la connexion du CLI Codex.";publish("codex.unavailable","Lecture indisponible ; dernier relevé conservé");}}
   request=null;readCancellation?.Dispose();readCancellation=null;
   next=discardRequest?DateTimeOffset.MinValue:failures>0?now.AddSeconds(Math.Min(300,RefreshSeconds*Math.Pow(2,failures))):started.AddSeconds(RefreshSeconds);
   discardRequest=false;
  }
  bool refresh=Interlocked.Exchange(ref manual,0)==1;
  if(request is null&&lifetime is not null&&(refresh||now>=next||now<started.AddSeconds(-5))){started=now;readCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=readCancellation.Token;request=Task.Run(()=>source.ReadAsync(token));}
  var state=Error is not null?"Données anciennes • "+Error:request is not null?"Actualisation en cours":Snapshot is null?"Indisponible":Snapshot.Windows.Count==0?"Aucun quota fourni":!IsFresh(now)?"Données anciennes":"Connecté";
  return new(now,false,new Dictionary<string,string>{
   ["status"]=state,["lastSuccess"]=Snapshot?.Timestamp.ToLocalTime().ToString("dd/MM HH:mm:ss zzz")??"Jamais",
   ["summary"]=Snapshot is null?"Indisponible":string.Join(" · ",Snapshot.Windows.Where(w=>w.LimitId=="codex").Select(w=>$"{w.Label} : {w.Remaining:0.#} % restant")),
   ["activity"]=Missions.SummaryAt(now)
  });
 }
}
