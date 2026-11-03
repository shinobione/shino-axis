using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Axis.Core.Projects;
public static partial class ControlEngine {
 static string Clean(string s)=>J.Clean(s);
 static bool Useful(string s)=>s.Length>0&&s.Length<=700&&!GENERIC.IsMatch(s);
 static bool Blocked(string s)=>BLOCKED.IsMatch(s)&&!RESOLVED_BLOCK.IsMatch(s);
 public static string? Classify(JsonObject e){
  if(!J.B(e,"inventoryCurrent",true))return null;
  string resume=Clean(J.S(e,"resumeAction")),text=Clean(J.S(e,"currentStateSummary")+" "+J.S(e,"summary")),full=resume+" "+text+" "+J.S(e,"title");
  if(string.IsNullOrWhiteSpace(full)||SUPERSEDED.IsMatch(full))return null;
  bool chat=J.S(e,"sourceType")=="chatgpt_thread";
  string hint=J.S(e,"derivedStatusHint");if(!chat&&new[]{"BLOCKED","NEEDS TEST","STABLE"}.Contains(hint))return hint;
  if(Useful(resume))return Blocked(resume)?"BLOCKED":TEST.IsMatch(resume)?"NEEDS TEST":"ACTIVE";
  if(chat){
   if(Blocked(text))return "BLOCKED";
   if(TEST.IsMatch(text)&&!NO_REMAINING.IsMatch(text))return "NEEDS TEST";
   if((NO_REMAINING.IsMatch(text)||CLOSED.IsMatch(text))&&!(OPEN.IsMatch(text)&&!NO_REMAINING.IsMatch(text)))return "STABLE";
   return "ACTIVE";
  }
  text=J.S(e,"title")+" "+J.S(e,"summary");
  if(BLOCKED_GITHUB.IsMatch(text)&&!RESOLVED_BLOCK.IsMatch(text))return "BLOCKED";
  if(PENDING_GITHUB.IsMatch(text))return "NEEDS TEST";
  if(DONE_GITHUB.IsMatch(text)||POSITIVE_GITHUB.IsMatch(text))return "STABLE";return null;
 }
 static double Weight(JsonObject e){
  double.TryParse(J.S(e,"confidence","0.7"),System.Globalization.CultureInfo.InvariantCulture,out double confidence);
  return confidence*100+(J.S(e,"sourceType") switch{"github_truth"=>25,"github_pr"=>15,"github_commit"=>10,"chatgpt_thread"=>18,_=>0})-(SUPERSEDED.IsMatch(J.S(e,"title")+" "+J.S(e,"summary"))?70:0);
 }
 public static void Derive(JsonObject state,IEnumerable<string>? dirty=null){
  var ids=dirty?.ToHashSet();var derived=J.A(state,"derived");
  foreach(var p in J.A(state,"projects").OfType<JsonObject>()){
   string id=J.S(p,"id");if(ids is not null&&!ids.Contains(id))continue;
   var usable=J.A(state,"evidence").OfType<JsonObject>().Where(e=>J.S(e,"projectId")==id&&J.B(e,"inventoryCurrent",true)&&!J.B(e,"controlExcluded")&&Weight(e)>20).OrderByDescending(e=>J.Date(e,"timestamp")).ToArray();
   JsonObject result;
   if(usable.Length==0)result=new(){["projectId"]=id,["status"]="UNSYNCED",["statusSource"]="no_evidence",["summary"]="Aucune preuve actuelle exploitable.",["resumeSource"]="unsynced",["nextAction"]="Run ChatGPT project backfill and/or GitHub sync to reconstruct the real current state.",["evidenceIds"]=new JsonArray()};
   else{
    var newest=usable[0];var recent=usable.Where(e=>J.Date(newest,"timestamp")-J.Date(e,"timestamp")<=TimeSpan.FromDays(45)).ToArray();
    var selected=recent.FirstOrDefault(e=>J.S(e,"sourceType")!="chatgpt_thread"&&new[]{"BLOCKED","NEEDS TEST","STABLE"}.Contains(J.S(e,"derivedStatusHint")))??recent.FirstOrDefault(e=>Classify(e)!=null);
    var status=selected is null?"ACTIVE":J.S(selected,"sourceType")!="chatgpt_thread"&&new[]{"BLOCKED","NEEDS TEST","STABLE"}.Contains(J.S(selected,"derivedStatusHint"))?J.S(selected,"derivedStatusHint"):Classify(selected)??"ACTIVE";
    var summary=recent.FirstOrDefault(e=>!SUPERSEDED.IsMatch(J.S(e,"title")+" "+J.S(e,"summary"))&&(J.S(e,"summary")!=""||J.S(e,"currentStateSummary")!=""||J.S(e,"sourceType")=="chatgpt_thread"))??newest;
    var resume=status is "BLOCKED" or "NEEDS TEST"?recent.FirstOrDefault(e=>Classify(e)==status)??selected??newest:recent.FirstOrDefault(e=>!SUPERSEDED.IsMatch(J.S(e,"title")+" "+J.S(e,"summary"))&&(J.S(e,"sourceType")=="chatgpt_thread"||J.S(e,"resumeAction")!=""))??newest;
    var action=Resume(p,resume);
    result=new(){["projectId"]=id,["status"]=status,["statusEvidenceId"]=selected?["id"]?.DeepClone(),["statusSource"]=selected is null?"default_active":StatusSource(selected),["statusReason"]=selected is null?"Preuves récentes sans signal plus fort.":"Règle CONTROL "+StatusSource(selected)+" • "+J.S(selected,"title"),["summary"]=J.S(summary,"currentStateSummary",J.S(summary,"summary",J.S(summary,"title"))),["lastMovementAt"]=newest["timestamp"]?.DeepClone(),["nextAction"]=status=="STABLE"&&action.Source=="title_heuristic"?"Aucune action immédiate — état stable. Reprendre uniquement lorsqu’un nouveau chantier apparaît.":action.Action,["resumeSource"]=action.Source,["resumeEvidenceId"]=resume["id"]?.DeepClone(),["evidenceIds"]=new JsonArray(recent.Take(8).Select(e=>e["id"]?.DeepClone()).ToArray())};
   }
   if(usable.Length>0&&DateTimeOffset.UtcNow-J.Date(usable[0],"timestamp")>TimeSpan.FromDays(60)&&J.S(result,"status")=="ACTIVE"){result["lastKnownStatus"]="ACTIVE";result["status"]="UNSYNCED";result["statusSource"]="expired_evidence";result["statusReason"]="Dernière preuve trop ancienne pour attester une activité actuelle.";result["summary"]="Observation périmée • "+J.S(result,"summary");}
   if(usable.Length==0){
    var inventory=state["settings"]?["lastChatgptInventory"]?["projects"] as JsonArray;
    var mappings=state["settings"]?["chatgptProjectMappings"] as JsonObject;
    var matches=inventory?.OfType<JsonObject>().Where(v=>(J.S(v,"key")!=""&&mappings?[J.S(v,"key")]?.ToString()==id)||J.Norm(J.S(v,"title"))==J.Norm(J.S(p,"name"))).ToArray()??[];
    if(matches.Length==1&&double.TryParse(J.S(matches[0],"conversationCount",J.S(matches[0],"total","0")),out double count)&&count==0){result["status"]="EMPTY";result["statusSource"]="inventory";result["summary"]="Inventaire ChatGPT : aucune conversation dans ce projet.";result["nextAction"]=$"Créer la première conversation dans {J.S(p,"name")} lorsqu’un vrai chantier doit y être suivi.";result["resumeSource"]="empty_project";}
   }
   var manual=J.S(p["control"],"statusOverride").ToUpperInvariant();if(new[]{"ACTIVE","NEEDS TEST","BLOCKED","STABLE","WAITING","DONE","EMPTY","UNSYNCED"}.Contains(manual)){result["autoStatus"]=result["status"]?.DeepClone();result["status"]=manual;result["statusSource"]="manual_override";}
   result["derivedAt"]=DateTimeOffset.UtcNow.ToString("O");if(usable.Length>0){double days=(DateTimeOffset.UtcNow-J.Date(usable[0],"timestamp")).TotalDays;result["freshness"]=days<=3?"FRESH":days<=14?"RECENT":days<=60?"AGING":"STALE";result["confidence"]=usable.Max(Weight)>=105&&usable.Select(e=>J.S(e,"sourceType")).Distinct().Count()>1?"HIGH":usable.Max(Weight)>=90?"MEDIUM":"LOW";if(J.S(result,"status")=="BLOCKED")result["blocker"]=J.S(usable.FirstOrDefault(e=>Classify(e)=="BLOCKED"),"summary");}
   var old=derived.OfType<JsonObject>().FirstOrDefault(d=>J.S(d,"projectId")==id);if(old is not null)derived.Remove(old);derived.Add(result);
  }
 }
 static string CleanResume(string value){
  value=Regex.Replace(value.Replace("\r",""),@"^\s*(?:USER|ASSISTANT|SYSTEM)\s*:\s*","",RegexOptions.IgnoreCase);
  value=Regex.Replace(value,@"^\s*(?:NEXT|TODO|TO-DO|RESUME|ACTION)\s*[:—-]\s*","",RegexOptions.IgnoreCase);
  return Clean(Regex.Replace(value,@"^\s*[-*•]+\s*",""));
 }
 public static (string Action,string Source) Resume(JsonObject project,JsonObject e){
  string title=J.S(e,"title",J.S(project,"name"));
  if(J.S(e,"sourceType")!="chatgpt_thread")return Useful(J.S(e,"resumeAction"))&&!NO_REMAINING.IsMatch(J.S(e,"resumeAction"))?(J.S(e,"resumeAction"),"explicit_resume_action"):("Review the newest evidence and choose the next safe action.","fallback");
  foreach(string key in new[]{"resumeAction","currentStateSummary","summary"}){
   var candidates=Regex.Split(J.S(e,key),@"\n+|\s+·\s+|(?<=[.!?])\s+(?=[A-ZÀ-ÖØ-Ý0-9])").Select(CleanResume).Where(s=>s.Length>=5&&Useful(s)&&!ResumePatterns.NOISY.IsMatch(s)).Select((s,i)=>new{Text=s,Index=i,Score=ResumeScore(s)}).OrderByDescending(x=>x.Score).ThenByDescending(x=>x.Index).FirstOrDefault();
   if(candidates is not null&&candidates.Score>0)return($"Reprendre « {title} » — {candidates.Text.TrimEnd('.',';')}.",key=="resumeAction"?"chat_resume_action":"chat_state_summary");
  }
  string fallback=Regex.IsMatch(title,@"todo|to-do|roadmap|checklist|liste de tâches|liste de taches",RegexOptions.IgnoreCase)?$"Reprendre la roadmap {J.S(project,"name")} depuis « {title} » et traiter le prochain élément ouvert.":
   Regex.IsMatch(title,@"test|tester|validation|smoke|compatibilit|essai",RegexOptions.IgnoreCase)?$"Reprendre « {title} » et exécuter la prochaine validation encore ouverte.":
   Regex.IsMatch(title,@"install|installation|setup|configur|pilote|driver",RegexOptions.IgnoreCase)?$"Reprendre « {title} » et poursuivre l’installation/configuration depuis le dernier point validé.":
   Regex.IsMatch(title,@"bug|erreur|fail|ko\b|dépann|depann|probl[eè]me|fix|corrig",RegexOptions.IgnoreCase)?$"Reprendre le diagnostic « {title} » depuis le dernier constat confirmé.":
   Regex.IsMatch(title,@"proposition|lyrics|cover|titre|soundcloud|spotify|suno|musique|music|canvas|canva",RegexOptions.IgnoreCase)?$"Reprendre « {title} » depuis le dernier choix créatif validé et poursuivre l’étape suivante.":
   J.S(project,"id")=="personnel"?$"Reprendre le sujet PERSONNEL « {title} » et continuer depuis le dernier échange utile.":$"Reprendre « {title} » et continuer depuis le dernier échange confirmé.";
  return(fallback,"title_heuristic");
 }
 static string StatusSource(JsonObject e){
  bool chat=J.S(e,"sourceType")=="chatgpt_thread";
  if(!chat&&new[]{"BLOCKED","NEEDS TEST","STABLE"}.Contains(J.S(e,"derivedStatusHint")))return "explicit_hint";
  if(Useful(Clean(J.S(e,"resumeAction"))))return chat?"chat_resume_action":"source_resume_action";
  return chat?Classify(e)=="ACTIVE"?"chat_activity":"chat_state_summary":"source_text";
 }
 static int ResumeScore(string s)=>ResumePatterns.NO_ACTION.IsMatch(s)?-100:(ResumePatterns.ACTION.IsMatch(s)?4:0)+(ResumePatterns.OPEN_STATE.IsMatch(s)?3:0)+(Regex.IsMatch(s,@"^(open|run|launch|verify|validate|test|check|fix|continue|finish|build|implement|deploy|send|compare|choose|review|audit|install|configure|create|update|remove|add|scan|sync)\b",RegexOptions.IgnoreCase|RegexOptions.ECMAScript)?3:0)+(Regex.IsMatch(s,@"\b(next step|next|todo|to-do|remaining|resume)\b",RegexOptions.IgnoreCase|RegexOptions.ECMAScript)?4:0)-(ResumePatterns.CLOSED_ONLY.IsMatch(s)&&!ResumePatterns.OPEN_STATE.IsMatch(s)?5:0)-(s.Length>360?2:0);
}
