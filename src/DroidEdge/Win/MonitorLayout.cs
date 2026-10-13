using DroidEdge.Core;
using DroidEdge.Native;

namespace DroidEdge.Win;

public static class MonitorLayout
{
    /// <summary>All monitors in virtual-screen coordinates, primary first.</summary>
    public static List<IntRect> GetMonitors()
    {
        var primary = new List<IntRect>();
        var others = new List<IntRect>();
        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr _, ref User32.RECT r, IntPtr _) =>
        {
            var mi = new User32.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<User32.MONITORINFO>() };
            bool isPrimary = User32.GetMonitorInfoW(h, ref mi) && (mi.dwFlags & User32.MONITORINFOF_PRIMARY) != 0;
            var rect = new IntRect(r.Left, r.Top, r.Right, r.Bottom);
            (isPrimary ? primary : others).Add(rect);
            return true;
        }, IntPtr.Zero);
        primary.AddRange(others);
        return primary;
    }

    public static IntRect Select(List<IntRect> monitors, string monitorSetting)
    {
        if (monitors.Count == 0) throw new InvalidOperationException("No monitors found");
        if (int.TryParse(monitorSetting, out var idx) && idx >= 0 && idx < monitors.Count) return monitors[idx];
        return monitors[0];
    }

    /// <summary>The one-pixel line along <paramref name="edge"/> of <paramref name="monitor"/>, minus spans where another monitor is adjacent. Null if fully blocked.</summary>
    public static IntRect? EdgeSegment(Edge edge, IntRect monitor, IEnumerable<IntRect> others)
    {
        bool vertical = edge is Edge.Left or Edge.Right;
        int lo = vertical ? monitor.Top : monitor.Left;
        int hi = vertical ? monitor.Bottom : monitor.Right;
        foreach (var o in others)
        {
            bool adjacent = edge switch
            {
                Edge.Left => o.Right == monitor.Left,
                Edge.Right => o.Left == monitor.Right,
                Edge.Top => o.Bottom == monitor.Top,
                _ => o.Top == monitor.Bottom,
            };
            if (!adjacent) continue;
            int oLo = vertical ? o.Top : o.Left;
            int oHi = vertical ? o.Bottom : o.Right;
            if (oHi <= lo || oLo >= hi) continue;
            // Keep the larger free span (a monitor rarely sits in the middle of an edge).
            int before = oLo - lo, after = hi - oHi;
            if (before >= after) hi = Math.Min(hi, oLo); else lo = Math.Max(lo, oHi);
        }
        if (hi <= lo) return null;
        return edge switch
        {
            Edge.Left => new IntRect(monitor.Left, lo, monitor.Left + 1, hi),
            Edge.Right => new IntRect(monitor.Right - 1, lo, monitor.Right, hi),
            Edge.Top => new IntRect(lo, monitor.Top, hi, monitor.Top + 1),
            _ => new IntRect(lo, monitor.Bottom - 1, hi, monitor.Bottom),
        };
    }
}
