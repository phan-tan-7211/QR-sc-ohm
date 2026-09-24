using System.Windows;

namespace UC2836Live;
public partial class DevSettingsWindow : Window
{
 public double QrTimeout { get; private set; }
 public int RescanDelayMs { get; private set; }
 public bool RequireQr { get; private set; }
 public DevSettingsWindow(double qrTimeout,int rescanDelayMs,bool requireQr)
 {
  InitializeComponent();
  TbTimeout.Text=qrTimeout.ToString("F1");
  TbRescan.Text=rescanDelayMs.ToString();
  CbRequireQr.IsChecked=requireQr;
 }
 void Apply_Click(object sender,RoutedEventArgs e)
 {
  if(!double.TryParse(TbTimeout.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var t)||t<=0.1)
  {ErrMsg.Text="Timeout phải là số dương (vd: 2.0)";ErrMsg.Visibility=Visibility.Visible;return;}
  if(!int.TryParse(TbRescan.Text,out var r)||r<200||r>10000)
  {ErrMsg.Text="Re-scan gate: 200–10000 ms";ErrMsg.Visibility=Visibility.Visible;return;}
  QrTimeout=t;RescanDelayMs=r;RequireQr=CbRequireQr.IsChecked==true;DialogResult=true;
 }
 void Cancel_Click(object sender,RoutedEventArgs e)=>DialogResult=false;
}
