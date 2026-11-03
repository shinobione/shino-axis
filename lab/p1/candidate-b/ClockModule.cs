using AxisLab;
using System.Text.Json.Nodes;
public sealed class ClockModule : ITestModule
{
    public string Id=>"CLOCK_SIM";
    public JsonObject Read(DateTimeOffset now)=>new(){["sampleKind"]="TEST",["utc"]=now.ToString("O")};
}
