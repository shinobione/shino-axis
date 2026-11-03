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
 WrapPanel? homeQuotaHost;StackPanel? homeProjects;string? homeQuotaSignature;
 StackPanel? homeActivity;readonly Dictionary<string,HomeMissionRow> homeRows=[];
 sealed class HomeMissionRow {public MissionGlyph Glyph=new();public TextBlock Title=new(){FontSize=14,FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis};public TextBlock Details=new(){FontSize=11,Foreground=QuotaDial.Ink("#A4B5CB"),TextTrimming=TextTrimming.CharacterEllipsis};public TextBlock Time=new(){FontSize=14,FontWeight=FontWeights.SemiBold};public Grid Layout=new();public DateTimeOffset? FadeEnd;}
 void Metrics(params (string Title,string Key)[] metrics){
  var row=new WrapPanel{Margin=new(0,0,0,4)};
  foreach(var m in metrics){
   var stack=new StackPanel();stack.Children.Add(Text(m.Title,12));
   var value=Text("Indisponible",25);value.FontWeight=FontWeights.SemiBold;values[m.Key]=value;metricKeys.Add(m.Key);value.TextWrapping=TextWrapping.NoWrap;stack.Children.Add(new Viewbox{Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Left,Height=34,Child=value});
   var tile=PanelCard(stack);tile.Width=144;tile.MinHeight=88;tile.BorderBrush=QuotaDial.Ink("#293C53");tile.BorderThickness=new(1);tile.ToolTip="Mesure locale — disponibilité et fraîcheur dans les modules";row.Children.Add(tile);
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
  var attention=new StackPanel();Section(attention,"À votre attention");
  attentionText=Text("",13);attentionText.Foreground=QuotaDial.Ink("#FFBB9E");attention.Children.Add(attentionText);
  homeRows.Clear();homeActivity=new StackPanel{Visibility=Visibility.Collapsed,Margin=new(0,0,0,4)};attention.Children.Add(homeActivity);
  PageContent.Children.Add(attention);
  Metrics(("CPU","pc.cpu"),("GPU","pc.gpu"),("RAM utilisée","pc.ram"),("Temp. GPU","pc.temperature"),("↓ Réseau","network.download"),("↑ Réseau","network.upload"));
  var mediaArea=new StackPanel();Music(mediaArea);
  var summary=new StackPanel();Section(summary,"CODEX • quotas");
  homeQuotaHost=new WrapPanel();summary.Children.Add(homeQuotaHost);summary.Children.Add(Link("Ouvrir CODEX →","CODEX"));
  PageContent.Children.Add(Columns(mediaArea,PanelCard(summary)));
  var sources=new StackPanel();Section(sources,"Sources & événements");
  healthText=Text("",12);sources.Children.Add(healthText);
  var events=Text("",12);values["summary.events"]=events;sources.Children.Add(events);
  var links=new WrapPanel();links.Children.Add(Link("Modules & écosystème →","Modules"));links.Children.Add(Link("Réseau →","NETWORK"));links.Children.Add(Link("Média →","MEDIA"));links.Children.Add(Link("PROJECTS →","PROJECTS"));sources.Children.Add(links);
  PageContent.Children.Add(PanelCard(sources));
 }
 void UpdateHomeActivity(bool enabled){
  if(homeActivity is null)return;
  var now=DateTimeOffset.UtcNow;bool fresh=enabled&&codex.Missions.IsFresh(now);
  var eligible=codex.Missions.Current.Recent.Where(m=>!m.Subagent).Where(m=>{
   var state=m.StatusAt(now,fresh);if(state=="EN COURS")return true;
   return enabled&&(state is "TERMINÉ" or "ÉCHEC" or "INTERROMPU")&&m.Finished is {} end&&now>=end&&now-end<TimeSpan.FromSeconds(60);
  }).OrderBy(m=>m.StatusAt(now,fresh)=="EN COURS"?0:1).ThenByDescending(m=>m.Finished??m.Started).ToArray();
  var shown=eligible.Take(4).ToArray();var keys=shown.Select(m=>m.Identity).ToHashSet();
  foreach(var key in homeRows.Keys.Where(k=>!keys.Contains(k)).ToArray()){homeRows[key].Glyph.SetState("À VÉRIFIER");homeActivity.Children.Remove(homeRows[key].Layout);homeRows.Remove(key);}
  string signature=string.Join("|",shown.Select(m=>m.Identity))+":"+eligible.Length;bool rebuild=!Equals(homeActivity.Tag,signature);
  if(rebuild){homeActivity.Children.Clear();homeActivity.Tag=signature;}
  if(shown.Length==0){homeActivity.Visibility=Visibility.Collapsed;return;}
  homeActivity.Visibility=Visibility.Visible;if(rebuild){var heading=Text("ACTIVITÉ CODEX",12);heading.Foreground=QuotaDial.Ink("#A4B5CB");homeActivity.Children.Add(heading);}
  foreach(var m in shown){
   if(!homeRows.TryGetValue(m.Identity,out var row)){
    row=new();row.Layout.ColumnDefinitions.Add(new(){Width=new GridLength(36)});row.Layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.Layout.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.Layout.Margin=new(0,2,0,2);row.Layout.Children.Add(row.Glyph);
    var info=new StackPanel();info.Children.Add(row.Title);info.Children.Add(row.Details);Grid.SetColumn(info,1);row.Layout.Children.Add(info);Grid.SetColumn(row.Time,2);row.Time.VerticalAlignment=VerticalAlignment.Center;row.Layout.Children.Add(row.Time);homeRows.Add(m.Identity,row);
   }
   var status=m.StatusAt(now,fresh);row.Glyph.SetState(status);SetText(row.Title,Supplied(m.Workspace)?m.Workspace:"Tour Codex");
   SetText(row.Details,string.Join(" · ",new[]{status,Supplied(m.Model)?m.Model:null,Supplied(m.Effort)?m.Effort:null}.Where(x=>x is not null)));SetText(row.Time,m.DurationAt(now,fresh));
   if(row.FadeEnd!=m.Finished){row.FadeEnd=m.Finished;row.Layout.BeginAnimation(UIElement.OpacityProperty,null);row.Layout.Opacity=1;
    if(settings.AnimationsEnabled&&m.Finished is {} finish){var left=TimeSpan.FromSeconds(60)-(now-finish);var fade=new System.Windows.Media.Animation.DoubleAnimation(1,0,TimeSpan.FromMilliseconds(500)){BeginTime=left>TimeSpan.FromMilliseconds(500)?left-TimeSpan.FromMilliseconds(500):TimeSpan.Zero};row.Layout.BeginAnimation(UIElement.OpacityProperty,fade);}}
   if(rebuild)homeActivity.Children.Add(row.Layout);
  }
  if(rebuild&&eligible.Length>4){var more=new Button{Content=$"+{eligible.Length-4} autres →",HorizontalAlignment=HorizontalAlignment.Left,Padding=new(8,3,8,3)};more.Click+=(_,_)=>Navigate("CODEX");homeActivity.Children.Add(more);}
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
   var entry=runtime.Modules.Single(m=>m.Id=="codex");bool enabled=entry.State==Axis.Core.ModuleState.Active;
   var windows=codex.Snapshot?.Windows.Where(w=>w.LimitId=="codex").Take(2).ToArray()??[];
   string signature=string.Join("|",windows.Select(w=>w.Minutes))+":"+enabled;
   if(signature!=homeQuotaSignature){
    homeQuotaSignature=signature;homeQuotaHost.Children.Clear();
    foreach(var w in windows){
     var stack=new StackPanel{Width=112};var dial=new QuotaDial{Width=96,Height=96};stack.Children.Add(dial);
     var label=Text(w.Label,12);label.TextAlignment=TextAlignment.Center;stack.Children.Add(label);homeQuotaHost.Children.Add(stack);
    }
    if(windows.Length==0)homeQuotaHost.Children.Add(Text("Quotas indisponibles",14));
    var credit=new StackPanel{Margin=new(14,12,0,0)};credit.Children.Add(Text("—",30));credit.Children.Add(Text("crédits de reset",12));homeQuotaHost.Children.Add(credit);
   }
   int quotaIndex=0;
   foreach(var tile in homeQuotaHost.Children.OfType<StackPanel>()){
    if(tile.Children[0] is QuotaDial dial){var w=windows[quotaIndex++];dial.SetReading(enabled?w.Remaining:null,QuotaFresh());}
    else if(tile.Children[0] is TextBlock credit)SetText(credit,enabled&&codex.Snapshot?.ResetCredits is {} n?n.ToString():"—");
   }
   UpdateHomeActivity(enabled);
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
