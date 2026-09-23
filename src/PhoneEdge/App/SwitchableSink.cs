using PhoneEdge.Core.Input;

namespace PhoneEdge.App;

/// <summary>Lets the InputRouter keep one sink reference while the underlying ControlChannel is replaced on reconnect.</summary>
public sealed class SwitchableSink : IReportSink
{
    private volatile IReportSink? _target;
    public IReportSink? Target { get => _target; set => _target = value; }
    public bool SendMouse(byte[] report) => _target?.SendMouse(report) ?? false;
    public bool SendKeyboard(byte[] report) => _target?.SendKeyboard(report) ?? false;
    public bool SendBack(bool down) => _target?.SendBack(down) ?? false;
    public bool SendDisplayPower(bool on) => _target?.SendDisplayPower(on) ?? false;
}
