using System.Text;
using System.IO;

namespace UC2836Live.Services;

public sealed class SessionService : IDisposable
{
    StreamWriter? writer;
    public string? Path { get; private set; }
    public void Start(string baseDirectory)
    {
        Dispose();
        var dir = System.IO.Path.Combine(baseDirectory, "Sessions");
        Directory.CreateDirectory(dir);
        Path = System.IO.Path.Combine(dir, $"UC2836_{DateTime.Now:yyyyMMdd_HHmmss_fff}.csv");
        writer = new StreamWriter(new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true)) { AutoFlush = true };
        writer.WriteLine("Time,QRCode,PassFail,LA,LB,C,Compare,RA,RB,Polarity,Wire,Mode,Dual,DCR,Raw");
    }
    public void Append(Measurement m, string mode, bool dual, bool dcr)
    {
        if (writer is null) throw new InvalidOperationException("Session chưa được mở.");
        static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        writer.WriteLine(string.Join(",", new[] { m.Time.ToString("O"), m.QrCode, m.PassFail, m.LAText, m.LBText, m.CText, m.Compare, m.RA, m.RB, m.Polarity, m.Wire, mode, dual.ToString(), dcr.ToString(), m.Raw }.Select(Quote)));
    }
    public void Dispose() { writer?.Dispose(); writer = null; }
}
