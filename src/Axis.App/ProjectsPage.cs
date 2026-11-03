using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Axis.Core.Projects;
namespace Axis.App;
public partial class MainWindow {
 TextBlock? projectsHealth;
 bool refreshingProjects;StackPanel? discoveredPanel;ListBox? projectList;StackPanel? projectDetail;TextBox? projectSearch;ComboBox? projectFilter;
 ProjectImport? pendingImport;TextBlock? importSummary;Button? applyImport;string selectedProject="";string renderedProjects="";
 void ProjectsClick(object s,RoutedEventArgs e)=>Navigate("PROJECTS");
 void RenderProjects(){
  projectsHealth=Text("",13);PageContent.Children.Add(PanelCard(projectsHealth));
  var tools=new StackPanel();PageContent.Children.Add(new Expander{Header="Import CONTROL & options de collecte",Content=tools,Margin=new(0,0,12,10)});
  PageContent.Children.Add(Text($"Réception locale : 127.0.0.1:{projects.Port}. Port 4177 réservé à CONTROL jusqu’à votre bascule manuelle.",12));
  Card(PageContent,"Synchronisation GitHub","projects.sync");if(PageContent.Children[^1] is FrameworkElement syncCard){syncCard.MaxWidth=700;syncCard.HorizontalAlignment=HorizontalAlignment.Left;}
  var importPath=new TextBox{Margin=new(0,10,0,4),Text="",ToolTip="Chemin du fichier state.local.json de CONTROL"};
  tools.Children.Add(importPath);
  var preview=new Button{Content="Choisir un fichier CONTROL et prévisualiser"};
  preview.Click+=async(_,_)=>{
   var dialog=new Microsoft.Win32.OpenFileDialog{FileName=System.Environment.GetEnvironmentVariable("SHINO_CONTROL_STATE")??"state.local.json",Filter="État CONTROL JSON|*.json",CheckFileExists=true};
   if(dialog.ShowDialog()!=true)return;importPath.Text=dialog.FileName;
   try{pendingImport=await projects.Store.PreviewAsync(dialog.FileName);var r=pendingImport.Review!;
    importSummary!.Text=$"{pendingImport.Projects} projets • {pendingImport.Sources} sources • {pendingImport.Evidence} preuves\n{r.Created} créations • {r.Updated} mises à jour • {r.Conflicts} conflits conservant AXIS.\n"+
     string.Join(" · ",J.A(pendingImport.Data,"projects").OfType<JsonObject>().Select(p=>J.S(p,"name")))+
     (r.Conflicts>0?"\nChamps en conflit : "+string.Join(", ",r.ConflictFields.Take(12)):"")+
     "\nLes transcriptions et HQ sont exclus. La source CONTROL reste inchangée.";applyImport!.IsEnabled=true;}
   catch(Exception){pendingImport=null;importSummary!.Text="Prévisualisation impossible : fichier ou schéma invalide.";applyImport!.IsEnabled=false;}
  };tools.Children.Add(preview);
  importSummary=Text("Aucun fichier importé.",12);tools.Children.Add(importSummary);
  applyImport=new Button{Content="Importer cette prévisualisation dans AXIS",IsEnabled=false};
  applyImport.Click+=async(_,_)=>{if(pendingImport is null)return;if(MessageBox.Show($"Importer {pendingImport.Projects} projets depuis cette prévisualisation ? Les conflits conserveront les valeurs AXIS.","AXIS • Confirmer cet import",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;applyImport.IsEnabled=false;try{int added=await projects.Store.ImportAsync(pendingImport);var done=projects.Store.Snapshot["axisLastImport"];importSummary.Text=$"{J.S(done,"created")} créations • {J.S(done,"updated")} mises à jour • {J.S(done,"conflicts")} conflits conservés. Identités et relations préservées.";pendingImport=null;renderedProjects="";UpdateProjects();}catch(Exception){importSummary.Text="Import non appliqué : stockage indisponible.";}};tools.Children.Add(applyImport);
  var endpoint=new TextBox{Text=$"http://127.0.0.1:{projects.Port}/api/ingest/chatgpt-delta",IsReadOnly=true,ToolTip="Endpoint à configurer dans le collecteur après autorisation de bascule"};tools.Children.Add(endpoint);
  tools.Children.Add(Text("Bascule : importer et vérifier les données, autoriser la réception privée, configurer ci-dessous l’identité de l’extension, puis changer son seul endpoint après votre accord. Retour : endpoint CONTROL 127.0.0.1:4177. Aucun double envoi.",12));
  var extensionId=new TextBox{Text=J.S(projects.Store.Snapshot["axisProjects"],"extensionId"),ToolTip="Identifiant Chrome de l’extension installée (32 lettres a-p)",Margin=new(0,4,0,4)};tools.Children.Add(extensionId);
  var configure=new Button{Content="Autoriser cette extension sur le récepteur AXIS",HorizontalAlignment=HorizontalAlignment.Left};
  configure.Click+=async(_,_)=>{
   string id=extensionId.Text.Trim();if(id!=""&&!System.Text.RegularExpressions.Regex.IsMatch(id,"^[a-p]{32}$")){Notice.Text="Identifiant Chrome invalide (32 lettres a-p).";return;}
   await ProjectAction(async()=>{await projects.Store.MutateAsync(s=>{J.O(s,"axisProjects")["extensionId"]=id;return true;});await projects.RestartReceiverAsync();});};
  tools.Children.Add(configure);
  tools.Children.Add(Text("Les trois contrats delta, catchup-plan et catchup-report utilisent le même récepteur. Si CONTROL exige un Bearer, conserver SHINO_CONTROL_TOKEN dans l’environnement au lancement ; aucun secret n’est affiché ici.",12));
  var options=projects.Store.Snapshot["axisProjects"];
  var collect=new CheckBox{Content="Autoriser la réception de deltas ChatGPT privés depuis mon collecteur",IsChecked=J.B(options,"acceptChat")};
  collect.Click+=async(_,_)=>{bool enabled=collect.IsChecked==true;await ProjectAction(()=>projects.Store.MutateAsync(s=>{J.O(s,"axisProjects")["acceptChat"]=enabled;return true;}));};tools.Children.Add(collect);
  var auto=new CheckBox{Content="Synchroniser les dépôts GitHub toutes les 5 minutes",IsChecked=J.B(options,"autoGithub")};
  auto.Click+=async(_,_)=>{bool enabled=auto.IsChecked==true;await ProjectAction(()=>projects.Store.MutateAsync(s=>{J.O(s,"axisProjects")["autoGithub"]=enabled;return true;}));};tools.Children.Add(auto);
  var sync=new Button{Content="↻ Synchroniser GitHub",HorizontalAlignment=HorizontalAlignment.Left,Padding=new(12,8,12,8)};sync.Click+=(_,_)=>{projects.RequestSync();Notice.Text="Synchronisation GitHub demandée (module PROJECTS actif requis).";};PageContent.Children.Add(sync);
  projectSearch=new TextBox{Width=600,MaxWidth=900,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,10,0,8),ToolTip="Rechercher nom, groupe, tags ou état"};
  projectFilter=new ComboBox{Width=300,HorizontalAlignment=HorizontalAlignment.Left,Margin=new(0,0,0,8),ItemsSource=new[]{"Tous","ACTIVE","STABLE","BLOCKED","NEEDS TEST","UNSYNCED","Archivés","Conteneurs"},SelectedIndex=0};
  PageContent.Children.Add(projectSearch);PageContent.Children.Add(projectFilter);
  projectList=new ListBox{MaxHeight=430,MinHeight=72,VerticalAlignment=VerticalAlignment.Top};
  ScrollViewer.SetHorizontalScrollBarVisibility(projectList,ScrollBarVisibility.Disabled);
  projectDetail=new(){Margin=new(14,0,0,0)};PageContent.Children.Add(Columns(projectList,projectDetail,1000,.65));
  var rowTemplate=new DataTemplate();
  var rowStack=new FrameworkElementFactory(typeof(StackPanel));
  var rowName=new FrameworkElementFactory(typeof(TextBlock));rowName.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name"));rowName.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);rowName.SetValue(TextBlock.FontSizeProperty,15.0);rowStack.AppendChild(rowName);
  var rowStatus=new FrameworkElementFactory(typeof(TextBlock));rowStatus.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Status"));rowStatus.SetValue(TextBlock.ForegroundProperty,Axis.App.Controls.QuotaDial.Ink("#8ADFE9"));rowStatus.SetValue(TextBlock.FontSizeProperty,12.0);rowStack.AppendChild(rowStatus);
  var rowNext=new FrameworkElementFactory(typeof(TextBlock));rowNext.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Next"));rowNext.SetValue(TextBlock.FontSizeProperty,12.0);rowNext.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);rowStack.AppendChild(rowNext);rowTemplate.VisualTree=rowStack;projectList.ItemTemplate=rowTemplate;

  projectSearch.TextChanged+=(_,_)=>{renderedProjects="";UpdateProjects();};projectFilter.SelectionChanged+=(_,_)=>{renderedProjects="";UpdateProjects();};
  projectList.SelectionChanged+=(_,_)=>{if(refreshingProjects)return;if(projectList.SelectedItem is ProjectRow row){selectedProject=row.Id;ShowProject(row.Id);}};
  var create=new Button{Content="+ Projet local",HorizontalAlignment=HorizontalAlignment.Left,Padding=new(12,8,12,8)};create.Click+=async(_,_)=>{string id="project-"+Guid.NewGuid().ToString("N");await ProjectAction(()=>projects.Store.MutateAsync(s=>{J.A(s,"projects").Add(new JsonObject{["id"]=id,["name"]="Nouveau projet",["kind"]="APP"});ControlEngine.Derive(s,[id]);return true;}));selectedProject=id;renderedProjects="";UpdateProjects();ShowProject(id);};PageContent.Children.Add(create);discoveredPanel=new();PageContent.Children.Add(discoveredPanel);
  renderedProjects="";UpdateProjects();if(selectedProject!="")ShowProject(selectedProject);
 }
 static bool Tracked(JsonNode p)=>J.B(p,"trackState",true)&&J.S(p,"kind")!="CHATGPT_CONTAINER"&&J.S(p,"id")!="shino-codes";
 static JsonNode C(JsonNode p)=>p["control"]??p;
 static bool Archived(JsonNode p)=>J.B(C(p),"archived");
 static double Order(JsonNode p)=>double.TryParse(J.S(C(p),"order"),out var order)?order:double.MaxValue;
 sealed record ProjectRow(string Id,string Label,string Name,string Status,string Next);
 async System.Threading.Tasks.Task ProjectAction(Func<System.Threading.Tasks.Task> action){try{await action();Notice.Text="Modification enregistrée.";renderedProjects="";UpdateProjects();}catch(Exception){Notice.Text="Opération non appliquée. Vérifier le module et le stockage local.";}}
 void UpdateProjects(){
  if(projectList is null||page!="PROJECTS")return;
  var state=projects.Store.Snapshot;
  if(projectsHealth is not null){
   var ds=J.A(state,"derived").OfType<JsonObject>().ToArray();var cp=state["settings"]?["lastChatgptCatchup"];var receipt=state["settings"]?["lastChatgptCollector"];
   projectsHealth.Text= $"{J.A(state,"projects").OfType<JsonObject>().Count(Tracked)} projets suivis / {J.A(state,"projects").Count} identités • {ds.Count(d=>J.S(d,"status")=="ACTIVE")} actifs • {ds.Count(d=>J.S(d,"status") is "BLOCKED" or "NEEDS TEST")} à suivre • {ds.Count(d=>J.S(d,"status")=="STABLE")} stables\n"+
    $"Collecteur : {(projects.ReceiverReady?"récepteur prêt":"arrêté")} • réception {(J.B(state["axisProjects"],"acceptChat")?"autorisée":"désactivée")} • dernier delta reçu par AXIS : {projects.LastDeltaReceivedAt?.ToLocalTime().ToString("dd/MM HH:mm:ss")??"aucun depuis ce lancement"}\n"+
     $"Historique source (CONTROL inclus) : {J.S(cp,"status","non reçu")} • {J.S(cp,"at","—")} • {J.S(cp,"newCount","0")} nouvelles / {J.S(cp,"changedCount","0")} modifiées / {J.S(cp,"refreshedCount","0")} rafraîchies / {J.S(cp,"failedCount","0")} échecs / {J.S(cp,"deferredCount","0")} différées • {J.A(state,"discovered").Count} non classées";
  }
  string fingerprint=state.ToJsonString();if(fingerprint==renderedProjects)return;renderedProjects=fingerprint;
  string search=projectSearch?.Text.ToLowerInvariant()??"",filter=projectFilter?.SelectedItem?.ToString()??"Tous";
  var rows=J.A(state,"projects").OfType<JsonObject>().Select(p=>(P:p,D:J.A(state,"derived").OfType<JsonObject>().FirstOrDefault(d=>J.S(d,"projectId")==J.S(p,"id"))))
   .Where(x=>filter=="Conteneurs"?!Tracked(x.P):Tracked(x.P)&&(filter=="Archivés"?Archived(x.P):!Archived(x.P)&&(filter=="Tous"||J.S(x.D,"status")==filter)))
   .Where(x=>(J.S(x.P,"name")+" "+J.S(C(x.P),"group")+" "+C(x.P)["tags"]?.ToJsonString()+" "+J.S(x.D,"status")).ToLowerInvariant().Contains(search))
   .OrderByDescending(x=>J.B(C(x.P),"pinned")).ThenBy(x=>Order(x.P)).ThenBy(x=>J.S(x.P,"name"))
   .Select(x=>new ProjectRow(J.S(x.P,"id"),(J.B(C(x.P),"pinned")?"★ ":"")+J.S(x.P,"name")+" • "+J.S(x.D,"status","UNSYNCED"),(J.B(C(x.P),"pinned")?"★ ":"")+J.S(x.P,"name"),J.S(x.D,"status","UNSYNCED"),J.S(x.D,"nextAction"))).ToArray();
  refreshingProjects=true;projectList.ItemsSource=rows;projectList.SelectedItem=rows.FirstOrDefault(r=>r.Id==selectedProject);refreshingProjects=false;RenderDiscovered(state);
  if(projects.Store.Warning is {} warning)Notice.Text=warning;
 }
 void ShowProject(string id){
  if(projectDetail is null)return;projectDetail.Children.Clear();var state=projects.Store.Snapshot;var p=J.Find(state,"projects",id);if(p is null)return;
  var d=J.A(state,"derived").OfType<JsonObject>().FirstOrDefault(d=>J.S(d,"projectId")==id);
  var projectName=Text(J.S(p,"name"),22);projectName.FontWeight=FontWeights.Bold;projectDetail.Children.Add(projectName);projectDetail.Children.Add(Text("Identifiant : "+id,12));if(J.S(p,"description")!="")projectDetail.Children.Add(Text(J.S(p,"description"),13));projectDetail.Children.Add(Text(J.S(d,"status","UNSYNCED")+" • "+J.S(d,"statusReason"),13));
  projectDetail.Children.Add(Text(J.S(d,"summary")));projectDetail.Children.Add(Text("Reprise : "+J.S(d,"nextAction"),16));
  projectDetail.Children.Add(Text("Dernière activité : "+J.S(d,"lastMovementAt","inconnue")+" • "+J.S(d,"freshness")+" • Statut : "+J.S(d,"statusSource")+" • Reprise : "+J.S(d,"resumeSource"),12));
  projectDetail.Children.Add(Text("Preuves : "+d?["evidenceIds"]?.ToJsonString(),12));
  var editPanel=new StackPanel();projectDetail.Children.Add(new Expander{Header="Modifier le projet",Content=editPanel,Margin=new(0,10,0,8)});
  TextBox Field(string label,string value,Panel? target=null){var panel=target??editPanel;panel.Children.Add(Text(label,12));var box=new TextBox{Text=value,Margin=new(0,0,0,6)};panel.Children.Add(box);return box;}

  var name=Field("Nom",J.S(p,"name"));var repo=Field("Dépôt GitHub (owner/repo)",J.S(p,"repo"));var group=Field("Groupe",J.S(C(p),"group"));var tags=Field("Tags, séparés par virgules",string.Join(", ",(C(p)["tags"] as JsonArray??new()).Select(t=>t?.ToString())));var order=Field("Ordre numérique",J.S(C(p),"order"));var notes=Field("Notes privées locales",J.S(C(p),"note",J.S(p,"notes")));
  var status=new ComboBox{ItemsSource=new[]{"AUTO","ACTIVE","NEEDS TEST","BLOCKED","STABLE","WAITING","DONE","EMPTY","UNSYNCED"},SelectedItem=J.S(p["control"],"statusOverride","AUTO")};editPanel.Children.Add(status);
  var priority=new CheckBox{Content="Projet prioritaire",IsChecked=J.B(C(p),"pinned")};var archived=new CheckBox{Content="Archivé",IsChecked=Archived(p)};editPanel.Children.Add(priority);editPanel.Children.Add(archived);
  var save=new Button{Content="Enregistrer les modifications manuelles"};
  save.Click+=async(_,_)=>{if(string.IsNullOrWhiteSpace(name.Text)){Notice.Text="Nom requis.";return;}await ProjectAction(()=>projects.Store.EditProjectAsync(id,project=>{
   project["name"]=name.Text.Trim();project["repo"]=repo.Text.Trim();J.O(project,"control")["group"]=group.Text;J.O(project,"control")["tags"]=new JsonArray(tags.Text.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(t=>(JsonNode?)JsonValue.Create(t)).ToArray());J.O(project,"control")["note"]=notes.Text;J.O(project,"control")["pinned"]=priority.IsChecked==true;J.O(project,"control")["archived"]=archived.IsChecked==true;J.O(project,"control")["order"]=double.TryParse(order.Text,out double number)?Math.Max(0,number):null;J.O(project,"control")["statusOverride"]=status.SelectedItem?.ToString()=="AUTO"?null:status.SelectedItem?.ToString();
  }));ShowProject(id);};editPanel.Children.Add(save);
  var sourcePanel=new StackPanel();projectDetail.Children.Add(new Expander{Header="Sources et affectations",Content=sourcePanel,Margin=new(0,4,0,8)});
  foreach(var source in J.A(state,"sources").OfType<JsonObject>().Where(s=>J.S(s,"projectId")==id)){
   string sid=J.S(source,"id");sourcePanel.Children.Add(Text("Source : "+J.S(source,"type")+" • "+J.S(source,"title"),13));
   var target=Field("Affecter cette source à un identifiant projet (vide = détacher)",id,sourcePanel);
   var move=new Button{Content="Appliquer cette affectation manuelle"};
   move.Click+=async(_,_)=>await ProjectAction(()=>projects.Store.MutateAsync(s=>{
    string destination=target.Text.Trim();if(destination!=""&&J.Find(s,"projects",destination)==null)throw new InvalidOperationException();
    var live=J.Find(s,"sources",sid)!;live["projectId"]=destination==""?null:destination;var control=J.O(live,"control");control["assignment"]="MANUAL";control["projectId"]=destination==""?null:destination;
    foreach(var e in J.A(s,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"sourceId")==sid)){e["projectId"]=destination==""?null:destination;e["controlExcluded"]=destination=="";}
    ControlEngine.Derive(s,new[]{id,destination});return true;
   }));sourcePanel.Children.Add(move);
   var sourceArchive=new CheckBox{Content="Source archivée (exclue des calculs)",IsChecked=J.B(source["control"],"archived")};
   sourceArchive.Click+=async(_,_)=>{bool value=sourceArchive.IsChecked==true;await ProjectAction(()=>projects.Store.MutateAsync(s=>{var live=J.Find(s,"sources",sid)!;J.O(live,"control")["archived"]=value;foreach(var e in J.A(s,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"sourceId")==sid))e["controlExcluded"]=value||J.S(live,"projectId")=="";ControlEngine.Derive(s,[id]);return true;}));};sourcePanel.Children.Add(sourceArchive);
  }
  projectDetail.Children.Add(Text("Activité récente",17));
  foreach(var e in J.A(state,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"projectId")==id).OrderByDescending(e=>J.Date(e,"timestamp")).Take(8))projectDetail.Children.Add(Text(J.S(e,"timestamp")+" • "+J.S(e,"sourceType")+" • "+J.S(e,"title"),12));
 }
 void RenderDiscovered(JsonObject state){
  if(discoveredPanel is null)return;discoveredPanel.Children.Clear();
  var rows=J.A(state,"discovered").OfType<JsonObject>().Where(r=>J.S(r,"type")=="chatgpt_thread").Take(20).ToArray();
  if(rows.Length==0)return;discoveredPanel.Children.Add(Text("Sources découvertes à affecter",18));
  foreach(var row in rows){
   discoveredPanel.Children.Add(Text(J.S(row,"title"),14));var target=new TextBox{ToolTip="Identifiant du projet cible",Margin=new(0,4,0,4)};discoveredPanel.Children.Add(target);
   var assign=new Button{Content="Associer à ce projet pour les prochaines collectes",HorizontalAlignment=HorizontalAlignment.Left};
   assign.Click+=async(_,_)=>await ProjectAction(()=>projects.Store.MutateAsync(s=>{
    string pid=target.Text.Trim(),key=J.S(row,"conversationKey");if(J.Find(s,"projects",pid)==null)throw new InvalidOperationException();
    J.O(J.O(s,"settings"),"manualMappings")[key]=pid;
    string sid="src-chat-"+J.Hash(key);if(J.Find(s,"sources",sid)==null)J.A(s,"sources").Add(new JsonObject{["id"]=sid,["type"]="chatgpt_thread",["projectId"]=pid,["externalId"]=key,["title"]=J.S(row,"title"),["url"]=J.S(row,"url"),["control"]=new JsonObject{["assignment"]="MANUAL",["projectId"]=pid}});
    var item=J.A(s,"discovered").OfType<JsonObject>().FirstOrDefault(r=>J.S(r,"id")==J.S(row,"id"));if(item is not null)J.A(s,"discovered").Remove(item);return true;
   }));discoveredPanel.Children.Add(assign);
  }
 }
}
