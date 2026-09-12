using System.Text;

namespace Task.Monitor.Cli.Utils;

public static class StringExtensions
{
    // Terminal columns the text occupies once rendered. Most characters take one column, but East
    // Asian wide characters (CJK ideographs, Hangul, fullwidth forms, ...) take two - a fixed-width
    // column layout that measures by string.Length instead overflows and corrupts everything drawn
    // after it whenever one of those shows up (e.g. in vendor-supplied metadata).
    public static int TerminalWidth(this string text) => TerminalWidth(text.AsSpan());

    public static int TerminalWidth(this ReadOnlySpan<char> text)
    {
        int width = 0;

        foreach (Rune rune in text.EnumerateRunes()) {
            width += IsWideRune(rune.Value) ? 2 : 1;
        }

        return width;
    }

    // The number of UTF-16 chars, from the start of text, that fit within maxWidth terminal columns
    // without splitting a rune. actualWidth is the columns those chars occupy (always <= maxWidth).
    public static int TruncateToTerminalWidth(this string text, int maxWidth, out int actualWidth) =>
        TruncateToTerminalWidth(text.AsSpan(), maxWidth, out actualWidth);

    public static int TruncateToTerminalWidth(this ReadOnlySpan<char> text, int maxWidth, out int actualWidth)
    {
        int width = 0;
        int charLength = 0;

        foreach (Rune rune in text.EnumerateRunes()) {
            int runeWidth = IsWideRune(rune.Value) ? 2 : 1;

            if (width + runeWidth > maxWidth) {
                break;
            }

            width += runeWidth;
            charLength += rune.Utf16SequenceLength;
        }

        actualWidth = width;
        return charLength;
    }

    // Approximate Unicode East Asian Wide/Fullwidth ranges - the common terminal "this renders as
    // two columns" set: CJK ideographs, Hangul, Hiragana/Katakana, fullwidth forms, and their
    // supplementary-plane extensions. Not exhaustive (combining marks and emoji variation selectors
    // are not modelled), but covers what shows up in real-world strings like version-resource
    // metadata.
    private static bool IsWideRune(int codePoint) =>
        (codePoint >= 0x1100 && codePoint <= 0x115F)  ||  // Hangul Jamo
        codePoint == 0x2329 || codePoint == 0x232A    ||
        (codePoint >= 0x2E80 && codePoint <= 0x303E)  ||  // CJK Radicals .. CJK Symbols/Punctuation
        (codePoint >= 0x3041 && codePoint <= 0x33FF)  ||  // Hiragana .. CJK Compatibility
        (codePoint >= 0x3400 && codePoint <= 0x4DBF)  ||  // CJK Unified Ideographs Extension A
        (codePoint >= 0x4E00 && codePoint <= 0x9FFF)  ||  // CJK Unified Ideographs
        (codePoint >= 0xA000 && codePoint <= 0xA4CF)  ||  // Yi Syllables / Radicals
        (codePoint >= 0xAC00 && codePoint <= 0xD7A3)  ||  // Hangul Syllables
        (codePoint >= 0xF900 && codePoint <= 0xFAFF)  ||  // CJK Compatibility Ideographs
        (codePoint >= 0xFE30 && codePoint <= 0xFE4F)  ||  // CJK Compatibility Forms
        (codePoint >= 0xFF00 && codePoint <= 0xFF60)  ||  // Fullwidth Forms
        (codePoint >= 0xFFE0 && codePoint <= 0xFFE6)  ||  // Fullwidth Signs
        (codePoint >= 0x20000 && codePoint <= 0x3FFFD);   // CJK Ext B and beyond

    public static string PadOrTruncate(this string str, char c, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(str);

        if (maxLength <= 0) {
            return string.Empty;
        }

        if (str.Length >= maxLength) {
            return str.Length == maxLength ? str : str[..maxLength];
        }

        int charsToAdd = maxLength - str.Length;

        return string.Create(maxLength, (source: str, c, charsToAdd), (span, state) => {
            state.source.AsSpan().CopyTo(span);
            span.Slice(state.source.Length, state.charsToAdd).Fill(state.c);
        });
    }
    
    public static string CentreWithLength(this string str, int length)
    {
        if (str.Length >= length) {
            return str.Substring(0, length);
        }
    
        int padding = length - str.Length;
        int padLeft = padding / 2;
        int padRight = padding - padLeft;
    
        return string.Create(length, (str, padLeft, padRight), static (span, state) =>
        {
            span.Slice(0, state.padLeft)
                .Fill(' ');
        
            state.str
                .AsSpan()
                .CopyTo(span.Slice(state.padLeft));
        
            span.Slice(state.padLeft + state.str.Length, state.padRight)
                .Fill(' ');
        });
    }    
}
