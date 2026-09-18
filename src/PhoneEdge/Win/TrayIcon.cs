using System.Drawing.Drawing2D;

namespace PhoneEdge.Win;

public enum TrayColor { Grey, Green, Blue, Red }

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _status;
    private readonly ToolStripMenuItem _startup;
    private readonly Dictionary<TrayColor, Icon> _icons = new();

    public TrayIcon(Action onReturnMouse, Action onDiagnostics, Action onOpenLog, Action onOpenConfig, Action<bool> onStartupToggle, Action onExit)
    {
        UiControl = new Control();
        _ = UiControl.Handle; // force creation on this (UI) thread so BeginInvoke works from other threads

        foreach (TrayColor c in Enum.GetValues<TrayColor>()) _icons[c] = MakeIcon(c);

        _status = new ToolStripMenuItem("Starting…") { Enabled = false };
        _startup = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
        _startup.CheckedChanged += (_, _) => onStartupToggle(_startup.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_status);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Return mouse to PC", null, (_, _) => onReturnMouse());
        menu.Items.Add("Run diagnostics", null, (_, _) => onDiagnostics());
        menu.Items.Add("Open log", null, (_, _) => onOpenLog());
        menu.Items.Add("Open config", null, (_, _) => onOpenConfig());
        menu.Items.Add(_startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        _icon = new NotifyIcon { Icon = _icons[TrayColor.Grey], Text = "PhoneEdge", Visible = true, ContextMenuStrip = menu };
    }

    public Control UiControl { get; }

    public void SetStatus(string text, TrayColor color)
    {
        _status.Text = text;
        _icon.Icon = _icons[color];
        _icon.Text = ("PhoneEdge - " + text).Length > 63 ? ("PhoneEdge - " + text)[..63] : "PhoneEdge - " + text;
    }

    public void Balloon(string title, string text, ToolTipIcon icon) => _icon.ShowBalloonTip(3000, title, text, icon);

    public void SetStartupChecked(bool checked_) => _startup.Checked = checked_;

    private static Icon MakeIcon(TrayColor c)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var fill = c switch { TrayColor.Green => Color.FromArgb(46, 160, 67), TrayColor.Blue => Color.FromArgb(31, 111, 235), TrayColor.Red => Color.FromArgb(207, 34, 46), _ => Color.FromArgb(110, 118, 129) };
        using var brush = new SolidBrush(fill);
        using var pen = new Pen(Color.FromArgb(30, 30, 30), 2);
        var rect = new Rectangle(6, 2, 20, 28);
        // System.Drawing.Graphics has no FillRoundedRectangle/DrawRoundedRectangle; fall back to an ellipse over the same rectangle.
        g.FillEllipse(brush, rect);
        g.DrawEllipse(pen, rect);
        return Icon.FromHandle(bmp.GetHicon()); // handle intentionally kept for the app lifetime
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        UiControl.Dispose();
    }
}
