namespace DroidEdge.Core.Clipboard;

/// <summary>
/// Decides whether the PC clipboard should be sent to the phone. Remembers the last text that is known to be
/// identical on both sides (pushed to the phone or received from it) so nothing is sent twice or echoed back.
/// </summary>
public sealed class ClipboardSyncState
{
    private string? _lastSynced;

    /// <param name="excluded">The clip carries a format asking clipboard monitors to ignore it (password managers).</param>
    public bool ShouldPush(string? pcText, bool excluded) =>
        !excluded && !string.IsNullOrEmpty(pcText) && pcText != _lastSynced;

    public void MarkSynced(string text) => _lastSynced = text;
}
