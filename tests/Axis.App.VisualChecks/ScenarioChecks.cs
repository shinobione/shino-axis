using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Axis.Core;
using Axis.Core.Modules;
using Axis.Core.Missions;
partial class Program {
 static void ScenarioRenders(Axis.App.MainWindow window,FrameworkElement root,ModuleRuntime runtime,CodexModule codex,string output){
  var original=runtime.Modules.ToDictionary(m=>m.Id,m=>(m.State,m.Data));var missions=codex.Missions.Current;var sample=missions.Recent.First(m=>m.Status=="EN COURS");
  try{
   foreach(var scenario in new[]{"idle","one-mission","four-missions","terminal","media-pause","media-absent","pc-unavailable","module-error"}){
    var now=DateTimeOffset.UtcNow;
    foreach(var entry in runtime.Modules){Set(entry,"State",original[entry.Id].State);Set(entry,"Data",original[entry.Id].Data! with{Timestamp=now});}
    int count=scenario=="one-mission"?1:scenario=="four-missions"?4:0;
    var rows=Enumerable.Range(0,count).Select(i=>sample with{Identity="scenario-"+i,Workspace="SIMULATION • mission "+(i+1),Started=now.AddMinutes(-i-1),LastActivity=now}).ToArray();
    if(scenario=="terminal")rows=[sample with{Status="TERMINÉ",Finished=now.AddSeconds(-10)},sample with{Identity="scenario-fail",Status="ÉCHEC",Finished=now.AddSeconds(-20)}];
    Set(codex.Missions,"Current",missions with{Checked=now,Recent=rows});
    var media=runtime.Modules.Single(m=>m.Id=="media");
    if(scenario=="media-pause"){var values=media.Data!.Values.ToDictionary(p=>p.Key,p=>p.Value);values["playback"]="Pause";Set(media,"Data",media.Data with{Values=values});}
    if(scenario=="media-absent")Set(media,"Data",new ModuleData(now,true,new Dictionary<string,string>{{"status","Aucune session"}}));
    if(scenario=="pc-unavailable")Set(runtime.Modules.Single(m=>m.Id=="pc"),"Data",new ModuleData(now,true,new Dictionary<string,string>{{"cpu","Indisponible"},{"ram","Indisponible"}}));
    if(scenario=="module-error"){var entry=runtime.Modules.Single(m=>m.Id=="network");Set(entry,"State",ModuleState.Error);Set(entry,"Error","Opération indisponible (IOException).");Set(runtime.Modules.Single(m=>m.Id=="pc"),"State",ModuleState.Stopped);}
    Call(window,"Navigate","Accueil");root.Width=1280;root.Height=900;root.Measure(new(1280,900));root.Arrange(new(0,0,1280,900));root.UpdateLayout();
    var bitmap=new RenderTargetBitmap(1280,900,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(output,"Scenario-"+scenario+"-SIMULATION.png")))encoder.Save(file);
    var host=(StackPanel)Field(window,"homeActivity");if(count>0&&host.Visibility!=Visibility.Visible)throw new Exception("Scenario missions hidden");
    var attention=(TextBlock)Field(window,"attentionText");if(scenario is "module-error" or "pc-unavailable"&&attention.Visibility!=Visibility.Visible)throw new Exception("Real issue hidden");
    if(scenario=="idle"&&attention.Visibility!=Visibility.Collapsed)throw new Exception("Healthy idle has noisy alert");
    Console.WriteLine("PASS scenario WPF "+scenario);
   }
  }finally{foreach(var entry in runtime.Modules){Set(entry,"State",original[entry.Id].State);Set(entry,"Data",original[entry.Id].Data);}Set(codex.Missions,"Current",missions);}
 }
}
