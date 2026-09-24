using QrCamera.Module;
using UC2836Live.Models;

namespace UC2836Live.Services;

public sealed class CameraService : IDisposable
{
    readonly QrScanner scanner = new();
    CancellationTokenSource? activeCancellation;
    Task? activeTask;
    public bool IsStreaming => activeTask is { IsCompleted: false };
    public ComponentState State { get; private set; } = ComponentState.Disconnected;
    public event Action<ComponentState>? StateChanged;
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
        SetState(ComponentState.Connecting);
        activeCancellation = new CancellationTokenSource();
        CurrentSourceIndex = index;
        activeTask = scanner.RunAsync(index, activeCancellation.Token);
        try { SetState(ComponentState.Streaming); await activeTask; }
        catch { SetState(ComponentState.Error); throw; }
        finally { activeTask = null; activeCancellation.Dispose(); activeCancellation = null; CurrentSourceIndex = null; if (State != ComponentState.Error) SetState(ComponentState.Disconnected); }
    }
    public async Task StopAsync()
    {
        activeCancellation?.Cancel();
        if (activeTask is not null) { try { await activeTask; } catch (OperationCanceledException) { } }
        if (!IsStreaming) SetState(ComponentState.Disconnected);
    }
    void SetState(ComponentState state) { State = state; StateChanged?.Invoke(state); }
    public void ResetCodes() => scanner.ResetCodes();
    public void SetSettings(ScanSettings settings) => scanner.Settings = settings;
    public void Dispose() { }
}
