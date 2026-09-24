using System.IO.Ports;
using UC2836Live.Models;

namespace UC2836Live.Services;

/// Owns the single live USB-CDC/COM connection used by the application.
public sealed class CommunicationManager : IDisposable
{
    readonly object gate = new();
    Device? connection;
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public Device? Connection { get { lock (gate) return connection; } }
    public event Action<ConnectionState>? StateChanged;

    public static string[] GetPorts() => SerialPort.GetPortNames().Order().ToArray();

    public Device Connect(string portName, int baudRate)
    {
        lock (gate)
        {
            if (connection is not null) return connection;
            SetState(ConnectionState.Connecting);
            try
            {
                connection = new Device(portName, baudRate);
                SetState(ConnectionState.Connected);
                return connection;
            }
            catch
            {
                SetState(ConnectionState.Error);
                throw;
            }
        }
    }

    public void Disconnect()
    {
        lock (gate)
        {
            if (connection is null) { SetState(ConnectionState.Disconnected); return; }
            SetState(ConnectionState.Disconnecting);
            connection.Dispose();
            connection = null;
            SetState(ConnectionState.Disconnected);
        }
    }

    void SetState(ConnectionState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public void Dispose() => Disconnect();
}
