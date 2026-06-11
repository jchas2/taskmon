using System.Text;

namespace Task.Monitor.Cli.Utils;

public static class IntegerExtensions
{
    private static readonly CompositeFormat MbpsFormat = CompositeFormat.Parse("{0,5:####0.0} MB/s");

    public static long PackFloat(this float value)
    {
        long integralPart = (long)value;
        float fractionalPart = value - integralPart;
        uint fractionalBits = (uint)(fractionalPart * uint.MaxValue);

        return (integralPart << 32) | (fractionalBits & 0xFFFFFFFFL);
    }
    
    public static double UnpackToDouble(this long packed)
    {
        long integralPart = packed >> 32;

        uint fractionalBits = (uint)(packed & 0xFFFFFFFF);
        double fractionalPart = (double)fractionalBits / uint.MaxValue;

        return integralPart + fractionalPart;
    }
    
    public static string ToFormattedByteSize(this int num) => ToFormattedByteSize((long)num);

    public static string ToFormattedByteSize(this long num) =>
        ToFormattedByteSizeInternal((ulong)num);

    public static string ToFormattedByteSize(this ulong num) =>
        ToFormattedByteSizeInternal(num);

    private static string ToFormattedByteSizeInternal(this ulong num)
    {
        string[] byteFormatters = ["B", "KB", "MB", "GB", "TB"];
        int index = 0;
        double count = num;

        while (count >= 1024 && index < byteFormatters.Length - 1) {
            index++;
            count /= 1024;
        }

        return $"{count:0.#} {byteFormatters[index]}";
    }
    
    public static string ToFormattedMbpsFromBytes(this long num)
    {
        double mbps = ToMbpsFromBytes(num);
        return string.Format(null, MbpsFormat, mbps);
    }
    
    public static string ToHexadecimal(this long num) =>
        ((ulong)num).ToHexadecimal();
    
    public static string ToHexadecimal(this ulong num)
    {
        return string.Create(18, num, static (span, value) =>
        {
            span[0] = '0';
            span[1] = 'x';
            value.TryFormat(span.Slice(2), out _, "X16");
        });
    }
    
    public static double ToMbpsFromBytes(this long num) =>
         Math.Ceiling((double)num / 1000000.0 * 10.0) / 10.0;
}
