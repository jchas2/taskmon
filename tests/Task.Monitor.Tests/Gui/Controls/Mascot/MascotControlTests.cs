using System.Diagnostics;
using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.Gui.Controls.Mascot;
using Task.Monitor.System.Tests.Controls;
using State = Task.Monitor.Gui.Controls.Mascot.MascotControl.State;

namespace Task.Monitor.Tests.Gui.Controls.Mascot;

public sealed class MascotControlTests
{
    // A 4 x 2 cell sprite: walks in from the left in two steps, rests, then blinks every second.
    private const string Animation =
        """
        [palette]
        r=#E01010
        b=#1010E0

        [frame rest]
        rrrr
        bbbb
        r..r
        .bb.

        [frame walk]
        rr..
        rr..
        ....
        ....

        [frame blink]
        bbbb
        bbbb
        ....
        ....

        [timeline intro]
        walk  100  -3
        walk  100  -1
        rest    0   0

        [idle]
        blink  50  1000  0
        """;

    // The timeline runs 200 ms; the four caption characters type out over the next 88 ms.
    private const int IntroEnd   = 200;
    private const int CaptionEnd = IntroEnd + 4 * 22;

    private const int ControlWidth = 12;

    // The resting sprite's left edge: centred in the control.
    private const int RestLeft = (ControlWidth - 4) / 2;

    private static readonly Color  Navy = Color.FromArgb(0, 0, 112);
    private static readonly string Red  = AnsiScreen.Key(Color.FromArgb(224, 16, 16));
    private static readonly string Blue = AnsiScreen.Key(Color.FromArgb(16, 16, 224));

    private readonly RecordingTerminal terminal = new();

    private MascotControl CreateControl(int width = ControlWidth, MascotAnimation? animation = null) =>
        new(terminal, animation ?? MascotAnimation.Parse(Animation), new Random(0)) {
            Caption          = ["Hi", "v1"],
            X                = 0,
            Y                = 0,
            Width            = width,
            Height           = 5,
            BackgroundColour = ConsolePalette.Transparent,
            ForegroundColour = Color.FromArgb(200, 200, 200),
            Visible          = true
        };

    [Fact]
    public void DrawState_Centres_The_Resting_Sprite()
    {
        MascotControl ctrl = CreateControl();

        ctrl.DrawState(new State("rest", 0, 4));

        AnsiScreen screen = new(terminal.Output);

        Assert.Equal((Red, Blue), screen.TopAndBottomAt(RestLeft, 0));
        Assert.Equal((Red, Blue), screen.TopAndBottomAt(RestLeft + 3, 0));
        Assert.Equal((AnsiScreen.Transparent, AnsiScreen.Transparent), screen.TopAndBottomAt(RestLeft - 1, 0));
        Assert.Equal((AnsiScreen.Transparent, AnsiScreen.Transparent), screen.TopAndBottomAt(RestLeft + 4, 0));
    }

    [Fact]
    public void DrawState_Draws_Nothing_When_The_Sprite_Does_Not_Fit()
    {
        MascotControl ctrl = CreateControl(width: 3);

        ctrl.DrawState(new State("rest", 0, 4));

        Assert.Empty(terminal.Output);
    }

    // The caption sits under a blank row, each line centred; untyped characters are blanks.
    [Fact]
    public void DrawState_Draws_The_Caption_Typed_So_Far_Centred_Under_The_Sprite()
    {
        MascotControl ctrl = CreateControl();

        ctrl.DrawState(new State("rest", 0, 3));

        AnsiScreen screen = new(terminal.Output);
        int        left   = (ControlWidth - 2) / 2;

        Assert.Equal("Hi", $"{screen[left, 3].Glyph}{screen[left + 1, 3].Glyph}");
        Assert.Equal("v ", $"{screen[left, 4].Glyph}{screen[left + 1, 4].Glyph}");
    }

    // Offsets move the sprite from its resting place; whatever falls outside the control is cut,
    // and every row is repainted edge to edge so a walk leaves no trail.
    [Fact]
    public void DrawState_Moves_The_Sprite_By_Its_Offset_And_Clips_It_To_The_Control()
    {
        MascotControl ctrl = CreateControl();

        ctrl.DrawState(new State("rest", -RestLeft - 2, 0));

        AnsiScreen screen = new(terminal.Output);

        // Columns 0 and 1 show the sprite's last two columns: r over b on the top row.
        Assert.Equal((Red, Blue), screen.TopAndBottomAt(0, 0));
        Assert.Equal((Red, Blue), screen.TopAndBottomAt(1, 0));
        Assert.Equal((AnsiScreen.Transparent, AnsiScreen.Transparent), screen.TopAndBottomAt(ControlWidth - 1, 1));
        Assert.Throws<KeyNotFoundException>(() => screen[ControlWidth, 0]);
    }

    [Fact]
    public void DrawState_Shows_Transparent_Pixels_As_The_Control_Background()
    {
        MascotControl ctrl = CreateControl();
        ctrl.BackgroundColour = Navy;

        ctrl.DrawState(new State("rest", 0, 4));

        AnsiScreen screen = new(terminal.Output);
        string     navy   = AnsiScreen.Key(Navy);

        // Bottom row "r..r" over ".bb.": top only, then bottom only.
        Assert.Equal((Red, navy), screen.TopAndBottomAt(RestLeft, 1));
        Assert.Equal((navy, Blue), screen.TopAndBottomAt(RestLeft + 1, 1));
        Assert.Equal((navy, navy), screen.TopAndBottomAt(0, 0));
    }

