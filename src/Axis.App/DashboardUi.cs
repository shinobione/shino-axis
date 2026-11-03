using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Axis.App.Controls;
using Axis.Core.Presentation;
using Axis.Core.Projects;
namespace Axis.App;
public partial class MainWindow {
 readonly Dictionary<string,int> metricBands=[];
 readonly HashSet<string> metricKeys=[];
 WrapPanel? homeQuotaHost;StackPanel? homeProjects;TextBlock? homeMission;
 void Metrics(params (string Title,string Key)[] metrics){
  var row=new WrapPanel{Margin=new(0,0,0,4)};
  foreach(var m in metrics){
   var stack=new StackPanel();stack.Children.Add(Text(m.Title,12));
   var value=Text("Indisponible",25);value.FontWeight=FontWeights.SemiBold;values[m.Key]=value;metricKeys.Add(m.Key);value.TextWrapping=TextWrapping.NoWrap;stack.Children.Add(new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,Height=34,Child=value});
   var tile=PanelCard(stack);tile.Width=144;tile.MinHeight=88;tile.ToolTip="Mesure locale — disponibilité et fraîcheur dans les modules";row.Children.Add(tile);
  }
  PageContent.Children.Add(row);
 }
 Grid Columns(UIElement first,UIElement second,double breakpoint=940,double firstWeight=1.12){
  var grid=new Grid{MaxWidth=1640,HorizontalAlignment=HorizontalAlignment.Stretch};
  grid.ColumnDefinitions.Add(new(){Width=new GridLength(firstWeight,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
  grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});
  grid.Children.Add(first);grid.Children.Add(second);
  void Arrange(){bool wide=grid.ActualWidth>=breakpoint;Grid.SetRow(second,wide?0:1);Grid.SetColumn(second,wide?1:0);Grid.SetColumnSpan(first,wide?1:2);Grid.SetColumnSpan(second,wide?1:2);}
  grid.SizeChanged+=(_,_)=>Arrange();Arrange();return grid;
 }
 void RenderHome(){
  Metrics(("CPU","pc.cpu"),("GPU","pc.gpu"),("RAM utilisée","pc.ram"),("Temp. GPU","pc.temperature"),("↓ Réseau","network.download"),("↑ Réseau","network.upload"));
  var mediaArea=new StackPanel();Music(mediaArea);
  var codexArea=new StackPanel();
  var summary=new StackPanel();var heading=Text("CODEX • quotas & mission",16);heading.FontWeight=FontWeights.SemiBold;summary.Children.Add(heading);
  homeQuotaHost=new WrapPanel();summary.Children.Add(homeQuotaHost);
  homeMission=Text("NON VÉRIFIÉ",14);summary.Children.Add(homeMission);
  var open=new Button{Content="Ouvrir CODEX →",HorizontalAlignment=HorizontalAlignment.Left,Padding=new(12,7,12,7)};open.Click+=(_,_)=>Navigate("CODEX");summary.Children.Add(open);codexArea.Children.Add(PanelCard(summary));
  PageContent.Children.Add(Columns(mediaArea,codexArea));
  homeProjects=new();var projectBox=new StackPanel();var title=Text("PROJECTS • priorités",16);title.FontWeight=FontWeights.SemiBold;projectBox.Children.Add(title);projectBox.Children.Add(homeProjects);
  var browse=new Button{Content="Gérer les projets →",HorizontalAlignment=HorizontalAlignment.Left,Padding=new(12,7,12,7)};browse.Click+=(_,_)=>Navigate("PROJECTS");projectBox.Children.Add(browse);
  var activity=new StackPanel();Card(activity,"Derniers événements","summary.events");
  PageContent.Children.Add(Columns(PanelCard(projectBox),activity));
 }
 void RenderPc(){
  Metrics(("CPU","pc.cpu"),("GPU","pc.gpu"),("RAM utilisée","pc.ram"),("Temp. GPU","pc.temperature"),("↓ Réseau","network.download"),("↑ Réseau","network.upload"));
  PageContent.Children.Add(Text("Matériel & système",19));
  GridCards(("Processeur","pc.cpuName"),("Carte graphique","pc.gpuName"),("RAM physique totale","pc.ramTotal"),("Système","pc.os"));
  PageContent.Children.Add(Text("Réseau • détails",19));
  GridCards(("Interface","network.name"),("Connexion","network.connection"),("Type","network.type"),("IPv4 locale","network.ipv4"),("Téléchargé • session","network.downloaded"),("Envoyé • session","network.uploaded"));
  PageContent.Children.Add(Text("Couleurs LINK locales : charge, RAM relative et température normalisée séparément. Mo décimaux, compteurs de session observée. Une donnée absente reste indisponible.",12));
 }
 static double? Numeric(string? value){
  if(string.IsNullOrWhiteSpace(value))return null;
  string number=new(value.TakeWhile(c=>char.IsDigit(c)||c is ',' or '.' or '-' or '+').ToArray());
  return double.TryParse(number.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n)&&double.IsFinite(n)?n:null;
 }
 void UpdateDashboard(){
  var pc=runtime.Modules.Single(m=>m.Id=="pc");var reading=pc.State==Axis.Core.ModuleState.Active?pc.Data:null;
  bool fresh=reading is not null&&DateTimeOffset.UtcNow-reading.Timestamp<=TimeSpan.FromSeconds(6);
  foreach(var key in metricKeys){
   if(!values.TryGetValue(key,out var label))continue;label.FontSize=label.Text=="Indisponible"?15:25;
   double? load=Numeric(label.Text);
   if(key=="pc.ram")load=load is {} used&&Numeric(reading?.Values.GetValueOrDefault("ramTotal")) is {} total?DisplayRules.RamLoad(used,total):null;
   if(key=="pc.temperature")load=load is {} temp?DisplayRules.TemperatureLoad(temp):null;
   if(key.StartsWith("network.")){var net=runtime.Modules.Single(m=>m.Id=="network");var data=net.State==Axis.Core.ModuleState.Active?net.Data:null;bool current=data is not null&&DateTimeOffset.UtcNow-data.Timestamp<=TimeSpan.FromSeconds(6);label.Foreground=QuotaDial.Ink(load is null||!current?"#8BA0B1":"#83CFE5");label.ToolTip=data is null?"Indisponible":$"Mesuré à {data.Timestamp.ToLocalTime():HH:mm:ss} • {(current?"récent":"périmé")}";continue;}
   int band=DisplayRules.LinkBand(fresh?load:null,metricBands.GetValueOrDefault(key,-1));metricBands[key]=band;label.Foreground=QuotaDial.Ink(DisplayRules.LinkHex(band));
   label.ToolTip=reading is null?"Indisponible":$"Mesuré à {reading.Timestamp.ToLocalTime():HH:mm:ss} • {(fresh?"récent":"périmé")}";
  }
  if(homeQuotaHost is not null){
   homeQuotaHost.Children.Clear();var entry=runtime.Modules.Single(m=>m.Id=="codex");bool enabled=entry.State==Axis.Core.ModuleState.Active;
   foreach(var w in codex.Snapshot?.Windows.Where(w=>w.LimitId=="codex").Take(2)??[]){
    var stack=new StackPanel{Width=112};var dial=new QuotaDial{Width=96,Height=96};dial.SetReading(enabled?w.Remaining:null,QuotaFresh());stack.Children.Add(dial);
    var label=Text(w.Label,12);label.TextAlignment=TextAlignment.Center;stack.Children.Add(label);homeQuotaHost.Children.Add(stack);
   }
   if(homeQuotaHost.Children.Count==0)homeQuotaHost.Children.Add(Text("Quotas indisponibles",14));
   var credit=new StackPanel{Margin=new(14,12,0,0)};credit.Children.Add(Text(codex.Snapshot?.ResetCredits is {} n?$"{n}":"—",30));credit.Children.Add(Text("crédits de reset",12));homeQuotaHost.Children.Add(credit);
   if(homeMission is not null)homeMission.Text=enabled?codex.Missions.SummaryAt(DateTimeOffset.UtcNow):"CODEX désactivé";
  }
  if(homeProjects is not null){
   homeProjects.Children.Clear();var state=projects.Store.Snapshot;
   var all=J.A(state,"projects").OfType<System.Text.Json.Nodes.JsonObject>().Where(p=>!Archived(p)).OrderByDescending(p=>J.B(C(p),"pinned")).ThenBy(Order).Take(3).ToArray();
   foreach(var p in all){
    var d=J.A(state,"derived").OfType<System.Text.Json.Nodes.JsonObject>().FirstOrDefault(d=>J.S(d,"projectId")==J.S(p,"id"));
    var title=Text((J.B(C(p),"pinned")?"★ ":"")+J.S(p,"name")+"  •  "+J.S(d,"status","UNSYNCED"),14);title.FontWeight=FontWeights.SemiBold;homeProjects.Children.Add(title);
    if(J.S(d,"nextAction")!="")homeProjects.Children.Add(Text(J.S(d,"nextAction"),12));
   }
   if(all.Length==0)homeProjects.Children.Add(Text("Aucun projet importé. Import et gestion dans PROJECTS.",14));
  }
 }
}
