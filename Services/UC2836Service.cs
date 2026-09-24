using System.Globalization;
using System.IO;
using UC2836Live.Models;

namespace UC2836Live.Services;

public sealed record UC2836Configuration(
    string Identity, string Mode, bool Dual, bool Dcr, string Function,
    string Page, string Frequency, string Voltage, string Speed, string Trigger);

/// Protocol facade. The USB CDC transport remains Device/SCPI; UI code never opens COM.
public sealed class UC2836Service
{
    public Device Connection { get; }
    public UC2836Service(Device connection) => Connection = connection;

    public UC2836Configuration ReadConfiguration()
    {
        var identity = Ask("*IDN?");
        if (!identity.Contains("UC2836CX", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Thiết bị không phải UC2836CX: " + identity);
        var mode = Ask(":TRAN:MODE?");
        var dual = Ask(":TRAN:DF?") == "1";
        var dcr = Ask(":TRAN:DCR?") == "1";
        return new(identity, mode, dual, dcr, Ask("FUNC:IMP?").ToUpperInvariant(),
            Ask("DISP:PAGE?").Replace(" ", "").ToUpperInvariant(), Ask("FREQ?"),
            Ask("VOLT?"), Ask("APER?"), Ask("TRIG:SOUR?"));
    }

    public string Ask(string command) => Connection.Query(command);
    public void FlushStaleResult() => Connection.FlushStaleResult();
    public void RequestMeasurement() => Connection.RequestMeasurement();
    public string ReadMeasurement() => Connection.ReadMeasurement();

    public MeasurementResult ParseMeasurement(string response, UC2836Configuration config, string qrCode, string portName)
    {
        var m = Measurement.Parse(response, config.Dual, config.Dcr) with
        {
            Function = config.Function,
            QrCode = qrCode,
            PassFail = Classify(response, config.Function)
        };
        return new MeasurementResult(m, portName, DateTime.Now);
    }

    static string Classify(string raw, string function)
    {
        var v = raw.Split(',').Select(x => x.Trim()).ToArray();
        if (v.Length < 3) return "LỖI DỮ LIỆU";
        if (!function.Contains("TRAN", StringComparison.OrdinalIgnoreCase) && v.Length is 3 or 4)
        {
            if (int.TryParse(v[2], out var status) && status != 0) return "LỖI PHÉP ĐO";
            if (v.Length == 4 && int.TryParse(v[3], out var bin)) return bin is >= 1 and <= 9 ? "PASS" : bin == 0 ? "FAIL / CHƯA SO SÁNH" : bin == 10 ? "AUX" : "FAIL";
            return "ĐÃ ĐO";
        }
        if (v.Length >= 6 && int.TryParse(v[3], out var lcrBin)) return lcrBin is >= 1 and <= 9 ? "PASS" : lcrBin == 0 ? "FAIL / CHƯA SO SÁNH" : lcrBin == 10 ? "AUX" : "FAIL";
        return "ĐÃ ĐO";
    }
}
