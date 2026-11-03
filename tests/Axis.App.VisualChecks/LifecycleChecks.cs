using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Axis.App;
using Axis.Core;
using Axis.Core.Modules;
using Axis.Core.Presentation;
partial class Program {
 static object? Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
 static void Until(Func<bool> ready){var clock=Stopwatch.StartNew();while(!ready()){if(clock.Elapsed>TimeSpan.FromSeconds(12))throw new Exception("Lifecycle timeout");Pump(15);}}
 static void Tray(MainWindow window,string label){
  var icon=Field(window,"tray");var menu=icon.GetType().GetProperty("ContextMenuStrip")!.GetValue(icon)!;
  var items=(System.Collections.IEnumerable)menu.GetType().GetProperty("Items")!.GetValue(menu)!;
  var item=items.Cast<object>().Single(x=>(string)x.GetType().GetProperty("Text")!.GetValue(x)! ==label);item.GetType().GetMethod("PerformClick")!.Invoke(item,null);
 }
 static int InstanceProbe(string[] args){using var guard=new AxisInstance(args[Array.IndexOf(args,"--profile")+1]);return guard.IsOwner?23:0;}
 static int Lifecycle(){
  string profile=MainWindow.ProfileDirectory();
  if(!Environment.GetCommandLineArgs().Contains("--data-dir")||Directory.Exists(profile))throw new Exception("New isolated --data-dir required");
  Directory.CreateDirectory(profile);var settings=new AppSettings{CloseToTray=true,RestoreLastPage=true,LastPage="Modules",AnimationsEnabled=false};
  foreach(var id in settings.EnabledModules.Keys.ToArray())settings.EnabledModules[id]=id=="projects";
  new SettingsStore(profile).SaveAsync(settings).GetAwaiter().GetResult();
  var app=new FixtureApp();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  MainWindow? window=null;
  try{
   // Real cross-process exclusion and activation event, without any real profile.
   using(var owner=new AxisInstance(profile)){
    if(!owner.IsOwner)throw new Exception("First instance refused");int activated=0;owner.OnActivate(()=>Interlocked.Increment(ref activated));
    using var child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"--instance-probe","--profile",profile}})!;
    if(!child.WaitForExit(5000)){child.Kill(true);throw new Exception("Duplicate instance did not exit");}
    Until(()=>Volatile.Read(ref activated)==1);if(child.ExitCode!=0)throw new Exception("Second process acquired same profile");
   }
   using(var reopened=new AxisInstance(profile))if(!reopened.IsOwner)throw new Exception("Mutex not released");
   Console.WriteLine("PASS real cross-process profile mutex, activation and release");
   if(app.Windows.Count!=0)throw new Exception("Test startup implicitly created a product window");
   window=new MainWindow();window.Show();Until(()=>(bool)Field(window,"initialized"));Until(()=>((Task)Field(window,"initialization")).IsCompleted);
   var runtime=(ModuleRuntime)Field(window,"runtime");var projects=(ProjectsModule)Field(window,"projects");
   if((string)Field(window,"page")!="Modules"||!projects.ReceiverReady)throw new Exception("Persisted startup/receiver: page="+Field(window,"page")+"; ready="+projects.ReceiverReady+"; "+string.Join("; ",runtime.Modules.Select(m=>m.Id+":"+m.State+":"+m.Error)));
   var originalInitialization=Field(window,"initialization");
   var watch=Stopwatch.StartNew();long memoryBefore=Process.GetCurrentProcess().PrivateMemorySize64;
   for(int cycle=0;cycle<10;cycle++)foreach(var page in new[]{"Accueil","PC","NETWORK","MEDIA","CODEX","PROJECTS","Modules","Paramètres"}){Call(window,"Navigate",page);Pump(2);}
   Console.WriteLine($"MEASURE navigation: pages=80; elapsedMs={watch.ElapsedMilliseconds}; privateBeforeMiB={memoryBefore/1048576.0:F1}; privateAfterMiB={Process.GetCurrentProcess().PrivateMemorySize64/1048576.0:F1}; synthetic-disabled-sources=true");
   Call(window,"Navigate","Modules");var controls=(Dictionary<string,CheckBox>)Field(window,"controls");var box=controls["projects"];
   box.IsChecked=false;box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Until(()=>box.IsEnabled&&runtime.Modules.Single(m=>m.Id=="projects").State==ModuleState.Stopped);
   if(projects.ReceiverReady)throw new Exception("Receiver survived module disable");
   box.IsChecked=true;box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Until(()=>box.IsEnabled&&projects.ReceiverReady);
   Console.WriteLine("PASS UI module disable/enable releases and restarts only AXIS receiver");
   Call(window,"Navigate","Paramètres");var content=(StackPanel)window.FindName("PageContent");
   var animation=Descendants(content).OfType<CheckBox>().Single(b=>Equals(b.Content,"Animations des missions"));animation.IsChecked=true;animation.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));Until(()=>animation.IsEnabled);
   var loaded=Task.Run(()=>new SettingsStore(profile).LoadAsync()).GetAwaiter().GetResult();if(!loaded.Settings.AnimationsEnabled||!loaded.Settings.CloseToTray||!loaded.Settings.EnabledModules["projects"])throw new Exception("Preferences lost");
   Tray(window,"Masquer AXIS");if(window.IsVisible)throw new Exception("Tray hide");Tray(window,"Afficher AXIS");Pump(30);if(!window.IsVisible||!ReferenceEquals(originalInitialization,Field(window,"initialization")))throw new Exception("Restore restarted initialization");
   window.Close();Pump(30);if(window.IsVisible||window.IsShuttingDown)throw new Exception("Close-to-tray exited");
   Tray(window,"Accueil");if((string)Field(window,"page")!="Accueil"||!window.IsVisible)throw new Exception("Tray home");Tray(window,"Paramètres");
   Call(window,"PowerChanged",window,new Microsoft.Win32.PowerModeChangedEventArgs(Microsoft.Win32.PowerModes.Resume));Pump(30);
   bool closed=false;window.Closed+=(_,_)=>closed=true;watch.Restart();
   // Hold the storage gate to simulate slow disk, then quit immediately after a preference change.
   var saveGate=(SemaphoreSlim)Field(Field(window,"store"),"saveGate");saveGate.Wait();
   try{
    var currentContent=(StackPanel)window.FindName("PageContent");
    var pending=Descendants(currentContent).OfType<CheckBox>().Single(b=>Equals(b.Content,"Animations des missions"));pending.IsChecked=false;pending.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    Tray(window,"Quitter AXIS");Pump(100);if(closed)throw new Exception("Quit completed before pending preference write");
   }finally{saveGate.Release();}
   Until(()=>closed);
   if(Task.Run(()=>new SettingsStore(profile).LoadAsync()).GetAwaiter().GetResult().Settings.AnimationsEnabled)throw new Exception("Final preference lost");
   if(runtime.Modules.Any(m=>m.State!=ModuleState.Stopped)||projects.ReceiverReady)throw new Exception("Shutdown retained modules");
   int port=projects.Port;var listener=new TcpListener(IPAddress.Loopback,port);listener.Start();listener.Stop();
   Console.WriteLine($"PASS real tray menu hide/show/home/settings/quit; close-to-tray; simulated resume; receiver port released; shutdownMs={watch.ElapsedMilliseconds}");
   // Reopen persisted profile, and close immediately while its asynchronous startup is in progress.
   window=new MainWindow();closed=false;window.Closed+=(_,_)=>closed=true;window.Show();Call(window,"RequestExit");Until(()=>closed);
   if(((ModuleRuntime)Field(window,"runtime")).Modules.Any(m=>m.State!=ModuleState.Stopped))throw new Exception("Startup/close race restarted module");
   listener=new TcpListener(IPAddress.Loopback,port);listener.Start();listener.Stop();
   Console.WriteLine("PASS reopen and close during startup: no late module activation or listener");
   var settingsBefore=File.ReadAllText(Path.Combine(profile,"settings.json"));
   window=new MainWindow();closed=false;window.Closed+=(_,_)=>closed=true;Call(window,"RequestExit");Until(()=>closed);
   if(File.ReadAllText(Path.Combine(profile,"settings.json"))!=settingsBefore)throw new Exception("Close before Loaded overwrote existing preferences");
   Console.WriteLine("PASS close before Loaded preserves existing preferences byte-for-byte");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
  finally{if(window is not null&&!window.IsShuttingDown){Call(window,"RequestExit");Pump(200);}app.Shutdown();}
 }
}
