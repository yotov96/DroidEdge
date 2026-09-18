using System.Diagnostics;
using Serilog;

namespace PhoneEdge.Phone;

public sealed record AdbResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Ok => ExitCode == 0;
    public override string ToString() => $"exit={ExitCode} out='{StdOut.Trim()}' err='{StdErr.Trim()}'";
}

public sealed class Adb
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private readonly ILogger _log;

    public Adb(string adbPath, string? serial, ILogger log)
    {
        AdbPath = adbPath;
        Serial = serial;
        _log = log;
    }

    public string AdbPath { get; }
    public string? Serial { get; }

    public Task<AdbResult> RunAsync(CancellationToken ct, params string[] args) => RunAsync(DefaultTimeout, ct, args);

    public Task<AdbResult> ShellAsync(CancellationToken ct, params string[] args) => RunAsync(DefaultTimeout, ct, ["shell", .. args]);

    public async Task<AdbResult> RunAsync(TimeSpan timeout, CancellationToken ct, params string[] args)
    {
        using var p = new Process { StartInfo = BuildStartInfo(args) };
        _log.Debug("adb {Args}", string.Join(' ', args));
        p.Start();
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw new TimeoutException($"adb {string.Join(' ', args)} timed out after {timeout.TotalSeconds:0}s");
        }
        var result = new AdbResult(p.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        if (!result.Ok) _log.Warning("adb {Args} failed: {Result}", string.Join(' ', args), result);
        return result;
    }

    /// <summary>Start an adb command that keeps running (the scrcpy server). Caller owns the process.</summary>
    public Process StartLongRunning(params string[] args)
    {
        var p = new Process { StartInfo = BuildStartInfo(args), EnableRaisingEvents = true };
        _log.Information("adb (long-running) {Args}", string.Join(' ', args));
        p.Start();
        return p;
    }

    private ProcessStartInfo BuildStartInfo(string[] args)
    {
        var psi = new ProcessStartInfo(AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (Serial is not null)
        {
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add(Serial);
        }
        foreach (var a in args) psi.ArgumentList.Add(a);
        return psi;
    }
}
