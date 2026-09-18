namespace PhoneEdge.Core.Input;

/// <summary>Receives HID reports (unframed) to deliver to the phone.</summary>
public interface IReportSink
{
    bool SendMouse(byte[] report);
    bool SendKeyboard(byte[] report);
}

public interface ICursor
{
    (int x, int y) GetPosition();
    void SetPosition(int x, int y);
}

/// <summary>Controls whether the low-level hooks swallow input.</summary>
public interface IInputGate
{
    bool Swallow { get; set; }
}

public interface IClock
{
    long NowMs { get; }
}

public sealed class SystemClock : IClock
{
    public long NowMs => Environment.TickCount64;
}
