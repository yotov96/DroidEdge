using DroidEdge.Core.Hid;
using DroidEdge.Core.Protocol;

namespace DroidEdge.Core.Input;

/// <summary>Bounded outbound queue. Producers are hook/raw-input threads and must never block; the single consumer is the socket sender thread.</summary>
public sealed class ReportQueue
{
    private sealed class Item
    {
        public required ushort DeviceId;   // 0 = raw framed message
        public required byte[] Data;
        public required bool Coalescable;
    }

    private readonly LinkedList<Item> _items = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly object _lock = new();
    private readonly int _capacity;
    private byte _lastMouseButtons;

    public ReportQueue(int capacity = 256)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count { get { lock (_lock) return _items.Count; } }

    public bool EnqueueRaw(byte[] framed, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMilliseconds(250));
        while (true)
        {
            lock (_lock)
            {
                if (_items.Count < _capacity || EvictOldestCoalescable())
                {
                    _items.AddLast(new Item { DeviceId = 0, Data = framed, Coalescable = false });
                    _available.Release();
                    return true;
                }
            }
            if (DateTime.UtcNow >= deadline) return false;
            Thread.Sleep(1); // only reached from non-hook threads (create/destroy); wait for the sender to drain
        }
    }

    public bool EnqueueMouse(byte[] report)
    {
        if (report.Length != MouseReport.Size) throw new ArgumentException("mouse report must be 5 bytes", nameof(report));
        lock (_lock)
        {
            // A button transition is reported with zero deltas, same as a coalescable move; only treat it as
            // coalescable when the buttons match the previously enqueued report, so a transition is never evicted.
            bool coalescable = report[3] == 0 && report[4] == 0 && report[0] == _lastMouseButtons;
            _lastMouseButtons = report[0];
            if (coalescable && _items.Last is { Value: { Coalescable: true, DeviceId: HidDescriptors.MouseId } tail } && tail.Data[0] == report[0])
            {
                int sx = (sbyte)tail.Data[1] + (sbyte)report[1];
                int sy = (sbyte)tail.Data[2] + (sbyte)report[2];
                if (sx is >= -127 and <= 127 && sy is >= -127 and <= 127)
                {
                    tail.Data[1] = (byte)(sbyte)sx;
                    tail.Data[2] = (byte)(sbyte)sy;
                    return true;
                }
                // Saturate the tail on the axis that fits and start a new item with the remainder.
                int fitX = Math.Clamp(sx, -127, 127), fitY = Math.Clamp(sy, -127, 127);
                int remX = sx - fitX, remY = sy - fitY;
                tail.Data[1] = (byte)(sbyte)fitX;
                tail.Data[2] = (byte)(sbyte)fitY;
                report = MouseReport.Encode((HidMouseButtons)report[0], (sbyte)remX, (sbyte)remY, 0, 0);
            }
            return Add(HidDescriptors.MouseId, (byte[])report.Clone(), coalescable);
        }
    }

    public bool EnqueueKeyboard(byte[] report)
    {
        if (report.Length != KeyboardState.ReportSize) throw new ArgumentException("keyboard report must be 8 bytes", nameof(report));
        lock (_lock) return Add(HidDescriptors.KeyboardId, (byte[])report.Clone(), coalescable: false);
    }

    public byte[]? Take(CancellationToken ct)
    {
        while (true)
        {
            try { _available.Wait(ct); }
            catch (OperationCanceledException) { return null; }
            Item item;
            lock (_lock)
            {
                // A permit can outlive its item when an eviction raced with this Wait; just wait again.
                if (_items.First is null) continue;
                item = _items.First.Value;
                _items.RemoveFirst();
            }
            return item.DeviceId == 0 ? item.Data : ControlMessages.UhidInput(item.DeviceId, item.Data);
        }
    }

    // Caller holds _lock.
    private bool Add(ushort deviceId, byte[] data, bool coalescable)
    {
        if (_items.Count >= _capacity && !EvictOldestCoalescable()) return false;
        _items.AddLast(new Item { DeviceId = deviceId, Data = data, Coalescable = coalescable });
        _available.Release();
        return true;
    }

    // Caller holds _lock. Removes the oldest coalescable item and balances the semaphore.
    private bool EvictOldestCoalescable()
    {
        for (var n = _items.First; n is not null; n = n.Next)
        {
            if (n.Value.Coalescable)
            {
                _items.Remove(n);
                _available.Wait(0); // consume the permit that belonged to the evicted item
                return true;
            }
        }
        return false;
    }
}
