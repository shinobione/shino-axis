using System.Diagnostics;
using Axis.Core.Missions;
using Axis.Core.Modules;

// Optional wall-clock qualification. No CLI, private profile or real mission source.
internal static class CodexLiveTiming {
 public static async Task<int> RunAsync(){
  var clock=Stopwatch.StartNew();var source=new TimedQuota(clock);
  var module=new CodexModule(source,new QuotaHistory(Path.Combine(Path.GetTempPath(),"axis-timing-"+Guid.NewGuid())));
  var missions=new List<double>();
  module.Missions.Read=now=>{lock(missions)missions.Add(clock.Elapsed.TotalSeconds);return new LiveTaskReport{CheckedUtc=now};};
  await module.StartAsync();
  try{
   while(clock.Elapsed.TotalSeconds<31.5){await module.PollAsync(DateTimeOffset.UtcNow,(_,_)=>{});await Task.Delay(50);}
  }finally{await module.StopAsync();}
  static double[] Gaps(IEnumerable<double> times)=>times.Zip(times.Skip(1),(a,b)=>b-a).ToArray();
  var quotaGaps=Gaps(source.Times);var missionGaps=Gaps(missions);
  Console.WriteLine("SIMULATION • real elapsed clock, no real collectors or missions");
  Console.WriteLine($"Quota reads={source.Times.Count}; gaps(s)={string.Join(", ",quotaGaps.Select(x=>x.ToString("F3")))}");
  Console.WriteLine($"Mission scans={missions.Count}; gaps(s)={string.Join(", ",missionGaps.Select(x=>x.ToString("F3")))}");
  bool pass=quotaGaps.Length==1&&Math.Abs(quotaGaps[0]-30)<.5&&missionGaps.Length>=5&&missionGaps.All(x=>Math.Abs(x-5)<.5);
  Console.WriteLine(pass?"PASS elapsed polling cadence":"FAIL elapsed polling cadence");return pass?0:1;
 }
 sealed class TimedQuota(Stopwatch clock):ICodexQuotaSource {
  internal readonly List<double> Times=[];
  public Task<QuotaSnapshot> ReadAsync(CancellationToken cancellation){Times.Add(clock.Elapsed.TotalSeconds);return Task.FromResult(new QuotaSnapshot(DateTimeOffset.UtcNow,[new("codex",300,25,null)],null));}
 }
}
