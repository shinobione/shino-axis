using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace AxisLab;

public sealed class LabError(int status, string code) : Exception(code)
{ public int Status { get; } = status; }
public interface ITestModule
{ string Id { get; } JsonObject Read(DateTimeOffset now); }

// Laboratory adapter policy shared by both candidates. Not the production HUB implementation.
public sealed class LabEngine : IDisposable
{
    private readonly object gate = new();
    private readonly FileStream writer;
    private readonly string stateFile;
    private readonly Dictionary<string, JsonSchema> schemas = new();
    private JsonObject state;
    private readonly Dictionary<string, string> modules = new() { ["LINK_SIM"]="RUNNING", ["PERISCOPE_SIM"]="RUNNING" };
    private readonly Queue<JsonObject> events = new();
    private ITestModule? extension;
    private DateTimeOffset labNow=new(2030,1,1,0,0,0,TimeSpan.Zero);
    private static readonly Dictionary<string, int> Ttls = new() { ["telemetry"]=6, ["media"]=10, ["codex"]=900 };

    public LabEngine(string directory, string relaySchema, string androidSchema)
    {
        Directory.CreateDirectory(directory);
        writer = new FileStream(Path.Combine(directory,"writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        stateFile = Path.Combine(directory,"state.json");
        state = File.Exists(stateFile) ? JsonNode.Parse(File.ReadAllText(stateFile))!.AsObject() : new JsonObject { ["version"]=1, ["domains"]=new JsonObject(), ["commands"]=new JsonObject() };
        if (state["version"]?.GetValue<int>() != 1) throw new InvalidDataException("Unknown lab state version; no reset");
        var recovered=state.DeepClone().AsObject(); bool recoveryChanged=false;
        foreach(var command in recovered["commands"]!.AsObject().Select(p=>p.Value!.AsObject()))
        {
            if(S(command["result"])=="PENDING") { command["result"]="UNKNOWN_NO_RETRY"; recoveryChanged=true; }
            else if(S(command["result"])=="COMPLETED") modules[S(command["target"]) ]="STOPPED";
        }
        if(recoveryChanged) Persist(recovered);
        foreach(var pair in new[] { ("relay", relaySchema), ("android", androidSchema) })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(pair.Item2));
            schemas[pair.Item1] = JsonSchema.Build(doc.RootElement.Clone(), new BuildOptions { SchemaRegistry = new() });
        }
    }
    public void RegisterExtension(ITestModule module)
    { lock(gate) { if(extension is not null) throw new InvalidOperationException("Only one known lab extension"); extension=module; } }
    public JsonObject Observe()
    { lock(gate) return new(){["modules"]=JsonSerializer.SerializeToNode(modules),["snapshot"]=Snapshot(labNow)}; }

