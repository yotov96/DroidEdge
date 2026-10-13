using DroidEdge.Phone;

namespace DroidEdge.Tests;

public class AdbParsersTests
{
    [Fact]
    public void ParseDevices_reads_serial_and_state()
    {
        const string s = "List of devices attached\r\n2B171FDH2001AB         device usb:1-2 product:husky model:Pixel_8_Pro device:husky transport_id:3\r\nemulator-5554  offline\r\n\r\n";
        var d = AdbParsers.ParseDevices(s);
        Assert.Equal(2, d.Count);
        Assert.Equal(("2B171FDH2001AB", "device"), (d[0].Serial, d[0].State));
        Assert.Equal(("emulator-5554", "offline"), (d[1].Serial, d[1].State));
    }

    [Fact]
    public void ParseDevices_handles_empty_list()
    {
        Assert.Empty(AdbParsers.ParseDevices("List of devices attached\n\n"));
    }

    [Fact]
    public void ParseWmSize_prefers_override()
    {
        Assert.Equal((1080, 2400), AdbParsers.ParseWmSize("Physical size: 1344x2992\nOverride size: 1080x2400\n"));
        Assert.Equal((1344, 2992), AdbParsers.ParseWmSize("Physical size: 1344x2992\n"));
        Assert.Null(AdbParsers.ParseWmSize("garbage"));
    }

    [Fact]
    public void ParseViewport_reads_orientation_and_logical_frame_of_display_0()
    {
        const string s = """
            Input Reader State:
              Viewport INTERNAL: displayId=0, uniqueId=local:4619827259835644672, port=129, orientation=1, logicalFrame=[0, 0, 2992, 1344], physicalFrame=[0, 0, 1344, 2992], deviceSize=[1344, 2992], isActive=true
              Viewport EXTERNAL: displayId=2, uniqueId=local:1, port=1, orientation=0, logicalFrame=[0, 0, 1920, 1080], physicalFrame=[0, 0, 1920, 1080], deviceSize=[1920, 1080], isActive=true
            """;
        var v = AdbParsers.ParseViewport(s);
        Assert.NotNull(v);
        Assert.Equal((2992, 1344, 1, 0), (v!.Width, v.Height, v.Rotation, v.DensityDpi));
    }

    [Fact]
    public void ParseViewport_reads_density_of_display_0()
    {
        // Android 17 format, taken from a Pixel 8 Pro
        const string s = """
                Viewport INTERNAL: displayId=0, uniqueId=local:4619827535830457088, port=0, orientation=0, densityDpi=360 xDpi=365.760010, yDpi=367.726013, logicalFrame=[0, 0, 1008, 2244], physicalFrame=[0, 0, 1008, 2244], deviceSize=[1008, 2244], isActive=[1]
                Viewport VIRTUAL: displayId=3, uniqueId=virtual:com.android.shell,2000,scrcpy,1, port=<none>, orientation=0, densityDpi=160 xDpi=160.000000, yDpi=160.000000, logicalFrame=[0, 0, 1918, 1000], physicalFrame=[0, 0, 1918, 1000], deviceSize=[1918, 1000], isActive=[1]
            """;
        Assert.Equal(new DisplayInfo(1008, 2244, 0, 360), AdbParsers.ParseViewport(s));
    }

    [Fact]
    public void ParseViewport_returns_null_without_display_0()
    {
        Assert.Null(AdbParsers.ParseViewport("nothing here"));
    }

    [Fact]
    public void ParseSettingValue_maps_null_literal()
    {
        Assert.Null(AdbParsers.ParseSettingValue("null\r\n"));
        Assert.Equal("0", AdbParsers.ParseSettingValue(" 0 \n"));
        Assert.Null(AdbParsers.ParseSettingValue(""));
    }
}
