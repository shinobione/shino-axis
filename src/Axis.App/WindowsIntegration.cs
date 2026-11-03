using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Axis.Core.Presentation;
namespace Axis.App;

// Owns only this named HKCU value. Read-only until the user clicks the explicit option.
sealed class WindowsStartupEntry : IStartupEntry {
 const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string Name="SHINO.AXIS";
 public string? Read(){using var key=Registry.CurrentUser.OpenSubKey(Key);return key?.GetValue(Name) as string;}
 public void Write(string command){using var key=Registry.CurrentUser.CreateSubKey(Key);key.SetValue(Name,command,RegistryValueKind.String);}
 public void Delete(){using var key=Registry.CurrentUser.OpenSubKey(Key,true);key?.DeleteValue(Name,false);}
}

public sealed class AxisInstance : IDisposable {
 readonly Mutex mutex;readonly EventWaitHandle activation;readonly bool owner;RegisteredWaitHandle? wait;
 public bool IsOwner=>owner;
 public AxisInstance(string directory){
  string identity=Environment.UserName+"|"+Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
  string key="Local\\SHINO.AXIS."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
  mutex=new(false,key);try{owner=mutex.WaitOne(0);}catch(AbandonedMutexException){owner=true;}
  activation=new(false,EventResetMode.AutoReset,key+".show");if(!owner)activation.Set();
 }
 public void OnActivate(Action show)=>wait=ThreadPool.RegisterWaitForSingleObject(activation,(_,_)=>show(),null,Timeout.Infinite,false);
 public void Dispose(){wait?.Unregister(null);activation.Dispose();if(owner)mutex.ReleaseMutex();mutex.Dispose();}
}
