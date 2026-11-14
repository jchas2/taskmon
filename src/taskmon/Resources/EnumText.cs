using System.Globalization;

namespace Task.Monitor.Resources;

// Display text for an enum value shown in the UI: the <EnumType>_<Member> entry in Strings.resx,
// e.g. PaneControlType_CpuMetre. The member name is what code and config files use, so it never
// changes with the UI language; a member without an entry shows its name.
internal static class EnumText
{
    internal static string ResourceKey<T>(T value) where T : struct, Enum => $"{typeof(T).Name}_{value}";

    public static string ToDisplayText<T>(this T value) where T : struct, Enum =>
        Strings.ResourceManager.GetString(ResourceKey(value), CultureInfo.CurrentUICulture) ?? value.ToString();
}
