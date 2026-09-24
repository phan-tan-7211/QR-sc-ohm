using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QrCamera.Module;
using UC2836Live.Models;
using UC2836Live.Services;

namespace UC2836Live;

public partial class CommunicationWindow : Window
{
    readonly CommunicationManager communication;
    readonly CameraService camera;
    readonly QrDecoderService qr;
    readonly SessionService session;
    readonly CancellationTokenSource lifetime = new();
    PreviewFrame? frame;
    bool entering;
    bool changingLanguage;

    public CommunicationWindow(CommunicationManager communication, CameraService camera, QrDecoderService qr, SessionService session)
    {
        InitializeComponent();
        this.communication = communication; this.camera = camera; this.qr = qr; this.session = session;
        LanguageSelector.SelectedValue = LocalizationManager.CurrentLanguage;
        LocalizationManager.LanguageChanged += RefreshLocalizedRuntimeText;
        communication.StateChanged += state => Dispatcher.Invoke(() => { UcState.Text = "● " + state.ToString().ToUpperInvariant(); UcState.Foreground = state == ConnectionState.Connected ? Brushes.DarkCyan : Brushes.IndianRed; RefreshReadiness(); });
        camera.FrameReady += f => Interlocked.Exchange(ref frame, f);
        camera.CodeRead += code => Dispatcher.Invoke(() => { qr.SetCode(code); LastQr.Text = LocalizationManager.Get("Communication.LastQrPrefix") + code; QrState.Text = "QR Decoder  ● READY"; RefreshReadiness(); });
        camera.StatusChanged += status => Dispatcher.Invoke(() => { CameraState.Text = "● " + status; RefreshReadiness(); });
        camera.StateChanged += state => Dispatcher.Invoke(() => { CameraState.Text = "● " + state.ToString().ToUpperInvariant(); RefreshReadiness(); });
        Loaded += (_, _) => InitializeCommunication();
        Closed += (_, _) => lifetime.Cancel();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        timer.Tick += (_, _) => RenderFrame(); timer.Start();
    }
    static string L(string key) => LocalizationManager.Get(key);
    void RefreshLocalizedRuntimeText() { if (CameraSources is null) return; changingLanguage = true; LanguageSelector.SelectedValue = LocalizationManager.CurrentLanguage; changingLanguage = false; }
    void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!changingLanguage && LanguageSelector.SelectedValue is string code) LocalizationManager.SetLanguage(code); }

    async void InitializeCommunication()
    {
        RefreshPorts();
        try { var sources = camera.ListSources(); CameraSources.ItemsSource = sources; CameraSources.SelectedItem = sources.FirstOrDefault(x => x.Name.Contains("DroidCam", StringComparison.OrdinalIgnoreCase)) ?? sources.FirstOrDefault(); }
        catch (Exception ex) { CameraState.Text = "● ERROR"; UcDetail.Text = ex.Message; }
        try { Directory.CreateDirectory(System.IO.Path.Combine(AppContext.BaseDirectory, "Sessions")); StorageState.Text = "● STORAGE · READY"; }
        catch (Exception ex) { StorageState.Text = "● STORAGE · ERROR: " + ex.Message; }
        RefreshReadiness();
        if (CameraSources.SelectedItem is CameraSource source) _ = StartCamera(source.Index);
        await ConnectSelectedPort();
    }

    void RefreshPorts()
    {
        var selected = Port.Text; Port.ItemsSource = CommunicationManager.GetPorts(); Port.Text = string.IsNullOrWhiteSpace(selected) ? "COM3" : selected;
    }
    void RefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshPorts();

    async Task ConnectSelectedPort()
    {
        try
        {
            if (!Port.Text.StartsWith("COM", StringComparison.OrdinalIgnoreCase)) throw new IOException("Chọn cổng COM hợp lệ.");
            communication.Connect(Port.Text.Trim(), 9600);
            UcDetail.Text = "USB CDC · 9600 8N1 · SCPI";
        }
        catch (Exception ex) { UcState.Text = "● ERROR"; UcDetail.Text = ex.Message; RefreshReadiness(); }
        await Task.CompletedTask;
    }
    async void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (communication.Connection is null) await ConnectSelectedPort();
            if (communication.Connection is not null) { var id = communication.Connection.Query("*IDN?"); UcDetail.Text = id; UcState.Text = id.Contains("UC2836CX", StringComparison.OrdinalIgnoreCase) ? "● CONNECTED" : "● ERROR"; }
        }
        catch (Exception ex) { UcState.Text = "● ERROR"; UcDetail.Text = ex.Message; }
        RefreshReadiness();
    }
    async void Reconnect_Click(object sender, RoutedEventArgs e) { communication.Disconnect(); await ConnectSelectedPort(); }

    async void CameraSources_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || CameraSources.SelectedItem is not CameraSource source) return;
        await camera.StopAsync(); _ = StartCamera(source.Index);
    }
    async Task StartCamera(int index)
    {
        try { CameraState.Text = "● CONNECTING"; QrState.Text = "QR Decoder  ● STARTING"; await camera.StartAsync(index); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CameraState.Text = "● ERROR"; QrState.Text = "QR Decoder  ● ERROR"; UcDetail.Text = ex.Message; RefreshReadiness(); }
    }
    async void CameraRetry_Click(object sender, RoutedEventArgs e) { if (CameraSources.SelectedItem is CameraSource source) { await camera.StopAsync(); _ = StartCamera(source.Index); } }

    void RenderFrame()
    {
        var f = Interlocked.Exchange(ref frame, null);
        if (f is null) return;
        var bitmap = BitmapSource.Create(f.Width, f.Height, 96, 96, PixelFormats.Bgr24, null, f.Bgr, f.Stride); bitmap.Freeze(); Preview.Source = bitmap; PreviewPlaceholder.Visibility = Visibility.Collapsed;
        CameraState.Text = "● STREAMING"; QrState.Text = "QR Decoder  ● READY"; RefreshReadiness();
    }
    void RefreshReadiness()
    {
        var uc = communication.State == ConnectionState.Connected;
        var cam = camera.State is ComponentState.Streaming or ComponentState.Ready && Preview.Source is not null;
        var storage = StorageState.Text.Contains("READY", StringComparison.OrdinalIgnoreCase);
        var qrReady = cam;
        Readiness.Text = $"UC2836 {(uc ? "✓" : "○")}   CAMERA {(cam ? "✓" : "○")}   QR {(qrReady ? "✓" : "○")}   STORAGE {(storage ? "✓" : "○")}";
        var ready = uc && cam && qrReady && storage;
        EnterSystem.IsEnabled = ready && !entering;
        SystemState.Text = ready ? LocalizationManager.Get("Communication.SystemReady") : LocalizationManager.Get("Communication.SystemNotReady");
        SystemState.Foreground = ready ? Brushes.DarkCyan : Brushes.IndianRed;
    }
    void EnterSystem_Click(object sender, RoutedEventArgs e)
    {
        if (entering) return; entering = true; RefreshReadiness();
        var main = new MainWindow(communication, camera, qr, session); Application.Current.MainWindow = main; main.Show(); Hide();
    }
}
