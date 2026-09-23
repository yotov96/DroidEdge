namespace PhoneEdge.Win;

/// <summary>
/// A small always-on-top window at the phone edge that accepts dropped files. Shown while a mouse button is held
/// against the edge; hides itself after a drop or once the button is released.
/// </summary>
public sealed class DropZoneWindow : Form
{
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;   // no taskbar button or Alt+Tab entry
    private const int WS_EX_NOACTIVATE = 0x08000000;   // appearing must not take focus from the drag source

    private readonly System.Windows.Forms.Timer _releaseCheck = new() { Interval = 150 };
    private readonly Font _font = new("Segoe UI", 11f, FontStyle.Bold);
    private bool _dragOver;

    public event Action<string[]>? FilesDropped;

    public DropZoneWindow()
    {
        Text = "PhoneEdge drop zone";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AllowDrop = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(31, 111, 235);
        Opacity = 0.92;

        _releaseCheck.Tick += (_, _) => { if (MouseButtons == MouseButtons.None) HideZone(); };
        DragEnter += (_, e) =>
        {
            _dragOver = e.Data?.GetDataPresent(DataFormats.FileDrop) == true;
            e.Effect = _dragOver ? DragDropEffects.Copy : DragDropEffects.None;
            Invalidate();
        };
        DragLeave += (_, _) => { _dragOver = false; Invalidate(); };
        DragDrop += (_, e) =>
        {
            var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
            HideZone();
            if (files is { Length: > 0 }) FilesDropped?.Invoke(files);
        };
    }

    /// <summary>Zone size in physical pixels for a monitor scale factor (1.0 = 96 dpi).</summary>
    public static Size ZoneSize(float scale) => new((int)(220 * scale), (int)(150 * scale));

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void ShowAt(Rectangle bounds)
    {
        if (Visible) return;
        Bounds = bounds;
        Show();
        Bounds = bounds; // again after showing: a DPI change on first show may have rescaled the window
        _releaseCheck.Start();
    }

    private void HideZone()
    {
        _releaseCheck.Stop();
        _dragOver = false;
        Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var text = _dragOver ? "Release to send\nto the phone" : "Drop files here\nto send to the phone";
        TextRenderer.DrawText(e.Graphics, text, _font, ClientRectangle, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _releaseCheck.Dispose();
            _font.Dispose();
        }
        base.Dispose(disposing);
    }
}
