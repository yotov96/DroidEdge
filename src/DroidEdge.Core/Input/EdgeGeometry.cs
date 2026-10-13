namespace DroidEdge.Core.Input;

public static class EdgeGeometry
{
    /// <summary>
    /// True when the cursor is on (or reported beyond) the configured edge, within the segment's span, and the
    /// delta points outward. WH_MOUSE_LL reports the unclipped position, so pushing past the left edge yields
    /// negative x values; those count as "on the edge".
    /// </summary>
    public static bool IsPushingOut(Edge edge, IntRect segment, int cx, int cy, int dx, int dy)
    {
        bool vertical = edge is Edge.Left or Edge.Right;
        bool inSpan = vertical
            ? cy >= segment.Top && cy < segment.Bottom
            : cx >= segment.Left && cx < segment.Right;
        if (!inSpan) return false;
        return edge switch
        {
            Edge.Left => cx <= segment.Left && dx < 0,
            Edge.Right => cx >= segment.Right - 1 && dx > 0,
            Edge.Top => cy <= segment.Top && dy < 0,
            Edge.Bottom => cy >= segment.Bottom - 1 && dy > 0,
            _ => false,
        };
    }

    /// <summary>A width×height rectangle flush against the edge, centred on the cursor along it, kept inside the monitor.</summary>
    public static IntRect DropZoneBounds(Edge edge, IntRect monitor, int cx, int cy, int width, int height)
    {
        int left = edge switch
        {
            Edge.Left => monitor.Left,
            Edge.Right => monitor.Right - width,
            _ => Math.Clamp(cx - width / 2, monitor.Left, monitor.Right - width),
        };
        int top = edge switch
        {
            Edge.Top => monitor.Top,
            Edge.Bottom => monitor.Bottom - height,
            _ => Math.Clamp(cy - height / 2, monitor.Top, monitor.Bottom - height),
        };
        return new IntRect(left, top, left + width, top + height);
    }

    public static double Fraction(Edge edge, IntRect monitor, int cx, int cy)
    {
        double f = edge is Edge.Left or Edge.Right
            ? (cy - monitor.Top) / (double)Math.Max(1, monitor.Height - 1)
            : (cx - monitor.Left) / (double)Math.Max(1, monitor.Width - 1);
        return Math.Clamp(f, 0.0, 1.0);
    }

    public static (int x, int y) PointOnEdge(Edge edge, IntRect monitor, double fraction, int inset)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        int along;
        switch (edge)
        {
            case Edge.Left:
                along = monitor.Top + (int)Math.Round(fraction * (monitor.Height - 1));
                return (monitor.Left + inset, along);
            case Edge.Right:
                along = monitor.Top + (int)Math.Round(fraction * (monitor.Height - 1));
                return (monitor.Right - 1 - inset, along);
            case Edge.Top:
                along = monitor.Left + (int)Math.Round(fraction * (monitor.Width - 1));
                return (along, monitor.Top + inset);
            default:
                along = monitor.Left + (int)Math.Round(fraction * (monitor.Width - 1));
                return (along, monitor.Bottom - 1 - inset);
        }
    }

    public static (int x, int y) PhoneEntry(Edge edge, double fraction, int w, int h)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        return edge switch
        {
            Edge.Left => (w - 1, (int)Math.Round(fraction * (h - 1))),
            Edge.Right => (0, (int)Math.Round(fraction * (h - 1))),
            Edge.Top => ((int)Math.Round(fraction * (w - 1)), h - 1),
            _ => ((int)Math.Round(fraction * (w - 1)), 0),
        };
    }

    public static bool IsPushingBack(Edge edge, PointerTracker p, int dx, int dy) => edge switch
    {
        Edge.Left => p.AtRight && dx > 0,
        Edge.Right => p.AtLeft && dx < 0,
        Edge.Top => p.AtBottom && dy > 0,
        _ => p.AtTop && dy < 0,
    };

    public static double PhoneFraction(Edge edge, PointerTracker p) => edge is Edge.Left or Edge.Right
        ? p.Y / (double)Math.Max(1, p.Height - 1)
        : p.X / (double)Math.Max(1, p.Width - 1);
}
