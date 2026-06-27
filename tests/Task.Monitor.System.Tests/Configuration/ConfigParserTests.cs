using Task.Monitor.Cli.Utils;
using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;

using System.Drawing;
namespace Task.Monitor.System.Tests.Configuration;

public sealed class ConfigParserTests
{
    internal static string MinConfigFile => @"
[section1]
key1=value1
key2=value2";

    internal static string MinConfigFileWithAllDataTypes = @"
[data-types]
string-key=string value
bool-true=true
bool-false=false
int-key1=12345678
int-key2=-12345678
console-color-black=black
console-color-darkblue=darkblue
console-color-darkgreen=darkgreen
console-color-darkcyan=darkcyan
console-color-darkred=darkred
console-color-darkmagenta=darkmagenta
console-color-darkyellow=darkyellow
console-color-gray=gray
console-color-darkgray=darkgray
console-color-blue=blue
console-color-green=green
console-color-cyan=cyan
console-color-red=red
console-color-magenta=magenta
console-color-yellow=yellow
console-color-white=white
";

    [Fact]
    public void Invalid_Config_File_Name_Should_Throw_FileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            new ConfigParser(
                new FileSystem(),
                "_file_does_not_exist.tmp"));
    }

    [Fact]
    public void Empty_Config_File_Should_Load_Empty_Config()
    {
        ConfigParser configParser = new("");
        configParser.Parse();

        Assert.NotNull(configParser.Sections);
        Assert.Empty(configParser.Sections);
    }
    
    [Fact]
    public void Empty_Section_Name_Should_Throw_ConfigParsingException()
    {
        ConfigParser configParser = new("[]\nkey1=value1\n");
        
        Assert.Throws<ConfigParseException>(() => configParser.Parse());
    }

    [Fact]
    public void Should_Parse_Min_Config_File()
    {
        var configParser = new ConfigParser(MinConfigFile);
        configParser.Parse();
        
        Assert.True(configParser.Sections.Count == 1);
        Assert.Equal("section1", configParser.Sections[0].Name);
        Assert.True(configParser.Sections[0].Contains("key1"));
        Assert.Equal("value1", configParser.Sections[0].GetString("key1"));
        Assert.Equal("value2", configParser.Sections[0].GetString("key2"));
    }

    [Fact]
    public void Should_Parse_Min_Config_File_With_All_DataTypes()
    {
        var configParser = new ConfigParser(MinConfigFileWithAllDataTypes);
        configParser.Parse();
        
        Assert.True(configParser.Sections.Count == 1);
        Assert.Equal("data-types", configParser.Sections[0].Name);
        Assert.Equal("string value", configParser.Sections[0].GetString("string-key"));
        Assert.True(configParser.Sections[0].GetBool("bool-true"));
        Assert.False(configParser.Sections[0].GetBool("bool-false"));
        Assert.Equal(12345678, configParser.Sections[0].GetInt("int-key1"));
        Assert.Equal(-12345678, configParser.Sections[0].GetInt("int-key2"));
        // Legacy colour names still parse, via the name-fallback in ConsolePalette.FromHex.
        Assert.Equal(ConsolePalette.Black.ToArgb(), configParser.Sections[0].GetColour("console-color-black").ToArgb());
        Assert.Equal(ConsolePalette.DarkBlue.ToArgb(), configParser.Sections[0].GetColour("console-color-darkblue").ToArgb());
        Assert.Equal(ConsolePalette.DarkGreen.ToArgb(), configParser.Sections[0].GetColour("console-color-darkgreen").ToArgb());
        Assert.Equal(ConsolePalette.DarkCyan.ToArgb(), configParser.Sections[0].GetColour("console-color-darkcyan").ToArgb());
        Assert.Equal(ConsolePalette.DarkRed.ToArgb(), configParser.Sections[0].GetColour("console-color-darkred").ToArgb());
        Assert.Equal(ConsolePalette.DarkMagenta.ToArgb(), configParser.Sections[0].GetColour("console-color-darkmagenta").ToArgb());
        Assert.Equal(ConsolePalette.DarkYellow.ToArgb(), configParser.Sections[0].GetColour("console-color-darkyellow").ToArgb());
        Assert.Equal(ConsolePalette.Gray.ToArgb(), configParser.Sections[0].GetColour("console-color-gray").ToArgb());
        Assert.Equal(ConsolePalette.DarkGray.ToArgb(), configParser.Sections[0].GetColour("console-color-darkgray").ToArgb());
        Assert.Equal(ConsolePalette.Blue.ToArgb(), configParser.Sections[0].GetColour("console-color-blue").ToArgb());
        Assert.Equal(ConsolePalette.Green.ToArgb(), configParser.Sections[0].GetColour("console-color-green").ToArgb());
        Assert.Equal(ConsolePalette.Cyan.ToArgb(), configParser.Sections[0].GetColour("console-color-cyan").ToArgb());
        Assert.Equal(ConsolePalette.Red.ToArgb(), configParser.Sections[0].GetColour("console-color-red").ToArgb());
        Assert.Equal(ConsolePalette.Magenta.ToArgb(), configParser.Sections[0].GetColour("console-color-magenta").ToArgb());
        Assert.Equal(ConsolePalette.Yellow.ToArgb(), configParser.Sections[0].GetColour("console-color-yellow").ToArgb());
        Assert.Equal(ConsolePalette.White.ToArgb(), configParser.Sections[0].GetColour("console-color-white").ToArgb());
    }
}
