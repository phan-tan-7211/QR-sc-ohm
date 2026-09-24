using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using QrCamera.Module;

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
 readonly Stopwatch elapsed=new();
 // Camera QR
 readonly QrScanner camScanner=new();
 CancellationTokenSource? camCancel;
 Task? camTask;
 PreviewFrame? camFrame;
 public MainWindow()
 {
  InitializeComponent();
  MinHeight=620;MinWidth=1100;
  Width=Math.Min(1560,SystemParameters.WorkArea.Width);
  Height=Math.Min(860,SystemParameters.WorkArea.Height);
  WindowStartupLocation=WindowStartupLocation.CenterScreen;
  History.ItemsSource=rows;RefreshPorts();LoadCamSources();
  CamEnhance.IsChecked=false;CamInvert.IsChecked=false;CamSharpen.IsChecked=false;CamShowProcessed.IsChecked=false;CamZoom.Value=1.0;
  camScanner.FrameReady+=f=>Interlocked.Exchange(ref camFrame,f);
  camScanner.CodeRead+=r=>Dispatcher.Invoke(()=>OnCamCode(r.Code));
  camScanner.StatusChanged+=t=>Dispatcher.Invoke(()=>CamStatus.Text=t);
  var renderTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  renderTimer.Tick+=(_,_)=>RenderTick();
  renderTimer.Start();
 }
 void RenderTick()
 {
  var f=Interlocked.Exchange(ref camFrame,null);
  if(f is not null){var bmp=BitmapSource.Create(f.Width,f.Height,96,96,PixelFormats.Bgr24,null,f.Bgr,f.Stride);bmp.Freeze();CamPreview.Source=bmp;CamPlaceholder.Visibility=Visibility.Collapsed;}
  // Kiểm tra QR hết hạn mỗi 80ms
  var ticks=Interlocked.Read(ref _lastQrTicks);
  if(ticks>0&&!string.IsNullOrEmpty(currentQr))
  {
   var ageSec=(DateTimeOffset.Now.Ticks-ticks)/(double)TimeSpan.TicksPerSecond;
   if(ageSec>_qrMaxAgeSec)
   {
    currentQr="";Interlocked.Exchange(ref _lastQrTicks,0);
    QrInput.Text="";
    QrState.Text="⚠ QR hết hạn · Đưa sản phẩm vào camera để quét lại";
    QrState.Foreground=new SolidColorBrush(Color.FromRgb(180,90,0));
    CamQrResult.Text="Hết hạn";CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(180,90,0));
   }
  }
 }
 void RefreshPorts() {var selected=Port.Text;Port.ItemsSource=SerialPort.GetPortNames().Order().ToArray();Port.Text=string.IsNullOrEmpty(selected)?"COM3":selected;}
 void Refresh_Click(object sender,RoutedEventArgs e)=>RefreshPorts();
 // ---- Camera QR ----
 void LoadCamSources()
 {
  try{var sources=CameraCatalog.List();CamSources.ItemsSource=sources;CamSources.SelectedItem=sources.FirstOrDefault(x=>x.Name.Contains("DroidCam",StringComparison.OrdinalIgnoreCase))??sources.FirstOrDefault();CamStatus.Text=sources.Count==0?"Không tìm thấy camera":"Chọn nguồn rồi nhấn Bật";}
  catch(Exception ex){CamStatus.Text="Lỗi liệt kê camera: "+ex.Message;}
 }
 void ApplyCamSettings()=>camScanner.Settings=new ScanSettings{Zoom=(float)CamZoom.Value,Enhance=CamEnhance.IsChecked==true,Invert=CamInvert.IsChecked==true,Sharpen=CamSharpen.IsChecked==true,ShowProcessed=CamShowProcessed.IsChecked==true};
 void CamSettings_Changed(object sender,RoutedEventArgs e){if(CamZoomLabel is null)return;CamZoomLabel.Text=$"{CamZoom.Value:F1}×";ApplyCamSettings();}
 async void CamToggle_Click(object sender,RoutedEventArgs e)
 {
  if(camTask is not null){camCancel?.Cancel();CamToggle.IsEnabled=false;CamStatus.Text="Đang tắt camera…";return;}
  if(CamSources.SelectedItem is not CameraSource src)return;
  camCancel=new();CamSources.IsEnabled=false;CamToggle.Content="Tắt";CamToggle.Background=new SolidColorBrush(Color.FromRgb(174,72,65));
  ApplyCamSettings();
  try{camTask=camScanner.RunAsync(src.Index,camCancel.Token);await camTask;CamStatus.Text="Camera đã dừng";}
  catch(OperationCanceledException){CamStatus.Text="Camera đã dừng";}
  catch(Exception ex){CamStatus.Text=ex.Message;}
  finally
  {
   camTask=null;camCancel?.Dispose();camCancel=null;
   CamSources.IsEnabled=CamToggle.IsEnabled=true;CamToggle.Content="Bật";
   CamToggle.Background=new SolidColorBrush(Color.FromRgb(8,126,117));
   CamPreview.Source=null;CamPlaceholder.Visibility=Visibility.Visible;
  }
 }
 void OnCamCode(string code)
 {
  currentQr=code;
  Interlocked.Exchange(ref _lastQrTicks,DateTimeOffset.Now.Ticks);
  QrInput.Text=code;
  QrState.Text="Camera: "+code;
  QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));
  CamQrResult.Text=code;CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
  // Reset gate sau 1.5s để camera liên tục xác nhận sản phẩm còn trong khung
  Task.Delay(_rescanDelayMs).ContinueWith(_=>camScanner.ResetCodes());
 }
 void CamReset_Click(object sender,RoutedEventArgs e)
 {
  currentQr="";Interlocked.Exchange(ref _lastQrTicks,0);
  QrInput.Text="";QrState.Text="Chưa quét mã";QrState.Foreground=new SolidColorBrush(Color.FromRgb(67,94,115));
  CamQrResult.Text="—";CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
  camScanner.ResetCodes();
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
  currentQr=code;Interlocked.Exchange(ref _lastQrTicks,DateTimeOffset.Now.Ticks);QrState.Text="Đã quét: "+code;QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));QrInput.SelectAll();e.Handled=true;
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
  if(!port.StartsWith("COM",StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Chọn cổng COM hợp lệ.");return;}
  int baud=int.Parse(((ComboBoxItem)Baud.SelectedItem).Content.ToString()!);
  int interval=int.Parse(((ComboBoxItem)Interval.SelectedItem).Content.ToString()!);
  cancellation=new();var token=cancellation.Token;
  Refresh.IsEnabled=Port.IsEnabled=Baud.IsEnabled=Interval.IsEnabled=false;Start.IsEnabled=true;Start.Content="Ngắt kết nối";Start.Background=new SolidColorBrush(Color.FromRgb(174,72,65));
  Status.Text="● Đang kết nối…";rows.Clear();points.Clear();count=0;elapsed.Restart();
  LA.Text=LB.Text=CValue.Text=Compare.Text="—";DrawChart();
  sessionPath=null;
  running=Task.Run(()=>ReadLoop(port,baud,interval,token));
  try {await running;Status.Text="● Đã dừng · COM đã đóng";}
  catch(OperationCanceledException) {Status.Text="● Đã dừng · COM đã đóng";}
  catch(Exception ex) {Status.Text="● Lỗi: "+ex.Message; Raw.Text=ex.Message; File.AppendAllText(Path.Combine(AppContext.BaseDirectory,"errors.log"),DateTime.Now+" "+ex+Environment.NewLine);}
  finally {running=null;elapsed.Stop();cancellation.Dispose();cancellation=null;Start.IsEnabled=Refresh.IsEnabled=Port.IsEnabled=Baud.IsEnabled=Interval.IsEnabled=true;Start.Content="Kết nối";Start.Background=new SolidColorBrush(Color.FromRgb(8,126,117));}
 }
 void ReadLoop(string port,int baud,int interval,CancellationToken token)
 {
  using var device=new Device(port,baud);
  string Ask(string q) {token.ThrowIfCancellationRequested();return device.Query(q);}
  var identity=Ask("*IDN?");
  if(!identity.Contains("UC2836CX",StringComparison.OrdinalIgnoreCase))throw new IOException("Thiết bị không phải UC2836CX: "+identity);
  var mode=Ask(":TRAN:MODE?");var dual=Ask(":TRAN:DF?")=="1";var dc=Ask(":TRAN:DCR?")=="1";
  function=Ask("FUNC:IMP?").ToUpperInvariant();
  var page=Ask("DISP:PAGE?").Replace(" ","").ToUpperInvariant();
  var frequency=Ask("FREQ?");var voltage=Ask("VOLT?");var speed=Ask("APER?");var trigger=Ask("TRIG:SOUR?");
  var dir=Path.Combine(AppContext.BaseDirectory,"Sessions");Directory.CreateDirectory(dir);
  var path=Path.Combine(dir,$"UC2836_{DateTime.Now:yyyyMMdd_HHmmss_fff}.csv");
  using var log=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(true)){AutoFlush=true};
  log.WriteLine("Time,QRCode,PassFail,LA,LB,C,Compare,RA,RB,Polarity,Wire,Mode,Dual,DCR,Raw");
  Dispatcher.Invoke(()=>{sessionPath=path;Identity.Text=identity;Status.Text="● LIVE · "+port;Settings.Text=$"Tần số: {frequency} Hz   ·   Điện áp: {voltage} V   ·   Tốc độ: {speed}   ·   Trigger: {trigger}   ·   Hai tần số: {(dual?"Bật":"Tắt")}";CLabel.Text="C · "+(dual?"N1":mode=="DEV"?"|LA − LB|":mode=="PEC"?"ĐỘ LỆCH %":mode);LogPath.Text="Tự lưu: "+path;});
  var pace=Stopwatch.StartNew();
  Dispatcher.Invoke(()=>{
   string Nice(string value,string unit)=>double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?Engineering.Format(n,unit):value;
   Settings.Text=$"Tần số: {Nice(frequency,"Hz")}   ·   Điện áp: {Nice(voltage,"V")}   ·   Tốc độ: {speed}   ·   Trigger: {trigger}";
   if(page.Contains("MEASDISPLAY")||page.Contains("LCRMEASDISP")){
    var units=Engineering.Units(function);PrimaryLabel.Text=units.nameA+" · THÔNG SỐ CHÍNH";SecondaryLabel.Text=units.nameB+" · THÔNG SỐ PHỤ";CLabel.Text="TRẠNG THÁI PHÉP ĐO";DcText.Text="Chờ kết quả đo";ChartLegend.Text=units.nameA+" · "+units.unitA;
   }
  });
  device.FlushStaleResult(); // xả kết quả tồn đọng trước khi app kết nối
  while(!token.IsCancellationRequested)
  {
   pace.Restart();
   device.RequestMeasurement();
   string response="";
   while(!token.IsCancellationRequested)
   {
    try {response=device.ReadMeasurement();if(response.Length>0)break;}
    catch(TimeoutException) {Dispatcher.Invoke(()=>{Status.Text="● Đã kết nối · Chờ phép đo / DUT";Stats.Text="Chưa có kết quả mới. Máy có thể đang chờ tự kích hoặc trigger ngoài.";});}
   }
   token.ThrowIfCancellationRequested();
   Measurement m;
   try
   {
    var qrAgeSec=(DateTimeOffset.Now.Ticks-Interlocked.Read(ref _lastQrTicks))/(double)TimeSpan.TicksPerSecond;
    var freshQr=!string.IsNullOrEmpty(currentQr)&&qrAgeSec<=_qrMaxAgeSec?currentQr:"";
    m=Measurement.Parse(response,dual,dc) with {Function=function, QrCode=freshQr, PassFail=Classify(response,function)};
    if(_requireQrForRecord&&string.IsNullOrWhiteSpace(m.QrCode))
    {
     Dispatcher.Invoke(()=>{Status.Text="● Đã kết nối · Chờ QR — bỏ qua kết quả không có mã";Stats.Text="Đã bỏ qua kết quả: chưa có QR hợp lệ.";});
     continue;
    }
   }
   catch(FormatException ex){Dispatcher.Invoke(()=>{Status.Text="● Đã kết nối · Dữ liệu chưa nhận dạng";Raw.Text=ex.Message;});continue;}
   static string Quote(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
   log.WriteLine(string.Join(",",new[]{m.Time.ToString("O"),m.QrCode,m.PassFail,m.LAText,m.LBText,m.CText,m.Compare,m.RA,m.RB,m.Polarity,m.Wire,mode,dual.ToString(),dc.ToString(),m.Raw}.Select(Quote)));
   var hadQr=!string.IsNullOrEmpty(m.QrCode);
   if(hadQr){currentQr="";Interlocked.Exchange(ref _lastQrTicks,0);}
   Dispatcher.Invoke(()=>
   {
    Display(m);
    if(hadQr)
    {
     QrInput.Text="";
     QrState.Text="✓ Đã ghi · Quét mã sản phẩm tiếp theo";
     QrState.Foreground=new SolidColorBrush(Color.FromRgb(8,117,103));
     CamQrResult.Text="—";CamQrResult.Foreground=new SolidColorBrush(Color.FromRgb(7,95,89));
    }
   });
   // Camera tự re-scan qua OnCamCode → không cần reset thủ công tại đây
   int delay=Math.Max(0,interval-(int)pace.ElapsedMilliseconds);
   if(token.WaitHandle.WaitOne(delay))break;
 }
 }
 static string Classify(string raw,string function)
 {
  var v=raw.Split(',').Select(x=>x.Trim()).ToArray();
  if(v.Length<3)return "LỖI DỮ LIỆU";
  if(!function.Contains("TRAN",StringComparison.OrdinalIgnoreCase) && v.Length is 3 or 4)
  {
   if(int.TryParse(v[2],out var status)&&status!=0)return "LỖI PHÉP ĐO";
   if(v.Length==4&&int.TryParse(v[3],out var lcrBin))return lcrBin is >=1 and <=9?"PASS":lcrBin==0?"FAIL / CHƯA SO SÁNH":lcrBin==10?"AUX":"FAIL";
   return "ĐÃ ĐO";
  }
  if(v.Length>=6&&int.TryParse(v[3],out var bin))return bin is >=1 and <=9?"PASS":bin==0?"FAIL / CHƯA SO SÁNH":bin==10?"AUX":"FAIL";
  return "ĐÃ ĐO";
 }
 void Display(Measurement m)
 {
  count++;Status.Text="● LIVE · "+Port.Text;LA.Text=m.DisplayA;LB.Text=m.DisplayB;CValue.Text=m.PassFail;Compare.Text=m.PassFail;
  QrState.Text=string.IsNullOrWhiteSpace(m.QrCode)?"Chưa quét mã":"Mã QR: "+m.QrCode+" · "+m.PassFail;
  if(m.IsLcr)
  {
   var units=Engineering.Units(m.Function);
   PrimaryLabel.Text=units.nameA+" · THÔNG SỐ CHÍNH";
   SecondaryLabel.Text=units.nameB+" · THÔNG SỐ PHỤ";
   CLabel.Text="TRẠNG THÁI MẪU";CValue.Text=m.PassFail;
   if(int.TryParse(m.Compare,out var bin))Compare.Text=m.MeasureStatus!="0"?"Lỗi phép đo":bin is >=1 and <=9?"PASS · BIN "+bin:bin==0?"OUT / chưa so sánh":bin==10?"AUX":"Mã "+bin;
   ChartLegend.Text=units.nameA+" · "+units.unitA;
  }
  else {PrimaryLabel.Text="LA · CUỘN A";SecondaryLabel.Text="LB · CUỘN B";ChartLegend.Text="LA ━ xanh ngọc · LB ━ xanh lam";}
  Wire.Text=$"Cực tính: {m.Polarity} · Dây: "+(m.Wire=="1"?"Bình thường":m.Wire=="2"?"Sai đấu dây":"Tắt");
  DcText.Text=$"RA: {m.DisplayRA} / RB: {m.DisplayRB}";Second.Text=m.Second;
  if(m.IsLcr){Wire.Text="Kết quả phân loại từ thiết bị";DcText.Text="0 = phép đo hợp lệ";Second.Text="Đơn vị tự động · Chế độ Single sẽ chờ lần kích đo tiếp theo";}
  rows.Insert(0,m);if(rows.Count>2000)rows.RemoveAt(rows.Count-1);
  points.Enqueue(m);if(points.Count>150)points.Dequeue();DrawChart();
  Stats.Text=$"{count:N0} mẫu nhận · {count/Math.Max(1,elapsed.Elapsed.TotalSeconds):F1} phản hồi/s · {m.Time:HH:mm:ss.fff} · QR: {(string.IsNullOrWhiteSpace(m.QrCode)?"chưa có":m.QrCode)}";
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
  if(a.Any(x=>Math.Abs(x.LA)>=1e30||Math.Abs(x.LB)>=1e30)){Scale.Text="Có giá trị ngoài dải đo; biểu đồ tạm ẩn.";return;}
  LineA.Points=new PointCollection(a.Select((x,i)=>new Point(i*w/(a.Length-1),h-(x.LA-min)/span*h)));
  if(!lcr)LineB.Points=new PointCollection(a.Select((x,i)=>new Point(i*w/(a.Length-1),h-(x.LB-min)/span*h)));
  var unit=lcr?Engineering.Units(a[^1].Function).unitA:"H";
  Scale.Text=$"Thứ tự mẫu nhận →     Giá trị: {Engineering.Format(min,unit)} … {Engineering.Format(max,unit)}";
 }
 void Chart_SizeChanged(object sender,SizeChangedEventArgs e)=>DrawChart();
 void Export_Click(object sender,RoutedEventArgs e)
 {
  if(sessionPath is null||!File.Exists(sessionPath)){MessageBox.Show(this,"Chưa có phiên đo để xuất.");return;}
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
    MessageBox.Show(this,"Đã xuất dữ liệu CSV.");
   }catch(Exception ex){MessageBox.Show(this,"Không xuất được: "+ex.Message);}
  }
 }
 async void Window_Closing(object? sender,CancelEventArgs e)
 {
  if(closing)return;
  if(running is not null||camTask is not null)
  {
   e.Cancel=true;
   cancellation?.Cancel();camCancel?.Cancel();
   try{if(running is not null)await running;}catch{}
   try{if(camTask is not null)await camTask;}catch{}
   closing=true;Close();
  }
 }
}
