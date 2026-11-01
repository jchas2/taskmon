using Task.Monitor.System.Configuration;

namespace Task.Monitor.System.Tests.Configuration;

public sealed class ConfigFileMergerTests
{
    private const string CommentedFile =
        "; my settings\n" +
        "\n" +
        "[ux]\n" +
        "; why I turned this off\n" +
        "show-tips = False   ; too busy\n" +
        "Default-Theme=Nord\n" +
        "\n" +
        "; stats live here\n" +
        "[stats]\n" +
        "delay=1000\n" +
        "\n" +
        "[my-notes]\n" +
        "anything=goes\n";

    [Fact]
    public void An_Empty_File_Gets_Every_Section()
    {
        Config current = new();
        current.AddConfigSection(new ConfigSection("ux").Add("a", "1"));
        current.AddConfigSection(new ConfigSection("stats").Add("b", "2"));

        string merged = ConfigFileMerger.Merge(string.Empty, new Config(), current);
        string nl     = Environment.NewLine;

        Assert.Equal($"[ux]{nl}a=1{nl}{nl}[stats]{nl}b=2{nl}", merged);
    }

    [Fact]
    public void A_Changed_Key_Keeps_Its_Spacing_And_Inline_Comment()
    {
        string merged = MergeChange(CommentedFile, "ux", "show-tips", "True");

        Assert.Contains("show-tips = True   ; too busy\n", merged);
    }

    [Fact]
    public void A_Changed_Key_Keeps_Its_Spelling()
    {
        string merged = MergeChange(CommentedFile, "ux", "default-theme", "Dracula Official");

        Assert.Contains("Default-Theme=Dracula Official\n", merged);
    }

    [Fact]
    public void A_Change_Touches_Only_Its_Own_Line()
    {
        string merged = MergeChange(CommentedFile, "stats", "delay", "2000");

        Assert.Equal(CommentedFile.Replace("delay=1000", "delay=2000"), merged);
    }

    [Fact]
    public void A_File_With_No_Trailing_New_Line_Is_Unchanged_When_Nothing_Changed()
    {
        const string text = "[ux]\nshow-tips=True";

        Assert.Equal(text, ConfigFileMerger.Merge(text, Parse(text), Parse(text)));
    }

    [Fact]
    public void A_Hand_Edit_Survives_When_Taskmon_Did_Not_Change_The_Key()
    {
        Config loaded = Parse("[stats]\ndelay=1000\n");

        string merged = ConfigFileMerger.Merge("[stats]\ndelay=500 ; faster\n", loaded, Parse("[stats]\ndelay=1000\n"));

        Assert.Equal("[stats]\ndelay=500 ; faster\n", merged);
    }

    [Fact]
    public void A_Key_Added_By_Hand_Is_Left_Alone()
    {
        const string loaded = "[stats]\ndelay=1000\n";

        string merged = ConfigFileMerger.Merge("[stats]\ndelay=1000\nnote=mine\n", Parse(loaded), Parse(loaded));

        Assert.Equal("[stats]\ndelay=1000\nnote=mine\n", merged);
    }

    [Fact]
    public void A_Line_With_An_Empty_Value_Takes_The_New_Value()
    {
        string merged = MergeChange("[filter]\nprocess=\n", "filter", "process", "chrome");

        Assert.Equal("[filter]\nprocess=chrome\n", merged);
    }

    [Fact]
    public void A_Missing_Key_In_An_Empty_Section_Follows_The_Header()
    {
        string merged = MergeChange("[ux]\n\n[stats]\ndelay=1\n", "ux", "show-tips", "True");

        Assert.Equal("[ux]\nshow-tips=True\n\n[stats]\ndelay=1\n", merged);
    }

    [Fact]
    public void A_Missing_Key_Is_Added_To_The_End_Of_Its_Section()
    {
        string merged = MergeChange(CommentedFile, "ux", "show-graphs", "True");

        Assert.Contains("Default-Theme=Nord\nshow-graphs=True\n\n; stats live here\n[stats]", merged);
    }

    [Fact]
    public void A_Missing_Key_On_The_Last_Line_Starts_A_New_Line()
    {
        Config merged = Parse(MergeChange("[ux]\na=1", "ux", "b", "2"));

        Assert.Single(merged.ConfigSections);
        Assert.Equal("1", merged.GetConfigSection("ux").GetString("a"));
        Assert.Equal("2", merged.GetConfigSection("ux").GetString("b"));
    }

