using QrCamera.Module;

namespace UC2836Live.Services;

public sealed class CameraService : IDisposable
{
    readonly QrScanner scanner = new();
    public CameraService() => scanner.CodeRead += r => CodeRead?.Invoke(r.Code);
    public event Action<PreviewFrame>? FrameReady { add => scanner.FrameReady += value; remove => scanner.FrameReady -= value; }
    public event Action<string>? CodeRead;
    public event Action<string>? StatusChanged { add => scanner.StatusChanged += value; remove => scanner.StatusChanged -= value; }
    public QrScanner Scanner => scanner;
    public IReadOnlyList<CameraSource> ListSources() => CameraCatalog.List();
    public Task RunAsync(int index, CancellationToken token) => scanner.RunAsync(index, token);
    public void ResetCodes() => scanner.ResetCodes();
    public void SetSettings(ScanSettings settings) => scanner.Settings = settings;
    public void Dispose() { }
}
