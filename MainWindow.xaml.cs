using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using QrCamera.Module;
using UC2836Live.Models;
using UC2836Live.Services;

namespace UC2836Live;
public partial class MainWindow : Window
{
 readonly ObservableCollection<Measurement> rows=new();
 readonly Queue<Measurement> points=new();
 CancellationTokenSource? cancellation;
 Task? running;
 string? sessionPath;
 bool closing;
 int count;
 string function="";
 volatile string currentQr="";
 long _lastQrTicks=0; // ticks lần cuối camera đọc QR; 0 = chưa có / đã hết hạn
 double _qrMaxAgeSec=2.0;
 int _rescanDelayMs=1500;
 bool _requireQrForRecord;
 bool changingLanguage;
 readonly Stopwatch elapsed=new();
 // Camera QR
 readonly CommunicationManager communication;
 readonly CameraService camera;
 readonly QrDecoderService qr;
 readonly SessionService session;
 UC2836Service? ucService;
 CancellationTokenSource? camCancel;
 Task? camTask;
 PreviewFrame? camFrame;
 public MainWindow(CommunicationManager communication, CameraService camera, QrDecoderService qr, SessionService session)
 {
  this.communication=communication;this.camera=camera;this.qr=qr;this.session=session;
  InitializeComponent();
  LanguageSelector.SelectedValue=LocalizationManager.CurrentLanguage;LocalizationManager.LanguageChanged+=RefreshLocalizedRuntimeText;
  MinHeight=620;MinWidth=1100;
  Width=Math.Min(1560,SystemParameters.WorkArea.Width);
  Height=Math.Min(860,SystemParameters.WorkArea.Height);
  WindowStartupLocation=WindowStartupLocation.CenterScreen;
  History.ItemsSource=rows;RefreshPorts();LoadCamSources();RefreshLocalizedRuntimeText();
  if(camera.IsStreaming){CamToggle.Content=L("Main.CameraOffButton");CamToggle.Background=new SolidColorBrush(Color.FromRgb(174,72,65));}
  CamEnhance.IsChecked=false;CamInvert.IsChecked=false;CamSharpen.IsChecked=false;CamShowProcessed.IsChecked=false;CamZoom.Value=1.0;
  camera.FrameReady+=f=>Interlocked.Exchange(ref camFrame,f);
  camera.CodeRead+=code=>Dispatcher.Invoke(()=>OnCamCode(code));
  camera.StatusChanged+=t=>Dispatcher.Invoke(()=>CamStatus.Text=t);
  var renderTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  renderTimer.Tick+=(_,_)=>RenderTick();
  renderTimer.Start();
 }
 static string L(string key)=>LocalizationManager.Get(key);
 void RefreshLocalizedRuntimeText(){if(LanguageSelector is null)return;changingLanguage=true;LanguageSelector.SelectedValue=LocalizationManager.CurrentLanguage;changingLanguage=false;if(running is null)Start.Content=L("Common.Connect");if(History.Columns.Count>=5){History.Columns[0].Header=L("Main.Time");History.Columns[1].Header=L("Main.QrCode");History.Columns[2].Header=L("Main.Primary");History.Columns[3].Header=L("Main.Secondary");History.Columns[^1].Header=L("Main.Result");}}
 void LanguageSelector_SelectionChanged(object sender,SelectionChangedEventArgs e){if(!changingLanguage&&LanguageSelector.SelectedValue is string code)LocalizationManager.SetLanguage(code);}
 void RenderTick()
 {
  var f=Interlocked.Exchange(ref camFrame,null);
  if(f is not null){var bmp=BitmapSource.Create(f.Width,f.Height,96,96,PixelFormats.Bgr24,null,f.Bgr,f.Stride);bmp.Freeze();CamPreview.Source=bmp;CamPlaceholder.Visibility=Visibility.Collapsed;}
  // Kiểm tra QR hết hạn mỗi 80ms
  if(qr.IsExpired(_qrMaxAgeSec))
  {
    qr.Clear(); currentQr=""; Interlocked.Exchange(ref _lastQrTicks,0);
    QrInput.Text="";
    QrState.Text=L("Main.QrExpired");
    QrState.Foreground=new SolidColorBrush(Color.FromRgb(180,90,0));
    CamQrResult.Text=L("Main.Expired");CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(180,90,0));
  }
 }
 void RefreshPorts() {var selected=Port.Text;Port.ItemsSource=CommunicationManager.GetPorts();Port.Text=string.IsNullOrEmpty(selected)?"COM3":selected;}
 void Refresh_Click(object sender,RoutedEventArgs e)=>RefreshPorts();
 // ---- Camera QR ----
 void LoadCamSources()
 {
  try{var sources=camera.ListSources();CamSources.ItemsSource=sources;CamSources.SelectedItem=sources.FirstOrDefault(x=>x.Name.Contains("DroidCam",StringComparison.OrdinalIgnoreCase))??sources.FirstOrDefault();CamStatus.Text=sources.Count==0?L("Main.NoCamera"):L("Main.CameraRunning");}
  catch(Exception ex){CamStatus.Text=L("Main.CameraListError")+ex.Message;}
 }
 void ApplyCamSettings()=>camera.SetSettings(new ScanSettings{Zoom=(float)CamZoom.Value,Enhance=CamEnhance.IsChecked==true,Invert=CamInvert.IsChecked==true,Sharpen=CamSharpen.IsChecked==true,ShowProcessed=CamShowProcessed.IsChecked==true});
 void CamSettings_Changed(object sender,RoutedEventArgs e){if(CamZoomLabel is null)return;CamZoomLabel.Text=$"{CamZoom.Value:F1}×";ApplyCamSettings();}
 async void CamToggle_Click(object sender,RoutedEventArgs e)
 {
  if(camera.IsStreaming || camTask is not null){camCancel?.Cancel();CamToggle.IsEnabled=false;CamStatus.Text=L("Main.CameraStopping");await camera.StopAsync();CamToggle.IsEnabled=true;CamToggle.Content=L("Main.CameraOn");CamToggle.Background=new SolidColorBrush(Color.FromRgb(8,126,117));return;}
  if(CamSources.SelectedItem is not CameraSource src)return;
  camCancel=new();CamSources.IsEnabled=false;CamToggle.Content=L("Main.CameraOffButton");CamToggle.Background=new SolidColorBrush(Color.FromRgb(174,72,65));
  ApplyCamSettings();
  try{camTask=camera.RunAsync(src.Index,camCancel.Token);await camTask;CamStatus.Text=L("Main.CameraStopped");}
  catch(OperationCanceledException){CamStatus.Text=L("Main.CameraStopped");}
  catch(Exception ex){CamStatus.Text=ex.Message;}
  finally
  {
   camTask=null;camCancel?.Dispose();camCancel=null;
   CamSources.IsEnabled=CamToggle.IsEnabled=true;CamToggle.Content=L("Main.CameraOn");
   CamToggle.Background=new SolidColorBrush(Color.FromRgb(8,126,117));
   CamPreview.Source=null;CamPlaceholder.Visibility=Visibility.Visible;
  }
 }
 void OnCamCode(string code)
 {
  qr.SetCode(code);currentQr=code;
  Interlocked.Exchange(ref _lastQrTicks,DateTimeOffset.Now.Ticks);
  QrInput.Text=code;
  QrState.Text=L("Main.CameraCodePrefix")+code;
  QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));
  CamQrResult.Text=code;CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
  // Reset gate sau 1.5s để camera liên tục xác nhận sản phẩm còn trong khung
  Task.Delay(_rescanDelayMs).ContinueWith(_=>camera.ResetCodes());
 }
 void CamReset_Click(object sender,RoutedEventArgs e)
 {
  qr.Clear();currentQr="";Interlocked.Exchange(ref _lastQrTicks,0);
  QrInput.Text="";QrState.Text=L("Main.NotScanned");QrState.Foreground=new SolidColorBrush(Color.FromRgb(67,94,115));
  CamQrResult.Text="—";CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
  camera.ResetCodes();
 }
 void CamDevSettings_Click(object sender,RoutedEventArgs e)
 {
  var dlg=new DevSettingsWindow(_qrMaxAgeSec,_rescanDelayMs,_requireQrForRecord){Owner=this};
  if(dlg.ShowDialog()==true){_qrMaxAgeSec=dlg.QrTimeout;_rescanDelayMs=dlg.RescanDelayMs;_requireQrForRecord=dlg.RequireQr;}
 }
 void QrInput_KeyDown(object sender,KeyEventArgs e)
 {
  if(e.Key!=Key.Enter)return;
  var code=QrInput.Text.Trim();
  if(code.Length==0)return;
  qr.SetCode(code);currentQr=code;Interlocked.Exchange(ref _lastQrTicks,DateTimeOffset.Now.Ticks);QrState.Text=L("Main.ManualQrPrefix")+code;QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));QrInput.SelectAll();e.Handled=true;
 }
 async void ToggleConnection_Click(object sender,RoutedEventArgs e)
 {
  if(running is null) await StartLive();
  else { Status.Text="● Đang ngắt kết nối…"; Start.IsEnabled=false; cancellation?.Cancel(); }
 }
 public async Task StartLive()
 {
  if(running is not null) return;
  string port=Port.Text.Trim();
  if(!port.StartsWith("COM",StringComparison.OrdinalIgnoreCase)){MessageBox.Show(L("Communication.ComPort"));return;}
  int baud=int.Parse(((ComboBoxItem)Baud.SelectedItem).Content.ToString()!);
  int interval=int.Parse(((ComboBoxItem)Interval.SelectedItem).Content.ToString()!);
  cancellation=new();var token=cancellation.Token;
  Refresh.IsEnabled=Port.IsEnabled=Baud.IsEnabled=Interval.IsEnabled=false;Start.IsEnabled=true;Start.Content=L("Common.Disconnect");Start.Background=new SolidColorBrush(Color.FromRgb(174,72,65));
  Status.Text=L("Status.Connecting");rows.Clear();points.Clear();count=0;elapsed.Restart();
  LA.Text=LB.Text=CValue.Text=Compare.Text="—";DrawChart();
  sessionPath=null;
  var liveConnection=communication.Connect(port,baud);
  ucService=new UC2836Service(liveConnection);
  running=Task.Run(()=>ReadLoop(ucService,port,interval,token));
  try {await running;Status.Text=L("Status.Disconnected");}
  catch(OperationCanceledException) {Status.Text=L("Status.Disconnected");}
  catch(Exception ex) {Status.Text=L("Status.Error")+ex.Message; Raw.Text=ex.Message; File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine);}
  finally {running=null;ucService=null;session.Dispose();communication.Disconnect();elapsed.Stop();cancellation.Dispose();cancellation=null;Start.IsEnabled=Refresh.IsEnabled=Port.IsEnabled=Baud.IsEnabled=Interval.IsEnabled=true;Start.Content=L("Common.Connect");Start.Background=new SolidColorBrush(Color.FromRgb(8,126,117));}
 }
 void ReadLoop(UC2836Service? service,string port,int interval,CancellationToken token)
 {
  if(service is null) throw new InvalidOperationException(L("Main.NoConnection"));
  var config=service.ReadConfiguration(); var mode=config.Mode; var dual=config.Dual; var dc=config.Dcr; function=config.Function;
  session.Start(AppContext.BaseDirectory); var path=session.Path!;
  Dispatcher.Invoke(()=>{sessionPath=path;Identity.Text=config.Identity;Status.Text=L("Status.Live")+port;Settings.Text=$"{L("Main.Frequency")}: {config.Frequency} Hz   ·   {L("Main.Voltage")}: {config.Voltage} V   ·   {L("Main.Speed")}: {config.Speed}   ·   {L("Main.Trigger")}: {config.Trigger}   ·   {L("Main.DualFrequency")}: {(dual?"ON":"OFF")}";CLabel.Text="C · "+(dual?"N1":mode=="DEV"?"|LA − LB|":mode=="PEC"?"% deviation":mode);LogPath.Text="Session: "+path;});
  var pace=Stopwatch.StartNew();
  Dispatcher.Invoke(()=>{
   string Nice(string value,string unit)=>double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?Engineering.Format(n,unit):value;
   Settings.Text=$"{L("Main.Frequency")}: {Nice(config.Frequency,"Hz")}   ·   {L("Main.Voltage")}: {Nice(config.Voltage,"V")}   ·   {L("Main.Speed")}: {config.Speed}   ·   {L("Main.Trigger")}: {config.Trigger}";
   if(config.Page.Contains("MEASDISPLAY")||config.Page.Contains("LCRMEASDISP")){
    var units=Engineering.Units(function);PrimaryLabel.Text=units.nameA+" · "+L("Main.Primary");SecondaryLabel.Text=units.nameB+" · "+L("Main.Secondary");CLabel.Text=L("Main.MeasurementState");DcText.Text=L("Main.NoMeasurement");ChartLegend.Text=units.nameA+" · "+units.unitA;
   }
  });
  service.FlushStaleResult(); // xả kết quả tồn đọng trước khi app kết nối
  while(!token.IsCancellationRequested)
  {
   pace.Restart();
   service.RequestMeasurement();
   string response="";
   while(!token.IsCancellationRequested)
   {
    try {response=service.ReadMeasurement();if(response.Length>0)break;}
    catch(TimeoutException) {Dispatcher.Invoke(()=>{Status.Text=L("Status.WaitMeasurement");Stats.Text=L("Main.WaitDut");});}
   }
   token.ThrowIfCancellationRequested();
   Measurement m;
   try
   {
    var freshQr=qr.GetFreshCode(_qrMaxAgeSec);
    m=service.ParseMeasurement(response,config,freshQr,port).Measurement;
    if(_requireQrForRecord&&string.IsNullOrWhiteSpace(m.QrCode))
    {
     Dispatcher.Invoke(()=>{Status.Text=L("Main.WaitQr");Stats.Text=L("Main.NoQrResult");});
     continue;
    }
   }
   catch(FormatException ex){Dispatcher.Invoke(()=>{Status.Text=L("Main.InvalidData");Raw.Text=ex.Message;});continue;}
   var hadQr=!string.IsNullOrEmpty(m.QrCode);
   session.Append(m,mode,dual,dc);
   if(hadQr){qr.Clear();currentQr="";Interlocked.Exchange(ref _lastQrTicks,0);}
   Dispatcher.Invoke(()=>
   {
    Display(m);
    if(hadQr)
    {
     QrInput.Text="";
     QrState.Text=L("Main.RecordedNext");
     QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));
     CamQrResult.Text="—";CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
    }
   });
   // Camera tự re-scan qua OnCamCode → không cần reset thủ công tại đây
   int delay=Math.Max(0,interval-(int)pace.ElapsedMilliseconds);
   if(token.WaitHandle.WaitOne(delay))break;
  }
 }
  void Display(Measurement m)
 {
  count++;Status.Text=L("Status.Live")+Port.Text;LA.Text=m.DisplayA;LB.Text=m.DisplayB;CValue.Text=m.PassFail;Compare.Text=m.PassFail;
  QrState.Text=string.IsNullOrWhiteSpace(m.QrCode)?L("Main.NotScanned"):"QR: "+m.QrCode+" · "+m.PassFail;
  if(m.IsLcr)
  {
   var units=Engineering.Units(m.Function);
   PrimaryLabel.Text=units.nameA+" · THÔNG SỐ CHÍNH";
   SecondaryLabel.Text=units.nameB+" · THÔNG SỐ PHỤ";
   CLabel.Text=L("Main.MeasurementState");CValue.Text=m.PassFail;
   if(int.TryParse(m.Compare,out var bin))Compare.Text=m.MeasureStatus!="0"?L("Main.MeasurementError"):bin is >=1 and <=9?"PASS · BIN "+bin:bin==0?L("Main.OutCompare"):bin==10?"AUX":L("Main.BinPrefix")+bin;
   ChartLegend.Text=units.nameA+" · "+units.unitA;
  }
  else {PrimaryLabel.Text=L("Main.CoilA");SecondaryLabel.Text=L("Main.CoilB");ChartLegend.Text="LA ━ teal · LB ━ blue";}
  Wire.Text=L("Main.PolarityValue")+m.Polarity+L("Main.WireValue")+(m.Wire=="1"?L("Main.WireNormal"):m.Wire=="2"?L("Main.WireWrong"):L("Main.WireOff"));
  DcText.Text=$"RA: {m.DisplayRA} / RB: {m.DisplayRB}";Second.Text=m.Second;
  if(m.IsLcr){Wire.Text=L("Main.ResultFromDevice");DcText.Text=L("Main.ValidMeasurement");Second.Text=L("Main.SingleMode");}
  rows.Insert(0,m);if(rows.Count>2000)rows.RemoveAt(rows.Count-1);
  points.Enqueue(m);if(points.Count>150)points.Dequeue();DrawChart();
  Stats.Text=$"{count:N0}{L("Main.SampleStats")}{count/Math.Max(1,elapsed.Elapsed.TotalSeconds):F1}{L("Main.Replies")}{m.Time:HH:mm:ss.fff} · {L("Main.QrPrefix")}{(string.IsNullOrWhiteSpace(m.QrCode)?L("Main.SampleNotScanned"):m.QrCode)}";
  Raw.Text="RX  "+m.Raw;
 }
 void DrawChart()
 {
  var a=points.ToArray();double w=Chart.ActualWidth,h=Chart.ActualHeight;
  LineA.Points=new();LineB.Points=new();if(a.Length<2||w<1||h<1)return;
  bool lcr=a[^1].IsLcr;
  var valid=a.SelectMany(x=>lcr?new[]{x.LA}:new[]{x.LA,x.LB}).Where(x=>Math.Abs(x)<1e30).ToArray();if(valid.Length==0)return;
  double min=valid.Min(),max=valid.Max(),span=max-min;if(span<1e-15)span=Math.Max(Math.Abs(max)*0.01,1e-6);
  min-=span*0.08;max+=span*0.08;span=max-min;
  // Sentinel values (e.g. 9.9E37) are excluded from the chart.
  if(a.Any(x=>Math.Abs(x.LA)>=1e30||Math.Abs(x.LB)>=1e30)){Scale.Text=L("Main.OutOfRange");return;}
  LineA.Points=new PointCollection(a.Select((x,i)=>new Point(i*w/(a.Length-1),h-(x.LA-min)/span*h)));
  if(!lcr)LineB.Points=new PointCollection(a.Select((x,i)=>new Point(i*w/(a.Length-1),h-(x.LB-min)/span*h)));
  var unit=lcr?Engineering.Units(a[^1].Function).unitA:"H";
  Scale.Text=$"Thứ tự mẫu nhận →     Giá trị: {Engineering.Format(min,unit)} … {Engineering.Format(max,unit)}";
 }
 void Chart_SizeChanged(object sender,SizeChangedEventArgs e)=>DrawChart();
 void Export_Click(object sender,RoutedEventArgs e)
 {
   if(sessionPath is null||!File.Exists(sessionPath)){MessageBox.Show(this,L("Main.NoSession"));return;}
  var save=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName=Path.GetFileName(sessionPath)};
  if(save.ShowDialog(this)==true)
  {
   try {
    if(Path.GetFullPath(save.FileName).Equals(Path.GetFullPath(sessionPath),StringComparison.OrdinalIgnoreCase))return;
    // Snapshot complete lines only, because acquisition may still be appending.
    using var input=new FileStream(sessionPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
    var length=input.Length;var bytes=new byte[checked((int)length)];input.ReadExactly(bytes);
    int last=Array.LastIndexOf(bytes,(byte)'\n');
    using var output=new FileStream(save.FileName,FileMode.Create,FileAccess.Write);output.Write(bytes,0,last+1);
     MessageBox.Show(this,L("Main.ExportDone"));
    }catch(Exception ex){MessageBox.Show(this,L("Main.ExportError")+ex.Message);}
  }
 }
 async void Window_Closing(object? sender,CancelEventArgs e)
 {
  if(closing)return;
  if(running is not null||camTask is not null||camera.IsStreaming)
  {
   e.Cancel=true;
   cancellation?.Cancel();camCancel?.Cancel();
   try{if(running is not null)await running;}catch{}
   try{if(camTask is not null)await camTask;}catch{}
   try{await camera.StopAsync();}catch{}
   closing=true;Close();
  }
 }
}
