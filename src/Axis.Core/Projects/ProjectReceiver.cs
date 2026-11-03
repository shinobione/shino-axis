using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
namespace Axis.Core.Projects;
public sealed class ProjectReceiver(ProjectStore store,int port,string extensionId="",string token=""){
 WebApplication? app;public DateTimeOffset? LastDeltaReceivedAt {get;private set;}
 public async Task StartAsync(){
  if(app is not null)return;
  var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions{Args=[]});
  builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Loopback,port);k.Limits.MaxRequestBodySize=1_048_576;k.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);});
  var server=builder.Build();
  server.Run(async context=>{
   var req=context.Request;var res=context.Response;res.Headers.CacheControl="no-store";res.Headers.XContentTypeOptions="nosniff";
   string origin=req.Headers.Origin.ToString(),host=req.Host.Value??"",site=req.Headers["Sec-Fetch-Site"].ToString();
   bool extension=extensionId!=""&&origin=="chrome-extension://"+extensionId;
   bool allowed=IPAddress.IsLoopback(context.Connection.RemoteIpAddress??IPAddress.None)&&(host==$"127.0.0.1:{port}"||host==$"localhost:{port}")&&(origin==""||origin=="http://"+host||extension)&&(site==""||site is "none" or "same-origin"||extension);
   if(!allowed){res.StatusCode=403;return;}
   if(token!=""&&req.Method!="OPTIONS"){
    var expected=Encoding.UTF8.GetBytes("Bearer "+token);var supplied=Encoding.UTF8.GetBytes(req.Headers.Authorization.ToString());
    if(!CryptographicOperations.FixedTimeEquals(expected,supplied)){res.StatusCode=401;return;}
   }
   if(extension){res.Headers.AccessControlAllowOrigin=origin;res.Headers.AccessControlAllowHeaders="Content-Type, Authorization";res.Headers.AccessControlAllowMethods="POST, OPTIONS";}
   if(req.Method=="OPTIONS"){res.StatusCode=204;return;}
   if(req.Method=="GET"&&req.Path=="/api/health"){await res.WriteAsJsonAsync(new{ok=true,receiver="AXIS",port},context.RequestAborted);return;}
   if(req.Method!="POST"||req.Path.Value is not ("/api/ingest/chatgpt-delta" or "/api/chatgpt/catchup-plan" or "/api/chatgpt/catchup-report")){res.StatusCode=404;return;}
   if(!string.Equals(req.ContentType?.Split(';',2)[0].Trim(),"application/json",StringComparison.OrdinalIgnoreCase)){res.StatusCode=415;return;}
   if(req.Headers["Sec-Fetch-Mode"]=="navigate"||req.Headers["Sec-Fetch-Dest"].ToString() is not ("" or "empty")){res.StatusCode=403;return;}
   if(!J.B(store.Snapshot["axisProjects"],"acceptChat")){res.StatusCode=409;await res.WriteAsJsonAsync(new{error="Collecte privée désactivée dans AXIS."});return;}
   try{
    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(TimeSpan.FromSeconds(10));
    var payload=await JsonNode.ParseAsync(req.Body,cancellationToken:timeout.Token) as JsonObject??throw new InvalidDataException();
    JsonObject result;
    if(req.Path=="/api/chatgpt/catchup-plan")result=ChatCatchup.Plan(store.Snapshot,payload);
    else if(req.Path=="/api/chatgpt/catchup-report")result=await store.MutateAsync(s=>ChatCatchup.Report(s,payload));
    else result=await store.MutateAsync(s=>{var response=ChatDelta.Ingest(s,payload);string key=J.S(payload,"conversationKey");bool cleared=(s["settings"]?["chatgptTimeoutQuarantine"] as JsonObject)?.Remove(key)??false;response["timeoutQuarantineCleared"]=cleared;return response;});if(req.Path=="/api/ingest/chatgpt-delta")LastDeltaReceivedAt=DateTimeOffset.UtcNow;await res.WriteAsJsonAsync(result,timeout.Token);
   }catch(Exception e)when(e is InvalidDataException or System.Text.Json.JsonException or IOException or OperationCanceledException or InvalidOperationException or ArgumentException){
    res.StatusCode=400;await res.WriteAsJsonAsync(new{error="Delta rejeté : contrat invalide ou stockage indisponible."});
   }
  });
  try{await server.StartAsync();app=server;}catch{await server.DisposeAsync();throw;}
 }
 public async Task StopAsync(){var server=app;app=null;if(server is not null){using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));try{await server.StopAsync(deadline.Token);}finally{await server.DisposeAsync();}}}
}
