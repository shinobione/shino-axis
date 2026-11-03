using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AxisLab;
public static class LabHttp
{
    public static WebApplication Create(string[] args, LabEngine engine, Func<JsonObject>? sdk=null)
    {
        var builder=WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args=[], ContentRootPath=Path.GetFullPath(args[0]) });
        builder.Configuration.Sources.Clear();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k=>{k.Listen(IPAddress.Loopback,0);k.Limits.MaxRequestBodySize=262144;});
        var app=builder.Build();
        app.MapPost("/lab",async (HttpContext ctx)=>
        {
            try
            {
                string auth=ctx.Request.Headers.Authorization.ToString();
                string role=auth.StartsWith("Bearer TEST_",StringComparison.Ordinal)?auth[12..]:"INVALID";
                if(!new[]{"ADMIN","LINK","PERISCOPE","READER"}.Contains(role)) throw new LabError(403,"ROLE_REJECTED");
                if(ctx.Request.ContentLength>262144) throw new LabError(413,"BODY_LIMIT");
                using var reader=new StreamReader(ctx.Request.Body,Encoding.UTF8);
                // Also bounded for chunked requests, without reading the entire stream first.
                var buffer=new char[4096]; var text=new StringBuilder(); int count;
                while((count=await reader.ReadAsync(buffer))>0)
                { text.Append(buffer,0,count); if(text.Length>262144) throw new LabError(413,"BODY_LIMIT"); }
                var req=LabEngine.Parse(text.ToString());
                JsonObject response;
                if(req["op"]?.GetValue<string>()=="sdk" && sdk is not null) response=sdk();
                else response=engine.Dispatch(req,role);
                await ctx.Response.WriteAsJsonAsync(response);
                if(req["op"]?.GetValue<string>()=="shutdown") app.Lifetime.StopApplication();
            }
            catch(LabError ex) { ctx.Response.StatusCode=ex.Status; await ctx.Response.WriteAsJsonAsync(new{error=ex.Message}); }
            catch(Exception ex) when(ex is InvalidOperationException or FormatException or ArgumentException or System.Text.Json.JsonException or KeyNotFoundException)
            { Console.Error.WriteLine(ex); ctx.Response.StatusCode=400; await ctx.Response.WriteAsJsonAsync(new{error="INVALID_REQUEST"}); }
        });
        app.Lifetime.ApplicationStarted.Register(()=>
        {
            string url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            File.WriteAllText(Path.Combine(args[0],"ready.json"),new JsonObject{["url"]=url,["sampleKind"]="TEST"}.ToJsonString());
        });
        return app;
    }
}
