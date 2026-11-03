using AxisLab;
using System.Text.Json.Nodes;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Testing;
using MacroDeck.Plugin.Protocol.Capabilities;
using MacroDeck.Plugin.Protocol.Handshake;

if(args.Length<3) throw new ArgumentException("state-directory relay-schema android-schema");
using var engine=new LabEngine(args[0],args[1],args[2]);
engine.RegisterExtension(new ClockModule());
// This is the official SDK's synthetic protocol host, NOT the Macro Deck desktop application.
await using var host=await MacroDeckTestHost.StartAsync();
var builder=MacroDeckPlugin.CreatePlugin();
builder.RegisterIntegration(_=>new LabIntegration(engine));
builder.Configuration["MacroDeck:Plugin:StateDirectory"]=Path.Combine(args[0],"sdk-state");
builder.Configuration["MacroDeck:Plugin:DataDirectory"]=Path.Combine(args[0],"sdk-data");
builder.WebApplicationBuilder.WebHost.UseUrls("http://127.0.0.1:0");
await using var plugin=await host.HostAsync(builder);
var session=await host.WaitForSessionAsync().WaitAsync(TimeSpan.FromSeconds(15));
await using var app=LabHttp.Create(args,engine,()=>
{
    var reply=session.InvokeAsync(CapabilityKinds.Variables,"test-snapshot",CapabilityOperations.Variables.Get)
        .GetAwaiter().GetResult();
    if(!reply.Succeeded) throw new InvalidOperationException("SDK variable invocation failed");
    return new JsonObject{["sampleKind"]="TEST",["boundary"]="OFFICIAL_SDK_TEST_HOST",["sdkReply"]=JsonNode.Parse(reply.Data!.Value.GetRawText())};
});
await app.RunAsync();