    [Fact]
    public void NextChangeAfter_Is_Never_Without_An_Idle_Section()
    {
        MascotControl ctrl = CreateControl(animation: MascotAnimation.Parse(Animation[..Animation.IndexOf("[idle]")]));

        Assert.Equal(-1, ctrl.NextChangeAfter(CaptionEnd + 5000));
    }

    [Fact]
    public void NextChangeAfter_Is_The_End_Of_The_Current_Step_During_The_Intro()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(100, ctrl.NextChangeAfter(0));
        Assert.Equal(200, ctrl.NextChangeAfter(100));
    }

    [Fact]
    public void NextChangeAfter_Is_The_Next_Blink_Edge_When_Idle()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(CaptionEnd + 1000, ctrl.NextChangeAfter(CaptionEnd));
        Assert.Equal(CaptionEnd + 1050, ctrl.NextChangeAfter(CaptionEnd + 1000));
        Assert.Equal(CaptionEnd + 2050, ctrl.NextChangeAfter(CaptionEnd + 1050));
    }

    [Fact]
    public void NextChangeAfter_Is_The_Next_Character_While_The_Caption_Types()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(IntroEnd + 22, ctrl.NextChangeAfter(IntroEnd));
        Assert.Equal(IntroEnd + 44, ctrl.NextChangeAfter(IntroEnd + 30));
    }

    [Fact]
    public void Play_Animates_On_A_Timer()
    {
        MascotControl ctrl = CreateControl();

        ctrl.Play();

        // The walk frame draws at once, the rest frame 200 ms later, then the caption types.
        Stopwatch waited = Stopwatch.StartNew();

        while (terminal.WriteSpanCalls < 3 && waited.ElapsedMilliseconds < 5000) {
            Thread.Sleep(10);
        }

        ctrl.Stop();

        Assert.True(terminal.WriteSpanCalls >= 3, $"only {terminal.WriteSpanCalls} draws in {waited.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Play_Waits_For_The_Control_To_Be_Big_Enough()
    {
        MascotControl ctrl = CreateControl(width: 3);

        ctrl.Play();
        Assert.False(ctrl.IsPlaying);

        ctrl.Width = ControlWidth;
        ctrl.Resize();
        Assert.True(ctrl.IsPlaying);

        ctrl.Stop();
    }

    [Fact]
    public void Skip_Shows_The_Rest_Frame_With_The_Whole_Caption()
    {
        MascotControl ctrl = CreateControl();

        ctrl.Play();
        ctrl.Skip();

        bool introPlaying = ctrl.IsIntroPlaying;
        bool playing      = ctrl.IsPlaying;
        ctrl.Stop();

        AnsiScreen screen = new(terminal.Output);
        int        left   = (ControlWidth - 2) / 2;

        Assert.False(introPlaying);
        Assert.True(playing);
        Assert.Equal((Red, Blue), screen.TopAndBottomAt(RestLeft, 0));
        Assert.Equal("v1", $"{screen[left, 4].Glyph}{screen[left + 1, 4].Glyph}");
    }

    // Asking about an earlier moment after a later one gives the same answer as asking in order.
    [Fact]
    public void StateAt_Answers_For_Any_Moment_In_Any_Order()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal("blink", ctrl.StateAt(CaptionEnd + 2060).Frame);
        Assert.Equal("rest", ctrl.StateAt(CaptionEnd + 1500).Frame);
        Assert.Equal("blink", ctrl.StateAt(CaptionEnd + 1010).Frame);
    }

    [Fact]
    public void StateAt_Blinks_On_Schedule_When_Idle()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(new State("rest", 0, 4), ctrl.StateAt(CaptionEnd + 999));
        Assert.Equal(new State("blink", 0, 4), ctrl.StateAt(CaptionEnd + 1000));
        Assert.Equal(new State("blink", 0, 4), ctrl.StateAt(CaptionEnd + 1049));
        Assert.Equal(new State("rest", 0, 4), ctrl.StateAt(CaptionEnd + 1050));
        Assert.Equal(new State("blink", 0, 4), ctrl.StateAt(CaptionEnd + 2050));
    }

    [Fact]
    public void StateAt_Follows_The_Timeline()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(new State("walk", -3, 0), ctrl.StateAt(0));
        Assert.Equal(new State("walk", -3, 0), ctrl.StateAt(99));
        Assert.Equal(new State("walk", -1, 0), ctrl.StateAt(100));
        Assert.Equal(new State("rest", 0, 0), ctrl.StateAt(IntroEnd));
    }

    [Fact]
    public void StateAt_Types_The_Caption_After_The_Intro()
    {
        MascotControl ctrl = CreateControl();

        Assert.Equal(1, ctrl.StateAt(IntroEnd + 22).CaptionChars);
        Assert.Equal(3, ctrl.StateAt(IntroEnd + 66).CaptionChars);
        Assert.Equal(4, ctrl.StateAt(CaptionEnd).CaptionChars);
    }

    [Fact]
    public void Unload_Stops_The_Animation()
    {
        MascotControl ctrl = CreateControl();

        ctrl.Play();
        ctrl.Unload();

        Assert.False(ctrl.IsPlaying);
    }
}
