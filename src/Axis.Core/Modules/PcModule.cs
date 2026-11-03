using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace Axis.Core.Modules;
public record PcReading(double? Cpu,double? RamUsed,double? RamTotal,double? Gpu,double? GpuTemperature,string CpuName,string GpuName);
public sealed class PcModule(Func<Task<PcReading>> read) : IAxisModule {
 public string Id=>"pc";public string Name=>"AXIS // PC";public string Description=>"Mesures Windows natives • affichage local uniquement";
 public TimeSpan Interval=>TimeSpan.FromSeconds(2);
 public ValueTask StartAsync()=>ValueTask.CompletedTask; public ValueTask StopAsync()=>ValueTask.CompletedTask;
 public async ValueTask<ModuleData> PollAsync(DateTimeOffset now,Action<string,string> publish) {
  var r=await read();
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
 long? oldIdle,oldTotal;
 public static (double? Usage,double? Temperature) ParseGpu(string text) {
  var fields=text.Trim().Split(',');
  double? Parse(int i,double low,double high)=>i<fields.Length&&double.TryParse(fields[i].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)&&v>=low&&v<=high?v:null;
  return(Parse(0,0,100),Parse(1,-40,130));
 }
 [System.Runtime.Versioning.SupportedOSPlatform("windows")]
 public async Task<PcReading> ReadAsync() {
  double? cpu=null,used=null,total=null,gpu=null,temp=null;
  if(GetSystemTimes(out var idle,out var kernel,out var user)) {
   long current=kernel+user;
   if(oldTotal is {} previous&&oldIdle is {} previousIdle&&current>previous)cpu=Math.Clamp(100.0*(1.0-(double)(idle-previousIdle)/(current-previous)),0,100);
   oldIdle=idle;oldTotal=current;
  }
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
