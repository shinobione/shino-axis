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
using Axis.App.Collectors;
using Forms=System.Windows.Forms;
namespace Axis.App;
public partial class MainWindow : Window {
 readonly ProjectsModule projects;readonly ModuleRuntime runtime;readonly MediaModule media;readonly CodexModule codex;
 readonly SettingsStore store;AppSettings settings=AppSettings.Default();
 readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly Forms.NotifyIcon tray;readonly System.Drawing.Icon axisIcon;
 readonly Dictionary<string,TextBlock> values=[];readonly Dictionary<string,CheckBox> controls=[];
 Image? cover;ProgressBar? progress;byte[]? shownArtwork;
 bool polling,closing;string page="Accueil";
 public MainWindow(){
  InitializeComponent();var pc=new WindowsPcSource();media=new(new WindowsMediaSource());

  var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--data-dir");
  var directory=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SHINO","AXIS");store=new(directory);codex=new(new CodexQuotaSource(),new QuotaHistory(directory));int port=4187;int portIndex=Array.IndexOf(args,"--projects-port");if(portIndex>=0&&portIndex+1<args.Length&&int.TryParse(args[portIndex+1],out int configured)&&configured is >=1024 and <=65535)port=configured;projects=new(new Axis.Core.Projects.ProjectStore(directory),port);runtime=new([new PcModule(pc.ReadAsync),new NetworkModule(NetworkModule.ReadWindows),media,codex,projects]);
  axisIcon=CreateIcon();tray=new(){Icon=axisIcon,Text="SHINO // AXIS V0.8",Visible=true};
  Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(axisIcon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
  var menu=new Forms.ContextMenuStrip();menu.Items.Add("Ouvrir AXIS",null,(_,_)=>Dispatcher.Invoke(Restore));menu.Items.Add("Quitter AXIS",null,(_,_)=>Dispatcher.Invoke(Close));tray.ContextMenuStrip=menu;
  tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Restore);
  Loaded+=async(_,_)=>{
   var loaded=await store.LoadAsync();settings=loaded.Settings;codex.RefreshSeconds=settings.CodexRefreshSeconds;Notice.Text=loaded.Warning;
   // Older settings gain the new modules without changing explicit preferences.
   foreach(var m in runtime.Modules){settings.EnabledModules.TryAdd(m.Id,true);await Task.Run(()=>runtime.SetEnabledAsync(m.Id,settings.EnabledModules[m.Id],DateTimeOffset.UtcNow));}
   Render();await Poll();timer.Start();
  };
  timer.Tick+=async(_,_)=>{UpdateCodex();await Poll();};

  Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
  StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized&&settings.MinimizeToTray)Hide();};
  Closing+=async(_,e)=>{if(closing)return;e.Cancel=true;closing=true;Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;timer.Stop();await Task.Run(()=>runtime.StopAllAsync(DateTimeOffset.UtcNow));tray.Dispose();axisIcon.Dispose();Close();};
 }
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
  if(e.Mode is Microsoft.Win32.PowerModes.Suspend or Microsoft.Win32.PowerModes.Resume)Dispatcher.BeginInvoke(new Action(()=>{codex.OnResume();UpdateCodex();if(e.Mode==Microsoft.Win32.PowerModes.Resume)_=Poll();}));
 }
 void Restore(){Show();WindowState=WindowState.Normal;Activate();}
 async Task Poll(){if(polling||closing)return;polling=true;try{await Task.Run(()=>runtime.TickAsync(DateTimeOffset.UtcNow));if(!closing){Update();foreach(var ended in codex.Missions.TakeNotifications())if(settings.NotifyMissionCompletion)tray.ShowBalloonTip(5000,"AXIS • tour Codex terminé",ended.Workspace+" • durée "+ended.Duration,Forms.ToolTipIcon.Info);}}finally{polling=false;}}
 void Navigate(string name){page=name;Render();Update();ContentScroll.ScrollToTop();}
 void HomeClick(object s,RoutedEventArgs e)=>Navigate("Accueil");
 void PcClick(object s,RoutedEventArgs e)=>Navigate("PC");
 void CodexClick(object s,RoutedEventArgs e)=>Navigate("CODEX");
 void ModulesClick(object s,RoutedEventArgs e)=>Navigate("Modules");
 void SettingsClick(object s,RoutedEventArgs e)=>Navigate("Paramètres");
 void TrayClick(object s,RoutedEventArgs e)=>Hide();
 void QuitClick(object s,RoutedEventArgs e)=>Close();
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
  var art=new Grid{Width=144,Height=144,Margin=new(0,0,18,0),Background=Axis.App.Controls.QuotaDial.Ink("#152033")};cover.Margin=new(0);
  var fallback=Text("♫",48);fallback.Foreground=Axis.App.Controls.QuotaDial.Ink("#60738D");fallback.HorizontalAlignment=HorizontalAlignment.Center;fallback.VerticalAlignment=VerticalAlignment.Center;art.Children.Add(fallback);art.Children.Add(cover);
  DockPanel.SetDock(art,Dock.Left);dock.Children.Add(art);
  var stack=new StackPanel();
  foreach(var (title,key,size) in new[]{("","media.title",21),("","media.artist",16),("Album : ","media.album",13),("","media.playback",13),("Source : ","media.app",12)}){
   var label=Text("Indisponible",size);label.Tag=title;if(key=="media.title")label.FontWeight=FontWeights.SemiBold;if(size<=13)label.Foreground=Axis.App.Controls.QuotaDial.Ink("#A4B5CB");values[key]=label;stack.Children.Add(label);
  }
  progress=new(){Height=5,Minimum=0,Maximum=1,Margin=new(0,10,0,6),Foreground=new SolidColorBrush(Color.FromRgb(125,223,234))};stack.Children.Add(progress);
  var time=Text("",12);values["media.time"]=time;stack.Children.Add(time);dock.Children.Add(stack);(target??PageContent).Children.Add(PanelCard(dock));
 }
 void Render(){
  PageTitle.Text=page;PageContent.Children.Clear();metricKeys.Clear();homeQuotaHost=null;homeProjects=null;homeMission=null;values.Clear();controls.Clear();quotaCards=null;chart=null;missionPanel=null;projectList=null;projectDetail=null;cover=null;progress=null;shownArtwork=null;
  if(page=="Accueil"){RenderHome();
  }else if(page=="PROJECTS"){RenderProjects();
  }else if(page=="CODEX"){RenderCodex();
  }else if(page=="PC"){RenderPc();
  }else if(page=="Modules"){
   foreach(var m in runtime.Modules){
    var stack=new StackPanel();var box=new CheckBox{Content=m.Name,IsChecked=m.State==ModuleState.Active};
    controls[m.Id]=box;
    box.Click+=async(_,_)=>{bool enabled=box.IsChecked==true;box.IsEnabled=false;await Task.Run(()=>runtime.SetEnabledAsync(m.Id,enabled,DateTimeOffset.UtcNow));settings.EnabledModules[m.Id]=m.State==ModuleState.Active;await Save();box.IsEnabled=true;Update();};
    stack.Children.Add(box);stack.Children.Add(Text(m.Description));
    var state=Text("",13);values[m.Id+".state"]=state;stack.Children.Add(state);PageContent.Children.Add(PanelCard(stack));
   }
  }else{
   var minimize=new CheckBox{Content="Masquer dans le tray lors de la minimisation",IsChecked=settings.MinimizeToTray};
   minimize.Click+=async(_,_)=>{settings.MinimizeToTray=minimize.IsChecked==true;await Save();};PageContent.Children.Add(minimize);
   var alerts=new CheckBox{Content="Notifier les fins de tours Codex confirmées",IsChecked=settings.NotifyMissionCompletion};alerts.Click+=async(_,_)=>{settings.NotifyMissionCompletion=alerts.IsChecked==true;await Save();};PageContent.Children.Add(alerts);
   PageContent.Children.Add(Text("Fermer la fenêtre quitte AXIS. Le tray permet de restaurer ou quitter l’application."));
   PageContent.Children.Add(Text("Collectes locales • Média en lecture seule • Aucun démarrage automatique."));
  }
 }
 async Task Save(){try{await store.SaveAsync(settings);Notice.Text="Préférences enregistrées.";}catch(Exception e)when(e is IOException or UnauthorizedAccessException){Notice.Text=e.Message;}}
 static string Time(double seconds)=>TimeSpan.FromSeconds(seconds).ToString(seconds>=3600?@"h\:mm\:ss":@"m\:ss");
 void Update(){
  Status.Text=$"{runtime.Modules.Count(m=>m.State==ModuleState.Active)}/{runtime.Modules.Count} modules actifs • {DateTimeOffset.Now:HH:mm:ss} • Données locales";Status.ToolTip=string.Join(Environment.NewLine,runtime.Modules.Select(m=>m.Name+": "+Describe(m)));
  foreach(var (key,label) in values){
   if(key=="summary.modules"){label.Text=string.Join("  ·  ",runtime.Modules.Where(m=>m.State==ModuleState.Active).Select(m=>m.Name));continue;}
   if(key=="summary.events"){label.FontSize=14;label.Text=string.Join(Environment.NewLine,runtime.Events.TakeLast(4).Reverse().Select(e=>$"{e.Timestamp.ToLocalTime():HH:mm:ss}  {e.ModuleId} — {e.Message}"));continue;}
   var parts=key.Split('.',2);var module=runtime.Modules.Single(m=>m.Id==parts[0]);
   if(parts[1]=="state"){label.Text=Describe(module);if(controls.TryGetValue(module.Id,out var box))box.IsChecked=module.State==ModuleState.Active;continue;}
   var data=module.State==ModuleState.Active?module.Data:null;
   var content=data?.Values.GetValueOrDefault(parts[1],"Indisponible")??"Indisponible";if(key=="media.app")content=FriendlyMedia(content);label.Text=(label.Tag as string??"")+content;if(key is "media.album" or "media.artist" or "media.app")label.Visibility=content=="Indisponible"?Visibility.Collapsed:Visibility.Visible;
  }
  UpdateCodex();UpdateProjects();UpdateDashboard();
  var mediaEntry=runtime.Modules.Single(m=>m.Id=="media");var snapshot=mediaEntry.State==ModuleState.Active?mediaEntry.Data:null;
  bool Parse(string key,out double number)=>double.TryParse(snapshot?.Values.GetValueOrDefault(key),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&double.IsFinite(number)&&number>=0;
  if(progress is not null&&values.TryGetValue("media.time",out var time)){
   bool valid=Parse("duration",out var duration)&&duration>0;bool positionValid=Parse("position",out var position);
   progress.Value=valid&&positionValid?Math.Clamp(position/duration,0,1):0;
   time.Text=(positionValid?Time(position):"Indisponible")+" / "+(valid?Time(duration):"Indisponible");
  }
  var bytes=mediaEntry.State==ModuleState.Active?media.Artwork:null;
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
 static string Describe(ModuleEntry m)=>m.State switch {
  ModuleState.Stopped=>"Désactivé",ModuleState.Error=>"Erreur : "+m.Error,_=>m.Data is null?"Initialisation":$"{(DateTimeOffset.UtcNow-m.Data.Timestamp>TimeSpan.FromSeconds(6)?"Périmé":"Actif")} · {m.Data.Timestamp.ToLocalTime():HH:mm:ss}"
 };
}