    public static JsonObject Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text)>262144) throw new LabError(413,"BODY_LIMIT");
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth=32 });
            CheckDuplicate(doc.RootElement);
            return JsonNode.Parse(text)!.AsObject();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { throw new LabError(400,"INVALID_JSON"); }
    }
    private static void CheckDuplicate(JsonElement el)
    {
        if(el.ValueKind==JsonValueKind.Object)
        {
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in el.EnumerateObject()) { if(!keys.Add(p.Name)) throw new LabError(400,"DUPLICATE_KEY"); CheckDuplicate(p.Value); }
        }
        else if(el.ValueKind==JsonValueKind.Array) foreach(var child in el.EnumerateArray()) CheckDuplicate(child);
        else if(el.ValueKind==JsonValueKind.Number && (!el.TryGetDouble(out var n)||!double.IsFinite(n))) throw new LabError(400,"NONFINITE");
    }
    private static string S(JsonNode? n) => n?.GetValue<string>() ?? throw new LabError(400,"MISSING_STRING");
    private static DateTimeOffset Time(JsonNode? n)
    {
        if(!DateTimeOffset.TryParseExact(S(n),"yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)) throw new LabError(400,"INVALID_TIME");
        return result;
    }
    private static string Stamp(DateTimeOffset now)=>now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture);
    private static string Canonical(JsonNode? n)
    {
        if(n is JsonObject o) return "{"+string.Join(",",o.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>JsonSerializer.Serialize(p.Key)+":"+Canonical(p.Value)))+"}";
        if(n is JsonArray a) return "["+string.Join(",",a.Select(Canonical))+"]";
        return n?.ToJsonString()??"null";
    }
    private static string Digest(JsonNode n)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(n))));
    private static string Fresh(string domain, JsonObject data, DateTimeOffset now)
    {
        if(data["capturedAt"] is null) return "UNKNOWN";
        var captured=Time(data["capturedAt"]);
        if(captured>now) throw new LabError(400,"FUTURE_CAPTURE");
        if((now-captured).TotalSeconds>=Ttls[domain]) return "STALE";
        if(domain=="codex") foreach(var key in new[]{"fiveHour","weekly"})
            if(data[key] is JsonObject quota && quota["resetsAt"] is not null && Time(quota["resetsAt"])<=now) return "STALE";
        return "CURRENT";
    }
    private void Validate(JsonObject doc,string schema)
    {
        if(!schemas.TryGetValue(schema,out var validator)) throw new LabError(400,"UNKNOWN_SCHEMA");
        using var parsed=JsonDocument.Parse(doc.ToJsonString());
        if(!validator.Evaluate(parsed.RootElement).IsValid) throw new LabError(400,"SCHEMA_REJECTED");
        if(S(doc["sampleKind"])!="TEST") throw new LabError(400,"TEST_ONLY");
        var generated=Time(doc["generatedAt"]);
        foreach(var domain in Ttls.Keys)
        {
            if(doc[domain] is not JsonObject data) continue;
            if(data["maxAgeSeconds"]!.GetValue<int>()!=Ttls[domain]) throw new LabError(400,"TTL_MISMATCH");
            if(S(data["freshness"])!=Fresh(domain,data,generated)) throw new LabError(400,"FRESHNESS_CONTRADICTION");
            if(data["capturedAt"] is null && S(data["availability"])!="UNAVAILABLE") throw new LabError(400,"MISSING_CAPTURE");
            if(domain=="telemetry")
            {
                if(data["network"] is JsonObject network && S(network["freshness"])!=Fresh("telemetry",network,generated)) throw new LabError(400,"NETWORK_FRESHNESS");
                int count=new[]{"cpuPercent","gpuPercent","ramUsedGiB","gpuTemperatureC"}.Count(k=>data[k]!["available"]!.GetValue<bool>());
                var expected=count==4?"AVAILABLE":count==0?"UNAVAILABLE":"PARTIAL";
                if(S(data["availability"])!=expected) throw new LabError(400,"AVAILABILITY_CONTRADICTION");
            }
            if(domain=="codex" && data["activity"] is JsonObject activity)
            {
                // This lab has no independently verified activity source.
                if(S(activity["state"])!="UNVERIFIED" || S(activity["evidence"])!="NONE" || activity["observedAt"] is not null) throw new LabError(400,"UNVERIFIED_ACTIVITY_ONLY");
            }
            if(domain=="media")
            {
                if(data["positionMs"] is not null && data["durationMs"] is not null && data["positionMs"]!.GetValue<long>()>data["durationMs"]!.GetValue<long>()) throw new LabError(400,"TIMELINE_CONTRADICTION");
                if(data["artwork"]!["available"]!.GetValue<bool>()!=(data["artwork"]!["reference"] is not null)) throw new LabError(400,"ARTWORK_CONTRADICTION");
            }
        }
    }
    private void Persist(JsonObject next)
    {
        string text=next.ToJsonString();
        if(Encoding.UTF8.GetByteCount(text)>65536) throw new LabError(507,"STATE_CAPACITY");
        var temp=stateFile+".next";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        { var bytes=Encoding.UTF8.GetBytes(text); stream.Write(bytes); stream.Flush(true); }
        File.Move(temp,stateFile,true);
        state=next;
    }
    private void Emit(string name,string target)
    {
        if(events.Count==128) events.Dequeue();
        events.Enqueue(new JsonObject { ["version"]=1,["name"]=name,["target"]=target,["sampleKind"]="TEST",["id"]=Guid.NewGuid().ToString() });
    }
    public JsonObject Snapshot(DateTimeOffset now)
    {
        lock(gate)
        {
            var snapshot=new JsonObject{["schemaVersion"]=2,["sampleKind"]="TEST",["generatedAt"]=Stamp(now)};
            foreach(var pair in state["domains"]!.AsObject())
            {
                var data=pair.Value!["data"]!.DeepClone().AsObject();
                data["freshness"]=Fresh(pair.Key,data,now);
                if(data["network"] is JsonObject network) network["freshness"]=Fresh("telemetry",network,now);
                snapshot[pair.Key]=data;
            }
            return snapshot;
        }
    }
    public JsonObject Dispatch(JsonObject req,string role)
    {
        lock(gate)
        {
            string op=S(req["op"]);
            if(!new[]{"ADMIN","LINK","PERISCOPE","READER"}.Contains(role)) throw new LabError(403,"ROLE_REJECTED");
            if(!new[]{"snapshot","smalltv","receive","modules","events","result","sdk"}.Contains(op) && op!="publish" && role!="ADMIN") throw new LabError(403,"ADMIN_REQUIRED");
            if(req["now"] is not null) labNow=Time(req["now"]);
            switch(op)
            {
                case "validate": Validate(req["document"]!.AsObject(),S(req["schema"])); return new(){["valid"]=true};
                case "publish":
                {
                    var doc=req["document"]!.AsObject();
                    Validate(doc,S(req["schema"]));
                    if(Time(doc["generatedAt"])>Time(req["now"])) throw new LabError(400,"FUTURE_DOCUMENT");
                    var next=state.DeepClone().AsObject(); bool changed=false;
                    foreach(var domain in Ttls.Keys)
                    {
                        if(doc[domain] is not JsonObject data) continue;
                        string owner=domain=="codex"?"PERISCOPE":"LINK";
                        if(role!=owner||S(data["source"]!["component"])!=owner) throw new LabError(403,"DOMAIN_AUTHORITY");
                        if(modules[owner+"_SIM"]!="RUNNING") throw new LabError(409,"MODULE_STOPPED");
                        string digest=Digest(data);
                        var previous=next["domains"]![domain];
                        if(previous is not null)
                        {
                            var old=previous["data"]!["capturedAt"];
                            var current=data["capturedAt"];
                            if(current is null && old is not null) throw new LabError(409,"WATERMARK_REGRESSION");
                            int order=current is null ? 0 : old is null ? 1 : Time(current).CompareTo(Time(old));
                            if(order<0 || (order==0 && S(previous["digest"])!=digest)) throw new LabError(409,"REPLAY_CONFLICT");
                            if(order==0) continue;
                        }
                        next["domains"]![domain]=new JsonObject{["data"]=data.DeepClone(),["digest"]=digest}; changed=true;
                    }
                    if(changed) { Persist(next); Emit("state.changed",role+"_SIM"); }
                    return new(){["outcome"]=changed?"CHANGED":"IDEMPOTENT"};
                }
                case "snapshot": return new(){["snapshot"]=Snapshot(Time(req["now"]))};
                case "smalltv":
                {
                    if(S(req["domain"])!="telemetry") throw new LabError(400,"UNSUPPORTED_DOMAIN");
                    var t=Snapshot(Time(req["now"]))["telemetry"]?.AsObject()??throw new LabError(400,"NO_DATA");
                    if(S(t["freshness"])!="CURRENT" || S(t["availability"])!="AVAILABLE") throw new LabError(400,"PROJECTION_UNAVAILABLE");
                    var metrics=new JsonObject{["ok"]=true,["gpu_available"]=true,["cpu_usage"]=t["cpuPercent"]!["value"]!.DeepClone(),["gpu_usage"]=t["gpuPercent"]!["value"]!.DeepClone(),["memory_used_gb"]=t["ramUsedGiB"]!["value"]!.DeepClone(),["gpu_temp_c"]=t["gpuTemperatureC"]!["value"]!.DeepClone()};
                    // These fields are not supplied by the HUB subset. Reject rather than fabricate VRAM/power.
                    if(req["supplement"] is not JsonObject extra) throw new LabError(400,"SMALLTV_SUPPLEMENT_REQUIRED");
                    if(S(extra["sampleKind"])!="TEST" || Time(extra["capturedAt"])!=Time(t["capturedAt"])) throw new LabError(400,"SUPPLEMENT_PROVENANCE");
                    metrics["gpu_vram_mb"]=extra["gpu_vram_mb"]?.DeepClone(); metrics["gpu_power"]=extra["gpu_power"]?.DeepClone();
                    string body=metrics.ToJsonString(); Receive(body,0); return new(){["body"]=body};
                }
                case "receive": return Receive(S(req["body"]),req["elapsedMs"]!.GetValue<long>());
                case "command":
                {
                    string id=S(req["id"]); if(id.Length is <1 or >64) throw new LabError(400,"COMMAND_ID");
                    string target=S(req["target"]); if(!modules.ContainsKey(target)||S(req["action"])!="stop") throw new LabError(400,"COMMAND_REJECTED");
                    string digest=Digest(new JsonObject{["target"]=target,["action"]="stop"});
                    if(state["commands"]![id] is JsonObject prior)
                    { if(S(prior["digest"])!=digest) throw new LabError(409,"COMMAND_CONFLICT"); return prior["ack"]!.DeepClone().AsObject(); }
                    if(state["commands"]!.AsObject().Count>=128) throw new LabError(507,"COMMAND_CAPACITY");
                    var next=state.DeepClone().AsObject();
                    var ack=new JsonObject{["id"]=id,["ack"]="ACCEPTED",["result"]="PENDING"};
                    next["commands"]![id]=new JsonObject{["digest"]=digest,["target"]=target,["result"]="PENDING",["ack"]=ack.DeepClone()}; Persist(next);
                    return ack;
                }
                case "complete":
                {
                    string id=S(req["id"]); var prior=state["commands"]![id]??throw new LabError(404,"COMMAND_UNKNOWN");
                    if(S(prior["result"])=="UNKNOWN_NO_RETRY") throw new LabError(409,"UNKNOWN_NO_RETRY");
                    if(S(prior["result"])=="PENDING")
                    { var next=state.DeepClone().AsObject(); next["commands"]![id]!["result"]="COMPLETED"; Persist(next); string target=S(prior["target"]); modules[target]="STOPPED"; Emit("module.stopped",target); }
                    return new(){["result"]="COMPLETED"};
                }
                case "result": return new(){["result"]=state["commands"]![S(req["id"])]?["result"]?.DeepClone()??"UNKNOWN"};
                case "events": return new(){["events"]=new JsonArray(events.Select(e=>(JsonNode)e.DeepClone()).ToArray())};
                case "extension":
                    if(extension is null) throw new LabError(400,"NO_EXTENSION");
                    modules.TryAdd(extension.Id,"RUNNING"); Emit("module.added",extension.Id);
                    return new(){["added"]=extension.Id,["state"]=extension.Read(new DateTimeOffset(2030,1,1,0,0,0,TimeSpan.Zero))};
                case "modules": return new(){["modules"]=JsonSerializer.SerializeToNode(modules)};
                case "shutdown": return new(){["stopping"]=true};
                default: throw new LabError(400,"UNKNOWN_OPERATION");
            }
        }
    }
    private static JsonObject Receive(string text,long elapsed)
    {
        if(text.Length is <16 or >384 || elapsed<0) throw new LabError(400,"SMALLTV_BOUND");
        var body=Parse(text);
        var bounds=new Dictionary<string,(double Min,double Max)>{["cpu_usage"]=(0,100),["gpu_usage"]=(0,100),["memory_used_gb"]=(0,256),["gpu_temp_c"]=(-40,130),["gpu_vram_mb"]=(0,65536),["gpu_power"]=(0,1200)};
        if(body.Count!=8 || body["ok"]?.GetValue<bool>()!=true || body["gpu_available"] is null) throw new LabError(400,"SMALLTV_SHAPE");
        _=body["gpu_available"]!.GetValue<bool>();
        foreach(var (key,range) in bounds)
        {
            if(body[key] is not JsonValue v || !v.TryGetValue<double>(out var n) || !double.IsFinite(n)||n<range.Min||n>range.Max) throw new LabError(400,"SMALLTV_METRIC");
        }
        return new(){["stale"]=elapsed>6000};
    }
    public void Dispose()=>writer.Dispose();
}
