using Task.Monitor.Internal.Abstractions;
using Task.Monitor.System.Configuration;
using Task.Monitor.Tests.Common;

namespace Task.Monitor.System.Tests.Configuration;

public sealed class ConfigTests
{
    [Fact]
    public void Should_Create_New_Empty_Config()
    {
        Config config = new();
        
        Assert.NotNull(config.ConfigSections);
        Assert.Empty(config.ConfigSections);
    }

    [Fact]
    public void Should_Add_ConfigSection_To_Config()
    {
        Config config = new();
        config.ConfigSections.Add(new ConfigSection("TestSection"));
        
        Assert.Single(config.ConfigSections);
        Assert.True(config.ContainsSection("TestSection"));
    }

    [Fact]
    public void Duplicate_ConfigSection_Throws_InvalidOperationException()
    {
        Config config = new(); 
        
        config
            .AddConfigSection(new ConfigSection("Section1"))
            .AddConfigSection(new ConfigSection("Section2"));
        
        Assert.Throws<InvalidOperationException>(() => config.AddConfigSection(new ConfigSection("Section1")));
        Assert.Throws<InvalidOperationException>(() => config.AddConfigSection(new ConfigSection("Section2")));
    }

    [Fact]
    public void Should_Find_Section_By_Name()
    {
        Config config = new();
        
        config
            .AddConfigSection(new ConfigSection("Section1"))
            .AddConfigSection(new ConfigSection("Section2"));

        Assert.True(config.ContainsSection("Section1"));
        Assert.True(config.ContainsSection("Section2"));
    }

    [Fact]
    public void Should_Load_Config_From_File()
    {
        using FileCleanupHelper cleanupHelper = new();
        string fileName = cleanupHelper.GetTempFile(".config");

        FileSystem fileSystem = new();
        fileSystem.WriteAllText(fileName, ConfigParserTests.MinConfigFileWithAllDataTypes);
        
        Config config = Config.FromFile(fileSystem, fileName);
        Assert.NotNull(config);

        ConfigSection configSection = config.GetConfigSection("data-types");
        Assert.Equal("string value", configSection.GetString("string-key"));
    }

    [Fact]
    public void Should_Load_Config_From_String()
    {
        Config? config = Config.FromString(ConfigParserTests.MinConfigFileWithAllDataTypes);
        Assert.NotNull(config);

        ConfigSection configSection = config.GetConfigSection("data-types");
        Assert.True(configSection.GetBool("bool-true"));
    }
}