namespace UC2836Live.Services;

public sealed class QrDecoderService
{
    readonly object gate = new();
    string code = "";
    DateTimeOffset lastRead;
    public string CurrentCode { get { lock (gate) return code; } }
    public event Action<string>? CodeChanged;
    public void SetCode(string value)
    {
        value = value.Trim();
        if (value.Length == 0) return;
        lock (gate) { code = value; lastRead = DateTimeOffset.Now; }
        CodeChanged?.Invoke(value);
    }
    public string GetFreshCode(double maxAgeSeconds)
    {
        lock (gate) return code.Length > 0 && (DateTimeOffset.Now - lastRead).TotalSeconds <= maxAgeSeconds ? code : "";
    }
    public bool IsExpired(double maxAgeSeconds)
    {
        lock (gate) return code.Length > 0 && (DateTimeOffset.Now - lastRead).TotalSeconds > maxAgeSeconds;
    }
    public void Clear() { lock (gate) { code = ""; lastRead = default; } }
}
