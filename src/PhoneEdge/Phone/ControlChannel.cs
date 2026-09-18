using PhoneEdge.Core.Hid;
using PhoneEdge.Core.Input;
using PhoneEdge.Core.Protocol;
using Serilog;

namespace PhoneEdge.Phone;

/// <summary>Owns the control socket: one sender thread draining the ReportQueue, one receiver thread draining device messages.</summary>
public sealed class ControlChannel : IReportSink, IDisposable
{
    private readonly ServerConnection _conn;
    private readonly ILogger _log;
    private readonly ReportQueue _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private Thread? _sender, _receiver;
    private int _lostSignalled;
    private bool _disposed;

    public ControlChannel(ServerConnection conn, ILogger log)
    {
        _conn = conn;
        _log = log;
    }

    public event Action<Exception?>? ConnectionLost;
    public bool IsAlive => _lostSignalled == 0 && !_disposed;

    public void Start()
    {
        _sender = new Thread(SenderLoop) { Name = "scrcpy-sender", IsBackground = true };
        _receiver = new Thread(ReceiverLoop) { Name = "scrcpy-receiver", IsBackground = true };
        _sender.Start();
        _receiver.Start();
        _conn.ServerProcess.Exited += (_, _) => SignalLost(new InvalidOperationException("scrcpy server process exited"));
    }

    public bool SendMouse(byte[] report) => _queue.EnqueueMouse(report);
    public bool SendKeyboard(byte[] report) => _queue.EnqueueKeyboard(report);
    public void SendRaw(byte[] framed) => _queue.EnqueueRaw(framed);

    public void CreateDevices()
    {
        SendRaw(ControlMessages.UhidCreate(HidDescriptors.MouseId, 0, 0, HidDescriptors.MouseName, HidDescriptors.Mouse));
        SendRaw(ControlMessages.UhidCreate(HidDescriptors.KeyboardId, 0, 0, HidDescriptors.KeyboardName, HidDescriptors.Keyboard));
    }

    public void DestroyDevices()
    {
        SendRaw(ControlMessages.UhidDestroy(HidDescriptors.KeyboardId));
        SendRaw(ControlMessages.UhidDestroy(HidDescriptors.MouseId));
    }

    private void SenderLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var msg = _queue.Take(_cts.Token);
                if (msg is null) return;
                _conn.Stream.Write(msg, 0, msg.Length);
            }
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            SignalLost(ex);
        }
    }

    private void ReceiverLoop()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var msg = DeviceMessageReader.Read(_conn.Stream);
                if (msg is null) { SignalLost(new IOException("server closed the control socket")); return; }
                _log.Debug("Device message: {Msg}", msg);
            }
        }
        catch (Exception ex) when (!_cts.IsCancellationRequested)
        {
            SignalLost(ex);
        }
    }

    private void SignalLost(Exception? ex)
    {
        if (Interlocked.Exchange(ref _lostSignalled, 1) != 0) return;
        _log.Warning(ex, "Control channel lost");
        ConnectionLost?.Invoke(ex);
    }

    /// <summary>Give the sender up to <paramref name="drain"/> to flush queued messages (e.g. UHID destroy), then tear down.</summary>
    public void Dispose(TimeSpan drain)
    {
        if (_disposed) return;
        _disposed = true;
        var until = DateTime.UtcNow + drain;
        while (_queue.Count > 0 && DateTime.UtcNow < until && _lostSignalled == 0) Thread.Sleep(10);
        _cts.Cancel();
        _conn.Dispose();
        _sender?.Join(1000);
        _receiver?.Join(1000);
        _cts.Dispose();
    }

    public void Dispose() => Dispose(TimeSpan.FromMilliseconds(500));
}
