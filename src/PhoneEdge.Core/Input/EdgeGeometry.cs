namespace PhoneEdge.Core.Input;

public static class EdgeGeometry
{
    public static bool IsPushingOut(Edge edge, IntRect segment, int cx, int cy, int dx, int dy)
    {
        if (!segment.Contains(cx, cy)) return false;
        return edge switch
        {
            Edge.Left => cx == segment.Left && dx < 0,
            Edge.Right => cx == segment.Right - 1 && dx > 0,
            Edge.Top => cy == segment.Top && dy < 0,
            Edge.Bottom => cy == segment.Bottom - 1 && dy > 0,
            _ => false,
        };
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
