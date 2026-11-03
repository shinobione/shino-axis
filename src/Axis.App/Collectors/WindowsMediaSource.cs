using System;
using System.Linq;
using System.Collections.Generic;
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
 DateTimeOffset refreshed;readonly Dictionary<GlobalSystemMediaTransportControlsSession,string> identities=[];long nextIdentity;
 string Identity(GlobalSystemMediaTransportControlsSession item){
  if(!identities.TryGetValue(item,out var id))identities[item]=id=item.SourceAppUserModelId+":"+(++nextIdentity);
  return id;
 }
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
   if(manager is null)await StartAsync(timeout.Token);
   var sessions=manager!.GetSessions().ToArray();var candidates=new List<MediaCandidate>();
   foreach(var item in sessions)try{
    var state=item.GetPlaybackInfo().PlaybackStatus;
    if(state is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
     candidates.Add(new(Identity(item),state==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing));
   }catch(Exception){/* A closed session must not hide another live player. */}
   var preferred=manager.GetCurrentSession();
   string? previousId=session is not null&&identities.TryGetValue(session,out var previous)?previous:null;
   string? preferredId=preferred is not null&&identities.TryGetValue(preferred,out var preferredValue)?preferredValue:null;
   string? selected=MediaRules.Select(candidates,previousId,preferredId);
   SetSession(selected is null?null:identities.FirstOrDefault(pair=>pair.Value==selected).Key);
   foreach(var old in identities.Keys.Where(k=>!sessions.Contains(k)).ToArray())identities.Remove(old);
   if(session is null)return new("Aucune session");
   int current=Volatile.Read(ref revision);
   var target=session;
   if(properties is null||readRevision!=current||DateTimeOffset.UtcNow-refreshed>=TimeSpan.FromSeconds(5)){
    properties=null;artwork=null;
    var metadata=await target.TryGetMediaPropertiesAsync().AsTask(timeout.Token);
    if(metadata.Thumbnail is {} thumbnail){
     try {
      using var stream=await thumbnail.OpenReadAsync().AsTask(timeout.Token);
      if(stream.Size>0&&stream.Size<=4*1024*1024){
       using var reader=new DataReader(stream);
       uint size=(uint)stream.Size;if(await reader.LoadAsync(size).AsTask(timeout.Token)!=size)throw new InvalidOperationException("Incomplete artwork");
       var bytes=new byte[size];reader.ReadBytes(bytes);artwork=bytes;
      }
     }catch(Exception e)when(e is not OperationCanceledException){artwork=null;}
    }
    if(current!=Volatile.Read(ref revision)){properties=null;artwork=null;return new("Actualisation");}
    properties=metadata;readRevision=current;refreshed=DateTimeOffset.UtcNow;
   }
   var timeline=session.GetTimelineProperties();var playback=session.GetPlaybackInfo();
   double start=timeline.StartTime.TotalSeconds,end=timeline.EndTime.TotalSeconds;
   double? duration=end>start?end-start:null;
   bool? playing=playback.PlaybackStatus switch{GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing=>true,GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused=>false,_=>null};
   if(playing is null)return new("Aucune session");
   double? position=MediaRules.Position(timeline.Position.TotalSeconds-start,duration,playing,playback.PlaybackRate,timeline.LastUpdatedTime,DateTimeOffset.UtcNow);

   if(current!=Volatile.Read(ref revision)){properties=null;artwork=null;return new("Actualisation");}
   return new("Disponible",properties.Title,properties.Artist,properties.AlbumTitle,session.SourceAppUserModelId,playing,position,duration,artwork,Identity(target),timeline.LastUpdatedTime);
  }catch(OperationCanceledException)when(!cancellation.IsCancellationRequested){properties=null;artwork=null;return new("Indisponible");}
  catch(Exception)when(!cancellation.IsCancellationRequested){await StopAsync();return new("Indisponible");}
 }
 public Task StopAsync(){
  if(manager is not null){manager.CurrentSessionChanged-=Changed;manager.SessionsChanged-=Sessions;}
  SetSession(null);manager=null;identities.Clear();return Task.CompletedTask;
 }
}
