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
    private static readonly TimeSpan RawEnqueueTimeout = TimeSpan.FromMilliseconds(250);
    private Thread? _sender, _receiver;
    private int _lostSignalled;
    private bool _disposed;
    private volatile bool _sending;

    public ControlChannel(ServerConnection conn, ILogger log)
    {
        _conn = conn;
        _log = log;
    }

    public event Action<Exception?>? ConnectionLost;
    /// <summary>The phone clipboard changed (only sent when the server runs with clipboard autosync). Raised on the receiver thread.</summary>
    public event Action<string>? ClipboardChanged;
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
    // Called from the input path, so these must not stall: give up quickly if the queue is full.
    private static readonly TimeSpan InputPathTimeout = TimeSpan.FromMilliseconds(20);
    public bool SendBack(bool down) => _queue.EnqueueRaw(ControlMessages.BackOrScreenOn(down), InputPathTimeout);
    public bool SendPower(bool down) => _queue.EnqueueRaw(ControlMessages.InjectKeycode(down, ControlMessages.KeycodePower), InputPathTimeout);
    public void SendRaw(byte[] framed)
    {
        if (!_queue.EnqueueRaw(framed, RawEnqueueTimeout))
            _log.Warning("Control message dropped: queue full for {Ms} ms", RawEnqueueTimeout.TotalMilliseconds);
    }

    /// <summary>Caller checks the text against <see cref="ControlMessages.MaxClipboardTextBytes"/>.</summary>
    public bool SendClipboard(string text) => _queue.EnqueueRaw(ControlMessages.SetClipboard(0, text, paste: false), RawEnqueueTimeout);

    public bool SendScanFile(string devicePath) => _queue.EnqueueRaw(ControlMessages.ScanFile(devicePath), RawEnqueueTimeout);

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
                _sending = true;
                try { _conn.Stream.Write(msg, 0, msg.Length); }
                finally { _sending = false; }
            }
        }
        catch (Exception ex)
        {
            // Never let an exception escape a background thread; a failure during our own teardown is expected noise.
            if (!_cts.IsCancellationRequested) SignalLost(ex);
            else _log.Debug(ex, "Sender stopped during dispose");
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
                if (msg is ClipboardMessage clip)
                {
                    _log.Debug("Phone clipboard changed ({Chars} chars)", clip.Text.Length); // never log the text itself
                    // A throwing subscriber must not end up in the catch below, which would drop the connection.
                    try { ClipboardChanged?.Invoke(clip.Text); }
                    catch (Exception ex) { _log.Error(ex, "ClipboardChanged subscriber threw"); }
                }
                else
                {
                    _log.Debug("Device message: {Msg}", msg);
                }
            }
        }
        catch (Exception ex)
        {
            if (!_cts.IsCancellationRequested) SignalLost(ex);
            else _log.Debug(ex, "Receiver stopped during dispose");
        }
    }

    private void SignalLost(Exception? ex)
    {
        if (Interlocked.Exchange(ref _lostSignalled, 1) != 0) return;
        if (_disposed) _log.Debug(ex, "Control channel lost (during dispose)");
        else _log.Warning(ex, "Control channel lost");
        ConnectionLost?.Invoke(ex);
    }

    /// <summary>Give the sender up to <paramref name="drain"/> to flush queued messages (e.g. UHID destroy), then tear down.</summary>
    public void Dispose(TimeSpan drain)
    {
        if (_disposed) return;
        _disposed = true;
        var until = DateTime.UtcNow + drain;
        while ((_queue.Count > 0 || _sending) && DateTime.UtcNow < until && _lostSignalled == 0) Thread.Sleep(10);
        _cts.Cancel();
        _conn.Dispose();
        _sender?.Join(1000);
        _receiver?.Join(1000);
        _cts.Dispose();
    }

    public void Dispose() => Dispose(TimeSpan.FromMilliseconds(500));
}
