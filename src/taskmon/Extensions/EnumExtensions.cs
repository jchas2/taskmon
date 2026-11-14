using System.Globalization;
using System.Reflection;
using Task.Monitor.Gui.Controls.Processes;
using Task.Monitor.Resources;

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
        FieldInfo                              fieldInfo = GetFieldInfo(value);
        ProcessControl.ColumnPropertyAttribute attribute = GetCustomAttribute<ProcessControl.ColumnPropertyAttribute>(fieldInfo);
        
        return attribute.Property;
    }

    // The attribute names a Strings.resx entry.
    public static string GetTitle(this Enum value)
    {
        FieldInfo                           fieldInfo = GetFieldInfo(value);
        ProcessControl.ColumnTitleAttribute attribute = GetCustomAttribute<ProcessControl.ColumnTitleAttribute>(fieldInfo);

        if (attribute.Title.Length == 0) {
            return attribute.Title;
        }

        return Strings.ResourceManager.GetString(attribute.Title, CultureInfo.CurrentUICulture) ?? attribute.Title;
    }
}
