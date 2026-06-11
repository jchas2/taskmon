using System.Reflection;

namespace Task.Monitor.Tests.Common;

public static class CanaryTestHelper
{
    public static int GetPropertyCount<T>() => GetPropertyCount<T>(BindingFlags.Instance | BindingFlags.Public);

    public static int GetPropertyCount<T>(BindingFlags flags)
    {
        PropertyInfo[] pinfos = typeof(T).GetProperties(flags);
        return pinfos.Length;
    }
}
