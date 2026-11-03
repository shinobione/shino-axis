using Axis.Core.Modules;
namespace Axis.Core.Presentation;
// Display conventions from local PERISCOPE defaults and LINK DashboardV2 specification.
// No measurement is created by this presentation policy.
public static class DisplayRules {
 public static string QuotaHex(double? remaining){
  if(remaining is not {} value||!double.IsFinite(value))return "#8BA0B1";
  int rounded=(int)Math.Round(Math.Clamp(value,0,100),MidpointRounding.AwayFromZero);
  return rounded>=76?"#A477FF":rounded>=51?"#51D7AF":rounded>=26?"#E5B64D":rounded>=11?"#F28C38":"#EE4B55";
 }
 public static double? RamLoad(double used,double total)=>double.IsFinite(used)&&double.IsFinite(total)&&total>0&&used>=0&&used<=total?used/total*100:null;
 public static double? TemperatureLoad(double value)=>double.IsFinite(value)?Math.Clamp((value-30)/60*100,0,100):null;
 public static int LinkBand(double? load,int previous=-1){
  if(load is not {} value||!double.IsFinite(value)||value<0)return -1;
  value=Math.Clamp(value,0,100);double[] thresholds=[20,50,80];
  if(previous<0||previous>3)return thresholds.Count(t=>value>=t);
  int result=previous;
  foreach(int candidate in Enumerable.Range(0,4)){
   if(candidate>previous&&value>=thresholds[candidate-1]+2)result=candidate;
   if(candidate<previous&&value<thresholds[candidate]-2)result=Math.Min(result,candidate);
  }
  return result;
 }
 public static string LinkHex(int band)=>band switch{0=>"#66D39A",1=>"#D8C35E",2=>"#D9894A",3=>"#8E394B",_=>"#8BA0B1"};
 public static IReadOnlyList<QuotaSnapshot> FilterHistory(IEnumerable<QuotaSnapshot> samples,DateTimeOffset now,int days){
  if(days is not (1 or 7 or 30))throw new ArgumentOutOfRangeException(nameof(days));
  return samples.Where(s=>s.Timestamp>=now.AddDays(-days)&&s.Timestamp<=now).OrderBy(s=>s.Timestamp).ToArray();
 }
}
