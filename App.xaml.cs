using System.Windows;
using System.IO;

namespace UC2836Live;
public partial class App : Application
{
 protected override async void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  if (e.Args.Contains("--self-test") || e.Args.Contains("--probe"))
  {
   try
   {
    string result = e.Args.Contains("--probe") ? await Task.Run(Device.Probe) : Measurement.SelfTest();
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "verification.txt"), result);
    Shutdown(0);
   }
   catch(Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"verification.txt"),ex.ToString()); Shutdown(1); }
   return;
  }
  var window = new MainWindow(); MainWindow = window; window.Show();
  if(e.Args.Contains("--capture"))
  {
   var timer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromSeconds(12)};
   timer.Tick+=(_,_)=>{timer.Stop();window.UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(AppContext.BaseDirectory,"ui-review.png"));encoder.Save(file);};
   timer.Start();
  }
  if(e.Args.Contains("--live")) await window.StartLive();
 }
}
