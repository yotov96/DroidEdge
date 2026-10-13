namespace DroidEdge.Core.Input;

/// <summary>Fires when "pushing" samples keep arriving (no gap longer than maxGapMs) for dwellMs.</summary>
public sealed class DwellTrigger
{
    private readonly int _dwellMs;
    private readonly int _maxGapMs;
    private long _start = -1;
    private long _last;

    public DwellTrigger(int dwellMs, int maxGapMs = 150)
    {
        _dwellMs = Math.Max(0, dwellMs);
        _maxGapMs = Math.Max(1, maxGapMs);
    }

    public bool Sample(bool pushing, long nowMs)
    {
        if (!pushing)
        {
            Reset();
            return false;
        }
        if (_start < 0 || nowMs - _last > _maxGapMs)
        {
            _start = nowMs;
        }
        _last = nowMs;
        if (nowMs - _start >= _dwellMs)
        {
            Reset();
            return true;
        }
        return false;
    }

    public void Reset() => _start = -1;
}
