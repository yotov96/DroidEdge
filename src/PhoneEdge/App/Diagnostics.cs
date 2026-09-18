using System.Text;
using PhoneEdge.Core.Config;
using PhoneEdge.Core.Hid;
using PhoneEdge.Core.Input;
using PhoneEdge.Phone;
using Serilog;

namespace PhoneEdge.App;

/// <summary>End-to-end check against the attached phone: bootstrap, create HID devices, drive the cursor to each corner, scroll, tear down.</summary>
public static class Diagnostics
{
    public static async Task<string> RunAsync(AppConfig cfg, ILogger log, CancellationToken ct)
    {
        var sb = new StringBuilder();
        bool ok = true;
        void Step(string name, bool pass, string detail = "")
        {
            ok &= pass;
            sb.AppendLine($"{(pass ? "ok  " : "FAIL")} {name}{(detail.Length > 0 ? ": " + detail : "")}");
        }

        var adbPath = Path.Combine(cfg.ScrcpyDir, "adb.exe");
        var jar = Path.Combine(cfg.ScrcpyDir, "scrcpy-server");
        Step("adb.exe present", File.Exists(adbPath), adbPath);
        Step("scrcpy-server present", File.Exists(jar), jar);
        if (!ok) return Finish(sb, ok);

        var probe = new Adb(adbPath, null, log);
        var devices = AdbParsers.ParseDevices((await probe.RunAsync(ct, "devices")).StdOut);
        var dev = devices.FirstOrDefault(d => d.State == "device" && (cfg.DeviceSerial is null || d.Serial == cfg.DeviceSerial));
        Step("device attached and authorized", dev is not null, string.Join(", ", devices.Select(d => $"{d.Serial}:{d.State}")));
        if (dev is null) return Finish(sb, ok);

        var adb = new Adb(adbPath, dev.Serial, log);
        var settings = new AndroidSettings(adb, log);
        ControlChannel? channel = null;
        try
        {
            var info = await settings.GetDisplayInfoAsync(ct);
            Step("display info", true, $"{info.Width}x{info.Height} rotation {info.Rotation}");

            var conn = await ServerBootstrap.StartAsync(adb, jar, log, ct);
            Step("server bootstrap", true, $"device '{conn.DeviceName}' port {conn.LocalPort}");
            channel = new ControlChannel(conn, log);
            channel.Start();
            channel.CreateDevices();
            await Task.Delay(700, ct);
            Step("UHID devices created", channel.IsAlive);

            await settings.ApplyPointerSettingsAsync(ct);
            Step("pointer acceleration disabled", true);

            (int x, int y)[] corners = [(0, 0), (info.Width - 1, 0), (info.Width - 1, info.Height - 1), (0, info.Height - 1), (info.Width / 2, info.Height / 2)];
            foreach (var (x, y) in corners)
            {
                foreach (var (dx, dy) in MoveChunker.Resync(info.Width, info.Height, x, y))
                    channel.SendMouse(MouseReport.Encode(HidMouseButtons.None, dx, dy, 0, 0));
                await Task.Delay(500, ct);
            }
            Step("cursor visited four corners and center (watch the phone)", channel.IsAlive);

            channel.SendMouse(MouseReport.Encode(HidMouseButtons.None, 0, 0, 1, 0));
            await Task.Delay(200, ct);
            channel.SendMouse(MouseReport.Encode(HidMouseButtons.None, 0, 0, -1, 0));
            await Task.Delay(300, ct);
            Step("wheel tick sent", channel.IsAlive);

            channel.DestroyDevices();
            await Task.Delay(300, ct);
            Step("UHID devices destroyed", channel.IsAlive);
        }
        catch (Exception ex)
        {
            Step("exception", false, ex.Message);
            log.Error(ex, "Diagnostics failed");
        }
        finally
        {
            try { await settings.RestoreAsync(CancellationToken.None); Step("pointer settings restored", true); }
            catch (Exception ex) { Step("pointer settings restored", false, ex.Message); }
            channel?.Dispose();
        }
        return Finish(sb, ok);
    }

    private static string Finish(StringBuilder sb, bool ok) => (ok ? "PASS" : "FAIL") + Environment.NewLine + sb;
}
