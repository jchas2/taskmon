using System.Reflection;
using Task.Monitor.Gui.Controls;

namespace Task.Monitor.Extensions;

public static class EnumExtensions
{
    private static T GetCustomAttribute<T>(FieldInfo fieldInfo) where T : Attribute
    {
        T? attribute = fieldInfo.GetCustomAttribute<T>();

        if (attribute is null) {
            throw new InvalidOperationException($"Field {fieldInfo.Name} has no {nameof(Attribute)}");
        }
        
        return attribute;
    }
    
    private static FieldInfo GetFieldInfo(Enum value)
    {
        FieldInfo? fieldInfo = value.GetType().GetField(value.ToString());

        if (fieldInfo == null) {
            throw new InvalidOperationException($"Cannot get field {value} from type {value.GetType()}");
        }
        
        return fieldInfo;
    }
    
    public static string GetProperty(this Enum value)
    {
        FieldInfo fieldInfo = GetFieldInfo(value);
        var attribute = GetCustomAttribute<ProcessControl.ColumnPropertyAttribute>(fieldInfo);
        return attribute.Property;
    }

    public static string GetTitle(this Enum value)
    {
        FieldInfo fieldInfo = GetFieldInfo(value);
        var attribute = GetCustomAttribute<ProcessControl.ColumnTitleAttribute>(fieldInfo);
        return attribute.Title;
    }
}
