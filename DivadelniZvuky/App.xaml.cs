using System.Windows;
namespace DivadelniZvuky;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  if (e.Args.Length == 2 && e.Args[0] == "--self-test")
  {
   DispatcherUnhandledException += (_, args) => { System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "test-error.txt"), args.Exception.ToString()); args.Handled = true; Shutdown(1); };
  }
  var window = new MainWindow();
  MainWindow = window;
  if (e.Args.Length == 2 && e.Args[0] == "--self-test")
  {
   window.TestMode = true;
   window.Show();
   window.Dispatcher.InvokeAsync(async () =>
   {
    try { await window.RunSmokeTest(e.Args[1]); Shutdown(0); }
    catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[1], "test-error.txt"), ex.ToString()); Shutdown(1); }
   }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
   return;
  }
  window.Show();
  if (e.Args.Length > 0) window.OpenProject(e.Args[0]);
 }
}
