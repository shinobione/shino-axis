using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
namespace Axis.Core.Modules;

public record NetworkReading(string Id,string Name,string Type,string Address,bool Connected,long Received,long Sent,double Seconds);
public sealed class NetworkModule(Func<NetworkReading?> read) : IAxisModule {
 public string Id=>"network";public string Name=>"AXIS // NETWORK";public string Description=>"Interface Windows et trafic réel • aucune requête externe";
 public TimeSpan Interval=>TimeSpan.FromSeconds(1);
 NetworkReading? previous;double downloaded,uploaded;string? lastInterface;
 public ValueTask StartAsync(){previous=null;return ValueTask.CompletedTask;}
 public ValueTask StopAsync(){previous=null;return ValueTask.CompletedTask;}
 static string F(double? value,string unit)=>value is {} v?v.ToString("0.00",CultureInfo.GetCultureInfo("fr-FR"))+unit:"Indisponible";
 public ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish) {
  var current=read();double? down=null,up=null;
  if(current is {Connected:true}) {
   if(lastInterface!=current.Id){publish("network.interface","Interface active modifiée");lastInterface=current.Id;}
   if(previous is {Connected:true} p&&p.Id==current.Id&&current.Seconds>p.Seconds&&current.Received>=p.Received&&current.Sent>=p.Sent) {
    double received=current.Received-p.Received,sent=current.Sent-p.Sent,elapsed=current.Seconds-p.Seconds;
    downloaded+=received;uploaded+=sent;down=received/elapsed/1_000_000;up=sent/elapsed/1_000_000;
   }
  }else if(previous is {Connected:true})publish("network.disconnected","Connexion indisponible");
  previous=current;
  return ValueTask.FromResult(new ModuleData(now,false,new Dictionary<string,string>{
   ["name"]=current?.Name??"Indisponible",["type"]=current?.Type??"Indisponible",["ipv4"]=current?.Address??"Indisponible",
   ["connection"]=current is {Connected:true}?"Connecté":current is null?"Indisponible":"Déconnecté",
   ["download"]=F(down," Mo/s"),["upload"]=F(up," Mo/s"),["downloaded"]=F(downloaded/1_000_000," Mo"),["uploaded"]=F(uploaded/1_000_000," Mo")
  }));
 }
 public static NetworkReading? ReadWindows() {
  var candidates=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel).ToArray();
  var active=candidates.Where(n=>n.OperationalStatus==OperationalStatus.Up).Select(n=>(Nic:n,Props:n.GetIPProperties()))
   .OrderByDescending(n=>n.Props.GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork&&!g.Address.Equals(System.Net.IPAddress.Any)))
   .ThenBy(n=>n.Nic.Id,StringComparer.Ordinal).FirstOrDefault();
  if(active.Nic is null)return new("","Aucune interface active","Indisponible","Indisponible",false,0,0,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency);
  var stats=active.Nic.GetIPStatistics();
  return new(active.Nic.Id,active.Nic.Name,active.Nic.NetworkInterfaceType.ToString(),
   active.Props.UnicastAddresses.FirstOrDefault(a=>a.Address.AddressFamily==AddressFamily.InterNetwork)?.Address.ToString()??"Indisponible",
   true,stats.BytesReceived,stats.BytesSent,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency);
 }
}
