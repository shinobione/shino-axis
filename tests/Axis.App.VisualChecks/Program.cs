using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Axis.Core;
using Axis.Core.Modules;
using Axis.Core.Missions;
using Axis.Core.Projects;
using Axis.App.Controls;
partial class Program {
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
 static void Set(object o,string property,object? value)=>o.GetType().GetProperty(property,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(o,value);
 [STAThread] static int Main(string[] args){
  Console.SetOut(new StreamWriter(Console.OpenStandardOutput()){AutoFlush=true});
  if(args.Contains("--instance-probe"))return InstanceProbe(args);
  if(args.Contains("--lifecycle"))return Lifecycle();
  if(args.Contains("--windows-probe"))return WindowsProbe().GetAwaiter().GetResult();
  var app=new FixtureApp();app.InitializeComponent();var window=new Axis.App.MainWindow();
  try{
   var runtime=(ModuleRuntime)Field(window,"runtime");var codex=(CodexModule)Field(window,"codex");var media=(MediaModule)Field(window,"media");var projects=(ProjectsModule)Field(window,"projects");
   CheckMotion();
   if(app.Windows.Count!=1||window.IsLoaded)throw new Exception("Visual harness unexpectedly started a product window");
   var now=DateTimeOffset.UtcNow;
   var pc=new Dictionary<string,string>{{"cpu","27,0 %"},{"gpu","42,0 %"},{"ram","11,3 Go"},{"temperature","62,0 °C"},{"ramTotal","32,0 Go"},{"cpuName","SIMULATION • Processeur de validation"},{"gpuName","SIMULATION • GPU de validation"},{"os","Windows 11 • build de test"}};
   var network=new Dictionary<string,string>{{"download","12,34 Mo/s"},{"upload","1,25 Mo/s"},{"name","Ethernet de test"},{"connection","Connecté"},{"ipv4","192.0.2.10"},{"type","Ethernet"},{"downloaded","234,56 Mo"},{"uploaded","12,34 Mo"}};
   var track=new Dictionary<string,string>{{"status","Disponible"},{"title","SIMULATION • Nuit en couleurs"},{"artist","Artiste de validation"},{"album","Album de validation"},{"app","Spotify"},{"playback","Lecture"},{"position","63"},{"duration","214"}};
   foreach(var m in runtime.Modules){Set(m,"State",ModuleState.Active);Set(m,"Data",new ModuleData(now,false,m.Id switch{"pc"=>pc,"network"=>network,"media"=>track,"codex"=>new Dictionary<string,string>{{"status","SIMULATION"},{"lastSuccess","Simulation"},{"summary","Simulation"},{"activity","EN COURS • fixture locale"}},_=>new Dictionary<string,string>{{"summary","Projets de validation"},{"sync","Simulation"}}}));}
   var history=Enumerable.Range(0,60).Select(i=>new QuotaSnapshot(now.AddMinutes(-5*(59-i)),[new("codex",300,15+i*.2,now.AddHours(4)),new("codex",10080,30+i*.1,now.AddDays(4))],1)).ToArray();
   foreach(var sample in history)Task.Run(()=>codex.History.AppendAsync(sample)).GetAwaiter().GetResult();
   Set(codex,"Snapshot",history[^1]);typeof(CodexModule).GetField("verified",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(codex,true);
   Set(codex.Missions,"Current",new MissionSnapshot(now,"EN COURS • fixture synthétique",[new("fixture-active","thread-a","Projet de validation","EN COURS","03:42","modèle de test","medium",false,now.AddMinutes(-4),null,now),new("fixture-complete","thread-b","Autre projet","TERMINÉ","12:08","modèle de test","high",false,now.AddMinutes(-20),now.AddMinutes(-8)),new("fixture-active-2","thread-c","Deuxième mission simultanée","EN COURS","00:20","modèle observé","high",false,now.AddSeconds(-20),null,now),new("fixture-failed","thread-d","Échec confirmé","ÉCHEC","00:30","modèle observé","medium",false,now.AddMinutes(-2),now.AddSeconds(-90),now.AddSeconds(-90)),new("fixture-interrupted","thread-e","Interruption confirmée","INTERROMPU","00:15","modèle observé","low",false,now.AddMinutes(-3),now.AddSeconds(-165),now.AddSeconds(-165)),new("fixture-old","thread-f","Observation ancienne","EN COURS","00:10","modèle observé","medium",false,now.AddMinutes(-5),null,now.AddMinutes(-2))],2,false));
   Task.Run(()=>projects.Store.MutateAsync(s=>{J.A(s,"projects").Add(new System.Text.Json.Nodes.JsonObject{["id"]="fixture",["name"]="SIMULATION • Projet prioritaire",["control"]=new System.Text.Json.Nodes.JsonObject{["pinned"]=true}});J.A(s,"derived").Add(new System.Text.Json.Nodes.JsonObject{["projectId"]="fixture",["status"]="NEEDS TEST",["nextAction"]="Vérifier le rendu des cartes et les filtres."});return true;})).GetAwaiter().GetResult();
   var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()){
    dc.DrawRectangle(new LinearGradientBrush(Colors.MediumPurple,Colors.Coral,45),null,new Rect(0,0,160,160));
    dc.DrawEllipse(Brushes.MidnightBlue,null,new Point(80,80),48,48);
   }
   var art=new RenderTargetBitmap(160,160,96,96,PixelFormats.Pbgra32);art.Render(drawing);var artEncoder=new PngBitmapEncoder();artEncoder.Frames.Add(BitmapFrame.Create(art));using(var stream=new MemoryStream()){artEncoder.Save(stream);Set(media,"Artwork",stream.ToArray());}
   var root=(FrameworkElement)window.Content;window.Content=null;if(root is Panel panel)panel.Background=window.Background;
   string output=args.FirstOrDefault(a=>a.StartsWith("--output="))?.Substring(9)??Path.Combine(Environment.CurrentDirectory,"artifacts","v06-layout-checks");Directory.CreateDirectory(output);
   int count=0;
   foreach(var size in new[]{(800,680),(1280,900),(1920,1080),(3440,1440)}){
    foreach(string page in args.Contains("--codex-only")?new[]{"CODEX"}:new[]{"Accueil","PC","NETWORK","MEDIA","CODEX","PROJECTS","Modules","Paramètres"}){
     typeof(Axis.App.MainWindow).GetMethod("Navigate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[page]);
     if(page=="CODEX")window.FindName("PageTitle").GetType().GetProperty("Text")!.SetValue(window.FindName("PageTitle"),"CODEX • SIMULATION DE VALIDATION");
     if(page=="PROJECTS"&&Field(window,"projectList") is ListBox list&&list.Items.Count>0){list.SelectedIndex=0;typeof(Axis.App.MainWindow).GetMethod("ShowProject",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,["fixture"]);}root.Width=size.Item1;root.Height=size.Item2;root.Measure(new(size.Item1,size.Item2));root.Arrange(new(0,0,size.Item1,size.Item2));root.UpdateLayout();
     Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Render);
     root.UpdateLayout();
     var bitmap=new RenderTargetBitmap(size.Item1,size.Item2,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
     using var file=File.Create(Path.Combine(output,$"{page}-{size.Item1}x{size.Item2}-SIMULATION.png"));encoder.Save(file);
     if(root.DesiredSize.Width>size.Item1+.1)throw new Exception("Horizontal overflow: "+page);
     foreach(var gauge in Descendants(root).OfType<QuotaDial>())if(gauge.ActualWidth>130||gauge.ActualWidth<90)throw new Exception("Gauge layout outside bounds");
     if(page=="CODEX")CheckStableCards(window,codex);
     if(page=="Accueil"){CheckHome(window,codex);CheckMedia(window,runtime,media);}
     Console.WriteLine($"PASS WPF layout {page} {size.Item1}x{size.Item2} (synthetic data)");count++;
    }
   }
   ScenarioRenders(window,root,runtime,codex,output);
   Console.WriteLine($"{count} size/page layout renders + 8 scenario renders created; no collectors started.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
  finally{typeof(Axis.App.MainWindow).GetField("closing",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,true);((IDisposable)Field(window,"tray")).Dispose();((IDisposable)Field(window,"axisIcon")).Dispose();window.Close();app.Shutdown();}
 }
 static async System.Threading.Tasks.Task<int> WindowsProbe(){
  var pc=new WindowsPcSource();var media=new Axis.App.Collectors.WindowsMediaSource();
  var first=await pc.ReadAsync();await System.Threading.Tasks.Task.Delay(2000);var second=await pc.ReadAsync();
  Console.WriteLine($"REAL PC: firstCpuUnavailable={first.Cpu is null}; secondCpu={second.Cpu:F1}; RAMGiB={second.RamUsed:F2}/{second.RamTotal:F2}; GPU={second.Gpu}; GPUC={second.GpuTemperature}; cpuIdentityAvailable={second.CpuName!="Indisponible"}");
  pc.Reset();var resumed=await pc.ReadAsync();Console.WriteLine($"SIMULATED resume on real source: cpuUnavailable={resumed.Cpu is null}");
  try{
   using var cancel=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));await media.StartAsync(cancel.Token);
   for(int i=0;i<3;i++){
    var sample=await media.ReadAsync(cancel.Token);
    Console.WriteLine($"REAL SMTC: status={sample.Status}; playing={sample.Playing}; titleAvailable={!string.IsNullOrWhiteSpace(sample.Title)}; artBytes={sample.Artwork?.Length??0}; position={sample.Position:F1}; duration={sample.Duration:F1}; sessionIdAvailable={sample.SessionId is not null}");
    await System.Threading.Tasks.Task.Delay(1000);
   }
  }catch(Exception e){Console.WriteLine("REAL SMTC unavailable: "+e.GetType().Name);}
  finally{await media.StopAsync();}
  return first.Cpu is null&&second.Cpu is >=0 and <=100&&second.RamUsed is >0&&resumed.Cpu is null?0:1;
 }
 static void CheckMedia(Axis.App.MainWindow window,ModuleRuntime runtime,MediaModule media){
  var entry=runtime.Modules.Single(m=>m.Id=="media");var original=entry.Data!;var art=media.Artwork;var observation=media.Snapshot;
  var labels=(Dictionary<string,TextBlock>)Field(window,"values");var cover=(Image)Field(window,"cover");var progress=(ProgressBar)Field(window,"progress");
  var update=typeof(Axis.App.MainWindow).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic)!;
  try{
   Set(media,"Snapshot",new MediaSnapshot(original.Timestamp.AddSeconds(1),new("Disponible","Next title",Artwork:[1])));update.Invoke(window,null);
   if(cover.Source is not null)throw new Exception("Mixed next artwork with previous title");Set(media,"Snapshot",observation);
   var paused=original.Values.ToDictionary(x=>x.Key,x=>x.Value);paused["playback"]="Pause";paused["position"]="63";
   Set(entry,"Data",original with{Values=paused});update.Invoke(window,null);
   if(!labels["media.playback"].Text.Contains("PAUSE")||!labels["media.time"].Text.StartsWith("1:03"))throw new Exception("Pause display");
   Set(entry,"Data",original with{Timestamp=DateTimeOffset.UtcNow.AddSeconds(-7)});update.Invoke(window,null);
   if(cover.Source is not null||progress.Value!=0||labels["media.title"].Text!="Observation périmée")throw new Exception("Stale media retained");
   Set(media,"Artwork",null);var empty=new Dictionary<string,string>{{"status","Aucune session"}};
   Set(entry,"Data",original with{Values=empty});update.Invoke(window,null);
   if(cover.Source is not null||!labels["media.title"].Text.Contains("Aucune musique"))throw new Exception("Empty media retained");
   var unknown=paused.ToDictionary(x=>x.Key,x=>x.Value);unknown["duration"]="";unknown["position"]="";
   Set(entry,"Data",original with{Values=unknown});update.Invoke(window,null);
   if(progress.Value!=0||labels["media.time"].Text!="—:— / —:—")throw new Exception("Unknown timeline invented");
   Console.WriteLine("PASS WPF media pause, stale cover removal, empty state and unknown timeline");
  }finally{Set(entry,"Data",original);Set(media,"Snapshot",observation);Set(media,"Artwork",art);update.Invoke(window,null);}
 }
 static void CheckHome(Axis.App.MainWindow window,CodexModule codex){
  var update=typeof(Axis.App.MainWindow).GetMethod("UpdateHomeActivity",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var host=(StackPanel)Field(window,"homeActivity");var original=codex.Missions.Current;var now=DateTimeOffset.UtcNow;
  var sample=original.Recent.First(m=>m.Identity=="fixture-active");
  try{
   var quota=(WrapPanel)Field(window,"homeQuotaHost");var stable=quota.Children[0];Call(window,"Update");if(!ReferenceEquals(stable,quota.Children[0]))throw new Exception("Home quotas rebuilt on tick");
   void Show(params MissionObservation[] rows){Set(codex.Missions,"Current",original with{Checked=now,Recent=rows});update.Invoke(window,[true]);}
   for(int count=1;count<=4;count++){Show(Enumerable.Range(0,count).Select(i=>sample with{Identity="home-"+i,LastActivity=now}).ToArray());var views=(System.Collections.IDictionary)Field(window,"homeRows");if(views.Count!=count)throw new Exception("Home mission limit");}
   foreach(string state in new[]{"TERMINÉ","ÉCHEC","INTERROMPU"}){Show(sample with{Status=state,Finished=now.AddSeconds(-59)});if(host.Visibility!=Visibility.Visible)throw new Exception("Recent terminal hidden");Show(sample with{Status=state,Finished=now.AddSeconds(-61)});if(host.Visibility!=Visibility.Collapsed)throw new Exception("Expired terminal visible");}
   Show(sample with{LastActivity=now.AddSeconds(-91)});if(host.Visibility!=Visibility.Collapsed)throw new Exception("Stale active visible");
   Show();if(host.Visibility!=Visibility.Collapsed)throw new Exception("Empty component reserves space");
   Console.WriteLine("PASS Home 1–4 missions, terminal expiry, uncertainty and empty state");
  }finally{Set(codex.Missions,"Current",original);update.Invoke(window,[true]);}
 }
 static void CheckStableCards(Axis.App.MainWindow window,CodexModule codex){
  var update=typeof(Axis.App.MainWindow).GetMethod("UpdateCodex",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var cards=(System.Collections.IDictionary)Field(window,"missionCards");var view=cards["fixture-active"]!;var card=(FrameworkElement)Field(view,"Card");
  var quota=(WrapPanel)Field(window,"quotaCards");var first=quota.Children[0];update.Invoke(window,null);
  if(!ReferenceEquals(first,quota.Children[0])||!ReferenceEquals(view,cards["fixture-active"]))throw new Exception("Tick rebuilt stable cards");
  var original=codex.Missions.Current;var now=DateTimeOffset.UtcNow;
  try{
   MissionObservation Change(MissionObservation m)=>m.Identity=="fixture-active"?m with{Status="TERMINÉ",Finished=now,Duration="04:00"}:m;
   Set(codex.Missions,"Current",original with{Checked=now,Recent=original.Recent.Select(Change).ToArray()});update.Invoke(window,null);
   if(((TextBlock)Field(view,"State")).Text!="TERMINÉ"||!ReferenceEquals(card.Parent,Field(window,"missionPanel")))throw new Exception("Terminal did not hold its card");
   Set(codex.Missions,"Current",codex.Missions.Current with{Recent=codex.Missions.Current.Recent.Select(m=>m.Identity=="fixture-active"?m with{Finished=now.AddSeconds(-9)}:m).ToArray()});update.Invoke(window,null);
   if(!ReferenceEquals(card.Parent,Field(window,"recentMissionPanel")))throw new Exception("Terminal did not move into recent list");
   Set(codex.Missions,"Current",original with{Checked=now,Recent=original.Recent.Select(m=>m.Identity=="fixture-active"?m with{LastActivity=now.AddSeconds(-91)}:m).ToArray()});update.Invoke(window,null);
   if(((TextBlock)Field(view,"State")).Text!="À VÉRIFIER")throw new Exception("Expired activity remained active");
   Console.WriteLine("PASS WPF stable cards, terminal hold/move, and expired activity projection");
  }finally{Set(codex.Missions,"Current",original);update.Invoke(window,null);}
 }
 static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static void CheckMotion(){
  var glyph=new MissionGlyph();var host=new Window{Title="AXIS • SIMULATION WPF",Content=glyph,Width=180,Height=150,ShowInTaskbar=false};host.Show();
  try{
   glyph.SetState("EN COURS");Pump(80);var transform=(RotateTransform)glyph.RenderTransform;double first=transform.Angle;Pump(180);
   if(!transform.HasAnimatedProperties||Math.Abs(transform.Angle-first)<1)throw new Exception("WPF active ring did not advance");
   Console.WriteLine("PASS WPF real animation clock advances while UI dispatcher remains responsive");
   foreach(string status in new[]{"TERMINÉ","ÉCHEC","INTERROMPU","À VÉRIFIER","INDISPONIBLE"}){
    glyph.SetState(status);Pump(40);if(transform.HasAnimatedProperties||transform.Angle!=0)throw new Exception("Ring did not stop: "+status);
    Console.WriteLine("PASS WPF ring stops immediately: "+status);glyph.SetState("EN COURS");Pump(20);
   }
   MissionGlyph.SetAnimations(host,false);Pump(40);if(transform.HasAnimatedProperties)throw new Exception("Disabled animation still runs");MissionGlyph.SetAnimations(host,true);Pump(40);if(!transform.HasAnimatedProperties)throw new Exception("Animation preference not restored");
   host.Hide();Pump(40);if(transform.HasAnimatedProperties)throw new Exception("Hidden ring still animated");
   host.Show();Pump(40);if(!transform.HasAnimatedProperties)throw new Exception("Visible active ring did not resume");
   glyph.SetState("À VÉRIFIER");Pump(20);if(transform.HasAnimatedProperties)throw new Exception("Uncertain observation animated");
   Console.WriteLine("PASS WPF visibility and uncertainty stop animation");
  }finally{host.Close();}
 }
 static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}}
}
