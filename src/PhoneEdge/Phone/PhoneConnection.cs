namespace PhoneEdge.Phone;

/// <summary>The connected phone: adb bound to its serial, and the live control channel to our server.</summary>
public sealed record PhoneConnection(Adb Adb, ControlChannel Channel)
{
    public string Serial => Adb.Serial ?? throw new InvalidOperationException("adb is not bound to a device serial");
}
