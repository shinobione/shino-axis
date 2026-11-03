using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Axis.Core;
using Axis.Core.Modules;
using Axis.Core.Presentation;
using Axis.App.Collectors;
using Forms=System.Windows.Forms;
namespace Axis.App;
public partial class MainWindow : Window {
 readonly WindowsPcSource pc=new();
 readonly ProjectsModule projects;readonly ModuleRuntime runtime;readonly MediaModule media;readonly CodexModule codex;
 readonly SettingsStore store;AppSettings settings=AppSettings.Default();
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly Forms.NotifyIcon tray;readonly System.Drawing.Icon axisIcon;
 readonly Dictionary<string,TextBlock> values=[];readonly Dictionary<string,CheckBox> controls=[];
 Image? cover;Grid? artworkHost;ProgressBar? progress;byte[]? shownArtwork;
 bool polling,closing,exitRequested;string page="Accueil";
 Task? initialization;Task? shutdown;bool initialized,settingsLoaded;
 public bool IsShuttingDown=>closing;
 readonly bool isolated=Array.IndexOf(Environment.GetCommandLineArgs(),"--data-dir")>=0;
 readonly StartupRegistration startup=new(new WindowsStartupEntry(),Environment.ProcessPath!);
 public static string ProfileDirectory(){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--data-dir");return index>=0&&index+1<args.Length?Path.GetFullPath(args[index+1]):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SHINO","AXIS");}

 public MainWindow(){
  InitializeComponent();media=new(new WindowsMediaSource());

  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--data-dir");
  var directory=ProfileDirectory();store=new(directory);codex=new(new CodexQuotaSource(),new QuotaHistory(directory));int port=4187;int portIndex=Array.IndexOf(args,"--projects-port");if(portIndex>=0&&portIndex+1<args.Length&&int.TryParse(args[portIndex+1],out int configured)&&configured is >=1024 and <=65535)port=configured;projects=new(new Axis.Core.Projects.ProjectStore(directory),port);runtime=new([new PcModule(pc.ReadAsync,pc.Reset),new NetworkModule(NetworkModule.ReadWindows),media,codex,projects]);
  axisIcon=CreateIcon();tray=new(){Icon=axisIcon,Text="SHINO // AXIS V1.0",Visible=true};
  Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(axisIcon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
  var menu=new Forms.ContextMenuStrip();
  menu.Items.Add("Afficher AXIS",null,(_,_)=>Dispatcher.Invoke(Restore));
  menu.Items.Add("Masquer AXIS",null,(_,_)=>Dispatcher.Invoke(Hide));
  menu.Items.Add("Accueil",null,(_,_)=>Dispatcher.Invoke(()=>{Restore();Navigate("Accueil");}));
  menu.Items.Add("Paramètres",null,(_,_)=>Dispatcher.Invoke(()=>{Restore();Navigate("Paramètres");}));
  menu.Items.Add("Quitter AXIS",null,(_,_)=>Dispatcher.Invoke(RequestExit));tray.ContextMenuStrip=menu;
  tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Restore);
  Loaded+=async(_,_)=>{if(initialization is not null||closing)return;initialization=InitializeAsync();await initialization;};
  timer.Tick+=async(_,_)=>{if(polling){if(IsVisible)Update();}else await Poll();};

  Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
  StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized&&settings.MinimizeToTray)Hide();};
  Closing+=(_,e)=>{if(shutdown?.IsCompleted==true)return;e.Cancel=true;
   if(!exitRequested&&initialized&&settings.CloseToTray){Hide();return;}
   if(closing)return;closing=true;shutdown=ShutdownAsync();};
 }
 async Task InitializeAsync(){
  try{
   var loaded=await store.LoadAsync();settings=loaded.Settings;settingsLoaded=true;codex.RefreshSeconds=settings.CodexRefreshSeconds;Notice.Text=loaded.Warning;
   foreach(var m in runtime.Modules){if(closing)break;settings.EnabledModules.TryAdd(m.Id,true);await Task.Run(()=>runtime.SetEnabledAsync(m.Id,settings.EnabledModules[m.Id],DateTimeOffset.UtcNow));}
   if(closing)return;initialized=true;ApplyAnimations();
   page=settings.RestoreLastPage&&new[]{"Accueil","PC","NETWORK","MEDIA","CODEX","PROJECTS","Modules","Paramètres"}.Contains(settings.LastPage)?settings.LastPage:"Accueil";
   Render();await Poll();if(!closing)timer.Start();
  }catch(Exception error){Notice.Text="Initialisation indisponible ("+error.GetType().Name+"). Quitter puis réessayer.";}
 }
 async Task ShutdownAsync(){
  IsEnabled=false;tray.Visible=false;
  Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;timer.Stop();
  var stopped=runtime.ShutdownAsync(DateTimeOffset.UtcNow);
  try{if(initialization is not null)await initialization;await stopped;if(settingsLoaded)await Save();}
  finally{tray.Visible=false;tray.ContextMenuStrip?.Dispose();tray.Dispose();axisIcon.Dispose();_=Dispatcher.BeginInvoke(new Action(Close));}
 }
 void RequestExit(){exitRequested=true;Close();}
 void ApplyAnimations(){Axis.App.Controls.MissionGlyph.SetAnimations(this,settings.AnimationsEnabled);foreach(var row in homeRows.Values){row.Layout.BeginAnimation(UIElement.OpacityProperty,null);row.Layout.Opacity=1;row.FadeEnd=null;}}
 static System.Drawing.Icon CreateIcon(){
  using var bitmap=new System.Drawing.Bitmap(32,32);
  using(var g=System.Drawing.Graphics.FromImage(bitmap)){
   g.Clear(System.Drawing.Color.FromArgb(22,30,46));
   using var pen=new System.Drawing.Pen(System.Drawing.Color.FromArgb(125,223,234),3);
   g.DrawLines(pen,[new(7,25),new(16,6),new(25,25)]);g.DrawLine(pen,11,19,22,19);
   using var coral=new System.Drawing.SolidBrush(System.Drawing.Color.Coral);g.FillEllipse(coral,23,3,6,6);
  }
  var handle=bitmap.GetHicon();try{using var original=System.Drawing.Icon.FromHandle(handle);return (System.Drawing.Icon)original.Clone();}finally{DestroyIcon(handle);}
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr handle);
 void PowerChanged(object sender,Microsoft.Win32.PowerModeChangedEventArgs e){
  if(!closing&&e.Mode is Microsoft.Win32.PowerModes.Suspend or Microsoft.Win32.PowerModes.Resume)Dispatcher.BeginInvoke(new Action(()=>{if(closing)return;pc.Reset();codex.OnResume();if(IsVisible)UpdateCodex();if(e.Mode==Microsoft.Win32.PowerModes.Resume)_=Poll();}));
 }
 void Restore(){if(closing)return;Show();WindowState=WindowState.Normal;Activate();if(initialized)Update();}
 async Task Poll(){if(polling||closing)return;polling=true;try{await Task.Run(()=>runtime.TickAsync(DateTimeOffset.UtcNow));if(!closing){if(IsVisible)Update();await ObserveExternal();foreach(var ended in codex.Missions.TakeNotifications())if(settings.NotifyMissionCompletion)tray.ShowBalloonTip(5000,"AXIS • tour Codex terminé",ended.Workspace+" • durée "+ended.Duration,Forms.ToolTipIcon.Info);}}finally{polling=false;}}
 void Navigate(string name){if(closing)return;page=name;Render();Update();ContentScroll.ScrollToTop();if(initialized&&settings.RestoreLastPage&&settings.LastPage!=name){settings.LastPage=name;_=Save();}}
 void HomeClick(object s,RoutedEventArgs e)=>Navigate("Accueil");
 void PcClick(object s,RoutedEventArgs e)=>Navigate("PC");
 void CodexClick(object s,RoutedEventArgs e)=>Navigate("CODEX");
 void ModulesClick(object s,RoutedEventArgs e)=>Navigate("Modules");
 void SettingsClick(object s,RoutedEventArgs e)=>Navigate("Paramètres");
 void TrayClick(object s,RoutedEventArgs e)=>Hide();
 void QuitClick(object s,RoutedEventArgs e)=>RequestExit();
 TextBlock Text(string text,double size=15)=>new(){Text=text,FontSize=size,Margin=new(0,4,0,4)};
 Border PanelCard(UIElement child)=>new(){Background=new SolidColorBrush(Color.FromRgb(27,37,56)),CornerRadius=new(12),Padding=new(14),Margin=new(0,0,12,12),Child=child};
 void Card(Panel parent,string title,string key){
  var stack=new StackPanel();var caption=Text(title,12);caption.Foreground=new SolidColorBrush(Color.FromRgb(170,187,212));stack.Children.Add(caption);
  var label=Text("Indisponible",22);values[key]=label;stack.Children.Add(label);
  parent.Children.Add(PanelCard(stack));
 }
 void GridCards(params (string Title,string Key)[] cards){
  var row=new WrapPanel();
  foreach(var c in cards){
   var stack=new StackPanel();stack.Children.Add(Text(c.Title,12));var value=Text("Indisponible",16);values[c.Key]=value;stack.Children.Add(value);
   var tile=PanelCard(stack);tile.Width=300;row.Children.Add(tile);
  }
  PageContent.Children.Add(row);
 }
 void Music(Panel? target=null){
  var dock=new DockPanel();cover=new(){Width=144,Height=144,Stretch=Stretch.Uniform,Margin=new(0,0,20,0)};shownArtwork=null;
  var art=new Grid{Width=144,Height=144,Margin=new(0,0,18,0),Background=Axis.App.Controls.QuotaDial.Ink("#152033")};artworkHost=art;cover.Margin=new(0);
  var fallback=Text("♫",48);fallback.Foreground=Axis.App.Controls.QuotaDial.Ink("#60738D");fallback.HorizontalAlignment=HorizontalAlignment.Center;fallback.VerticalAlignment=VerticalAlignment.Center;art.Children.Add(fallback);art.Children.Add(cover);
  DockPanel.SetDock(art,Dock.Left);dock.Children.Add(art);
  var stack=new StackPanel();
  var heading=Text("MEDIA  /  SESSION WINDOWS",11);heading.Foreground=Axis.App.Controls.QuotaDial.Ink("#83CFE5");stack.Children.Add(heading);
  foreach(var (title,key,size) in new[]{("","media.title",21),("","media.artist",16),("Album : ","media.album",13),("","media.playback",13),("Source : ","media.app",12)}){
   var label=Text("Indisponible",size);label.Tag=title;if(key=="media.title")label.FontWeight=FontWeights.SemiBold;if(size<=13)label.Foreground=Axis.App.Controls.QuotaDial.Ink("#A4B5CB");values[key]=label;stack.Children.Add(label);
  }
  progress=new(){Height=6,Minimum=0,Maximum=1,Margin=new(0,10,0,6),Foreground=new SolidColorBrush(Color.FromRgb(125,223,234))};stack.Children.Add(progress);
  var time=Text("",12);values["media.time"]=time;stack.Children.Add(time);dock.Children.Add(stack);var card=PanelCard(dock);card.BorderBrush=Axis.App.Controls.QuotaDial.Ink("#30435C");card.BorderThickness=new(1);(target??PageContent).Children.Add(card);
 }
 void Render(){
  PageTitle.Text=page;PageContent.Children.Clear();metricKeys.Clear();homeQuotaHost=null;homeProjects=null;homeActivity=null;attentionText=null;healthText=null;externalText=null;homeQuotaSignature=null;values.Clear();controls.Clear();quotaCards=null;chart=null;missionPanel=null;projectList=null;projectDetail=null;cover=null;progress=null;shownArtwork=null;
  if(page=="Accueil"){RenderHome();
  }else if(page=="PROJECTS"){RenderProjects();
  }else if(page=="CODEX"){RenderCodex();
  }else if(page=="PC"){RenderPc();
  }else if(page=="MEDIA"){Music();
  }else if(page=="NETWORK"){GridCards(("Interface","network.name"),("Connexion","network.connection"),("IPv4 locale","network.ipv4"),("↓ Réseau","network.download"),("↑ Réseau","network.upload"),("Téléchargé • session","network.downloaded"),("Envoyé • session","network.uploaded"));
  }else if(page=="Modules"){RenderModules();
  }else{RenderSettings();}
 }

 async Task Save(){try{await store.SaveAsync(settings);Notice.Text="Préférences enregistrées.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Notice.Text="Écriture indisponible ("+e.GetType().Name+"). Configuration conservée.";}}
 static string Time(double seconds)=>TimeSpan.FromSeconds(seconds).ToString(seconds>=3600?@"h\:mm\:ss":@"m\:ss");
 void Update(){
  Status.Text=$"{runtime.Modules.Count(m=>m.State==ModuleState.Active)}/{runtime.Modules.Count} modules actifs • {DateTimeOffset.Now:HH:mm:ss} • Données locales";Status.ToolTip=string.Join(Environment.NewLine,runtime.Modules.Select(m=>m.Name+": "+Describe(m)));
  foreach(var (key,label) in values){
   if(key=="summary.modules"){label.Text=string.Join("  ·  ",runtime.Modules.Where(m=>m.State==ModuleState.Active).Select(m=>m.Name));continue;}
   if(key=="summary.events"){label.FontSize=14;label.Text=string.Join(Environment.NewLine,runtime.Events.TakeLast(4).Reverse().Select(e=>$"{e.Timestamp.ToLocalTime():HH:mm:ss}  {e.ModuleId} — {e.Message}"));continue;}
   var parts=key.Split('.',2);var module=runtime.Modules.Single(m=>m.Id==parts[0]);
   if(parts[1]=="state"){label.Text=Describe(module);if(controls.TryGetValue(module.Id,out var box))box.IsChecked=settings.EnabledModules.GetValueOrDefault(module.Id,true);continue;}
   var data=module.State==ModuleState.Active?module.Data:null;
   bool expired=module.Id is "pc" or "media" or "network"&&data is not null&&(DateTimeOffset.UtcNow<data.Timestamp||DateTimeOffset.UtcNow-data.Timestamp>TimeSpan.FromSeconds(6));
   if(expired)data=null;
   var content=data?.Values.GetValueOrDefault(parts[1],"Indisponible")??"Indisponible";if(key=="media.app")content=FriendlyMedia(content);if(key=="media.title"&&(data is null||data.Values.GetValueOrDefault("status")=="Aucune session"))content=expired?"Observation périmée":"Aucune musique en cours";
   if(key=="media.playback")content=content switch{"Lecture"=>"●  LECTURE","Pause"=>"Ⅱ  PAUSE",_=>"—  En attente du lecteur"};
   label.Text=(label.Tag as string??"")+content;if(key is "media.album" or "media.artist" or "media.app")label.Visibility=content=="Indisponible"?Visibility.Collapsed:Visibility.Visible;
  }
  UpdateCodex();UpdateProjects();UpdateDashboard();UpdateCockpit();
  var mediaEntry=runtime.Modules.Single(m=>m.Id=="media");var snapshot=mediaEntry.State==ModuleState.Active?mediaEntry.Data:null;
  bool mediaFresh=snapshot is not null&&DateTimeOffset.UtcNow>=snapshot.Timestamp&&DateTimeOffset.UtcNow-snapshot.Timestamp<=TimeSpan.FromSeconds(6);
  if(!mediaFresh)snapshot=null;
  bool hasSession=snapshot is not null&&snapshot.Values.GetValueOrDefault("status") is not ("Aucune session" or "Indisponible");
  if(artworkHost is not null)artworkHost.Visibility=hasSession?Visibility.Visible:Visibility.Collapsed;
  bool Parse(string key,out double number)=>double.TryParse(snapshot?.Values.GetValueOrDefault(key),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&double.IsFinite(number)&&number>=0;
  if(progress is not null&&values.TryGetValue("media.time",out var time)){
   progress.Visibility=time.Visibility=hasSession?Visibility.Visible:Visibility.Collapsed;
   bool valid=Parse("duration",out var duration)&&duration>0;bool positionValid=Parse("position",out var position);
   progress.Opacity=valid&&positionValid?1:.3;
   progress.Value=valid&&positionValid?Math.Clamp(position/duration,0,1):0;
   time.Text=(positionValid?Time(position):"—:—")+" / "+(valid?Time(duration):"—:—");
  }
  var observed=media.Snapshot;
  // The polling worker may already hold the next observation: never mix its art with the displayed title.
  var bytes=mediaFresh?(observed is null?media.Artwork:observed.CapturedAt==snapshot!.Timestamp&&observed.IsAvailable?observed.Reading.Artwork:null):null;
  if(cover is not null&&!ReferenceEquals(bytes,shownArtwork)){
   shownArtwork=bytes;cover.Source=null;
   if(bytes is not null)try{using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=256;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();cover.Source=bitmap;}catch(Exception e)when(e is NotSupportedException or IOException or ArgumentException){}
  }
 }
  static string FriendlyMedia(string id){
  if(id.Contains("spotify",StringComparison.OrdinalIgnoreCase))return "Spotify";
  if(id.Contains("chrome",StringComparison.OrdinalIgnoreCase))return "Chrome";
  if(id.Contains("msedge",StringComparison.OrdinalIgnoreCase))return "Microsoft Edge";
  if(id.Contains("firefox",StringComparison.OrdinalIgnoreCase))return "Firefox";
  if(id.Contains("vlc",StringComparison.OrdinalIgnoreCase))return "VLC";
  if(id.Contains("zunemusic",StringComparison.OrdinalIgnoreCase))return "Lecteur multimédia";
  return id.Contains('_')||id.Contains('!')?"Lecteur Windows":id;
 }
 static string Describe(ModuleEntry m){var h=CockpitStatus.Read(m,DateTimeOffset.UtcNow);return h.State+" · "+h.Detail+" · Source : "+(h.Observed?.ToLocalTime().ToString("dd/MM HH:mm:ss")??"aucune observation");}
}
