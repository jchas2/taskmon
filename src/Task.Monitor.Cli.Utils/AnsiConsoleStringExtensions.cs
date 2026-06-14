namespace Task.Monitor.Cli.Utils;

public static class AnsiConsoleStringExtensions
{
    public const string Reset = "\u001b[0m";
    
    private const string BlackBackground = "\u001b[40m";
    private const string DarkBlueBackground = "\u001b[44m";
    private const string DarkGreenBackground = "\u001b[42m";
    private const string DarkCyanBackground = "\u001b[46m";
    private const string DarkRedBackground = "\u001b[41m";
    private const string DarkMagentaBackground = "\u001b[45m";
    private const string DarkYellowBackground = "\u001b[43m";
    private const string GrayBackground = "\u001b[47m";
    private const string DarkGrayBackground = "\u001b[100m";
    private const string BlueBackground = "\u001b[104m";
    private const string GreenBackground = "\u001b[102m";
    private const string CyanBackground = "\u001b[106m";
    private const string RedBackground = "\u001b[101m";
    private const string MagentaBackground = "\u001b[105m";
    private const string YellowBackground = "\u001b[103m";
    private const string WhiteBackground = "\u001b[107m";
    
    private const string BlackForeground = "\u001b[30m";
    private const string DarkBlueForeground = "\u001b[34m";
    private const string DarkGreenForeground = "\u001b[32m";
    private const string DarkCyanForeground = "\u001b[36m";
    private const string DarkRedForeground = "\u001b[31m";
    private const string DarkMagentaForeground = "\u001b[35m";
    private const string DarkYellowForeground = "\u001b[33m";
    private const string GrayForeground = "\u001b[37m";
    private const string DarkGrayForeground = "\u001b[90m";
    private const string BlueForeground = "\u001b[94m";
    private const string GreenForeground = "\u001b[92m";
    private const string CyanForeground = "\u001b[96m";
    private const string RedForeground = "\u001b[91m";
    private const string MagentaForeground = "\u001b[95m";
    private const string YellowForeground = "\u001b[93m";
    private const string WhiteForeground = "\u001b[97m";
    
    public static ReadOnlySpan<char> GetBackgroundCode(ConsoleColor background) => background switch
    {
        ConsoleColor.Black => BlackBackground,
        ConsoleColor.DarkBlue => DarkBlueBackground,
        ConsoleColor.DarkGreen => DarkGreenBackground,
        ConsoleColor.DarkCyan => DarkCyanBackground,
        ConsoleColor.DarkRed => DarkRedBackground,
        ConsoleColor.DarkMagenta => DarkMagentaBackground,
        ConsoleColor.DarkYellow => DarkYellowBackground,
        ConsoleColor.Gray => GrayBackground,
        ConsoleColor.DarkGray => DarkGrayBackground,
        ConsoleColor.Blue => BlueBackground,
        ConsoleColor.Green => GreenBackground,
        ConsoleColor.Cyan => CyanBackground,
        ConsoleColor.Red => RedBackground,
        ConsoleColor.Magenta => MagentaBackground,
        ConsoleColor.Yellow => YellowBackground,
        ConsoleColor.White => WhiteBackground,
        _ => throw new ArgumentOutOfRangeException(nameof(background))
    };

    public static ReadOnlySpan<char> GetForegroundCode(ConsoleColor foreground) => foreground switch
    {
        ConsoleColor.Black => BlackForeground,
        ConsoleColor.DarkBlue => DarkBlueForeground,
        ConsoleColor.DarkGreen => DarkGreenForeground,
        ConsoleColor.DarkCyan => DarkCyanForeground,
        ConsoleColor.DarkRed => DarkRedForeground,
        ConsoleColor.DarkMagenta => DarkMagentaForeground,
        ConsoleColor.DarkYellow => DarkYellowForeground,
        ConsoleColor.Gray => GrayForeground,
        ConsoleColor.DarkGray => DarkGrayForeground,
        ConsoleColor.Blue => BlueForeground,
        ConsoleColor.Green => GreenForeground,
        ConsoleColor.Cyan => CyanForeground,
        ConsoleColor.Red => RedForeground,
        ConsoleColor.Magenta => MagentaForeground,
        ConsoleColor.Yellow => YellowForeground,
        ConsoleColor.White => WhiteForeground,
        _ => throw new ArgumentOutOfRangeException(nameof(foreground))
    };
    
