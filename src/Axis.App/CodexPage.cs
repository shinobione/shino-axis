using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Axis.Core;
using Axis.Core.Modules;
using Axis.Core.Presentation;
using Axis.App.Controls;
namespace Axis.App;
public partial class MainWindow {
 WrapPanel? quotaCards;Canvas? chart;Button? refresh;QuotaSnapshot? plotted;TextBlock? codexConnection,historyInfo;
 int historyDays=1;long chartMinute;
 readonly System.Collections.Generic.Dictionary<string,QuotaCardView> liveQuotaCards=[];
 sealed class QuotaCardView {
  internal readonly QuotaDial Dial=new();internal readonly TextBlock Name=new(){FontSize=16,FontWeight=FontWeights.SemiBold};
  internal readonly TextBlock Used=new(){FontSize=12,Margin=new(0,6,0,6)},Reset=new(){FontSize=14},Countdown=new(){FontSize=12};internal Border Card=null!;
 }
 TextBlock? creditAmount,creditStatus,creditExpiry;
 static void SetText(TextBlock label,string value){if(label.Text!=value)label.Text=value;}
 bool QuotaFresh(){
  return runtime.Modules.Single(m=>m.Id=="codex").State==ModuleState.Active&&codex.IsFresh(DateTimeOffset.UtcNow);
 }
 void RenderCodex(){
  var toolbar=new DockPanel{Margin=new(0,0,0,12)};
  refresh=new Button{Content="↻ Actualiser",Padding=new(12,8,12,8),HorizontalAlignment=HorizontalAlignment.Left};
  refresh.Click+=(_,_)=>{codex.RequestRefresh();refresh.IsEnabled=false;};
  DockPanel.SetDock(refresh,Dock.Right);toolbar.Children.Add(refresh);
  var cadence=new ComboBox{ItemsSource=new[]{30,60,120,300},SelectedItem=codex.RefreshSeconds,Width=80,Margin=new(10,0,12,0),ToolTip="Intervalle des quotas en secondes"};
  cadence.SelectionChanged+=async(_,_)=>{if(cadence.SelectedItem is int seconds){codex.RefreshSeconds=seconds;settings.CodexRefreshSeconds=seconds;await Save();}};
  DockPanel.SetDock(cadence,Dock.Right);toolbar.Children.Add(cadence);
  codexConnection=Text("",13);toolbar.Children.Add(codexConnection);PageContent.Children.Add(toolbar);
  liveQuotaCards.Clear();quotaCards=new WrapPanel();PageContent.Children.Add(quotaCards);
  RenderMissions();
  var chartArea=new StackPanel();var chartToolbar=new DockPanel();
  var selector=new ComboBox{ItemsSource=new[]{"Jour","Semaine","Mois"},SelectedIndex=historyDays==1?0:historyDays==7?1:2,Width=140,HorizontalAlignment=HorizontalAlignment.Right};
  selector.SelectionChanged+=(_,_)=>{historyDays=selector.SelectedIndex switch{1=>7,2=>30,_=>1};DrawHistory();};
  DockPanel.SetDock(selector,Dock.Right);chartToolbar.Children.Add(selector);
  historyInfo=Text("Historique • quota restant",16);chartToolbar.Children.Add(historyInfo);chartArea.Children.Add(chartToolbar);
  chart=new(){Height=244,Background=QuotaDial.Ink("#131D2C"),ClipToBounds=true,Margin=new(0,10,0,0)};
  chart.SizeChanged+=(_,_)=>DrawHistory();chartArea.Children.Add(chart);
  chartArea.Children.Add(Text("● 5 h (menthe)   ● Hebdomadaire (or)  •  bandes = sans relevé  •  pointillés = rupture de fenêtre ou mesure",12));
  PageContent.Children.Add(PanelCard(chartArea));
  plotted=null;chartMinute=-1;
 }
 QuotaCardView QuotaTile(string key,string label){
  var view=new QuotaCardView();var dock=new DockPanel();DockPanel.SetDock(view.Dial,Dock.Left);dock.Children.Add(view.Dial);
  var detail=new StackPanel{Width=178,Margin=new(10,4,0,0)};SetText(view.Name,label);
  detail.Children.Add(view.Name);detail.Children.Add(view.Used);detail.Children.Add(view.Reset);detail.Children.Add(view.Countdown);dock.Children.Add(detail);
  view.Card=PanelCard(dock);view.Card.Width=340;view.Card.MinHeight=152;liveQuotaCards.Add(key,view);quotaCards!.Children.Add(view.Card);return view;
 }
 void UpdateCodex(){
  if(quotaCards is null)return;
  bool enabled=runtime.Modules.Single(m=>m.Id=="codex").State==ModuleState.Active;
  if(refresh is not null)refresh.IsEnabled=enabled&&!codex.IsReading;
  if(codexConnection is not null)codexConnection.Text=!enabled?"CODEX désactivé":$"{(codex.Error is not null?"Lecture indisponible • relevé ancien":codex.IsReading?"Actualisation…":QuotaFresh()?"Connecté":"Données anciennes ou indisponibles")}  ·  account/rateLimits/read\nDernier relevé : {codex.Snapshot?.Timestamp.ToLocalTime().ToString("dd/MM HH:mm:ss")??"jamais"}";
  var snap=codex.Snapshot;bool fresh=QuotaFresh();var windows=(snap?.Windows??[]).ToList();
  foreach(int minutes in new[]{300,10080})if(!windows.Any(w=>w.LimitId=="codex"&&w.Minutes==minutes))windows.Add(new("codex",minutes,double.NaN,null));
  var keys=windows.Select(w=>w.LimitId+":"+w.Minutes).ToHashSet();
  foreach(var key in liveQuotaCards.Keys.Where(k=>!keys.Contains(k)).ToArray()){quotaCards.Children.Remove(liveQuotaCards[key].Card);liveQuotaCards.Remove(key);}
  int position=0;
  foreach(var w in windows.OrderBy(w=>w.LimitId=="codex"?0:1).ThenBy(w=>w.LimitId).ThenBy(w=>w.Minutes)){
   string key=w.LimitId+":"+w.Minutes;var view=liveQuotaCards.TryGetValue(key,out var existing)?existing:QuotaTile(key,w.LimitId.ToUpperInvariant()+" • "+w.Label);
   bool available=enabled&&double.IsFinite(w.Used);view.Dial.SetReading(available?w.Remaining:null,fresh);
   SetText(view.Used,available?$"{w.Used:0.#} % utilisé • {(fresh?"récent":"ancien")}":"Indisponible");
   SetText(view.Reset,enabled&&w.Reset is {} reset?reset.ToLocalTime().ToString("dd MMM · HH:mm zzz"):"Reset non fourni");
   var left=w.Reset-DateTimeOffset.UtcNow;
   SetText(view.Countdown,enabled&&left is {} t?t>TimeSpan.Zero?$"dans {(int)t.TotalDays} j {t.Hours} h {t.Minutes} min":"Date atteinte · attente source":"");
   if(quotaCards.Children.IndexOf(view.Card)!=position){quotaCards.Children.Remove(view.Card);quotaCards.Children.Insert(position,view.Card);}position++;
  }
  if(creditAmount is null||!quotaCards.Children.OfType<Border>().Any(c=>Equals(c.Tag,"credits"))){
   var credit=new StackPanel();credit.Children.Add(Text("CRÉDITS DE RESET",12));creditAmount=Text("",36);creditAmount.FontWeight=FontWeights.Bold;credit.Children.Add(creditAmount);
   creditStatus=Text("",13);creditExpiry=Text("",12);credit.Children.Add(creditStatus);credit.Children.Add(creditExpiry);
   var card=PanelCard(credit);card.Tag="credits";card.Width=206;card.MinHeight=152;quotaCards.Children.Add(card);
  }
  SetText(creditAmount,enabled&&snap?.ResetCredits is {} count?count.ToString():"—");
  creditAmount.Foreground=QuotaDial.Ink(!enabled||snap?.ResetCredits is null?"#8BA0B1":snap.ResetCredits>0?"#51D7AF":"#F28C38");
  SetText(creditStatus!,!enabled||snap?.ResetCredits is null?"Non fournis":snap.ResetCredits==0?"Aucun crédit disponible":"Disponibles");
  SetText(creditExpiry!,enabled&&snap?.ResetCreditsExpiry is {} expiry?"Expire : "+expiry.ToLocalTime().ToString("dd/MM HH:mm zzz"):"Aucune action automatique");
  UpdateMissions(enabled);
  long minute=DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60;
  if(!ReferenceEquals(plotted,snap)||minute!=chartMinute){plotted=snap;chartMinute=minute;DrawHistory();}
 }
 void DrawHistory(){
  if(chart is null)return;chart.Children.Clear();var now=DateTimeOffset.UtcNow;var begin=now.AddDays(-historyDays);
  var samples=DisplayRules.FilterHistory(codex.History.Samples,now,historyDays);
  if(historyInfo is not null)historyInfo.Text=$"Historique • {historyDays switch{1=>"24 heures",7=>"7 jours",_=>"30 jours"}} • {samples.Count(s=>s.Windows.Count>0)} relevés";
  double width=Math.Max(280,chart.ActualWidth),left=40,right=18,top=16,height=176;
  double X(DateTimeOffset t)=>left+(width-left-right)*(t-begin).TotalSeconds/(now-begin).TotalSeconds;
  double Y(double value)=>top+height*(1-value/100);
  void Label(string text,double x,double y,Brush? color=null){var label=Text(text,11);label.Foreground=color??QuotaDial.Ink("#A4B5CB");Canvas.SetLeft(label,x);Canvas.SetTop(label,y);chart.Children.Add(label);}
  foreach(int value in new[]{0,25,50,75,100}){
   chart.Children.Add(new Line{X1=left,Y1=Y(value),X2=width-right,Y2=Y(value),Stroke=QuotaDial.Ink("#29374D"),StrokeThickness=1});Label(value+" %",1,Y(value)-8);
  }
  for(int i=0;i<=4;i++){var t=begin+(now-begin)*((double)i/4);string format=historyDays==1?"HH:mm":"dd/MM";Label(t.ToLocalTime().ToString(format),Math.Clamp(X(t)-20,left,width-52),top+height+8);}
  var valid=samples.Where(s=>s.Windows.Count>0).ToArray();
  DateTimeOffset last=begin;
  foreach(var s in valid.Append(new QuotaSnapshot(now,[],null))){
   if(s.Timestamp-last>TimeSpan.FromMinutes(11)){
    var shade=new Rectangle{Width=Math.Max(1,X(s.Timestamp)-X(last)),Height=height,Fill=new SolidColorBrush(Color.FromArgb(20,164,119,255)),IsHitTestVisible=false};
    Canvas.SetLeft(shade,X(last));Canvas.SetTop(shade,top);chart.Children.Insert(0,shade);
   }last=s.Timestamp;
  }
  if(valid.Length==0)Label("Aucun relevé dans cette période — aucune courbe fabriquée.",left+14,top+height/2);
  foreach(var series in new[]{(Minutes:300,Color:"#51D7AF"),(Minutes:10080,Color:"#E5B64D")}){
   QuotaSnapshot? previous=null;Point? prior=null;
   foreach(var s in samples){
    var w=s.Windows.FirstOrDefault(w=>w.LimitId=="codex"&&w.Minutes==series.Minutes);
    if(w is null){previous=null;prior=null;continue;}
    var point=new Point(X(s.Timestamp),Y(w.Remaining));var brush=QuotaDial.Ink(series.Color);
    if(previous is not null&&prior is {} p){
     if(QuotaHistory.Connect(previous,s,"codex",series.Minutes))chart.Children.Add(new Line{X1=p.X,Y1=p.Y,X2=point.X,Y2=point.Y,Stroke=brush,StrokeThickness=2});
     else chart.Children.Add(new Line{X1=point.X,Y1=top,X2=point.X,Y2=top+height,Stroke=QuotaDial.Ink("#B98583"),StrokeDashArray=new(){2,4},StrokeThickness=1,ToolTip="Rupture : fenêtre/reset source modifié, observation manquante ou consommation en baisse."});
    }
    var dot=new Ellipse{Width=7,Height=7,Fill=brush,ToolTip=$"{w.Label} • {s.Timestamp.ToLocalTime():dd/MM/yyyy HH:mm:ss zzz} • {w.Remaining:0.#} % restant"};
    Canvas.SetLeft(dot,point.X-3.5);Canvas.SetTop(dot,point.Y-3.5);chart.Children.Add(dot);previous=s;prior=point;
   }
  }
 }
}
