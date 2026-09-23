namespace PhoneEdge.Phone;

/// <summary>The connected phone: adb bound to its serial, the live control channel to our server, and its API level (0 if unknown).</summary>
public sealed record PhoneConnection(Adb Adb, ControlChannel Channel, int SdkVersion)
{
    public string Serial => Adb.Serial ?? throw new InvalidOperationException("adb is not bound to a device serial");
}
