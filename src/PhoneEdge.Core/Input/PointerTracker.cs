namespace PhoneEdge.Core.Input;

/// <summary>Dead-reckoned position of the phone cursor. Deltas are scaled, fractional remainders kept, position clamped to bounds.</summary>
public sealed class PointerTracker
{
    private double _fracX, _fracY;

    public PointerTracker(int width, int height, double sensitivity)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "bounds must be positive");
        if (sensitivity <= 0) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        Width = width;
        Height = height;
        Sensitivity = sensitivity;
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public double Sensitivity { get; }
    public int X { get; private set; }
    public int Y { get; private set; }

    public bool AtLeft => X == 0;
    public bool AtRight => X == Width - 1;
    public bool AtTop => Y == 0;
    public bool AtBottom => Y == Height - 1;

    public void SetBounds(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "bounds must be positive");
        Width = width;
        Height = height;
        X = Math.Clamp(X, 0, Width - 1);
        Y = Math.Clamp(Y, 0, Height - 1);
    }

    public void SetPosition(int x, int y)
    {
        X = Math.Clamp(x, 0, Width - 1);
        Y = Math.Clamp(y, 0, Height - 1);
        _fracX = _fracY = 0;
    }

    /// <returns>The integer delta actually applied after scaling and clamping.</returns>
    public (int dx, int dy) Move(int rawDx, int rawDy)
    {
        _fracX += rawDx * Sensitivity;
        _fracY += rawDy * Sensitivity;
        int ix = (int)Math.Truncate(_fracX);
        int iy = (int)Math.Truncate(_fracY);
        _fracX -= ix;
        _fracY -= iy;
        int nx = Math.Clamp(X + ix, 0, Width - 1);
        int ny = Math.Clamp(Y + iy, 0, Height - 1);
        var applied = (nx - X, ny - Y);
        X = nx;
        Y = ny;
        return applied;
    }
}
