using Axis.Core.Modules;
namespace Axis.Core.Presentation;

public record ModuleHealth(string State,DateTimeOffset? Observed,string Detail,bool Attention);
public static class CockpitStatus {
 public static ModuleHealth Read(ModuleEntry entry,DateTimeOffset now){
  if(entry.State==ModuleState.Stopped)return new("Arrêté",null,"Acquisition AXIS désactivée",false);
  if(entry.State==ModuleState.Error)return new("Erreur",null,entry.Error??"Nouvel essai automatique",true);
  var data=entry.Data;
  if(data is null)return new("Actif",null,"En attente de la première observation",false);
  var time=entry.Module is CodexModule c?c.Snapshot?.Timestamp:data.Timestamp;
  double ttl=entry.Module is CodexModule codex?Math.Max(90,codex.RefreshSeconds*2):6;
  if(time is null||now<time||now-time>TimeSpan.FromSeconds(ttl))return new("Dégradé",time,"Source inconnue ou observation périmée",true);
  if(entry.Module is CodexModule cm&&!cm.IsFresh(now))return new("Dégradé",time,"Quotas non vérifiés · missions observées séparément",true);
  if(entry.Id=="media"){
   var status=data.Values.GetValueOrDefault("status");
   if(status=="Aucune session")return new("Actif",time,"Aucun lecteur en cours",false);
   if(status!="Disponible")return new("Indisponible",time,"Source multimédia indisponible",true);
  }
  if(entry.Id=="pc"&&data.Values.GetValueOrDefault("cpu")=="Indisponible"&&data.Values.GetValueOrDefault("ram")=="Indisponible")return new("Indisponible",time,"Mesures système indisponibles",true);
  if(entry.Id=="network"&&data.Values.GetValueOrDefault("connection")!="Connecté")return new("Dégradé",time,"Aucune connexion locale confirmée",true);
  return new("Actif",time,"Observation récente · santé externe non déduite",false);
 }
}

public record ExternalPresence(string Name,bool? Detected,DateTimeOffset Observed,string Source){
 public string State=>Detected switch{true=>"Processus détecté · santé inconnue",false=>"Non détecté · service inconnu",_=>"Présence inconnue"};
}
public static class ExternalComponents {
 // Exact executable basenames only. Generic python/node processes are deliberately not attributed.
 public static IReadOnlyList<ExternalPresence> Observe(Func<string,bool> exists,DateTimeOffset now){
  return new[]{("SHINO // LINK",new[]{"SHINO-LINK","shino_link"}),("PERISCOPE",new[]{"PERISCOPE","SHINO-PERISCOPE"}),
   ("CONTROL CORE",new[]{"SHINO-CONTROL","CONTROL-CORE"}),("RELAY / HUB",new[]{"SHINO-RELAY","SHINO-HUB"})}.Select(item=>{
    bool? found;try{found=item.Item2.Any(exists);}catch{found=null;}
    return new ExternalPresence(item.Item1,found,now,"Processus locaux : "+string.Join(", ",item.Item2)+" · aucun contrôle de service");
   }).ToArray();
 }
}

public interface IStartupEntry {string? Read();void Write(string command);void Delete();}
public sealed class StartupRegistration(IStartupEntry entry,string executable){
 public string Command=>"\""+Path.GetFullPath(executable)+"\"";
 public bool Enabled=>string.Equals(entry.Read(),Command,StringComparison.OrdinalIgnoreCase);
 public bool Registered=>entry.Read() is not null;
 public void Apply(bool enabled){
  var current=entry.Read();
  if(enabled){if(current!=Command)entry.Write(Command);}
  else if(current is not null)entry.Delete();
 }
}
