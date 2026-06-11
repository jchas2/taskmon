using System.ComponentModel;
using System.Reflection;

namespace Task.Monitor.Cli.Utils;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);

        if (name == null) {
            return value.ToString();
        }

        FieldInfo? fieldInfo = type.GetField(name);
        DescriptionAttribute? attribute = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
    
        return attribute?.Description ?? name;
    }    
}
