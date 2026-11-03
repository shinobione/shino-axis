using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Axis.App.Controls;
using Axis.Core.Missions;
namespace Axis.App;
public partial class MainWindow {
 StackPanel? missionPanel,recentMissionPanel;TextBlock? missionSummary,missionFooter;
 readonly Dictionary<string,MissionCardView> missionCards=[];
 sealed class MissionCardView {
  internal readonly MissionGlyph Glyph=new();
  internal readonly TextBlock Heading=new(){FontSize=16,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
  internal readonly TextBlock State=new(){FontSize=13,FontWeight=FontWeights.Bold};
  internal readonly TextBlock Duration=new(){FontSize=21,FontWeight=FontWeights.SemiBold};
  internal readonly TextBlock Details=new(){FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=QuotaDial.Ink("#A4B5CB")};
  internal Border Card=null!;
 }
 void RenderMissions(){
  missionCards.Clear();
  PageContent.Children.Add(Text("Missions • tours observés",20));
  missionSummary=Text("",13);PageContent.Children.Add(missionSummary);
  missionPanel=new();PageContent.Children.Add(missionPanel);
  PageContent.Children.Add(Text("Événements récents • tours locaux",14));recentMissionPanel=new();PageContent.Children.Add(recentMissionPanel);
  missionFooter=Text("",12);PageContent.Children.Add(missionFooter);
 }
 static bool Supplied(string value)=>!string.IsNullOrWhiteSpace(value)&&value is not ("Not provided" or "Project not provided" or "Indisponible");
 void UpdateMissions(bool enabled){
  if(missionPanel is null)return;
  var snapshot=codex.Missions.Current;var now=DateTimeOffset.UtcNow;
  bool fresh=enabled&&codex.Missions.IsFresh(now);
  if(missionSummary is not null)missionSummary.Text=!enabled?"NON VÉRIFIÉ • module désactivé":fresh?snapshot.Summary:"NON VÉRIFIÉ • observation périmée";
  var all=snapshot.Recent.Where(m=>!m.Subagent).ToArray();
  bool Held(MissionObservation m)=>fresh&&m.Finished is {} end&&now-end<TimeSpan.FromSeconds(8)&&now>=end.AddSeconds(-5);
  bool Foreground(MissionObservation m)=>m.StatusAt(now,fresh)=="EN COURS"||Held(m);
  var roots=all.Where(Foreground).Concat(all.Where(m=>!Foreground(m)).Take(6)).ToArray();
  var keys=roots.Select(m=>m.Identity).ToHashSet();
  foreach(var stale in missionCards.Keys.Where(k=>!keys.Contains(k)).ToArray()){missionCards[stale].Glyph.SetState("À VÉRIFIER");missionPanel.Children.Remove(missionCards[stale].Card);recentMissionPanel?.Children.Remove(missionCards[stale].Card);missionCards.Remove(stale);}
  int position=0,recentPosition=0;
  foreach(var m in roots){
   if(!missionCards.TryGetValue(m.Identity,out var view)){
    view=new();var layout=new Grid();
    layout.ColumnDefinitions.Add(new(){Width=new GridLength(42)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});layout.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
    Grid.SetColumn(view.Glyph,0);layout.Children.Add(view.Glyph);
    var text=new StackPanel{Margin=new(0,0,18,0)};text.Children.Add(view.Heading);text.Children.Add(view.State);text.Children.Add(view.Details);Grid.SetColumn(text,1);layout.Children.Add(text);
    Grid.SetColumn(view.Duration,2);view.Duration.VerticalAlignment=VerticalAlignment.Center;layout.Children.Add(view.Duration);
    view.Card=PanelCard(layout);view.Card.ToolTip="Observation locale JSONL, pas la conversation sélectionnée dans Desktop. Une fin de tour ne termine pas le projet.";
    missionCards[m.Identity]=view;
   }
   string status=m.StatusAt(now,fresh);
   SetText(view.Heading,(Supplied(m.Workspace)?m.Workspace+"  ·  ":"")+"Tour Codex"+(string.IsNullOrEmpty(m.Thread)?"":" · "+m.Thread[..Math.Min(8,m.Thread.Length)]));
   if(view.State.Text!=status){SetText(view.State,status);
   view.State.Foreground=QuotaDial.Ink(status switch{"EN COURS"=>"#A477FF","TERMINÉ"=>"#51D7AF","ÉCHEC"=>"#EE4B55","INTERROMPU"=>"#F28C38",_=>"#8BA0B1"});
   view.Card.BorderBrush=status=="EN COURS"?QuotaDial.Ink("#69518F"):QuotaDial.Ink("#2B384D");view.Card.BorderThickness=new(1);}
   SetText(view.Duration,m.DurationAt(now,fresh));view.Duration.ToolTip="Durée observée depuis le début du tour";view.Glyph.SetState(status);
   var metadata=new List<string>();if(Supplied(m.Model))metadata.Add(m.Model);if(Supplied(m.Effort))metadata.Add(m.Effort);metadata.Add(m.Started.ToLocalTime().ToString("dd/MM HH:mm:ss"));
   if(m.LastActivity is {} activity)metadata.Add("Activité : "+activity.ToLocalTime().ToString("HH:mm:ss"));
   SetText(view.Details,string.Join("  ·  ",metadata));
   var target=Foreground(m)?missionPanel:recentMissionPanel!;int slot=Foreground(m)?position++:recentPosition++;
   view.Duration.FontSize=Foreground(m)?21:16;view.Card.Padding=new(Foreground(m)?14:8);
   if(target.Children.IndexOf(view.Card)!=slot){missionPanel.Children.Remove(view.Card);recentMissionPanel?.Children.Remove(view.Card);target.Children.Insert(slot,view.Card);}
  }
  if(missionFooter is not null)missionFooter.Text=(!fresh||roots.Length==0?"Aucun tour racine actuel vérifiable. ":"")+(snapshot.ChildCount>0?$"{snapshot.ChildCount} sous-agents séparés. ":"")+(snapshot.Limited?"Couverture partielle. ":"")+"Source : JSONL local non documenté • relevé : "+(snapshot.Checked==DateTimeOffset.MinValue?"indisponible":snapshot.Checked.ToLocalTime().ToString("HH:mm:ss"))+" • conversation sélectionnée non attestée.";
 }
}
