using System.Diagnostics;
using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
using Task.Monitor.System.Controls;

namespace Task.Monitor.Gui.Controls.Mascot;

public sealed class MascotControl : Control
{
    private const int CaptionGap = 1;

    private const int    CaptionCharMilliseconds = 22;
    private const string IntroTimeline           = "intro";

    private readonly MascotAnimation           animation;
    private readonly AnsiScreenBuffer          frame = new();
    private readonly IReadOnlyList<MascotStep> intro;
    private readonly Random                    random;

    private readonly long[] stepEnds;

    private readonly List<long> blinkStarts = [];
    private readonly Stopwatch  clock       = new();

    private long   clockOffset;
    private State? drawnState;
    private bool   playRequested;
    private Timer? timer;

    internal readonly record struct State(string Frame, int OffsetX, int CaptionChars);

    public MascotControl(ISystemTerminal terminal, MascotAnimation animation, Random? random = null) : base(terminal)
    {
        this.animation = animation;
        this.random    = random ?? Random.Shared;

        intro    = animation.GetTimeline(IntroTimeline);
        stepEnds = new long[intro.Count];

        long end = 0;

        for (int i = 0; i < intro.Count; i++) {
            end        += intro[i].Milliseconds;
            stepEnds[i] = end;
        }
    }

    public IReadOnlyList<string> Caption { get; set; } = [];

    private long CaptionEnd => IntroEnd + CaptionLength * CaptionCharMilliseconds;

    private int CaptionLength => Caption.Sum(line => line.Length);

    private void DrawCaption(int captionChars)
    {
        for (int i = 0; i < Caption.Count; i++) {
            int row = animation.Height + CaptionGap + i;

            if (row >= Height) {
                return;
            }

            string line  = Caption[i];
            string text  = line[..Math.Clamp(captionChars, 0, line.Length)].PadRight(line.Length);
            int    left  = Math.Clamp((Width - line.Length) / 2, 0, Width);
            int    shown = Math.Min(text.Length, Width - left);

            captionChars -= line.Length;

            frame.MoveTo(X, Y + row);
            frame.SetColour(ForegroundColour, BackgroundColour);
            frame.Append(' ', left);
            frame.Append(text.AsSpan(0, shown));
            frame.Append(' ', Width - left - shown);
        }
    }

    internal void DrawState(State state)
    {
        if (!Fits) {
            return;
        }

        MascotFrame sprite = animation.GetFrame(state.Frame);
        int         left   = (Width - animation.Width) / 2 + state.OffsetX;

        frame.Clear();

        for (int row = 0; row < animation.Height; row++) {
            frame.MoveTo(X, Y + row);

            for (int col = 0; col < Width; col++) {
                int x = col - left;

                if (x < 0 || x >= sprite.Width) {
                    frame.SetColour(ForegroundColour, BackgroundColour);
                    frame.Append(' ');
                    continue;
                }

                MascotCell cell = sprite[x, row];

                frame.SetColour(
                    cell.Glyph == ' ' ? ForegroundColour : Resolve(cell.Foreground),
                    Resolve(cell.Background));
                
                frame.Append(cell.Glyph);
            }
        }

        DrawCaption(state.CaptionChars);

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());

        drawnState = state;
    }

    private long Elapsed => clock.ElapsedMilliseconds + clockOffset;

    public bool Fits => Width >= animation.Width && Height >= animation.Height;

    private long IntroEnd => stepEnds[^1];

    private bool IsBlinking(long idleElapsed, out long nextChange)
    {
        MascotIdle idle = animation.Idle!;

        while (blinkStarts.Count == 0 || blinkStarts[^1] <= idleElapsed) {
            long previousEnd = blinkStarts.Count == 0 ? 0 : blinkStarts[^1] + idle.Milliseconds;
            blinkStarts.Add(previousEnd + idle.Every + random.Next(0, idle.Extra + 1));
        }

        int found  = blinkStarts.BinarySearch(idleElapsed);
        int latest = found >= 0 ? found : ~found - 1;

        if (latest >= 0 && idleElapsed < blinkStarts[latest] + idle.Milliseconds) {
            nextChange = blinkStarts[latest] + idle.Milliseconds;
            return true;
        }

        nextChange = blinkStarts[latest + 1];
        return false;
    }

    public bool IsIntroPlaying => timer is not null && Elapsed < CaptionEnd;

    public bool IsPlaying => timer is not null;

    internal long NextChangeAfter(long elapsed)
    {
        if (elapsed < IntroEnd) {
            return stepEnds.First(end => end > elapsed);
        }

        if (elapsed < CaptionEnd) {
            return IntroEnd + ((elapsed - IntroEnd) / CaptionCharMilliseconds + 1) * CaptionCharMilliseconds;
        }

        if (animation.Idle is null) {
            return -1;
        }

        IsBlinking(elapsed - CaptionEnd, out long nextChange);
        return CaptionEnd + nextChange;
    }

    protected override void OnDraw()
    {
        if (timer is not null) {
            DrawState(StateAt(Elapsed));
        }
    }

    protected override void OnResize()
    {
        if (playRequested && timer is null && Fits) {
            Start();
        }

        base.OnResize();
    }

    private void OnTick(object? _)
    {
        lock (DrawingLock) {
            if (timer is null) {
                return;
            }

            Update();
        }
    }

    protected override void OnUnload()
    {
        Stop();
        base.OnUnload();
    }

    public void Play()
    {
        lock (DrawingLock) {
            Stop();
            playRequested = true;

            if (Fits) {
                Start();
            }
        }
    }

    public int PreferredHeight => animation.Height + (Caption.Count > 0 ? CaptionGap + Caption.Count : 0);

    private Color Resolve(Color colour) => colour.IsTransparent() ? BackgroundColour : colour;

    private State RestState => new(intro[^1].Frame, intro[^1].OffsetX, CaptionLength);

    public void Skip()
    {
        lock (DrawingLock) {
            if (timer is null) {
                return;
            }

            if (Elapsed < CaptionEnd) {
                clockOffset += CaptionEnd - Elapsed;
            }

            Update();
        }
    }

    private void Start()
    {
        blinkStarts.Clear();
        clockOffset = 0;
        drawnState  = null;

        clock.Restart();
        timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);

        Update();
    }

    internal State StateAt(long elapsed)
    {
        if (elapsed < IntroEnd) {
            MascotStep step = intro[Array.FindIndex(stepEnds, end => end > elapsed)];

            return new State(step.Frame, step.OffsetX, 0);
        }

        State rest = RestState;

        if (elapsed < CaptionEnd) {
            return rest with { CaptionChars = (int)((elapsed - IntroEnd) / CaptionCharMilliseconds) };
        }

        return animation.Idle is { } idle && IsBlinking(elapsed - CaptionEnd, out _)
            ? rest with { Frame = idle.Frame }
            : rest;
    }

    public void Stop()
    {
        lock (DrawingLock) {
            playRequested = false;
            timer?.Dispose();
            timer = null;
        }
    }

    private void Update()
    {
        long elapsed = Elapsed;

        if (StateAt(elapsed) != drawnState) {
            Draw();
        }

        long next = NextChangeAfter(elapsed);

        timer!.Change(next < 0 ? Timeout.Infinite : Math.Max(1, next - Elapsed), Timeout.Infinite);
    }
}
