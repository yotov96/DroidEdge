using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace PhoneEdge.Phone;

public sealed class ServerConnection : IDisposable
{
    private readonly Adb _adb;
    private readonly ILogger _log;
    private readonly TcpClient _client;
    private bool _disposed;

    internal ServerConnection(Adb adb, ILogger log, Process serverProcess, TcpClient client, int localPort, uint scid, string deviceName)
    {
        _adb = adb;
        _log = log;
        ServerProcess = serverProcess;
        _client = client;
        Stream = client.GetStream();
        LocalPort = localPort;
        Scid = scid;
        DeviceName = deviceName;
    }

    public Process ServerProcess { get; }
    public NetworkStream Stream { get; }
    public int LocalPort { get; }
    public uint Scid { get; }
    public string DeviceName { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Stream.Close(); } catch { }
        try { _client.Close(); } catch { }
        try { _adb.RunAsync(TimeSpan.FromSeconds(3), CancellationToken.None, "forward", "--remove", $"tcp:{LocalPort}").GetAwaiter().GetResult(); } catch (Exception ex) { _log.Debug(ex, "forward --remove failed"); }
        try { if (!ServerProcess.HasExited) ServerProcess.Kill(entireProcessTree: true); } catch { }
        try { ServerProcess.Dispose(); } catch { }
    }
}

public static class ServerBootstrap
{
    public const string ServerVersion = "4.1";
    public const string DeviceServerPath = "/data/local/tmp/scrcpy-server.jar";
    private const int DeviceNameFieldLength = 64;

    public static async Task<ServerConnection> StartAsync(Adb adb, string serverJarPath, ILogger log, CancellationToken ct)
    {
        if (!File.Exists(serverJarPath)) throw new FileNotFoundException("scrcpy-server not found", serverJarPath);

        // 1. Kill a stale server from a previous crash (best effort).
        await adb.ShellAsync(ct, "pkill", "-f", "com.genymobile.scrcpy");

        // 2. Push the server.
        var push = await adb.RunAsync(TimeSpan.FromSeconds(30), ct, "push", serverJarPath, DeviceServerPath);
        if (!push.Ok) throw new InvalidOperationException($"adb push failed: {push}");

        // 3. Forward a local port to the server's abstract socket.
        uint scid = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
        string socketName = $"scrcpy_{scid:x8}";
        int port = FreePort();
        var fwd = await adb.RunAsync(ct, "forward", $"tcp:{port}", $"localabstract:{socketName}");
        if (!fwd.Ok) throw new InvalidOperationException($"adb forward failed: {fwd}");

        // 4. Launch the server.
        Process proc;
        try
        {
            proc = adb.StartLongRunning("shell",
                $"CLASSPATH={DeviceServerPath}", "app_process", "/", "com.genymobile.scrcpy.Server", ServerVersion,
                $"scid={scid:x8}", "log_level=info", "video=false", "audio=false", "tunnel_forward=true", "clipboard_autosync=false");
        }
        catch
        {
            await adb.RunAsync(ct, "forward", "--remove", $"tcp:{port}");
            throw;
        }
        PumpOutput(proc, log);

        // 5. Connect; the forward accepts before the server listens, so retry until the dummy byte arrives.
        TcpClient? client = null;
        string deviceName = "";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (proc.HasExited) throw new InvalidOperationException($"scrcpy server exited early with code {proc.ExitCode}; see log for [server] lines");
            var c = new TcpClient();
            try
            {
                await c.ConnectAsync(IPAddress.Loopback, port, ct);
                c.NoDelay = true;
                var s = c.GetStream();
                var one = new byte[1];
                using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    var remaining = deadline - DateTime.UtcNow;
                    readCts.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1));
                    int n = await s.ReadAsync(one, readCts.Token);
                    if (n != 1) throw new IOException("server not ready (EOF before dummy byte)");
                }
                if (one[0] != 0) throw new IOException($"unexpected first byte 0x{one[0]:X2}");
                var name = new byte[DeviceNameFieldLength];
                await s.ReadExactlyAsync(name, ct);
                int len = Array.IndexOf(name, (byte)0);
                deviceName = Encoding.UTF8.GetString(name, 0, len < 0 ? name.Length : len);
                client = c;
                break;
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException && !ct.IsCancellationRequested)
            {
                last = ex;
                c.Dispose();
                await Task.Delay(100, ct);
            }
        }
        if (client is null)
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            await adb.RunAsync(ct, "forward", "--remove", $"tcp:{port}");
            throw new TimeoutException($"Could not connect to scrcpy server on port {port}: {last?.Message}");
        }
        log.Information("Connected to scrcpy server on {Device} (scid {Scid}, port {Port})", deviceName, $"{scid:x8}", port);
        return new ServerConnection(adb, log, proc, client, port, scid, deviceName);
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static void PumpOutput(Process p, ILogger log)
    {
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) log.Information("[server] {Line}", e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) log.Warning("[server] {Line}", e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
    }
}
