#if __APPLE__
using System.Text;
using Task.Monitor.Interop.Mach;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class PropertyListTests
{
    // The XmlJob below converted with "plutil -convert binary1"; launchd plists ship in both forms.
    private const string BinaryJob =
        "YnBsaXN0MDDXAQIDBAUGBwgJCgsOERJUQmxvYl8QEFRocm90dGxlSW50ZXJ2YWxUTmljZV8QEFByb2dyYW1Bcmd1bWVudHNZ" +
        "S2VlcEFsaXZlWVJ1bkF0TG9hZFVMYWJlbEMBAgMQHiM/+AAAAAAAAKIMDV8QNC9BcHBsaWNhdGlvbnMvRXhhbXBsZSBBcHAu" +
        "YXBwL0NvbnRlbnRzL01hY09TL0V4YW1wbGVcLS1iYWNrZ3JvdW5k0Q8QXlN1Y2Nlc3NmdWxFeGl0CAlfEBFjb20uZXhhbXBs" +
        "ZS5hZ2VudAgXHC80R1FbYWVncHOqt7rJyssAAAAAAAABAQAAAAAAAAATAAAAAAAAAAAAAAAAAAAA3w==";

    private const string XmlJob = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key><string>com.example.agent</string>
            <key>ProgramArguments</key><array><string>/Applications/Example App.app/Contents/MacOS/Example</string><string>--background</string></array>
            <key>RunAtLoad</key><true/>
            <key>KeepAlive</key><dict><key>SuccessfulExit</key><false/></dict>
            <key>ThrottleInterval</key><integer>30</integer>
            <key>Nice</key><real>1.5</real>
            <key>Blob</key><data>AQID</data>
        </dict>
        </plist>
        """;

    public static TheoryData<string> Formats => new() { "xml", "binary" };

    [Theory]
    [MemberData(nameof(Formats))]
    public void Parse_Materialises_Every_Value_Type(string format)
    {
        byte[] bytes = format == "xml"
            ? Encoding.UTF8.GetBytes(XmlJob)
            : Convert.FromBase64String(BinaryJob);

        Dictionary<string, object?> job = Assert.IsType<Dictionary<string, object?>>(PropertyList.Parse(bytes));

        Assert.Equal("com.example.agent", job["Label"]);
        Assert.Equal(
            ["/Applications/Example App.app/Contents/MacOS/Example", "--background"],
            Assert.IsType<List<object?>>(job["ProgramArguments"]));
        Assert.Equal(true, job["RunAtLoad"]);
        Assert.Equal(false, Assert.IsType<Dictionary<string, object?>>(job["KeepAlive"])["SuccessfulExit"]);
        Assert.Equal(30L, job["ThrottleInterval"]);
        Assert.Equal(1.5, job["Nice"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, job["Blob"]);
    }

    [Fact]
    public void ParseKeyedArchive_Keeps_Object_References()
    {
        Dictionary<string, object?> archive = Assert.IsType<Dictionary<string, object?>>(
            PropertyList.ParseKeyedArchive(BackgroundItemsFixture.Bytes));

        Assert.Equal("NSKeyedArchiver", archive["$archiver"]);
        Dictionary<string, object?> top = Assert.IsType<Dictionary<string, object?>>(archive["$top"]);
        Assert.IsType<PropertyListUid>(top["store"]);
    }

    [Fact]
    public void Parse_Drops_Object_References_It_Cannot_Read()
    {
        Dictionary<string, object?> archive = Assert.IsType<Dictionary<string, object?>>(
            PropertyList.Parse(BackgroundItemsFixture.Bytes));

        Assert.Null(Assert.IsType<Dictionary<string, object?>>(archive["$top"])["store"]);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void ParseKeyedArchive_Reads_A_Plain_Property_List_Too(string format)
    {
        byte[] bytes = format == "xml"
            ? Encoding.UTF8.GetBytes(XmlJob)
            : Convert.FromBase64String(BinaryJob);

        Dictionary<string, object?> job = Assert.IsType<Dictionary<string, object?>>(PropertyList.ParseKeyedArchive(bytes));

        Assert.Equal("com.example.agent", job["Label"]);
        Assert.Equal(30L, job["ThrottleInterval"]);
        Assert.Equal(1.5, job["Nice"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, job["Blob"]);
        Assert.Equal(false, Assert.IsType<Dictionary<string, object?>>(job["KeepAlive"])["SuccessfulExit"]);
    }

    [Fact]
    public void Parse_Returns_Null_For_Something_That_Is_Not_A_Property_List()
    {
        Assert.Null(PropertyList.Parse(Encoding.UTF8.GetBytes("not a plist <dict>")));
    }

    [Fact]
    public void Parse_Returns_Null_For_No_Bytes()
    {
        Assert.Null(PropertyList.Parse(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void ReadFile_Returns_Null_For_A_Missing_File()
    {
        Assert.Null(PropertyList.ReadFile(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.plist")));
    }
}
#endif
