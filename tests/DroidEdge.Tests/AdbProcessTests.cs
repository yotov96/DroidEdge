using DroidEdge.Phone;
using Serilog.Core;

namespace DroidEdge.Tests;

public class AdbProcessTests
{
    private static Adb Cmd() => new("cmd.exe", null, Logger.None);

    [Fact]
    public async Task Returns_exit_code_and_output()
    {
        var r = await Cmd().RunAsync(CancellationToken.None, "/c", "echo hello & exit 3");
        Assert.Equal(3, r.ExitCode);
        Assert.False(r.Ok);
        Assert.Contains("hello", r.StdOut);
    }

    [Fact]
    public async Task Timeout_kills_process_and_throws_TimeoutException()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => Cmd().RunAsync(TimeSpan.FromMilliseconds(300), CancellationToken.None, "/c", "ping -n 30 127.0.0.1 > nul"));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"took {sw.Elapsed}");
    }

    [Fact]
    public async Task Caller_cancellation_surfaces_as_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource(200);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cmd().RunAsync(TimeSpan.FromSeconds(30), cts.Token, "/c", "ping -n 30 127.0.0.1 > nul"));
    }
}
