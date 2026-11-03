#nullable disable
using System.Text.Json;
namespace Axis.Core.Missions;
// Compatibility adapter for the narrow metadata parser; no prompt or command payload retained.
internal sealed class MetadataJson {
 public int MaxJsonLength {get;set;}=1048576;
 static readonly HashSet<string> Allowed=new(StringComparer.Ordinal){
  "type","timestamp","payload","id","thread_source","originator","parent_thread_id","cwd","turn_id",
  "started_at","duration_ms","model","effort","reasoning_effort","model_reasoning_effort","collaboration_mode","settings",
  "subagent","sub_agent","spawn","error"
 };
 public object DeserializeObject(string text){
  if(text.Length>MaxJsonLength)throw new ArgumentException("Metadata too large");
  try {using var doc=JsonDocument.Parse(text);return Convert(doc.RootElement);}
  catch(JsonException e){throw new ArgumentException("Invalid metadata",e);}
 }
 static object Convert(JsonElement element){
  switch(element.ValueKind){
   case JsonValueKind.Object:
    var result=new Dictionary<string,object>();
    foreach(var p in element.EnumerateObject())if(Allowed.Contains(p.Name)){
     // Retain only the presence of a terminal error, never its content.
     result[p.Name]=p.Name=="error"?(p.Value.ValueKind==JsonValueKind.Null?null:true):Convert(p.Value);
    }return result;
   case JsonValueKind.String:return element.GetString();
   case JsonValueKind.Number:if(element.TryGetInt64(out long n))return n;return element.GetDouble();
   case JsonValueKind.True:return true;
   case JsonValueKind.False:return false;
   default:return null;
  }
 }
 public string Serialize(object value)=>JsonSerializer.Serialize(value);
}
internal static class Json {
 internal static Dictionary<string,object> Object(object value)=>value as Dictionary<string,object>;
 internal static object Get(Dictionary<string,object> value,string name)=>value!=null&&value.TryGetValue(name,out var result)?result:null;
 internal static string String(object value)=>value as string;
 internal static long? Long(object value)=>value is long n?n:null;
}
