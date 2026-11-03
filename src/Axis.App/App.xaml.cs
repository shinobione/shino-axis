using System.Windows;
namespace Axis.App;
public partial class App : Application {
 AxisInstance? instance;
 protected override void OnStartup(StartupEventArgs e){
  base.OnStartup(e);instance=new(Axis.App.MainWindow.ProfileDirectory());
  if(!instance.IsOwner){Shutdown();return;}
  var window=new MainWindow();MainWindow=window;
  instance.OnActivate(()=>Dispatcher.BeginInvoke(new System.Action(()=>{if(!window.IsShuttingDown){window.Show();window.WindowState=WindowState.Normal;window.Activate();}})));
  window.Show();
 }
 protected override void OnExit(ExitEventArgs e){instance?.Dispose();base.OnExit(e);}
}
