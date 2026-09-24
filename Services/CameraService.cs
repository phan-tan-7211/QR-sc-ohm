using QrCamera.Module;

namespace UC2836Live.Services;

public sealed class CameraService : IDisposable
{
    readonly QrScanner scanner = new();
    CancellationTokenSource? activeCancellation;
    Task? activeTask;
    public bool IsStreaming => activeTask is { IsCompleted: false };
    public int? CurrentSourceIndex { get; private set; }
    public CameraService() => scanner.CodeRead += r => CodeRead?.Invoke(r.Code);
    public event Action<PreviewFrame>? FrameReady { add => scanner.FrameReady += value; remove => scanner.FrameReady -= value; }
    public event Action<string>? CodeRead;
    public event Action<string>? StatusChanged { add => scanner.StatusChanged += value; remove => scanner.StatusChanged -= value; }
    public QrScanner Scanner => scanner;
    public IReadOnlyList<CameraSource> ListSources() => CameraCatalog.List();
    public Task RunAsync(int index, CancellationToken token) => scanner.RunAsync(index, token);
    public async Task StartAsync(int index)
    {
        if (IsStreaming) return;
        activeCancellation = new CancellationTokenSource();
        CurrentSourceIndex = index;
        activeTask = scanner.RunAsync(index, activeCancellation.Token);
        try { await activeTask; }
        finally { activeTask = null; activeCancellation.Dispose(); activeCancellation = null; CurrentSourceIndex = null; }
    }
    public async Task StopAsync()
    {
        activeCancellation?.Cancel();
        if (activeTask is not null) { try { await activeTask; } catch (OperationCanceledException) { } }
    }
    public void ResetCodes() => scanner.ResetCodes();
    public void SetSettings(ScanSettings settings) => scanner.Settings = settings;
    public void Dispose() { }
}
