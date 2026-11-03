using System.Globalization;
namespace Axis.Core.Modules;
public record MediaReading(string Status,string? Title=null,string? Artist=null,string? Album=null,string? App=null,
 bool? Playing=null,double? Position=null,double? Duration=null,byte[]? Artwork=null,string? SessionId=null,DateTimeOffset? TimelineUpdatedAt=null);
public interface IMediaSource {
 Task StartAsync(CancellationToken cancellation);
 Task<MediaReading> ReadAsync(CancellationToken cancellation);
 Task StopAsync();
}
public sealed class MediaModule(IMediaSource source) : IAxisModule {
 public string Id=>"media";public string Name=>"AXIS // MEDIA";public string Description=>"Session multimédia Windows • lecture seule";
 public TimeSpan Interval=>TimeSpan.FromSeconds(1);
 CancellationTokenSource? lifetime;public byte[]? Artwork {get;private set;}
 public MediaSnapshot? Snapshot {get;private set;}
 string? lastTrack;
 public async ValueTask StartAsync(){lifetime=new();try{await source.StartAsync(lifetime.Token);}catch(Exception e)when(e is not OperationCanceledException and not OutOfMemoryException){}}
 public async ValueTask StopAsync(){lifetime?.Cancel();await source.StopAsync();lifetime?.Dispose();lifetime=null;Artwork=null;lastTrack=null;Snapshot=null;}
 public async ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish){
  MediaReading r;
  try{r=await source.ReadAsync(lifetime?.Token??CancellationToken.None);}
  catch(Exception e)when(e is not OperationCanceledException and not OutOfMemoryException){r=new("Indisponible");}
  bool available=r.Status=="Disponible";
  static double? Timeline(double? v)=>v is >=0 and <=604800&&double.IsFinite(v.Value)?v:null;
  if(!available)r=new(r.Status);
  else r=r with{Position=Timeline(r.Position),Duration=r.Duration is >0?Timeline(r.Duration):null,Artwork=r.Artwork is {Length:>4*1024*1024}?null:r.Artwork};
  if(r.Position is {} pos&&r.Duration is {} duration)r=r with{Position=Math.Min(pos,duration)};
  Snapshot=new(now,r);
  Artwork=available?r.Artwork:null;
  string? track=available?r.App+"|"+r.Title+"|"+r.Artist:null;
  if(track!=lastTrack){publish("media.changed",available?"Session ou morceau modifié":"Session multimédia terminée");lastTrack=track;}
  static string Field(string? s)=>string.IsNullOrWhiteSpace(s)?"Indisponible":s;
  static string Seconds(double? s)=>s is {} v&&double.IsFinite(v)&&v>=0?v.ToString(CultureInfo.InvariantCulture):"";
  return new(now,false,new Dictionary<string,string>{
   ["sessionId"]=available?r.SessionId??r.App??"":"",["status"]=r.Status,["title"]=available?Field(r.Title):r.Status,["artist"]=available?Field(r.Artist):"Indisponible",
   ["album"]=available?Field(r.Album):"Indisponible",["app"]=available?Field(r.App):"Indisponible",
   ["playback"]=available?r.Playing switch{true=>"Lecture",false=>"Pause",_=>"Indisponible"}:"Indisponible",
   ["position"]=available?Seconds(r.Position):"",["duration"]=available?Seconds(r.Duration):""
  });
 }
}