    [Fact]
    public void A_Missing_Section_Is_Added_To_The_End_With_Its_Comment()
    {
        Config        current = Parse(CommentedFile);
        ConfigSection health  = new("health") { Comment = "Thresholds for HEALTH.\nSee the README." };
        current.AddConfigSection(health.Add("cpu-warning-percent", "90"));

        string merged = ConfigFileMerger.Merge(CommentedFile, Parse(CommentedFile), current);

        Assert.Equal(
            CommentedFile +
            "\n; Thresholds for HEALTH.\n; See the README.\n[health]\ncpu-warning-percent=90\n",
            merged);
    }

    [Fact]
    public void A_New_Section_After_A_Blank_Line_Does_Not_Add_Another()
    {
        Config current = Parse("[ux]\na=1\n");
        current.AddConfigSection(new ConfigSection("stats").Add("b", "2"));

        string merged = ConfigFileMerger.Merge("[ux]\na=1\n\n", Parse("[ux]\na=1\n"), current);

        Assert.Equal("[ux]\na=1\n\n[stats]\nb=2\n", merged);
    }

    [Fact]
    public void A_Repeated_Key_Updates_The_First_One_The_Parser_Keeps()
    {
        string merged = MergeChange("[ux]\na=1\na=2\n", "ux", "a", "3");

        Assert.Equal("[ux]\na=3\na=2\n", merged);
    }

    [Fact]
    public void A_Section_Already_In_The_File_Does_Not_Gain_Its_Comment()
    {
        const string text = "[health]\ncpu-warning-percent=80\n";

        Config current = Parse(text);
        current.GetConfigSection("health").Comment = "Thresholds.";

        Assert.Equal(text, ConfigFileMerger.Merge(text, Parse(text), current));
    }

    [Fact]
    public void Both_Changing_A_Key_Takes_The_Taskmon_Value()
    {
        Config loaded  = Parse("[stats]\ndelay=1000\n");
        Config current = Parse("[stats]\ndelay=2000\n");

        string merged = ConfigFileMerger.Merge("[stats]\ndelay=500\n", loaded, current);

        Assert.Equal("[stats]\ndelay=2000\n", merged);
    }

    [Fact]
    public void Commented_Out_Keys_Are_Not_Mistaken_For_Keys()
    {
        string merged = MergeChange("[ux]\n; a=old note\na=1\n", "ux", "a", "2");

        Assert.Equal("[ux]\n; a=old note\na=2\n", merged);
    }

    [Fact]
    public void Crlf_Files_Stay_Crlf()
    {
        const string text = "[ux]\r\na=1\r\n\r\n[stats]\r\nb=2\r\n";

        Config current = Parse(text);
        current.GetConfigSection("ux").Add("a", "9").Add("c", "3");
        current.AddConfigSection(new ConfigSection("new").Add("d", "4"));

        string merged = ConfigFileMerger.Merge(text, Parse(text), current);

        Assert.Equal("[ux]\r\na=9\r\nc=3\r\n\r\n[stats]\r\nb=2\r\n\r\n[new]\r\nd=4\r\n", merged);
    }

    private static string MergeChange(string fileText, string section, string key, string value)
    {
        Config current = Parse(fileText);
        current.GetConfigSection(section).Add(key, value);

        return ConfigFileMerger.Merge(fileText, Parse(fileText), current);
    }

    [Fact]
    public void Merged_Text_Parses_Back_To_The_Current_Values()
    {
        Config current = Parse(CommentedFile);
        current.GetConfigSection("ux").Add("show-tips", "True").Add("added", "1");
        current.GetConfigSection("stats").Add("delay", "2000");

        Config merged = Parse(ConfigFileMerger.Merge(CommentedFile, Parse(CommentedFile), current));

        Assert.Equal("True", merged.GetConfigSection("ux").GetString("show-tips"));
        Assert.Equal("Nord", merged.GetConfigSection("ux").GetString("default-theme"));
        Assert.Equal("1", merged.GetConfigSection("ux").GetString("added"));
        Assert.Equal("2000", merged.GetConfigSection("stats").GetString("delay"));
        Assert.Equal("goes", merged.GetConfigSection("my-notes").GetString("anything"));
    }

    [Fact]
    public void Nothing_Changed_Returns_The_File_Byte_For_Byte()
    {
        Assert.Equal(
            CommentedFile,
            ConfigFileMerger.Merge(CommentedFile, Parse(CommentedFile), Parse(CommentedFile)));
    }

    private static Config Parse(string text) => Config.FromString(text)!;
}
