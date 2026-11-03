namespace Axis.Core.Modules;

// No transport or presentation strings: reusable local observations for future adapters.
public record PcSnapshot(DateTimeOffset CapturedAt, PcReading Reading) {
 public TimeSpan MaxAge=>TimeSpan.FromSeconds(6);
 public bool IsFresh(DateTimeOffset now)=>now>=CapturedAt&&now-CapturedAt<=MaxAge;
 public const string MemoryUnit="GiB", LoadUnit="percent", TemperatureUnit="celsius";
}
public record MediaSnapshot(DateTimeOffset CapturedAt, MediaReading Reading) {
 public bool IsAvailable=>Reading.Status=="Disponible";
 public const string TimelineUnit="seconds";
 public TimeSpan MaxAge=>TimeSpan.FromSeconds(6);
 public bool IsFresh(DateTimeOffset now)=>now>=CapturedAt&&now-CapturedAt<=MaxAge;
}
public sealed class CpuCounter {
 long? idle,total;DateTimeOffset? sampled;
 public void Reset(){idle=total=null;sampled=null;}
 public double? Sample(long nextIdle,long nextTotal,DateTimeOffset now){
  double? result=null;
  if(total is {} t&&idle is {} i&&sampled is {} at&&now>at&&now-at<=TimeSpan.FromSeconds(10)
    &&nextTotal>t&&nextIdle>=i&&nextIdle-i<=nextTotal-t)
   result=100.0*(1.0-(double)(nextIdle-i)/(nextTotal-t));
  idle=nextIdle;total=nextTotal;sampled=now;return result;
 }
}
public record MediaCandidate(string Id,bool? Playing);
public static class MediaRules {
 public static string? Select(IReadOnlyList<MediaCandidate> sessions,string? previous,string? current){
  bool Has(string? id,bool playing)=>id is not null&&sessions.Any(s=>s.Id==id&&(!playing||s.Playing==true));
  if(Has(previous,true))return previous;
  if(Has(current,true))return current;
  var active=sessions.FirstOrDefault(s=>s.Playing==true);if(active is not null)return active.Id;
  if(Has(previous,false))return previous;
  return Has(current,false)?current:sessions.FirstOrDefault()?.Id;
 }
 public static double? Position(double raw,double? duration,bool? playing,double? rate,DateTimeOffset updated,DateTimeOffset now){
  if(!double.IsFinite(raw)||raw<0)return null;
  double value=raw;
  // Missing rate is not proof of a moving timeline (Spotify may expose a stale anchor).
  if(duration is >0&&playing==true&&rate is {} speed&&double.IsFinite(speed)&&speed>0&&speed<=4
    &&now>=updated&&now-updated<=TimeSpan.FromSeconds(30))value+=(now-updated).TotalSeconds*speed;
  return duration is {} d&&double.IsFinite(d)&&d>0?Math.Clamp(value,0,d):value;
 }
}
