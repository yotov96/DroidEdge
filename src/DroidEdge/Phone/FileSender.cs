using DroidEdge.Core.Files;
using Serilog;

namespace DroidEdge.Phone;

public sealed record FileSendResult(int Sent, IReadOnlyList<string> Failed);

/// <summary>Pushes local files and folders to the phone's Download folder without overwriting, then asks the phone to index them.</summary>
public static class FileSender
{
    public const string TargetDir = "/sdcard/Download/";
    private static readonly TimeSpan PushTimeout = TimeSpan.FromHours(1);

    public static async Task<FileSendResult> SendAsync(PhoneConnection phone, IReadOnlyList<string> localPaths, ILogger log, CancellationToken ct)
    {
        // Shared storage compares names case-insensitively, so "Photo.jpg" would overwrite "photo.jpg".
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var listing = await phone.Adb.ShellAsync(ct, "ls", "-1a", TargetDir);
        foreach (var line in listing.StdOut.Split('\n'))
        {
            var name = line.TrimEnd('\r');
            if (name.Length > 0) existing.Add(name);
        }

        int sent = 0;
        var failed = new List<string>();
        foreach (var path in localPaths)
        {
            var local = Path.TrimEndingDirectorySeparator(path);
            bool isDir = Directory.Exists(local);
            if (!isDir && !File.Exists(local))
            {
                failed.Add($"{Path.GetFileName(local)} (not found)");
                continue;
            }

            string remoteName = UniqueFileName.Pick(Path.GetFileName(local), existing);
            existing.Add(remoteName);
            string remote = TargetDir + remoteName;
            var push = await phone.Adb.RunAsync(PushTimeout, ct, "push", local, remote);
            if (!push.Ok)
            {
                failed.Add($"{Path.GetFileName(local)} ({push.StdErr.Trim()})");
                continue;
            }
            sent++;
            log.Information("Pushed {Local} to {Remote}", local, remote);

            var pushedFiles = isDir
                ? Directory.EnumerateFiles(local, "*", SearchOption.AllDirectories).Select(f => remote + "/" + Path.GetRelativePath(local, f).Replace('\\', '/'))
                : [remote];
            foreach (var devicePath in pushedFiles)
            {
                try { phone.Channel.SendScanFile(devicePath); }
                catch (ArgumentException) { log.Debug("Not indexing {Path}: path too long for the scan message", devicePath); }
            }
        }
        return new FileSendResult(sent, failed);
    }
}