    public static string ToBold(this string str) => $"\u001b[1m{str}\u001b[1m";
    
    private static string ToColor(this string str, string colourCode)
    {
        ReadOnlySpan<char> prefix = colourCode;
        ReadOnlySpan<char> suffix = Reset;
        ReadOnlySpan<char> text = str;
        
        return string.Create(
            prefix.Length + text.Length + suffix.Length,
            (str, colourCode),
            (span, state) =>
            {
                ReadOnlySpan<char> colourCodeSpan = state.colourCode;
                ReadOnlySpan<char> strSpan = state.str;
                ReadOnlySpan<char> resetSpan = Reset;
                
                colourCodeSpan.CopyTo(span);
                strSpan.CopyTo(span.Slice(colourCodeSpan.Length));
                resetSpan.CopyTo(span.Slice(colourCodeSpan.Length + strSpan.Length));
            });
    }
    
    public static string ToColour(this string str, ConsoleColor foreground, ConsoleColor background)
    {
        if (string.IsNullOrEmpty(str))
            return str;
    
        ReadOnlySpan<char> bgCode = GetBackgroundCode(background);
        ReadOnlySpan<char> fgCode = GetForegroundCode(foreground);
        ReadOnlySpan<char> reset = Reset;
        ReadOnlySpan<char> text = str;
    
        return string.Create(
            bgCode.Length + fgCode.Length + text.Length + reset.Length,
            (background, foreground, str),
            (span, state) =>
            {
                ReadOnlySpan<char> backgroundSpan = GetBackgroundCode(state.background);
                ReadOnlySpan<char> foregroundSpan = GetForegroundCode(state.foreground);
                ReadOnlySpan<char> strSpan = state.str;
                ReadOnlySpan<char> resetSpan = Reset;
            
                int pos = 0;
                backgroundSpan.CopyTo(span.Slice(pos));
                pos += backgroundSpan.Length;
            
                foregroundSpan.CopyTo(span.Slice(pos));
                pos += foregroundSpan.Length;
            
                strSpan.CopyTo(span.Slice(pos));
                pos += strSpan.Length;
            
                resetSpan.CopyTo(span.Slice(pos));
            });
    }

    public static string ToBlack(this string str) => str.ToColor(BlackForeground);
    public static string ToBlue(this string str) => str.ToColor(BlueForeground);
    public static string ToCyan(this string str) => str.ToColor(CyanForeground);
    public static string ToDarkBlue(this string str) => str.ToColor(DarkBlueForeground);
    public static string ToDarkCyan(this string str) => str.ToColor(DarkCyanForeground);
    public static string ToDarkGreen(this string str) => str.ToColor(DarkGreenForeground);
    public static string ToDarkGray(this string str) => str.ToColor(DarkGrayForeground);
    public static string ToDarkMagenta(this string str) => str.ToColor(DarkMagentaForeground);
    public static string ToDarkRed(this string str) => str.ToColor(DarkRedForeground);
    public static string ToDarkYellow(this string str) => str.ToColor(DarkYellowForeground);
    public static string ToGray(this string str) => str.ToColor(GrayForeground);
    public static string ToGreen(this string str) => str.ToColor(GreenForeground);
    public static string ToMagenta(this string str) => str.ToColor(MagentaForeground);
    public static string ToRed(this string str) => str.ToColor(RedForeground);
    public static string ToWhite(this string str) => str.ToColor(WhiteForeground);
    public static string ToYellow(this string str) => str.ToColor(YellowForeground);    
}

