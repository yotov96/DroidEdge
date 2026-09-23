namespace PhoneEdge.Core.Input;

/// <summary>
/// Dead-reckoned position of the phone cursor. Deltas are scaled, fractional remainders kept, position clamped to bounds.
/// Converts the tracked position into the HID counts that move the real cursor there.
/// </summary>
public sealed class PointerTracker
{
    private const double ScalingBaselineDpi = 320;

    private double _fracX, _fracY;
    // HID counts the real cursor has been moved from the top-left corner. Rounded down, so the real cursor never gets
    // ahead of the tracked position and cannot cross an edge onto a neighbouring display.
    private int _countX, _countY;

    public PointerTracker(int width, int height, double sensitivity, double pixelsPerCount = 1.0)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "bounds must be positive");
        if (sensitivity <= 0) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        if (pixelsPerCount <= 0) throw new ArgumentOutOfRangeException(nameof(pixelsPerCount));
        Width = width;
        Height = height;
        Sensitivity = sensitivity;
        PixelsPerCount = pixelsPerCount;
    }

    /// <summary>
    /// How far the phone moves its cursor per HID count. Android 17 scales mouse movement by the display density
    /// relative to 320 dpi (CursorInputMapper); Android 16 did so only behind a server-side feature flag.
    /// </summary>
    public static double PixelsPerCountFor(int sdkVersion, int densityDpi) =>
        sdkVersion >= 37 && densityDpi > 0 ? densityDpi / ScalingBaselineDpi : 1.0;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public double Sensitivity { get; }
    public double PixelsPerCount { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }

    public bool AtLeft => X == 0;
    public bool AtRight => X == Width - 1;
    public bool AtTop => Y == 0;
    public bool AtBottom => Y == Height - 1;

    public void SetBounds(int width, int height, double pixelsPerCount)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "bounds must be positive");
        if (pixelsPerCount <= 0) throw new ArgumentOutOfRangeException(nameof(pixelsPerCount));
        Width = width;
        Height = height;
        PixelsPerCount = pixelsPerCount;
        X = Math.Clamp(X, 0, Width - 1);
        Y = Math.Clamp(Y, 0, Height - 1);
        SyncCounts();
    }

    /// <summary>Sets the tracked position; follow with <see cref="Resync"/> to bring the real cursor there.</summary>
    public void SetPosition(int x, int y)
    {
        X = Math.Clamp(x, 0, Width - 1);
        Y = Math.Clamp(y, 0, Height - 1);
        _fracX = _fracY = 0;
        SyncCounts();
    }

    /// <returns>The HID counts that move the real cursor to the new tracked position.</returns>
    public (int dx, int dy) Move(int rawDx, int rawDy)
    {
        _fracX += rawDx * Sensitivity;
        _fracY += rawDy * Sensitivity;
        int ix = (int)Math.Truncate(_fracX);
        int iy = (int)Math.Truncate(_fracY);
        _fracX -= ix;
        _fracY -= iy;
        X = Math.Clamp(X + ix, 0, Width - 1);
        Y = Math.Clamp(Y + iy, 0, Height - 1);
        int cx = CountsTo(X), cy = CountsTo(Y);
        var counts = (cx - _countX, cy - _countY);
        (_countX, _countY) = (cx, cy);
        return counts;
    }

    /// <summary>Moves that pin the real cursor at the top-left corner and then bring it to the tracked position.</summary>
    public IEnumerable<(sbyte dx, sbyte dy)> Resync()
    {
        SyncCounts();
        // The overshoot is sized in counts so that it reaches the corner whatever the scale
        int widthCounts = (int)Math.Ceiling(Width / PixelsPerCount);
        int heightCounts = (int)Math.Ceiling(Height / PixelsPerCount);
        return MoveChunker.Resync(widthCounts, heightCounts, _countX, _countY);
    }

    private void SyncCounts() => (_countX, _countY) = (CountsTo(X), CountsTo(Y));

    private int CountsTo(int pixels) => (int)Math.Floor(pixels / PixelsPerCount);
}
