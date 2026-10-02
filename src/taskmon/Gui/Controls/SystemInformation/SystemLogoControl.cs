using System.Drawing;
using System.Linq;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using SysThreading = System.Threading;

namespace Task.Monitor.Gui.Controls.SystemInformation;

public sealed class SystemLogoControl : Control
{
    private static readonly int logoWidth  = LogoArt.Lines.Max(line => line.Length);
    private static readonly int logoHeight = LogoArt.Lines.Length;

#if __WIN32__
    private static readonly int[] SplitIndex =
        { 21, 21, 21, 20, 20, 19, 18, 16, 17, 23, 16, 15, 15, 14, 14, 0 };

    private static readonly string[] LeftHalves =
        LogoArt.Lines.Select((line, i) => line[..SplitIndex[i]]).ToArray();

    private static readonly string[] RightHalves =
        LogoArt.Lines.Select((line, i) => line[SplitIndex[i]..]).ToArray();

    private static readonly Color TopLeftColour     = ConsolePalette.FromHex("F25022", ConsolePalette.White);
    private static readonly Color TopRightColour    = ConsolePalette.FromHex("7FBA00", ConsolePalette.White);
    private static readonly Color BottomRightColour = ConsolePalette.FromHex("FFB900", ConsolePalette.White);
    private static readonly Color BottomLeftColour  = ConsolePalette.FromHex("00A4EF", ConsolePalette.White);
#endif
#if __APPLE__
    private static readonly string[] RowHex =
    {
        "3CC846", "3CC846", "3CC846", "3CC846", "3CC846",
        "E63C32", "E63C32",
        "F08C1E", "F08C1E",
        "EBD228", "EBD228",
        "3CC846", "3CC846",
        "28C8D2", "28C8D2",
        "326EE6", "326EE6",
        "BE46C8", "BE46C8", "BE46C8",
    };
#endif

    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);

    private const int Margin = 2;

    private readonly AnsiScreenBuffer frame = new();

    private SysThreading::Timer? pulseTimer;
    private volatile bool pulseBold;
    private volatile bool loaded;

    public SystemLogoControl(ISystemTerminal terminal) : base(terminal) { }

    public int LogoWidth => logoWidth + Margin * 2;

    public int LogoHeight => logoHeight + Margin * 2;

    protected override void OnLoad()
    {
        pulseBold = false;
        loaded = true;
        pulseTimer = new SysThreading::Timer(OnPulseTick, null, PulseInterval, PulseInterval);

        base.OnLoad();
    }

    private void OnPulseTick(object? state)
    {
        if (!loaded) {
            return;
        }

        pulseBold = !pulseBold;
        Draw();
    }

    protected override void OnDraw()
    {
        frame.Clear();
        frame.SetColour(ForegroundColour, BackgroundColour);

        for (int row = 0; row < Margin; row++) {
            frame.MoveTo(X, Y + row);
            frame.Append(' ', Width);
        }

        frame.SetBold(pulseBold);

        for (int row = 0; row < LogoArt.Lines.Length; row++) {
            frame.MoveTo(X, Y + Margin + row);
            frame.Append(' ', Margin);
            DrawRow(row);
            frame.Append(' ', Math.Max(0, Width - Margin - LogoArt.Lines[row].Length));
        }

        frame.SetBold(false);
        frame.SetColour(ForegroundColour, BackgroundColour);

        for (int row = 0; row < Margin; row++) {
            frame.MoveTo(X, Y + Margin + LogoArt.Lines.Length + row);
            frame.Append(' ', Width);
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

#if __APPLE__
    private void DrawRow(int row)
    {
        frame.SetColour(ConsolePalette.FromHex(RowHex[row], ConsolePalette.White), BackgroundColour);
        frame.Append(LogoArt.Lines[row]);
    }
#endif
#if __WIN32__
    private void DrawRow(int row)
    {
        bool topHalf = row < LogoArt.Lines.Length / 2;

        frame.SetColour(topHalf 
            ? TopLeftColour 
            : BottomLeftColour, BackgroundColour);
        
        frame.Append(LeftHalves[row]);
        
        frame.SetColour(topHalf 
            ? TopRightColour 
            : BottomRightColour, BackgroundColour);
        
        frame.Append(RightHalves[row]);
    }
#endif

    protected override void OnUnload()
    {
        loaded = false;

        if (pulseTimer is { } timer) {
            using SysThreading::ManualResetEvent disposed = new(false);
            timer.Dispose(disposed);
            disposed.WaitOne();
            pulseTimer = null;
        }

        base.OnUnload();
    }
}
