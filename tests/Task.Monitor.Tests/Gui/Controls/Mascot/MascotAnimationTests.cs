using Task.Monitor.Gui.Controls.Mascot;

namespace Task.Monitor.Tests.Gui.Controls.Mascot;

public sealed class MascotAnimationTests
{
    // Each malformed variant swaps one line of this valid file.
    private const string Valid =
        """
        ; a comment
        [palette]
        a=#FF0000   ; red
        b=#0000FF

        [frame one]
        ab
        ba

        [frame two]
        ..
        ..

        [timeline intro]
        one  100  -2
        two    0   0

        [idle]
        two  50  1000  0
        """;

    [Fact]
    public void LoadEmbedded_Intro_Walks_In_From_The_Left_And_Rests_Centred()
    {
        IReadOnlyList<MascotStep> intro = MascotAnimation.LoadEmbedded().GetTimeline("intro");

        Assert.True(intro[0].OffsetX < 0);
        Assert.Equal(new MascotStep("hello", 0, 0), intro[^1]);
    }

    // The shipped asset is parsed in full, so an edit that breaks the format fails here rather
    // than when the About screen opens.
    [Fact]
    public void LoadEmbedded_Parses_The_Shipped_Asset()
    {
        MascotAnimation animation = MascotAnimation.LoadEmbedded();

        Assert.Equal(24, animation.Width);
        Assert.Equal(15, animation.Height);
        Assert.Equal(new MascotIdle("blink", 140, 3500, 2500), animation.Idle);
        Assert.Superset(
            new HashSet<string> { "walk-a", "walk-b", "turn", "off", "hello", "wave-a", "wave-b", "busy", "blink" },
            animation.FrameNames.ToHashSet());
    }

    [Fact]
    public void Parse_Ignores_Comments_And_Blank_Lines()
    {
        MascotAnimation animation = MascotAnimation.Parse(Valid);

        Assert.Equal(new[] { "one", "two" }, animation.FrameNames.Order());
        Assert.Equal(2, animation.Width);
        Assert.Equal(1, animation.Height);
        Assert.Equal(new[] { new MascotStep("one", 100, -2), new MascotStep("two", 0, 0) }, animation.GetTimeline("intro"));
        Assert.Equal(new MascotIdle("two", 50, 1000, 0), animation.Idle);
    }

    [Theory]
    [InlineData("ba",                 "bab",                "line 8: frame 'one' row is 3 pixels wide, not 2")]
    [InlineData("ba",                 "bz",                 "line 8: 'z' is not in the palette")]
    [InlineData("ba",                 "ba\nab",             "line 6: frame 'one' has 3 pixel rows")]
    [InlineData("one  100  -2",       "three  100  -2",     "line 15: unknown frame 'three'")]
    [InlineData("one  100  -2",       "one  0  -2",         "line 15: only the last step can be 0 ms")]
    [InlineData("one  100  -2",       "one  100",           "line 15: expected frame, ms and x")]
    [InlineData("one  100  -2",       "one  soon  -2",      "line 15: 'soon' is not a valid number here")]
    [InlineData("b=#0000FF",          "b=blueish",          "line 4: 'blueish' is not a #RRGGBB colour")]
    [InlineData("b=#0000FF",          "a=#00FF00",          "line 4: 'a' is in the palette twice")]
    [InlineData("b=#0000FF",          ".=#00FF00",          "line 4: palette entries look like")]
    [InlineData("[frame two]",        "[frame one]",        "line 10: frame 'one' is defined twice")]
    [InlineData("[frame two]",        "[sprite two]",       "line 10: unknown section '[sprite]'")]
    [InlineData("[frame two]",        "[frame]",            "line 10: [frame] needs a name")]
    [InlineData("[idle]",             "[idle now]",         "line 18: [idle] doesn't take a name")]
    [InlineData("two  50  1000  0",   "three  50  1000  0", "line 19: unknown frame 'three'")]
    [InlineData("; a comment",        "stray",              "line 1: text outside a section")]
    public void Parse_Rejects_A_Malformed_File_Naming_The_Line(string line, string replacement, string message)
    {
        string text = Valid.Replace(line, replacement);

        FormatException error = Assert.Throws<FormatException>(() => MascotAnimation.Parse(text));

        Assert.StartsWith($"Mascot.anim {message}", error.Message);
    }
}
