// using System.Linq.Expressions;
// using System.Reflection;

namespace Task.Monitor.Cli.Utils;

public static class QueryableExtensions
{
    /* The DynamicOrderBy<TSource> method breaks .net native AoT compilation */
    /*
    public static IOrderedQueryable<TSource> DynamicOrderBy<TSource>(
        this IQueryable<TSource> source,
        string propertyName,
        bool isDescending = false)
    {
        ArgumentNullException.ThrowIfNull(source, nameof(source));

        if (string.IsNullOrWhiteSpace(propertyName)) {
            throw new ArgumentException($"{nameof(propertyName)} cannot be null or whitespace.", nameof(propertyName));
        }

        var entityType = typeof(TSource);

        PropertyInfo? propertyInfo = entityType.GetProperty(
            propertyName, 
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (propertyInfo == null) {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type '{entityType.Name}'.");
        }

        ParameterExpression parameter = Expression.Parameter(entityType, "p");
        MemberExpression propertyAccess = Expression.MakeMemberAccess(parameter, propertyInfo);
        LambdaExpression orderByExp = Expression.Lambda(propertyAccess, parameter);

        var methodName = isDescending ? "OrderByDescending" : "OrderBy";

        // Find the generic OrderBy/OrderByDescending method via reflection.
        MethodCallExpression resultExp = Expression.Call(
            typeof(Queryable), 
            methodName,
            [entityType, propertyInfo.PropertyType], 
            source.Expression, 
            Expression.Quote(orderByExp));

        return (IOrderedQueryable<TSource>)source.Provider.CreateQuery<TSource>(resultExp);
    }
    */
}
