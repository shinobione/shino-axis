using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Axis.Core;
using Axis.Core.Presentation;
using Axis.App.Controls;
namespace Axis.App;
public partial class MainWindow {
 TextBlock? attentionText,healthText,externalText;
 IReadOnlyList<ExternalPresence> external=[];DateTimeOffset nextExternal;
 async Task ObserveExternal(){
  var now=DateTimeOffset.UtcNow;if(now<nextExternal)return;nextExternal=now.AddSeconds(30);
  external=await Task.Run(()=>ExternalComponents.Observe(name=>{var processes=Process.GetProcessesByName(name);try{return processes.Length>0;}finally{foreach(var process in processes)process.Dispose();}},now));
  if(IsVisible&&!closing)UpdateCockpit();
 }
 Button Link(string label,string target){var button=new Button{Content=label,Padding=new(10,6,10,6),Margin=new(0,4,8,4)};button.Click+=(_,_)=>Navigate(target);return button;}
 void Section(Panel parent,string heading){var title=Text(heading,17);title.FontWeight=FontWeights.SemiBold;title.Margin=new(0,8,0,8);parent.Children.Add(title);}
 CheckBox Preference(Panel panel,string title,bool selected,Func<bool,Task> apply){
  var box=new CheckBox{Content=title,IsChecked=selected,FontSize=14,Margin=new(0,7,0,7)};
  box.Click+=async(_,_)=>{box.IsEnabled=false;try{await apply(box.IsChecked==true);}catch(Exception e){Notice.Text="Opération indisponible ("+e.GetType().Name+").";box.IsChecked=!box.IsChecked;}finally{box.IsEnabled=true;}};
  panel.Children.Add(box);return box;
 }
 void RenderModules(){
  PageContent.Children.Add(Text("AXIS collecte ; les sources externes gardent leur propre cycle de vie.",13));
  foreach(var m in runtime.Modules){
   var stack=new StackPanel();var box=Preference(stack,m.Name,settings.EnabledModules.GetValueOrDefault(m.Id,true),async enabled=>{
    if(closing)return;
    settings.EnabledModules[m.Id]=enabled;var saved=Save();
    await Task.Run(()=>runtime.SetEnabledAsync(m.Id,enabled,DateTimeOffset.UtcNow));await saved;if(!closing)Update();
   });controls[m.Id]=box;
   stack.Children.Add(Text(m.Description,13));var state=Text("",13);values[m.Id+".state"]=state;stack.Children.Add(state);
   stack.Children.Add(Text(m.Id=="codex"?"Missions : observation et fraîcheur distinctes des quotas. Cadence dans Paramètres.":"La désactivation arrête uniquement ce module dans AXIS.",12));
   stack.Children.Add(Link("Ouvrir les détails →",m.Id switch{"pc"=>"PC","network"=>"NETWORK","media"=>"MEDIA","codex"=>"CODEX",_=>"PROJECTS"}));
   PageContent.Children.Add(PanelCard(stack));
  }
  Section(PageContent,"Écosystème SHINO • observation légère");externalText=Text("",13);PageContent.Children.Add(PanelCard(externalText));
 }
 void RenderSettings(){
  var application=new StackPanel();Section(application,"Application Windows");
  bool actual=false,obsolete=false;try{actual=startup.Registered;obsolete=actual&&!startup.Enabled;}catch{ }
  var autostart=Preference(application,"Démarrer AXIS avec Windows",actual,async enabled=>{
   if(isolated)throw new InvalidOperationException();startup.Apply(enabled);settings.StartWithWindows=enabled;await Save();
  });autostart.IsEnabled=!isolated;autostart.ToolTip=isolated?"Indisponible dans un profil isolé ; aucun démarrage réel modifié.":"Entrée dédiée par utilisateur, sans droits administrateur. L'exécutable doit rester à son emplacement.";
  if(obsolete)application.Children.Add(Text("Une entrée AXIS vise un autre emplacement. Décochez pour la retirer ; recochez pour utiliser cet exécutable.",12));
  Preference(application,"Masquer lors de la minimisation",settings.MinimizeToTray,async enabled=>{settings.MinimizeToTray=enabled;await Save();});
  Preference(application,"Fermer la fenêtre masque AXIS dans le tray",settings.CloseToTray,async enabled=>{settings.CloseToTray=enabled;await Save();});
  application.Children.Add(Text("Quitter AXIS dans le menu ou le tray ferme toujours l'application et ses collecteurs.",12));
  Preference(application,"Rouvrir la dernière page",settings.RestoreLastPage,async enabled=>{settings.RestoreLastPage=enabled;settings.LastPage=page;await Save();});
  PageContent.Children.Add(PanelCard(application));
  var modules=new StackPanel();Section(modules,"Modules & actualisation");modules.Children.Add(Link("Gérer les cinq modules →","Modules"));
  var cadence=new DockPanel();cadence.Children.Add(Text("Quotas Codex · secondes",13));
  var select=new ComboBox{ItemsSource=new[]{30,60,120,300},SelectedItem=settings.CodexRefreshSeconds,Width=110,HorizontalAlignment=HorizontalAlignment.Right};
  select.SelectionChanged+=async(_,_)=>{if(select.SelectedItem is int seconds){settings.CodexRefreshSeconds=seconds;codex.RefreshSeconds=seconds;await Save();}};
  cadence.Children.Add(select);modules.Children.Add(cadence);modules.Children.Add(Text("Missions : 5 s nominales · PC : 2 s · Réseau et média : 1 s. Cadences existantes conservées.",12));PageContent.Children.Add(PanelCard(modules));
  var visual=new StackPanel();Section(visual,"Interface & notifications");
  Preference(visual,"Animations des missions",settings.AnimationsEnabled,async enabled=>{settings.AnimationsEnabled=enabled;ApplyAnimations();await Save();});
  Preference(visual,"Notifier les fins de tours confirmées",settings.NotifyMissionCompletion,async enabled=>{settings.NotifyMissionCompletion=enabled;await Save();});PageContent.Children.Add(PanelCard(visual));
  var diagnostics=new StackPanel();Section(diagnostics,"Diagnostics");diagnostics.Children.Add(Text("AXIS "+typeof(MainWindow).Assembly.GetName().Version+" · .NET "+Environment.Version+" · Windows x64",13));
  healthText=Text("",12);diagnostics.Children.Add(healthText);diagnostics.Children.Add(Link("Modules et sources →","Modules"));PageContent.Children.Add(PanelCard(diagnostics));
 }
 void UpdateCockpit(){
  var now=DateTimeOffset.UtcNow;
  if(healthText is not null)SetText(healthText,string.Join(Environment.NewLine,runtime.Modules.Select(m=>m.Name+" — "+Describe(m))));
  if(attentionText is not null){
   var issues=runtime.Modules.Select(m=>(Entry:m,Health:CockpitStatus.Read(m,now))).Where(x=>x.Health.Attention).ToArray();
   string summary=string.Join(Environment.NewLine,issues.Select(x=>x.Entry.Name+" · "+x.Health.State+" — "+x.Health.Detail));
   SetText(attentionText,summary);attentionText.Visibility=issues.Length>0?Visibility.Visible:Visibility.Collapsed;
  }
  if(externalText is not null)SetText(externalText,external.Count==0?"Présence inconnue · première observation en attente":string.Join("\n\n",external.Select(x=>x.Name+" — "+x.State+(now-x.Observed>TimeSpan.FromSeconds(65)?" · observation périmée":"")+"\n"+x.Source+"\nObservé : "+x.Observed.ToLocalTime().ToString("dd/MM HH:mm:ss"))));
 }
}
