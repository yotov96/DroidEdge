using System.Text;
using DroidEdge.Core.Clipboard;
using DroidEdge.Core.Protocol;
using DroidEdge.Phone;
using Serilog;

namespace DroidEdge.App;

/// <summary>Moves text between the Windows clipboard and the phone. Call only on the UI (STA) thread.</summary>
public sealed class ClipboardSync
{
    // Formats that ask clipboard monitors to ignore a clip; KeePass and other password managers set them.
    private static readonly string[] ExclusionFormats = ["ExcludeClipboardContentFromMonitorProcessing", "Clipboard Viewer Ignore"];

    private readonly ClipboardSyncState _state = new();
    private readonly ILogger _log;

    public ClipboardSync(ILogger log) => _log = log;

    public void OnPhoneClipboard(string text)
    {
        try
        {
            Clipboard.SetDataObject(text, copy: true, retryTimes: 10, retryDelay: 50);
            _state.MarkSynced(text);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not set the Windows clipboard from the phone");
        }
    }

    public void OnEnteredPhone(ControlChannel channel)
    {
        string? text;
        bool excluded;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) return;
            excluded = ExclusionFormats.Any(f => data.GetDataPresent(f));
            text = data.GetData(DataFormats.UnicodeText) as string;
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Could not read the Windows clipboard");
            return;
        }

        if (!_state.ShouldPush(text, excluded)) return;
        if (Encoding.UTF8.GetByteCount(text!) > ControlMessages.MaxClipboardTextBytes)
        {
            _log.Information("Clipboard text is too large to send to the phone ({Chars} chars)", text!.Length);
            _state.MarkSynced(text!); // so it is not retried on every crossing
            return;
        }
        if (channel.SendClipboard(text!)) _state.MarkSynced(text!);
    }
}
