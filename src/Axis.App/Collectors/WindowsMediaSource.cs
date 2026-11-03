using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Axis.Core.Modules;
using Windows.Media.Control;
using Windows.Storage.Streams;
namespace Axis.App.Collectors;
public sealed class WindowsMediaSource : IMediaSource {
 GlobalSystemMediaTransportControlsSessionManager? manager;
 GlobalSystemMediaTransportControlsSession? session;
 GlobalSystemMediaTransportControlsSessionMediaProperties? properties;
 byte[]? artwork;int revision=1,readRevision;
 void Changed(GlobalSystemMediaTransportControlsSessionManager s,CurrentSessionChangedEventArgs e)=>Interlocked.Increment(ref revision);
 void Sessions(GlobalSystemMediaTransportControlsSessionManager s,SessionsChangedEventArgs e)=>Interlocked.Increment(ref revision);
 void Metadata(GlobalSystemMediaTransportControlsSession s,MediaPropertiesChangedEventArgs e)=>Interlocked.Increment(ref revision);
 public async Task StartAsync(CancellationToken cancellation){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(2500);
  manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(timeout.Token);
  manager.CurrentSessionChanged+=Changed;manager.SessionsChanged+=Sessions;
 }
 void SetSession(GlobalSystemMediaTransportControlsSession? next){
  if(ReferenceEquals(next,session))return;
  if(session is not null)session.MediaPropertiesChanged-=Metadata;
  session=next;properties=null;artwork=null;Interlocked.Increment(ref revision);
  if(session is not null)session.MediaPropertiesChanged+=Metadata;
 }
 public async Task<MediaReading> ReadAsync(CancellationToken cancellation){
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(2500);
  try{
   SetSession(manager?.GetCurrentSession()??manager?.GetSessions().FirstOrDefault());
   if(session is null)return new("Aucune session");
   int current=Volatile.Read(ref revision);
   if(properties is null||readRevision!=current){
    properties=null;artwork=null;
    var metadata=await session.TryGetMediaPropertiesAsync().AsTask(timeout.Token);
    if(metadata.Thumbnail is {} thumbnail){
     try {
      using var stream=await thumbnail.OpenReadAsync().AsTask(timeout.Token);
      if(stream.Size>0&&stream.Size<=4*1024*1024){
       using var reader=new DataReader(stream);
       uint size=(uint)stream.Size;await reader.LoadAsync(size).AsTask(timeout.Token);
       var bytes=new byte[size];reader.ReadBytes(bytes);artwork=bytes;
      }
     }catch(Exception e)when(e is not OperationCanceledException){artwork=null;}
    }
    properties=metadata;readRevision=current;
   }
   var timeline=session.GetTimelineProperties();var playback=session.GetPlaybackInfo();
   double start=timeline.StartTime.TotalSeconds,end=timeline.EndTime.TotalSeconds;
   double? duration=end>start?end-start:null;
   double? position=duration is not null?Math.Clamp(timeline.Position.TotalSeconds-start,0,duration.Value):null;
   bool? playing=playback.PlaybackStatus switch{GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing=>true,GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused=>false,_=>null};
   // Windows supplies the timeline's own observation time; extrapolate only when playing.
   if(playing==true&&position is {} p&&duration is {} d&&timeline.LastUpdatedTime<=DateTimeOffset.UtcNow)
    position=Math.Clamp(p+Math.Max(0,(DateTimeOffset.UtcNow-timeline.LastUpdatedTime).TotalSeconds)*playback.PlaybackRate.GetValueOrDefault(1),0,d);
   return new("Disponible",properties.Title,properties.Artist,properties.AlbumTitle,session.SourceAppUserModelId,playing,position,duration,artwork);
  }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){properties=null;artwork=null;return new("Indisponible");}
  catch(Exception)when(!cancellation.IsCancellationRequested){properties=null;artwork=null;return new("Indisponible");}
 }
 public Task StopAsync(){
  if(manager is not null){manager.CurrentSessionChanged-=Changed;manager.SessionsChanged-=Sessions;}
  SetSession(null);manager=null;return Task.CompletedTask;
 }
}
