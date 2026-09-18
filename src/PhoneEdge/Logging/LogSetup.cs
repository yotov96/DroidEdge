using Serilog;
using Serilog.Events;

namespace PhoneEdge.Logging;

public static class LogSetup
{
    public static ILogger Create(string directory, string level)
    {
        var min = level.Trim().ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "warn" or "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => LogEventLevel.Information,
        };
        return new LoggerConfiguration()
            .MinimumLevel.Is(min)
            .WriteTo.File(Path.Combine(directory, "phoneedge.log"),
                fileSizeLimitBytes: 5 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 3,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
