namespace Task.Monitor.Cli.Utils;

public static class StringExtensions
{
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
