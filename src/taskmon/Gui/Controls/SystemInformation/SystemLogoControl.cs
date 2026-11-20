using System.Drawing;
using Task.Monitor.Cli.Utils;
using Task.Monitor.System;
using Task.Monitor.System.Controls;
using SysThreading = System.Threading;

namespace Task.Monitor.Gui.Controls.SystemInformation;

public sealed class SystemLogoControl : Control
{
    private const int Margin = 2;

    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);

#if __APPLE__
    private static readonly string[] Lines =
    {
        "                  .,o",
        "                 /gg,",
        "              (dMMb",
        "               .o,",
        "    .gggMbgg.     .,ggMMg,",
        "   dMMMMMMMMMMMMMMMMMMMMMMb",
        "  dMMMMMMMMMMMMMMMMMMMMMMMMb",
        " dMMMMMMMMMMMMMMMMMMMMMMMMMMb",
        ".MMMMMMMMMMMMMMMMMMMMMMMMMb,",
        "MMMMMMMMMMMMMMMMMMMMMMMMM`",
        "MMMMMMMMMMMMMMMMMMMMMMMM`",
        "MMMMMMMMMMMMMMMMMMMMMMMM,",
        "MMMMMMMMMMMMMMMMMMMMMMMMM.",
        ".MMMMMMMMMMMMMMMMMMMMMMMMMM'",
        " `MMMMMMMMMMMMMMMMMMMMMMMMMMd'",
        "  `bMMMMMMMMMMMMMMMMMMMMMMMMd'",
        "   `bMMMMMMMMMTASKMMMMMMMMMd'",
        "     `MbMMMMMMONITORMMMMMdM'",
        "       `MMbgg,,,,,,,ggdMM'",
        "         `''        ''`",
    };

    // One colour per row of the art, top to bottom.
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
#if __WIN32__
    private static readonly string[] Lines =
    {
        "        ,.=:!!t3Z3z.,",
        "       :tt:::tt333EE3",
        "       Et:::ztt33EEEL @Ee.,      ..,",
        "      ;tt:::tt333EE7 ;EEEEEEttttt33#",
        "     :Et:::zt333EEQ. $EEEEEttttt33QL",
        "     it::::tt333EEF @EEEEEEttttt33F",
        "    ;3=*^```'*4EEV :EEEEEEttttt33@.",
        "    ,.=::::it=., ` @EEEEEEtttz33QF",
        "   ;::::::::zt33)   \"4EEEtttji3P*",
        "  :t::::::::tt33.:Z3z..  `` ,..g.",
        "  i::::::::zt33F ATASKttt::::ztF",
        " ;:::::::::t33V ;MONITORt::::t3",
        " E::::::::zt33L @EEEtttt::::z3F",
        "{3=*^```'*4E3) ;EEEtttt:::::tZ`",
        "             ` :EEEEtttt::::z7",
        "                 \"VEzjt:;;z>*`",
    };

    // Where each row of the art splits between the left and right panes of the flag.
    private static readonly int[] SplitIndex =
        { 21, 21, 21, 20, 20, 19, 18, 16, 17, 23, 16, 15, 15, 14, 14, 0 };

    private static readonly Color TopLeftColour     = ConsolePalette.FromHex("F25022", ConsolePalette.White);
    private static readonly Color TopRightColour    = ConsolePalette.FromHex("7FBA00", ConsolePalette.White);
    private static readonly Color BottomRightColour = ConsolePalette.FromHex("FFB900", ConsolePalette.White);
    private static readonly Color BottomLeftColour  = ConsolePalette.FromHex("00A4EF", ConsolePalette.White);
#endif

    private readonly AnsiScreenBuffer frame = new();

    private SysThreading::Timer? pulseTimer;

    private volatile bool pulseBold;
    private volatile bool loaded;

    public SystemLogoControl(ISystemTerminal terminal) : base(terminal) { }

#if __APPLE__
    private void DrawRow(int row)
    {
        frame.SetColour(ConsolePalette.FromHex(RowHex[row], ConsolePalette.White), BackgroundColour);
        frame.Append(Lines[row]);
    }
#endif
#if __WIN32__
    private void DrawRow(int row)
    {
        bool topHalf = row < Lines.Length / 2;

        frame.SetColour(topHalf ? TopLeftColour : BottomLeftColour, BackgroundColour);
        frame.Append(Lines[row].AsSpan(0, SplitIndex[row]));
        frame.SetColour(topHalf ? TopRightColour : BottomRightColour, BackgroundColour);
        frame.Append(Lines[row].AsSpan(SplitIndex[row]));
    }
#endif

    public int LogoHeight => Lines.Length + Margin * 2;

    public int LogoWidth => Lines.Max(line => line.Length) + Margin * 2;

    protected override void OnDraw()
    {
        frame.Clear();
        frame.SetColour(ForegroundColour, BackgroundColour);

        for (int row = 0; row < Margin; row++) {
            frame.MoveTo(X, Y + row);
            frame.Append(' ', Width);
        }

        frame.SetBold(pulseBold);

        for (int row = 0; row < Lines.Length; row++) {
            frame.MoveTo(X, Y + Margin + row);
            frame.Append(' ', Margin);
            DrawRow(row);
            frame.Append(' ', Math.Max(0, Width - Margin - Lines[row].Length));
        }

        frame.SetBold(false);
        frame.SetColour(ForegroundColour, BackgroundColour);

        for (int row = 0; row < Margin; row++) {
            frame.MoveTo(X, Y + Margin + Lines.Length + row);
            frame.Append(' ', Width);
        }

        frame.ResetColour();
        Terminal.Write(frame.AsSpan());
    }

    protected override void OnLoad()
    {
        pulseBold  = false;
        loaded     = true;
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
