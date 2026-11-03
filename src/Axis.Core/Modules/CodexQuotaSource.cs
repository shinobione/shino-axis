using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace Axis.Core.Modules;
public sealed class CodexQuotaSource : ICodexQuotaSource {
 public static string? Locate(){
  string? custom=Environment.GetEnvironmentVariable("SHINO_CODEX_EXE");
  if(custom is not null&&File.Exists(custom)&&custom.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))return custom;
  var paths=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Where(p=>!string.IsNullOrWhiteSpace(p)).Select(p=>Path.Combine(p.Trim('"'),"codex.exe"));
  string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  return paths.Concat(new[]{Path.Combine(roaming,@"npm\node_modules\@openai\codex\node_modules\@openai\codex-win32-x64\vendor\x86_64-pc-windows-msvc\bin\codex.exe"),
   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".codex\bin\codex.exe")}).FirstOrDefault(File.Exists);
 }
 public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellation){
  var binary=Locate()??throw new FileNotFoundException("CLI Codex introuvable.");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(20));var token=timeout.Token;
  using var process=new Process{StartInfo=new(binary,"app-server --listen stdio://"){
   UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false)
  }};
  process.Start();
  // Drain stderr without retaining or logging any private output.
  var drain=Task.Run(async()=>{var buffer=new char[2048];try{while(await process.StandardError.ReadAsync(buffer,token)>0){}}catch(OperationCanceledException){}});
  try{
   async Task Send(object message){await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(),token);await process.StandardInput.FlushAsync(token);}
   async Task<JsonElement> Receive(int expected){
    for(int i=0;i<100;i++){
     var line=await process.StandardOutput.ReadLineAsync(token);
     if(line is null||line.Length>1_048_576)throw new IOException("Réponse Codex invalide.");
     using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
     if(root.TryGetProperty("id",out var id)&&id.ValueKind==JsonValueKind.Number&&id.TryGetInt32(out int value)&&value==expected){
      if(root.TryGetProperty("error",out _))throw new IOException("Requête refusée par Codex.");
      return root.GetProperty("result").Clone();
     }
    }throw new IOException("Réponse Codex absente.");
   }
   await Send(new{method="initialize",id=1,@params=new{clientInfo=new{name="shino_axis",title="SHINO AXIS",version="0.3.0"}}});await Receive(1);
   await Send(new{method="initialized",@params=new{}});
   await Send(new{method="account/rateLimits/read",id=2});
   var result=await Receive(2);return QuotaParser.Parse(result.GetRawText(),DateTimeOffset.UtcNow);
  }finally{
   if(!process.HasExited)process.Kill(true);
   await process.WaitForExitAsync();timeout.Cancel();
   try{await drain;}catch(IOException){}
  }
 }
}
