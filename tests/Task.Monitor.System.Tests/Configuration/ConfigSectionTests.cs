using Task.Monitor.System.Configuration;

namespace Task.Monitor.System.Tests.Configuration;

public class ConfigSectionTests
{
    [Fact]
    public void Constructor_With_Valid_Name_Initialises_Name_Correctly()
    {
        ConfigSection configSection = new("MySection");
        Assert.Equal("MySection", configSection.Name);
    }

    [Fact]
    public void Constructor_With_Empty_Name_Throws_ArgumentNullException() =>
        Assert.Throws<ArgumentException>(() => new ConfigSection(string.Empty));

    [Fact]
    public void Should_Set_Name_Property()
    {
        ConfigSection configSection = new("MySection");
        Assert.Equal("MySection", configSection.Name);
        
        configSection.Name = "NewSection";
        Assert.Equal("NewSection", configSection.Name);
    }

    [Fact]
    public void Should_Overwrite_Value_With_Existing_Key()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("key1", "value1");
        configSection.Add("key1", "newValue");
        
        Assert.Equal("newValue", configSection.GetString("key1"));
    }

    [Fact]
    public void AddIfMissing_Adds_KeyValuePair_When_Key_Does_Not_Exist()
    {
        ConfigSection configSection = new("MySection");
        configSection.AddIfMissing("key1", "value1");
        
        Assert.Equal("value1", configSection.GetString("key1"));
    }

    [Fact]
    public void AddIfMissing_Does_Not_Overwrite_KeyValuePair_When_Key_Exists()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("key1", "value1");
        configSection.AddIfMissing("key1", "newValue");
        
        Assert.Equal("value1", configSection.GetString("key1"));
    }

    // Regression test: the typed getters used to ignore their default, returning 0 / false for a
    // missing or unreadable value whatever the caller asked for.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a value")]
    public void Typed_Getters_Return_The_Callers_Default_For_A_Missing_Or_Unreadable_Value(string? value)
    {
        ConfigSection configSection = new("MySection");

        if (value != null) {
            configSection.Add("int", value).Add("float", value).Add("bool", value);
        }

        Assert.Equal(1500, configSection.GetInt("int", 1500));
        Assert.Equal(-1, configSection.GetInt("int", -1));
        Assert.Equal(0.5f, configSection.GetFloat("float", 0.5f));
        Assert.True(configSection.GetBool("bool", true));
        Assert.False(configSection.GetBool("bool", false));
    }

    [Fact]
    public void Typed_Getters_Return_A_Readable_Value_Over_The_Default()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("int", "42").Add("float", "2").Add("bool", "False");

        Assert.Equal(42, configSection.GetInt("int", 1500));
        Assert.Equal(2f, configSection.GetFloat("float", 0.5f));
        Assert.False(configSection.GetBool("bool", true));
    }

    [Fact]
    public void Remove_Deletes_The_Key()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("key", "value").Add("other", "kept");

        configSection.Remove("key");

        Assert.False(configSection.Contains("key"));
        Assert.Equal("kept", configSection.GetString("other"));
    }

    [Fact]
    public void Remove_Of_A_Missing_Key_Does_Nothing()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("key", "value");

        configSection.Remove("missing");

        Assert.Equal("value", configSection.GetString("key"));
    }

    [Fact]
    public void Should_Return_True_When_Key_Exists()
    {
        ConfigSection configSection = new("MySection");
        configSection.Add("key1", "value1");
        
        Assert.True(configSection.Contains("key1"));
    }

    [Fact]
    public void Should_Return_False_When_Key_Does_Not_Exist()
    {
        ConfigSection configSection = new("MySection");
        Assert.False(configSection.Contains("key1"));        
    }
    
    public static TheoryData<string, string> StringData()
        => new()
        {
            { "key1", "Value1" },
            { "key2", "ksadjfhaslkjdfhkasjdhfkjsadhfkjlsadhfkjlasdhfkljasdhfkjsaldhfkajshdf" },
            { "key3", "\n\n\n\n\n\nsome value\n\n\n\n\n\nanother\t\t\tvalue\t\t\t\n\n\n" }
        };
    
    [Theory]
    [MemberData(nameof(StringData))]
    public void Should_Add_Section_With_Strings(string key, string value)
    {
        var section = new ConfigSection("Strings")
            .Add(key, value);
        
        Assert.Equal(value, section.GetString(key));
    }

    public static TheoryData<string, int> IntData()
        => new()
        {
            { "key1", 12345678 },
            { "key2", -12345678 },
            { "key3", 0 },
            { "key4", int.MinValue },
            { "key5", int.MaxValue },
            { "key6", short.MinValue },
            { "key7", short.MaxValue },
            { "key8", byte.MinValue },
            { "key9", byte.MaxValue }
        };
    
    [Theory]
    [MemberData(nameof(IntData))]
    public void Should_Add_Section_With_Signed_Integers(string key, int value)
    {
        var section = new ConfigSection("Value-Types")
            .Add(key, value.ToString());
        
        Assert.Equal(value, section.GetInt(key));
    }
    
    [Fact]
    public void Should_Add_Section_With_Booleans()
    {
        var section = new ConfigSection("Value-Types")
            .Add("Key1", "true")
            .Add("Key2", "false");
        
        Assert.True(section.GetBool("Key1"));
        Assert.False(section.GetBool("Key2"));
    }
}