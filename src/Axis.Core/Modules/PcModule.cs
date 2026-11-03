using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace Axis.Core.Modules;
public record PcReading(double? Cpu,double? RamUsed,double? RamTotal,double? Gpu,double? GpuTemperature,string CpuName,string GpuName);
public sealed class PcModule(Func<Task<PcReading>> read,Action? reset=null) : IAxisModule {
 public string Id=>"pc";public string Name=>"AXIS // PC";public string Description=>"Mesures Windows natives • affichage local uniquement";
 public TimeSpan Interval=>TimeSpan.FromSeconds(2);
 public PcSnapshot? Snapshot {get;private set;}
 public ValueTask StartAsync(){reset?.Invoke();Snapshot=null;return ValueTask.CompletedTask;} public ValueTask StopAsync(){reset?.Invoke();Snapshot=null;return ValueTask.CompletedTask;}
 public async ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish) {
  PcReading r;
  try{r=await read();}catch(Exception e)when(e is not OutOfMemoryException){r=new(null,null,null,null,null,"Indisponible","Indisponible");}
  static double? Valid(double? n,double low,double high)=>n is {} v&&double.IsFinite(v)&&v>=low&&v<=high?v:null;
  r=r with{Cpu=Valid(r.Cpu,0,100),Gpu=Valid(r.Gpu,0,100),GpuTemperature=Valid(r.GpuTemperature,-40,130),RamTotal=Valid(r.RamTotal,0,65536),RamUsed=Valid(r.RamUsed,0,r.RamTotal??65536)};
  Snapshot=new(now,r);
  string F(double? n,string unit)=>n is {} v&&double.IsFinite(v)?v.ToString("0.0",CultureInfo.GetCultureInfo("fr-FR"))+unit:"Indisponible";
  return new(now,false,new Dictionary<string,string>{
   ["cpu"]=F(r.Cpu," %"),["ram"]=F(r.RamUsed," Go"),["ramTotal"]=F(r.RamTotal," Go"),
   ["gpu"]=F(r.Gpu," %"),["temperature"]=F(r.GpuTemperature," °C"),["cpuName"]=r.CpuName,["gpuName"]=r.GpuName,
   ["os"]=OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)?"Windows 11 • build "+Environment.OSVersion.Version.Build:RuntimeInformation.OSDescription,["architecture"]=RuntimeInformation.OSArchitecture.ToString()
  });
 }
}
public sealed class WindowsPcSource {
 [StructLayout(LayoutKind.Sequential)] struct Memory {public uint Length,Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
 [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory memory);
 [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
 readonly CpuCounter counter=new();
 int resetRequested;
 public void Reset()=>Interlocked.Exchange(ref resetRequested,1);
 public static (double? Usage,double? Temperature) ParseGpu(string text) {
  var fields=text.Trim().Split(',');
  double? Parse(int i,double low,double high)=>i<fields.Length&&double.TryParse(fields[i].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)&&v>=low&&v<=high?v:null;
  return(Parse(0,0,100),Parse(1,-40,130));
 }
 [System.Runtime.Versioning.SupportedOSPlatform("windows")]
 public async Task<PcReading> ReadAsync() {
  if(Interlocked.Exchange(ref resetRequested,0)!=0)counter.Reset();
  double? cpu=null,used=null,total=null,gpu=null,temp=null;
  if(GetSystemTimes(out var idle,out var kernel,out var user)) {
   cpu=counter.Sample(idle,kernel+user,DateTimeOffset.UtcNow);
  }
  else counter.Reset();
  var memory=new Memory{Length=(uint)Marshal.SizeOf<Memory>()};
  if(GlobalMemoryStatusEx(ref memory)){total=memory.Total/1073741824.0;used=(memory.Total-memory.Available)/1073741824.0;}
  string cpuName="Indisponible",gpuName="Indisponible";
  using(var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
   cpuName=key?.GetValue("ProcessorNameString")?.ToString()?.Trim()??cpuName;
  string? smi=new[]{Path.Combine(Environment.SystemDirectory,"nvidia-smi.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"NVIDIA Corporation","NVSMI","nvidia-smi.exe")}.FirstOrDefault(File.Exists);
  if(smi is not null) {
   try {
    using var process=new Process{StartInfo=new(smi,"--id=0 --query-gpu=utilization.gpu,temperature.gpu,name --format=csv,noheader,nounits"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};
    process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
    using var timeout=new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
    try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);await process.WaitForExitAsync();throw;}
    string row=await output;await error;
    if(process.ExitCode==0){(gpu,temp)=ParseGpu(row);var fields=row.Trim().Split(',');if(fields.Length>=3)gpuName=string.Join(",",fields.Skip(2)).Trim();}
   }catch(Exception e)when(e is System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException){}
  }
  return new(cpu,used,total,gpu,temp,cpuName,gpuName);
 }
}
